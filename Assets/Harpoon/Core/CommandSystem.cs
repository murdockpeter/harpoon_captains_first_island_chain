using System;
using System.Collections.Generic;
using System.Linq;

namespace Harpoon.Core
{
    public interface IRulesEngine
    {
        GameState State { get; }
        CommandResult Execute(GameCommand command);
        SideGameView ViewFor(Side viewer, bool opponentKnown = true);
    }

    public enum GameCommandType
    {
        DeclareSpeed,
        Move,
        RadiateRadar,
        Search,
        AllocateMissileFire,
        Attack,
        Defend,
        Counterattack,
        BreakOff,
        EndActivation,
        Concede
    }

    public enum RuleViolationCode
    {
        None,
        GameOver,
        StaleRevision,
        DuplicateCommand,
        WrongSide,
        WrongPhase,
        OutsideMap,
        ImpassableTerrain,
        BeyondMovementAllowance,
        NotAdjacent,
        SpeedNotDeclared,
        InvalidSpeed,
        MovementIncomplete,
        MovementExhausted,
        NoLegalWeapon,
        AlreadyActed,
        InvalidPayload,
        UnsupportedCommand
    }

    public enum RuleEventType
    {
        Setup,
        CommandAccepted,
        CommandRejected,
        Turn,
        Activation,
        Movement,
        SpeedDeclared,
        MovementOpportunity,
        DieRolled,
        Ammunition,
        Combat,
        Defense,
        Damage,
        Victory,
        Detection,
        Network,
        Information
    }

    [Serializable]
    public sealed class GameCommandData
    {
        public string id;
        public GameCommandType type;
        public Side actor;
        public int expectedRevision;
        public int column;
        public int row;
        public int declaredSpeed;
        public int factors;
        public string targetId;
        public bool enabled;
    }

    public sealed class GameCommand
    {
        public string Id { get; }
        public GameCommandType Type { get; }
        public Side Actor { get; }
        public int ExpectedRevision { get; }
        public HexCoord Destination { get; }
        public int DeclaredSpeed { get; }
        public int Factors { get; }
        public string TargetId { get; }
        public bool Enabled { get; }

        public GameCommand(GameCommandType type, Side actor, int expectedRevision,
            HexCoord destination = default, int declaredSpeed = 0, int factors = 0,
            string targetId = null, bool enabled = false, string id = null)
        {
            Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id;
            Type = type;
            Actor = actor;
            ExpectedRevision = expectedRevision;
            Destination = destination;
            DeclaredSpeed = declaredSpeed;
            Factors = factors;
            TargetId = targetId ?? string.Empty;
            Enabled = enabled;
        }

        public GameCommandData ToData() => new GameCommandData
        {
            id = Id,
            type = Type,
            actor = Actor,
            expectedRevision = ExpectedRevision,
            column = Destination.Column,
            row = Destination.Row,
            declaredSpeed = DeclaredSpeed,
            factors = Factors,
            targetId = TargetId,
            enabled = Enabled
        };

        public static GameCommand FromData(GameCommandData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            return new GameCommand(data.type, data.actor, data.expectedRevision,
                new HexCoord(data.column, data.row), data.declaredSpeed, data.factors,
                data.targetId, data.enabled, data.id);
        }
    }

    public sealed class RuleViolation
    {
        public RuleViolationCode Code { get; }
        public string Message { get; }
        public string Field { get; }

        public RuleViolation(RuleViolationCode code, string message, string field = "")
        {
            Code = code;
            Message = message ?? string.Empty;
            Field = field ?? string.Empty;
        }

        public override string ToString() => $"{Code}: {Message}";
    }

    public sealed class RuleEvent
    {
        public int Sequence { get; }
        public int Revision { get; }
        public int Turn { get; }
        public ActivationPhase Phase { get; }
        public RuleEventType Type { get; }
        public Side Actor { get; }
        public string CommandId { get; }
        public string Detail { get; }

