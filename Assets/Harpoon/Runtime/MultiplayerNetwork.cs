using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Harpoon.Core;
using UnityEngine;

namespace Harpoon.Runtime
{
    [Serializable]
    public sealed class NetworkMessage
    {
        public string kind;
        public string text;
        public string action;
        public string side;
        public int column;
        public int row;
        public int soundId;
        public string snapshot;
        public GameCommandData command;
        public string commandId;
        public string violationCode;
    }

    /// <summary>One-opponent TCP connection. All callbacks are polled on Unity's main thread.</summary>
    public sealed class MultiplayerNetwork : IDisposable
    {
        private readonly ConcurrentQueue<NetworkMessage> _inbox = new ConcurrentQueue<NetworkMessage>();
        private readonly object _sendLock = new object();
        private TcpListener _listener;
        private TcpClient _client;
        private StreamWriter _writer;
        private Thread _worker;
        private volatile bool _stopping;
        private volatile string _status = "Offline";

        public bool IsHost { get; private set; }
        public bool IsConnected => _client != null && _client.Connected && _writer != null;
        public string Status => _status;

        public void StartHost(int port)
        {
            Stop();
            IsHost = true;
            _status = $"Hosting on port {port}; waiting for opponent...";
            _worker = new Thread(() => HostWorker(port)) { IsBackground = true, Name = "Harpoon Host" };
            _worker.Start();
        }

        public void StartClient(string address, int port)
        {
            Stop();
            IsHost = false;
            _status = $"Connecting to {address}:{port}...";
            _worker = new Thread(() => ClientWorker(address, port)) { IsBackground = true, Name = "Harpoon Client" };
            _worker.Start();
        }

        public bool TryReceive(out NetworkMessage message) => _inbox.TryDequeue(out message);

        public void Send(NetworkMessage message)
        {
            var json = JsonUtility.ToJson(message);
            lock (_sendLock)
            {
                if (_writer == null) return;
                try
                {
                    _writer.WriteLine(json);
                    _writer.Flush();
                }
                catch (Exception exception)
                {
                    _status = "Send failed: " + exception.Message;
                }
            }
        }

        private void HostWorker(int port)
        {
            try
            {
                _listener = new TcpListener(IPAddress.Any, port);
                _listener.Start(1);
                _client = _listener.AcceptTcpClient();
                if (_stopping) return;
                _status = "Opponent connected";
                ReadLoop();
            }
            catch (Exception exception)
            {
                if (!_stopping) _status = "Host error: " + exception.Message;
            }
        }

        private void ClientWorker(string address, int port)
        {
            try
            {
                _client = new TcpClient();
                _client.Connect(address, port);
                if (_stopping) return;
                _status = "Connected to host";
                ReadLoop();
            }
            catch (Exception exception)
            {
                if (!_stopping) _status = "Connection error: " + exception.Message;
            }
        }

        private void ReadLoop()
        {
            using (var stream = _client.GetStream())
            using (var reader = new StreamReader(stream, Encoding.UTF8, false, 4096, true))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, true))
            {
                writer.AutoFlush = true;
                lock (_sendLock) _writer = writer;
                while (!_stopping)
                {
                    var line = reader.ReadLine();
                    if (line == null) break;
                    var message = JsonUtility.FromJson<NetworkMessage>(line);
                    if (message != null) _inbox.Enqueue(message);
                }
                lock (_sendLock) _writer = null;
            }
            if (!_stopping) _status = "Opponent disconnected";
        }

        public void Stop()
        {
            _stopping = true;
            lock (_sendLock) _writer = null;
            try { _client?.Close(); } catch { }
            try { _listener?.Stop(); } catch { }
            _client = null;
            _listener = null;
            while (_inbox.TryDequeue(out _)) { }
            _stopping = false;
            _status = "Offline";
        }

        public void Dispose() => Stop();
    }
}
