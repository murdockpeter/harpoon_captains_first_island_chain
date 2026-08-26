using System;
using System.Collections.Generic;
using System.Linq;

namespace Harpoon.Core
{
    [Serializable]
    public sealed class UnitSnapshot
    {
        public string id;
        public int hullDamage;
        public int shortMissiles;
        public int longMissiles;
    }

    [Serializable]
    public sealed class TransactionSnapshot
    {
        public int sequence;
        public int turn;
        public ActivationPhase phase;
        public string category;
        public string detail;
    }

    [Serializable]
    public sealed class RuleEventSnapshot
    {
        public int sequence;
        public int revision;
        public int turn;
        public ActivationPhase phase;
        public RuleEventType type;
        public Side actor;
        public string commandId;
        public string detail;
    }

    [Serializable]
    public sealed class HexCoordSnapshot
    {
        public int column;
        public int row;
    }

    [Serializable]
    public sealed class FormationSnapshot
    {
        public string id;
        public Side side;
        public int column;
        public int row;
        public int declaredSpeed;
        public int movementSpent;
        public HexCoordSnapshot[] movementPath;
        public string[] unitIds;
        public bool radarRadiating;
        public bool radarDeclared;
        public DefensePairData[] defensePairs;
    }

    [Serializable]
    public sealed class ScenarioOneSnapshot
    {
        public string scenarioId;
        public int seed;
        public bool detectionRulesEnabled;
        public int revision;
        public int turn;
        public ActivationPhase phase;
        public Side activeSide;
        public string activeFormationId;
        public bool usActivated;
        public bool planActivated;
        public bool hasMoved;
        public bool hasAttacked;
        public bool hasSearched;
        public bool gameOver;
        public string result;
        public ScenarioEndReason endReason;
        public bool usRequestedScoring;
        public bool planRequestedScoring;
        public int usColumn;
        public int usRow;
        public int planColumn;
        public int planRow;
        public int usDeclaredSpeed;
        public int usMovementSpent;
        public int planDeclaredSpeed;
        public int planMovementSpent;
        public HexCoordSnapshot[] usMovementPath;
        public HexCoordSnapshot[] planMovementPath;
        public UnitSnapshot[] units;
        public string[] eventLog;
        public TransactionSnapshot[] transactions;
        public RuleEventSnapshot[] events;
        public GameCommandData[] commands;
        public MovementChitData[] remainingChits;
        public MovementChitData[] drawnChits;
        public FormationSnapshot[] formations;
        public ContactSnapshotData[] contacts;
        public MissileEngagementData missileCombat;
        public GunEngagementData gunCombat;
    }

    public static class ScenarioOne
    {
        public const string Name = "Contact off the Bashi Channel";

        public static GameState Create(bool detectionRulesEnabled = false, ScenarioDefinition definition = null)
        {
            definition ??= FirstIslandChainScenarios.ContactOffBashiChannel;
            var formations = definition.Formations.Select(item => new TaskForceState(item.Id, item.Side, item.Start,
                item.Units.Select(slot => new UnitState(ModernPlatformDatabase.Get(slot.PlatformId)
                    .CreateUnit(item.Side, slot.Role, slot.UnitId))))).ToArray();
            var us = formations.First(item => item.Side == Side.UsNavy);
            var plan = formations.First(item => item.Side == Side.Plan);
            var useDetection = detectionRulesEnabled || definition.DetectionRulesEnabled;
            var state = new GameState(us, plan, definition.MaximumTurns, FirstIslandChainMap.Instance,
                useDetection, definition);
            foreach (var formation in formations.Where(item => item != us && item != plan)) state.AddForce(formation);
            state.Log.Add($"Scenario 1: {definition.Name}");
            state.Log.Add(definition.VictoryText);
            state.Trace("SETUP", $"Scenario '{definition.Id}' loaded from data; " +
                (definition.MaximumTurns == 0 ? "no printed turn limit" : $"{definition.MaximumTurns}-turn limit") +
                "; detection rules " + (useDetection ? "enabled." : "omitted by the printed learning scenario."));
            state.Trace("SETUP", $"US Task Force at {us.Position}: {string.Join(", ", us.Units.Select(unit => unit.Definition.DisplayName))}.");
            state.Trace("SETUP", $"PLAN Task Force at {plan.Position}: {string.Join(", ", plan.Units.Select(unit => unit.Definition.DisplayName))}.");
            return state;
        }
    }

    public sealed class ScenarioOneGame : IRulesEngine
    {
        private readonly IDieRoller _dice;
        private readonly CombatResolver _combat;
        private readonly DetectionResolver _detection;
        private readonly MissileCombatResolver _missileCombat;
        private readonly GunCombatResolver _gunCombat;
        private readonly Func<HexCoord, bool> _isNavigable;
        private bool _manualOpponent;
        private bool _isActivatingEnemy;
        public GameState State { get; private set; }
        public int Seed { get; private set; }
        public event Action<Side, AttackReport> AttackResolved;
        public event Action<GameCommand, CommandResult> CommandProcessed;

        public SideGameView ViewFor(Side viewer, bool? opponentKnown = null) =>
            State.ViewFor(viewer, opponentKnown);

        public ScenarioOneGame(int seed = 2026, Func<HexCoord, bool> isNavigable = null,
            bool manualOpponent = false, bool detectionRulesEnabled = false, IDieRoller dieRoller = null)
        {
            Seed = seed;
            var random = dieRoller ?? new SeededDieRoller(seed);
            _dice = random;
            _isNavigable = isNavigable ?? (_ => true);
            _manualOpponent = manualOpponent;
            State = ScenarioOne.Create(detectionRulesEnabled);
            State.MovementCup = new MovementChitCup(random as IRandomSource ?? new SeededDieRoller(seed));
            _combat = new CombatResolver(_dice, Trace);
            _detection = new DetectionResolver(_dice, Trace);
            _missileCombat = new MissileCombatResolver(_dice, Trace);
            _gunCombat = new GunCombatResolver(_dice, Trace);
            BeginTurn();
        }

        public CommandResult Execute(GameCommand command)
        {
            if (command == null)
                return Publish(null, Reject(null, RuleViolationCode.InvalidPayload, "Command payload is missing."));
            var firstEvent = State.Events.Count;
            if (State.IsGameOver)
                return Publish(command, Reject(command, RuleViolationCode.GameOver, "The game is already over.", firstEvent));
            if (command.ExpectedRevision != State.Revision)
                return Publish(command, Reject(command, RuleViolationCode.StaleRevision,
                    $"Expected revision {command.ExpectedRevision}, but authoritative revision is {State.Revision}.", firstEvent));
            if (State.CommandLog.Any(item => item.id == command.Id))
                return Publish(command, Reject(command, RuleViolationCode.DuplicateCommand,
                    $"Command {command.Id} was already processed.", firstEvent));

            State.CurrentCommand = command;
            var accepted = false;
            RuleViolation violation = null;
            AttackReport attackReport = null;
            try
            {
                switch (command.Type)
                {
                    case GameCommandType.DrawMovementChit:
                        accepted = DrawMovementChitInternal(out violation);
                        break;
                    case GameCommandType.SplitTaskForce:
                        accepted = SplitTaskForceInternal(command, out violation);
                        break;
                    case GameCommandType.DeclareSpeed:
                        accepted = DeclareSpeedInternal(command.Actor, command.DeclaredSpeed, out violation);
                        break;
                    case GameCommandType.Move:
                        accepted = TryMoveInternal(command.Actor, command.Destination, out violation);
                        break;
                    case GameCommandType.RadiateRadar:
                        accepted = RadiateRadarInternal(command, out violation);
                        break;
                    case GameCommandType.Attack:
                        attackReport = AttackInternal(command.Actor, out violation);
                        accepted = violation == null;
                        break;
                    case GameCommandType.AllocateMissileFire:
                        accepted = AllocateMissileFireInternal(command, out violation);
                        break;
                    case GameCommandType.Defend:
                        accepted = DefendMissileAttackInternal(command, out violation, out attackReport);
                        break;
                    case GameCommandType.Counterattack:
                        accepted = CounterattackInternal(command, out violation);
                        break;
                    case GameCommandType.ArrangeGunfire:
                        accepted = ArrangeGunfireInternal(command, out violation);
                        break;
                    case GameCommandType.FireGuns:
                        accepted = FireGunsInternal(command, out violation, out attackReport);
                        break;
                    case GameCommandType.BreakOff:
                        accepted = BreakOffInternal(command, out violation);
                        break;
                    case GameCommandType.Search:
                        accepted = SearchInternal(command, out violation);
                        break;
                    case GameCommandType.EndActivation:
                        accepted = EndActivationInternal(command.Actor, out violation);
                        break;
                    case GameCommandType.Disengage:
                        accepted = DisengageInternal(command.Actor, out violation);
                        break;
                    case GameCommandType.RequestScoring:
                        accepted = RequestScoringInternal(command.Actor, out violation);
                        break;
                    case GameCommandType.Concede:
                        accepted = ConcedeInternal(command.Actor);
                        break;
                    default:
                        violation = new RuleViolation(RuleViolationCode.UnsupportedCommand,
                            $"{command.Type} is represented by the command protocol but is not used by Scenario 1.", "type");
                        Trace("REJECTED", violation.Message);
                        break;
                }

                if (!accepted)
                    return Publish(command, CommandResult.Rejected(violation ?? new RuleViolation(RuleViolationCode.InvalidPayload,
                        "Command could not be applied."), State.Events.Skip(firstEvent)));

                State.Revision++;
                State.CommandLog.Add(command.ToData());
                Trace("COMMAND", $"Accepted {command.Type} from {command.Actor}; revision={State.Revision}.");
            }
            finally { State.CurrentCommand = null; }

            var result = CommandResult.Success(State.Events.Skip(firstEvent), attackReport);
            CommandProcessed?.Invoke(command, result);
            if (!_manualOpponent && !_isActivatingEnemy && !State.IsGameOver && State.ActiveSide == Side.Plan)
                ActivateEnemy();
            return result;
        }

        private CommandResult Publish(GameCommand command, CommandResult result)
        {
            CommandProcessed?.Invoke(command, result);
            return result;
        }

        private CommandResult Reject(GameCommand command, RuleViolationCode code, string message, int firstEvent = -1)
        {
            if (firstEvent < 0) firstEvent = State.Events.Count;
            State.CurrentCommand = command;
            Trace("REJECTED", message);
            State.CurrentCommand = null;
            return CommandResult.Rejected(new RuleViolation(code, message), State.Events.Skip(firstEvent));
        }

        public bool TryMovePlayer(HexCoord destination, out string reason)
            => TryMove(Side.UsNavy, destination, out reason);

        public CommandResult DrawMovementChit(Side actor = Side.UsNavy) =>
            Execute(new GameCommand(GameCommandType.DrawMovementChit, actor, State.Revision));

        private bool DrawMovementChitInternal(out RuleViolation violation)
        {
            violation = null;
            if (State.Phase != ActivationPhase.AwaitingChit || State.MovementCup == null ||
                State.MovementCup.IsEmpty)
            {
                violation = new RuleViolation(RuleViolationCode.CupNotReady,
                    "The movement chit cup is not ready for a draw.", "phase");
                Trace("REJECTED", violation.Message);
                return false;
            }
            var chit = State.MovementCup.Draw();
            State.ActiveFormationId = chit.FormationId;
            State.ActiveSide = chit.Side;
            State.ActiveForce?.ResetActivation();
            State.ActiveForce?.BeginSensorDeclaration();
            State.PlayerHasMoved = false;
            State.PlayerHasAttacked = false;
            State.PlayerHasSearched = false;
            State.Phase = ActivationPhase.DeclareSpeed;
            Trace("CHIT", $"Drew {chit.FormationId} ({chit.Side}); " +
                $"{State.MovementCup.Remaining.Count} chit(s) remain in the cup.");
            AddLog($"{chit.FormationId} chit drawn for {State.TimeLabel}.");
            return true;
        }

