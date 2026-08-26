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
    public sealed class ScenarioOneSnapshot
    {
        public int revision;
        public int turn;
        public ActivationPhase phase;
        public Side activeSide;
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
    }

    public static class ScenarioOne
    {
        public const string Name = "Contact off the Bashi Channel";

        public static GameState Create()
        {
            var burke = new UnitDefinition("us-burke-iia", "Arleigh Burke Flight IIA", Side.UsNavy,
                UnitRole.Escort, 3, 8, 4, 2, 1, 2, 3, 2, 2, 1, 4, 5);
            var merchant = new UnitDefinition("us-merchant", "Merchant Ship", Side.UsNavy,
                UnitRole.Objective, 0, 0, 0, 0, 0, 0, 2, 4, 0, 1, 0, 0);
            var type054A = new UnitDefinition("plan-type-054a", "Type 054A Frigate", Side.Plan,
                UnitRole.Escort, 3, 0, 3, 2, 0, 1, 2, 1, 1, 1, 3, 3);
            var type071 = new UnitDefinition("plan-type-071", "Type 071 LPD", Side.Plan,
                UnitRole.Objective, 0, 0, 2, 0, 0, 1, 2, 3, 1, 1, 0, 1);

            var us = new TaskForceState("US Task Force", Side.UsNavy, new HexCoord(7, 13),
                new[] { new UnitState(burke), new UnitState(merchant) });
            var plan = new TaskForceState("PLAN Task Force", Side.Plan, new HexCoord(10, 10),
                new[] { new UnitState(type054A), new UnitState(type071) });
            var state = new GameState(us, plan, 0, FirstIslandChainMap.Instance);
            state.Log.Add($"Scenario 1: {Name}");
            state.Log.Add("Damage the enemy objective ship while protecting your merchant.");
            state.Trace("SETUP", $"Scenario 1 '{Name}' loaded; no turn limit; detection rules omitted.");
            state.Trace("SETUP", $"US Task Force at {us.Position}: {string.Join(", ", us.Units.Select(unit => unit.Definition.DisplayName))}.");
            state.Trace("SETUP", $"PLAN Task Force at {plan.Position}: {string.Join(", ", plan.Units.Select(unit => unit.Definition.DisplayName))}.");
            return state;
        }
    }

    public sealed class ScenarioOneGame : IRulesEngine
    {
        private readonly IDieRoller _dice;
        private readonly CombatResolver _combat;
        private readonly Func<HexCoord, bool> _isNavigable;
        private readonly bool _manualOpponent;
        private bool _isActivatingEnemy;
        public GameState State { get; private set; }
        public int Seed { get; }
        public event Action<Side, AttackReport> AttackResolved;
        public event Action<GameCommand, CommandResult> CommandProcessed;

        public SideGameView ViewFor(Side viewer, bool opponentKnown = true) =>
            State.ViewFor(viewer, opponentKnown);

        public ScenarioOneGame(int seed = 2026, Func<HexCoord, bool> isNavigable = null,
            bool manualOpponent = false)
        {
            Seed = seed;
            _dice = new SeededDieRoller(seed);
            _isNavigable = isNavigable ?? (_ => true);
            _manualOpponent = manualOpponent;
            State = ScenarioOne.Create();
            _combat = new CombatResolver(_dice, Trace);
            BeginTurn();
            if (!_manualOpponent && State.ActiveSide == Side.Plan) ActivateEnemy();
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
                    case GameCommandType.DeclareSpeed:
                        accepted = DeclareSpeedInternal(command.Actor, command.DeclaredSpeed, out violation);
                        break;
                    case GameCommandType.Move:
                        accepted = TryMoveInternal(command.Actor, command.Destination, out violation);
                        break;
                    case GameCommandType.Attack:
                        attackReport = AttackInternal(command.Actor, out violation);
                        accepted = violation == null;
                        break;
                    case GameCommandType.Search:
                        accepted = SearchInternal(command.Actor, out violation);
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
            return true;
        }

        private bool SearchInternal(Side side, out RuleViolation violation)
        {
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
            if (State.PlayerHasSearched)
            {
                violation = new RuleViolation(RuleViolationCode.AlreadyActed,
                    "This task force has already searched in the current hex.");
                Trace("REJECTED", $"{side} search: {violation.Message}");
                return false;
            }
            State.PlayerHasSearched = true;
            Trace("DETECTION", $"{State.ForceFor(side).Id} used its search opportunity in " +
                $"{State.ForceFor(side).Position}; Scenario 1 omits detection resolution.");
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
            var defender = State.ForceFor(side == Side.UsNavy ? Side.Plan : Side.UsNavy);
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
            Trace("ACTIVATION", $"{side} ended activation.");
            if (side == Side.UsNavy) State.UsActivated = true;
            else State.PlanActivated = true;
            if (!State.UsActivated || !State.PlanActivated)
            {
                State.ActiveSide = State.UsActivated ? Side.Plan : Side.UsNavy;
                State.PlayerHasMoved = false;
                State.PlayerHasAttacked = false;
                State.PlayerHasSearched = false;
                State.ForceFor(State.ActiveSide).ResetActivation();
                State.Phase = ActivationPhase.DeclareSpeed;
                Trace("ACTIVATION", $"{State.ActiveSide} activation begins.");
                return true;
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
            State.Player.ResetActivation();
            State.Enemy.ResetActivation();
            var initiativeRoll = _dice.RollD6();
            State.EnemyActivatedFirst = initiativeRoll <= 3;
            State.ActiveSide = State.EnemyActivatedFirst ? Side.Plan : Side.UsNavy;
            Trace("DIE", $"Movement-chit prototype roll D6={initiativeRoll}; PLAN first on 1-3.");
            State.Phase = ActivationPhase.DeclareSpeed;
            AddLog($"Turn {State.Turn} ({TimeLabel(State.Turn)}). " +
                   (State.EnemyActivatedFirst ? "PLAN chit drawn first." : "US chit drawn first."));
        }

        private void ActivateEnemy()
        {
            if (_isActivatingEnemy || State.IsGameOver || State.Enemy.IsDestroyed) return;
            _isActivatingEnemy = true;
            try
            {
                // A new turn may also draw PLAN first, so finish consecutive PLAN activations
                // without recursively re-entering through Execute.
                while (!State.IsGameOver && State.ActiveSide == Side.Plan)
                {
                    if (State.Phase == ActivationPhase.DeclareSpeed)
                    {
                        var path = State.Map.FindPath(State.Enemy.Position, State.Player.Position, Side.Plan);
                        var declared = path.Count == 0 ? 0 : Math.Min(State.Enemy.EffectiveSpeed,
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
                        if (!State.PlayerHasAttacked && State.Enemy.Position.DistanceTo(State.Player.Position) <= 3)
                            Execute(new GameCommand(GameCommandType.Attack, Side.Plan, State.Revision));
                    }
                    if (!State.IsGameOver && State.Phase == ActivationPhase.PlayerAction &&
                        !State.PlayerHasAttacked)
                        Execute(new GameCommand(GameCommandType.Attack, Side.Plan, State.Revision));
                    if (!State.IsGameOver && State.ActiveSide == Side.Plan)
                        Execute(new GameCommand(GameCommandType.EndActivation, Side.Plan, State.Revision));
                }
            }
            finally { _isActivatingEnemy = false; }
        }

        private HexCoord BestEnemyDestination()
        {
            return State.Map.NavigableNeighbors(State.Enemy.Position, Side.Plan).Where(_isNavigable)
                .OrderBy(hex => hex.DistanceTo(State.Player.Position))
                .DefaultIfEmpty(State.Enemy.Position).First();
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
            if (State.Player.Objective.IsSunk || State.Enemy.Objective.IsSunk) EndByScore();
        }

        private void EndByScore()
        {
            var damageInflicted = State.Enemy.Objective.HullDamage;
            var damageSuffered = State.Player.Objective.HullDamage;
            var combatantDamageInflicted = State.Enemy.Units
                .Where(unit => unit.Definition.Role != UnitRole.Objective).Sum(unit => unit.HullDamage);
            var combatantDamageSuffered = State.Player.Units
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
                units = State.Player.Units.Concat(State.Enemy.Units).Select(unit => new UnitSnapshot
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
                    enabled = item.enabled
                }).ToArray()
            };
        }

        public void ApplySnapshot(ScenarioOneSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            State.Revision = snapshot.revision;
            State.Turn = snapshot.turn;
            State.Phase = snapshot.phase;
            State.ActiveSide = snapshot.activeSide;
            State.UsActivated = snapshot.usActivated;
            State.PlanActivated = snapshot.planActivated;
            State.PlayerHasMoved = snapshot.hasMoved;
            State.PlayerHasAttacked = snapshot.hasAttacked;
            State.PlayerHasSearched = snapshot.hasSearched;
            State.IsGameOver = snapshot.gameOver;
            State.Result = snapshot.result ?? string.Empty;
            State.Player.MoveTo(new HexCoord(snapshot.usColumn, snapshot.usRow));
            State.Enemy.MoveTo(new HexCoord(snapshot.planColumn, snapshot.planRow));
            State.Player.RestoreMovement(snapshot.usDeclaredSpeed, snapshot.usMovementSpent,
                (snapshot.usMovementPath ?? Array.Empty<HexCoordSnapshot>())
                .Select(item => new HexCoord(item.column, item.row)));
            State.Enemy.RestoreMovement(snapshot.planDeclaredSpeed, snapshot.planMovementSpent,
                (snapshot.planMovementPath ?? Array.Empty<HexCoordSnapshot>())
                .Select(item => new HexCoord(item.column, item.row)));
            foreach (var unitSnapshot in snapshot.units ?? Array.Empty<UnitSnapshot>())
            {
                var unit = State.Player.Units.Concat(State.Enemy.Units)
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
        }

        public static ScenarioOneGame Replay(int seed, IEnumerable<GameCommandData> commands,
            Func<HexCoord, bool> isNavigable = null)
        {
            var replay = new ScenarioOneGame(seed, isNavigable, true);
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
        private static string TimeLabel(int turn)
        {
            var day = ((turn - 1) / 3) + 1;
            var period = (turn - 1) % 3 == 0 ? "AM" : (turn - 1) % 3 == 1 ? "PM" : "Night";
            return $"Day {day} {period}";
        }
    }
}