        public RuleEvent(int sequence, int revision, int turn, ActivationPhase phase,
            RuleEventType type, Side actor, string commandId, string detail)
        {
            Sequence = sequence;
            Revision = revision;
            Turn = turn;
            Phase = phase;
            Type = type;
            Actor = actor;
            CommandId = commandId ?? string.Empty;
            Detail = detail ?? string.Empty;
        }
    }

    public sealed class CommandResult
    {
        public bool Accepted { get; }
        public RuleViolation Violation { get; }
        public IReadOnlyList<RuleEvent> Events { get; }
        public AttackReport AttackReport { get; }
        public string Summary => Accepted ?
            (AttackReport?.Summary ?? "Command accepted.") : Violation?.Message ?? "Command rejected.";

        private CommandResult(bool accepted, RuleViolation violation,
            IReadOnlyList<RuleEvent> events, AttackReport attackReport)
        {
            Accepted = accepted;
            Violation = violation;
            Events = events ?? Array.Empty<RuleEvent>();
            AttackReport = attackReport;
        }

        public static CommandResult Success(IEnumerable<RuleEvent> events, AttackReport report = null) =>
            new CommandResult(true, null, events.ToArray(), report);

        public static CommandResult Rejected(RuleViolation violation, IEnumerable<RuleEvent> events) =>
            new CommandResult(false, violation, events.ToArray(), null);
    }

    public sealed class UnitView
    {
        public string Id { get; }
        public string Name { get; }
        public int HullRemaining { get; }
        public int ShortMissiles { get; }
        public int LongMissiles { get; }

        public UnitView(UnitState unit)
        {
            Id = unit.Definition.Id;
            Name = unit.Definition.DisplayName;
            HullRemaining = unit.HullRemaining;
            ShortMissiles = unit.ShortMissilesRemaining;
            LongMissiles = unit.LongMissilesRemaining;
        }
    }

    public sealed class FormationViewState
    {
        public string Id { get; }
        public Side Side { get; }
        public bool IsKnown { get; }
        public HexCoord Position { get; }
        public int DeclaredSpeed { get; }
        public int MovementPointsSpent { get; }
        public int MovementRemaining { get; }
        public IReadOnlyList<UnitView> Units { get; }

        public FormationViewState(TaskForceState force, bool isKnown, bool includePrivateDetails)
        {
            Id = isKnown ? force.Id : "UNKNOWN CONTACT";
            Side = force.Side;
            IsKnown = isKnown;
            Position = isKnown ? force.Position : default;
            DeclaredSpeed = isKnown ? force.DeclaredSpeed : -1;
            MovementPointsSpent = isKnown ? force.MovementPointsSpent : 0;
            MovementRemaining = isKnown ? force.MovementRemaining : 0;
            Units = isKnown && includePrivateDetails
                ? force.Units.Select(unit => new UnitView(unit)).ToArray()
                : Array.Empty<UnitView>();
        }
    }

    public sealed class SideGameView
    {
        public Side Viewer { get; }
        public int Revision { get; }
        public int Turn { get; }
        public ActivationPhase Phase { get; }
        public Side ActiveSide { get; }
        public FormationViewState OwnFormation { get; }
        public FormationViewState OpposingFormation { get; }

        public SideGameView(Side viewer, GameState state, bool opponentKnown)
        {
            Viewer = viewer;
            Revision = state.Revision;
            Turn = state.Turn;
            Phase = state.Phase;
            ActiveSide = state.ActiveSide;
            OwnFormation = new FormationViewState(state.ForceFor(viewer), true, true);
            // Scenario 1 omits detection, so opponentKnown is true there. Future scenarios can hide it.
            OpposingFormation = new FormationViewState(state.ForceFor(Opposing(viewer)), opponentKnown, opponentKnown);
        }

        private static Side Opposing(Side side) => side == Side.UsNavy ? Side.Plan : Side.UsNavy;
    }
}
