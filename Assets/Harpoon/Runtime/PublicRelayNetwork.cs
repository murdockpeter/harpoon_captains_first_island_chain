using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace Harpoon.Runtime
{
    public sealed class PublicSessionListing
    {
        public string Id;
        public string Name;
        public int AvailableSlots;
        public bool HasPassword;
    }

    /// <summary>Unity Relay/DTLS public-session transport for the existing rules protocol.</summary>
    public sealed class PublicRelayNetwork : IDisposable
    {
        private const string MessageName = "harpoon.rules.v1";
        private const string LastSessionKey = "Harpoon.LastRelaySession";
        private readonly ConcurrentQueue<NetworkMessage> _inbox = new ConcurrentQueue<NetworkMessage>();
        private NetworkManager _manager;
        private ISession _session;
        private string _status = "Public service offline";
        private bool _operationPending;
        private bool _notificationsChecked;

        public bool IsHost { get; private set; }
        public bool IsConnected => _manager != null &&
            (IsHost ? _manager.IsHost && _manager.ConnectedClientsIds.Count > 1 : _manager.IsConnectedClient);
        public string Status => _status;
        public string JoinCode { get; private set; } = string.Empty;
        public IReadOnlyList<PublicSessionListing> Listings { get; private set; } = Array.Empty<PublicSessionListing>();
        public IReadOnlyList<string> ServiceNotifications { get; private set; } = Array.Empty<string>();

        public async void Host(string sessionName, string password, bool discoverable)
        {
            if (_operationPending) return;
            _operationPending = true;
            _status = "Signing in to Unity services...";
            try
            {
                await EnsureServicesAndNetworkManager();
                IsHost = true;
                _status = "Creating encrypted Relay session...";
                var options = new SessionOptions
                {
                    Name = string.IsNullOrWhiteSpace(sessionName) ? "Harpoon Scenario 1" : sessionName.Trim(),
                    MaxPlayers = 2,
                    IsPrivate = !discoverable,
                    Password = NormalizePassword(password)
                }.WithRelayNetwork();
                _session = await MultiplayerService.Instance.CreateSessionAsync(options);
                JoinCode = _session.Code;
                PlayerPrefs.SetString(LastSessionKey, _session.Id);
                PlayerPrefs.Save();
                RegisterMessageHandler();
                _status = $"Public Relay ready - join code {JoinCode}";
            }
            catch (Exception exception)
            {
                _status = FriendlyFailure(exception);
            }
            finally { _operationPending = false; }
        }

        public async void JoinByCode(string joinCode, string password)
        {
            if (_operationPending) return;
            _operationPending = true;
            _status = "Joining encrypted Relay session...";
            try
            {
                await EnsureServicesAndNetworkManager();
                IsHost = false;
                _session = await MultiplayerService.Instance.JoinSessionByCodeAsync(joinCode.Trim().ToUpperInvariant(),
                    new JoinSessionOptions { Password = NormalizePassword(password) });
                JoinCode = joinCode.Trim().ToUpperInvariant();
                PlayerPrefs.SetString(LastSessionKey, _session.Id);
                PlayerPrefs.Save();
                RegisterMessageHandler();
                _status = "Connected through encrypted Unity Relay";
            }
            catch (Exception exception) { _status = FriendlyFailure(exception); }
            finally { _operationPending = false; }
        }

        public async void JoinById(string sessionId, string password)
        {
            if (_operationPending) return;
            _operationPending = true;
            _status = "Joining selected public session...";
            try
            {
                await EnsureServicesAndNetworkManager();
                IsHost = false;
                _session = await MultiplayerService.Instance.JoinSessionByIdAsync(sessionId,
                    new JoinSessionOptions { Password = NormalizePassword(password) });
                PlayerPrefs.SetString(LastSessionKey, _session.Id);
                PlayerPrefs.Save();
                RegisterMessageHandler();
                _status = "Connected through encrypted Unity Relay";
            }
            catch (Exception exception) { _status = FriendlyFailure(exception); }
            finally { _operationPending = false; }
        }

        public async void RefreshListings()
        {
            if (_operationPending) return;
            _operationPending = true;
            _status = "Finding public Harpoon sessions...";
            try
            {
                await EnsureServicesAndNetworkManager(false);
                var results = await MultiplayerService.Instance.QuerySessionsAsync(new QuerySessionsOptions { Count = 20 });
                Listings = results.Sessions.Select(item => new PublicSessionListing
                {
                    Id = item.Id,
                    Name = item.Name,
                    AvailableSlots = item.AvailableSlots,
                    HasPassword = item.HasPassword
                }).ToArray();
                _status = Listings.Count == 0 ? "No public sessions found" : $"Found {Listings.Count} public session(s)";
            }
            catch (Exception exception) { _status = FriendlyFailure(exception); }
            finally { _operationPending = false; }
        }

        public async void Reconnect()
        {
            if (_operationPending) return;
            var sessionId = PlayerPrefs.GetString(LastSessionKey, string.Empty);
            if (sessionId.Length == 0) { _status = "No previous Relay session is saved."; return; }
            _operationPending = true;
            _status = "Reconnecting to previous session...";
            try
            {
                await EnsureServicesAndNetworkManager();
                _session = await MultiplayerService.Instance.ReconnectToSessionAsync(sessionId);
                IsHost = _session.Host == AuthenticationService.Instance.PlayerId;
                RegisterMessageHandler();
                _status = "Relay session reconnected";
            }
            catch (Exception exception) { _status = FriendlyFailure(exception); }
            finally { _operationPending = false; }
        }

        public bool TryReceive(out NetworkMessage message) => _inbox.TryDequeue(out message);

        public void Send(NetworkMessage message)
        {
            if (_manager == null || !_manager.IsListening) return;
            var json = JsonUtility.ToJson(message);
            var size = Encoding.UTF8.GetByteCount(json) + 16;
            using (var writer = new FastBufferWriter(size, Allocator.Temp, Math.Max(size, 65536)))
            {
                writer.WriteValueSafe(json);
                if (IsHost)
                {
                    foreach (var clientId in _manager.ConnectedClientsIds.Where(id => id != _manager.LocalClientId))
                        _manager.CustomMessagingManager.SendNamedMessage(MessageName, clientId, writer,
                            NetworkDelivery.ReliableFragmentedSequenced);
                }
                else
                    _manager.CustomMessagingManager.SendNamedMessage(MessageName, NetworkManager.ServerClientId,
                        writer, NetworkDelivery.ReliableFragmentedSequenced);
            }
        }

        private async Task EnsureServicesAndNetworkManager(bool requireNetworkManager = true)
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            if (!_notificationsChecked)
            {
                _notificationsChecked = true;
                try
                {
                    var notifications = await AuthenticationService.Instance.GetNotificationsAsync();
                    ServiceNotifications = notifications.Select(item => item.Message).Where(item => !string.IsNullOrWhiteSpace(item)).ToArray();
                }
                catch { ServiceNotifications = Array.Empty<string>(); }
            }
            if (!requireNetworkManager || _manager != null) return;
            var networkObject = new GameObject("Harpoon Public Relay");
            UnityEngine.Object.DontDestroyOnLoad(networkObject);
            var transport = networkObject.AddComponent<UnityTransport>();
            _manager = networkObject.AddComponent<NetworkManager>();
            _manager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                EnableSceneManagement = false,
                ConnectionApproval = false
            };
            _manager.OnClientDisconnectCallback += OnClientDisconnected;
        }

        private void RegisterMessageHandler()
        {
            _manager.CustomMessagingManager.UnregisterNamedMessageHandler(MessageName);
            _manager.CustomMessagingManager.RegisterNamedMessageHandler(MessageName, OnMessage);
        }

        private void OnMessage(ulong sender, FastBufferReader reader)
        {
            reader.ReadValueSafe(out string json);
            var message = JsonUtility.FromJson<NetworkMessage>(json);
            if (message != null) _inbox.Enqueue(message);
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (_manager != null && clientId == _manager.LocalClientId)
                _status = "Relay disconnected - Reconnect is available";
            else if (IsHost)
                _status = "Opponent disconnected - awaiting reconnect";
        }

        public async void Stop()
        {
            try
            {
                if (_session != null) await _session.LeaveAsync();
            }
            catch { }
            _session = null;
            JoinCode = string.Empty;
            if (_manager != null)
            {
                if (_manager.CustomMessagingManager != null)
                    _manager.CustomMessagingManager.UnregisterNamedMessageHandler(MessageName);
                UnityEngine.Object.Destroy(_manager.gameObject);
                _manager = null;
            }
            while (_inbox.TryDequeue(out _)) { }
            _status = "Public service offline";
        }

        private static string NormalizePassword(string password) =>
            string.IsNullOrWhiteSpace(password) ? null : password.Trim();

        private static string FriendlyFailure(Exception exception)
        {
            var message = exception.Message;
            if (message.IndexOf("project", StringComparison.OrdinalIgnoreCase) >= 0 ||
                message.IndexOf("environment", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Relay setup required: link this Unity project to a Cloud project in the Editor.";
            return "Public service error: " + message;
        }

        public void Dispose() => Stop();
    }
}