        private bool SplitTaskForceInternal(GameCommand command, out RuleViolation violation)
        {
            violation = null;
            if (State.Phase != ActivationPhase.AwaitingChit || State.MovementCup == null ||
                !State.MovementCup.FirstDrawPending)
            {
                violation = new RuleViolation(RuleViolationCode.SplitWindowClosed,
                    "Task forces may split only before the first movement chit is drawn.", "phase");
                Trace("REJECTED", violation.Message);
                return false;
            }
            var source = State.Formation(command.FormationId);
            if (source == null || source.Side != command.Actor ||
                string.IsNullOrWhiteSpace(command.NewFormationId) || State.Formation(command.NewFormationId) != null)
            {
                violation = new RuleViolation(RuleViolationCode.InvalidFormation,
                    "The source or new task-force identity is invalid.", "formationId");
                Trace("REJECTED", violation.Message);
                return false;
            }
            var chosen = new HashSet<string>(command.UnitIds);
            if (chosen.Count == 0 || chosen.Count >= source.Units.Count ||
                chosen.Any(id => source.Units.All(unit => unit.Definition.Id != id)))
            {
                violation = new RuleViolation(RuleViolationCode.InvalidUnitSelection,
                    "A split must move existing units while leaving at least one ship in the original force.", "unitIds");
                Trace("REJECTED", violation.Message);
                return false;
            }
            var split = source.SplitOff(command.NewFormationId, chosen);
            State.AddForce(split);
            State.MovementCup.Reset(State.Forces);
            Trace("SPLIT", $"{source.Id} split before the first draw; {split.Id} formed at {split.Position} " +
                $"with {string.Join(", ", split.Units.Select(unit => unit.Definition.DisplayName))}.");
            AddLog($"{split.Id} formed from {source.Id}; its movement chit was added to the cup.");
            return true;
        }

        public CommandResult DeclareSpeed(Side side, int speed) =>
            Execute(new GameCommand(GameCommandType.DeclareSpeed, side, State.Revision,
                declaredSpeed: speed));

        private bool DeclareSpeedInternal(Side side, int speed, out RuleViolation violation)
        {
            violation = null;
            if (State.ActiveSide != side)
            {
                violation = new RuleViolation(RuleViolationCode.WrongSide,
                    $"It is {State.ActiveSide}'s activation.", "actor");
                Trace("REJECTED", $"{side} speed declaration: {violation.Message}");
                return false;
            }
            if (State.Phase != ActivationPhase.DeclareSpeed)
            {
                violation = new RuleViolation(RuleViolationCode.WrongPhase,
                    "Speed has already been declared for this activation.", "phase");
                Trace("REJECTED", $"{side} speed declaration: {violation.Message}");
                return false;
            }
            var force = State.ForceFor(side);
            if (State.DetectionRulesEnabled && !force.RadarDeclaredThisActivation)
            {
                violation = new RuleViolation(RuleViolationCode.RadarDeclarationRequired,
                    "Declare surface-search radar on or silent before declaring speed.", "enabled");
                Trace("REJECTED", $"{side} speed declaration: {violation.Message}");
                return false;
            }
            if (speed < 0 || speed > force.EffectiveSpeed)
            {
                violation = new RuleViolation(RuleViolationCode.InvalidSpeed,
                    $"Declared speed must be between 0 and {force.EffectiveSpeed}.", "declaredSpeed");
                Trace("REJECTED", $"{side} declared speed {speed}: {violation.Message}");
                return false;
            }
            force.DeclareSpeed(speed);
            State.Phase = speed == 0 ? ActivationPhase.PlayerAction : ActivationPhase.PlayerMove;
            Trace("SPEED", $"{force.Id} declared speed {speed}; task-force maximum={force.EffectiveSpeed}.");
            AddLog($"{force.Id} declared speed {speed}.");
            return true;
        }

        private bool RadiateRadarInternal(GameCommand command, out RuleViolation violation)
        {
            violation = null;
            if (!State.DetectionRulesEnabled)
            {
                violation = new RuleViolation(RuleViolationCode.UnsupportedCommand,
                    "Scenario 1 omits detection and radar declarations.", "type");
                Trace("REJECTED", violation.Message);
                return false;
            }
            if (State.ActiveSide != command.Actor)
            {
                violation = new RuleViolation(RuleViolationCode.WrongSide,
                    $"It is {State.ActiveSide}'s activation.", "actor");
                Trace("REJECTED", violation.Message);
                return false;
            }
            if (State.Phase != ActivationPhase.DeclareSpeed)
            {
                violation = new RuleViolation(RuleViolationCode.WrongPhase,
                    "Radar is declared at the beginning of a formation's movement activation.", "phase");
                Trace("REJECTED", violation.Message);
                return false;
            }
            var force = State.ForceFor(command.Actor);
            if (command.Enabled && !force.CanRadiateRadar)
            {
                violation = new RuleViolation(RuleViolationCode.SensorUnavailable,
                    "No operational surface-search radar is available.", "enabled");
                Trace("REJECTED", violation.Message);
                return false;
            }
            force.DeclareRadar(command.Enabled);
            Trace("DETECTION", $"{force.Id} declared surface-search radar " +
                (force.RadarRadiating ? "RADIATING." : "SILENT."));
            AddLog($"{force.Id} radar {(force.RadarRadiating ? "radiating" : "silent")}.");
            ResolveAutomaticRadar(force);
            return true;
        }

        public bool TryMove(Side side, HexCoord destination, out string reason)
        {
            var result = Execute(new GameCommand(GameCommandType.Move, side, State.Revision, destination));
            reason = result.Summary;
            return result.Accepted;
        }

        private bool TryMoveInternal(Side side, HexCoord destination, out RuleViolation violation)
        {
            violation = null;
            if (State.ActiveSide != side)
            {
                violation = new RuleViolation(RuleViolationCode.WrongSide, $"It is {State.ActiveSide}'s activation.", "actor");
                Trace("REJECTED", $"{side} move to {destination}: {violation.Message}");
                return false;
            }
            if (State.Phase == ActivationPhase.DeclareSpeed)
            {
                violation = new RuleViolation(RuleViolationCode.SpeedNotDeclared,
                    "Declare task-force speed before moving.", "declaredSpeed");
                Trace("REJECTED", $"{side} move to {destination}: {violation.Message}");
                return false;
            }
            if (State.Phase != ActivationPhase.PlayerMove)
            {
                violation = new RuleViolation(RuleViolationCode.WrongPhase, "Movement is complete.", "phase");
                Trace("REJECTED", $"{side} move to {destination}: {violation.Message}");
                return false;
            }
            if (!State.Map.Contains(destination))
            {
                violation = new RuleViolation(RuleViolationCode.OutsideMap, "That hex is outside the operational map.", "destination");
                Trace("REJECTED", $"{side} move to {destination}: {violation.Message}");
                return false;
            }
            var force = State.ForceFor(side);
            if (!force.Position.IsAdjacentTo(destination))
            {
                violation = new RuleViolation(RuleViolationCode.NotAdjacent,
                    "Movement commands enter exactly one adjacent hex.", "destination");
                Trace("REJECTED", $"{side} move {force.Position}->{destination}: {violation.Message}");
                return false;
            }
            if (force.MovementRemaining <= 0)
            {
                violation = new RuleViolation(RuleViolationCode.MovementExhausted,
                    "The task force has spent its declared movement.", "declaredSpeed");
                Trace("REJECTED", $"{side} move {force.Position}->{destination}: {violation.Message}");
                return false;
            }
            if (!State.Map.IsNavigable(destination, side) || !_isNavigable(destination))
            {
                violation = new RuleViolation(RuleViolationCode.ImpassableTerrain, "That hex is not navigable.", "destination");
                Trace("REJECTED", $"{side} move to {destination}: {violation.Message}");
                return false;
            }
            var origin = force.Position;
            force.MoveOneHex(destination);
            State.PlayerHasMoved = true;
            State.PlayerHasAttacked = false;
            State.PlayerHasSearched = false;
            State.Phase = force.MovementRemaining == 0 ? ActivationPhase.PlayerAction : ActivationPhase.PlayerMove;
            AddLog($"{force.Id} moved to {destination}.");
            Trace("MOVEMENT", $"{force.Id} {origin}->{destination}; step={force.MovementPointsSpent}/{force.DeclaredSpeed}.");
            var opponent = State.ForceFor(side == Side.UsNavy ? Side.Plan : Side.UsNavy);
            Trace("OPPORTUNITY", opponent.Position == destination
                ? $"{force.Id} entered enemy-occupied hex {destination}; attack, search, and reaction window opened."
                : $"{force.Id} entered {destination}; attack/search window opened; enemy range={destination.DistanceTo(opponent.Position)}.");
            if (State.DetectionRulesEnabled) ResolveMovementDetection(force);
            return true;
        }

        private bool SearchInternal(GameCommand command, out RuleViolation violation)
        {
            var side = command.Actor;
            violation = null;
            if (State.ActiveSide != side)
            {
                violation = new RuleViolation(RuleViolationCode.WrongSide,
                    $"It is {State.ActiveSide}'s activation.", "actor");
                Trace("REJECTED", $"{side} search: {violation.Message}");
                return false;
            }
            if (State.Phase == ActivationPhase.DeclareSpeed)
            {
                violation = new RuleViolation(RuleViolationCode.SpeedNotDeclared,
                    "Declare speed before taking actions.", "declaredSpeed");
                Trace("REJECTED", $"{side} search: {violation.Message}");
                return false;
            }
            if (!State.DetectionRulesEnabled)
            {
                if (State.PlayerHasSearched)
                {
                    violation = new RuleViolation(RuleViolationCode.AlreadyActed,
                        "This task force has already searched in the current hex.");
                    Trace("REJECTED", $"{side} search: {violation.Message}");
                    return false;
                }
                if (State.TimeOfDay == TimeOfDay.Night &&
                    string.Equals(command.TargetId, "visual", StringComparison.OrdinalIgnoreCase))
                {
                    violation = new RuleViolation(RuleViolationCode.NightRestricted,
                        "Visual search is prohibited during a Night turn.", "targetId");
                    Trace("REJECTED", $"{side} visual search: {violation.Message}");
                    return false;
                }
                State.PlayerHasSearched = true;
                Trace("DETECTION", $"{State.ForceFor(side).Id} used its search opportunity in " +
                    $"{State.ForceFor(side).Position}; Scenario 1 omits detection resolution.");
                return true;
            }

            var observer = State.ForceFor(side);
            var mode = string.IsNullOrWhiteSpace(command.SearchMode) ? command.TargetId : command.SearchMode;
            var targetId = string.IsNullOrWhiteSpace(command.SearchMode) ? string.Empty : command.TargetId;
            var target = FindDetectionTarget(side, targetId, mode);
            if (target == null)
            {
                violation = new RuleViolation(RuleViolationCode.NoDetectionOpportunity,
                    "No enemy formation is in range of that sensor.", "targetId");
                Trace("REJECTED", $"{side} {mode} search: {violation.Message}");
                return false;
            }
            if (string.Equals(mode, "esm", StringComparison.OrdinalIgnoreCase))
            {
                if (!observer.CanUseEsm || !target.RadarRadiating || observer.Position.DistanceTo(target.Position) != 1)
                {
                    violation = new RuleViolation(RuleViolationCode.NoDetectionOpportunity,
                        "ESM requires an operational receiver adjacent to a radiating enemy.", "searchMode");
                    Trace("REJECTED", violation.Message);
                    return false;
                }
                var detected = _detection.ResolveEsm(observer, target);
                RecordDetection(side, target, DetectionMethod.Esm, detected);
                return true;
            }
            if (!string.Equals(mode, "visual", StringComparison.OrdinalIgnoreCase))
            {
                violation = new RuleViolation(RuleViolationCode.InvalidPayload,
                    "Search mode must be visual or ESM; surface radar resolves automatically.", "searchMode");
                Trace("REJECTED", violation.Message);
                return false;
            }
            if (State.TimeOfDay == TimeOfDay.Night)
            {
                violation = new RuleViolation(RuleViolationCode.NightRestricted,
                    "Visual search is prohibited during a Night turn.", "searchMode");
                Trace("REJECTED", violation.Message);
                return false;
            }
            if (observer.RadarRadiating || observer.Position != target.Position)
            {
                violation = new RuleViolation(RuleViolationCode.NoDetectionOpportunity,
                    "Visual search requires a radar-silent formation in the enemy's hex.", "targetId");
                Trace("REJECTED", violation.Message);
                return false;
            }
            if (State.PlayerHasSearched)
            {
                if (observer.MovementRemaining <= 0)
                {
                    violation = new RuleViolation(RuleViolationCode.MovementExhausted,
                        "No movement point remains for another visual-search attempt.", "declaredSpeed");
                    Trace("REJECTED", violation.Message);
                    return false;
                }
                observer.SpendMovementPointSearching();
                State.Phase = observer.MovementRemaining == 0 ? ActivationPhase.PlayerAction : ActivationPhase.PlayerMove;
                Trace("MOVEMENT", $"{observer.Id} spent one movement point searching in {observer.Position}; " +
                    $"step={observer.MovementPointsSpent}/{observer.DeclaredSpeed}.");
            }
            State.PlayerHasSearched = true;
            var visualDetected = _detection.ResolveVisual(observer, target, State.TimeOfDay);
            RecordDetection(side, target, DetectionMethod.Visual, visualDetected);
            return true;
        }

