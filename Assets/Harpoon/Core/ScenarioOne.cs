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
        public int gunfireHullDamage;
        public int shortMissiles;
        public int longMissiles;
        public int embarkedAircraft;
        public int serviceableAircraft;
        public AircraftMissionState aircraftMissionState;
        public int aircraftReadyTurn;
        public int aircraftLastAttackTurn;
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
        public bool arrived;
        public bool entered = true;
        public int dummyCards;
        public DefensePairData[] defensePairs;
        public string[] aircraftSearchModes;
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
        public bool hasProjectedScore;
        public int scoreUsObjective;
        public int scorePlanObjective;
        public int scoreUsTieBreak;
        public int scorePlanTieBreak;
        public string scoreResult;
        public TacticalFlightSnapshot[] tacticalFlights;
        public AirBaseSnapshot[] airBases;
    }

    public static class ScenarioOne
    {
        public const string Name = "Contact off the Bashi Channel";

        public static GameState Create(bool detectionRulesEnabled = false, ScenarioDefinition definition = null)
        {
            definition ??= FirstIslandChainScenarios.ContactOffBashiChannel;
            var formations = definition.Formations.Select(item => new TaskForceState(item.Id, item.Side, item.Start,
                item.Units.Select(slot => new UnitState(CreateScenarioUnit(item.Side, slot))), item.DummyCards,
                item.EntryEdge != BoardEdge.None)).ToArray();
            var us = formations.First(item => item.Side == Side.UsNavy);
            var plan = formations.First(item => item.Side == Side.Plan);
            var useDetection = detectionRulesEnabled || definition.DetectionRulesEnabled;
            var state = new GameState(us, plan, definition.MaximumTurns, FirstIslandChainMap.Instance,
                useDetection, definition);
            foreach (var formation in formations.Where(item => item != us && item != plan)) state.AddForce(formation);
            if (definition.TacticalAirEnabled)
            {
                var bases = definition.AirBaseIds.Select(id => new AirBaseState(ModernAirBaseDatabase.Get(id))).ToArray();
                var flights = definition.TacticalFlights.Select(item => new TacticalFlightState(item.Id,
                    ModernTacticalAircraftDatabase.Get(item.AircraftId), item.BaseId, item.Strength)).ToArray();
                state.ConfigureTacticalAir(flights, bases);
            }
            state.Log.Add($"{definition.Subtitle}: {definition.Name}");
            state.Log.Add(definition.VictoryText);
            state.Trace("SETUP", $"Scenario '{definition.Id}' loaded from data; " +
                (definition.MaximumTurns == 0 ? "no printed turn limit" : $"{definition.MaximumTurns}-turn limit") +
                "; detection rules " + (useDetection ? "enabled." : "omitted by the printed learning scenario."));
            state.Trace("SETUP", $"US Task Force at {us.Position}: {string.Join(", ", us.Units.Select(unit => unit.Definition.DisplayName))}.");
            state.Trace("SETUP", $"PLAN Task Force at {plan.Position}: {string.Join(", ", plan.Units.Select(unit => unit.Definition.DisplayName))}.");
            return state;
        }

        private static UnitDefinition CreateScenarioUnit(Side side, ScenarioUnitDefinition slot)
        {
            if (ModernAircraftDatabase.TryGet(slot.PlatformId, out var aircraft))
                return aircraft.CreateUnit(slot.Role, slot.UnitId);
            return ModernPlatformDatabase.Get(slot.PlatformId).CreateUnit(side, slot.Role, slot.UnitId);
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
        private ScenarioScore _projectedScore;
        public GameState State { get; private set; }
        public int Seed { get; private set; }
        public event Action<Side, AttackReport> AttackResolved;
        public event Action<GameCommand, CommandResult> CommandProcessed;

        public SideGameView ViewFor(Side viewer, bool? opponentKnown = null) =>
            State.ViewFor(viewer, opponentKnown);

        public ScenarioOneGame(int seed = 2026, Func<HexCoord, bool> isNavigable = null,
            bool manualOpponent = false, bool detectionRulesEnabled = false, IDieRoller dieRoller = null,
            ScenarioDefinition scenario = null)
        {
            Seed = seed;
            var random = dieRoller ?? new SeededDieRoller(seed);
            _dice = random;
            _isNavigable = isNavigable ?? (_ => true);
            _manualOpponent = manualOpponent;
            State = ScenarioOne.Create(detectionRulesEnabled, scenario);
            if ((State.Scenario.PlanDeploymentMinimumDistance > 0 || State.Scenario.HasDeploymentZones ||
                 State.Scenario.HasDistanceDeployment) &&
                !manualOpponent)
            {
                var legal = State.Map.AllHexes.Where(hex => State.Map.IsNavigable(hex, Side.Plan) &&
                    IsLegalDeploymentHex(State.Scenario, State.Map, Side.Plan, hex)).ToArray();
                foreach (var planForce in State.Forces.Where(force => force.Side == Side.Plan &&
                             State.Scenario.Formations.First(item => item.Id == force.Id).CanDeploy))
                {
                    var stableId = planForce.Id.Aggregate(17,
                        (value, character) => unchecked(value * 31 + character));
                    planForce.MoveTo(legal[new Random(seed ^ stableId).Next(legal.Length)]);
                }
                State.Trace("SETUP", "PLAN AI selected seeded legal concealed deployment hexes.");
            }
            State.MovementCup = new MovementChitCup(random as IRandomSource ?? new SeededDieRoller(seed));
            _combat = new CombatResolver(_dice, Trace);
            _detection = new DetectionResolver(_dice, Trace);
            _missileCombat = new MissileCombatResolver(_dice, Trace);
            _gunCombat = new GunCombatResolver(_dice, Trace);
            BeginTurn();
        }

        public CommandResult Execute(GameCommand command)
        {
            _projectedScore = null;
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
                    case GameCommandType.DeployFormation:
                        accepted = DeployFormationInternal(command, out violation);
                        break;
                    case GameCommandType.TransferDummyCards:
                        accepted = TransferDummyCardsInternal(command, out violation);
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
                    case GameCommandType.ExitMap:
                        accepted = ExitMapInternal(command.Actor, out violation);
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
                    case GameCommandType.AssignCap:
                        accepted = AssignDefensiveAirInternal(command, TacticalAirMission.Cap, out violation);
                        break;
                    case GameCommandType.AssignDeckInterceptor:
                        accepted = AssignDefensiveAirInternal(command, TacticalAirMission.DeckInterceptor, out violation);
                        break;
                    case GameCommandType.LaunchTacticalStrike:
                        accepted = LaunchTacticalStrikeInternal(command, out violation, out attackReport);
                        break;
                    default:
                        violation = new RuleViolation(RuleViolationCode.UnsupportedCommand,
                            $"{command.Type} is represented by the command protocol but is not used by {State.Scenario.Name}.", "type");
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
            State.Phase = State.ActiveForce?.IsAircraftOnly == true
                ? ActivationPhase.AircraftAction : ActivationPhase.DeclareSpeed;
            Trace("CHIT", $"Drew {chit.FormationId} ({chit.Side}); " +
                $"{State.MovementCup.Remaining.Count} chit(s) remain in the cup.");
            AddLog($"{chit.FormationId} chit drawn for {State.TimeLabel}.");
            if (State.ActiveForce?.IsAircraftOnly == true)
            {
                var aircraft = State.ActiveForce.ActiveUnits.FirstOrDefault();
                if (aircraft == null || !aircraft.CanFlyAircraft(State.Turn))
                {
                    Trace("ACTIVATION", $"{chit.FormationId} is unavailable until turn {aircraft?.AircraftReadyTurn ?? 0}; its chit is consumed.");
                    return EndActivationInternal(chit.Side, out violation);
                }
                if (State.ActiveForce.HasEnteredMap) ResolveAircraftDetectionAndSam(State.ActiveForce);
            }
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

        private bool DeployFormationInternal(GameCommand command, out RuleViolation violation)
        {
            violation = null;
            var scenario = State.Scenario;
            if ((scenario.PlanDeploymentMinimumDistance <= 0 && !scenario.HasDeploymentZones &&
                 !scenario.HasDistanceDeployment) ||
                State.Phase != ActivationPhase.AwaitingChit || State.MovementCup == null ||
                !State.MovementCup.FirstDrawPending)
            {
                violation = new RuleViolation(RuleViolationCode.DeploymentUnavailable,
                    "Scenario deployment is available only before the first movement-chit draw.", "phase");
                Trace("REJECTED", violation.Message);
                return false;
            }
            var force = string.IsNullOrWhiteSpace(command.FormationId)
                ? State.Forces.First(item => item.Side == command.Actor) : State.Formation(command.FormationId);
            var formationDefinition = force == null ? null : scenario.Formations.FirstOrDefault(item => item.Id == force.Id);
            if (force == null || force.Side != command.Actor || formationDefinition == null ||
                !formationDefinition.CanDeploy || force.IsAircraftOnly ||
                !IsLegalDeploymentHex(scenario, State.Map, command.Actor, command.Destination))
            {
                violation = new RuleViolation(RuleViolationCode.InvalidFormation,
                    scenario.HasDistanceDeployment
                        ? command.Actor == Side.UsNavy
                            ? $"US submarine setup must be at least {scenario.UsDeploymentMinimumDistance} hexes from Xiamen."
                            : $"PLAN submarine setup must be within {scenario.PlanDeploymentMaximumDistance} hexes of Xiamen."
                        : scenario.HasDeploymentZones
                        ? command.Actor == Side.UsNavy
                            ? $"US formations must deploy within {scenario.UsDeploymentRadius} hexes of {scenario.DeploymentCenter}."
                            : $"PLAN formations may not deploy at Taipei or within {scenario.PlanProhibitedRadius} hexes of {scenario.DeploymentCenter}."
                        : $"Deploy in PLAN-navigable water more than {scenario.PlanDeploymentMinimumDistance} hexes from both Subic Bay and Taipei / Zuoying.",
                    "destination");
                Trace("REJECTED", violation.Message);
                return false;
            }
            force.MoveTo(command.Destination);
            Trace("SETUP", $"PLAN player deployed {force.Id} to {command.Destination} outside both exclusion zones.");
            return true;
        }

        public static bool IsLegalDeploymentHex(ScenarioDefinition scenario, OperationalMap map,
            Side side, HexCoord destination)
        {
            if (scenario == null || map == null || !map.IsNavigable(destination, side)) return false;
            if (scenario.HasDistanceDeployment)
            {
                var distance = destination.DistanceTo(scenario.DistanceDeploymentCenter);
                return side == Side.UsNavy
                    ? distance >= scenario.UsDeploymentMinimumDistance
                    : distance <= scenario.PlanDeploymentMaximumDistance;
            }
            if (scenario.HasDeploymentZones)
            {
                if (side == Side.UsNavy)
                    return destination != scenario.UsDestination &&
                           destination.DistanceTo(scenario.DeploymentCenter) <= scenario.UsDeploymentRadius;
                return destination != scenario.UsDestination &&
                       destination.DistanceTo(scenario.DeploymentCenter) > scenario.PlanProhibitedRadius;
            }
            if (side != Side.Plan || scenario.PlanDeploymentMinimumDistance <= 0) return false;
            var subic = map.Bases.First(item => item.Id == "us-subic").Position;
            var taipei = map.Bases.First(item => item.Id == "us-taipei").Position;
            return destination.DistanceTo(subic) > scenario.PlanDeploymentMinimumDistance &&
                   destination.DistanceTo(taipei) > scenario.PlanDeploymentMinimumDistance;
        }

        private bool TransferDummyCardsInternal(GameCommand command, out RuleViolation violation)
        {
            violation = null;
            var source = State.Formation(command.FormationId);
            var target = State.Formation(command.TargetId);
            if (source == null || source.Side != command.Actor || command.Factors <= 0 ||
                command.Factors > source.DummyCards)
            {
                violation = new RuleViolation(RuleViolationCode.DummyActionUnavailable,
                    "Choose a friendly source containing enough dummy cards.", "factors");
                Trace("REJECTED", violation.Message);
                return false;
            }
            if (target == null && !string.IsNullOrWhiteSpace(command.NewFormationId))
            {
                if (State.Formation(command.NewFormationId) != null)
                {
                    violation = new RuleViolation(RuleViolationCode.InvalidFormation,
                        "That dummy task-force identity is already in use.", "newFormationId");
                    Trace("REJECTED", violation.Message);
                    return false;
                }
                target = new TaskForceState(command.NewFormationId, command.Actor, source.Position,
                    Array.Empty<UnitState>());
                State.AddForce(target);
                if (State.Phase == ActivationPhase.AwaitingChit && State.MovementCup.FirstDrawPending)
                    State.MovementCup.Reset(State.Forces);
            }
            if (target == null || target == source || target.Side != command.Actor)
            {
                violation = new RuleViolation(RuleViolationCode.InvalidFormation,
                    "Dummy cards may transfer only to another friendly task force.", "targetId");
                Trace("REJECTED", violation.Message);
                return false;
            }
            source.TryRemoveDummyCards(command.Factors);
            target.AddDummyCards(command.Factors);
            Trace("DUMMY", $"{command.Actor} openly verified transfer of {command.Factors} dummy card(s) " +
                $"from {source.Id} to {target.Id}; no real ship contents were revealed.");
            AddLog($"{command.Actor} openly verified transfer of {command.Factors} dummy card(s); " +
                "no real ships transferred.");
            if (source.Units.Count == 0 && source.DummyCards == 0)
            {
                State.MovementCup.RemoveUndrawnFormation(source.Id);
                State.RemoveForce(source);
            }
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
            var force = State.ActiveForce != null && State.ActiveForce.Side == side
                ? State.ActiveForce : State.ForceFor(side);
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
                    $"{State.Scenario.Name} omits detection and radar declarations.", "type");
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
            var force = State.ActiveForce;
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
            if (State.ActiveForce?.IsAircraftOnly == true)
                return RelocateAircraftInternal(side, destination, out violation);
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
            var force = State.ActiveForce;
            if (!force.HasEnteredMap)
            {
                if (force.MovementRemaining <= 0)
                {
                    violation = new RuleViolation(RuleViolationCode.MovementExhausted,
                        "The task force needs one movement point to enter the board.", "declaredSpeed");
                    Trace("REJECTED", violation.Message);
                    return false;
                }
                var formation = State.Scenario.Formations.FirstOrDefault(item => item.Id == force.Id);
                if (formation == null || formation.EntryEdge == BoardEdge.None ||
                    !IsBoardEdgeHex(State.Map, destination, formation.EntryEdge, side))
                {
                    violation = new RuleViolation(RuleViolationCode.OutsideMap,
                        $"{force.Id} must enter through a navigable {formation?.EntryEdge.ToString().ToLowerInvariant()} edge hex.",
                        "destination");
                    Trace("REJECTED", violation.Message);
                    return false;
                }
                force.EnterMap(destination);
                State.PlayerHasMoved = true;
                State.Phase = force.MovementRemaining == 0 ? ActivationPhase.PlayerAction : ActivationPhase.PlayerMove;
                AddLog($"{force.Id} entered from the {formation.EntryEdge.ToString().ToLowerInvariant()} edge at {destination}.");
                Trace("MOVEMENT", $"{force.Id} entered map at {destination}; step={force.MovementPointsSpent}/{force.DeclaredSpeed}.");
                if (State.DetectionRulesEnabled) ResolveMovementDetection(force);
                return true;
            }
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
            if (State.Scenario.HasPatrolLine && side == State.Scenario.PatrolRestrictedSide &&
                DistanceToPatrolLine(State.Scenario, destination) > State.Scenario.PatrolLineRadius)
            {
                violation = new RuleViolation(RuleViolationCode.ImpassableTerrain,
                    $"{side} formations must remain within {State.Scenario.PatrolLineRadius} hexes of the " +
                    $"{State.Scenario.PatrolLineStart}-{State.Scenario.PatrolLineEnd} patrol line.", "destination");
                Trace("REJECTED", violation.Message);
                return false;
            }
            var origin = force.Position;
            force.MoveOneHex(destination);
            if (side == Side.UsNavy && State.Scenario.HasUsDestination &&
                destination == State.Scenario.UsDestination &&
                force.ActiveUnits.Any(unit => unit.Definition.Role == UnitRole.Objective))
            {
                force.MarkArrived();
                State.MovementCup.RemoveUndrawnFormation(force.Id);
                Trace("VICTORY", $"{force.Id} reached Taipei / Zuoying with at least one merchant afloat.");
                AddLog($"{force.Id} entered Taipei / Zuoying and left the operational map.");
            }
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
            CheckGameOver();
            return true;
        }

        private bool RelocateAircraftInternal(Side side, HexCoord destination, out RuleViolation violation)
        {
            violation = null;
            var force = State.ActiveForce;
            var aircraft = force?.ActiveUnits.FirstOrDefault();
            if (State.Phase != ActivationPhase.AircraftAction || force == null || aircraft == null)
            {
                violation = new RuleViolation(RuleViolationCode.WrongPhase,
                    "Patrol-aircraft relocation is available only on its movement chit.", "phase");
                return false;
            }
            if (State.PlayerHasMoved)
            {
                violation = new RuleViolation(RuleViolationCode.AlreadyActed,
                    "A patrol-aircraft model relocates once when its chit is drawn.", "destination");
                return false;
            }
            var home = State.Scenario.Formations.First(item => item.Id == force.Id).Start;
            if (!State.Map.Contains(destination) || destination.DistanceTo(home) > aircraft.Definition.AircraftRadius)
            {
                violation = new RuleViolation(RuleViolationCode.OutsideMap,
                    $"{aircraft.Definition.DisplayName} must remain within radius {aircraft.Definition.AircraftRadius} of {home}.",
                    "destination");
                return false;
            }
            var origin = force.Position;
            force.RelocateAircraft(destination);
            State.PlayerHasMoved = true;
            Trace("MOVEMENT", $"{force.Id} patrol model relocated {origin}->{destination} within base radius {aircraft.Definition.AircraftRadius}.");
            AddLog($"{force.Id} established patrol station at {destination}.");
            ResolveAircraftDetectionAndSam(force);
            return true;
        }

        public static int DistanceToPatrolLine(ScenarioDefinition scenario, HexCoord destination)
        {
            if (scenario == null || !scenario.HasPatrolLine) return int.MaxValue;
            if (scenario.PatrolLineStart.Row == scenario.PatrolLineEnd.Row)
            {
                // Scenario 8's starting segment defines a westbound patrol axis; the two-hex
                // restriction follows that row to the board edge so the printed exit remains reachable.
                var first = scenario.ScoringMode == ScenarioScoringMode.CarrierEscape
                    ? FirstIslandChainMap.Instance.MinimumColumn
                    : Math.Min(scenario.PatrolLineStart.Column, scenario.PatrolLineEnd.Column);
                var last = scenario.ScoringMode == ScenarioScoringMode.CarrierEscape
                    ? FirstIslandChainMap.Instance.MaximumColumn
                    : Math.Max(scenario.PatrolLineStart.Column, scenario.PatrolLineEnd.Column);
                return Enumerable.Range(first, last - first + 1)
                    .Min(column => destination.DistanceTo(new HexCoord(column, scenario.PatrolLineStart.Row)));
            }
            return Math.Min(destination.DistanceTo(scenario.PatrolLineStart),
                destination.DistanceTo(scenario.PatrolLineEnd));
        }

        public static bool IsBoardEdgeHex(OperationalMap map, HexCoord hex, BoardEdge edge, Side side)
        {
            if (map == null || !map.IsNavigable(hex, side)) return false;
            if (edge == BoardEdge.East) return hex.Column == map.MaximumColumn;
            if (edge != BoardEdge.West) return false;
            return !map.AllHexes.Any(candidate => candidate.Row == hex.Row &&
                candidate.Column < hex.Column && map.IsNavigable(candidate, side));
        }

        public bool CanExitMap(TaskForceState force)
        {
            if (force == null || force.IsOffMap || State.Scenario.VictoryExitEdge == BoardEdge.None ||
                force.Side != Side.Plan || force.MovementRemaining <= 0) return false;
            if (State.Scenario.ScoringMode == ScenarioScoringMode.SubmarineEscape)
                return force.IsSubmarineOnly &&
                       IsBoardEdgeHex(State.Map, force.Position, State.Scenario.VictoryExitEdge, force.Side);
            var carrier = force.ActiveUnits.FirstOrDefault(unit => unit.Definition.IsAircraftCarrier);
            return carrier != null && carrier.CanLaunchAircraft &&
                   IsBoardEdgeHex(State.Map, force.Position, State.Scenario.VictoryExitEdge, force.Side);
        }

        private bool ExitMapInternal(Side side, out RuleViolation violation)
        {
            violation = null;
            var force = State.ActiveForce != null && State.ActiveForce.Side == side
                ? State.ActiveForce : State.ForceFor(side);
            if (State.ActiveSide != side || State.Phase != ActivationPhase.PlayerMove)
            {
                violation = new RuleViolation(RuleViolationCode.WrongPhase,
                    "A formation exits during its movement while it still has a movement point.", "phase");
                Trace("REJECTED", violation.Message);
                return false;
            }
            if (!CanExitMap(force))
            {
                violation = new RuleViolation(RuleViolationCode.ExitUnavailable,
                    State.Scenario.ScoringMode == ScenarioScoringMode.SubmarineEscape
                        ? "A PLAN submarine must be on the eastern edge with movement remaining."
                        : "Fujian must be on the western navigable edge, have movement remaining, and remain capable of launching aircraft.",
                    "formationId");
                Trace("REJECTED", violation.Message);
                return false;
            }
            force.MarkExited();
            State.MovementCup.RemoveUndrawnFormation(force.Id);
            State.PlayerHasMoved = true;
            if (State.Scenario.ScoringMode == ScenarioScoringMode.SubmarineEscape)
            {
                Trace("VICTORY", $"{force.Id} exited the east edge; escaped submarine total={CurrentScore().UsObjectiveDamage}.");
                AddLog($"{force.Id} escaped from the east edge.");
                CheckGameOver();
            }
            else
            {
                Trace("VICTORY", $"{force.Id} exited the west edge with its embarked air group launch-capable.");
                AddLog($"{force.Id} exited the west edge with aircraft launch capability intact.");
                EndByScore(ScenarioEndReason.BoardEdgeExited);
            }
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
                Trace("DETECTION", $"{State.ActiveForce.Id} used its search opportunity in " +
                    $"{State.ActiveForce.Position}; {State.Scenario.Name} omits detection resolution.");
                return true;
            }

            var observer = State.ActiveForce;
            if (observer.IsDummyOnly)
            {
                violation = new RuleViolation(RuleViolationCode.DummyActionUnavailable,
                    "Dummy task forces cannot detect or search.", "formationId");
                Trace("REJECTED", violation.Message);
                return false;
            }
            var mode = string.IsNullOrWhiteSpace(command.SearchMode) ? command.TargetId : command.SearchMode;
            var targetId = string.IsNullOrWhiteSpace(command.SearchMode) ? string.Empty : command.TargetId;
            if (observer.IsAircraftOnly)
                return SearchFromPatrolAircraft(observer, mode, targetId, out violation);
            var target = FindDetectionTarget(side, targetId, mode);
            if (target == null)
            {
                violation = new RuleViolation(RuleViolationCode.NoDetectionOpportunity,
                    "No enemy formation is in range of that sensor.", "targetId");
                Trace("REJECTED", $"{side} {mode} search: {violation.Message}");
                return false;
            }
            if (string.Equals(mode, "sonar", StringComparison.OrdinalIgnoreCase))
            {
                var prior = State.Detection.ContactFor(side, target.Id).IsDetected;
                var detected = _detection.ResolveSonar(observer, target, prior);
                if (detected && target.IsDummyOnly)
                {
                    var receiver = State.Forces.FirstOrDefault(force => force.Side == target.Side &&
                        force != target && force.Units.Count > 0);
                    receiver?.AddDummyCards(target.DummyCards);
                    State.MovementCup.RemoveUndrawnFormation(target.Id);
                    State.RemoveForce(target);
                    Trace("DUMMY", $"{side} sonar proved {target.Id} was a dummy task force; its counter was removed " +
                        "and its dummy cards returned to another friendly formation.");
                    AddLog($"Sonar cleared false contact {target.Id}.");
                }
                else RecordDetection(side, target, DetectionMethod.Sonar, detected);
                return true;
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
                    "Search mode must be visual, ESM, or sonar; surface radar resolves automatically.", "searchMode");
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

        private bool SearchFromPatrolAircraft(TaskForceState observer, string mode, string targetId,
            out RuleViolation violation)
        {
            violation = null;
            mode = (mode ?? string.Empty).ToLowerInvariant();
            if (State.Phase != ActivationPhase.AircraftAction || observer.IsOffMap)
            {
                violation = new RuleViolation(RuleViolationCode.WrongPhase,
                    "Patrol aircraft search only from its final on-map station during its activation.", "phase");
                return false;
            }
            if (mode != "sonar" && mode != "esm" && mode != "visual" && mode != "ssr" && mode != "asr")
            {
                violation = new RuleViolation(RuleViolationCode.InvalidPayload,
                    "Patrol search mode must be ASR, SSR, sonar, ESM, or visual.", "searchMode");
                return false;
            }
            if (observer.HasUsedAircraftSearch(mode))
            {
                violation = new RuleViolation(RuleViolationCode.AlreadyActed,
                    $"This patrol model already used {mode.ToUpperInvariant()} from its current station.", "searchMode");
                return false;
            }
            var target = FindDetectionTarget(observer.Side, targetId, mode);
            if (target == null)
            {
                violation = new RuleViolation(RuleViolationCode.NoDetectionOpportunity,
                    $"No opposing contact is in {mode.ToUpperInvariant()} search range.", "targetId");
                return false;
            }
            var detected = false;
            var method = DetectionMethod.ScenarioKnown;
            if (mode == "sonar")
            {
                method = DetectionMethod.Sonar;
                detected = _detection.ResolveSonar(observer, target,
                    State.Detection.ContactFor(observer.Side, target.Id).IsDetected);
            }
            else if (mode == "esm")
            {
                method = DetectionMethod.Esm;
                var roll = _dice.RollD6();
                detected = roll <= 5;
                Trace("DIE", $"PATROL ESM {observer.Id}->{target.Id}: D6={roll}; needs 1-5; " +
                    (detected ? "DETECTED." : "NO CONTACT."));
            }
            else if (mode == "visual")
            {
                if (State.TimeOfDay == TimeOfDay.Night)
                {
                    violation = new RuleViolation(RuleViolationCode.NightRestricted,
                        "Patrol-aircraft visual search is prohibited at Night.", "searchMode");
                    return false;
                }
                method = DetectionMethod.Visual;
                var roll = _dice.RollD6();
                detected = roll <= 5;
                Trace("DIE", $"PATROL VISUAL {observer.Id}->{target.Id}: D6={roll}; needs 1-5; " +
                    (detected ? "DETECTED." : "NO CONTACT."));
            }
            else
            {
                method = mode == "asr" ? DetectionMethod.AirSearchRadar : DetectionMethod.SurfaceSearchRadar;
                detected = true;
            }
            observer.MarkAircraftSearchUsed(mode);
            RecordDetection(observer.Side, target, method, detected);
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
            var actionPhase = State.Phase == ActivationPhase.PlayerMove || State.Phase == ActivationPhase.PlayerAction ||
                              State.Phase == ActivationPhase.AircraftAction;
            if (State.ActiveSide != side || !actionPhase || State.PlayerHasAttacked)
            {
                Trace("REJECTED", $"{side} attack: active={State.ActiveSide}, phase={State.Phase}, already attacked={State.PlayerHasAttacked}.");
                violation = new RuleViolation(State.ActiveSide != side ? RuleViolationCode.WrongSide :
                    State.PlayerHasAttacked ? RuleViolationCode.AlreadyActed : RuleViolationCode.WrongPhase,
                    "Attack is not available.");
                return null;
            }
            var attacker = State.ActiveForce;
            var attackingAircraft = attacker.IsAircraftOnly ? attacker.ActiveUnits.FirstOrDefault() : null;
            if (attackingAircraft != null && attackingAircraft.AircraftLastAttackTurn == State.Turn)
            {
                violation = new RuleViolation(RuleViolationCode.AlreadyActed,
                    "A patrol-aircraft type may attack only once per turn.");
                return null;
            }
            if (attacker.IsOffMap)
            {
                violation = new RuleViolation(RuleViolationCode.NoLegalWeapon,
                    "An arrived formation has left the operational map.");
                return null;
            }
            var defender = FindOpponent(side, State.CurrentCommand?.TargetId);
            if (State.DetectionRulesEnabled && !State.Detection.IsClassified(side, defender.Id))
            {
                violation = new RuleViolation(RuleViolationCode.TargetUndetected,
                    "A task force may not be attacked until it is detected.", "targetId");
                Trace("REJECTED", $"{attacker.Id} attack on undetected {defender.Id}: {violation.Message}");
                return null;
            }
            var range = attacker.Position.DistanceTo(defender.Position);
            if (defender.IsAircraftOnly)
            {
                violation = new RuleViolation(RuleViolationCode.NoLegalWeapon,
                    "Patrol aircraft are engaged by ASR-directed SAM reaction, not surface weapons.");
                return null;
            }
            if (defender.IsSubmarineOnly && !attacker.IsSubmarineOnly)
            {
                if (range != 0 || !attacker.ActiveUnits.Any(unit => unit.EffectiveAntiSubmarineWarfare > 0))
                {
                    violation = new RuleViolation(RuleViolationCode.NoLegalWeapon,
                        "Detected submarines may be attacked only in the same hex with ASW weapons.");
                    return null;
                }
                if (attackingAircraft != null) attackingAircraft.MarkAircraftAttack(State.Turn);
                return ResolveUnderseaAttack(attacker, defender, false);
            }
            if (attacker.IsSubmarineOnly && range == 0 && !State.CurrentCommand.Enabled)
            {
                MissileAllocationData[] allocations = null;
                if (!defender.IsSubmarineOnly && !TryTorpedoAllocations(attacker, defender,
                        State.CurrentCommand?.MissileAllocations, out allocations, out violation))
                    return null;
                return ResolveUnderseaAttack(attacker, defender, !defender.IsSubmarineOnly,
                    defender.IsSubmarineOnly ? null : allocations);
            }
            if (defender.IsSubmarineOnly)
            {
                violation = new RuleViolation(RuleViolationCode.NoLegalWeapon,
                    "Surface-to-surface missiles and guns may not attack submarines.");
                return null;
            }
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
            if (attackingAircraft != null) attackingAircraft.MarkAircraftAttack(State.Turn);
            State.Phase = ActivationPhase.MissileCombat;
            Trace("COMBAT", $"{attacker.Id} opened a missile attack on {defender.Id} at range {range}; " +
                "awaiting explicit fire allocation.");
            AddLog($"{attacker.Id} is allocating SSM fire against {defender.Id}.");
            return null;
        }

        private bool TryTorpedoAllocations(TaskForceState attacker, TaskForceState defender,
            IReadOnlyList<MissileAllocationData> requested, out MissileAllocationData[] allocations,
            out RuleViolation violation)
        {
            violation = null;
            allocations = requested?.Where(item => item != null).ToArray() ?? Array.Empty<MissileAllocationData>();
            if (allocations.Length == 0)
            {
                var target = defender.ActiveUnits.FirstOrDefault();
                allocations = attacker.ActiveUnits.Where(unit => unit.EffectiveTorpedoes > 0).Select((unit, index) =>
                    new MissileAllocationData { id = $"TORP-{index + 1}", sourceUnitId = unit.Definition.Id,
                        targetUnitId = target?.Definition.Id, shortFactors = unit.EffectiveTorpedoes }).ToArray();
            }
            var activeSources = new HashSet<string>(attacker.ActiveUnits.Where(unit => unit.EffectiveTorpedoes > 0)
                .Select(unit => unit.Definition.Id));
            var activeTargets = new HashSet<string>(defender.ActiveUnits.Select(unit => unit.Definition.Id));
            if (allocations.Length == 0 || allocations.Any(item => !activeSources.Contains(item.sourceUnitId) ||
                    !activeTargets.Contains(item.targetUnitId) || item.shortFactors < 0 || item.longFactors < 0 ||
                    item.shortFactors + item.longFactors <= 0))
            {
                violation = new RuleViolation(RuleViolationCode.InvalidAllocation,
                    "Torpedo factors require operational submarine sources and surface-ship targets.",
                    "missileAllocations");
                return false;
            }
            foreach (var group in allocations.GroupBy(item => item.sourceUnitId))
            {
                var source = attacker.Units.First(unit => unit.Definition.Id == group.Key);
                if (group.Sum(item => item.shortFactors + item.longFactors) != source.EffectiveTorpedoes)
                {
                    violation = new RuleViolation(RuleViolationCode.InvalidAllocation,
                        $"{source.Definition.DisplayName} must allocate its complete torpedo strength of " +
                        $"{source.EffectiveTorpedoes} among one or more ships.", "missileAllocations");
                    return false;
                }
            }
            return true;
        }

        private AttackReport ResolveUnderseaAttack(TaskForceState attacker, TaskForceState defender,
            bool torpedoAttack, IReadOnlyList<MissileAllocationData> torpedoAllocations = null)
        {
            var report = new AttackReport { Fired = true };
            State.PlayerHasAttacked = true;
            var source = attacker.ActiveUnits.OrderByDescending(unit => torpedoAttack
                ? unit.EffectiveTorpedoes : unit.EffectiveAntiSubmarineWarfare).FirstOrDefault();
            var target = defender.ActiveUnits.FirstOrDefault();
            if (source == null || target == null) return report;
            report.SourceUnitId = source.Definition.Id;
            report.TargetUnitId = target.Definition.Id;

            if (torpedoAttack)
            {
                if (defender.DefensePairs.Count == 0)
                    defender.SetDefensePairs(DefaultDefensePairs(defender));
                var screenIds = new HashSet<string>(defender.DefensePairs.Select(pair => pair.firstUnitId));
                foreach (var sourceGroup in torpedoAllocations.GroupBy(item => item.sourceUnitId))
                {
                    source = attacker.Units.First(unit => unit.Definition.Id == sourceGroup.Key);
                    var screenAttacks = sourceGroup.Where(item => screenIds.Contains(item.targetUnitId)).ToArray();
                    var screenedAttacks = sourceGroup.Where(item => !screenIds.Contains(item.targetUnitId)).ToArray();
                    ResolveTorpedoAllocations(source, defender, screenAttacks, report);
                    if (screenedAttacks.Length > 0)
                        ResolveAswCounterattack(defender, source, report);
                    else
                    {
                        var directCounter = screenAttacks.Select(item => defender.Units.First(unit =>
                                unit.Definition.Id == item.targetUnitId)).Where(unit => !unit.IsSunk)
                            .OrderByDescending(unit => unit.EffectiveAntiSubmarineWarfare).FirstOrDefault();
                        if (directCounter != null) ResolveAswCounterattack(defender, source, report, directCounter);
                    }
                    if (!source.IsSunk) ResolveTorpedoAllocations(source, defender, screenedAttacks, report);
                }
            }
            else
            {
                report.AttackFactors = source.EffectiveAntiSubmarineWarfare;
                for (var die = 0; die < report.AttackFactors; die++)
                {
                    var roll = _dice.RollD6();
                    var hits = CombatTables.Hits(CombatTableColumn.Asw, roll);
                    report.HullHits += target.ApplyDamage(hits, DamageSource.Other).AppliedHits;
                    Trace("DIE", $"ASW {source.Definition.DisplayName}->{target.Definition.DisplayName}: D6={roll}, hits={hits}.");
                }
            }
            report.SankAnyShip |= target.IsSunk;
            report.Summary = torpedoAttack
                ? $"Torpedo attack inflicted {report.HullHits} hull hit(s)."
                : $"ASW attack inflicted {report.HullHits} hull hit(s).";
            Trace("COMBAT", report.Summary);
            AddLog(report.Summary);
            PruneDestroyedMovementChits();
            CheckGameOver();
            AttackResolved?.Invoke(attacker.Side, report);
            return report;
        }

        private void ResolveTorpedoAllocations(UnitState source, TaskForceState defender,
            IEnumerable<MissileAllocationData> allocations, AttackReport report)
        {
            foreach (var allocation in allocations)
            {
                var target = defender.Units.First(unit => unit.Definition.Id == allocation.targetUnitId);
                var factors = allocation.shortFactors + allocation.longFactors;
                report.AttackFactors += factors;
                report.TargetUnitId = target.Definition.Id;
                for (var die = 0; die < factors; die++)
                {
                    var roll = _dice.RollD6();
                    var hits = CombatTables.Hits(CombatTableColumn.Torpedoes, roll);
                    report.HullHits += target.ApplyDamage(hits, DamageSource.Torpedo).AppliedHits;
                    Trace("DIE", $"TORPEDO {source.Definition.DisplayName}->{target.Definition.DisplayName}: D6={roll}, hits={hits}.");
                }
                report.SankAnyShip |= target.IsSunk;
            }
        }

        private void ResolveAswCounterattack(TaskForceState surfaceForce, UnitState submarine,
            AttackReport report, UnitState requiredCounter = null)
        {
            var submarineForce = State.Forces.FirstOrDefault(force => force.Units.Contains(submarine));
            if (State.DetectionRulesEnabled && (submarineForce == null ||
                !State.Detection.IsClassified(surfaceForce.Side, submarineForce.Id))) return;
            var counter = requiredCounter ?? surfaceForce.ActiveUnits
                .OrderByDescending(unit => unit.EffectiveAntiSubmarineWarfare)
                .FirstOrDefault(unit => unit.EffectiveAntiSubmarineWarfare > 0);
            if (counter == null) return;
            var hitsTotal = 0;
            for (var die = 0; die < counter.EffectiveAntiSubmarineWarfare; die++)
            {
                var roll = _dice.RollD6();
                var hits = CombatTables.Hits(CombatTableColumn.Asw, roll);
                hitsTotal += submarine.ApplyDamage(hits, DamageSource.Other).AppliedHits;
                Trace("DIE", $"ASW COUNTERATTACK {counter.Definition.DisplayName}->{submarine.Definition.DisplayName}: " +
                    $"D6={roll}, hits={hits}.");
            }
            report.InterceptedFactors += hitsTotal;
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
            if (attacker.IsAircraftOnly || target.IsAircraftOnly) return false;
            var range = attacker.Position.DistanceTo(target.Position);
            if (target.IsSubmarineOnly) return range == 0 &&
                attacker.ActiveUnits.Any(unit => unit.EffectiveAntiSubmarineWarfare > 0);
            return CanFireMissiles(attacker, range) && (!State.DetectionRulesEnabled ||
                State.Detection.IsClassified(attacker.Side, target.Id));
        }

        private static bool CanFireMissiles(TaskForceState force, int range) => force.ActiveUnits.Any(unit =>
            (range <= 1 && unit.AvailableShortSsm > 0) || (range <= 3 && unit.AvailableLongSsm > 0));

        private bool CanOpenAttack(TaskForceState attacker, TaskForceState defender)
        {
            if (attacker == null || defender == null || attacker.IsDestroyed || defender.IsDestroyed ||
                attacker.IsOffMap || defender.IsOffMap) return false;
            if (State.DetectionRulesEnabled && !State.Detection.IsClassified(attacker.Side, defender.Id)) return false;
            var patrolAircraft = attacker.IsAircraftOnly ? attacker.ActiveUnits.FirstOrDefault() : null;
            if (patrolAircraft != null && patrolAircraft.AircraftLastAttackTurn == State.Turn) return false;
            var range = attacker.Position.DistanceTo(defender.Position);
            if (range == 0 && attacker.IsSubmarineOnly)
                return attacker.ActiveUnits.Any(unit => defender.IsSubmarineOnly
                    ? unit.EffectiveAntiSubmarineWarfare > 0 : unit.EffectiveTorpedoes > 0);
            if (defender.IsSubmarineOnly)
                return range == 0 && attacker.ActiveUnits.Any(unit => unit.EffectiveAntiSubmarineWarfare > 0);
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
            if (movingForce != null && opponent != null && !movingForce.IsAircraftOnly && !opponent.IsAircraftOnly &&
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
            var force = State.ActiveForce;
            if (!force.IsAircraftOnly && force.DeclaredSpeed < 0)
            {
                violation = new RuleViolation(RuleViolationCode.SpeedNotDeclared,
                    "Declare speed before ending the activation.", "declaredSpeed");
                Trace("REJECTED", $"{side} end activation: {violation.Message}");
                return false;
            }
            if (!force.IsAircraftOnly && force.MovementRemaining > 0)
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

        private bool AssignDefensiveAirInternal(GameCommand command, TacticalAirMission mission,
            out RuleViolation violation)
        {
            violation = null;
            if (!State.Scenario.TacticalAirEnabled)
                return TacticalReject(RuleViolationCode.TacticalAirUnavailable,
                    "This scenario does not use tactical aircraft.", out violation);
            if (State.Phase != ActivationPhase.AwaitingChit || !State.MovementCup.FirstDrawPending)
                return TacticalReject(RuleViolationCode.WrongPhase,
                    "CAP and deck-launched interceptors must be declared before the first chit draw.", out violation);
            var flight = State.TacticalFlight(command.SourceUnitId);
            if (flight == null || flight.Side != command.Actor)
                return TacticalReject(RuleViolationCode.AircraftUnavailable,
                    "Select one of your ready tactical-aircraft flights.", out violation);
            var airBase = State.AirBase(flight.BaseId);
            if (airBase == null || !BaseCanLaunch(airBase))
                return TacticalReject(RuleViolationCode.BaseDisabled,
                    "That base or carrier cannot launch aircraft.", out violation);
            if (mission == TacticalAirMission.DeckInterceptor && !airBase.Definition.IsCarrier)
                return TacticalReject(RuleViolationCode.AircraftUnavailable,
                    "Deck-launched interceptors must be based aboard a carrier.", out violation);
            if (airBase.Definition.IsCarrier && State.TacticalFlights.Count(item => item.BaseId == flight.BaseId &&
                    item.Mission != TacticalAirMission.Destroyed) > airBase.Definition.FlightCapacity)
                return TacticalReject(RuleViolationCode.DeckCapacityExceeded,
                    "The carrier air wing exceeds its printed deck capacity.", out violation);
            if (!flight.AssignDefensiveMission(mission, command.Enabled))
                return TacticalReject(RuleViolationCode.AircraftUnavailable,
                    "Defensive missions require a full ready four-aircraft fighter flight.", out violation);
            Trace("AIR", $"{flight.Id} assigned {(mission == TacticalAirMission.Cap ? "CAP" : "DLI")}; " +
                $"radar {(command.Enabled ? "on" : "silent")}.");
            AddLog($"{flight.Id} assigned {(mission == TacticalAirMission.Cap ? "CAP" : "deck interception")}.");
            return true;
        }

        private bool LaunchTacticalStrikeInternal(GameCommand command, out RuleViolation violation,
            out AttackReport attackReport)
        {
            violation = null;
            attackReport = null;
            if (!State.Scenario.TacticalAirEnabled)
                return TacticalReject(RuleViolationCode.TacticalAirUnavailable,
                    "This scenario does not use tactical aircraft.", out violation);
            if (command.Actor != State.ActiveSide || State.Phase == ActivationPhase.AwaitingChit ||
                State.Phase == ActivationPhase.MissileCombat || State.Phase == ActivationPhase.GunCombat)
                return TacticalReject(RuleViolationCode.WrongPhase,
                    "Launch a tactical strike during one of your formation activations.", out violation);
            var flight = State.TacticalFlight(command.SourceUnitId);
            if (flight == null || flight.Side != command.Actor || flight.Mission != TacticalAirMission.Ready)
                return TacticalReject(RuleViolationCode.AircraftUnavailable,
                    "The selected flight is not ready for another mission this turn.", out violation);
            var airBase = State.AirBase(flight.BaseId);
            if (airBase == null || !BaseCanLaunch(airBase))
                return TacticalReject(RuleViolationCode.BaseDisabled,
                    "The selected base or carrier cannot launch aircraft.", out violation);
            var targetForce = State.Formation(command.TargetId);
            var targetBase = State.AirBase(command.TargetId);
            if ((targetForce == null || targetForce.Side == command.Actor || targetForce.IsDestroyed) &&
                (targetBase == null || targetBase.Definition.Side == command.Actor))
                return TacticalReject(RuleViolationCode.InvalidFormation,
                    "Select an opposing formation or air base.", out violation);
            if (targetForce != null && State.DetectionRulesEnabled &&
                !State.Detection.IsDetected(command.Actor, targetForce.Id))
                return TacticalReject(RuleViolationCode.TargetUndetected,
                    "A friendly force must first detect the formation selected for an air strike.", out violation);
            if (!Enum.TryParse(command.SearchMode, true, out TacticalWeapon weapon))
                return TacticalReject(RuleViolationCode.InvalidPayload,
                    "Select LongAsm, ShortAsm, or Bombs.", out violation);
            var weaponFactor = weapon == TacticalWeapon.LongAsm ? flight.Definition.LongAsm :
                weapon == TacticalWeapon.ShortAsm ? flight.Definition.ShortAsm : flight.Definition.Bombs;
            if (weaponFactor <= 0)
                return TacticalReject(RuleViolationCode.NoLegalWeapon,
                    $"{flight.Definition.DisplayName} has no {weapon} factor.", out violation);
            var source = AirBasePosition(airBase);
            var target = targetForce?.Position ?? targetBase.Definition.Position;
            var terminalRange = weapon == TacticalWeapon.LongAsm ? 3 : 1;
            if (source.DistanceTo(target) > flight.Definition.Radius + terminalRange)
                return TacticalReject(RuleViolationCode.RadiusExceeded,
                    $"Target is beyond aircraft radius {flight.Definition.Radius} plus {terminalRange}-hex weapon reach.", out violation);
            var requested = Math.Max(1, command.Factors);
            if (requested > airBase.MaximumStrikeSize)
                return TacticalReject(RuleViolationCode.BaseDisabled,
                    $"{airBase.Definition.DisplayName} can launch at most {airBase.MaximumStrikeSize} aircraft in this strike.",
                    out violation);
            var launched = flight.Launch(requested);
            if (launched == 0)
                return TacticalReject(RuleViolationCode.AircraftUnavailable,
                    "No aircraft in that flight are ready.", out violation);

            var report = new TacticalStrikeReport
            {
                Launched = true, FlightId = flight.Id, TargetId = command.TargetId,
                Weapon = weapon, AircraftLaunched = launched
            };
            State.LastTacticalStrike = report;
            Trace("AIR", $"{flight.Id} launched {launched} aircraft from {airBase.Definition.DisplayName} " +
                $"against {command.TargetId} with {weapon}; range {source.DistanceTo(target)}.");

            var escorts = (command.UnitIds ?? Array.Empty<string>()).Select(State.TacticalFlight)
                .Where(item => item != null && item.Side == command.Actor && item.BaseId == flight.BaseId &&
                    item.IsFighter && item.Id != flight.Id && item.Mission == TacticalAirMission.Ready).ToArray();
            foreach (var escort in escorts) escort.Launch(escort.ReadyAircraft, TacticalAirMission.Escort);
            var electronicAttack = State.Scenario.Ea18gSensorReductionEnabled && escorts.Any(item => item.Definition.ElectronicAttack);
            if (electronicAttack) Trace("AIR", "EA-18G escort reduced defending ASR and SSR by one (minimum zero).");

            var defender = command.Actor == Side.UsNavy ? Side.Plan : Side.UsNavy;
            var interceptors = EligibleInterceptors(defender, targetBase, targetForce, target, electronicAttack).ToList();
            var missionAircraft = launched;
            foreach (var interceptor in interceptors)
            {
                var escort = escorts.FirstOrDefault(item => item.AircraftRemaining > 0);
                var victim = escort ?? flight;
                var attackingAircraft = interceptor.Mission == TacticalAirMission.Cap ? 1 : interceptor.ReadyAircraft;
                var escortCounterAircraft = escort == null ? 0 : Math.Min(escort.AircraftRemaining, 4);
                var hits = ResolveAirToAir(interceptor, victim, attackingAircraft);
                var losses = ApplyAircraftHits(victim, hits, victim == flight ? missionAircraft : victim.AircraftRemaining);
                if (victim == flight)
                {
                    missionAircraft = Math.Max(0, missionAircraft - losses.shotDown - losses.aborted);
                    report.AircraftShotDown += losses.shotDown;
                    report.AircraftAborted += losses.aborted;
                }
                if (escort != null && escortCounterAircraft > 0)
                {
                    var counterHits = ResolveAirToAir(escort, interceptor, escortCounterAircraft);
                    ApplyAircraftHits(interceptor, counterHits, interceptor.AircraftRemaining);
                }
                interceptor.MarkInterceptorUsed();
            }

            var selectedTargetUnit = targetForce?.ActiveUnits.FirstOrDefault(unit =>
                unit.Definition.Id == command.FormationId) ?? targetForce?.ActiveUnits.FirstOrDefault();
            var longDefense = targetBase != null ? targetBase.Definition.LongSam :
                targetForce.ActiveUnits.Sum(unit => unit.EffectiveLongSam);
            var shortDefense = targetBase != null ? targetBase.Definition.ShortSam :
                TacticalShortRangeDefense(targetForce, selectedTargetUnit);
            var pointDefense = targetBase != null ? targetBase.Definition.PointDefense :
                selectedTargetUnit?.EffectivePointDefense ?? 0;

            if (weapon != TacticalWeapon.LongAsm && missionAircraft > 0 && longDefense > 0)
            {
                var losses = ApplyAircraftHits(flight,
                    _missileCombat.RollDefense("LR SAM vs strike aircraft", longDefense, CombatTableColumn.Sam), missionAircraft);
                missionAircraft = Math.Max(0, missionAircraft - losses.shotDown - losses.aborted);
                report.AircraftShotDown += losses.shotDown;
                report.AircraftAborted += losses.aborted;
            }
            if (weapon == TacticalWeapon.Bombs && missionAircraft > 0 && shortDefense > 0)
            {
                var losses = ApplyAircraftHits(flight,
                    _missileCombat.RollDefense("SR SAM vs bombing aircraft", shortDefense, CombatTableColumn.Sam), missionAircraft);
                missionAircraft = Math.Max(0, missionAircraft - losses.shotDown - losses.aborted);
                report.AircraftShotDown += losses.shotDown;
                report.AircraftAborted += losses.aborted;
            }

            var attackFactors = missionAircraft * weaponFactor;
            report.MissileFactors = weapon == TacticalWeapon.Bombs ? 0 : attackFactors;
            if (weapon != TacticalWeapon.Bombs && attackFactors > 0)
            {
                foreach (var interceptor in interceptors.Where(item => item.Mission == TacticalAirMission.Cap &&
                             item.AircraftRemaining > 0))
                {
                    var destroyed = TacticalAirTables.AirToAirHits(_dice.RollD6() + interceptor.Definition.AirToAir);
                    attackFactors = Math.Max(0, attackFactors - destroyed);
                    report.MissileFactorsIntercepted += destroyed;
                    Trace("AIR", $"{interceptor.Id} engaged missiles at Defense 0 and destroyed {destroyed} factor(s).");
                }
                var lrHits = _missileCombat.RollDefense("LR SAM vs air-launched missiles", longDefense, CombatTableColumn.Sam);
                attackFactors = Math.Max(0, attackFactors - lrHits);
                report.MissileFactorsIntercepted += lrHits;
                var srHits = _missileCombat.RollDefense("SR SAM vs air-launched missiles", shortDefense, CombatTableColumn.Sam);
                attackFactors = Math.Max(0, attackFactors - srHits);
                report.MissileFactorsIntercepted += srHits;
                var pdHits = _missileCombat.RollDefense("Point defense vs air-launched missiles", pointDefense,
                    CombatTableColumn.PointDefense);
                attackFactors = Math.Max(0, attackFactors - pdHits);
                report.MissileFactorsIntercepted += pdHits;
            }

            var impactHits = 0;
            for (var factor = 0; factor < attackFactors; factor++)
            {
                var roll = _dice.RollD6();
                var hits = TacticalAirTables.BombHits(roll);
                impactHits += hits;
                Trace("DIE", $"{weapon} impact {factor + 1}/{attackFactors}: D6={roll}; {hits} hit(s).");
            }
            if (targetBase != null)
                report.RunwayHits = targetBase.ApplyRunwayHits(impactHits);
            else
            {
                if (selectedTargetUnit != null)
                    report.HullHits = selectedTargetUnit.ApplyDamage(impactHits,
                        weapon == TacticalWeapon.Bombs ? DamageSource.Bomb : DamageSource.Missile).AppliedHits;
            }
            report.Summary = $"{flight.Id} {weapon} strike: {launched} launched, {report.AircraftShotDown} shot down, " +
                $"{report.AircraftAborted} aborted; {report.HullHits} hull / {report.RunwayHits} runway hit(s).";
            AddLog(report.Summary);
            Trace("COMBAT", report.Summary);
            attackReport = new AttackReport { Fired = true, AttackFactors = attackFactors,
                InterceptedFactors = report.MissileFactorsIntercepted,
                HullHits = report.HullHits + report.RunwayHits, Summary = report.Summary };
            AttackResolved?.Invoke(command.Actor, attackReport);
            CheckGameOver();
            return true;
        }

        private int TacticalShortRangeDefense(TaskForceState force, UnitState target)
        {
            if (force == null || target == null) return 0;
            var pair = force.DefensePairs.FirstOrDefault(item => item.firstUnitId == target.Definition.Id ||
                item.secondUnitId == target.Definition.Id);
            var pairId = pair == null ? string.Empty : pair.firstUnitId == target.Definition.Id
                ? pair.secondUnitId : pair.firstUnitId;
            var mate = force.ActiveUnits.FirstOrDefault(unit => unit.Definition.Id == pairId) ??
                force.ActiveUnits.Where(unit => unit != target)
                    .OrderByDescending(unit => unit.EffectiveShortSam).FirstOrDefault();
            return target.EffectiveShortSam + (mate?.EffectiveShortSam ?? 0);
        }

        private IEnumerable<TacticalFlightState> EligibleInterceptors(Side defender, AirBaseState targetBase,
            TaskForceState targetForce, HexCoord target, bool electronicAttack)
        {
            foreach (var flight in State.TacticalFlights.Where(item => item.Side == defender && item.IsFighter &&
                         (item.Mission == TacticalAirMission.Cap || item.Mission == TacticalAirMission.DeckInterceptor)))
            {
                var ownTarget = targetBase?.Definition.Id == flight.BaseId ||
                    (targetForce != null && CarrierForceForBase(State.AirBase(flight.BaseId)) == targetForce);
                var radarRange = Math.Max(0, flight.Definition.AirSearchRadar - (electronicAttack ? 1 : 0));
                if (ownTarget || (flight.Mission == TacticalAirMission.Cap && flight.RadarOn &&
                    AirBasePosition(State.AirBase(flight.BaseId)).DistanceTo(target) <= radarRange)) yield return flight;
            }
        }

        private int ResolveAirToAir(TacticalFlightState attacker, TacticalFlightState defender, int aircraft)
        {
            var hits = 0;
            for (var index = 0; index < aircraft; index++)
            {
                var roll = _dice.RollD6();
                var modified = roll + attacker.Definition.AirToAir - defender.Definition.Defense;
                var result = TacticalAirTables.AirToAirHits(modified);
                hits += result;
                Trace("DIE", $"AIR-TO-AIR {attacker.Id} vs {defender.Id}: D6={roll} + ATA " +
                    $"{attacker.Definition.AirToAir} - DEF {defender.Definition.Defense} = {modified}; {result} hit(s).");
            }
            return hits;
        }

        private (int shotDown, int aborted) ApplyAircraftHits(TacticalFlightState flight, int hits, int exposed)
        {
            var shotDown = 0;
            var aborted = 0;
            for (var hit = 0; hit < hits && shotDown + aborted < exposed; hit++)
            {
                var roll = _dice.RollD6();
                var damage = CombatTables.AircraftDamage(roll);
                Trace("DIE", $"AIRCRAFT DAMAGE {flight.Id}: D6={roll}; {damage}.");
                if (damage == AircraftDamageResult.ShotDown) shotDown++;
                else if (damage == AircraftDamageResult.Abort) aborted++;
            }
            flight.ApplyAirDamage(shotDown, aborted);
            return (shotDown, aborted);
        }

        private HexCoord AirBasePosition(AirBaseState airBase)
        {
            if (airBase == null) return default;
            if (!airBase.Definition.IsCarrier) return airBase.Definition.Position;
            var carrierForce = CarrierForceForBase(airBase);
            return carrierForce?.Position ?? State.ForceFor(airBase.Definition.Side).Position;
        }

        private TaskForceState CarrierForceForBase(AirBaseState airBase)
        {
            if (airBase == null || !airBase.Definition.IsCarrier) return null;
            var objectiveId = airBase.Definition.Side == Side.UsNavy
                ? State.Scenario.UsObjectiveUnitId : State.Scenario.PlanObjectiveUnitId;
            return State.Forces.FirstOrDefault(force => force.Side == airBase.Definition.Side &&
                force.ActiveUnits.Any(unit => unit.Definition.Id == objectiveId));
        }

        private bool BaseCanLaunch(AirBaseState airBase)
        {
            if (!airBase.CanLaunch) return false;
            if (!airBase.Definition.IsCarrier) return true;
            var carrier = State.Forces.Where(force => force.Side == airBase.Definition.Side)
                .SelectMany(force => force.ActiveUnits).FirstOrDefault(unit =>
                    unit.Definition.Id == State.Scenario.UsObjectiveUnitId ||
                    unit.Definition.Id == State.Scenario.PlanObjectiveUnitId);
            return carrier != null && carrier.CanLaunchAircraft;
        }

        private bool TacticalReject(RuleViolationCode code, string message, out RuleViolation violation)
        {
            violation = new RuleViolation(code, message);
            Trace("REJECTED", message);
            return false;
        }

        private void BeginTurn()
        {
            State.PlayerHasMoved = false;
            State.PlayerHasAttacked = false;
            State.PlayerHasSearched = false;
            State.UsActivated = false;
            State.PlanActivated = false;
            foreach (var force in State.Forces) force.ResetActivation();
            foreach (var unit in State.Forces.SelectMany(force => force.Units)) unit.BeginAircraftTurn(State.Turn);
            foreach (var flight in State.TacticalFlights) flight.BeginTurn();
            if (!_manualOpponent && State.Scenario.TacticalAirEnabled)
            {
                var planCap = State.TacticalFlights.FirstOrDefault(flight => flight.Side == Side.Plan &&
                    flight.IsFighter && flight.AircraftRemaining == 4);
                if (planCap?.AssignDefensiveMission(TacticalAirMission.Cap, true) == true)
                    Trace("AIR", $"PLAN assigned {planCap.Id} to persistent radar CAP for {State.TimeLabel}.");
            }
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
                        var trackedPlayer = State.Forces.FirstOrDefault(force => force.Side == Side.UsNavy &&
                            !force.IsOffMap && State.Detection.IsDetected(Side.Plan, force.Id));
                        var navigationTarget = State.DetectionRulesEnabled && trackedPlayer == null &&
                            State.Scenario.HasUsDestination ? State.Scenario.UsDestination :
                            (trackedPlayer ?? State.Player).Position;
                        if (State.DetectionRulesEnabled && !enemyForce.RadarDeclaredThisActivation)
                        {
                            Execute(new GameCommand(GameCommandType.RadiateRadar, Side.Plan,
                                State.Revision, enabled: enemyForce.CanRadiateRadar));
                        }
                        var path = State.Map.FindPath(enemyForce.Position, navigationTarget, Side.Plan);
                        var declared = State.Scenario.ScoringMode == ScenarioScoringMode.CarrierEscape ||
                                       State.Scenario.ScoringMode == ScenarioScoringMode.SubmarineEscape
                            ? enemyForce.EffectiveSpeed
                            : path.Count == 0 ? 0 : Math.Min(enemyForce.EffectiveSpeed,
                                Math.Max(0, path.Count - 2));
                        Execute(new GameCommand(GameCommandType.DeclareSpeed, Side.Plan,
                            State.Revision, declaredSpeed: declared));
                    }
                    while (!State.IsGameOver && State.Phase == ActivationPhase.PlayerMove)
                    {
                        if (CanExitMap(enemyForce))
                        {
                            Execute(new GameCommand(GameCommandType.ExitMap, Side.Plan, State.Revision));
                            break;
                        }
                        var destination = BestEnemyDestination();
                        var movement = Execute(new GameCommand(GameCommandType.Move, Side.Plan,
                            State.Revision, destination));
                        if (!movement.Accepted) break;
                        TryEnemyDetection(enemyForce);
                        if (!State.PlayerHasAttacked && CanOpenAttack(enemyForce, State.Player))
                            Execute(new GameCommand(GameCommandType.Attack, Side.Plan, State.Revision));
                    }
                    TryEnemyDetection(enemyForce);
                    if (!State.IsGameOver && State.Phase == ActivationPhase.PlayerAction)
                        TryEnemyTacticalStrike();
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

        private void TryEnemyTacticalStrike()
        {
            if (!State.Scenario.TacticalAirEnabled || State.ActiveSide != Side.Plan) return;
            var target = State.Forces.Where(force => force.Side == Side.UsNavy && !force.IsDestroyed &&
                    !force.IsOffMap && State.Detection.IsDetected(Side.Plan, force.Id))
                .OrderByDescending(force => force.ActiveUnits.Any(unit =>
                    unit.Definition.Id == State.Scenario.UsObjectiveUnitId)).FirstOrDefault();
            if (target == null) return;
            var flight = State.TacticalFlights.Where(item => item.Side == Side.Plan &&
                    item.Mission == TacticalAirMission.Ready && item.Definition.LongAsm > 0)
                .OrderByDescending(item => item.Definition.LongAsm).FirstOrDefault();
            if (flight == null) return;
            var escorts = State.TacticalFlights.Where(item => item.Side == Side.Plan && item.IsFighter &&
                item.BaseId == flight.BaseId && item.Id != flight.Id && item.Mission == TacticalAirMission.Ready)
                .Take(1).Select(item => item.Id).ToArray();
            var targetUnit = target.ActiveUnits.FirstOrDefault(unit =>
                unit.Definition.Id == State.Scenario.UsObjectiveUnitId) ?? target.ActiveUnits.FirstOrDefault();
            Execute(new GameCommand(GameCommandType.LaunchTacticalStrike, Side.Plan, State.Revision,
                factors: Math.Min(4, flight.ReadyAircraft), targetId: target.Id,
                sourceUnitId: flight.Id, formationId: targetUnit?.Definition.Id,
                unitIds: escorts, searchMode: TacticalWeapon.LongAsm.ToString()));
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
            if (State.Scenario.ScoringMode == ScenarioScoringMode.CarrierEscape)
                return State.Map.NavigableNeighbors(enemyForce.Position, Side.Plan).Where(_isNavigable)
                    .Where(hex => DistanceToPatrolLine(State.Scenario, hex) <= State.Scenario.PatrolLineRadius)
                    .OrderBy(hex => hex.Column).ThenBy(hex => Math.Abs(hex.Row - 12))
                    .DefaultIfEmpty(enemyForce.Position).First();
            if (State.Scenario.ScoringMode == ScenarioScoringMode.SubmarineEscape)
                return State.Map.NavigableNeighbors(enemyForce.Position, Side.Plan).Where(_isNavigable)
                    .OrderByDescending(hex => hex.Column).ThenBy(hex => Math.Abs(hex.Row - 10))
                    .DefaultIfEmpty(enemyForce.Position).First();
            var trackedPlayer = State.Forces.FirstOrDefault(force => force.Side == Side.UsNavy &&
                !force.IsOffMap && State.Detection.IsDetected(Side.Plan, force.Id));
            var targetPosition = State.DetectionRulesEnabled && trackedPlayer == null &&
                State.Scenario.HasUsDestination ? State.Scenario.UsDestination :
                (trackedPlayer ?? State.Player).Position;
            return State.Map.NavigableNeighbors(enemyForce.Position, Side.Plan).Where(_isNavigable)
                .OrderBy(hex => hex.DistanceTo(targetPosition))
                .DefaultIfEmpty(enemyForce.Position).First();
        }

        private void TryEnemyDetection(TaskForceState observer)
        {
            if (!State.DetectionRulesEnabled || observer == null) return;
            if (observer.ActiveUnits.Any(unit => unit.EffectiveSonar > 0))
            {
                var sonarTarget = FindDetectionTarget(observer.Side, string.Empty, "sonar");
                if (sonarTarget != null && !State.Detection.IsClassified(observer.Side, sonarTarget.Id))
                {
                    Execute(new GameCommand(GameCommandType.Search, observer.Side, State.Revision,
                        targetId: sonarTarget.Id, searchMode: "sonar"));
                    if (State.Detection.IsClassified(observer.Side, sonarTarget.Id)) return;
                }
            }
            if (State.Scenario.ScoringMode == ScenarioScoringMode.SubmarineEscape)
            {
                var score = CurrentScore();
                if (score.UsObjectiveDamage >= 3)
                {
                    EndByScore(ScenarioEndReason.BoardEdgeExited);
                    return;
                }
                var possibleEscapes = score.UsObjectiveDamage + State.Forces.Count(force =>
                    force.Side == Side.Plan && force.IsSubmarineOnly && !force.IsDestroyed && !force.HasArrived);
                if (possibleEscapes < 3)
                {
                    EndByScore(ScenarioEndReason.FixedResult);
                    return;
                }
            }
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
            var opponents = State.Forces.Where(force => force.Side != observer && !force.IsDestroyed &&
                !force.IsOffMap).ToArray();
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
            var observingForce = State.ForceFor(observer);
            var origin = observingForce.Position;
            var candidates = State.Forces.Where(force => force.Side != observer && !force.IsDestroyed &&
                !force.IsOffMap);
            if (!string.IsNullOrWhiteSpace(targetId))
                candidates = candidates.Where(force => force.Id == targetId);
            if (string.Equals(mode, "visual", StringComparison.OrdinalIgnoreCase))
                candidates = candidates.Where(force => force.Position == origin);
            else if (string.Equals(mode, "esm", StringComparison.OrdinalIgnoreCase))
                candidates = candidates.Where(force => force.RadarRadiating &&
                    force.Position.DistanceTo(origin) <= (observingForce.IsAircraftOnly ? 3 : 1));
            else if (string.Equals(mode, "sonar", StringComparison.OrdinalIgnoreCase))
                candidates = candidates.Where(force => force.Position.DistanceTo(origin) <= 2);
            else if (string.Equals(mode, "ssr", StringComparison.OrdinalIgnoreCase))
                candidates = candidates.Where(force => !force.IsAircraftOnly && force.Position == origin);
            else if (string.Equals(mode, "asr", StringComparison.OrdinalIgnoreCase))
            {
                var range = observingForce.ActiveUnits.Max(unit => unit.EffectiveAirSearchRadar);
                candidates = candidates.Where(force => force.IsAircraftOnly &&
                    force.Position.DistanceTo(origin) <= range);
            }
            return candidates.OrderBy(force => force.Position.DistanceTo(origin)).FirstOrDefault();
        }

        private void ResolveMovementDetection(TaskForceState moving)
        {
            ResolveAutomaticRadar(moving);
            var opponents = State.Forces.Where(force => force.Side != moving.Side && !force.IsDestroyed &&
                !force.IsOffMap).ToArray();
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

        private void ResolveAircraftDetectionAndSam(TaskForceState aircraftForce)
        {
            if (!State.DetectionRulesEnabled || aircraftForce == null || !aircraftForce.IsAircraftOnly ||
                aircraftForce.IsOffMap) return;
            var defenders = State.Forces.Where(force => force.Side != aircraftForce.Side && force.IsSurfaceOnly &&
                !force.IsDestroyed && !force.IsOffMap).ToArray();
            foreach (var defender in defenders)
            {
                var asr = defender.ActiveUnits.Max(unit => unit.EffectiveAirSearchRadar);
                var range = defender.Position.DistanceTo(aircraftForce.Position);
                if (defender.RadarRadiating && asr > 0 && range <= asr)
                    RecordDetection(defender.Side, aircraftForce, DetectionMethod.AirSearchRadar, true);
            }
            foreach (var defender in defenders)
            {
                if (aircraftForce.IsOffMap || !State.Detection.IsDetected(defender.Side, aircraftForce.Id)) break;
                var range = defender.Position.DistanceTo(aircraftForce.Position);
                var samFactors = range == 0
                    ? defender.ActiveUnits.Max(unit => Math.Max(unit.EffectiveLongSam, unit.EffectiveShortSam))
                    : defender.RadarRadiating && range <= defender.ActiveUnits.Max(unit => unit.EffectiveAirSearchRadar)
                        ? defender.ActiveUnits.Max(unit => unit.EffectiveLongSam) : 0;
                if (samFactors <= 0) continue;
                ResolveSamAgainstPatrolAircraft(defender, aircraftForce, samFactors);
            }
        }

        private AttackReport ResolveSamAgainstPatrolAircraft(TaskForceState defender,
            TaskForceState aircraftForce, int samFactors)
        {
            var aircraft = aircraftForce.ActiveUnits.FirstOrDefault();
            var report = new AttackReport { Fired = samFactors > 0, AttackFactors = samFactors };
            if (aircraft == null || samFactors <= 0) return report;
            var hits = _missileCombat.RollDefense($"SAM vs {aircraft.Definition.DisplayName}",
                samFactors, CombatTableColumn.Sam);
            var aborted = false;
            var shotDown = 0;
            for (var hit = 0; hit < hits && aircraft.ServiceableAircraftRemaining > 0; hit++)
            {
                var roll = _dice.RollD6();
                var damage = CombatTables.AircraftDamage(roll);
                Trace("DIE", $"AIRCRAFT DAMAGE hit {hit + 1}/{hits}: D6={roll}; {damage}.");
                if (damage == AircraftDamageResult.ShotDown)
                {
                    aircraft.ShootDownAircraft(State.Turn);
                    shotDown++;
                }
                else if (damage == AircraftDamageResult.Abort)
                {
                    aircraft.AbortAircraft(State.Turn);
                    aborted = true;
                }
            }
            if (aborted || shotDown > 0)
                aircraftForce.RemoveAircraftFromMap();
            report.HullHits = shotDown;
            report.SankAnyShip = aircraft.IsSunk;
            report.Summary = $"{defender.Id} fired {samFactors} SAM factor(s): {hits} hit(s), " +
                $"{shotDown} aircraft shot down" + (aborted ? ", patrol aborted." : ".");
            Trace("COMBAT", report.Summary);
            AddLog(report.Summary);
            AttackResolved?.Invoke(defender.Side, report);
            return report;
        }

        private void ResolveAutomaticRadar(TaskForceState observer)
        {
            if (!State.DetectionRulesEnabled || observer == null || !observer.RadarRadiating) return;
            foreach (var target in State.Forces.Where(force => force.Side != observer.Side &&
                         !force.IsDestroyed && !force.IsOffMap && force.Position == observer.Position &&
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
            if ((target.IsDummyOnly || target.IsSubmarineOnly) && method != DetectionMethod.Sonar &&
                method != DetectionMethod.Esm)
            {
                var dummyContact = State.Detection.Detect(observer, target, method, State.Turn, false);
                Trace("DETECTION", $"{observer} searched {target.Id} at {target.Position} by {method}: " +
                    "NO SURFACE SHIPS PRESENT; submarine possibility remains unresolved.");
                AddLog($"{observer} search at {dummyContact.LastKnownPosition}: no surface ships present.");
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
            if (State.Scenario.ScoringMode == ScenarioScoringMode.CarrierEscape)
            {
                var carrier = State.Unit(State.Scenario.PlanObjectiveUnitId);
                if (carrier == null || carrier.IsSunk)
                {
                    EndByScore(ScenarioEndReason.ObjectiveSunk);
                    return;
                }
            }
            if (State.Scenario.ScoringMode == ScenarioScoringMode.CarrierPosition)
            {
                var carrier = State.Unit(State.Scenario.UsObjectiveUnitId);
                var carrierForce = State.Forces.FirstOrDefault(force => force.Units.Contains(carrier));
                if (carrier == null || carrier.IsSunk)
                {
                    EndByScore(ScenarioEndReason.ObjectiveSunk);
                    return;
                }
                if (carrierForce != null && !carrierForce.IsOffMap && carrier.CanLaunchAircraft &&
                    carrierForce.Position.DistanceTo(State.Scenario.CarrierObjectiveHex) <=
                    State.Scenario.CarrierObjectiveRadius)
                {
                    EndByScore(ScenarioEndReason.DestinationReached);
                    return;
                }
            }
            if (State.Scenario.ScoringMode == ScenarioScoringMode.SubmarineEscape)
            {
                var breakout = CurrentScore();
                if (breakout.UsObjectiveDamage >= 3)
                {
                    EndByScore(ScenarioEndReason.BoardEdgeExited);
                    return;
                }
                if (breakout.UsObjectiveDamage + breakout.UsTieBreakDamage < 3)
                {
                    EndByScore(ScenarioEndReason.ForceDestroyed);
                    return;
                }
            }
            if (State.Scenario.ScoringMode == ScenarioScoringMode.ConvoyArrival)
            {
                var merchants = State.Forces.Where(force => force.Side == Side.UsNavy)
                    .SelectMany(force => force.Units).Where(unit => unit.Definition.Role == UnitRole.Objective).ToArray();
                if (State.Forces.Where(force => force.Side == Side.Plan && force.Units.Count > 0)
                    .All(force => force.IsDestroyed))
                {
                    EndByScore(ScenarioEndReason.ForceDestroyed);
                    return;
                }
                if (merchants.All(unit => unit.IsSunk))
                {
                    EndByScore(ScenarioEndReason.ObjectiveSunk);
                    return;
                }
                if (State.Forces.Any(force => force.Side == Side.UsNavy && force.HasArrived &&
                    force.ActiveUnits.Any(unit => unit.Definition.Role == UnitRole.Objective)))
                {
                    EndByScore(ScenarioEndReason.DestinationReached);
                    return;
                }
            }
            if (State.Scenario.ScoringMode == ScenarioScoringMode.ConvoySurvival &&
                CurrentScore().Result == "US NAVY VICTORY")
            {
                EndByScore(ScenarioEndReason.DestinationReached);
                return;
            }
            if (State.Scenario.ScoringMode == ScenarioScoringMode.ObjectiveThenEscort &&
                (State.ObjectiveFor(Side.UsNavy).IsSunk || State.ObjectiveFor(Side.Plan).IsSunk))
            {
                EndByScore(ScenarioEndReason.ObjectiveSunk);
                return;
            }
            var usEliminated = !State.Forces.Where(force => force.Side == Side.UsNavy)
                .SelectMany(force => force.ActiveUnits).Any();
            var planEliminated = !State.Forces.Where(force => force.Side == Side.Plan)
                .SelectMany(force => force.ActiveUnits).Any();
            if ((usEliminated && State.Scenario.ScoringMode != ScenarioScoringMode.CarrierEscape &&
                 State.Scenario.ScoringMode != ScenarioScoringMode.SubmarineEscape) || (planEliminated &&
                State.Scenario.ScoringMode != ScenarioScoringMode.ConvoySurvival))
            {
                EndByScore(ScenarioEndReason.ForceDestroyed);
                return;
            }

            if (State.Scenario.ScoringMode == ScenarioScoringMode.ConvoyArrival ||
                State.Scenario.ScoringMode == ScenarioScoringMode.SubmarineSurvival ||
                State.Scenario.ScoringMode == ScenarioScoringMode.ConvoySurvival ||
                State.Scenario.ScoringMode == ScenarioScoringMode.CarrierEscape ||
                State.Scenario.ScoringMode == ScenarioScoringMode.SubmarineEscape ||
                State.Scenario.ScoringMode == ScenarioScoringMode.CarrierPosition) return;

            var score = CurrentScore();
            var usCanScore = CanInflictFurtherDamage(Side.UsNavy);
            var planCanScore = CanInflictFurtherDamage(Side.Plan);
            var usRemainingScore = RemainingScorableHull(Side.Plan);
            var planRemainingScore = RemainingScorableHull(Side.UsNavy);
            var usCeiling = score.UsObjectiveDamage + (usCanScore ? usRemainingScore : 0);
            var planCeiling = score.PlanObjectiveDamage + (planCanScore ? planRemainingScore : 0);
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
            if (_projectedScore != null) return _projectedScore;
            var scenario = State.Scenario ?? FirstIslandChainScenarios.ContactOffBashiChannel;
            if (scenario.ScoringMode == ScenarioScoringMode.TotalHullHits)
                return new ScenarioScore(TotalHullDamage(Side.Plan), TotalHullDamage(Side.UsNavy), 0, 0);
            if (scenario.ScoringMode == ScenarioScoringMode.GunfireHullHits)
                return new ScenarioScore(TotalGunfireHullDamage(Side.Plan),
                    TotalGunfireHullDamage(Side.UsNavy), 0, 0);
            if (scenario.ScoringMode == ScenarioScoringMode.ConvoyArrival)
            {
                var arrived = State.Forces.Any(force => force.Side == Side.UsNavy && force.HasArrived &&
                    force.ActiveUnits.Any(unit => unit.Definition.Role == UnitRole.Objective));
                var planDestroyed = State.Forces.Where(force => force.Side == Side.Plan && force.Units.Count > 0)
                    .All(force => force.IsDestroyed);
                var merchants = State.Forces.Where(force => force.Side == Side.UsNavy).SelectMany(force => force.Units)
                    .Where(unit => unit.Definition.Role == UnitRole.Objective).ToArray();
                var surviving = merchants.Count(unit => !unit.IsSunk);
                var sunk = merchants.Length - surviving;
                return new ScenarioScore(arrived ? surviving : 0, sunk, 0, 0,
                    arrived || planDestroyed ? "US NAVY VICTORY" : "PLAN VICTORY");
            }
            if (scenario.ScoringMode == ScenarioScoringMode.SubmarineSurvival)
            {
                var planSubmarines = State.Forces.Where(force => force.Side == Side.Plan)
                    .SelectMany(force => force.Units).Where(unit => unit.Definition.IsSubmarine).ToArray();
                var planLosses = planSubmarines.Count(unit => unit.IsSunk);
                var usShipLosses = State.Forces.Where(force => force.Side == Side.UsNavy)
                    .SelectMany(force => force.Units).Count(unit => unit.IsSunk);
                var adjustedLosses = Math.Max(0, planLosses - usShipLosses / 2);
                var planWins = planSubmarines.Length - adjustedLosses >= 2;
                return new ScenarioScore(planLosses, usShipLosses, adjustedLosses, 0,
                    planWins ? "PLAN VICTORY" : "US NAVY VICTORY");
            }
            if (scenario.ScoringMode == ScenarioScoringMode.ConvoySurvival)
            {
                var merchants = State.Forces.Where(force => force.Side == Side.UsNavy)
                    .SelectMany(force => force.Units).Where(unit => unit.Definition.Role == UnitRole.Objective).ToArray();
                var arrived = State.Forces.Where(force => force.Side == Side.UsNavy && force.HasArrived)
                    .SelectMany(force => force.ActiveUnits).Count(unit => unit.Definition.Role == UnitRole.Objective);
                var sunk = merchants.Count(unit => unit.IsSunk);
                var submarinesSunk = State.Forces.Where(force => force.Side == Side.Plan)
                    .SelectMany(force => force.Units).Count(unit => unit.Definition.IsSubmarine && unit.IsSunk);
                var offsets = Math.Min(sunk, submarinesSunk);
                var qualified = arrived + offsets >= 3;
                return new ScenarioScore(arrived, sunk, submarinesSunk, offsets,
                    qualified ? "US NAVY VICTORY" : "PLAN VICTORY");
            }
            if (scenario.ScoringMode == ScenarioScoringMode.CarrierEscape)
            {
                var carrier = State.Unit(scenario.PlanObjectiveUnitId);
                var carrierForce = State.Forces.FirstOrDefault(force => force.Units.Contains(carrier));
                var sunk = carrier == null || carrier.IsSunk;
                var escaped = carrierForce != null && carrierForce.HasArrived && carrier.CanLaunchAircraft;
                return new ScenarioScore(sunk ? 1 : 0, escaped ? 1 : 0,
                    carrier?.HullDamage ?? 0, carrier?.EmbarkedAircraftRemaining ?? 0,
                    sunk ? "US NAVY VICTORY" : escaped ? "PLAN VICTORY" : "DRAW");
            }
            if (scenario.ScoringMode == ScenarioScoringMode.SubmarineEscape)
            {
                var submarines = State.Forces.Where(force => force.Side == Side.Plan && force.IsSubmarineOnly).ToArray();
                var escaped = submarines.Count(force => force.HasArrived && !force.IsDestroyed);
                var sunk = submarines.SelectMany(force => force.Units).Count(unit => unit.IsSunk);
                var remaining = submarines.Count(force => !force.HasArrived && !force.IsDestroyed);
                return new ScenarioScore(escaped, sunk, remaining, 0,
                    escaped >= 3 ? "PLAN VICTORY" : "US NAVY VICTORY");
            }
            if (scenario.ScoringMode == ScenarioScoringMode.CarrierPosition)
            {
                var carrier = State.Unit(scenario.UsObjectiveUnitId);
                var carrierForce = State.Forces.FirstOrDefault(force => force.Units.Contains(carrier));
                var distance = carrierForce == null ? int.MaxValue :
                    carrierForce.Position.DistanceTo(scenario.CarrierObjectiveHex);
                var capable = carrier != null && !carrier.IsSunk && carrier.CanLaunchAircraft;
                var reached = capable && distance <= scenario.CarrierObjectiveRadius;
                return new ScenarioScore(reached ? 1 : 0, reached ? 0 : 1,
                    distance == int.MaxValue ? 99 : distance, carrier?.HullDamage ?? 0,
                    reached ? "US NAVY VICTORY" : "PLAN VICTORY");
            }
            return new ScenarioScore(
                State.Unit(scenario.PlanObjectiveUnitId)?.HullDamage ?? 0,
                State.Unit(scenario.UsObjectiveUnitId)?.HullDamage ?? 0,
                State.Unit(scenario.PlanTieBreakUnitId)?.HullDamage ?? 0,
                State.Unit(scenario.UsTieBreakUnitId)?.HullDamage ?? 0);
        }

        private int TotalHullDamage(Side side) => State.Forces.Where(force => force.Side == side)
            .SelectMany(force => force.Units).Sum(unit => unit.HullDamage);

        private int TotalGunfireHullDamage(Side side) => State.Forces.Where(force => force.Side == side)
            .SelectMany(force => force.Units).Sum(unit => unit.GunfireHullDamage);

        private int RemainingScorableHull(Side side)
        {
            var scenario = State.Scenario ?? FirstIslandChainScenarios.ContactOffBashiChannel;
            if (scenario.ScoringMode == ScenarioScoringMode.TotalHullHits)
                return State.Forces.Where(force => force.Side == side).SelectMany(force => force.Units)
                    .Sum(unit => unit.HullRemaining);
            if (scenario.ScoringMode == ScenarioScoringMode.GunfireHullHits)
                return State.Forces.Where(force => force.Side == side).SelectMany(force => force.Units)
                    .Sum(unit => unit.HullRemaining);
            return ObjectiveUnit(side).HullRemaining;
        }

        private UnitState ObjectiveUnit(Side side)
        {
            var scenario = State.Scenario ?? FirstIslandChainScenarios.ContactOffBashiChannel;
            return State.Unit(side == Side.UsNavy ? scenario.UsObjectiveUnitId : scenario.PlanObjectiveUnitId)
                   ?? State.ObjectiveFor(side);
        }

        private bool CanInflictFurtherDamage(Side side)
        {
            var units = State.Forces.Where(force => force.Side == side).SelectMany(force => force.ActiveUnits);
            if (State.Scenario.ScoringMode == ScenarioScoringMode.GunfireHullHits)
                return units.Any(unit => unit.EffectiveGuns > 0);
            return units.Any(unit => unit.AvailableShortSsm > 0 || unit.AvailableLongSsm > 0 ||
                unit.EffectiveGuns > 0 || unit.EffectiveTorpedoes > 0);
        }

        private void EndByScore(ScenarioEndReason reason)
        {
            var score = CurrentScore();
            State.Result = score.Result;
            State.IsGameOver = true;
            State.Phase = ActivationPhase.GameOver;
            State.EndReason = reason;
            var scoreKind = State.Scenario.ScoringMode == ScenarioScoringMode.TotalHullHits
                ? "total hull hits" : State.Scenario.ScoringMode == ScenarioScoringMode.GunfireHullHits
                    ? "gunfire hull hits" : State.Scenario.ScoringMode == ScenarioScoringMode.SubmarineSurvival
                        ? "submarine survival offsets" : State.Scenario.ScoringMode == ScenarioScoringMode.ConvoySurvival
                            ? "convoy arrivals and submarine offsets" : State.Scenario.ScoringMode == ScenarioScoringMode.CarrierEscape
                                ? "Fujian escape capability" : State.Scenario.ScoringMode == ScenarioScoringMode.SubmarineEscape
                                    ? "submarine east-edge escapes" : State.Scenario.ScoringMode == ScenarioScoringMode.CarrierPosition
                                        ? "Ford launch-capable arrival" : "objective damage";
            Trace("VICTORY", $"{State.Result}; reason={reason}; {scoreKind} inflicted US/PLAN=" +
                $"{score.UsObjectiveDamage}/{score.PlanObjectiveDamage}; tie-break US/PLAN=" +
                $"{score.UsTieBreakDamage}/{score.PlanTieBreakDamage}.");
            AddLog($"{State.Result} ({reason}): {scoreKind} inflicted US {score.UsObjectiveDamage}, " +
                $"PLAN {score.PlanObjectiveDamage}.");
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
            => CaptureSnapshotInternal(State.ActiveSide, false);

        public ScenarioOneSnapshot CaptureSnapshotFor(Side viewer)
            => CaptureSnapshotInternal(viewer, true);

        private ScenarioOneSnapshot CaptureSnapshotInternal(Side viewer, bool redactHidden)
        {
            var redact = redactHidden && State.DetectionRulesEnabled && !State.IsGameOver;
            Func<TaskForceState, bool> visible = force => force.Side == viewer || !redact ||
                State.Detection.IsDetected(viewer, force.Id);
            Func<TaskForceState, bool> classified = force => force.Side == viewer || !redact ||
                State.Detection.IsClassified(viewer, force.Id);
            Func<TacticalFlightState, TacticalFlightSnapshot> tacticalSnapshot = flight =>
            {
                var snapshot = flight.Capture();
                if (!redact || flight.Side == viewer) return snapshot;
                snapshot.readyAircraft = snapshot.aircraftRemaining;
                snapshot.flownAircraft = 0;
                snapshot.abortedAircraft = 0;
                snapshot.mission = snapshot.aircraftRemaining == 0
                    ? TacticalAirMission.Destroyed : TacticalAirMission.Ready;
                snapshot.radarOn = false;
                return snapshot;
            };
            var view = State.ViewFor(viewer);
            var publicScore = CurrentScore();
            var usVisible = visible(State.Player);
            var planVisible = visible(State.Enemy);
            return new ScenarioOneSnapshot
            {
                scenarioId = State.Scenario?.Id ?? "fic-01",
                seed = Seed,
                detectionRulesEnabled = State.DetectionRulesEnabled,
                revision = view.Revision,
                turn = State.Turn,
                phase = State.Phase,
                activeSide = State.ActiveSide,
                activeFormationId = State.ActiveForce != null && visible(State.ActiveForce)
                    ? State.ActiveFormationId : string.Empty,
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
                usColumn = usVisible ? State.Player.Position.Column : 0,
                usRow = usVisible ? State.Player.Position.Row : 0,
                planColumn = planVisible ? State.Enemy.Position.Column : 0,
                planRow = planVisible ? State.Enemy.Position.Row : 0,
                usDeclaredSpeed = usVisible ? State.Player.DeclaredSpeed : -1,
                usMovementSpent = usVisible ? State.Player.MovementPointsSpent : 0,
                planDeclaredSpeed = planVisible ? State.Enemy.DeclaredSpeed : -1,
                planMovementSpent = planVisible ? State.Enemy.MovementPointsSpent : 0,
                usMovementPath = usVisible ? State.Player.MovementPath.Select(ToSnapshot).ToArray() : Array.Empty<HexCoordSnapshot>(),
                planMovementPath = planVisible ? State.Enemy.MovementPath.Select(ToSnapshot).ToArray() : Array.Empty<HexCoordSnapshot>(),
                units = State.Forces.Where(classified).SelectMany(force => force.Units).Select(unit => new UnitSnapshot
                {
                    id = unit.Definition.Id,
                    hullDamage = unit.HullDamage,
                    gunfireHullDamage = unit.GunfireHullDamage,
                    shortMissiles = unit.ShortMissilesRemaining,
                    longMissiles = unit.LongMissilesRemaining,
                    embarkedAircraft = unit.EmbarkedAircraftRemaining,
                    serviceableAircraft = unit.ServiceableAircraftRemaining,
                    aircraftMissionState = unit.AircraftMissionState,
                    aircraftReadyTurn = unit.AircraftReadyTurn,
                    aircraftLastAttackTurn = unit.AircraftLastAttackTurn
                }).ToArray(),
                eventLog = redact
                    ? new[] { "Opponent position and formation contents remain hidden until detected." }
                        .Concat(State.CommandLog.Where(item => item.type == GameCommandType.TransferDummyCards)
                            .Select(item => $"{item.actor} openly verified transfer of {item.factors} dummy card(s); no real ships transferred."))
                        .ToArray()
                    : State.Log.ToArray(),
                transactions = (redact ? Enumerable.Empty<RuleTransaction>() : State.Transactions)
                    .Select(item => new TransactionSnapshot
                {
                    sequence = item.Sequence,
                    turn = item.Turn,
                    phase = item.Phase,
                    category = item.Category,
                    detail = item.Detail
                }).ToArray(),
                events = (redact ? Enumerable.Empty<RuleEvent>() : State.Events).Select(item => new RuleEventSnapshot
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
                commands = State.CommandLog.Where(item => !redact || item.actor == viewer ||
                    item.type == GameCommandType.TransferDummyCards).Select(item => new GameCommandData
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
                    column = visible(force) ? force.Position.Column : 0,
                    row = visible(force) ? force.Position.Row : 0,
                    declaredSpeed = visible(force) ? force.DeclaredSpeed : -1,
                    movementSpent = visible(force) ? force.MovementPointsSpent : 0,
                    movementPath = visible(force) ? force.MovementPath.Select(ToSnapshot).ToArray() : Array.Empty<HexCoordSnapshot>(),
                    unitIds = classified(force) ? force.Units.Select(unit => unit.Definition.Id).ToArray() : Array.Empty<string>(),
                    radarRadiating = visible(force) && force.RadarRadiating,
                    radarDeclared = visible(force) && force.RadarDeclaredThisActivation,
                    arrived = visible(force) && force.HasArrived,
                    entered = visible(force) && force.HasEnteredMap,
                    dummyCards = force.Side == viewer || !redact ? force.DummyCards : 0,
                    defensePairs = visible(force) ? force.DefensePairs.ToArray() : Array.Empty<DefensePairData>(),
                    aircraftSearchModes = visible(force) ? force.AircraftSearchModes.ToArray() : Array.Empty<string>()
                }).ToArray(),
                contacts = State.Detection.Contacts.Where(contact => !redact || contact.Observer == viewer)
                    .Select(contact => contact.ToData()).ToArray(),
                missileCombat = State.PendingMissileCombat?.ToData(),
                gunCombat = State.PendingGunCombat?.ToData(),
                hasProjectedScore = redact,
                scoreUsObjective = publicScore.UsObjectiveDamage,
                scorePlanObjective = publicScore.PlanObjectiveDamage,
                scoreUsTieBreak = publicScore.UsTieBreakDamage,
                scorePlanTieBreak = publicScore.PlanTieBreakDamage,
                scoreResult = publicScore.Result
                ,tacticalFlights = State.TacticalFlights.Select(tacticalSnapshot).ToArray()
                ,airBases = State.AirBases.Select(airBase => new AirBaseSnapshot
                {
                    id = airBase.Definition.Id,
                    runwayHits = airBase.RunwayHits
                }).ToArray()
            };
        }

        public void ApplySnapshot(ScenarioOneSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.seed != 0) Seed = snapshot.seed;
            var snapshotScenario = FirstIslandChainScenarios.Get(snapshot.scenarioId);
            if (snapshotScenario == null)
                throw new InvalidOperationException($"Unknown scenario ID '{snapshot.scenarioId}'.");
            if (State.Scenario?.Id != snapshotScenario.Id)
            {
                State = ScenarioOne.Create(snapshot.detectionRulesEnabled, snapshotScenario);
                State.MovementCup = new MovementChitCup(new SeededDieRoller(Seed));
            }
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
                var canonicalState = ScenarioOne.Create(snapshot.detectionRulesEnabled, snapshotScenario);
                var availableUnits = canonicalState.Forces.SelectMany(force => force.Units)
                    .ToDictionary(unit => unit.Definition.Id);
                var restoredForces = snapshot.formations.Select(item =>
                {
                    var force = new TaskForceState(item.id, item.side, new HexCoord(item.column, item.row),
                        (item.unitIds ?? Array.Empty<string>()).Where(availableUnits.ContainsKey)
                        .Select(id => availableUnits[id]), item.dummyCards, !item.entered);
                    force.RestoreMovement(item.declaredSpeed, item.movementSpent,
                        (item.movementPath ?? Array.Empty<HexCoordSnapshot>())
                        .Select(hex => new HexCoord(hex.column, hex.row)));
                    force.RestoreSensors(item.radarRadiating, item.radarDeclared);
                    force.RestoreArrival(item.arrived, item.entered);
                    force.SetDefensePairs(item.defensePairs);
                    force.RestoreAircraftSearchModes(item.aircraftSearchModes);
                    return force;
                }).ToArray();
                State.ReplaceForces(restoredForces);
            }
            else
            {
                State.Player.MoveTo(new HexCoord(snapshot.usColumn, snapshot.usRow));
                State.Enemy.MoveTo(new HexCoord(snapshot.planColumn, snapshot.planRow));
                State.Player.RestoreMovement(snapshot.usDeclaredSpeed, snapshot.usMovementSpent,
                    (snapshot.usMovementPath ?? Array.Empty<HexCoordSnapshot>())
                    .Select(item => new HexCoord(item.column, item.row)));
                State.Enemy.RestoreMovement(snapshot.planDeclaredSpeed, snapshot.planMovementSpent,
                    (snapshot.planMovementPath ?? Array.Empty<HexCoordSnapshot>())
                    .Select(item => new HexCoord(item.column, item.row)));
            }
            foreach (var unitSnapshot in snapshot.units ?? Array.Empty<UnitSnapshot>())
            {
                var unit = State.Forces.SelectMany(force => force.Units)
                    .FirstOrDefault(candidate => candidate.Definition.Id == unitSnapshot.id);
                unit?.Restore(unitSnapshot.hullDamage, unitSnapshot.shortMissiles, unitSnapshot.longMissiles,
                        unitSnapshot.gunfireHullDamage, unitSnapshot.embarkedAircraft,
                        unitSnapshot.serviceableAircraft, unitSnapshot.aircraftMissionState,
                        unitSnapshot.aircraftReadyTurn, unitSnapshot.aircraftLastAttackTurn);
            }
            if (snapshot.tacticalFlights != null && snapshot.tacticalFlights.Length > 0)
            {
                var restoredBases = State.Scenario.AirBaseIds.Select(id =>
                    new AirBaseState(ModernAirBaseDatabase.Get(id))).ToArray();
                foreach (var savedBase in snapshot.airBases ?? Array.Empty<AirBaseSnapshot>())
                    restoredBases.FirstOrDefault(item => item.Definition.Id == savedBase.id)?.Restore(savedBase.runwayHits);
                State.ConfigureTacticalAir(snapshot.tacticalFlights.Select(TacticalFlightState.Restore), restoredBases);
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
            _projectedScore = snapshot.hasProjectedScore
                ? new ScenarioScore(snapshot.scoreUsObjective, snapshot.scorePlanObjective,
                    snapshot.scoreUsTieBreak, snapshot.scorePlanTieBreak, snapshot.scoreResult)
                : null;
        }

        public static ScenarioOneGame Replay(int seed, IEnumerable<GameCommandData> commands,
            Func<HexCoord, bool> isNavigable = null, bool detectionRulesEnabled = false,
            bool manualOpponent = true, ScenarioDefinition scenario = null)
        {
            var replay = new ScenarioOneGame(seed, isNavigable, true, detectionRulesEnabled, null, scenario);
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
