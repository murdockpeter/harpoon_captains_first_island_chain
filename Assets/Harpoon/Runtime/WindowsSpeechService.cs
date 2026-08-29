using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Harpoon.Runtime
{
    /// <summary>
    /// Talks to the bundled Windows-local speech companion over redirected standard streams.
    /// The companion uses installed Windows voices and recognizer; no audio or text leaves the PC.
    /// </summary>
    public sealed class WindowsSpeechService : IDisposable
    {
        private readonly ConcurrentQueue<RecognizedSpeech> _recognized = new ConcurrentQueue<RecognizedSpeech>();
        private readonly object _writeLock = new object();
        private Process _process;
        private bool _recognitionConfigured;

        public bool SpeechAvailable => _process != null && !_process.HasExited;
        public bool RecognitionAvailable => SpeechAvailable && _recognitionConfigured;
        public bool IsListening { get; private set; }
        public string LastError { get; private set; } = string.Empty;

        public void Initialize(IEnumerable<string> commandPhrases)
        {
            Dispose();
            if (Application.platform != RuntimePlatform.WindowsPlayer &&
                Application.platform != RuntimePlatform.WindowsEditor)
            {
                LastError = "Windows speech services are unavailable on this platform.";
                return;
            }
            try
            {
                var helper = FindHelper();
                if (helper == null)
                {
                    LastError = "HarpoonAccessibilitySpeech.exe was not found. Build the Windows player first.";
                    return;
                }
                _process = new Process
                {
                    StartInfo = new ProcessStartInfo(helper)
                    {
                        UseShellExecute = false,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        WorkingDirectory = Path.GetDirectoryName(helper)
                    },
                    EnableRaisingEvents = true
                };
                _process.OutputDataReceived += OnOutput;
                _process.ErrorDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrWhiteSpace(args.Data)) LastError = args.Data;
                };
                _process.Exited += (sender, args) => IsListening = false;
                _process.Start();
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();
                var phrases = commandPhrases.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct().ToArray();
                _recognitionConfigured = phrases.Length > 0;
                if (_recognitionConfigured) Send("GRAMMAR\t" + Encode(string.Join("\n", phrases)));
                LastError = string.Empty;
            }
            catch (Exception exception)
            {
                LastError = RootMessage(exception);
                Dispose();
            }
        }

        public void SetVoice(int rate, int volume)
        {
            if (!SpeechAvailable) return;
            Send($"VOICE\t{Math.Max(-10, Math.Min(10, rate))},{Math.Max(0, Math.Min(100, volume))}");
        }

        public void Speak(string text, bool interrupt = true)
        {
            if (!SpeechAvailable || string.IsNullOrWhiteSpace(text)) return;
            if (interrupt) Send("CANCEL");
            Send("SPEAK\t" + Encode(text));
        }

        public bool StartListening()
        {
            if (!RecognitionAvailable) return false;
            Send("LISTEN");
            IsListening = true;
            return true;
        }

        public void StopListening()
        {
            if (!SpeechAvailable || !IsListening) return;
            Send("STOP");
            IsListening = false;
        }

        public bool TryGetRecognized(out RecognizedSpeech speech) => _recognized.TryDequeue(out speech);

        private void OnOutput(object sender, DataReceivedEventArgs args)
        {
            if (string.IsNullOrWhiteSpace(args.Data)) return;
            var parts = args.Data.Split('\t');
            if (parts[0] == "HEARD" && parts.Length >= 3 &&
                float.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var confidence))
                _recognized.Enqueue(new RecognizedSpeech(Decode(parts[2]), confidence));
            else if (parts[0] == "ERROR" && parts.Length >= 2) LastError = Decode(parts[1]);
            else if (parts[0] == "LISTENING" && parts.Length >= 2) IsListening = parts[1] == "1";
        }

        private void Send(string command)
        {
            try
            {
                lock (_writeLock)
                {
                    if (!SpeechAvailable) return;
                    _process.StandardInput.WriteLine(command);
                    _process.StandardInput.Flush();
                }
            }
            catch (Exception exception) { LastError = RootMessage(exception); }
        }

        private static string FindHelper()
        {
            var candidates = new[]
            {
                Path.GetFullPath(Path.Combine(Application.dataPath, "..", "HarpoonAccessibilitySpeech.exe")),
                Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Build", "Windows", "HarpoonAccessibilitySpeech.exe"))
            };
            return candidates.FirstOrDefault(File.Exists);
        }

        private static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
        private static string Decode(string value) => Encoding.UTF8.GetString(Convert.FromBase64String(value));
        private static string RootMessage(Exception exception)
        {
            while (exception.InnerException != null) exception = exception.InnerException;
            return exception.Message;
        }

        public void Dispose()
        {
            try
            {
                if (_process != null && !_process.HasExited)
                {
                    Send("QUIT");
                    if (!_process.WaitForExit(750)) _process.Kill();
                }
            }
            catch { /* Best effort during player shutdown. */ }
            if (_process != null)
            {
                _process.Dispose();
                _process = null;
            }
            _recognitionConfigured = false;
            IsListening = false;
            while (_recognized.TryDequeue(out _)) { }
        }
    }

    public readonly struct RecognizedSpeech
    {
        public readonly string Text;
        public readonly float Confidence;
        public RecognizedSpeech(string text, float confidence) { Text = text; Confidence = confidence; }
    }
}