        public AttackReport PlayerAttack()
            => Attack(Side.UsNavy);

        public AttackReport Attack(Side side)
        {
            var result = Execute(new GameCommand(GameCommandType.Attack, side, State.Revision));
            return result.AttackReport ?? new AttackReport { Summary = result.Summary };
        }

        private AttackReport AttackInternal(Side side, out RuleViolation violation)
        {
            violation = null;
            var actionPhase = State.Phase == ActivationPhase.PlayerMove || State.Phase == ActivationPhase.PlayerAction;
            if (State.ActiveSide != side || !actionPhase || State.PlayerHasAttacked)
            {
                Trace("REJECTED", $"{side} attack: active={State.ActiveSide}, phase={State.Phase}, already attacked={State.PlayerHasAttacked}.");
                violation = new RuleViolation(State.ActiveSide != side ? RuleViolationCode.WrongSide :
                    State.PlayerHasAttacked ? RuleViolationCode.AlreadyActed : RuleViolationCode.WrongPhase,
                    "Attack is not available.");
                return null;
            }
            var attacker = State.ForceFor(side);
            var defender = FindOpponent(side, State.CurrentCommand?.TargetId);
            if (State.DetectionRulesEnabled && !State.Detection.IsDetected(side, defender.Id))
            {
                violation = new RuleViolation(RuleViolationCode.TargetUndetected,
                    "A task force may not be attacked until it is detected.", "targetId");
                Trace("REJECTED", $"{attacker.Id} attack on undetected {defender.Id}: {violation.Message}");
                return null;
            }
            var range = attacker.Position.DistanceTo(defender.Position);
            if (!CanFireMissiles(attacker, range))
            {
                if (range == 0)
                {
                    if (!attacker.ActiveUnits.Any(unit => unit.EffectiveGuns > 0) &&
                        !defender.ActiveUnits.Any(unit => unit.EffectiveGuns > 0))
                    {
                        violation = new RuleViolation(RuleViolationCode.NoLegalWeapon,
                            "Neither force has an operational gun battery.");
                        return null;
                    }
                    State.PlayerHasAttacked = true;
                    BeginGunCombat(attacker, defender, side, State.ActiveFormationId, State.Phase);
                    return null;
                }
                violation = new RuleViolation(RuleViolationCode.NoLegalWeapon,
                    "No unexpended surface-to-surface missile factor is in range.");
                Trace("REJECTED", violation.Message);
                return null;
            }
            var returnPhase = State.Phase;
            State.PendingMissileCombat = new MissileEngagement(attacker.Id, defender.Id,
                side, State.ActiveFormationId, returnPhase) { DecisionSide = side };
            State.Phase = ActivationPhase.MissileCombat;
            Trace("COMBAT", $"{attacker.Id} opened a missile attack on {defender.Id} at range {range}; " +
                "awaiting explicit fire allocation.");
            AddLog($"{attacker.Id} is allocating SSM fire against {defender.Id}.");
            return null;
        }

        private bool AllocateMissileFireInternal(GameCommand command, out RuleViolation violation)
        {
            violation = null;
            var engagement = State.PendingMissileCombat;
            if (!ValidateMissileDecision(command, MissileCombatPhase.AllocateFire, out violation)) return false;
            var attacker = State.Formation(engagement.AttackerFormationId);
            var defender = State.Formation(engagement.DefenderFormationId);
            var range = attacker.Position.DistanceTo(defender.Position);
            var allocations = command.MissileAllocations ?? Array.Empty<MissileAllocationData>();
            if (allocations.Count == 0)
                return RejectMissile(RuleViolationCode.InvalidAllocation,
                    "Allocate at least one missile factor.", "missileAllocations", out violation);

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var sourceTotals = new Dictionary<string, int[]>(StringComparer.Ordinal);
            foreach (var allocation in allocations)
            {
                var source = attacker.Units.FirstOrDefault(unit => unit.Definition.Id == allocation.sourceUnitId && !unit.IsSunk);
                var target = defender.Units.FirstOrDefault(unit => unit.Definition.Id == allocation.targetUnitId && !unit.IsSunk);
                if (source == null || target == null || string.IsNullOrWhiteSpace(allocation.id) ||
                    !ids.Add(allocation.id) || allocation.shortFactors < 0 || allocation.longFactors < 0 ||
                    allocation.shortFactors + allocation.longFactors <= 0 ||
                    (allocation.shortFactors > 0 && range > 1) || (allocation.longFactors > 0 && range > 3))
                    return RejectMissile(RuleViolationCode.InvalidAllocation,
                        "Every salvo needs a unique ID, a legal source and target, positive in-range factors, and no negative values.",
                        "missileAllocations", out violation);
                if (!sourceTotals.TryGetValue(source.Definition.Id, out var totals))
                    sourceTotals[source.Definition.Id] = totals = new int[2];
                totals[0] += allocation.shortFactors;
                totals[1] += allocation.longFactors;
            }
            foreach (var item in sourceTotals)
            {
                var source = attacker.Units.First(unit => unit.Definition.Id == item.Key);
                if (item.Value[0] > source.AvailableShortSsm || item.Value[1] > source.AvailableLongSsm)
                    return RejectMissile(RuleViolationCode.InsufficientAmmunition,
                        $"{source.Definition.DisplayName} cannot fire SR {item.Value[0]} / LR {item.Value[1]}; " +
                        $"available SR {source.AvailableShortSsm} / LR {source.AvailableLongSsm}.",
                        "missileAllocations", out violation);
            }

            foreach (var item in sourceTotals)
            {
                var source = attacker.Units.First(unit => unit.Definition.Id == item.Key);
                var shortBefore = source.ShortMissilesRemaining;
                var longBefore = source.LongMissilesRemaining;
                source.TryCommitMissiles(item.Value[0], item.Value[1]);
                Trace("AMMUNITION", $"{source.Definition.DisplayName}: committed SR {item.Value[0]}, LR {item.Value[1]}; " +
                    $"SR {shortBefore}->{source.ShortMissilesRemaining}, LR {longBefore}->{source.LongMissilesRemaining}.");
            }
            engagement.SetSalvos(allocations.Select(item => new MissileSalvo(item.id,
                item.sourceUnitId, item.targetUnitId, item.shortFactors, item.longFactors)));
            State.PlayerHasAttacked = true;
            engagement.Phase = MissileCombatPhase.DefensiveDeployment;
            engagement.DecisionSide = defender.Side;
            State.ActiveSide = defender.Side;
            Trace("COMBAT", $"{attacker.Id} launched {engagement.InitialFactors} factor(s) in " +
                $"{engagement.Salvos.Count} allocated salvo(s); {defender.Id} must deploy its defensive pairs.");
            return true;
        }

        private bool DefendMissileAttackInternal(GameCommand command, out RuleViolation violation,
            out AttackReport report)
        {
            report = null;
            violation = null;
            var engagement = State.PendingMissileCombat;
            if (engagement == null || State.Phase != ActivationPhase.MissileCombat ||
                engagement.DecisionSide != command.Actor)
                return RejectMissile(RuleViolationCode.NoPendingCombat,
                    "No missile-defense decision is waiting for this side.", "actor", out violation);
            switch (engagement.Phase)
            {
                case MissileCombatPhase.DefensiveDeployment:
                    return DeployMissileDefense(command, engagement, out violation);
                case MissileCombatPhase.LongRangeRemoval:
                    return ApplyLongRangeRemovals(command, engagement, out violation);
                case MissileCombatPhase.ShortRangeDefense:
                    return ApplyShortRangeDefense(command, engagement, out violation, out report);
                default:
                    return RejectMissile(RuleViolationCode.WrongPhase,
                        "The pending missile raid is not waiting for a defense command.", "phase", out violation);
            }
        }

        private bool DeployMissileDefense(GameCommand command, MissileEngagement engagement,
            out RuleViolation violation)
        {
            violation = null;
            var defender = State.Formation(engagement.DefenderFormationId);
            var activeIds = new HashSet<string>(defender.ActiveUnits.Select(unit => unit.Definition.Id));
            var used = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pair in command.DefensePairs ?? Array.Empty<DefensePairData>())
                if (pair == null || pair.firstUnitId == pair.secondUnitId ||
                    !activeIds.Contains(pair.firstUnitId) || !activeIds.Contains(pair.secondUnitId) ||
                    !used.Add(pair.firstUnitId) || !used.Add(pair.secondUnitId))
                    return RejectMissile(RuleViolationCode.InvalidDefense,
                        "Defensive pairs must contain two different operational ships, each used at most once.",
                        "defensePairs", out violation);
            engagement.SetDefensePairs(command.DefensePairs);
            defender.SetDefensePairs(command.DefensePairs);
            var longRangeDice = defender.ActiveUnits.Sum(unit => unit.EffectiveLongSam);
            engagement.LongRangeHits = Math.Min(engagement.RemainingFactors,
                _missileCombat.RollDefense("Formation LR SAM", longRangeDice, CombatTableColumn.Sam));
            Trace("DEFENSE", $"{defender.Id} deployed {engagement.DefensePairs.Count} pair(s); " +
                $"LR SAM dice={longRangeDice}, removals available={engagement.LongRangeHits}.");
            engagement.Phase = engagement.LongRangeHits > 0
                ? MissileCombatPhase.LongRangeRemoval : MissileCombatPhase.ShortRangeDefense;
            return true;
        }

