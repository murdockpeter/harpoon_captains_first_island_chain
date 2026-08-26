using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Harpoon.Core;
using UnityEngine;

namespace Harpoon.Runtime
{
    public sealed class HarpoonGameBootstrap : MonoBehaviour
    {
        private enum SessionMode { SinglePlayer, HotSeat, Host, Client, PublicHost, PublicClient }
        private const float HexRadius = 1.12f;
        private readonly Dictionary<HexCoord, HexTileView> _tiles = new Dictionary<HexCoord, HexTileView>();
        private ScenarioOneGame _game;
        private Transform _playerMarker;
        private Transform _enemyMarker;
        private readonly Dictionary<string, Transform> _formationMarkers = new Dictionary<string, Transform>();
        private readonly Dictionary<string, int> _formationMarkerShipCounts = new Dictionary<string, int>();
        private LineRenderer _movementPathPreview;
        private GUIStyle _titleStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _cardHeaderStyle;
        private GUIStyle _cardStatStyle;
        private GUIStyle _tooltipStyle;
        private GUIStyle _debugStyle;
        private GUIStyle _debugHeaderStyle;
        private GUIStyle _activationStyle;
        private GUIStyle _sectionHeaderStyle;
        private Side _selectedFormation = Side.UsNavy;
        private string _selectedFormationId = "US Task Force";
        private HexCoord? _hoveredHex;
        private bool _showDebug;
        private bool _detectionTestMode;
        private Vector2 _debugScroll;
        private Vector2 _commandPanelScroll;
        private Vector2 _formationPanelScroll;
        private readonly Dictionary<string, int[]> _missileDraft = new Dictionary<string, int[]>();
        private readonly List<DefensePairData> _defensePairDraft = new List<DefensePairData>();
        private readonly Dictionary<string, int> _longRangeRemovalDraft = new Dictionary<string, int>();
        private readonly Dictionary<string, string> _shortRangeDefenseDraft = new Dictionary<string, string>();
        private readonly List<GunPairData> _gunPairDraft = new List<GunPairData>();
        private string _pairSelection = string.Empty;
        private string _combatDraftMarker = string.Empty;
        private int _lastDebugCount;
        private readonly MultiplayerNetwork _network = new MultiplayerNetwork();
        private readonly PublicRelayNetwork _publicNetwork = new PublicRelayNetwork();
        private readonly List<string> _chat = new List<string>();
        private readonly Dictionary<string, GameCommand> _pendingCommands = new Dictionary<string, GameCommand>();
        private SessionMode _sessionMode;
        private bool _showMultiplayer;
        private bool _wasConnected;
        private string _lastNetworkStatus = "Offline";
        private string _ipAddress = "127.0.0.1";
        private string _portText = "7777";
        private string _chatInput = string.Empty;
        private string _publicSessionName = "Harpoon Scenario 1";
        private string _publicPassword = string.Empty;
        private string _joinCode = string.Empty;
        private bool _publicDiscoverable = true;
        private Side _hostSideChoice = Side.UsNavy;
        private Side _localSide = Side.UsNavy;
        private bool _muteOpponent;
        private float _lastChatSentAt = -10f;
        private float _lastSoundSentAt = -10f;
        private Vector2 _chatScroll;
        private Vector2 _lobbyScroll;
        private AudioSource _soundboardSource;
        private AudioClip[] _soundboardClips;
        private AudioSource _gameAudioSource;
        private AudioClip _moveClip;
        private AudioClip _attackClip;
        private AudioClip _impactClip;
        private AudioClip _actionClip;
        private AudioClip _rejectClip;
        private AudioClip _chitClip;
        private AudioClip _gunClip;
        private AudioClip _sinkingClip;
        private float _chitBannerUntil;
        private string _chitBanner = string.Empty;
        private object _speechVoice;
        private static readonly string[] SoundboardNames =
        {
            "You sank my battleship!", "Incoming!", "All hands brace!", "Good hunting!"
        };
        private string _status = "Draw the first movement chit to begin the turn.";
        private int _matchSeed = 2026;
        private string _seedText = "2026";
        private string _exportStatus = string.Empty;
        private bool _isPaused;
        private bool _showBriefing = true;
        private bool _confirmRestart;
        private bool _confirmExit;
        private string _saveStatus = string.Empty;
        private bool _checkingForUpdate;
        private bool _downloadingUpdate;
        private float _updateProgress;
        private string _updateStatus = "Updates have not been checked.";
        private UpdateCheckResult _availableUpdate;
        private bool _showObjectiveSection = true;
        private bool _showOrdersSection = true;
        private bool _showRosterSection;
        private bool _showSystemSection;
        private bool _showEventSection;
        private ScenarioDefinition _selectedScenario = FirstIslandChainScenarios.ContactOffBashiChannel;
        private bool _placingPlanDeployment;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureBootstrap()
        {
            if (FindFirstObjectByType<HarpoonGameBootstrap>() == null)
                new GameObject("Harpoon Game").AddComponent<HarpoonGameBootstrap>();
        }

