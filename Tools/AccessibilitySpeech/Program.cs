using System;
using System.Globalization;
using System.Linq;
using System.Speech.Recognition;
using System.Speech.Synthesis;
using System.Text;

internal static class Program
{
    private static readonly object OutputLock = new object();
    private static readonly SpeechSynthesizer Synthesizer = new SpeechSynthesizer();
    private static SpeechRecognitionEngine _recognizer;

    private static void Main()
    {
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = Encoding.UTF8;
        Write("READY\t1");
        string line;
        while ((line = Console.ReadLine()) != null)
        {
            try
            {
                var split = line.Split(new[] { '\t' }, 2);
                var command = split[0];
                var payload = split.Length > 1 ? split[1] : string.Empty;
                if (command == "SPEAK") Synthesizer.SpeakAsync(Decode(payload));
                else if (command == "CANCEL") Synthesizer.SpeakAsyncCancelAll();
                else if (command == "VOICE") SetVoice(payload);
                else if (command == "GRAMMAR") ConfigureRecognition(Decode(payload));
                else if (command == "LISTEN") StartRecognition();
                else if (command == "STOP") StopRecognition();
                else if (command == "QUIT") break;
            }
            catch (Exception exception) { Write("ERROR\t" + Encode(RootMessage(exception))); }
        }
        StopRecognition();
        if (_recognizer != null) _recognizer.Dispose();
        Synthesizer.Dispose();
    }

    private static void SetVoice(string payload)
    {
        var values = payload.Split(',');
        Synthesizer.Rate = Math.Max(-10, Math.Min(10, int.Parse(values[0], CultureInfo.InvariantCulture)));
        Synthesizer.Volume = Math.Max(0, Math.Min(100, int.Parse(values[1], CultureInfo.InvariantCulture)));
    }

    private static void ConfigureRecognition(string payload)
    {
        StopRecognition();
        if (_recognizer != null) _recognizer.Dispose();
        var phrases = payload.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).Distinct().ToArray();
        if (phrases.Length == 0) { _recognizer = null; return; }
        _recognizer = new SpeechRecognitionEngine();
        var builder = new GrammarBuilder(new Choices(phrases));
        _recognizer.LoadGrammar(new Grammar(builder));
        _recognizer.SetInputToDefaultAudioDevice();
        _recognizer.SpeechRecognized += (sender, args) =>
            Write("HEARD\t" + args.Result.Confidence.ToString(CultureInfo.InvariantCulture) + "\t" + Encode(args.Result.Text));
        Write("RECOGNITION\tREADY");
    }

    private static void StartRecognition()
    {
        if (_recognizer == null) throw new InvalidOperationException("Speech recognition grammar is not configured.");
        Synthesizer.SpeakAsyncCancelAll();
        _recognizer.RecognizeAsync(RecognizeMode.Multiple);
        Write("LISTENING\t1");
    }

    private static void StopRecognition()
    {
        if (_recognizer == null) return;
        try { _recognizer.RecognizeAsyncCancel(); }
        catch (InvalidOperationException) { }
        Write("LISTENING\t0");
    }

    private static void Write(string value)
    {
        lock (OutputLock) { Console.WriteLine(value); Console.Out.Flush(); }
    }

    private static string Encode(string value)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
    }

    private static string Decode(string value)
    {
        return Encoding.UTF8.GetString(Convert.FromBase64String(value));
    }
    private static string RootMessage(Exception exception)
    {
        while (exception.InnerException != null) exception = exception.InnerException;
        return exception.Message;
    }
}