        private bool ApplyLongRangeRemovals(GameCommand command, MissileEngagement engagement,
            out RuleViolation violation)
        {
            violation = null;
            var reductions = command.MissileReductions ?? Array.Empty<MissileReductionData>();
            var grouped = reductions.GroupBy(item => item.salvoId)
                .ToDictionary(group => group.Key, group => group.Sum(item => item.factors), StringComparer.Ordinal);
            if (grouped.Values.Any(value => value < 0) || grouped.Values.Sum() != engagement.LongRangeHits)
                return RejectMissile(RuleViolationCode.InvalidDefense,
                    $"Assign exactly {engagement.LongRangeHits} long-range SAM removal(s).",
                    "missileReductions", out violation);
            foreach (var item in grouped)
            {
                var salvo = engagement.Salvos.FirstOrDefault(candidate => candidate.Id == item.Key);
                if (salvo == null || item.Value > salvo.RemainingFactors)
                    return RejectMissile(RuleViolationCode.InvalidDefense,
                        "Long-range SAM removals cannot exceed an existing salvo's remaining factors.",
                        "missileReductions", out violation);
            }
            foreach (var item in grouped)
            {
                engagement.Salvos.First(salvo => salvo.Id == item.Key).Remove(item.Value);
                Trace("DEFENSE", $"LR SAM removed {item.Value} factor(s) from salvo {item.Key}.");
            }
            engagement.LongRangeHits = 0;
            engagement.Phase = MissileCombatPhase.ShortRangeDefense;
            return true;
        }

        private bool ApplyShortRangeDefense(GameCommand command, MissileEngagement engagement,
            out RuleViolation violation, out AttackReport report)
        {
            violation = null;
            report = null;
            var defender = State.Formation(engagement.DefenderFormationId);
            var usedDefenders = new HashSet<string>(StringComparer.Ordinal);
            foreach (var assignment in command.ShortRangeDefenses ?? Array.Empty<ShortRangeDefenseData>())
            {
                var ship = defender.ActiveUnits.FirstOrDefault(unit => unit.Definition.Id == assignment.defendingUnitId);
                var salvo = engagement.Salvos.FirstOrDefault(item => item.Id == assignment.salvoId && item.RemainingFactors > 0);
                var pairMate = engagement.PairMate(assignment.defendingUnitId);
                if (ship == null || ship.EffectiveShortSam <= 0 || salvo == null ||
                    (salvo.TargetUnitId != ship.Definition.Id && salvo.TargetUnitId != pairMate) ||
                    !usedDefenders.Add(ship.Definition.Id))
                    return RejectMissile(RuleViolationCode.InvalidDefense,
                        "Each operational short-range SAM battery may engage one salvo attacking itself or its pair-mate.",
                        "shortRangeDefenses", out violation);
            }
            foreach (var assignment in command.ShortRangeDefenses ?? Array.Empty<ShortRangeDefenseData>())
            {
                var ship = defender.Units.First(unit => unit.Definition.Id == assignment.defendingUnitId);
                var salvo = engagement.Salvos.First(item => item.Id == assignment.salvoId);
                var hits = _missileCombat.RollDefense($"SR SAM {ship.Definition.DisplayName}",
                    ship.EffectiveShortSam, CombatTableColumn.Sam);
                var removed = salvo.Remove(hits);
                Trace("DEFENSE", $"{ship.Definition.DisplayName} SR SAM engaged salvo {salvo.Id}; " +
                    $"removed={removed}, salvo remaining={salvo.RemainingFactors}.");
            }
            report = ResolveMissileRaid(engagement);
            return true;
        }

        private AttackReport ResolveMissileRaid(MissileEngagement engagement)
        {
            var attacker = State.Formation(engagement.AttackerFormationId);
            var defender = State.Formation(engagement.DefenderFormationId);
            var report = _missileCombat.ResolvePointDefenseAndImpacts(engagement, defender);
            AddLog(report.Summary);
            Trace("COMBAT", $"{attacker.Id} raid resolved against {defender.Id}: {report.Summary}");
            AttackResolved?.Invoke(attacker.Side, report);
            PruneDestroyedMovementChits();
            CheckGameOver();
            if (State.IsGameOver)
            {
                State.PendingMissileCombat = null;
                return report;
            }
            if (!engagement.IsCounterattack && CanCounterattack(defender, attacker))
            {
                engagement.Phase = MissileCombatPhase.CounterattackDecision;
                engagement.DecisionSide = defender.Side;
                State.ActiveSide = defender.Side;
                Trace("COMBAT", $"{defender.Id} may counterattack {attacker.Id} before movement resumes.");
            }
            else FinishMissileCombat(engagement);
            return report;
        }

        private bool CounterattackInternal(GameCommand command, out RuleViolation violation)
        {
            violation = null;
            var engagement = State.PendingMissileCombat;
            if (engagement == null || engagement.Phase != MissileCombatPhase.CounterattackDecision ||
                engagement.DecisionSide != command.Actor)
                return RejectMissile(RuleViolationCode.CounterattackUnavailable,
                    "No counterattack decision is waiting for this side.", "actor", out violation);
            var counterattacker = State.Formation(engagement.DefenderFormationId);
            var target = State.Formation(engagement.AttackerFormationId);
            if (!command.Enabled)
            {
                Trace("COMBAT", $"{counterattacker.Id} declined its counterattack.");
                FinishMissileCombat(engagement);
                return true;
            }
            if (!CanCounterattack(counterattacker, target))
                return RejectMissile(RuleViolationCode.CounterattackUnavailable,
                    "The non-moving force has no legal detected missile target or in-range ammunition.",
                    "enabled", out violation);
            State.PendingMissileCombat = new MissileEngagement(counterattacker.Id, target.Id,
                engagement.MovementOwnerSide, engagement.MovementOwnerFormationId,
                engagement.ReturnPhase, true) { DecisionSide = counterattacker.Side };
            State.ActiveSide = counterattacker.Side;
            Trace("COMBAT", $"{counterattacker.Id} elected to counterattack {target.Id}; awaiting fire allocation.");
            return true;
        }

        private bool ValidateMissileDecision(GameCommand command, MissileCombatPhase expected,
            out RuleViolation violation)
        {
            violation = null;
            var engagement = State.PendingMissileCombat;
            if (engagement == null || State.Phase != ActivationPhase.MissileCombat)
                return RejectMissile(RuleViolationCode.NoPendingCombat,
                    "No missile engagement is pending.", "phase", out violation);
            if (engagement.DecisionSide != command.Actor)
                return RejectMissile(RuleViolationCode.WrongSide,
                    $"The missile decision belongs to {engagement.DecisionSide}.", "actor", out violation);
            if (engagement.Phase != expected)
                return RejectMissile(RuleViolationCode.WrongPhase,
                    $"The missile engagement is waiting for {engagement.Phase}.", "phase", out violation);
            return true;
        }

        private bool RejectMissile(RuleViolationCode code, string message, string field,
            out RuleViolation violation)
        {
            violation = new RuleViolation(code, message, field);
            Trace("REJECTED", message);
            return false;
        }

        private bool CanCounterattack(TaskForceState attacker, TaskForceState target)
        {
            var range = attacker.Position.DistanceTo(target.Position);
            return CanFireMissiles(attacker, range) && (!State.DetectionRulesEnabled ||
                State.Detection.IsDetected(attacker.Side, target.Id));
        }

        private static bool CanFireMissiles(TaskForceState force, int range) => force.ActiveUnits.Any(unit =>
            (range <= 1 && unit.AvailableShortSsm > 0) || (range <= 3 && unit.AvailableLongSsm > 0));

        private bool CanOpenAttack(TaskForceState attacker, TaskForceState defender)
        {
            if (attacker == null || defender == null || attacker.IsDestroyed || defender.IsDestroyed) return false;
            if (State.DetectionRulesEnabled && !State.Detection.IsDetected(attacker.Side, defender.Id)) return false;
            var range = attacker.Position.DistanceTo(defender.Position);
            return CanFireMissiles(attacker, range) || (range == 0 &&
                (attacker.ActiveUnits.Any(unit => unit.EffectiveGuns > 0) ||
                 defender.ActiveUnits.Any(unit => unit.EffectiveGuns > 0)));
        }

        private void FinishMissileCombat(MissileEngagement engagement)
        {
            State.PendingMissileCombat = null;
            var movingForce = State.Formation(engagement.MovementOwnerFormationId);
            var opponent = State.Forces.FirstOrDefault(force => force.Side != engagement.MovementOwnerSide &&
                movingForce != null && force.Position.Equals(movingForce.Position) && !force.IsDestroyed);
            if (movingForce != null && opponent != null &&
                (movingForce.ActiveUnits.Any(unit => unit.EffectiveGuns > 0) ||
                 opponent.ActiveUnits.Any(unit => unit.EffectiveGuns > 0)))
            {
                BeginGunCombat(movingForce, opponent, engagement.MovementOwnerSide,
                    engagement.MovementOwnerFormationId, engagement.ReturnPhase);
                return;
            }
            State.ActiveSide = engagement.MovementOwnerSide;
            State.ActiveFormationId = engagement.MovementOwnerFormationId;
            State.Phase = engagement.ReturnPhase;
            Trace("COMBAT", $"Missile exchange complete; {State.ActiveFormationId} resumes its activation.");
        }

        private void BeginGunCombat(TaskForceState attacker, TaskForceState defender,
            Side movementOwnerSide, string movementOwnerFormationId, ActivationPhase returnPhase)
        {
            var engagement = new GunEngagement(attacker.Id, defender.Id, movementOwnerSide,
                movementOwnerFormationId, returnPhase);
            State.PendingGunCombat = engagement;
            State.Phase = ActivationPhase.GunCombat;
            State.ActiveSide = defender.Side;
            var attackerSpeed = attacker.EffectiveSpeed;
            var defenderSpeed = defender.EffectiveSpeed;
            Trace("GUNFIRE", $"{attacker.Id} entered {defender.Id}'s hex; effective speeds " +
                $"{attackerSpeed} vs {defenderSpeed}.");
            if (defenderSpeed > attackerSpeed)
            {
                engagement.Phase = GunCombatPhase.EngageDecision;
                engagement.DecisionSide = defender.Side;
                AddLog($"{defender.Id} is faster and may evade or accept gunfire.");
                return;
            }
            if (defenderSpeed == attackerSpeed)
            {
                var roll = _dice.RollD6();
                Trace("DIE", $"Equal-speed engagement: D6={roll}; attacker engages on 1-3.");
                if (!GunCombatRules.InitialEngagementSucceeds(attackerSpeed, defenderSpeed, roll))
                {
                    AddLog($"{attacker.Id} failed to maneuver into a gun engagement (rolled {roll}).");
                    FinishGunCombat("Equal-speed defender evaded the initial engagement.");
                    return;
                }
            }
            StartGunArrangement(engagement);
        }

        private void StartGunArrangement(GunEngagement engagement)
        {
            var attacker = State.Formation(engagement.AttackerFormationId);
            engagement.Phase = GunCombatPhase.ArrangeAttacker;
            engagement.DecisionSide = attacker.Side;
            State.ActiveSide = attacker.Side;
            AddLog($"Gunfire round {engagement.Round}: {attacker.Id} must nominate its firing ships and screens.");
        }

