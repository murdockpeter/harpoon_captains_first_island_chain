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
    }

    [Serializable]
    public sealed class ScenarioOneSnapshot
    {
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
    }

    public static class ScenarioOne
    {
        public const string Name = "Contact off the Bashi Channel";

        public static GameState Create(bool detectionRulesEnabled = false)
        {
            var burke = new UnitDefinition("us-burke-iia", "Arleigh Burke Flight IIA", Side.UsNavy,
                UnitRole.Escort, 3, 8, 4, 2, 1, 2, 3, 2, 2, 1, 4, 5);
            var merchant = new UnitDefinition("us-merchant", "Merchant Ship", Side.UsNavy,
                UnitRole.Objective, 0, 0, 0, 0, 0, 0, 2, 4, 0, 1, 0, 0, esmEquipped: false);
            var type054A = new UnitDefinition("plan-type-054a", "Type 054A Frigate", Side.Plan,
                UnitRole.Escort, 3, 0, 3, 2, 0, 1, 2, 1, 1, 1, 3, 3);
            var type071 = new UnitDefinition("plan-type-071", "Type 071 LPD", Side.Plan,
                UnitRole.Objective, 0, 0, 2, 0, 0, 1, 2, 3, 1, 1, 0, 1);

            var us = new TaskForceState("US Task Force", Side.UsNavy, new HexCoord(7, 13),
                new[] { new UnitState(burke), new UnitState(merchant) });
            var plan = new TaskForceState("PLAN Task Force", Side.Plan, new HexCoord(10, 10),
                new[] { new UnitState(type054A), new UnitState(type071) });
            var state = new GameState(us, plan, 0, FirstIslandChainMap.Instance, detectionRulesEnabled);
            state.Log.Add($"Scenario 1: {Name}");
            state.Log.Add("Damage the enemy objective ship while protecting your merchant.");
            state.Trace("SETUP", $"Scenario 1 '{Name}' loaded; no turn limit; detection rules " +
                (detectionRulesEnabled ? "enabled for rules testing." : "omitted by the printed learning scenario."));
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
        private readonly Func<HexCoord, bool> _isNavigable;
        private readonly bool _manualOpponent;
        private bool _isActivatingEnemy;
        public GameState State { get; private set; }
        public int Seed { get; }
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
                    case GameCommandType.Search:
                        accepted = SearchInternal(command, out violation);
                        break;
                    case GameCommandType.EndActivation:
                        accepted = EndActivationInternal(command.Actor, out violation);
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
            var report = _combat.Attack(attacker, defender);
            State.PlayerHasAttacked = report.Fired;
            AddLog(report.Summary);
            if (!report.Fired)
            {
                violation = new RuleViolation(RuleViolationCode.NoLegalWeapon, report.Summary);
                return report;
            }
            if (report.Fired) AttackResolved?.Invoke(side, report);
            CheckGameOver();
            return report;
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
                    var enemyForce = State.ActiveForce ?? State.Enemy;
                    if (State.Phase == ActivationPhase.DeclareSpeed)
                    {
                        if (State.DetectionRulesEnabled && !enemyForce.RadarDeclaredThisActivation)
                        {
                            var targetRange = enemyForce.Position.DistanceTo(State.Player.Position);
                            Execute(new GameCommand(GameCommandType.RadiateRadar, Side.Plan,
                                State.Revision, enabled: targetRange <= 1));
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
                        if (!State.PlayerHasAttacked && enemyForce.Position.DistanceTo(State.Player.Position) <= 3 &&
                            (!State.DetectionRulesEnabled || State.Detection.IsDetected(Side.Plan, State.Player.Id)))
                            Execute(new GameCommand(GameCommandType.Attack, Side.Plan, State.Revision));
                    }
                    TryEnemyDetection(enemyForce);
                    if (!State.IsGameOver && State.Phase == ActivationPhase.PlayerAction &&
                        !State.PlayerHasAttacked && (!State.DetectionRulesEnabled ||
                        State.Detection.IsDetected(Side.Plan, State.Player.Id)))
                        Execute(new GameCommand(GameCommandType.Attack, Side.Plan, State.Revision));
                    if (!State.IsGameOver && State.ActiveSide == Side.Plan)
                        Execute(new GameCommand(GameCommandType.EndActivation, Side.Plan, State.Revision));
                }
            }
            finally { _isActivatingEnemy = false; }
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
            if (State.MaximumTurns > 0 && State.Turn >= State.MaximumTurns) { EndByScore(); return; }
            State.Turn++;
            Trace("TURN", $"Previous turn complete; advancing to turn {State.Turn}.");
            BeginTurn();
        }

        private void CheckGameOver()
        {
            if (State.ObjectiveFor(Side.UsNavy).IsSunk || State.ObjectiveFor(Side.Plan).IsSunk) EndByScore();
        }

        private void EndByScore()
        {
            var damageInflicted = State.ObjectiveFor(Side.Plan).HullDamage;
            var damageSuffered = State.ObjectiveFor(Side.UsNavy).HullDamage;
            var combatantDamageInflicted = State.Forces.Where(force => force.Side == Side.Plan).SelectMany(force => force.Units)
                .Where(unit => unit.Definition.Role != UnitRole.Objective).Sum(unit => unit.HullDamage);
            var combatantDamageSuffered = State.Forces.Where(force => force.Side == Side.UsNavy).SelectMany(force => force.Units)
                .Where(unit => unit.Definition.Role != UnitRole.Objective).Sum(unit => unit.HullDamage);
            State.Result = CompareScore(damageInflicted, damageSuffered,
                combatantDamageInflicted, combatantDamageSuffered);
            State.IsGameOver = true;
            State.Phase = ActivationPhase.GameOver;
            Trace("VICTORY", $"{State.Result}; objective damage inflicted/suffered={damageInflicted}/{damageSuffered}; " +
                  $"combatant tie-break inflicted/suffered={combatantDamageInflicted}/{combatantDamageSuffered}.");
            AddLog($"{State.Result}: objective damage US {damageSuffered}, PLAN {damageInflicted}; " +
                   $"combatant tie-break US {combatantDamageSuffered}, PLAN {combatantDamageInflicted}.");
        }

        private bool ConcedeInternal(Side side)
        {
            State.Result = side == Side.UsNavy ? "PLAN VICTORY" : "US NAVY VICTORY";
            State.IsGameOver = true;
            State.Phase = ActivationPhase.GameOver;
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
                    enabled = item.enabled,
                    formationId = item.formationId,
                    newFormationId = item.newFormationId,
                    unitIds = item.unitIds?.ToArray() ?? Array.Empty<string>(),
                    searchMode = item.searchMode
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
                    radarDeclared = force.RadarDeclaredThisActivation
                }).ToArray(),
                contacts = State.Detection.Contacts.Select(contact => contact.ToData()).ToArray()
            };
        }

        public void ApplySnapshot(ScenarioOneSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
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
        }

        public static ScenarioOneGame Replay(int seed, IEnumerable<GameCommandData> commands,
            Func<HexCoord, bool> isNavigable = null, bool detectionRulesEnabled = false)
        {
            var replay = new ScenarioOneGame(seed, isNavigable, true, detectionRulesEnabled);
            foreach (var data in commands ?? Array.Empty<GameCommandData>())
            {
                var result = replay.Execute(GameCommand.FromData(data));
                if (!result.Accepted)
                    throw new InvalidOperationException($"Replay rejected command {data.id}: {result.Summary}");
            }
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