        private void Awake()
        {
            if (!Application.isEditor) EnterBorderlessFullscreen();
            Application.targetFrameRate = 60;
            QualitySettings.shadowDistance = 80f;
            BuildLightingAndCamera();
            BuildBoard();
            BuildTaskForceMarkers();
            BuildSoundboard();
            BuildGameAudio();
            Restart();
            if (!Application.isEditor) StartCoroutine(CheckForUpdates());
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_showDebug) _showDebug = false;
                else if (_showBriefing) _showBriefing = false;
                else if (_confirmRestart || _confirmExit) { _confirmRestart = false; _confirmExit = false; }
                else SetPaused(!_isPaused);
                return;
            }
            if (Input.GetKeyDown(KeyCode.P)) SetPaused(!_isPaused);
            if (Input.GetKeyDown(KeyCode.F1)) _showBriefing = !_showBriefing;
            if (Input.GetKeyDown(KeyCode.F3)) _showDebug = !_showDebug;
            if (Input.GetKeyDown(KeyCode.F11)) ToggleFullscreen();
            ProcessNetwork();
            if (_isPaused || _showBriefing || _confirmRestart || _confirmExit) return;
            UpdateHoveredHex();
            if (!Input.GetMouseButtonDown(0) || IsPointerOverPanel()) { HighlightMovement(); return; }
            var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, 200f))
            {
                var formation = hit.collider.GetComponentInParent<FormationView>();
                if (formation != null)
                {
                    SelectFormation(formation.Side, formation.FormationId);
                    HighlightMovement();
                    return;
                }
                var tile = hit.collider.GetComponent<HexTileView>();
                if (tile != null)
                {
                    if (_placingPlanDeployment) DeployPlanFormation(tile.Coordinate);
                    else TryLocalMove(tile.Coordinate);
                    RefreshViews();
                }
            }
            HighlightMovement();
        }

        private void Restart()
        {
            SetPaused(false);
            _confirmRestart = false;
            _confirmExit = false;
            if (!int.TryParse(_seedText, out _matchSeed) || _matchSeed == 0)
            {
                _matchSeed = 2026;
                _seedText = _matchSeed.ToString();
            }
            _pendingCommands.Clear();
            _game = new ScenarioOneGame(_matchSeed, null, _sessionMode != SessionMode.SinglePlayer,
                _detectionTestMode, null, _selectedScenario);
            _game.AttackResolved += OnAttackResolved;
            _game.CommandProcessed += OnCommandProcessed;
            _selectedFormation = LocalSide;
            _selectedFormationId = _game.State.Forces.First(force => force.Side == LocalSide).Id;
            _status = "Draw the first movement chit to begin the turn.";
            _debugScroll = Vector2.zero;
            _commandPanelScroll = Vector2.zero;
            _formationPanelScroll = Vector2.zero;
            ResetCombatDrafts();
            _lastDebugCount = 0;
            RefreshViews();
            if (IsHostSession && NetworkConnected) BroadcastSnapshot();
        }

        private bool IsPublicSession => _sessionMode == SessionMode.PublicHost || _sessionMode == SessionMode.PublicClient;
        private bool IsHostSession => _sessionMode == SessionMode.Host || _sessionMode == SessionMode.PublicHost;
        private bool IsClientSession => _sessionMode == SessionMode.Client || _sessionMode == SessionMode.PublicClient;
        private Side LocalSide => _sessionMode == SessionMode.HotSeat ? (_game?.State.ActiveSide ?? Side.UsNavy) :
            _sessionMode == SessionMode.SinglePlayer ? Side.UsNavy : _localSide;
        private static Side OpposingSide(Side side) => side == Side.UsNavy ? Side.Plan : Side.UsNavy;
        private bool NetworkConnected => IsPublicSession ? _publicNetwork.IsConnected : _network.IsConnected;
        private string NetworkStatus => IsPublicSession ? _publicNetwork.Status : _network.Status;

        private void NetworkSend(NetworkMessage message)
        {
            if (IsPublicSession) _publicNetwork.Send(message);
            else _network.Send(message);
        }

        private bool NetworkTryReceive(out NetworkMessage message) =>
            IsPublicSession ? _publicNetwork.TryReceive(out message) : _network.TryReceive(out message);

        private void TryLocalMove(HexCoord destination)
        {
            if (IsClientSession)
            {
                SendCommand(GameCommandType.Move, destination);
                _status = $"Move to {destination} sent to host.";
                return;
            }
            var result = _game.Execute(new GameCommand(GameCommandType.Move, LocalSide,
                _game.State.Revision, destination));
            if (result.Accepted)
            {
                _status = $"Moved to {destination}. Attack or end activation.";
                if (IsHostSession) BroadcastSnapshot();
            }
            else _status = result.Summary;
        }

        private void DeclareLocalSpeed(int speed)
        {
            if (IsClientSession)
            {
                SendCommand(GameCommandType.DeclareSpeed, declaredSpeed: speed);
                _status = $"Speed {speed} declaration sent to host.";
                return;
            }
            var result = _game.Execute(new GameCommand(GameCommandType.DeclareSpeed, LocalSide,
                _game.State.Revision, declaredSpeed: speed));
            _status = result.Accepted
                ? speed == 0 ? "Holding position. Attack, search, or end activation."
                    : $"Speed {speed} declared. Enter one highlighted adjacent hex at a time."
                : result.Summary;
            RefreshViews();
            if (IsHostSession) BroadcastSnapshot();
        }

        private void DrawLocalMovementChit()
        {
            if (IsClientSession)
            {
                SendCommand(GameCommandType.DrawMovementChit);
                _status = "Movement-chit draw requested from host.";
                return;
            }
            var result = _game.Execute(new GameCommand(GameCommandType.DrawMovementChit,
                LocalSide, _game.State.Revision));
            _status = result.Accepted
                ? _game.State.ActiveSide == LocalSide ? "Your formation chit was drawn. Declare speed."
                    : "Opponent formation chit drawn."
                : result.Summary;
            RefreshViews();
            if (IsHostSession) BroadcastSnapshot();
        }

        private void SplitLocalFormation(TaskForceState source, UnitState unit)
        {
            var number = _game.State.Forces.Count(force => force.Side == LocalSide) + 1;
            var newId = $"{SideLabel(LocalSide)} Task Force {number}";
            var command = new GameCommand(GameCommandType.SplitTaskForce, LocalSide,
                _game.State.Revision, formationId: source.Id, newFormationId: newId,
                unitIds: new[] { unit.Definition.Id });
            if (IsClientSession)
            {
                _pendingCommands[command.Id] = command;
                NetworkSend(new NetworkMessage { kind = "command", command = command.ToData() });
                _status = $"Split request for {unit.Definition.DisplayName} sent to host.";
                return;
            }
            var result = _game.Execute(command);
            _status = result.Accepted ? $"Formed {newId}; its chit was added to the cup." : result.Summary;
            if (result.Accepted) SelectFormation(LocalSide, newId);
            RefreshViews();
            if (IsHostSession) BroadcastSnapshot();
        }

        private void DeployPlanFormation(HexCoord destination)
        {
            var command = new GameCommand(GameCommandType.DeployFormation, Side.Plan,
                _game.State.Revision, destination, formationId: "PLAN Picket Group");
            if (IsClientSession)
            {
                _pendingCommands[command.Id] = command;
                NetworkSend(new NetworkMessage { kind = "command", command = command.ToData() });
                _status = "PLAN deployment request sent to host.";
            }
            else
            {
                var result = _game.Execute(command);
                _status = result.Accepted ? "PLAN picket deployed. Its position remains hidden from the US player."
                    : result.Summary;
                if (result.Accepted && IsHostSession) BroadcastSnapshot();
            }
            _placingPlanDeployment = false;
        }

        private void DeclareLocalRadar(bool enabled)
        {
            var command = new GameCommand(GameCommandType.RadiateRadar, LocalSide,
                _game.State.Revision, enabled: enabled, formationId: _game.State.ActiveFormationId);
            if (IsClientSession)
            {
                _pendingCommands[command.Id] = command;
                NetworkSend(new NetworkMessage { kind = "command", command = command.ToData() });
                _status = $"Radar {(enabled ? "radiate" : "silent")} declaration sent to host.";
                return;
            }
            var result = _game.Execute(command);
            _status = result.Accepted ? $"Surface-search radar {(enabled ? "radiating" : "silent")}." : result.Summary;
            RefreshViews();
            if (IsHostSession) BroadcastSnapshot();
        }

        private void SearchLocal(string mode, TaskForceState target)
        {
            if (target == null) return;
            var command = new GameCommand(GameCommandType.Search, LocalSide, _game.State.Revision,
                targetId: target.Id, formationId: _game.State.ActiveFormationId, searchMode: mode);
            if (IsClientSession)
            {
                _pendingCommands[command.Id] = command;
                NetworkSend(new NetworkMessage { kind = "command", command = command.ToData() });
                _status = $"{mode.ToUpperInvariant()} search sent to host.";
                return;
            }
            var result = _game.Execute(command);
            var contact = _game.State.Detection.ContactFor(LocalSide, target.Id);
            _status = result.Accepted
                ? contact.IsDetected ? $"CONTACT: {target.Id} classified by {mode.ToUpperInvariant()}."
                    : $"No {mode.ToUpperInvariant()} contact."
                : result.Summary;
            RefreshViews();
            if (IsHostSession) BroadcastSnapshot();
        }

        private void SubmitCombatCommand(GameCommand command, string sentStatus)
        {
            if (IsClientSession)
            {
                _pendingCommands[command.Id] = command;
                NetworkSend(new NetworkMessage { kind = "command", command = command.ToData() });
                _status = sentStatus;
                return;
            }
            var result = _game.Execute(command);
            _status = result.Summary;
            RefreshViews();
            if (IsHostSession) BroadcastSnapshot();
        }

        private void ResetCombatDrafts()
        {
            _missileDraft.Clear();
            _defensePairDraft.Clear();
            _longRangeRemovalDraft.Clear();
            _shortRangeDefenseDraft.Clear();
            _gunPairDraft.Clear();
            _pairSelection = string.Empty;
            _combatDraftMarker = string.Empty;
        }

        private void StartHosting()
        {
            if (!TryReadPort(out var port)) return;
            _sessionMode = SessionMode.Host;
            _detectionTestMode = false;
            _localSide = _hostSideChoice;
            _network.StartHost(port);
            _chat.Clear();
            _chat.Add($"SYSTEM: Hosting as {SideLabel(_localSide)} on port {port}.");
            _showMultiplayer = false;
            Restart();
        }

        private void JoinHost()
        {
            if (!TryReadPort(out var port)) return;
            _sessionMode = SessionMode.Client;
            _detectionTestMode = false;
            _localSide = Side.Plan;
            _network.StartClient(_ipAddress.Trim(), port);
            _chat.Clear();
            _chat.Add($"SYSTEM: Joining {_ipAddress.Trim()}:{port}; awaiting side assignment.");
            _showMultiplayer = false;
            Restart();
        }

        private void StartPublicHost()
        {
            if (_publicPassword.Length > 0 && _publicPassword.Length < 8)
            {
                _status = "Public session passwords must be at least eight characters.";
                return;
            }
            _network.Stop();
            _sessionMode = SessionMode.PublicHost;
            _detectionTestMode = false;
            _localSide = _hostSideChoice;
            _chat.Clear();
            _chat.Add($"SYSTEM: Creating public encrypted Relay session as {SideLabel(_localSide)}.");
            _showMultiplayer = false;
            _publicNetwork.Host(_publicSessionName, _publicPassword, _publicDiscoverable);
            Restart();
        }

        private void JoinPublicByCode()
        {
            if (_joinCode.Trim().Length < 6) { _status = "Enter the host's Relay join code."; return; }
            _network.Stop();
            _sessionMode = SessionMode.PublicClient;
            _detectionTestMode = false;
            _localSide = Side.Plan;
            _chat.Clear();
            _chat.Add("SYSTEM: Joining public encrypted Relay session; awaiting side assignment.");
            _showMultiplayer = false;
            _publicNetwork.JoinByCode(_joinCode, _publicPassword);
            Restart();
        }

        private void JoinPublicListing(string sessionId)
        {
            _network.Stop();
            _sessionMode = SessionMode.PublicClient;
            _detectionTestMode = false;
            _localSide = Side.Plan;
            _chat.Clear();
            _chat.Add("SYSTEM: Joining selected public session; awaiting side assignment.");
            _publicNetwork.JoinById(sessionId, _publicPassword);
            Restart();
        }

        private bool TryReadPort(out int port)
        {
            if (int.TryParse(_portText, out port) && port >= 1024 && port <= 65535) return true;
            _status = "Enter a port from 1024 through 65535.";
            return false;
        }

        private void ReturnToSinglePlayer()
        {
            _network.Stop();
            _publicNetwork.Stop();
            _sessionMode = SessionMode.SinglePlayer;
            _wasConnected = false;
            _showMultiplayer = false;
            Restart();
        }

        private void ProcessNetwork()
        {
            if (_sessionMode == SessionMode.SinglePlayer || _sessionMode == SessionMode.HotSeat) return;
            if (IsPublicSession && _publicNetwork.IsConnected)
                _sessionMode = _publicNetwork.IsHost ? SessionMode.PublicHost : SessionMode.PublicClient;
            if (NetworkStatus != _lastNetworkStatus)
            {
                _lastNetworkStatus = NetworkStatus;
                _game.State.Trace("NETWORK", _lastNetworkStatus);
                _status = _lastNetworkStatus;
            }
            if (NetworkConnected && !_wasConnected)
            {
                _wasConnected = true;
                _chat.Add("SYSTEM: Opponent connected. Chat and soundboard are live.");
                if (IsHostSession)
                {
                    NetworkSend(new NetworkMessage
                    {
                        kind = "assignment", side = OpposingSide(LocalSide).ToString()
                    });
                    _game.State.Trace("NETWORK", $"Assigned opponent to {SideLabel(OpposingSide(LocalSide))}; host controls {SideLabel(LocalSide)}.");
                    BroadcastSnapshot();
                }
            }
            else if (!NetworkConnected && _wasConnected)
            {
                _wasConnected = false;
                _chat.Add("SYSTEM: Opponent disconnected.");
            }

            while (NetworkTryReceive(out var message))
            {
                switch (message.kind)
                {
                    case "assignment":
                        if (IsClientSession && Enum.TryParse(message.side, true, out Side assignedSide))
                        {
                            _localSide = assignedSide;
                            _selectedFormation = assignedSide;
                            _chat.Add($"SYSTEM: You command {SideLabel(assignedSide)}.");
                            _game.State.Trace("NETWORK", $"Side assignment received: {assignedSide}.");
                            RefreshViews();
                        }
                        break;
                    case "command":
                        if (IsHostSession) ExecuteRemoteCommand(message);
                        break;
                    case "snapshot":
                        if (IsClientSession)
                        {
                            var knownCommands = _game.State.CommandLog.Count;
                            var snapshot = JsonUtility.FromJson<ScenarioOneSnapshot>(message.snapshot);
                            _game.ApplySnapshot(snapshot);
                            _selectedScenario = _game.State.Scenario;
                            _matchSeed = _game.Seed;
                            _seedText = _matchSeed.ToString();
                            _selectedFormation = LocalSide;
                            _selectedFormationId = _game.State.Forces.First(force => force.Side == LocalSide).Id;
                            foreach (var observed in _game.State.CommandLog.Skip(knownCommands)
                                         .Where(item => item.actor != LocalSide))
                                PlayClientCommandFeedback(GameCommand.FromData(observed), true);
                            RefreshViews();
                            _status = _game.State.Phase == ActivationPhase.AwaitingChit
                                ? "Draw the first movement chit to begin the turn."
                                : _game.State.ActiveSide == LocalSide ? "Your activation." : "Waiting for opponent.";
                        }
                        break;
                    case "commandResult":
                        _status = message.text;
                        if (_pendingCommands.TryGetValue(message.commandId, out var pendingCommand))
                        {
                            PlayClientCommandFeedback(pendingCommand, string.IsNullOrEmpty(message.violationCode));
                            _pendingCommands.Remove(message.commandId);
                        }
                        _game.State.Trace("NETWORK", string.IsNullOrEmpty(message.violationCode)
                            ? $"Host accepted command {message.commandId}."
                            : $"Host rejected command {message.commandId}: {message.violationCode} - {message.text}");
                        break;
                    case "chat":
                        if (!_muteOpponent && !string.IsNullOrEmpty(message.text))
                            _chat.Add(message.text.Substring(0, Mathf.Min(240, message.text.Length)));
                        _chatScroll.y = float.MaxValue;
                        break;
                    case "sound":
                        if (!_muteOpponent)
                        {
                            PlaySoundboard(message.soundId);
                            _chat.Add($"SOUNDBOARD · Opponent: {SoundboardLabel(message.soundId)}");
                        }
                        _chatScroll.y = float.MaxValue;
                        break;
                }
            }
        }

        private void ExecuteRemoteCommand(NetworkMessage message)
        {
            if (message.command == null)
            {
                _game.State.Trace("NETWORK", "Rejected network message without a command payload.");
                return;
            }
            var command = GameCommand.FromData(message.command);
            var remoteSide = command.Actor;
            if (remoteSide != OpposingSide(LocalSide))
            {
                _game.State.Trace("NETWORK", $"Rejected command with invalid side claim '{remoteSide}'.");
                NetworkSend(new NetworkMessage
                {
                    kind = "commandResult",
                    commandId = command.Id,
                    text = $"This connection controls {SideLabel(OpposingSide(LocalSide))}, not {SideLabel(remoteSide)}.",
                    violationCode = RuleViolationCode.WrongSide.ToString()
                });
                BroadcastSnapshot();
                return;
            }
            var result = _game.Execute(command);
            _status = result.Summary;
            _game.State.Trace("NETWORK", $"{remoteSide} command '{command.Type}' processed; accepted={result.Accepted}.");
            NetworkSend(new NetworkMessage
            {
                kind = "commandResult",
                commandId = command.Id,
                text = result.Summary,
                violationCode = result.Violation?.Code.ToString() ?? string.Empty
            });
            RefreshViews();
            BroadcastSnapshot();
        }

        private void SendCommand(GameCommandType type, HexCoord coordinate = default, int declaredSpeed = 0,
            string targetId = null)
        {
            if (!NetworkConnected)
            {
                _status = "No opponent connection.";
                return;
            }
            var command = new GameCommand(type, LocalSide, _game.State.Revision, coordinate,
                declaredSpeed: declaredSpeed, targetId: targetId,
                formationId: _game.State.ActiveFormationId);
            _pendingCommands[command.Id] = command;
            NetworkSend(new NetworkMessage
            {
                kind = "command", command = command.ToData()
            });
            _game.State.Trace("NETWORK", $"Sent {LocalSide} command '{type}' at expected revision {_game.State.Revision}.");
        }

        private void BroadcastSnapshot()
        {
            if (!NetworkConnected || !IsHostSession) return;
            NetworkSend(new NetworkMessage
            {
                kind = "snapshot", snapshot = JsonUtility.ToJson(_game.CaptureSnapshotFor(OpposingSide(LocalSide)))
            });
        }

        private void SendChat()
        {
            var text = _chatInput.Trim();
            if (text.Length == 0 || !NetworkConnected || Time.unscaledTime - _lastChatSentAt < 0.4f) return;
            _lastChatSentAt = Time.unscaledTime;
            var line = $"{SideLabel(LocalSide)}: {text}";
            _chat.Add(line);
            NetworkSend(new NetworkMessage { kind = "chat", text = line });
            _chatInput = string.Empty;
            _chatScroll.y = float.MaxValue;
        }

        private void SendSoundboard(int soundId)
        {
            if (!NetworkConnected || Time.unscaledTime - _lastSoundSentAt < 1f) return;
            _lastSoundSentAt = Time.unscaledTime;
            PlaySoundboard(soundId);
            _chat.Add($"SOUNDBOARD · You: {SoundboardLabel(soundId)}");
            NetworkSend(new NetworkMessage { kind = "sound", soundId = soundId });
            _chatScroll.y = float.MaxValue;
        }

        private void BuildSoundboard()
        {
            _soundboardSource = gameObject.AddComponent<AudioSource>();
            _soundboardSource.spatialBlend = 0f;
            _soundboardSource.volume = 0.6f;
            try
            {
                var speechType = Type.GetTypeFromProgID("SAPI.SpVoice");
                if (speechType != null) _speechVoice = Activator.CreateInstance(speechType);
            }
            catch { _speechVoice = null; }
            _soundboardClips = new AudioClip[SoundboardNames.Length];
            for (var cue = 0; cue < _soundboardClips.Length; cue++)
            {
                const int sampleRate = 22050;
                var length = sampleRate * 2 / 3;
                var samples = new float[length];
                for (var index = 0; index < length; index++)
                {
                    var time = index / (float)sampleRate;
                    var envelope = Mathf.Min(1f, time * 18f) * Mathf.Clamp01(1f - time / 0.68f);
                    var frequency = 220f + cue * 85f + (index > length / 2 ? 110f : 0f);
                    samples[index] = Mathf.Sin(time * frequency * Mathf.PI * 2f) * envelope * 0.28f;
                }
                var clip = AudioClip.Create("Soundboard " + cue, length, 1, sampleRate, false);
                clip.SetData(samples, 0);
                _soundboardClips[cue] = clip;
            }
        }

        private void BuildGameAudio()
        {
            _gameAudioSource = gameObject.AddComponent<AudioSource>();
            _gameAudioSource.spatialBlend = 0f;
            _gameAudioSource.volume = 0.55f;
            _moveClip = CreateProceduralClip("Movement - wake and engines", 0.48f, time =>
            {
                var envelope = Mathf.Sin(Mathf.Clamp01(time / 0.48f) * Mathf.PI);
                var engine = Mathf.Sin(time * Mathf.PI * 2f * 72f) * 0.18f +
                             Mathf.Sin(time * Mathf.PI * 2f * 37f) * 0.09f;
                var water = Mathf.Sin(time * 1733f) * Mathf.Sin(time * 927f) * 0.07f;
                return (engine + water) * envelope;
            });
            _attackClip = CreateProceduralClip("Attack - missile launch", 0.52f, time =>
            {
                var normalized = time / 0.52f;
                var frequency = 180f + normalized * 780f;
                var envelope = Mathf.Clamp01(time * 25f) * (1f - normalized);
                return (Mathf.Sin(time * frequency * Mathf.PI * 2f) * 0.28f +
                        Mathf.Sin(time * 1319f) * 0.09f) * envelope;
            });
            _impactClip = CreateProceduralClip("Attack - impact", 0.62f, time =>
            {
                var envelope = Mathf.Exp(-time * 6f);
                var noise = Mathf.Sin(time * 9173f) * Mathf.Sin(time * 3137f);
                return (noise * 0.38f + Mathf.Sin(time * Mathf.PI * 2f * 54f) * 0.32f) * envelope;
            });
            _actionClip = CreateProceduralClip("Action - command ping", 0.38f, time =>
                Mathf.Sin(time * Mathf.PI * 2f * 620f) * Mathf.Exp(-time * 9f) * 0.32f);
            _rejectClip = CreateProceduralClip("Action - rejected", 0.26f, time =>
            {
                var frequency = time < 0.13f ? 190f : 125f;
                return Mathf.Sin(time * Mathf.PI * 2f * frequency) * 0.22f;
            });
            _chitClip = CreateProceduralClip("Movement chit draw", 0.56f, time =>
            {
                var click = Mathf.Sin(time * Mathf.PI * 2f * (210f + time * 520f));
                var rattle = Mathf.Sin(time * 4733f) * Mathf.Sin(time * 1901f);
                return (click * 0.24f + rattle * 0.08f) * Mathf.Exp(-time * 4f);
            });
            _gunClip = CreateProceduralClip("Naval gunfire", 0.72f, time =>
            {
                var first = Mathf.Exp(-time * 22f);
                var secondTime = Mathf.Max(0f, time - 0.24f);
                var second = time >= 0.24f ? Mathf.Exp(-secondTime * 24f) : 0f;
                var blast = Mathf.Sin(time * 11311f) * Mathf.Sin(time * 4729f);
                var concussion = Mathf.Sin(time * Mathf.PI * 2f * 46f);
                return (blast * 0.38f + concussion * 0.34f) * (first + second * 0.72f);
            });
            _sinkingClip = CreateProceduralClip("Ship sinking", 1.35f, time =>
            {
                var rumble = Mathf.Sin(time * Mathf.PI * 2f * 34f) * 0.34f +
                             Mathf.Sin(time * Mathf.PI * 2f * 51f) * 0.2f;
                var fracture = Mathf.Sin(time * 6781f) * Mathf.Sin(time * 2317f) * 0.16f;
                return (rumble + fracture) * Mathf.Exp(-time * 2.1f);
            });
        }

        private static AudioClip CreateProceduralClip(string name, float duration, Func<float, float> sample)
        {
            const int sampleRate = 22050;
            var length = Mathf.CeilToInt(duration * sampleRate);
            var samples = new float[length];
            for (var index = 0; index < length; index++)
                samples[index] = Mathf.Clamp(sample(index / (float)sampleRate), -0.9f, 0.9f);
            var clip = AudioClip.Create(name, length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void PlayGameSound(AudioClip clip, float volume = 1f)
        {
            if (_gameAudioSource != null && clip != null)
                _gameAudioSource.PlayOneShot(clip, volume);
        }

        private void PlaySoundboard(int soundId)
        {
            if (_soundboardSource == null || soundId < 0 || soundId >= _soundboardClips.Length) return;
            _soundboardSource.PlayOneShot(_soundboardClips[soundId]);
            if (_speechVoice == null) return;
            try
            {
                _speechVoice.GetType().InvokeMember("Speak", BindingFlags.InvokeMethod, null,
                    _speechVoice, new object[] { SoundboardLabel(soundId), 1 });
            }
            catch { }
        }

        private static string SoundboardLabel(int soundId) =>
            soundId >= 0 && soundId < SoundboardNames.Length ? SoundboardNames[soundId] : "Unknown cue";

        private static string SideLabel(Side side) => side == Side.UsNavy ? "US NAVY" : "PLAN";

        private void BuildLightingAndCamera()
        {
            RenderSettings.ambientLight = new Color(0.38f, 0.45f, 0.52f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.055f, 0.12f, 0.17f);
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.006f;
            var lightObject = new GameObject("Sun");
            var sun = lightObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.94f, 0.84f);
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            if (Camera.main != null) Destroy(Camera.main.gameObject);
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.018f, 0.035f, 0.06f);
            var focus = new Vector3(12.5f, 0f, 28f);
            camera.fieldOfView = 44f;
            camera.nearClipPlane = 0.15f;
            camera.farClipPlane = 180f;
            cameraObject.AddComponent<TacticalCamera>().Initialize(focus);
        }

        private void BuildBoard()
        {
            var boardRoot = new GameObject("Operational Map").transform;
            var ocean = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ocean.name = "Ocean Surface";
            ocean.transform.SetParent(boardRoot);
            ocean.transform.position = new Vector3(11f, -0.32f, 27f);
            ocean.transform.localScale = new Vector3(4.2f, 1f, 7f);
            ocean.GetComponent<Renderer>().sharedMaterial = VisualFactory.Material(new Color(0.018f, 0.12f, 0.19f), 0.15f, 0.88f);
            foreach (var coordinate in FirstIslandChainMap.Instance.AllHexes)
            {
                var terrain = FirstIslandChainMap.Instance.TerrainAt(coordinate);
                var isLand = terrain == TerrainType.Land || terrain == TerrainType.NavalBase;
                var height = isLand ? 0.28f + ((coordinate.Column * 17 + coordinate.Row * 11) % 4) * 0.1f : 0.055f;
                var tile = CreateHexTile(height);
                tile.name = $"Hex {coordinate}";
                tile.transform.SetParent(boardRoot);
                tile.transform.position = WorldPosition(coordinate);
                var renderer = tile.GetComponent<Renderer>();
                renderer.sharedMaterial = VisualFactory.Material(isLand
                    ? new Color(0.42f, 0.38f + height * 0.15f, 0.22f)
                    : new Color(0.025f, 0.25f, 0.37f), isLand ? 0.02f : 0.12f, isLand ? 0.18f : 0.75f);
                var view = tile.AddComponent<HexTileView>();
                view.Initialize(coordinate);
                _tiles.Add(coordinate, view);
            }
            BuildBaseMarkers(boardRoot);
        }

        private static void BuildBaseMarkers(Transform boardRoot)
        {
            foreach (var navalBase in FirstIslandChainMap.Instance.Bases)
            {
                var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                marker.name = navalBase.Name;
                marker.transform.SetParent(boardRoot);
                marker.transform.position = WorldPosition(navalBase.Position) + Vector3.up * 0.72f;
                marker.transform.localScale = new Vector3(0.16f, 0.42f, 0.16f);
                var color = navalBase.Side == Side.UsNavy
                    ? new Color(0.18f, 0.62f, 1f) : new Color(1f, 0.24f, 0.16f);
                marker.GetComponent<Renderer>().sharedMaterial = VisualFactory.Material(color, 0.25f, 0.55f);
                var beacon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                beacon.name = navalBase.Name + " Beacon";
                beacon.transform.SetParent(marker.transform);
                beacon.transform.localPosition = Vector3.up * 0.58f;
                beacon.transform.localScale = Vector3.one * 1.7f;
                beacon.GetComponent<Renderer>().sharedMaterial = VisualFactory.Material(color * 1.25f, 0f, 0.2f);
            }
        }

        private static GameObject CreateHexTile(float height)
        {
            var tile = new GameObject("Hex Tile");
            var vertices = new Vector3[12];
            for (var i = 0; i < 6; i++)
            {
                var angle = Mathf.Deg2Rad * (60f * i);
                var x = Mathf.Cos(angle) * (HexRadius * 0.97f);
                var z = Mathf.Sin(angle) * (HexRadius * 0.97f);
                vertices[i] = new Vector3(x, height, z);
                vertices[i + 6] = new Vector3(x, -0.08f, z);
            }

            var triangles = new List<int>(60);
            for (var i = 1; i < 5; i++)
            {
                triangles.Add(0); triangles.Add(i + 1); triangles.Add(i);
                triangles.Add(6); triangles.Add(6 + i); triangles.Add(6 + i + 1);
            }
            for (var i = 0; i < 6; i++)
            {
                var next = (i + 1) % 6;
                triangles.Add(i); triangles.Add(i + 6); triangles.Add(next);
                triangles.Add(next); triangles.Add(i + 6); triangles.Add(next + 6);
            }

            var mesh = new Mesh { name = "Operational Hex" };
            mesh.vertices = vertices;
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            tile.AddComponent<MeshFilter>().sharedMesh = mesh;
            tile.AddComponent<MeshRenderer>();
            tile.AddComponent<MeshCollider>().sharedMesh = mesh;
            return tile;
        }

        private void BuildTaskForceMarkers()
        {
            _playerMarker = VisualFactory.CreateFormation("US Task Force", new Color(0.08f, 0.46f, 0.95f), false);
            _enemyMarker = VisualFactory.CreateFormation("PLAN Task Force", new Color(0.9f, 0.12f, 0.08f), true);
            _playerMarker.gameObject.AddComponent<FormationView>().Initialize(Side.UsNavy, "US Task Force");
            _enemyMarker.gameObject.AddComponent<FormationView>().Initialize(Side.Plan, "PLAN Task Force");
            _formationMarkers["US Task Force"] = _playerMarker;
            _formationMarkers["PLAN Task Force"] = _enemyMarker;
            _formationMarkerShipCounts["US Task Force"] = 2;
            _formationMarkerShipCounts["PLAN Task Force"] = 2;
            var previewObject = new GameObject("Movement Step Preview");
            _movementPathPreview = previewObject.AddComponent<LineRenderer>();
            _movementPathPreview.useWorldSpace = true;
            _movementPathPreview.positionCount = 0;
            _movementPathPreview.startWidth = 0.14f;
            _movementPathPreview.endWidth = 0.05f;
            _movementPathPreview.sharedMaterial = VisualFactory.Material(new Color(0.15f, 0.9f, 1f), 0f, 0.18f);
        }

        private void HighlightMovement()
        {
            if (_game == null) return;
            foreach (var pair in _tiles)
            {
                var terrain = _game.State.Map.TerrainAt(pair.Key);
                var isLand = !_game.State.Map.IsNavigable(pair.Key, LocalSide);
                var hovered = _hoveredHex.HasValue && _hoveredHex.Value == pair.Key;
                var localForce = _game.State.ForceFor(LocalSide);
                var movable = CanLocalAct() && _game.State.Phase == ActivationPhase.PlayerMove && !isLand &&
                              localForce.MovementRemaining > 0 && localForce.Position.IsAdjacentTo(pair.Key);
                var deployable = _placingPlanDeployment && IsLegalPlanDeployment(pair.Key);
                var convoyDestination = _game.State.Scenario.HasUsDestination &&
                    pair.Key == _game.State.Scenario.UsDestination;
                pair.Value.GetComponent<Renderer>().material.color = isLand
                    ? hovered ? new Color(0.7f, 0.66f, 0.3f)
                    : terrain == TerrainType.NavalBase ? new Color(0.34f, 0.4f, 0.24f) : new Color(0.42f, 0.43f, 0.22f)
                    : deployable ? (hovered ? new Color(0.8f, 0.32f, 0.92f) : new Color(0.38f, 0.12f, 0.52f))
                    : convoyDestination ? (hovered ? new Color(0.3f, 1f, 0.62f) : new Color(0.05f, 0.55f, 0.34f))
                    : hovered ? new Color(0.12f, 0.72f, 0.8f)
                    : movable ? new Color(0.04f, 0.48f, 0.58f) : new Color(0.025f, 0.25f, 0.37f);
            }
            UpdateMovementPreview();
        }

        private bool IsLegalPlanDeployment(HexCoord hex)
        {
            if (_game.State.Scenario.PlanDeploymentMinimumDistance <= 0 ||
                !_game.State.Map.IsNavigable(hex, Side.Plan)) return false;
            var subic = _game.State.Map.Bases.First(item => item.Id == "us-subic").Position;
            var taipei = _game.State.Map.Bases.First(item => item.Id == "us-taipei").Position;
            return hex.DistanceTo(subic) > _game.State.Scenario.PlanDeploymentMinimumDistance &&
                   hex.DistanceTo(taipei) > _game.State.Scenario.PlanDeploymentMinimumDistance;
        }

        private void UpdateMovementPreview()
        {
            if (_movementPathPreview == null || !_hoveredHex.HasValue || !CanLocalAct() ||
                _game.State.Phase != ActivationPhase.PlayerMove)
            {
                if (_movementPathPreview != null) _movementPathPreview.positionCount = 0;
                return;
            }
            var force = _game.State.ForceFor(LocalSide);
            if (!force.Position.IsAdjacentTo(_hoveredHex.Value) ||
                !_game.State.Map.IsNavigable(_hoveredHex.Value, LocalSide))
            {
                _movementPathPreview.positionCount = 0;
                return;
            }
            _movementPathPreview.positionCount = 2;
            _movementPathPreview.SetPosition(0, WorldPosition(force.Position) + Vector3.up * 0.34f);
            _movementPathPreview.SetPosition(1, WorldPosition(_hoveredHex.Value) + Vector3.up * 0.34f);
        }

        private void UpdateHoveredHex()
        {
            _hoveredHex = null;
            if (IsPointerOverPanel() || Camera.main == null) return;
            var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            foreach (var hit in Physics.RaycastAll(ray, 200f).OrderBy(result => result.distance))
            {
                var tile = hit.collider.GetComponent<HexTileView>();
                if (tile == null) continue;
                _hoveredHex = tile.Coordinate;
                return;
            }
        }

        private void RefreshViews()
        {
            if (_game == null || _playerMarker == null) return;
            foreach (var force in _game.State.Forces)
            {
                var markerShipCount = Mathf.Clamp(force.ActiveUnits.Count(), 1, 4);
                if (_formationMarkers.TryGetValue(force.Id, out var staleMarker) &&
                    (!_formationMarkerShipCounts.TryGetValue(force.Id, out var priorCount) ||
                     priorCount != markerShipCount))
                {
                    Destroy(staleMarker.gameObject);
                    _formationMarkers.Remove(force.Id);
                }
                if (!_formationMarkers.TryGetValue(force.Id, out var marker))
                {
                    var color = force.Side == Side.UsNavy
                        ? new Color(0.08f, 0.46f, 0.95f) : new Color(0.9f, 0.12f, 0.08f);
                    var amphibious = force.Units.Any(unit => unit.Definition.DisplayName.Contains("LPD") ||
                        unit.Definition.DisplayName.Contains("Merchant"));
                    marker = VisualFactory.CreateFormation(force.Id, color, amphibious, markerShipCount);
                    marker.gameObject.AddComponent<FormationView>().Initialize(force.Side, force.Id);
                    _formationMarkers[force.Id] = marker;
                    _formationMarkerShipCounts[force.Id] = markerShipCount;
                }
                var knownContact = force.Side == LocalSide || !_game.State.DetectionRulesEnabled ||
                    _game.State.Detection.IsDetected(LocalSide, force.Id);
                marker.position = WorldPosition(force.Position) + Vector3.up * (force.IsDestroyed ? 0.38f : 0.75f);
                marker.gameObject.SetActive(knownContact);
                var selected = force.Id == _selectedFormationId;
                var active = force.Id == _game.State.ActiveFormationId;
                marker.localScale = Vector3.one * (selected ? 1.18f : active ? 1.1f : 0.92f);
                var formationView = marker.GetComponent<FormationView>();
                formationView?.SetSensorState(force.RadarRadiating, knownContact && force.Side != LocalSide);
                formationView?.SetTacticalState(IsLegalAttackTarget(force), active, selected);
                var totalHull = Mathf.Max(1, force.Units.Sum(unit => unit.Definition.Hull));
                var damageFraction = force.Units.Sum(unit => unit.HullDamage) / (float)totalHull;
                formationView?.SetDamageState(damageFraction,
                    force.ActiveUnits.Any(unit => unit.HasTwoThirdsDamage), force.IsDestroyed);
            }
            foreach (var pair in _formationMarkers)
                if (_game.State.Formation(pair.Key) == null) pair.Value.gameObject.SetActive(false);
        }

        private bool IsLegalAttackTarget(TaskForceState target)
        {
            if (target == null || target.Side == LocalSide || target.IsDestroyed || !CanLocalAct()) return false;
            if (_game.State.Phase != ActivationPhase.PlayerMove &&
                _game.State.Phase != ActivationPhase.PlayerAction) return false;
            if (_game.State.PlayerHasAttacked) return false;
            if (_game.State.DetectionRulesEnabled && !_game.State.Detection.IsDetected(LocalSide, target.Id)) return false;
            var attacker = _game.State.ForceFor(LocalSide);
            var range = attacker.Position.DistanceTo(target.Position);
            var missile = attacker.ActiveUnits.Any(unit =>
                (range <= 1 && unit.AvailableShortSsm > 0) ||
                (range <= 3 && unit.AvailableLongSsm > 0));
            var guns = range == 0 && (attacker.ActiveUnits.Any(unit => unit.EffectiveGuns > 0) ||
                target.ActiveUnits.Any(unit => unit.EffectiveGuns > 0));
            return missile || guns;
        }

        private bool IsFormationVisibleToLocal(TaskForceState force) => force.Side == LocalSide ||
            !_game.State.DetectionRulesEnabled || _game.State.Detection.IsDetected(LocalSide, force.Id) ||
            _game.State.IsGameOver;

        private IEnumerable<string> VisibleEventLog()
        {
            if (!_game.State.DetectionRulesEnabled || _game.State.IsGameOver) return _game.State.Log;
            var hiddenNames = _game.State.Forces.Where(force => force.Side != LocalSide &&
                !_game.State.Detection.IsDetected(LocalSide, force.Id)).Select(force => force.Id).ToArray();
            return _game.State.Log.Where(entry => hiddenNames.All(name => !entry.Contains(name)) &&
                !entry.Contains(OpposingSide(LocalSide).ToString()));
        }

        private void SelectFormation(Side side)
        {
            var force = _game.State.Forces.First(candidate => candidate.Side == side);
            SelectFormation(side, force.Id);
        }

        private void SelectFormation(Side side, string formationId)
        {
            if (_selectedFormation != side || _selectedFormationId != formationId)
                _formationPanelScroll = Vector2.zero;
            _selectedFormation = side;
            _selectedFormationId = string.IsNullOrEmpty(formationId)
                ? _game.State.Forces.First(force => force.Side == side).Id : formationId;
            _status = $"Inspecting {_selectedFormationId} formation cards.";
            RefreshViews();
        }

        private void OnAttackResolved(Side attacker, AttackReport report)
        {
            if (report.IsGunfire) PlayGameSound(_gunClip, 0.9f);
            else PlayGameSound(_attackClip);
            var engagement = _game.State.PendingMissileCombat;
            var attackingForce = engagement != null
                ? _game.State.Formation(engagement.AttackerFormationId) : _game.State.ForceFor(attacker);
            var defendingForce = engagement != null
                ? _game.State.Formation(engagement.DefenderFormationId)
                : _game.State.ForceFor(attacker == Side.UsNavy ? Side.Plan : Side.UsNavy);
            var origin = _formationMarkers.TryGetValue(attackingForce.Id, out var attackingMarker)
                ? attackingMarker.position : WorldPosition(attackingForce.Position);
            var target = _formationMarkers.TryGetValue(defendingForce.Id, out var defendingMarker)
                ? defendingMarker.position : WorldPosition(defendingForce.Position);
            if (report.IsGunfire)
            {
                var lateral = attacker == Side.UsNavy ? Vector3.left : Vector3.right;
                StartCoroutine(PlayGunfireEffect(origin + lateral * 0.85f,
                    target - lateral * 0.85f, report));
            }
            else StartCoroutine(PlayAttackEffect(origin, target, report));
        }

        private IEnumerator PlayGunfireEffect(Vector3 origin, Vector3 target, AttackReport report)
        {
            var muzzle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            muzzle.name = "Gun Muzzle Flash";
            muzzle.transform.position = origin + Vector3.up * 0.8f;
            muzzle.GetComponent<Renderer>().sharedMaterial = VisualFactory.Material(
                new Color(1f, 0.68f, 0.08f), 0.1f, 0.1f);
            var rounds = new List<GameObject>();
            var count = Mathf.Clamp(report.AttackFactors, 1, 4);
            for (var index = 0; index < count; index++)
            {
                var tracer = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                tracer.name = "Naval Gun Tracer";
                tracer.transform.localScale = new Vector3(0.07f, 0.07f, 0.24f);
                tracer.GetComponent<Renderer>().sharedMaterial = VisualFactory.Material(
                    new Color(1f, 0.38f + index * 0.08f, 0.04f), 0f, 0.05f);
                rounds.Add(tracer);
            }
            const float duration = 0.42f;
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                var t = Mathf.Clamp01(elapsed / duration);
                muzzle.transform.localScale = Vector3.one * Mathf.Lerp(0.55f, 0.02f, t);
                for (var index = 0; index < rounds.Count; index++)
                {
                    var delayed = Mathf.Clamp01(t * 1.35f - index * 0.09f);
                    rounds[index].transform.position = Vector3.Lerp(origin, target, delayed) +
                        Vector3.up * (0.65f + Mathf.Sin(delayed * Mathf.PI) * (0.45f + index * 0.08f));
                }
                yield return null;
            }
            Destroy(muzzle);
            foreach (var round in rounds) Destroy(round);
            if (report.HullHits <= 0) yield break;
            PlayGameSound(_impactClip, 0.65f);
            var smoke = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            smoke.name = "Gunfire Impact Smoke";
            smoke.transform.position = target + Vector3.up * 0.7f;
            smoke.GetComponent<Renderer>().sharedMaterial = VisualFactory.Material(
                new Color(0.25f, 0.22f, 0.18f), 0f, 0.55f);
            for (var elapsed = 0f; elapsed < 0.6f; elapsed += Time.deltaTime)
            {
                smoke.transform.position += Vector3.up * Time.deltaTime * 0.35f;
                smoke.transform.localScale = Vector3.one * (0.12f + elapsed * 0.9f);
                yield return null;
            }
            Destroy(smoke);
            if (report.SankAnyShip) yield return PlaySinkingEffect(target);
        }

        private IEnumerator PlayAttackEffect(Vector3 origin, Vector3 target, AttackReport report)
        {
            var count = Mathf.Clamp(report.AttackFactors, 1, 4);
            var missiles = new GameObject[count];
            var defenseBursts = new List<GameObject>();
            var interceptedVisuals = report.AttackFactors <= 0 ? 0 : Mathf.Clamp(
                Mathf.RoundToInt(count * report.InterceptedFactors / (float)report.AttackFactors), 0, count);
            for (var i = 0; i < count; i++)
            {
                missiles[i] = VisualFactory.CreateMissile(new Color(1f, 0.3f + i * 0.08f, 0.05f));
                missiles[i].transform.position = origin + Vector3.up * (0.7f + i * 0.08f);
            }
            const float duration = 0.9f;
            var elapsed = 0f;
            var interceptionsShown = false;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                for (var i = 0; i < missiles.Length; i++)
                {
                    if (missiles[i] == null) continue;
                    var offset = Vector3.right * ((i - (count - 1) * 0.5f) * 0.12f);
                    missiles[i].transform.position = Vector3.Lerp(origin, target, t) + offset +
                                                     Vector3.up * (0.8f + Mathf.Sin(t * Mathf.PI) * 4f);
                }
                if (!interceptionsShown && t >= 0.62f)
                {
                    interceptionsShown = true;
                    for (var i = 0; i < interceptedVisuals; i++)
                    {
                        if (missiles[i] == null) continue;
                        var interceptBurst = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        interceptBurst.name = "SAM Intercept";
                        interceptBurst.transform.position = missiles[i].transform.position;
                        interceptBurst.transform.localScale = Vector3.one * 0.22f;
                        interceptBurst.GetComponent<Renderer>().sharedMaterial = VisualFactory.Material(
                            new Color(0.1f, 0.82f, 1f), 0f, 0.15f);
                        defenseBursts.Add(interceptBurst);
                        Destroy(missiles[i]);
                    }
                }
                foreach (var defenseBurst in defenseBursts)
                    if (defenseBurst != null) defenseBurst.transform.localScale += Vector3.one * (Time.deltaTime * 1.8f);
                yield return null;
            }
            foreach (var missile in missiles) if (missile != null) Destroy(missile);
            foreach (var defenseBurst in defenseBursts) if (defenseBurst != null) Destroy(defenseBurst);
            if (report.HullHits <= 0) yield break;
            PlayGameSound(_impactClip);
            var burst = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            burst.name = "Impact";
            burst.transform.position = target + Vector3.up * 0.7f;
            burst.GetComponent<Renderer>().sharedMaterial = VisualFactory.Material(new Color(1f, 0.3f, 0.02f), 0f, 0.2f);
            for (var t = 0f; t < 0.35f; t += Time.deltaTime)
            {
                burst.transform.localScale = Vector3.one * (0.2f + t * 4f);
                yield return null;
            }
            Destroy(burst);
            if (report.SankAnyShip) yield return PlaySinkingEffect(target);
        }

        private IEnumerator PlaySinkingEffect(Vector3 target)
        {
            PlayGameSound(_sinkingClip, 0.95f);
            var rings = new List<GameObject>();
            for (var index = 0; index < 3; index++)
            {
                var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ring.name = "Sinking Water Ring";
                ring.transform.position = target + Vector3.up * (0.05f + index * 0.025f);
                ring.transform.localScale = new Vector3(0.15f, 0.012f, 0.15f);
                ring.GetComponent<Renderer>().sharedMaterial = VisualFactory.Material(
                    new Color(0.38f, 0.78f, 0.9f), 0f, 0.6f);
                rings.Add(ring);
            }
            var column = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            column.name = "Sinking Steam Column";
            column.transform.position = target + Vector3.up * 0.45f;
            column.GetComponent<Renderer>().sharedMaterial = VisualFactory.Material(
                new Color(0.42f, 0.45f, 0.44f), 0f, 0.1f);
            const float duration = 1.15f;
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                var t = Mathf.Clamp01(elapsed / duration);
                for (var index = 0; index < rings.Count; index++)
                {
                    var delayed = Mathf.Clamp01(t * 1.35f - index * 0.18f);
                    rings[index].transform.localScale = new Vector3(0.15f + delayed * 1.65f,
                        0.012f, 0.15f + delayed * 1.65f);
                }
                column.transform.localScale = new Vector3(0.25f + t * 0.75f,
                    0.35f + Mathf.Sin(t * Mathf.PI) * 1.8f, 0.25f + t * 0.75f);
                column.transform.position += Vector3.up * Time.deltaTime * 0.28f;
                yield return null;
            }
            foreach (var ring in rings) Destroy(ring);
            Destroy(column);
        }

        private void OnCommandProcessed(GameCommand command, CommandResult result)
        {
            if (!result.Accepted)
            {
                PlayGameSound(_rejectClip, 0.8f);
                return;
            }
            if (command == null) return;
            if (_sessionMode == SessionMode.HotSeat && _game.State.ActiveFormationId.Length > 0)
            {
                _selectedFormation = _game.State.ActiveSide;
                _selectedFormationId = _game.State.ActiveFormationId;
            }
            switch (command.Type)
            {
                case GameCommandType.DrawMovementChit:
                    AnnounceChitDraw();
                    break;
                case GameCommandType.Move:
                    PlayGameSound(_moveClip, 0.85f);
                    var movingId = string.IsNullOrEmpty(command.FormationId)
                        ? _game.State.ActiveFormationId : command.FormationId;
                    _formationMarkers.TryGetValue(movingId, out var marker);
                    if (marker != null)
                        StartCoroutine(PlayWakeEffect(marker.position,
                            WorldPosition(command.Destination) + Vector3.up * 0.12f, command.Actor));
                    break;
                case GameCommandType.Attack:
                    // Successful attacks play through OnAttackResolved so launch timing matches the animation.
                    break;
                default:
                    PlayGameSound(_actionClip, 0.8f);
                    break;
            }
        }

        private void PlayClientCommandFeedback(GameCommand command, bool accepted)
        {
            if (!accepted)
            {
                PlayGameSound(_rejectClip, 0.8f);
                return;
            }
            if (command.Type == GameCommandType.DrawMovementChit)
                AnnounceChitDraw();
            else if (command.Type == GameCommandType.Move)
            {
                PlayGameSound(_moveClip, 0.85f);
                var movingId = string.IsNullOrEmpty(command.FormationId)
                    ? _game.State.ActiveFormationId : command.FormationId;
                _formationMarkers.TryGetValue(movingId, out var marker);
                if (marker != null)
                    StartCoroutine(PlayWakeEffect(marker.position,
                        WorldPosition(command.Destination) + Vector3.up * 0.12f, command.Actor));
            }
            else if (command.Type == GameCommandType.Attack) PlayGameSound(_attackClip);
            else PlayGameSound(_actionClip, 0.8f);
        }

        private void AnnounceChitDraw()
        {
            PlayGameSound(_chitClip, 0.9f);
            _chitBanner = _game.State.ActiveFormationId.Length > 0
                ? _game.State.ActiveFormationId.ToUpperInvariant() : "MOVEMENT CHIT DRAWN";
            _chitBannerUntil = Time.unscaledTime + 2.2f;
        }

        private IEnumerator PlayWakeEffect(Vector3 origin, Vector3 destination, Side side)
        {
            const int count = 6;
            var wakes = new GameObject[count];
            var color = side == Side.UsNavy
                ? new Color(0.35f, 0.82f, 1f) : new Color(1f, 0.56f, 0.36f);
            for (var index = 0; index < count; index++)
            {
                var wake = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                wake.name = "Formation Wake";
                Destroy(wake.GetComponent<Collider>());
                var t = (index + 1f) / (count + 1f);
                wake.transform.position = Vector3.Lerp(origin, destination, t) + Vector3.down * 0.5f;
                wake.transform.localScale = new Vector3(0.3f + t * 0.35f, 0.035f, 0.12f + t * 0.15f);
                wake.GetComponent<Renderer>().sharedMaterial = VisualFactory.Material(color, 0f, 0.15f);
                wakes[index] = wake;
            }
            for (var elapsed = 0f; elapsed < 0.7f; elapsed += Time.deltaTime)
            {
                var fade = 1f - elapsed / 0.7f;
                foreach (var wake in wakes)
                    if (wake != null) wake.transform.localScale *= 0.985f * fade + 0.015f;
                yield return null;
            }
            foreach (var wake in wakes) if (wake != null) Destroy(wake);
        }

        private static Vector3 WorldPosition(HexCoord coordinate)
        {
            var point = coordinate.ToMapPoint(HexRadius);
            return new Vector3((float)point.X, 0f, (float)point.Y);
        }

        private bool IsPointerOverPanel()
        {
            var overLeft = Input.mousePosition.x < 390f && Input.mousePosition.y > 18f;
            var overRight = Input.mousePosition.x > Screen.width - 410f && Input.mousePosition.y > 18f;
            var overDebug = _showDebug && Input.mousePosition.x > 406f &&
                            Input.mousePosition.x < Screen.width - 406f &&
                            Input.mousePosition.y > 24f && Input.mousePosition.y < Screen.height - 24f;
            var overLobby = _showMultiplayer && Input.mousePosition.x > (Screen.width - 620f) * 0.5f &&
                            Input.mousePosition.x < (Screen.width + 620f) * 0.5f &&
                            Input.mousePosition.y > 30f && Input.mousePosition.y < Screen.height - 30f;
            return overLeft || overRight || overDebug || overLobby;
        }

        private void StartHotSeat()
        {
            _network.Stop();
            _publicNetwork.Stop();
            _wasConnected = false;
            _sessionMode = SessionMode.HotSeat;
            _showMultiplayer = false;
            Restart();
            _status = "Hot-seat enabled. The active side is shown at the top of the command panel.";
        }

        private void SetPaused(bool paused)
        {
            _isPaused = paused;
            Time.timeScale = paused ? 0f : 1f;
        }

        private string SavePath => Path.Combine(Application.persistentDataPath, "Saves",
            $"{_game?.State.Scenario?.Id ?? _selectedScenario.Id}-save.json");

        private void SaveMatch()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SavePath) ?? Application.persistentDataPath);
                File.WriteAllText(SavePath, JsonUtility.ToJson(_game.CaptureSnapshot(), true));
                _saveStatus = $"Saved seed {_game.Seed}: {SavePath}";
                _status = "Scenario saved.";
            }
            catch (Exception exception)
            {
                _saveStatus = "Save failed: " + exception.Message;
                _status = _saveStatus;
            }
        }

        private void LoadMatch()
        {
            try
            {
                if (!File.Exists(SavePath))
                {
                    _saveStatus = $"No {_selectedScenario.Name} save exists yet.";
                    _status = _saveStatus;
                    return;
                }
                var snapshot = JsonUtility.FromJson<ScenarioOneSnapshot>(File.ReadAllText(SavePath));
                var scenario = FirstIslandChainScenarios.Get(snapshot.scenarioId) ??
                    throw new InvalidOperationException($"Unknown saved scenario '{snapshot.scenarioId}'.");
                _detectionTestMode = snapshot.detectionRulesEnabled;
                var loaded = ScenarioOneGame.Replay(snapshot.seed, snapshot.commands, null,
                    snapshot.detectionRulesEnabled, _sessionMode != SessionMode.SinglePlayer, scenario);
                _game = loaded;
                _selectedScenario = scenario;
                _game.AttackResolved += OnAttackResolved;
                _game.CommandProcessed += OnCommandProcessed;
                _matchSeed = _game.Seed;
                _seedText = _matchSeed.ToString();
                _selectedFormation = LocalSide;
                _selectedFormationId = _game.State.Forces.First(force => force.Side == LocalSide).Id;
                ResetCombatDrafts();
                RefreshViews();
                _saveStatus = $"Loaded seed {_game.Seed} from deterministic command replay.";
                _status = _saveStatus;
                SetPaused(false);
                if (IsHostSession && NetworkConnected) BroadcastSnapshot();
            }
            catch (Exception exception)
            {
                _saveStatus = "Load failed: " + exception.Message;
                _status = _saveStatus;
            }
        }

        private void UseRandomSeed()
        {
            _matchSeed = unchecked(Environment.TickCount ^ DateTime.UtcNow.Ticks.GetHashCode());
            if (_matchSeed == 0) _matchSeed = 1;
            _seedText = _matchSeed.ToString();
            Restart();
        }

        private string MatchLogText()
        {
            var snapshot = JsonUtility.ToJson(_game.CaptureSnapshot(), true);
            var trace = string.Join("\n", _game.State.Transactions.Select(item => item.ToString()));
            return $"HARPOON MATCH EXPORT\nScenario: {_game.State.Scenario?.Id}\nSeed: {_game.Seed}\n" +
                   $"Result: {_game.State.Result}\nEnd reason: {_game.State.EndReason}\n\nSNAPSHOT\n{snapshot}\n\nRULES TRACE\n{trace}";
        }

        private void ExportMatchLog()
        {
            try
            {
                var directory = Path.Combine(Application.persistentDataPath, "Exports");
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, $"harpoon-{DateTime.Now:yyyyMMdd-HHmmss}-seed-{_game.Seed}.txt");
                File.WriteAllText(path, MatchLogText());
                _exportStatus = $"Exported: {path}";
                _status = _exportStatus;
            }
            catch (Exception exception)
            {
                _exportStatus = $"Export failed: {exception.Message}";
                _status = _exportStatus;
            }
        }

        private void SubmitScenarioCommand(GameCommandType type)
        {
            if (IsClientSession)
            {
                SendCommand(type);
                _status = $"{type} sent to host.";
                return;
            }
            var result = _game.Execute(new GameCommand(type, LocalSide, _game.State.Revision));
            _status = result.Accepted ? (_game.State.IsGameOver ? _game.State.Result : result.Summary) : result.Summary;
            RefreshViews();
            if (IsHostSession) BroadcastSnapshot();
        }

        private void DrawScenarioControls()
        {
            var score = _game.CurrentScore();
            var scoringMode = _game.State.Scenario.ScoringMode;
            GUILayout.Label(_game.State.Scenario.VictoryText, _cardStatStyle);
            var oldColor = GUI.color;
            GUI.color = new Color(0.28f, 0.78f, 1f);
            var scoreTitle = scoringMode == ScenarioScoringMode.ConvoyArrival ? "CONVOY STATUS" :
                scoringMode == ScenarioScoringMode.TotalHullHits ? "HULL HITS INFLICTED" :
                scoringMode == ScenarioScoringMode.GunfireHullHits ? "GUNFIRE HITS" : "OBJECTIVE DAMAGE";
            var scoreText = scoringMode == ScenarioScoringMode.ConvoyArrival
                ? $"{scoreTitle}    ARRIVED {score.UsObjectiveDamage}   ·   LOST {score.PlanObjectiveDamage}"
                : $"{scoreTitle}    US {score.UsObjectiveDamage}   -   PLAN {score.PlanObjectiveDamage}";
            GUILayout.Label(scoreText,
                _cardHeaderStyle);
            GUI.color = oldColor;
            if (scoringMode == ScenarioScoringMode.ObjectiveThenEscort)
                GUILayout.Label($"Escort tie-break: US {score.UsTieBreakDamage}   -   PLAN {score.PlanTieBreakDamage}", _cardStatStyle);
            if (!_game.State.IsGameOver)
            {
                GUILayout.BeginHorizontal();
                GUI.enabled = CanLocalAct() && _game.State.Phase != ActivationPhase.MissileCombat &&
                              _game.State.Phase != ActivationPhase.GunCombat;
                if (GUILayout.Button("DISENGAGE & SCORE", _buttonStyle)) SubmitScenarioCommand(GameCommandType.Disengage);
                GUI.enabled = _sessionMode == SessionMode.HotSeat || NetworkConnected;
                if (GUILayout.Button("REQUEST SCORE", _buttonStyle)) SubmitScenarioCommand(GameCommandType.RequestScoring);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            else
                GUILayout.Label($"{_game.State.Result} - {_game.State.EndReason}", _titleStyle);
            GUILayout.Space(8f);
        }

        private bool DrawSectionHeader(string title, string summary, Color accent, bool expanded)
        {
            var previous = GUI.backgroundColor;
            GUI.backgroundColor = accent;
            var label = $"{(expanded ? "-" : "+")}  {title}";
            if (!string.IsNullOrWhiteSpace(summary)) label += $"     {summary}";
            var clicked = GUILayout.Button(label, _sectionHeaderStyle);
            GUI.backgroundColor = previous;
            return clicked;
        }

        private void DrawCurrentOrders()
        {
            DrawSpeedDeclaration();
            GUILayout.Space(6f);
            var actionPhase = _game.State.Phase == ActivationPhase.PlayerMove ||
                              _game.State.Phase == ActivationPhase.PlayerAction;
            var legalTargets = _game.State.Forces.Where(IsLegalAttackTarget).ToArray();
            var prior = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.78f, 0.24f, 0.16f);
            GUI.enabled = CanLocalAct() && actionPhase && !_game.State.PlayerHasAttacked && legalTargets.Length > 0;
            var attackLabel = legalTargets.Length == 0 ? "NO TARGET IN WEAPON RANGE" :
                legalTargets.Length == 1 ? $"ATTACK {legalTargets[0].Id.ToUpperInvariant()}" :
                "ATTACK SELECTED RED-RINGED TARGET";
            if (GUILayout.Button(attackLabel, _buttonStyle))
            {
                var selectedTarget = _game.State.Formation(_selectedFormationId);
                var targetFormationId = selectedTarget != null && legalTargets.Contains(selectedTarget)
                    ? selectedTarget.Id : legalTargets.Length == 1 ? legalTargets[0].Id : null;
                if (IsClientSession)
                {
                    SendCommand(GameCommandType.Attack, targetId: targetFormationId);
                    _status = "Attack command sent to host.";
                }
                else
                {
                    var commandResult = _game.Execute(new GameCommand(GameCommandType.Attack,
                        LocalSide, _game.State.Revision, targetId: targetFormationId));
                    _status = commandResult.Accepted && _game.State.Phase == ActivationPhase.MissileCombat
                        ? "Missile engagement opened. Allocate factors to targets."
                        : commandResult.Accepted && _game.State.Phase == ActivationPhase.GunCombat
                            ? "Close action opened. Resolve the gun engagement."
                            : commandResult.Summary;
                    RefreshViews();
                    if (IsHostSession) BroadcastSnapshot();
                }
            }
            GUI.backgroundColor = new Color(0.18f, 0.56f, 0.34f);
            var activeForce = _game.State.ForceFor(LocalSide);
            GUI.enabled = !_game.State.IsGameOver && CanLocalAct() &&
                          activeForce.DeclaredSpeed >= 0 && activeForce.MovementRemaining == 0;
            if (GUILayout.Button("END ACTIVATION", _buttonStyle))
            {
                if (IsClientSession)
                {
                    SendCommand(GameCommandType.EndActivation);
                    _status = "End activation sent to host.";
                }
                else
                {
                    var commandResult = _game.Execute(new GameCommand(GameCommandType.EndActivation,
                        LocalSide, _game.State.Revision));
                    _status = commandResult.Summary;
                    if (commandResult.Accepted)
                        _status = _game.State.IsGameOver ? _game.State.Result : "Waiting for the other activation.";
                    RefreshViews();
                    if (IsHostSession) BroadcastSnapshot();
                }
            }
            GUI.enabled = true;
            GUI.backgroundColor = prior;
        }

        private void DrawSystemControls()
        {
            GUILayout.Label("INTRODUCTORY SCENARIO", _cardStatStyle);
            GUILayout.BeginHorizontal();
            foreach (var scenario in FirstIslandChainScenarios.Introductory)
            {
                var priorScenarioColor = GUI.backgroundColor;
                if (scenario.Id == _game.State.Scenario.Id)
                    GUI.backgroundColor = new Color(0.12f, 0.58f, 0.78f);
                GUI.enabled = !IsClientSession;
                var scenarioLabel = scenario.Id == "fic-01" ? "1 BASHI" : scenario.Id == "fic-02"
                    ? "2 FLAGSHIP" : scenario.Id == "fic-03" ? "3 GUN DUEL" : "4 PICKET";
                if (GUILayout.Button(scenarioLabel, _buttonStyle))
                {
                    _selectedScenario = scenario;
                    _publicSessionName = $"Harpoon {scenario.Name}";
                    Restart();
                    _showBriefing = true;
                }
                GUI.enabled = true;
                GUI.backgroundColor = priorScenarioColor;
            }
            GUILayout.EndHorizontal();
            GUILayout.Label(_game.State.Scenario.Name.ToUpperInvariant(), _cardHeaderStyle);
            if (_game.State.Scenario.PlanDeploymentMinimumDistance > 0 &&
                _game.State.Phase == ActivationPhase.AwaitingChit && _game.State.MovementCup.FirstDrawPending)
            {
                var mayDeploy = _sessionMode == SessionMode.HotSeat || LocalSide == Side.Plan;
                GUI.enabled = mayDeploy;
                var priorDeployColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.52f, 0.18f, 0.68f);
                if (GUILayout.Button(_placingPlanDeployment ? "CANCEL PLAN DEPLOYMENT" : "PLACE PLAN PICKET ON MAP", _buttonStyle))
                    _placingPlanDeployment = !_placingPlanDeployment;
                GUI.backgroundColor = priorDeployColor;
                GUI.enabled = true;
                if (!mayDeploy) GUILayout.Label("Solo PLAN deployment is seeded and hidden.", _cardStatStyle);
            }
            GUILayout.Space(7f);
            GUILayout.Label($"INSTALLED VERSION   {Application.version}", _cardStatStyle);
            var versionColor = GUI.color;
            GUI.color = _availableUpdate?.UpdateAvailable == true
                ? new Color(0.35f, 1f, 0.48f) : new Color(0.66f, 0.82f, 0.9f);
            GUILayout.Label(_updateStatus, _cardStatStyle);
            GUI.color = versionColor;
            if (_downloadingUpdate)
            {
                GUILayout.Label($"DOWNLOADING & VERIFYING   {Mathf.RoundToInt(_updateProgress * 100f)}%", _cardHeaderStyle);
            }
            else
            {
                GUILayout.BeginHorizontal();
                GUI.enabled = !_checkingForUpdate;
                if (GUILayout.Button(_checkingForUpdate ? "CHECKING..." : "CHECK FOR UPDATE", _buttonStyle))
                    StartCoroutine(CheckForUpdates());
                GUI.enabled = _availableUpdate?.UpdateAvailable == true;
                var priorUpdateColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.16f, 0.68f, 0.34f);
                if (GUILayout.Button("INSTALL UPDATE", _buttonStyle)) StartCoroutine(InstallAvailableUpdate());
                GUI.backgroundColor = priorUpdateColor;
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(7f);
            GUILayout.Label($"DETERMINISTIC MATCH SEED   {_game.Seed}", _cardStatStyle);
            GUILayout.BeginHorizontal();
            _seedText = GUILayout.TextField(_seedText, 16, GUILayout.Width(112f));
            GUI.enabled = !IsClientSession;
            if (GUILayout.Button("RESTART SEED", _buttonStyle)) Restart();
            if (GUILayout.Button("RANDOM", _buttonStyle, GUILayout.Width(78f))) UseRandomSeed();
            GUILayout.EndHorizontal();
            if (GUILayout.Button("PAUSE / SAVE  [P]", _buttonStyle)) SetPaused(true);
            if (GUILayout.Button("BRIEFING / RULES  [F1]", _buttonStyle)) _showBriefing = true;
            if (GUILayout.Button("RESTART SCENARIO", _buttonStyle)) _confirmRestart = true;
            GUI.enabled = true;
            if (GUILayout.Button(_sessionMode == SessionMode.HotSeat ? "RETURN TO SOLO" : "HOT-SEAT 1 vs 1", _buttonStyle))
            {
                if (_sessionMode == SessionMode.HotSeat)
                {
                    _sessionMode = SessionMode.SinglePlayer;
                    Restart();
                }
                else StartHotSeat();
            }
            if (GUILayout.Button(_sessionMode == SessionMode.SinglePlayer ? "MULTIPLAYER" : "MULTIPLAYER / CONNECTION", _buttonStyle))
                _showMultiplayer = !_showMultiplayer;
            if (GUILayout.Button(_showDebug ? "CLOSE DEBUG TRACE  [F3]" : "DEBUG TRACE  [F3]", _buttonStyle))
                _showDebug = !_showDebug;
            var previous = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.62f, 0.18f, 0.16f);
            if (GUILayout.Button("EXIT GAME", _buttonStyle)) _confirmExit = true;
            GUI.backgroundColor = previous;
        }

        private IEnumerator CheckForUpdates()
        {
            if (_checkingForUpdate || _downloadingUpdate) yield break;
            _checkingForUpdate = true;
            _updateStatus = "Checking GitHub Releases...";
            UpdateCheckResult result = null;
            yield return GitHubUpdateService.Check(Application.version, value => result = value);
            _checkingForUpdate = false;
            _availableUpdate = result?.UpdateAvailable == true ? result : null;
            _updateStatus = result?.Message ?? "Update check returned no result.";
        }

        private IEnumerator InstallAvailableUpdate()
        {
            if (_downloadingUpdate || _availableUpdate?.UpdateAvailable != true) yield break;
            _downloadingUpdate = true;
            _updateProgress = 0f;
            _updateStatus = $"Downloading {_availableUpdate.Release.tag_name}...";
            string packagePath = null;
            string downloadError = null;
            yield return GitHubUpdateService.DownloadAndVerify(_availableUpdate.Release, _availableUpdate.Asset,
                value => _updateProgress = value, (path, error) =>
                {
                    packagePath = path;
                    downloadError = error;
                });
            _downloadingUpdate = false;
            if (!string.IsNullOrWhiteSpace(downloadError))
            {
                _updateStatus = downloadError;
                yield break;
            }
            SaveMatch();
            var targetVersion = _availableUpdate.Release.tag_name.TrimStart('v', 'V');
            if (!GitHubUpdateService.LaunchInstaller(packagePath, targetVersion, out var launchError))
            {
                _updateStatus = "Installer could not start: " + launchError;
                yield break;
            }
            _updateStatus = "Verified. Closing Harpoon so the updater can install and relaunch.";
            yield return null;
            QuitGame();
        }

        private string CurrentDecisionPrompt()
        {
            if (_game.State.IsGameOver) return "SCENARIO COMPLETE - review the score or begin a new match.";
            if (_game.State.Phase == ActivationPhase.MissileCombat)
                return _game.State.PendingMissileCombat?.DecisionSide == LocalSide
                    ? "DECISION REQUIRED - resolve the highlighted missile-combat stage."
                    : "WAITING - opponent is resolving missile combat.";
            if (_game.State.Phase == ActivationPhase.GunCombat)
                return _game.State.PendingGunCombat?.DecisionSide == LocalSide
                    ? "DECISION REQUIRED - resolve the highlighted close-action stage."
                    : "WAITING - opponent is resolving close action.";
            if (_game.State.Phase == ActivationPhase.AwaitingChit)
                return "DECISION REQUIRED - split now if desired, then draw a movement chit.";
            if (_game.State.ActiveSide != LocalSide) return "WAITING - opposing formation is active.";
            if (_game.State.Phase == ActivationPhase.DeclareSpeed)
                return _game.State.DetectionRulesEnabled && !_game.State.ForceFor(LocalSide).RadarDeclaredThisActivation
                    ? "DECISION REQUIRED - declare SSR silent or radiating, then choose speed."
                    : "DECISION REQUIRED - declare formation speed.";
            var force = _game.State.ForceFor(LocalSide);
            if (force.MovementRemaining > 0)
                return "DECISION REQUIRED - enter a cyan adjacent hex; red-ringed formations are legal targets.";
            return _game.State.Forces.Any(IsLegalAttackTarget)
                ? "DECISION REQUIRED - attack a red-ringed target or end activation."
                : "NO WEAPON IN RANGE - end this formation's activation.";
        }

        private void DrawBriefingOverlay()
        {
            GUI.Box(new Rect(0f, 0f, Screen.width, Screen.height), GUIContent.none);
            var width = Mathf.Min(760f, Screen.width - 48f);
            var height = Mathf.Min(680f, Screen.height - 48f);
            var rect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUI.Box(rect, GUIContent.none);
            GUILayout.BeginArea(new Rect(rect.x + 28f, rect.y + 24f, rect.width - 56f, rect.height - 48f));
            GUILayout.Label("OPERATIONAL BRIEFING", _titleStyle);
            GUILayout.Label(_game.State.Scenario.Name.ToUpperInvariant(), _debugHeaderStyle);
            GUILayout.Label(_game.State.Scenario?.Briefing ?? FirstIslandChainScenarios.ContactOffBashiChannel.Briefing, _labelStyle);
            GUILayout.Space(10f);
            GUILayout.Label("MISSION", _cardHeaderStyle);
            GUILayout.Label(_game.State.Scenario.VictoryText, _labelStyle);
            GUILayout.Label(_game.State.Scenario.ScoringMode == ScenarioScoringMode.TotalHullHits
                ? "All hull hits against every opposing warship count. Equality is a draw. No turn limit is printed."
                : _game.State.Scenario.ScoringMode == ScenarioScoringMode.GunfireHullHits
                    ? "Only hull hits caused by naval gunfire count. Missile damage still degrades ships but scores nothing. Equality is a draw."
                    : _game.State.Scenario.ScoringMode == ScenarioScoringMode.ConvoyArrival
                        ? "Reach the green Taipei / Zuoying destination with a merchant afloat, or destroy the PLAN picket. Both merchants sunk is a PLAN victory."
                    : "Escort damage is the tie-break after objective damage; equality after both comparisons is a draw. No turn limit is printed.",
                _cardStatStyle);
            GUILayout.Space(10f);
            GUILayout.Label($"CAPTAIN'S RULES - {_game.State.Scenario.Subtitle} QUICK REFERENCE", _cardHeaderStyle);
            GUILayout.Label("1. Draw a named movement chit. That formation declares speed, limited by its slowest ship.\n" +
                "2. Move one highlighted adjacent sea hex at a time. An attack opportunity exists after every entered hex.\n" +
                "3. SSM range: short 1 hex, long 3 hexes. Missile ammunition is expended permanently.\n" +
                "4. Defenses resolve LR SAM, assigned SR SAM, self-only point defense, then impacts.\n" +
                "5. Same-hex forces may enter naval gunfire. Choose firing/screened ships, targets, and break-off decisions explicitly.\n" +
                (_game.State.Scenario.DetectionRulesEnabled
                    ? "6. Detection is mandatory: declare SSR, use ESM against adjacent radiators, and search visually in the same hex."
                    : "6. Introductory Scenarios 1-3 omit detection. Detection Test Mode in F3 exposes the general SSR/ESM/visual rules."), _labelStyle);
            GUILayout.Space(10f);
            GUILayout.Label("VISUAL LANGUAGE", _cardHeaderStyle);
            GUILayout.Label("Cyan hex: legal next movement step   |   Red pulsing ring: legal attack target\n" +
                "Green ring: active formation   |   Gold ring: selected formation   |   Cyan sensor ring: radiating SSR\n" +
                "Amber/red damage ring and smoke: degraded or destroyed formation", _cardStatStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label("F1 briefing   P / Escape pause   F3 trace   F11 display", _cardStatStyle);
            if (GUILayout.Button("BEGIN / RETURN TO ACTION", _buttonStyle)) _showBriefing = false;
            GUILayout.EndArea();
        }

        private void DrawPauseOverlay()
        {
            var rect = new Rect((Screen.width - 430f) * 0.5f, (Screen.height - 350f) * 0.5f, 430f, 350f);
            GUI.Box(rect, GUIContent.none);
            GUILayout.BeginArea(new Rect(rect.x + 28f, rect.y + 24f, rect.width - 56f, rect.height - 48f));
            GUILayout.Label("ACTION PAUSED", _titleStyle);
            GUILayout.Label($"{_game.State.Scenario.Name} - seed {_game.Seed}", _cardStatStyle);
            if (GUILayout.Button("RESUME", _buttonStyle)) SetPaused(false);
            if (GUILayout.Button("BRIEFING / RULES  [F1]", _buttonStyle)) { SetPaused(false); _showBriefing = true; }
            GUI.enabled = !IsClientSession;
            if (GUILayout.Button("SAVE MATCH", _buttonStyle)) SaveMatch();
            if (GUILayout.Button("LOAD LAST SAVE", _buttonStyle)) LoadMatch();
            if (GUILayout.Button("RESTART SCENARIO", _buttonStyle)) { SetPaused(false); _confirmRestart = true; }
            GUI.enabled = true;
            if (GUILayout.Button("EXIT GAME", _buttonStyle)) { SetPaused(false); _confirmExit = true; }
            if (_saveStatus.Length > 0) GUILayout.Label(_saveStatus, _cardStatStyle);
            GUILayout.EndArea();
        }

        private void DrawConfirmationOverlay(bool restart)
        {
            var rect = new Rect((Screen.width - 460f) * 0.5f, (Screen.height - 220f) * 0.5f, 460f, 220f);
            GUI.Box(rect, GUIContent.none);
            GUILayout.BeginArea(new Rect(rect.x + 28f, rect.y + 24f, rect.width - 56f, rect.height - 48f));
            GUILayout.Label(restart ? "RESTART SCENARIO?" : "EXIT HARPOON?", _titleStyle);
            GUILayout.Label(restart ? "Unsaved progress in the current match will be replaced." :
                "Unsaved progress will be lost. You can return and save from the pause menu.", _labelStyle);
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("CANCEL", _buttonStyle)) { _confirmRestart = false; _confirmExit = false; }
            if (GUILayout.Button(restart ? "RESTART" : "EXIT", _buttonStyle))
            {
                if (restart) { _confirmRestart = false; Restart(); }
                else QuitGame();
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void OnGUI()
        {
            if (_game == null) return;
            EnsureStyles();
            if (_showBriefing) { DrawBriefingOverlay(); return; }
            if (_confirmRestart) { DrawConfirmationOverlay(true); return; }
            if (_confirmExit) { DrawConfirmationOverlay(false); return; }
            if (_isPaused) { DrawPauseOverlay(); return; }
            var commandPanelHeight = Mathf.Max(300f, Screen.height - 36f);
            GUI.Box(new Rect(18, 18, 370, commandPanelHeight), GUIContent.none);
            GUILayout.BeginArea(new Rect(28, 28, 350, commandPanelHeight - 20f));
            GUILayout.BeginHorizontal();
            GUILayout.Label("HARPOON", _titleStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label(_sessionMode == SessionMode.SinglePlayer ? "SOLO - US NAVY" :
                _sessionMode == SessionMode.HotSeat ? $"HOT-SEAT - {LocalSide.ToString().ToUpperInvariant()} TO ACT" :
                $"ONLINE - {LocalSide.ToString().ToUpperInvariant()}", _cardStatStyle);
            GUILayout.EndHorizontal();
            var turnLimit = _game.State.MaximumTurns > 0 ? _game.State.MaximumTurns.ToString() : "∞";
            GUILayout.Label($"TURN {_game.State.Turn}/{turnLimit}   ·   {_game.State.Phase}   ·   SEED {_game.Seed}", _cardStatStyle);
            GUILayout.Label(_status, _cardStatStyle);
            var priorPrompt = GUI.color;
            GUI.color = new Color(1f, 0.84f, 0.28f);
            GUILayout.Label(CurrentDecisionPrompt(), _cardHeaderStyle);
            GUI.color = priorPrompt;
            GUILayout.Space(6f);

            if (_game.State.Phase == ActivationPhase.MissileCombat ||
                _game.State.Phase == ActivationPhase.GunCombat) _showOrdersSection = true;
            _commandPanelScroll = GUILayout.BeginScrollView(_commandPanelScroll, false, true,
                GUILayout.ExpandHeight(true));

            var score = _game.CurrentScore();
            if (DrawSectionHeader("VICTORY / OBJECTIVE",
                    $"US {score.UsObjectiveDamage} : {score.PlanObjectiveDamage} PLAN",
                    new Color(0.08f, 0.48f, 0.68f), _showObjectiveSection))
                _showObjectiveSection = !_showObjectiveSection;
            if (_showObjectiveSection)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                DrawScenarioControls();
                GUILayout.EndVertical();
            }

            var usShips = _game.State.Forces.Where(force => force.Side == Side.UsNavy)
                .Sum(force => force.ActiveUnits.Count());
            var planShips = _game.State.Forces.Where(force => force.Side == Side.Plan)
                .Sum(force => force.ActiveUnits.Count());
            if (DrawSectionHeader("ORDER OF BATTLE", $"US {usShips} · PLAN {planShips}",
                    new Color(0.16f, 0.34f, 0.62f), _showRosterSection))
                _showRosterSection = !_showRosterSection;
            if (_showRosterSection)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                foreach (var force in _game.State.Forces)
                {
                    if (IsFormationVisibleToLocal(force)) DrawForce(force);
                    else GUILayout.Label($"UNDETECTED {SideLabel(force.Side).ToUpperInvariant()} CONTACT", _cardStatStyle);
                    GUILayout.Space(3f);
                }
                GUILayout.EndVertical();
            }

            if (DrawSectionHeader("CURRENT ORDERS", _game.State.Phase.ToString().ToUpperInvariant(),
                    new Color(0.82f, 0.45f, 0.08f), _showOrdersSection))
                _showOrdersSection = !_showOrdersSection;
            if (_showOrdersSection)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                DrawCurrentOrders();
                GUILayout.EndVertical();
            }

            var systemSummary = _availableUpdate?.UpdateAvailable == true
                ? $"UPDATE {_availableUpdate.Release.tag_name}"
                : (_sessionMode == SessionMode.SinglePlayer ? "SOLO" : "1 vs 1");
            if (DrawSectionHeader("MATCH & SYSTEM", systemSummary,
                    new Color(0.32f, 0.36f, 0.42f), _showSystemSection))
                _showSystemSection = !_showSystemSection;
            if (_showSystemSection)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                DrawSystemControls();
                GUILayout.EndVertical();
            }

            var visibleLog = VisibleEventLog().ToArray();
            var latestEvent = visibleLog.LastOrDefault() ?? "No visible events";
            if (latestEvent.Length > 25) latestEvent = latestEvent.Substring(0, 25) + "...";
            if (DrawSectionHeader("EVENT LOG", latestEvent, new Color(0.18f, 0.48f, 0.44f), _showEventSection))
                _showEventSection = !_showEventSection;
            if (_showEventSection)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                foreach (var entry in visibleLog.Skip(System.Math.Max(0, visibleLog.Length - 9)))
                    GUILayout.Label("• " + entry, _cardStatStyle);
                GUILayout.EndVertical();
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            DrawFormationCards();
            DrawActivationRibbon();
            DrawChitBanner();
            DrawMissileCombatRibbon();
            DrawGunCombatRibbon();
            DrawVictoryOverlay();
            if (_sessionMode != SessionMode.SinglePlayer && _sessionMode != SessionMode.HotSeat) DrawChatAndSoundboard();
            if (_showMultiplayer) DrawMultiplayerLobby();
            if (_showDebug) DrawDebugPanel();
            if (_hoveredHex.HasValue && !IsPointerOverPanel())
            {
                var mouse = Event.current.mousePosition;
                GUI.Label(new Rect(mouse.x + 16f, mouse.y + 14f, 118f, 28f), $"HEX {_hoveredHex.Value}", _tooltipStyle);
            }
        }

        private bool CanLocalAct()
        {
            if (_game.State.IsGameOver || _game.State.ActiveSide != LocalSide) return false;
            return _sessionMode == SessionMode.SinglePlayer || _sessionMode == SessionMode.HotSeat || NetworkConnected;
        }

        private void DrawVictoryOverlay()
        {
            if (!_game.State.IsGameOver || _showDebug || _showMultiplayer) return;
            var width = Mathf.Min(620f, Screen.width - 820f);
            if (width < 420f) width = Mathf.Min(420f, Screen.width - 40f);
            var rect = new Rect((Screen.width - width) * 0.5f, Screen.height * 0.34f, width, 230f);
            GUI.Box(rect, GUIContent.none);
            var accent = GUI.color;
            GUI.color = _game.State.Result.StartsWith("US") ? new Color(0.15f, 0.68f, 1f) :
                _game.State.Result.StartsWith("PLAN") ? new Color(1f, 0.3f, 0.24f) : new Color(0.92f, 0.82f, 0.35f);
            GUI.Box(new Rect(rect.x, rect.y, rect.width, 6f), GUIContent.none);
            GUI.color = accent;
            GUILayout.BeginArea(new Rect(rect.x + 24f, rect.y + 22f, rect.width - 48f, rect.height - 38f));
            GUILayout.Label(_game.State.Result, _titleStyle);
            GUILayout.Label($"SCENARIO COMPLETE - {_game.State.EndReason}", _cardHeaderStyle);
            var score = _game.CurrentScore();
            if (_game.State.Scenario.ScoringMode == ScenarioScoringMode.ConvoyArrival)
                GUILayout.Label($"Convoy result   arrived {score.UsObjectiveDamage}   |   merchants lost {score.PlanObjectiveDamage}", _labelStyle);
            else
            {
                GUILayout.Label($"Score   US {score.UsObjectiveDamage}   |   PLAN {score.PlanObjectiveDamage}", _labelStyle);
                if (_game.State.Scenario.ScoringMode == ScenarioScoringMode.ObjectiveThenEscort)
                    GUILayout.Label($"Escort tie-break   US {score.UsTieBreakDamage}   |   PLAN {score.PlanTieBreakDamage}", _cardStatStyle);
            }
            GUILayout.Label($"Seed {_game.Seed} - full deterministic record available in Debug Trace", _cardStatStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("EXPORT MATCH", _buttonStyle)) ExportMatchLog();
            GUI.enabled = !IsClientSession;
            if (GUILayout.Button("PLAY AGAIN", _buttonStyle)) Restart();
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawSpeedDeclaration()
        {
            if (_game.State.Phase == ActivationPhase.MissileCombat)
            {
                DrawMissileCombatDecision();
                return;
            }
            if (_game.State.Phase == ActivationPhase.GunCombat)
            {
                DrawGunCombatDecision();
                return;
            }
            if (_game.State.Phase == ActivationPhase.AwaitingChit)
            {
                GUILayout.Label($"MOVEMENT CUP  ·  {_game.State.MovementCup.Remaining.Count} CHITS", _cardHeaderStyle);
                GUILayout.Label("Optional task-force splits close when the first chit is drawn.", _cardStatStyle);
                var splittable = _game.State.Forces.FirstOrDefault(force =>
                    force.Side == LocalSide && force.Units.Count > 1);
                if (splittable != null)
                {
                    GUI.enabled = _sessionMode == SessionMode.SinglePlayer || _sessionMode == SessionMode.HotSeat || NetworkConnected;
                    foreach (var unit in splittable.Units.ToArray())
                    {
                        var selectedUnit = unit;
                        if (GUILayout.Button("SPLIT: " + unit.Definition.DisplayName.ToUpperInvariant(),
                                _buttonStyle))
                            SplitLocalFormation(splittable, selectedUnit);
                    }
                    GUI.enabled = true;
                }
                GUI.enabled = _sessionMode == SessionMode.SinglePlayer || _sessionMode == SessionMode.HotSeat || NetworkConnected;
                if (GUILayout.Button("DRAW FIRST CHIT", _buttonStyle)) DrawLocalMovementChit();
                GUI.enabled = true;
                return;
            }
            var force = _game.State.ForceFor(LocalSide);
            if (_game.State.ActiveSide != LocalSide)
            {
                var opponentForce = _game.State.ForceFor(_game.State.ActiveSide);
                GUILayout.Label($"OPPONENT SPEED  {(opponentForce.DeclaredSpeed < 0 ? "PENDING" : opponentForce.DeclaredSpeed.ToString())}",
                    _cardStatStyle);
                return;
            }
            if (_game.State.Phase == ActivationPhase.DeclareSpeed)
            {
                if (_game.State.DetectionRulesEnabled)
                {
                    GUILayout.Label(force.RadarDeclaredThisActivation
                        ? $"SSR  {(force.RadarRadiating ? "RADIATING" : "SILENT")}"
                        : "DECLARE SURFACE-SEARCH RADAR", _cardHeaderStyle);
                    GUILayout.BeginHorizontal();
                    GUI.enabled = CanLocalAct();
                    if (GUILayout.Button("RADAR SILENT", _buttonStyle)) DeclareLocalRadar(false);
                    GUI.enabled = CanLocalAct() && force.CanRadiateRadar;
                    if (GUILayout.Button("RADIATE SSR", _buttonStyle)) DeclareLocalRadar(true);
                    GUI.enabled = true;
                    GUILayout.EndHorizontal();
                }
                GUILayout.Label($"DECLARE SPEED  ·  MAX {force.EffectiveSpeed}", _cardHeaderStyle);
                GUILayout.BeginHorizontal();
                GUI.enabled = CanLocalAct() &&
                              (!_game.State.DetectionRulesEnabled || force.RadarDeclaredThisActivation);
                for (var speed = 0; speed <= force.EffectiveSpeed; speed++)
                {
                    var selectedSpeed = speed;
                    if (GUILayout.Button(speed.ToString(), _buttonStyle)) DeclareLocalSpeed(selectedSpeed);
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label($"SPEED {force.DeclaredSpeed}  ·  STEPS {force.MovementPointsSpent}/{force.DeclaredSpeed}  ·  REMAINING {force.MovementRemaining}",
                    _cardStatStyle);
                DrawDetectionActions(force);
            }
        }

        private void DrawDetectionActions(TaskForceState observer)
        {
            if (!_game.State.DetectionRulesEnabled || _game.State.ActiveSide != LocalSide) return;
            var targets = _game.State.Forces.Where(force => force.Side != LocalSide && !force.IsDestroyed).ToArray();
            var visual = targets.FirstOrDefault(force => force.Position == observer.Position);
            var esm = targets.FirstOrDefault(force => force.RadarRadiating &&
                force.Position.DistanceTo(observer.Position) == 1);
            GUILayout.Label("SENSOR ACTIONS", _cardHeaderStyle);
            GUILayout.BeginHorizontal();
            GUI.enabled = CanLocalAct() && visual != null && !observer.RadarRadiating &&
                          _game.State.TimeOfDay != TimeOfDay.Night;
            if (GUILayout.Button("VISUAL SEARCH", _buttonStyle)) SearchLocal("visual", visual);
            GUI.enabled = CanLocalAct() && esm != null && observer.CanUseEsm;
            if (GUILayout.Button("ESM SEARCH", _buttonStyle)) SearchLocal("esm", esm);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        private void DrawMissileCombatDecision()
        {
            var engagement = _game.State.PendingMissileCombat;
            if (engagement == null)
            {
                GUILayout.Label("MISSILE EXCHANGE SYNCHRONIZING", _cardHeaderStyle);
                return;
            }
            var marker = engagement.AttackerFormationId + "|" + engagement.DefenderFormationId + "|" +
                         engagement.IsCounterattack + "|" + engagement.Phase;
            if (_combatDraftMarker != marker)
            {
                _missileDraft.Clear();
                _defensePairDraft.Clear();
                _longRangeRemovalDraft.Clear();
                _shortRangeDefenseDraft.Clear();
                _pairSelection = string.Empty;
                _combatDraftMarker = marker;
            }
            GUILayout.Label("MISSILE COMBAT  ·  " + CombatStageLabel(engagement.Phase), _cardHeaderStyle);
            GUILayout.Label($"{engagement.AttackerFormationId} → {engagement.DefenderFormationId}", _cardStatStyle);
            GUILayout.Label($"RAID {engagement.RemainingFactors}/{engagement.InitialFactors} FACTORS  ·  DECISION {SideLabel(engagement.DecisionSide)}",
                _cardStatStyle);
            if (engagement.DecisionSide != LocalSide)
            {
                GUILayout.Label("Waiting for the opponent's combat decision…", _labelStyle);
                return;
            }
            switch (engagement.Phase)
            {
                case MissileCombatPhase.AllocateFire:
                    DrawMissileAllocation(engagement);
                    break;
                case MissileCombatPhase.DefensiveDeployment:
                    DrawDefensiveDeployment(engagement);
                    break;
                case MissileCombatPhase.LongRangeRemoval:
                    DrawLongRangeRemovals(engagement);
                    break;
                case MissileCombatPhase.ShortRangeDefense:
                    DrawShortRangeDefense(engagement);
                    break;
                case MissileCombatPhase.CounterattackDecision:
                    DrawCounterattackDecision(engagement);
                    break;
            }
        }

        private void DrawGunCombatDecision()
        {
            var engagement = _game.State.PendingGunCombat;
            if (engagement == null)
            {
                GUILayout.Label("GUN ENGAGEMENT SYNCHRONIZING", _cardHeaderStyle);
                return;
            }
            var marker = "GUN|" + engagement.Round + "|" + engagement.Phase + "|" + engagement.DecisionSide;
            if (_combatDraftMarker != marker)
            {
                _gunPairDraft.Clear();
                _combatDraftMarker = marker;
            }
            GUILayout.Label($"NAVAL GUNFIRE  ·  ROUND {engagement.Round}", _cardHeaderStyle);
            GUILayout.Label($"{engagement.AttackerFormationId} ↔ {engagement.DefenderFormationId}", _cardStatStyle);
            GUILayout.Label("SAME HEX  ·  STRONGEST BATTERY FIRES FIRST", _cardStatStyle);
            if (engagement.DecisionSide != LocalSide)
            {
                GUILayout.Label("Waiting for the opponent's gunfire decision…", _labelStyle);
                return;
            }
            switch (engagement.Phase)
            {
                case GunCombatPhase.EngageDecision:
                    GUILayout.Label("Your force is faster. Evade cleanly, or turn and accept close action.", _labelStyle);
                    GUILayout.BeginHorizontal();
                    GunDecisionButton("EVADE", true);
                    GunDecisionButton("ENGAGE", false);
                    GUILayout.EndHorizontal();
                    break;
                case GunCombatPhase.ArrangeAttacker:
                case GunCombatPhase.ArrangeDefender:
                    DrawGunArrangement();
                    break;
                case GunCombatPhase.Firing:
                    DrawGunTargeting(engagement);
                    break;
                case GunCombatPhase.BreakOffAttacker:
                case GunCombatPhase.BreakOffDefender:
                    GUILayout.Label("All eligible firing ships have acted. Choose whether to disengage or fight another round.", _labelStyle);
                    GUILayout.BeginHorizontal();
                    GunDecisionButton("BREAK OFF", true);
                    GunDecisionButton("CONTINUE", false);
                    GUILayout.EndHorizontal();
                    break;
            }
        }

        private void DrawGunArrangement()
        {
            var engagement = _game.State.PendingGunCombat;
            var attacker = _game.State.Formation(engagement.AttackerFormationId);
            var force = attacker.Side == LocalSide ? attacker :
                _game.State.Formation(engagement.DefenderFormationId);
            if (_gunPairDraft.Count == 0)
                _gunPairDraft.AddRange(ScenarioOneGame.DefaultGunPairs(force));
            GUILayout.Label("Choose the firing ship on top of each pair. Its mate is screened and is harder to hit.", _labelStyle);
            foreach (var pair in _gunPairDraft)
            {
                var firing = force.Units.First(unit => unit.Definition.Id == pair.firingUnitId);
                var screened = force.Units.FirstOrDefault(unit => unit.Definition.Id == pair.screenedUnitId);
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label($"FIRING  {firing.Definition.DisplayName.ToUpperInvariant()}  ·  GUNS {firing.EffectiveGuns}", _cardHeaderStyle);
                GUILayout.Label(screened == null
                    ? "UNPAIRED SHIP"
                    : $"SCREENED  {screened.Definition.DisplayName.ToUpperInvariant()}  ·  GUNS {screened.EffectiveGuns}", _cardStatStyle);
                GUI.enabled = CanLocalAct() && screened != null && screened.EffectiveGuns > 0;
                if (GUILayout.Button("SWAP FIRING SHIP", _buttonStyle))
                {
                    var oldFiring = pair.firingUnitId;
                    pair.firingUnitId = pair.screenedUnitId;
                    pair.screenedUnitId = oldFiring;
                }
                GUI.enabled = true;
                GUILayout.EndVertical();
            }
            GUI.enabled = CanLocalAct();
            if (GUILayout.Button("LOCK FIRING FORMATION", _buttonStyle))
                SubmitCombatCommand(new GameCommand(GameCommandType.ArrangeGunfire, LocalSide,
                    _game.State.Revision, gunPairs: _gunPairDraft.ToArray()),
                    "Gunfire formation sent to host.");
            GUI.enabled = true;
        }

        private void DrawGunTargeting(GunEngagement engagement)
        {
            if (engagement.FiringIndex >= engagement.FiringOrder.Count) return;
            var shooterId = engagement.FiringOrder[engagement.FiringIndex];
            var attacker = _game.State.Formation(engagement.AttackerFormationId);
            var force = attacker.Side == LocalSide ? attacker :
                _game.State.Formation(engagement.DefenderFormationId);
            var shooter = force.Units.First(unit => unit.Definition.Id == shooterId);
            var targets = attacker.Side == LocalSide
                ? _game.State.Formation(engagement.DefenderFormationId) : attacker;
            GUILayout.Label($"NOW FIRING  ·  {shooter.Definition.DisplayName.ToUpperInvariant()}  ·  {shooter.EffectiveGuns} DICE", _cardHeaderStyle);
            GUILayout.Label($"SHOT {engagement.FiringIndex + 1}/{engagement.FiringOrder.Count}", _cardStatStyle);
            foreach (var target in targets.ActiveUnits)
            {
                var screened = engagement.IsScreened(target.Definition.Id);
                GUI.enabled = CanLocalAct();
                var label = $"FIRE AT {target.Definition.DisplayName.ToUpperInvariant()}" +
                            (screened ? "  ·  SCREENED −1" : "  ·  EXPOSED");
                if (GUILayout.Button(label, _buttonStyle))
                    SubmitCombatCommand(new GameCommand(GameCommandType.FireGuns, LocalSide,
                        _game.State.Revision, targetId: target.Definition.Id,
                        sourceUnitId: shooter.Definition.Id), "Gun target sent to host.");
                GUI.enabled = true;
            }
        }

        private void GunDecisionButton(string label, bool enabled)
        {
            GUI.enabled = CanLocalAct();
            if (GUILayout.Button(label, _buttonStyle))
                SubmitCombatCommand(new GameCommand(GameCommandType.BreakOff, LocalSide,
                    _game.State.Revision, enabled: enabled), "Gun engagement decision sent to host.");
            GUI.enabled = true;
        }

        private void DrawMissileAllocation(MissileEngagement engagement)
        {
            var attacker = _game.State.Formation(engagement.AttackerFormationId);
            var defender = _game.State.Formation(engagement.DefenderFormationId);
            var range = attacker.Position.DistanceTo(defender.Position);
            foreach (var source in attacker.ActiveUnits.Where(unit => unit.AvailableShortSsm > 0 || unit.AvailableLongSsm > 0))
            {
                GUILayout.Label($"{ShortUnitName(source.Definition.DisplayName)}  ·  AVAILABLE SR {source.AvailableShortSsm} / LR {source.AvailableLongSsm}",
                    _cardStatStyle);
                foreach (var target in defender.ActiveUnits)
                {
                    var key = source.Definition.Id + "|" + target.Definition.Id;
                    if (!_missileDraft.TryGetValue(key, out var factors))
                        _missileDraft[key] = factors = new int[2];
                    var usedShort = _missileDraft.Where(item => item.Key.StartsWith(source.Definition.Id + "|"))
                        .Sum(item => item.Value[0]);
                    var usedLong = _missileDraft.Where(item => item.Key.StartsWith(source.Definition.Id + "|"))
                        .Sum(item => item.Value[1]);
                    GUILayout.Label("TARGET: " + ShortUnitName(target.Definition.DisplayName), _cardStatStyle);
                    GUILayout.BeginHorizontal();
                    GUI.enabled = factors[0] > 0;
                    if (GUILayout.Button("−", _buttonStyle, GUILayout.Width(34f))) factors[0]--;
                    GUI.enabled = CanLocalAct() && range <= 1 && usedShort < source.AvailableShortSsm;
                    if (GUILayout.Button($"SR {factors[0]}  +", _buttonStyle)) factors[0]++;
                    GUI.enabled = factors[1] > 0;
                    if (GUILayout.Button("−", _buttonStyle, GUILayout.Width(34f))) factors[1]--;
                    GUI.enabled = CanLocalAct() && range <= 3 && usedLong < source.AvailableLongSsm;
                    if (GUILayout.Button($"LR {factors[1]}  +", _buttonStyle)) factors[1]++;
                    GUI.enabled = true;
                    GUILayout.EndHorizontal();
                }
            }
            var allocations = _missileDraft.Where(item => item.Value[0] + item.Value[1] > 0)
                .Select((item, index) =>
                {
                    var ids = item.Key.Split('|');
                    return new MissileAllocationData
                    {
                        id = $"SALVO-{_game.State.Revision}-{index + 1}",
                        sourceUnitId = ids[0],
                        targetUnitId = ids[1],
                        shortFactors = item.Value[0],
                        longFactors = item.Value[1]
                    };
                }).ToArray();
            GUI.enabled = CanLocalAct() && allocations.Length > 0;
            if (GUILayout.Button($"LAUNCH {allocations.Sum(item => item.shortFactors + item.longFactors)} ALLOCATED FACTOR(S)", _buttonStyle))
                SubmitCombatCommand(new GameCommand(GameCommandType.AllocateMissileFire, LocalSide,
                    _game.State.Revision, missileAllocations: allocations), "Missile allocation sent to host.");
            GUI.enabled = true;
        }

        private void DrawDefensiveDeployment(MissileEngagement engagement)
        {
            var defender = _game.State.Formation(engagement.DefenderFormationId);
            GUILayout.Label("Select two ships to form each mutual-defense pair. An odd ship may remain unpaired.", _cardStatStyle);
            foreach (var pair in _defensePairDraft)
                GUILayout.Label($"PAIR: {UnitName(defender, pair.firstUnitId)} + {UnitName(defender, pair.secondUnitId)}", _cardStatStyle);
            var paired = new HashSet<string>(_defensePairDraft.SelectMany(pair =>
                new[] { pair.firstUnitId, pair.secondUnitId }));
            foreach (var ship in defender.ActiveUnits.Where(unit => !paired.Contains(unit.Definition.Id)))
            {
                var selected = _pairSelection == ship.Definition.Id;
                var oldColor = GUI.backgroundColor;
                if (selected) GUI.backgroundColor = new Color(0.95f, 0.72f, 0.18f);
                if (GUILayout.Button((selected ? "SELECTED: " : "PAIR: ") + ship.Definition.DisplayName.ToUpperInvariant(), _buttonStyle))
                {
                    if (string.IsNullOrEmpty(_pairSelection)) _pairSelection = ship.Definition.Id;
                    else if (_pairSelection != ship.Definition.Id)
                    {
                        _defensePairDraft.Add(new DefensePairData
                        {
                            firstUnitId = _pairSelection,
                            secondUnitId = ship.Definition.Id
                        });
                        _pairSelection = string.Empty;
                    }
                }
                GUI.backgroundColor = oldColor;
            }
            GUILayout.BeginHorizontal();
            GUI.enabled = _defensePairDraft.Count > 0 || !string.IsNullOrEmpty(_pairSelection);
            if (GUILayout.Button("CLEAR PAIRS", _buttonStyle))
            {
                _defensePairDraft.Clear();
                _pairSelection = string.Empty;
            }
            GUI.enabled = CanLocalAct();
            if (GUILayout.Button("DEPLOY DEFENSE", _buttonStyle))
                SubmitCombatCommand(new GameCommand(GameCommandType.Defend, LocalSide,
                    _game.State.Revision, defensePairs: _defensePairDraft.ToArray()), "Defensive deployment sent to host.");
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        private void DrawLongRangeRemovals(MissileEngagement engagement)
        {
            GUILayout.Label($"Long-range SAM scored {engagement.LongRangeHits} removal(s). Choose which salvos lose factors.", _cardStatStyle);
            foreach (var salvo in engagement.Salvos.Where(item => item.RemainingFactors > 0))
            {
                if (!_longRangeRemovalDraft.ContainsKey(salvo.Id)) _longRangeRemovalDraft[salvo.Id] = 0;
                GUILayout.Label($"{salvo.Id} → {salvo.TargetUnitId}  ·  {salvo.RemainingFactors} factor(s)", _cardStatStyle);
                GUILayout.BeginHorizontal();
                GUI.enabled = _longRangeRemovalDraft[salvo.Id] > 0;
                if (GUILayout.Button("−", _buttonStyle, GUILayout.Width(42f))) _longRangeRemovalDraft[salvo.Id]--;
                GUILayout.Label($"REMOVE {_longRangeRemovalDraft[salvo.Id]}", _cardHeaderStyle);
                var assigned = _longRangeRemovalDraft.Values.Sum();
                GUI.enabled = assigned < engagement.LongRangeHits &&
                              _longRangeRemovalDraft[salvo.Id] < salvo.RemainingFactors;
                if (GUILayout.Button("+", _buttonStyle, GUILayout.Width(42f))) _longRangeRemovalDraft[salvo.Id]++;
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            var total = _longRangeRemovalDraft.Values.Sum();
            GUI.enabled = CanLocalAct() && total == engagement.LongRangeHits;
            if (GUILayout.Button($"CONFIRM LR SAM REMOVALS  {total}/{engagement.LongRangeHits}", _buttonStyle))
                SubmitCombatCommand(new GameCommand(GameCommandType.Defend, LocalSide,
                    _game.State.Revision, missileReductions: _longRangeRemovalDraft.Where(item => item.Value > 0)
                    .Select(item => new MissileReductionData { salvoId = item.Key, factors = item.Value }).ToArray()),
                    "Long-range SAM removals sent to host.");
            GUI.enabled = true;
        }

        private void DrawShortRangeDefense(MissileEngagement engagement)
        {
            var defender = _game.State.Formation(engagement.DefenderFormationId);
            GUILayout.Label("Assign each short-range SAM battery to one salvo attacking itself or its pair-mate.", _cardStatStyle);
            foreach (var ship in defender.ActiveUnits.Where(unit => unit.EffectiveShortSam > 0))
            {
                var mate = engagement.PairMate(ship.Definition.Id);
                var legal = engagement.Salvos.Where(salvo => salvo.RemainingFactors > 0 &&
                    (salvo.TargetUnitId == ship.Definition.Id || salvo.TargetUnitId == mate)).ToArray();
                if (legal.Length == 0) continue;
                GUILayout.Label($"{ShortUnitName(ship.Definition.DisplayName)}  ·  SR SAM {ship.EffectiveShortSam}", _cardStatStyle);
                foreach (var salvo in legal)
                {
                    var selected = _shortRangeDefenseDraft.TryGetValue(ship.Definition.Id, out var salvoId) && salvoId == salvo.Id;
                    var oldColor = GUI.backgroundColor;
                    if (selected) GUI.backgroundColor = new Color(0.18f, 0.78f, 0.92f);
                    if (GUILayout.Button($"{(selected ? "ASSIGNED" : "ENGAGE")} {salvo.Id} → {salvo.TargetUnitId}", _buttonStyle))
                        _shortRangeDefenseDraft[ship.Definition.Id] = salvo.Id;
                    GUI.backgroundColor = oldColor;
                }
            }
            GUI.enabled = CanLocalAct();
            if (GUILayout.Button("FIRE SHORT-RANGE SAM / RESOLVE RAID", _buttonStyle))
                SubmitCombatCommand(new GameCommand(GameCommandType.Defend, LocalSide,
                    _game.State.Revision, shortRangeDefenses: _shortRangeDefenseDraft.Select(item =>
                    new ShortRangeDefenseData { defendingUnitId = item.Key, salvoId = item.Value }).ToArray()),
                    "Short-range defense assignments sent to host.");
            GUI.enabled = true;
        }

        private void DrawCounterattackDecision(MissileEngagement engagement)
        {
            GUILayout.Label("The non-moving force may launch its missile counterattack now, before the moving formation resumes.", _cardStatStyle);
            GUILayout.BeginHorizontal();
            GUI.enabled = CanLocalAct();
            if (GUILayout.Button("COUNTERATTACK", _buttonStyle))
                SubmitCombatCommand(new GameCommand(GameCommandType.Counterattack, LocalSide,
                    _game.State.Revision, enabled: true), "Counterattack decision sent to host.");
            if (GUILayout.Button("DECLINE", _buttonStyle))
                SubmitCombatCommand(new GameCommand(GameCommandType.Counterattack, LocalSide,
                    _game.State.Revision, enabled: false), "Counterattack declined.");
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        private static string UnitName(TaskForceState force, string unitId) => force.Units
            .FirstOrDefault(unit => unit.Definition.Id == unitId)?.Definition.DisplayName ?? unitId;

        private static string CombatStageLabel(MissileCombatPhase phase)
        {
            switch (phase)
            {
                case MissileCombatPhase.AllocateFire: return "1 / ALLOCATE FIRE";
                case MissileCombatPhase.DefensiveDeployment: return "2 / DEPLOY & LR SAM";
                case MissileCombatPhase.LongRangeRemoval: return "3 / ASSIGN LR HITS";
                case MissileCombatPhase.ShortRangeDefense: return "4 / SR SAM, PD & IMPACT";
                default: return "COUNTERATTACK DECISION";
            }
        }

        private static string ShortUnitName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "UNIT";
            return name.Length <= 12 ? name.ToUpperInvariant() : name.Substring(0, 12).ToUpperInvariant();
        }

        private void DrawActivationRibbon()
        {
            const float width = 390f;
            const float height = 54f;
            var left = (Screen.width - width) * 0.5f;
            var active = _game.State.ActiveSide;
            var oldColor = GUI.color;
            GUI.color = active == Side.UsNavy
                ? new Color(0.12f, 0.48f, 0.86f, 0.94f)
                : new Color(0.82f, 0.18f, 0.14f, 0.94f);
            GUI.Box(new Rect(left, 18f, width, height), GUIContent.none);
            GUI.color = oldColor;

            var ownership = _game.State.IsGameOver
                ? "ACTION COMPLETE"
                : _game.State.Phase == ActivationPhase.AwaitingChit ? "AWAITING FIRST DRAW"
                : active == LocalSide ? "YOUR COMMAND" : "OPPONENT COMMAND";
            var side = _game.State.Phase == ActivationPhase.AwaitingChit ? "MOVEMENT CUP"
                : active == Side.UsNavy ? "US NAVY" : "PLAN";
            GUI.Label(new Rect(left + 8f, 22f, width - 16f, 28f),
                $"{side}{(_game.State.Phase == ActivationPhase.AwaitingChit ? string.Empty : " ACTIVATION")}  -  {ownership}", _activationStyle);
            GUI.Label(new Rect(left + 8f, 47f, width - 16f, 20f),
                $"{_game.State.TimeLabel.ToUpperInvariant()}  -  CUP {_game.State.MovementCup.Remaining.Count}/{_game.State.MovementCup.TotalCount}  -  REV {_game.State.Revision:0000}",
                _cardStatStyle);
        }

        private void DrawChitBanner()
        {
            if (Time.unscaledTime >= _chitBannerUntil || string.IsNullOrEmpty(_chitBanner)) return;
            const float width = 360f;
            const float height = 66f;
            var left = (Screen.width - width) * 0.5f;
            var top = 86f;
            var remaining = Mathf.Clamp01((_chitBannerUntil - Time.unscaledTime) / 2.2f);
            var oldColor = GUI.color;
            GUI.color = new Color(0.08f, 0.16f, 0.22f, 0.75f + remaining * 0.2f);
            GUI.Box(new Rect(left, top, width, height), GUIContent.none);
            GUI.color = oldColor;
            GUI.Label(new Rect(left + 10f, top + 8f, width - 20f, 28f), "CHIT DRAWN", _activationStyle);
            GUI.Label(new Rect(left + 10f, top + 34f, width - 20f, 24f), _chitBanner, _activationStyle);
        }

        private void DrawMissileCombatRibbon()
        {
            var engagement = _game.State.PendingMissileCombat;
            if (_game.State.Phase != ActivationPhase.MissileCombat || engagement == null) return;
            const float width = 470f;
            const float height = 58f;
            var left = (Screen.width - width) * 0.5f;
            const float top = 158f;
            var oldColor = GUI.color;
            GUI.color = new Color(0.45f, 0.08f, 0.06f, 0.93f);
            GUI.Box(new Rect(left, top, width, height), GUIContent.none);
            GUI.color = oldColor;
            GUI.Label(new Rect(left + 8f, top + 5f, width - 16f, 24f),
                "MISSILE EXCHANGE  ·  " + CombatStageLabel(engagement.Phase), _activationStyle);
            GUI.Label(new Rect(left + 8f, top + 31f, width - 16f, 20f),
                $"{engagement.AttackerFormationId} → {engagement.DefenderFormationId}  ·  RAID {engagement.RemainingFactors}/{engagement.InitialFactors}",
                _cardStatStyle);
        }

        private void DrawGunCombatRibbon()
        {
            var engagement = _game.State.PendingGunCombat;
            if (_game.State.Phase != ActivationPhase.GunCombat || engagement == null) return;
            const float width = 500f;
            const float height = 62f;
            var left = (Screen.width - width) * 0.5f;
            const float top = 158f;
            var oldColor = GUI.color;
            GUI.color = new Color(0.78f, 0.29f, 0.035f, 0.94f);
            GUI.Box(new Rect(left, top, width, height), GUIContent.none);
            GUI.color = oldColor;
            GUI.Label(new Rect(left + 8f, top + 5f, width - 16f, 25f),
                $"CLOSE ACTION  ·  GUNFIRE ROUND {engagement.Round}", _activationStyle);
            GUI.Label(new Rect(left + 8f, top + 32f, width - 16f, 21f),
                $"{engagement.AttackerFormationId} ↔ {engagement.DefenderFormationId}  ·  {engagement.Phase}",
                _cardStatStyle);
        }

        private void DrawMultiplayerLobby()
        {
            const float width = 620f;
            var height = Mathf.Min(720f, Screen.height - 60f);
            var left = (Screen.width - width) * 0.5f;
            var top = (Screen.height - height) * 0.5f;
            GUI.Box(new Rect(left, top, width, height), GUIContent.none);
            var sideSelectorColor = GUI.color;
            GUI.color = new Color(0.1f, 0.76f, 0.92f, 0.95f);
            GUI.Box(new Rect(left, top, width, 5f), GUIContent.none);
            GUI.color = sideSelectorColor;
            GUILayout.BeginArea(new Rect(left + 24f, top + 22f, width - 48f, height - 42f));
            _lobbyScroll = GUILayout.BeginScrollView(_lobbyScroll);
            GUILayout.Label("ONE vs ONE - PUBLIC & DIRECT", _titleStyle);
            GUILayout.Label("The host owns the authoritative rules state and chooses a side. The joining player is assigned the opposing side.", _labelStyle);
            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.Label("HOST COMMAND", _cardHeaderStyle, GUILayout.Width(124f));
            var oldColor = GUI.color;
            GUI.enabled = _sessionMode == SessionMode.SinglePlayer;
            GUI.color = _hostSideChoice == Side.UsNavy ? new Color(0.32f, 0.68f, 1f) : Color.white;
            if (GUILayout.Button("US NAVY", _buttonStyle)) _hostSideChoice = Side.UsNavy;
            GUI.color = _hostSideChoice == Side.Plan ? new Color(1f, 0.38f, 0.3f) : Color.white;
            if (GUILayout.Button("PLAN", _buttonStyle)) _hostSideChoice = Side.Plan;
            GUI.color = oldColor;
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.Label($"Host: {SideLabel(_hostSideChoice)}    Joining player: {SideLabel(OpposingSide(_hostSideChoice))}", _cardStatStyle);
            GUILayout.Space(8f);
            GUILayout.Label("PUBLIC RELAY (RECOMMENDED)", _debugHeaderStyle);
            GUILayout.Label("Encrypted DTLS, anonymous Unity authentication, join codes, discovery, and reconnect. No IP address or router setup is shared with the opponent.", _labelStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("SESSION", _cardStatStyle, GUILayout.Width(82f));
            _publicSessionName = GUILayout.TextField(_publicSessionName, 64, GUILayout.Height(27f));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("PASSWORD", _cardStatStyle, GUILayout.Width(82f));
            _publicPassword = GUILayout.PasswordField(_publicPassword, '*', 64, GUILayout.Height(27f));
            GUILayout.EndHorizontal();
            _publicDiscoverable = GUILayout.Toggle(_publicDiscoverable, " List this match in the public session browser", _labelStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("HOST PUBLIC", _buttonStyle)) StartPublicHost();
            if (GUILayout.Button("REFRESH BROWSER", _buttonStyle)) _publicNetwork.RefreshListings();
            if (GUILayout.Button("RECONNECT", _buttonStyle))
            {
                _sessionMode = SessionMode.PublicClient;
                _publicNetwork.Reconnect();
                _showMultiplayer = false;
                Restart();
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("JOIN CODE", _cardStatStyle, GUILayout.Width(82f));
            _joinCode = GUILayout.TextField(_joinCode, 12, GUILayout.Height(27f));
            if (GUILayout.Button("JOIN BY CODE", _buttonStyle, GUILayout.Width(170f))) JoinPublicByCode();
            GUILayout.EndHorizontal();
            if (_publicNetwork.JoinCode.Length > 0)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("HOST CODE: " + _publicNetwork.JoinCode, _debugHeaderStyle);
                if (GUILayout.Button("COPY", _buttonStyle, GUILayout.Width(72f)))
                    GUIUtility.systemCopyBuffer = _publicNetwork.JoinCode;
                GUILayout.EndHorizontal();
            }
            foreach (var listing in _publicNetwork.Listings.Take(4))
            {
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.Label($"{listing.Name} - {listing.AvailableSlots} slot - {(listing.HasPassword ? "locked" : "open")}", _cardStatStyle);
                if (GUILayout.Button("JOIN", _buttonStyle, GUILayout.Width(70f))) JoinPublicListing(listing.Id);
                GUILayout.EndHorizontal();
            }
            foreach (var notification in _publicNetwork.ServiceNotifications.Take(2))
                GUILayout.Label("UNITY SERVICE NOTICE: " + notification, _labelStyle);
            GUILayout.Label("RELAY STATUS - " + _publicNetwork.Status, _debugStyle);
            GUILayout.Space(6f);
            GUILayout.Label("DIRECT IP (LOCAL / TRUSTED LAN)", _cardHeaderStyle);
            GUILayout.Label("HOST IP ADDRESS", _cardHeaderStyle);
            GUILayout.BeginHorizontal();
            _ipAddress = GUILayout.TextField(_ipAddress, 64, GUILayout.Height(28f));
            _portText = GUILayout.TextField(_portText, 5, GUILayout.Width(72f), GUILayout.Height(28f));
            if (GUILayout.Button("HOST DIRECT", _buttonStyle)) StartHosting();
            if (GUILayout.Button("JOIN DIRECT", _buttonStyle)) JoinHost();
            GUILayout.EndHorizontal();
            if (_sessionMode != SessionMode.SinglePlayer && GUILayout.Button("DISCONNECT / SOLO", _buttonStyle))
                ReturnToSinglePlayer();
            GUILayout.Label("Direct status - " + _network.Status, _cardStatStyle);
            if (GUILayout.Button("CLOSE", _buttonStyle)) _showMultiplayer = false;
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawChatAndSoundboard()
        {
            const float panelWidth = 384f;
            const float top = 680f;
            var height = Mathf.Max(202f, Screen.height - top - 18f);
            GUI.Box(new Rect(Screen.width - panelWidth - 18f, top, panelWidth, height), GUIContent.none);
            GUILayout.BeginArea(new Rect(Screen.width - panelWidth - 2f, top + 12f, panelWidth - 32f, height - 22f));
            GUILayout.Label("COMMS - CHAT & SOUNDBOARD", _cardHeaderStyle);
            _muteOpponent = GUILayout.Toggle(_muteOpponent, " Mute opponent chat and sounds", _cardStatStyle);
            _chatScroll = GUILayout.BeginScrollView(_chatScroll, GUI.skin.box, GUILayout.MinHeight(62f));
            foreach (var line in _chat.Skip(System.Math.Max(0, _chat.Count - 40)))
                GUILayout.Label(line, _cardStatStyle);
            GUILayout.EndScrollView();
            GUILayout.BeginHorizontal();
            _chatInput = GUILayout.TextField(_chatInput, 180, GUILayout.Height(28f));
            if (GUILayout.Button("SEND", _buttonStyle, GUILayout.Width(72f))) SendChat();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            for (var index = 0; index < SoundboardNames.Length; index++)
            {
                var soundId = index;
                if (GUILayout.Button($"S{index + 1}", _buttonStyle, GUILayout.Width(54f))) SendSoundboard(soundId);
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("S1 Sink - S2 Incoming - S3 Brace - S4 Good hunting", _cardStatStyle);
            GUILayout.EndArea();
        }

        private void DrawDebugPanel()
        {
            var left = 406f;
            var width = Mathf.Max(420f, Screen.width - 812f);
            var height = Mathf.Max(360f, Screen.height - 48f);
            GUI.Box(new Rect(left, 24f, width, height), GUIContent.none);
            var oldColor = GUI.color;
            GUI.color = new Color(0.08f, 0.78f, 0.92f, 0.95f);
            GUI.Box(new Rect(left, 24f, width, 4f), GUIContent.none);
            GUI.color = oldColor;

            GUILayout.BeginArea(new Rect(left + 16f, 38f, width - 32f, height - 28f));
            if (_game.State.DetectionRulesEnabled && !_game.State.IsGameOver)
            {
                GUILayout.Label("AUTHORITATIVE TRACE SEALED", _debugHeaderStyle);
                GUILayout.Label("Scenario 4 contains hidden positions and formation state. The complete transaction trace becomes available after the scenario ends.", _labelStyle);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("CLOSE", _buttonStyle)) _showDebug = false;
                GUILayout.EndArea();
                return;
            }
            GUILayout.BeginHorizontal();
            GUILayout.Label("RULES TRANSACTION TRACE", _debugHeaderStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"{_game.State.Transactions.Count} TRANSACTIONS", _labelStyle);
            if (GUILayout.Button("COPY ALL", _buttonStyle, GUILayout.Width(108f)))
                GUIUtility.systemCopyBuffer = string.Join("\n", _game.State.Transactions.Select(item => item.ToString()));
            if (GUILayout.Button("COPY MATCH", _buttonStyle, GUILayout.Width(118f)))
                GUIUtility.systemCopyBuffer = MatchLogText();
            if (GUILayout.Button("EXPORT", _buttonStyle, GUILayout.Width(86f))) ExportMatchLog();
            if (GUILayout.Button("CLOSE", _buttonStyle, GUILayout.Width(82f))) _showDebug = false;
            GUILayout.EndHorizontal();
            GUILayout.Label("Complete deterministic rules activity. Rejected commands and every die result are retained.", _labelStyle);
            GUI.enabled = _sessionMode == SessionMode.SinglePlayer || _sessionMode == SessionMode.HotSeat;
            if (GUILayout.Button(_detectionTestMode
                    ? "DETECTION TEST MODE: ON  (RESTART IN SCENARIO MODE)"
                    : "DETECTION TEST MODE: OFF  (RESTART WITH SECTION 5 RULES)", _buttonStyle))
            {
                _detectionTestMode = !_detectionTestMode;
                Restart();
                _status = _detectionTestMode
                    ? "Detection Test Mode enabled: declare radar before speed and establish contact before attacking."
                    : $"{_game.State.Scenario.Name} detection exemption restored.";
            }
            GUI.enabled = true;
            GUILayout.Space(7f);

            if (_game.State.Transactions.Count != _lastDebugCount)
            {
                _lastDebugCount = _game.State.Transactions.Count;
                _debugScroll.y = float.MaxValue;
            }
            _debugScroll = GUILayout.BeginScrollView(_debugScroll, GUI.skin.box);
            foreach (var transaction in _game.State.Transactions)
                GUILayout.Label(transaction.ToString(), _debugStyle);
            GUILayout.EndScrollView();
            GUILayout.Space(4f);
            GUILayout.Label("F3 toggles this trace. Escape closes it before exiting the game.", _labelStyle);
            GUILayout.EndArea();
        }

        private void DrawFormationCards()
        {
            var panelWidth = 384f;
            var availableHeight = Mathf.Max(300f, Screen.height - 36f);
            var panelHeight = _sessionMode == SessionMode.SinglePlayer || _sessionMode == SessionMode.HotSeat
                ? availableHeight : Mathf.Min(644f, availableHeight);
            GUI.Box(new Rect(Screen.width - panelWidth - 18f, 18f, panelWidth, panelHeight), GUIContent.none);
            GUILayout.BeginArea(new Rect(Screen.width - panelWidth - 8f, 28f, panelWidth - 20f, panelHeight - 20f));
            GUILayout.Label("FORMATION CARDS", _titleStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("US NAVY", _buttonStyle)) SelectFormation(Side.UsNavy);
            if (GUILayout.Button("PLAN", _buttonStyle)) SelectFormation(Side.Plan);
            GUILayout.EndHorizontal();
            GUILayout.Space(8);
            _formationPanelScroll = GUILayout.BeginScrollView(_formationPanelScroll, false, true);

            var sideForces = _game.State.Forces.Where(candidate => candidate.Side == _selectedFormation &&
                (!_game.State.DetectionRulesEnabled || candidate.Side == LocalSide ||
                 _game.State.Detection.IsDetected(LocalSide, candidate.Id))).ToArray();
            if (sideForces.Length == 0)
            {
                GUILayout.Label("NO DETECTED CONTACTS", _cardHeaderStyle);
                GUILayout.Label("Radiate SSR in the same hex, use ESM against an adjacent radiating enemy, or conduct a daytime visual search in its hex.", _cardStatStyle);
                GUILayout.Label("Enemy formation cards remain hidden until classified.", _labelStyle);
                GUILayout.EndScrollView();
                GUILayout.EndArea();
                return;
            }
            if (sideForces.Length > 1)
            {
                GUILayout.Label("TASK FORCES", _cardStatStyle);
                GUILayout.BeginHorizontal();
                foreach (var candidate in sideForces)
                {
                    var selectedForce = candidate;
                    var oldColor = GUI.backgroundColor;
                    if (candidate.Id == _selectedFormationId)
                        GUI.backgroundColor = _selectedFormation == Side.UsNavy
                            ? new Color(0.25f, 0.62f, 1f) : new Color(1f, 0.32f, 0.26f);
                    if (GUILayout.Button(ShortFormationName(candidate.Id), _buttonStyle))
                        SelectFormation(candidate.Side, candidate.Id);
                    GUI.backgroundColor = oldColor;
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(5);
            }

            var force = _game.State.Formation(_selectedFormationId) ?? sideForces.First();
            var sideColor = _selectedFormation == Side.UsNavy
                ? new Color(0.32f, 0.68f, 1f) : new Color(1f, 0.38f, 0.3f);
            var previousHeaderColor = _cardHeaderStyle.normal.textColor;
            _cardHeaderStyle.normal.textColor = sideColor;
            var contact = force.Side == LocalSide ? null : _game.State.Detection.ContactFor(LocalSide, force.Id);
            var contactText = !_game.State.DetectionRulesEnabled ? "SCENARIO CONTACT: CLASSIFIED"
                : contact == null ? "CONTACT: UNDETECTED"
                : $"CONTACT: {contact.Level.ToString().ToUpperInvariant()} / {contact.Method.ToString().ToUpperInvariant()}";
            GUILayout.Label($"{force.Id.ToUpperInvariant()} · HEX {force.Position}", _cardHeaderStyle);
            GUILayout.Label($"SSR {(force.RadarRadiating ? "RADIATING" : "SILENT")} · {contactText}", _cardStatStyle);
            GUILayout.Label(force.DeclaredSpeed < 0
                ? $"MAX SPEED {force.EffectiveSpeed} · AWAITING DECLARATION"
                : $"DECLARED {force.DeclaredSpeed} · MOVED {force.MovementPointsSpent} · REMAINING {force.MovementRemaining}",
                _cardStatStyle);
            if (force.DefensePairs.Count > 0)
                GUILayout.Label("DEFENSE PAIRS   " + string.Join("  |  ", force.DefensePairs.Select(pair =>
                    $"{ShortUnitName(UnitName(force, pair.firstUnitId))} + {ShortUnitName(UnitName(force, pair.secondUnitId))}")),
                    _cardStatStyle);
            else
                GUILayout.Label("DEFENSE PAIRS   NOT YET DEPLOYED", _cardStatStyle);
            _cardHeaderStyle.normal.textColor = previousHeaderColor;

            foreach (var unit in force.Units) DrawUnitCard(unit, sideColor);
            GUILayout.Label("Click either 3D formation or use the tabs above.", _labelStyle);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawUnitCard(UnitState unit, Color sideColor)
        {
            var previousBackground = GUI.backgroundColor;
            GUI.backgroundColor = unit.IsSunk ? new Color(0.32f, 0.12f, 0.1f)
                : unit.HasTwoThirdsDamage ? new Color(0.48f, 0.16f, 0.08f)
                : unit.HasHalfDamage ? new Color(0.5f, 0.34f, 0.08f) : previousBackground;
            GUILayout.BeginVertical(GUI.skin.box);
            GUI.backgroundColor = previousBackground;
            var oldColor = _cardHeaderStyle.normal.textColor;
            _cardHeaderStyle.normal.textColor = unit.IsSunk ? Color.gray : sideColor;
            GUILayout.Label(unit.Definition.DisplayName.ToUpperInvariant(), _cardHeaderStyle);
            _cardHeaderStyle.normal.textColor = oldColor;
            GUILayout.Label($"{unit.Definition.Role} · {DamageStateLabel(unit)}", _cardStatStyle);
            GUILayout.Space(3);
            GUILayout.Label($"HULL   {HullBoxes(unit)}   {unit.HullRemaining}/{unit.Definition.Hull}", _cardStatStyle);
            if (_game.State.Scenario.ScoringMode == ScenarioScoringMode.GunfireHullHits)
            {
                var priorCreditColor = GUI.color;
                GUI.color = unit.GunfireHullDamage > 0
                    ? new Color(1f, 0.62f, 0.22f) : new Color(0.58f, 0.66f, 0.72f);
                GUILayout.Label($"GUNFIRE SCORE CREDIT   {unit.GunfireHullDamage}", _cardStatStyle);
                GUI.color = priorCreditColor;
            }
            GUILayout.Label($"THRESHOLDS   HALF {unit.HalfDamageThreshold} HIT{(unit.HalfDamageThreshold == 1 ? "" : "S")} · TWO-THIRDS {unit.TwoThirdsDamageThreshold}", _cardStatStyle);
            GUILayout.Label($"ASR {EffectiveStat(unit.EffectiveAirSearchRadar, unit.Definition.AirSearchRadar)}    " +
                $"SSR {EffectiveStat(unit.EffectiveSurfaceSearchRadar, unit.Definition.SurfaceSearchRadar)}    " +
                $"SON {EffectiveStat(unit.EffectiveSonar, unit.Definition.Sonar)}    " +
                $"ASW {EffectiveStat(unit.EffectiveAntiSubmarineWarfare, unit.Definition.AntiSubmarineWarfare)}", _cardStatStyle);
            GUILayout.Label($"SAM {EffectiveStat(unit.EffectiveShortSam, unit.Definition.ShortSam)}-" +
                $"{EffectiveStat(unit.EffectiveLongSam, unit.Definition.LongSam)}    " +
                $"PD {EffectiveStat(unit.EffectivePointDefense, unit.Definition.PointDefense)}    " +
                $"GUNS {EffectiveStat(unit.EffectiveGuns, unit.Definition.Guns)}", _cardStatStyle);
            GUILayout.Label($"SSM {unit.AvailableShortSsm}-{unit.AvailableLongSsm} AVAILABLE    " +
                $"SPEED {EffectiveStat(unit.EffectiveSpeed, unit.Definition.Speed)}", _cardStatStyle);
            GUILayout.Label($"MISSILE BOXES   SR {unit.ShortMissilesRemaining} · LR {unit.LongMissilesRemaining}", _cardStatStyle);
            if (unit.Definition.Torpedoes > 0)
                GUILayout.Label($"TORPEDOES   {EffectiveStat(unit.EffectiveTorpedoes, unit.Definition.Torpedoes)}", _cardStatStyle);
            if (unit.Definition.IsAircraftCarrier)
                GUILayout.Label("AIRCRAFT LAUNCH   " + (unit.CanLaunchAircraft ? "READY" : "PROHIBITED BY DAMAGE"),
                    _cardStatStyle);
            GUILayout.EndVertical();
            GUILayout.Space(6);
        }

        private static string EffectiveStat(int effective, int printed) =>
            effective == printed ? printed.ToString() : printed + "→" + effective;

        private static string DamageStateLabel(UnitState unit)
        {
            switch (unit.DamageLevel)
            {
                case ShipDamageLevel.HalfDamage: return "HALF DAMAGE · DEGRADED";
                case ShipDamageLevel.TwoThirdsDamage: return "TWO-THIRDS DAMAGE · MISSION KILL";
                case ShipDamageLevel.Sunk: return "SUNK · ALL CAPABILITIES LOST";
                default: return "OPERATIONAL";
            }
        }

        private static string HullBoxes(UnitState unit)
        {
            var boxes = string.Empty;
            for (var index = 0; index < unit.Definition.Hull; index++)
                boxes += index < unit.HullRemaining ? "■" : "□";
            return boxes;
        }

        private void DrawForce(TaskForceState force)
        {
            var activity = force.Id == _game.State.ActiveFormationId ? "  [ACTIVE]" : string.Empty;
            if (_game.State.DetectionRulesEnabled && force.Side != LocalSide &&
                !_game.State.Detection.IsDetected(LocalSide, force.Id))
            {
                GUILayout.Label($"UNKNOWN {SideLabel(force.Side)} CONTACT · HEX {force.Position}{activity}", _labelStyle);
                GUILayout.Label("  Formation contents undetected", _labelStyle);
                return;
            }
            GUILayout.Label($"{force.Id} · HEX {force.Position}{activity}", _labelStyle);
            foreach (var unit in force.Units)
                GUILayout.Label($"  {unit.Definition.DisplayName}: {unit.HullRemaining}/{unit.Definition.Hull} hull · {DamageStateLabel(unit)}", _labelStyle);
        }

        private static string ShortFormationName(string formationId)
        {
            if (string.IsNullOrWhiteSpace(formationId)) return "FORMATION";
            var words = formationId.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return words.Length <= 2 ? formationId.ToUpperInvariant() : string.Join(" ", words.Skip(words.Length - 2)).ToUpperInvariant();
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null) return;
            _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            _titleStyle.normal.textColor = new Color(0.87f, 0.94f, 1f);
            _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
            _labelStyle.normal.textColor = new Color(0.85f, 0.9f, 0.94f);
            _buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 14, fixedHeight = 34f };
            _sectionHeaderStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                fixedHeight = 32f,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(10, 8, 4, 4)
            };
            _sectionHeaderStyle.normal.textColor = Color.white;
            _sectionHeaderStyle.hover.textColor = Color.white;
            _sectionHeaderStyle.active.textColor = Color.white;
            _cardHeaderStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, wordWrap = true };
            _cardHeaderStyle.normal.textColor = new Color(0.87f, 0.94f, 1f);
            _cardStatStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            _cardStatStyle.normal.textColor = new Color(0.82f, 0.87f, 0.9f);
            _tooltipStyle = new GUIStyle(GUI.skin.box) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _tooltipStyle.normal.textColor = new Color(0.9f, 0.98f, 1f);
            _debugHeaderStyle = new GUIStyle(_titleStyle) { fontSize = 18 };
            _debugHeaderStyle.normal.textColor = new Color(0.18f, 0.88f, 1f);
            _activationStyle = new GUIStyle(_cardHeaderStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 15
            };
            _activationStyle.normal.textColor = Color.white;
            _debugStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                font = Font.CreateDynamicFontFromOSFont("Consolas", 12),
                wordWrap = true,
                richText = false,
                padding = new RectOffset(7, 7, 3, 3)
            };
            _debugStyle.normal.textColor = new Color(0.72f, 0.94f, 0.98f);
        }

        private void OnDestroy()
        {
            _network.Dispose();
            _publicNetwork.Dispose();
        }

        private void QuitGame()
        {
            _network.Stop();
            _publicNetwork.Stop();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private static void ToggleFullscreen()
        {
            if (Screen.fullScreen)
                Screen.SetResolution(1600, 900, FullScreenMode.Windowed);
            else
                EnterBorderlessFullscreen();
        }

        private static void EnterBorderlessFullscreen()
        {
            var display = Display.main;
            Screen.SetResolution(display.systemWidth, display.systemHeight,
                FullScreenMode.FullScreenWindow);
        }
    }
}