        private bool ArrangeGunfireInternal(GameCommand command, out RuleViolation violation)
        {
            violation = null;
            var engagement = State.PendingGunCombat;
            if (engagement == null || State.Phase != ActivationPhase.GunCombat ||
                engagement.DecisionSide != command.Actor ||
                (engagement.Phase != GunCombatPhase.ArrangeAttacker &&
                 engagement.Phase != GunCombatPhase.ArrangeDefender))
                return RejectGun(RuleViolationCode.NoPendingCombat,
                    "No gunfire arrangement is waiting for this side.", "actor", out violation);
            var attacker = State.Formation(engagement.AttackerFormationId);
            var defender = State.Formation(engagement.DefenderFormationId);
            var force = command.Actor == attacker.Side ? attacker : defender;
            var active = new HashSet<string>(force.ActiveUnits.Select(unit => unit.Definition.Id), StringComparer.Ordinal);
            var used = new HashSet<string>(StringComparer.Ordinal);
            var pairs = command.GunPairs ?? Array.Empty<GunPairData>();
            foreach (var pair in pairs)
            {
                if (pair == null || string.IsNullOrWhiteSpace(pair.firingUnitId) ||
                    !active.Contains(pair.firingUnitId) || !used.Add(pair.firingUnitId) ||
                    (!string.IsNullOrWhiteSpace(pair.screenedUnitId) &&
                     (!active.Contains(pair.screenedUnitId) || !used.Add(pair.screenedUnitId) ||
                      pair.screenedUnitId == pair.firingUnitId)))
                    return RejectGun(RuleViolationCode.InvalidGunPairing,
                        "Each operational ship must appear once; every group needs one nominated firing ship and at most one screened ship.",
                        "gunPairs", out violation);
            }
            if (!used.SetEquals(active))
                return RejectGun(RuleViolationCode.InvalidGunPairing,
                    "The firing formation must account for every operational ship exactly once.",
                    "gunPairs", out violation);
            if (!MatchesExistingScreen(force.DefensePairs, pairs))
                return RejectGun(RuleViolationCode.InvalidGunPairing,
                    "Ships paired during the missile exchange must retain the same formation for gunfire.",
                    "gunPairs", out violation);

            engagement.SetPairs(command.Actor, pairs, attacker.Side);
            Trace("GUNFIRE", $"{force.Id} arranged {pairs.Count} firing group(s): " +
                string.Join(", ", pairs.Select(pair => pair.firingUnitId +
                    (string.IsNullOrWhiteSpace(pair.screenedUnitId) ? string.Empty : $" screens {pair.screenedUnitId}"))));
            if (engagement.Phase == GunCombatPhase.ArrangeAttacker)
            {
                engagement.Phase = GunCombatPhase.ArrangeDefender;
                engagement.DecisionSide = defender.Side;
                State.ActiveSide = defender.Side;
            }
            else BeginGunFiring(engagement);
            return true;
        }

        private static bool MatchesExistingScreen(IReadOnlyList<DefensePairData> existing,
            IReadOnlyList<GunPairData> proposed)
        {
            if (existing == null || existing.Count == 0) return true;
            var active = new HashSet<string>(proposed.SelectMany(item => new[]
                { item.firingUnitId, item.screenedUnitId }).Where(id => !string.IsNullOrWhiteSpace(id)),
                StringComparer.Ordinal);
            foreach (var pair in existing)
            {
                if (!active.Contains(pair.firstUnitId) || !active.Contains(pair.secondUnitId)) continue;
                var proposedPair = proposed.FirstOrDefault(item =>
                    (item.firingUnitId == pair.firstUnitId && item.screenedUnitId == pair.secondUnitId) ||
                    (item.firingUnitId == pair.secondUnitId && item.screenedUnitId == pair.firstUnitId));
                if (proposedPair == null) return false;
            }
            return true;
        }

        private void BeginGunFiring(GunEngagement engagement)
        {
            var attacker = State.Formation(engagement.AttackerFormationId);
            var defender = State.Formation(engagement.DefenderFormationId);
            var shooters = engagement.AttackerPairs.Concat(engagement.DefenderPairs)
                .Select(pair => attacker.Units.Concat(defender.Units)
                    .First(unit => unit.Definition.Id == pair.firingUnitId))
                .Where(unit => !unit.IsSunk && unit.EffectiveGuns > 0).ToList();
            var order = new List<string>();
            foreach (var group in shooters.GroupBy(unit => unit.EffectiveGuns).OrderByDescending(group => group.Key))
            {
                var tied = group.ToList();
                while (tied.Count > 0)
                {
                    var index = tied.Count == 1 ? 0 : _dice.RollD6() % tied.Count;
                    if (tied.Count > 1)
                        Trace("DIE", $"Equal gun factor initiative ({group.Key}): tie roll selected {tied[index].Definition.DisplayName}.");
                    order.Add(tied[index].Definition.Id);
                    tied.RemoveAt(index);
                }
            }
            engagement.SetFiringOrder(order);
            engagement.Phase = GunCombatPhase.Firing;
            AdvanceGunFiring(engagement);
        }

        private void AdvanceGunFiring(GunEngagement engagement)
        {
            var allUnits = State.Forces.SelectMany(force => force.Units).ToDictionary(unit => unit.Definition.Id);
            while (engagement.FiringIndex < engagement.FiringOrder.Count &&
                   (!allUnits.TryGetValue(engagement.FiringOrder[engagement.FiringIndex], out var next) ||
                    next.IsSunk || next.EffectiveGuns <= 0))
                engagement.FiringIndex++;
            if (engagement.FiringIndex >= engagement.FiringOrder.Count)
            {
                var attacker = State.Formation(engagement.AttackerFormationId);
                engagement.Phase = GunCombatPhase.BreakOffAttacker;
                engagement.DecisionSide = attacker.Side;
                State.ActiveSide = attacker.Side;
                Trace("GUNFIRE", $"Gunfire round {engagement.Round} complete; requesting break-off choices.");
                return;
            }
            var shooter = allUnits[engagement.FiringOrder[engagement.FiringIndex]];
            var owner = State.Forces.First(force => force.Units.Contains(shooter));
            engagement.DecisionSide = owner.Side;
            State.ActiveSide = owner.Side;
        }

        private bool FireGunsInternal(GameCommand command, out RuleViolation violation, out AttackReport report)
        {
            violation = null;
            report = null;
            var engagement = State.PendingGunCombat;
            if (engagement == null || engagement.Phase != GunCombatPhase.Firing ||
                engagement.DecisionSide != command.Actor ||
                engagement.FiringIndex >= engagement.FiringOrder.Count)
                return RejectGun(RuleViolationCode.NoPendingCombat,
                    "No gun attack is waiting for this side.", "actor", out violation);
            var expectedShooter = engagement.FiringOrder[engagement.FiringIndex];
            if (command.SourceUnitId != expectedShooter)
                return RejectGun(RuleViolationCode.InvalidGunTarget,
                    $"{expectedShooter} has the next shot by gun-factor order.", "sourceUnitId", out violation);
            var attacker = State.Formation(engagement.AttackerFormationId);
            var defender = State.Formation(engagement.DefenderFormationId);
            var shooterForce = command.Actor == attacker.Side ? attacker : defender;
            var targetForce = command.Actor == attacker.Side ? defender : attacker;
            var shooter = shooterForce.ActiveUnits.FirstOrDefault(unit => unit.Definition.Id == command.SourceUnitId);
            var target = targetForce.ActiveUnits.FirstOrDefault(unit => unit.Definition.Id == command.TargetId);
            if (shooter == null || target == null)
                return RejectGun(RuleViolationCode.InvalidGunTarget,
                    "The firing and target ships must both be operational and in the same hex.", "targetId", out violation);
            report = _gunCombat.Fire(shooter, target, engagement.IsScreened(target.Definition.Id));
            AddLog(report.Summary);
            AttackResolved?.Invoke(command.Actor, report);
            engagement.FiringIndex++;
            PruneDestroyedMovementChits();
            CheckGameOver();
            if (State.IsGameOver) State.PendingGunCombat = null;
            else AdvanceGunFiring(engagement);
            return true;
        }

        private bool BreakOffInternal(GameCommand command, out RuleViolation violation)
        {
            violation = null;
            var engagement = State.PendingGunCombat;
            if (engagement == null || engagement.DecisionSide != command.Actor)
                return RejectGun(RuleViolationCode.BreakOffUnavailable,
                    "No engage/evade or break-off decision is waiting for this side.", "actor", out violation);
            var attacker = State.Formation(engagement.AttackerFormationId);
            var defender = State.Formation(engagement.DefenderFormationId);
            if (engagement.Phase == GunCombatPhase.EngageDecision)
            {
                if (command.Actor != defender.Side || defender.EffectiveSpeed <= attacker.EffectiveSpeed)
                    return RejectGun(RuleViolationCode.BreakOffUnavailable,
                        "Only a faster defending force may choose to evade the initial engagement.", "enabled", out violation);
                if (command.Enabled) FinishGunCombat($"{defender.Id} used superior speed to evade gunfire.");
                else StartGunArrangement(engagement);
                return true;
            }
            if (engagement.Phase != GunCombatPhase.BreakOffAttacker &&
                engagement.Phase != GunCombatPhase.BreakOffDefender)
                return RejectGun(RuleViolationCode.BreakOffUnavailable,
                    "Break-off choices occur after every firing ship has acted.", "phase", out violation);
            if (command.Actor == attacker.Side) engagement.AttackerBreakOff = command.Enabled ? 1 : 0;
            else engagement.DefenderBreakOff = command.Enabled ? 1 : 0;
            Trace("GUNFIRE", $"{command.Actor} chose {(command.Enabled ? "break off" : "continue")} after round {engagement.Round}.");
            if (engagement.Phase == GunCombatPhase.BreakOffAttacker)
            {
                engagement.Phase = GunCombatPhase.BreakOffDefender;
                engagement.DecisionSide = defender.Side;
                State.ActiveSide = defender.Side;
                return true;
            }
            ResolveBreakOff(engagement, attacker, defender);
            return true;
        }

        private void ResolveBreakOff(GunEngagement engagement, TaskForceState attacker, TaskForceState defender)
        {
            var attackerChoice = engagement.AttackerBreakOff == 1;
            var defenderChoice = engagement.DefenderBreakOff == 1;
            if (attackerChoice && defenderChoice)
            {
                FinishGunCombat("Both forces agreed to break off.");
                return;
            }
            if (attackerChoice || defenderChoice)
            {
                var leaving = attackerChoice ? attacker : defender;
                var other = attackerChoice ? defender : attacker;
                if (leaving.EffectiveSpeed > other.EffectiveSpeed)
                {
                    FinishGunCombat($"{leaving.Id} broke off automatically using superior speed.");
                    return;
                }
                var threshold = GunCombatRules.BreakOffThreshold(leaving.EffectiveSpeed, other.EffectiveSpeed);
                var roll = _dice.RollD6();
                Trace("DIE", $"{leaving.Id} break-off: D6={roll}; succeeds on 1-{threshold}.");
                if (GunCombatRules.BreakOffSucceeds(leaving.EffectiveSpeed, other.EffectiveSpeed, roll))
                {
                    FinishGunCombat($"{leaving.Id} successfully broke off (rolled {roll}).");
                    return;
                }
            }
            engagement.Round++;
            engagement.AttackerBreakOff = -1;
            engagement.DefenderBreakOff = -1;
            BeginGunFiring(engagement);
            AddLog($"Neither force escaped; gunfire continues with round {engagement.Round}.");
        }

        private void FinishGunCombat(string reason)
        {
            var engagement = State.PendingGunCombat;
            if (engagement == null) return;
            State.PendingGunCombat = null;
            State.ActiveSide = engagement.MovementOwnerSide;
            State.ActiveFormationId = engagement.MovementOwnerFormationId;
            State.Phase = engagement.ReturnPhase;
            Trace("GUNFIRE", reason + $" {State.ActiveFormationId} resumes its activation.");
            AddLog(reason);
        }

        private bool RejectGun(RuleViolationCode code, string message, string field,
            out RuleViolation violation)
        {
            violation = new RuleViolation(code, message, field);
            Trace("REJECTED", message);
            return false;
        }

        public void EndPlayerActivation()
            => EndActivation(Side.UsNavy);

        public void EndActivation(Side side)
        {
            Execute(new GameCommand(GameCommandType.EndActivation, side, State.Revision));
        }

        private bool EndActivationInternal(Side side, out RuleViolation violation)
        {
            violation = null;
            if (State.IsGameOver)
            {
                violation = new RuleViolation(RuleViolationCode.GameOver, "The game is already over.");
                return false;
            }
            if (State.ActiveSide != side)
            {
                Trace("REJECTED", $"{side} tried to end {State.ActiveSide}'s activation.");
                violation = new RuleViolation(RuleViolationCode.WrongSide,
                    $"It is {State.ActiveSide}'s activation.", "actor");
                return false;
            }
            var force = State.ForceFor(side);
            if (force.DeclaredSpeed < 0)
            {
                violation = new RuleViolation(RuleViolationCode.SpeedNotDeclared,
                    "Declare speed before ending the activation.", "declaredSpeed");
                Trace("REJECTED", $"{side} end activation: {violation.Message}");
                return false;
            }
            if (force.MovementRemaining > 0)
            {
                violation = new RuleViolation(RuleViolationCode.MovementIncomplete,
                    $"{force.MovementRemaining} declared movement step(s) remain.", "declaredSpeed");
                Trace("REJECTED", $"{side} end activation: {violation.Message}");
                return false;
            }
            Trace("ACTIVATION", $"{force.Id} ({side}) ended activation.");
            if (side == Side.UsNavy) State.UsActivated = true;
            else State.PlanActivated = true;
            if (!State.MovementCup.IsEmpty)
            {
                State.Phase = ActivationPhase.AwaitingChit;
                return DrawMovementChitInternal(out violation);
            }
            CompleteTurn();
            return true;
        }

        private void BeginTurn()
        {
            State.PlayerHasMoved = false;
            State.PlayerHasAttacked = false;
            State.PlayerHasSearched = false;
            State.UsActivated = false;
            State.PlanActivated = false;
            foreach (var force in State.Forces) force.ResetActivation();
            State.ActiveFormationId = string.Empty;
            State.MovementCup.Reset(State.Forces);
            State.Phase = ActivationPhase.AwaitingChit;
            Trace("TURN", $"{State.TimeLabel} begins; {State.MovementCup.TotalCount} movement chit(s) returned to the cup.");
            AddLog($"Turn {State.Turn} ({State.TimeLabel}). Draw the first movement chit.");
        }

        private void ActivateEnemy()
        {
            if (_isActivatingEnemy || State.IsGameOver || State.Enemy.IsDestroyed) return;
            _isActivatingEnemy = true;
            try
            {
                // A new turn may also draw PLAN first, so finish consecutive PLAN activations
                // without recursively re-entering through Execute.
                while (!State.IsGameOver && State.ActiveSide == Side.Plan &&
                       State.Phase != ActivationPhase.AwaitingChit)
                {
                    if (State.Phase == ActivationPhase.MissileCombat)
                    {
                        if (State.PendingMissileCombat == null ||
                            State.PendingMissileCombat.DecisionSide != Side.Plan) break;
                        ExecutePlanMissileDecision();
                        continue;
                    }
                    if (State.Phase == ActivationPhase.GunCombat)
                    {
                        if (State.PendingGunCombat == null ||
                            State.PendingGunCombat.DecisionSide != Side.Plan) break;
                        ExecutePlanGunDecision();
                        continue;
                    }
                    var enemyForce = State.ActiveForce ?? State.Enemy;
                    if (State.Phase == ActivationPhase.DeclareSpeed)
                    {
                        if (State.DetectionRulesEnabled && !enemyForce.RadarDeclaredThisActivation)
                        {
                            var targetRange = enemyForce.Position.DistanceTo(State.Player.Position);
                            Execute(new GameCommand(GameCommandType.RadiateRadar, Side.Plan,
                                State.Revision, enabled: enemyForce.CanRadiateRadar && targetRange <= 1));
                        }
                        var path = State.Map.FindPath(enemyForce.Position, State.Player.Position, Side.Plan);
                        var declared = path.Count == 0 ? 0 : Math.Min(enemyForce.EffectiveSpeed,
                            Math.Max(0, path.Count - 2));
                        Execute(new GameCommand(GameCommandType.DeclareSpeed, Side.Plan,
                            State.Revision, declaredSpeed: declared));
                    }
                    while (!State.IsGameOver && State.Phase == ActivationPhase.PlayerMove)
                    {
                        var destination = BestEnemyDestination();
                        var movement = Execute(new GameCommand(GameCommandType.Move, Side.Plan,
                            State.Revision, destination));
                        if (!movement.Accepted) break;
                        TryEnemyDetection(enemyForce);
                        if (!State.PlayerHasAttacked && CanOpenAttack(enemyForce, State.Player))
                            Execute(new GameCommand(GameCommandType.Attack, Side.Plan, State.Revision));
                    }
                    TryEnemyDetection(enemyForce);
                    if (!State.IsGameOver && State.Phase == ActivationPhase.PlayerAction &&
                        !State.PlayerHasAttacked && CanOpenAttack(enemyForce, State.Player))
                        Execute(new GameCommand(GameCommandType.Attack, Side.Plan, State.Revision));
                    if (!State.IsGameOver && State.ActiveSide == Side.Plan &&
                        State.Phase != ActivationPhase.MissileCombat &&
                        State.Phase != ActivationPhase.GunCombat)
                        Execute(new GameCommand(GameCommandType.EndActivation, Side.Plan, State.Revision));
                }
            }
            finally { _isActivatingEnemy = false; }
        }

        private void ExecutePlanMissileDecision()
        {
            var engagement = State.PendingMissileCombat;
            if (engagement == null) return;
            var attacker = State.Formation(engagement.AttackerFormationId);
            var defender = State.Formation(engagement.DefenderFormationId);
            switch (engagement.Phase)
            {
                case MissileCombatPhase.AllocateFire:
                {
                    var range = attacker.Position.DistanceTo(defender.Position);
                    var target = defender.Objective != null && !defender.Objective.IsSunk
                        ? defender.Objective : defender.ActiveUnits.First();
                    var allocations = attacker.ActiveUnits.Select((unit, index) => new MissileAllocationData
                    {
                        id = $"PLAN-{State.Revision}-{index + 1}",
                        sourceUnitId = unit.Definition.Id,
                        targetUnitId = target.Definition.Id,
                        shortFactors = range <= 1 ? unit.AvailableShortSsm : 0,
                        longFactors = range <= 3 ? unit.AvailableLongSsm : 0
                    }).Where(item => item.shortFactors + item.longFactors > 0).ToArray();
                    Execute(new GameCommand(GameCommandType.AllocateMissileFire, Side.Plan,
                        State.Revision, missileAllocations: allocations));
                    break;
                }
                case MissileCombatPhase.DefensiveDeployment:
                    Execute(new GameCommand(GameCommandType.Defend, Side.Plan, State.Revision,
                        defensePairs: DefaultDefensePairs(defender)));
                    break;
                case MissileCombatPhase.LongRangeRemoval:
                {
                    var remaining = engagement.LongRangeHits;
                    var reductions = new List<MissileReductionData>();
                    foreach (var salvo in engagement.Salvos.OrderByDescending(item =>
                                 defender.Units.First(unit => unit.Definition.Id == item.TargetUnitId)
                                     .Definition.Role == UnitRole.Objective).ThenByDescending(item => item.RemainingFactors))
                    {
                        var amount = Math.Min(remaining, salvo.RemainingFactors);
                        if (amount > 0) reductions.Add(new MissileReductionData { salvoId = salvo.Id, factors = amount });
                        remaining -= amount;
                        if (remaining == 0) break;
                    }
                    Execute(new GameCommand(GameCommandType.Defend, Side.Plan, State.Revision,
                        missileReductions: reductions));
                    break;
                }
                case MissileCombatPhase.ShortRangeDefense:
                    Execute(new GameCommand(GameCommandType.Defend, Side.Plan, State.Revision,
                        shortRangeDefenses: DefaultShortRangeAssignments(engagement, defender)));
                    break;
                case MissileCombatPhase.CounterattackDecision:
                    Execute(new GameCommand(GameCommandType.Counterattack, Side.Plan,
                        State.Revision, enabled: true));
                    break;
            }
        }

        private static DefensePairData[] DefaultDefensePairs(TaskForceState defender)
        {
            var remaining = defender.ActiveUnits.OrderByDescending(unit => unit.Definition.Role == UnitRole.Objective)
                .ThenBy(unit => unit.Definition.Id).ToList();
            var pairs = new List<DefensePairData>();
            while (remaining.Count >= 2)
            {
                var first = remaining[0];
                remaining.RemoveAt(0);
                var second = remaining.OrderByDescending(unit => unit.EffectiveShortSam)
                    .ThenBy(unit => unit.Definition.Id).First();
                remaining.Remove(second);
                pairs.Add(new DefensePairData
                {
                    firstUnitId = first.Definition.Id,
                    secondUnitId = second.Definition.Id
                });
            }
            return pairs.ToArray();
        }

        private static ShortRangeDefenseData[] DefaultShortRangeAssignments(
            MissileEngagement engagement, TaskForceState defender)
        {
            var assignments = new List<ShortRangeDefenseData>();
            foreach (var ship in defender.ActiveUnits.Where(unit => unit.EffectiveShortSam > 0))
            {
                var pairMate = engagement.PairMate(ship.Definition.Id);
                var salvo = engagement.Salvos.Where(item => item.RemainingFactors > 0 &&
                    (item.TargetUnitId == ship.Definition.Id || item.TargetUnitId == pairMate))
                    .OrderByDescending(item => item.RemainingFactors).FirstOrDefault();
                if (salvo != null) assignments.Add(new ShortRangeDefenseData
                {
                    defendingUnitId = ship.Definition.Id,
                    salvoId = salvo.Id
                });
            }
            return assignments.ToArray();
        }

        private void ExecutePlanGunDecision()
        {
            var engagement = State.PendingGunCombat;
            if (engagement == null) return;
            switch (engagement.Phase)
            {
                case GunCombatPhase.EngageDecision:
                    Execute(new GameCommand(GameCommandType.BreakOff, Side.Plan,
                        State.Revision, enabled: false));
                    break;
                case GunCombatPhase.ArrangeAttacker:
                case GunCombatPhase.ArrangeDefender:
                    Execute(new GameCommand(GameCommandType.ArrangeGunfire, Side.Plan,
                        State.Revision, gunPairs: DefaultGunPairs(Side.Plan ==
                            State.Formation(engagement.AttackerFormationId).Side
                            ? State.Formation(engagement.AttackerFormationId)
                            : State.Formation(engagement.DefenderFormationId))));
                    break;
                case GunCombatPhase.Firing:
                {
                    var shooterId = engagement.FiringOrder[engagement.FiringIndex];
                    var targetForce = State.Forces.First(force => force.Side == Side.UsNavy && !force.IsDestroyed);
                    var target = targetForce.Objective != null && !targetForce.Objective.IsSunk
                        ? targetForce.Objective : targetForce.ActiveUnits.First();
                    Execute(new GameCommand(GameCommandType.FireGuns, Side.Plan, State.Revision,
                        targetId: target.Definition.Id, sourceUnitId: shooterId));
                    break;
                }
                case GunCombatPhase.BreakOffAttacker:
                case GunCombatPhase.BreakOffDefender:
                    Execute(new GameCommand(GameCommandType.BreakOff, Side.Plan,
                        State.Revision, enabled: false));
                    break;
            }
        }

        public static GunPairData[] DefaultGunPairs(TaskForceState force)
        {
            var active = force.ActiveUnits.ToList();
            var result = new List<GunPairData>();
            if (force.DefensePairs.Count > 0)
            {
                foreach (var pair in force.DefensePairs)
                {
                    var first = active.FirstOrDefault(unit => unit.Definition.Id == pair.firstUnitId);
                    var second = active.FirstOrDefault(unit => unit.Definition.Id == pair.secondUnitId);
                    if (first == null || second == null) continue;
                    var firing = first.EffectiveGuns >= second.EffectiveGuns ? first : second;
                    var screened = firing == first ? second : first;
                    result.Add(new GunPairData
                    {
                        firingUnitId = firing.Definition.Id,
                        screenedUnitId = screened.Definition.Id
                    });
                    active.Remove(first);
                    active.Remove(second);
                }
            }
            while (active.Count > 0)
            {
                var firing = active.OrderByDescending(unit => unit.EffectiveGuns).First();
                active.Remove(firing);
                UnitState screened = null;
                if (active.Count > 0)
                {
                    screened = active.OrderBy(unit => unit.EffectiveGuns).First();
                    active.Remove(screened);
                }
                result.Add(new GunPairData
                {
                    firingUnitId = firing.Definition.Id,
                    screenedUnitId = screened?.Definition.Id ?? string.Empty
                });
            }
            return result.ToArray();
        }

        private HexCoord BestEnemyDestination()
        {
            var enemyForce = State.ActiveForce ?? State.Enemy;
            return State.Map.NavigableNeighbors(enemyForce.Position, Side.Plan).Where(_isNavigable)
                .OrderBy(hex => hex.DistanceTo(State.Player.Position))
                .DefaultIfEmpty(enemyForce.Position).First();
        }

        private void TryEnemyDetection(TaskForceState observer)
        {
            if (!State.DetectionRulesEnabled || observer == null) return;
            var target = FindDetectionTarget(observer.Side, string.Empty, "visual");
            if (target == null) return;
            do
            {
                var result = Execute(new GameCommand(GameCommandType.Search, observer.Side, State.Revision,
                    targetId: target.Id, searchMode: "visual"));
                if (!result.Accepted) break;
            } while (!State.Detection.IsDetected(observer.Side, target.Id) && observer.MovementRemaining > 0);
        }

        private TaskForceState FindOpponent(Side observer, string targetId)
        {
            var opponents = State.Forces.Where(force => force.Side != observer && !force.IsDestroyed).ToArray();
            if (!string.IsNullOrWhiteSpace(targetId))
            {
                var named = opponents.FirstOrDefault(force => force.Id == targetId);
                if (named != null) return named;
            }
            var origin = State.ForceFor(observer).Position;
            return opponents.OrderBy(force => force.Position.DistanceTo(origin)).First();
        }

        private TaskForceState FindDetectionTarget(Side observer, string targetId, string mode)
        {
            var origin = State.ForceFor(observer).Position;
            var candidates = State.Forces.Where(force => force.Side != observer && !force.IsDestroyed);
            if (!string.IsNullOrWhiteSpace(targetId))
                candidates = candidates.Where(force => force.Id == targetId);
            if (string.Equals(mode, "visual", StringComparison.OrdinalIgnoreCase))
                candidates = candidates.Where(force => force.Position == origin);
            else if (string.Equals(mode, "esm", StringComparison.OrdinalIgnoreCase))
                candidates = candidates.Where(force => force.RadarRadiating && force.Position.DistanceTo(origin) == 1);
            return candidates.OrderBy(force => force.Position.DistanceTo(origin)).FirstOrDefault();
        }

        private void ResolveMovementDetection(TaskForceState moving)
        {
            ResolveAutomaticRadar(moving);
            var opponents = State.Forces.Where(force => force.Side != moving.Side && !force.IsDestroyed).ToArray();
            foreach (var opponent in opponents)
            {
                if (moving.Position.DistanceTo(opponent.Position) == 1)
                {
                    if (opponent.RadarRadiating && moving.CanUseEsm &&
                        !State.Detection.IsDetected(moving.Side, opponent.Id))
                        RecordDetection(moving.Side, opponent, DetectionMethod.Esm,
                            _detection.ResolveEsm(moving, opponent));
                    if (moving.RadarRadiating && opponent.CanUseEsm &&
                        !State.Detection.IsDetected(opponent.Side, moving.Id))
                        RecordDetection(opponent.Side, moving, DetectionMethod.Esm,
                            _detection.ResolveEsm(opponent, moving));
                }
                if (moving.Position == opponent.Position)
                {
                    if (opponent.RadarRadiating) ResolveAutomaticRadar(opponent);
                    else if (!State.Detection.IsDetected(opponent.Side, moving.Id))
                        RecordDetection(opponent.Side, moving, DetectionMethod.Visual,
                        _detection.ResolveVisual(opponent, moving, State.TimeOfDay));
                }
                var enemyBase = State.Map.BaseAt(moving.Position);
                if (enemyBase != null && enemyBase.Side != moving.Side)
                    RecordDetection(enemyBase.Side, moving, DetectionMethod.Visual, true);
            }
        }

        private void ResolveAutomaticRadar(TaskForceState observer)
        {
            if (!State.DetectionRulesEnabled || observer == null || !observer.RadarRadiating) return;
            foreach (var target in State.Forces.Where(force => force.Side != observer.Side &&
                         !force.IsDestroyed && force.Position == observer.Position &&
                         !State.Detection.IsDetected(observer.Side, force.Id)))
                RecordDetection(observer.Side, target, DetectionMethod.SurfaceSearchRadar, true);
        }

        private void RecordDetection(Side observer, TaskForceState target, DetectionMethod method, bool success)
        {
            if (!success)
            {
                Trace("DETECTION", $"{observer} found no contact on {target.Id} by {method}.");
                return;
            }
            var contact = State.Detection.Detect(observer, target, method, State.Turn, true);
            Trace("DETECTION", $"{observer} detected and classified {target.Id} at {target.Position} by {method}; " +
                "the contact is shared with all friendly task forces.");
            AddLog($"{observer} contact: {target.Id} classified at {contact.LastKnownPosition} ({method}).");
        }

        private void CompleteTurn()
        {
            CheckGameOver();
            if (State.IsGameOver) return;
            if (State.MaximumTurns > 0 && State.Turn >= State.MaximumTurns)
            {
                EndByScore(ScenarioEndReason.TurnLimit);
                return;
            }
            State.Turn++;
            Trace("TURN", $"Previous turn complete; advancing to turn {State.Turn}.");
            BeginTurn();
        }

        private void CheckGameOver()
        {
            if (State.IsGameOver) return;
            if (State.ObjectiveFor(Side.UsNavy).IsSunk || State.ObjectiveFor(Side.Plan).IsSunk)
            {
                EndByScore(ScenarioEndReason.ObjectiveSunk);
                return;
            }
            if (!State.Forces.Where(force => force.Side == Side.UsNavy).SelectMany(force => force.ActiveUnits).Any() ||
                !State.Forces.Where(force => force.Side == Side.Plan).SelectMany(force => force.ActiveUnits).Any())
            {
                EndByScore(ScenarioEndReason.ForceDestroyed);
                return;
            }

            var score = CurrentScore();
            var usCanScore = CanInflictFurtherDamage(Side.UsNavy);
            var planCanScore = CanInflictFurtherDamage(Side.Plan);
            var planObjective = ObjectiveUnit(Side.Plan);
            var usObjective = ObjectiveUnit(Side.UsNavy);
            var usCeiling = score.UsObjectiveDamage + (usCanScore ? planObjective.HullRemaining : 0);
            var planCeiling = score.PlanObjectiveDamage + (planCanScore ? usObjective.HullRemaining : 0);
            if (score.UsObjectiveDamage > planCeiling || score.PlanObjectiveDamage > usCeiling ||
                (!usCanScore && !planCanScore))
                EndByScore(ScenarioEndReason.FixedResult);
        }

        private void PruneDestroyedMovementChits()
        {
            if (State.MovementCup == null) return;
            foreach (var force in State.Forces.Where(force => force.IsDestroyed))
                if (State.MovementCup.RemoveUndrawnFormation(force.Id))
                    Trace("CHIT", $"Removed destroyed formation {force.Id}'s undrawn movement chit from the cup.");
        }

        public ScenarioScore CurrentScore()
        {
            var scenario = State.Scenario ?? FirstIslandChainScenarios.ContactOffBashiChannel;
            return new ScenarioScore(
                State.Unit(scenario.PlanObjectiveUnitId)?.HullDamage ?? 0,
                State.Unit(scenario.UsObjectiveUnitId)?.HullDamage ?? 0,
                State.Unit(scenario.PlanTieBreakUnitId)?.HullDamage ?? 0,
                State.Unit(scenario.UsTieBreakUnitId)?.HullDamage ?? 0);
        }

        private UnitState ObjectiveUnit(Side side)
        {
            var scenario = State.Scenario ?? FirstIslandChainScenarios.ContactOffBashiChannel;
            return State.Unit(side == Side.UsNavy ? scenario.UsObjectiveUnitId : scenario.PlanObjectiveUnitId)
                   ?? State.ObjectiveFor(side);
        }

        private bool CanInflictFurtherDamage(Side side) => State.Forces.Where(force => force.Side == side)
            .SelectMany(force => force.ActiveUnits).Any(unit => unit.AvailableShortSsm > 0 ||
                unit.AvailableLongSsm > 0 || unit.EffectiveGuns > 0 || unit.EffectiveTorpedoes > 0);

        private void EndByScore(ScenarioEndReason reason)
        {
            var score = CurrentScore();
            State.Result = score.Result;
            State.IsGameOver = true;
            State.Phase = ActivationPhase.GameOver;
            State.EndReason = reason;
            Trace("VICTORY", $"{State.Result}; reason={reason}; objective damage US/PLAN=" +
                $"{score.UsObjectiveDamage}/{score.PlanObjectiveDamage}; analogous escort tie-break US/PLAN=" +
                $"{score.UsTieBreakDamage}/{score.PlanTieBreakDamage}.");
            AddLog($"{State.Result} ({reason}): objective damage US {score.UsObjectiveDamage}, " +
                $"PLAN {score.PlanObjectiveDamage}; escort tie-break US {score.UsTieBreakDamage}, " +
                $"PLAN {score.PlanTieBreakDamage}.");
        }

        private bool DisengageInternal(Side side, out RuleViolation violation)
        {
            violation = null;
            if (side != State.ActiveSide)
            {
                violation = new RuleViolation(RuleViolationCode.WrongSide,
                    $"Only the active side ({State.ActiveSide}) may disengage.", "actor");
                Trace("REJECTED", violation.Message);
                return false;
            }
            if (State.Phase == ActivationPhase.MissileCombat || State.Phase == ActivationPhase.GunCombat)
            {
                violation = new RuleViolation(RuleViolationCode.WrongPhase,
                    "Resolve the pending combat before disengaging.", "phase");
                Trace("REJECTED", violation.Message);
                return false;
            }
            Trace("VICTORY", $"{side} disengaged; score is now final.");
            EndByScore(ScenarioEndReason.Disengagement);
            return true;
        }

        private bool RequestScoringInternal(Side side, out RuleViolation violation)
        {
            violation = null;
            if (side == Side.UsNavy) State.UsRequestedScoring = true;
            else State.PlanRequestedScoring = true;
            Trace("VICTORY", $"{side} requested mutual scoring.");
            if (State.UsRequestedScoring && State.PlanRequestedScoring)
                EndByScore(ScenarioEndReason.MutualScoring);
            return true;
        }

        private bool ConcedeInternal(Side side)
        {
            State.Result = side == Side.UsNavy ? "PLAN VICTORY" : "US NAVY VICTORY";
            State.IsGameOver = true;
            State.Phase = ActivationPhase.GameOver;
            State.EndReason = ScenarioEndReason.Concession;
            Trace("VICTORY", $"{side} conceded; {State.Result}.");
            AddLog($"{side} conceded. {State.Result}.");
            return true;
        }

        public static string CompareScore(int objectiveDamageInflicted, int objectiveDamageSuffered,
            int combatantDamageInflicted, int combatantDamageSuffered)
        {
            if (objectiveDamageInflicted != objectiveDamageSuffered)
                return objectiveDamageInflicted > objectiveDamageSuffered ? "US NAVY VICTORY" : "PLAN VICTORY";
            if (combatantDamageInflicted != combatantDamageSuffered)
                return combatantDamageInflicted > combatantDamageSuffered ? "US NAVY VICTORY" : "PLAN VICTORY";
            return "DRAW";
        }

        public ScenarioOneSnapshot CaptureSnapshot()
            => CaptureSnapshotFor(State.ActiveSide);

        public ScenarioOneSnapshot CaptureSnapshotFor(Side viewer)
        {
            // Scenario 1 explicitly omits detection, so both formations are public. The viewer
            // parameter establishes the projection boundary used by later hidden-information scenarios.
            var view = State.ViewFor(viewer, true);
            return new ScenarioOneSnapshot
            {
                scenarioId = State.Scenario?.Id ?? "fic-01",
                seed = Seed,
                detectionRulesEnabled = State.DetectionRulesEnabled,
                revision = view.Revision,
                turn = State.Turn,
                phase = State.Phase,
                activeSide = State.ActiveSide,
                activeFormationId = State.ActiveFormationId,
                usActivated = State.UsActivated,
                planActivated = State.PlanActivated,
                hasMoved = State.PlayerHasMoved,
                hasAttacked = State.PlayerHasAttacked,
                hasSearched = State.PlayerHasSearched,
                gameOver = State.IsGameOver,
                result = State.Result,
                endReason = State.EndReason,
                usRequestedScoring = State.UsRequestedScoring,
                planRequestedScoring = State.PlanRequestedScoring,
                usColumn = State.Player.Position.Column,
                usRow = State.Player.Position.Row,
                planColumn = State.Enemy.Position.Column,
                planRow = State.Enemy.Position.Row,
                usDeclaredSpeed = State.Player.DeclaredSpeed,
                usMovementSpent = State.Player.MovementPointsSpent,
                planDeclaredSpeed = State.Enemy.DeclaredSpeed,
                planMovementSpent = State.Enemy.MovementPointsSpent,
                usMovementPath = State.Player.MovementPath.Select(ToSnapshot).ToArray(),
                planMovementPath = State.Enemy.MovementPath.Select(ToSnapshot).ToArray(),
                units = State.Forces.SelectMany(force => force.Units).Select(unit => new UnitSnapshot
                {
                    id = unit.Definition.Id,
                    hullDamage = unit.HullDamage,
                    shortMissiles = unit.ShortMissilesRemaining,
                    longMissiles = unit.LongMissilesRemaining
                }).ToArray(),
                eventLog = State.Log.ToArray(),
                transactions = State.Transactions.Select(item => new TransactionSnapshot
                {
                    sequence = item.Sequence,
                    turn = item.Turn,
                    phase = item.Phase,
                    category = item.Category,
                    detail = item.Detail
                }).ToArray(),
                events = State.Events.Select(item => new RuleEventSnapshot
                {
                    sequence = item.Sequence,
                    revision = item.Revision,
                    turn = item.Turn,
                    phase = item.Phase,
                    type = item.Type,
                    actor = item.Actor,
                    commandId = item.CommandId,
                    detail = item.Detail
                }).ToArray(),
                commands = State.CommandLog.Select(item => new GameCommandData
                {
                    id = item.id,
                    type = item.type,
                    actor = item.actor,
                    expectedRevision = item.expectedRevision,
                    column = item.column,
                    row = item.row,
                    declaredSpeed = item.declaredSpeed,
                    factors = item.factors,
                    targetId = item.targetId,
                    sourceUnitId = item.sourceUnitId,
                    enabled = item.enabled,
                    formationId = item.formationId,
                    newFormationId = item.newFormationId,
                    unitIds = item.unitIds?.ToArray() ?? Array.Empty<string>(),
                    searchMode = item.searchMode,
                    missileAllocations = item.missileAllocations?.ToArray() ?? Array.Empty<MissileAllocationData>(),
                    defensePairs = item.defensePairs?.ToArray() ?? Array.Empty<DefensePairData>(),
                    missileReductions = item.missileReductions?.ToArray() ?? Array.Empty<MissileReductionData>(),
                    shortRangeDefenses = item.shortRangeDefenses?.ToArray() ?? Array.Empty<ShortRangeDefenseData>(),
                    gunPairs = item.gunPairs?.ToArray() ?? Array.Empty<GunPairData>()
                }).ToArray(),
                remainingChits = State.MovementCup.Remaining.Select(item => item.ToData()).ToArray(),
                drawnChits = State.MovementCup.Drawn.Select(item => item.ToData()).ToArray(),
                formations = State.Forces.Select(force => new FormationSnapshot
                {
                    id = force.Id,
                    side = force.Side,
                    column = force.Position.Column,
                    row = force.Position.Row,
                    declaredSpeed = force.DeclaredSpeed,
                    movementSpent = force.MovementPointsSpent,
                    movementPath = force.MovementPath.Select(ToSnapshot).ToArray(),
                    unitIds = force.Units.Select(unit => unit.Definition.Id).ToArray(),
                    radarRadiating = force.RadarRadiating,
                    radarDeclared = force.RadarDeclaredThisActivation,
                    defensePairs = force.DefensePairs.ToArray()
                }).ToArray(),
                contacts = State.Detection.Contacts.Select(contact => contact.ToData()).ToArray(),
                missileCombat = State.PendingMissileCombat?.ToData(),
                gunCombat = State.PendingGunCombat?.ToData()
            };
        }

        public void ApplySnapshot(ScenarioOneSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.seed != 0) Seed = snapshot.seed;
            State.Revision = snapshot.revision;
            State.Turn = snapshot.turn;
            State.Phase = snapshot.phase;
            State.ActiveSide = snapshot.activeSide;
            State.ActiveFormationId = snapshot.activeFormationId ?? string.Empty;
            State.UsActivated = snapshot.usActivated;
            State.PlanActivated = snapshot.planActivated;
            State.PlayerHasMoved = snapshot.hasMoved;
            State.PlayerHasAttacked = snapshot.hasAttacked;
            State.PlayerHasSearched = snapshot.hasSearched;
            State.IsGameOver = snapshot.gameOver;
            State.Result = snapshot.result ?? string.Empty;
            State.EndReason = snapshot.endReason;
            State.UsRequestedScoring = snapshot.usRequestedScoring;
            State.PlanRequestedScoring = snapshot.planRequestedScoring;
            if (snapshot.formations != null && snapshot.formations.Length > 0)
            {
                var availableUnits = State.Forces.SelectMany(force => force.Units)
                    .ToDictionary(unit => unit.Definition.Id);
                var restoredForces = snapshot.formations.Select(item =>
                {
                    var force = new TaskForceState(item.id, item.side, new HexCoord(item.column, item.row),
                        (item.unitIds ?? Array.Empty<string>()).Where(availableUnits.ContainsKey)
                        .Select(id => availableUnits[id]));
                    force.RestoreMovement(item.declaredSpeed, item.movementSpent,
                        (item.movementPath ?? Array.Empty<HexCoordSnapshot>())
                        .Select(hex => new HexCoord(hex.column, hex.row)));
                    force.RestoreSensors(item.radarRadiating, item.radarDeclared);
                    force.SetDefensePairs(item.defensePairs);
                    return force;
                }).ToArray();
                State.ReplaceForces(restoredForces);
            }
            else
            {
                State.Player.MoveTo(new HexCoord(snapshot.usColumn, snapshot.usRow));
                State.Enemy.MoveTo(new HexCoord(snapshot.planColumn, snapshot.planRow));
            }
            State.Player.RestoreMovement(snapshot.usDeclaredSpeed, snapshot.usMovementSpent,
                (snapshot.usMovementPath ?? Array.Empty<HexCoordSnapshot>())
                .Select(item => new HexCoord(item.column, item.row)));
            State.Enemy.RestoreMovement(snapshot.planDeclaredSpeed, snapshot.planMovementSpent,
                (snapshot.planMovementPath ?? Array.Empty<HexCoordSnapshot>())
                .Select(item => new HexCoord(item.column, item.row)));
            foreach (var unitSnapshot in snapshot.units ?? Array.Empty<UnitSnapshot>())
            {
                var unit = State.Forces.SelectMany(force => force.Units)
                    .FirstOrDefault(candidate => candidate.Definition.Id == unitSnapshot.id);
                unit?.Restore(unitSnapshot.hullDamage, unitSnapshot.shortMissiles, unitSnapshot.longMissiles);
            }
            State.Log.Clear();
            State.Log.AddRange(snapshot.eventLog ?? Array.Empty<string>());
            State.Transactions.Clear();
            foreach (var item in snapshot.transactions ?? Array.Empty<TransactionSnapshot>())
                State.Transactions.Add(new RuleTransaction(item.sequence, item.turn, item.phase,
                    item.category, item.detail));
            State.Events.Clear();
            foreach (var item in snapshot.events ?? Array.Empty<RuleEventSnapshot>())
                State.Events.Add(new RuleEvent(item.sequence, item.revision, item.turn, item.phase,
                    item.type, item.actor, item.commandId, item.detail));
            State.CommandLog.Clear();
            State.CommandLog.AddRange(snapshot.commands ?? Array.Empty<GameCommandData>());
            State.MovementCup.Restore(snapshot.remainingChits, snapshot.drawnChits);
            State.Detection.Restore(snapshot.contacts);
            State.PendingMissileCombat = MissileEngagement.FromData(snapshot.missileCombat);
            State.PendingGunCombat = GunEngagement.FromData(snapshot.gunCombat);
        }

        public static ScenarioOneGame Replay(int seed, IEnumerable<GameCommandData> commands,
            Func<HexCoord, bool> isNavigable = null, bool detectionRulesEnabled = false,
            bool manualOpponent = true)
        {
            var replay = new ScenarioOneGame(seed, isNavigable, true, detectionRulesEnabled);
            foreach (var data in commands ?? Array.Empty<GameCommandData>())
            {
                var result = replay.Execute(GameCommand.FromData(data));
                if (!result.Accepted)
                    throw new InvalidOperationException($"Replay rejected command {data.id}: {result.Summary}");
            }
            replay._manualOpponent = manualOpponent;
            return replay;
        }

        private void AddLog(string message) => State.Log.Add(message);
        private static HexCoordSnapshot ToSnapshot(HexCoord hex) => new HexCoordSnapshot
        {
            column = hex.Column,
            row = hex.Row
        };
        private void Trace(string category, string detail) => State.Trace(category, detail);
    }
}
