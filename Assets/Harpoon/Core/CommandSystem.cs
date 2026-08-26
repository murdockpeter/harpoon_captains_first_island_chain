using System;
using System.Collections.Generic;
using System.Linq;

namespace Harpoon.Core
{
    public interface IRulesEngine
    {
        GameState State { get; }
        CommandResult Execute(GameCommand command);
        SideGameView ViewFor(Side viewer, bool? opponentKnown = null);
    }

    public enum GameCommandType
    {
        DrawMovementChit,
        SplitTaskForce,
        DeclareSpeed,
        Move,
        RadiateRadar,
        Search,
        AllocateMissileFire,
        Attack,
        Defend,
        Counterattack,
        ArrangeGunfire,
        FireGuns,
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
        CupNotReady,
        SplitWindowClosed,
        InvalidFormation,
        InvalidUnitSelection,
        NightRestricted,
        RadarDeclarationRequired,
        SensorUnavailable,
        TargetUndetected,
        NoDetectionOpportunity,
        NoPendingCombat,
        InvalidAllocation,
        InsufficientAmmunition,
        InvalidDefense,
        InvalidGunPairing,
        InvalidGunTarget,
        BreakOffUnavailable,
        CounterattackUnavailable,
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
        ChitDrawn,
        FormationSplit,
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
    public sealed class MissileAllocationData
    {
        public string id;
        public string sourceUnitId;
        public string targetUnitId;
        public int shortFactors;
        public int longFactors;
    }

    [Serializable]
    public sealed class DefensePairData
    {
        public string firstUnitId;
        public string secondUnitId;
    }

    [Serializable]
    public sealed class MissileReductionData
    {
        public string salvoId;
        public int factors;
    }

    [Serializable]
    public sealed class ShortRangeDefenseData
    {
        public string defendingUnitId;
        public string salvoId;
    }

    [Serializable]
    public sealed class GunPairData
    {
        public string firingUnitId;
        public string screenedUnitId;
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
        public string sourceUnitId;
        public bool enabled;
        public string formationId;
        public string newFormationId;
        public string[] unitIds;
        public string searchMode;
        public MissileAllocationData[] missileAllocations;
        public DefensePairData[] defensePairs;
        public MissileReductionData[] missileReductions;
        public ShortRangeDefenseData[] shortRangeDefenses;
        public GunPairData[] gunPairs;
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
        public string SourceUnitId { get; }
        public bool Enabled { get; }
        public string FormationId { get; }
        public string NewFormationId { get; }
        public IReadOnlyList<string> UnitIds { get; }
        public string SearchMode { get; }
        public IReadOnlyList<MissileAllocationData> MissileAllocations { get; }
        public IReadOnlyList<DefensePairData> DefensePairs { get; }
        public IReadOnlyList<MissileReductionData> MissileReductions { get; }
        public IReadOnlyList<ShortRangeDefenseData> ShortRangeDefenses { get; }
        public IReadOnlyList<GunPairData> GunPairs { get; }

        public GameCommand(GameCommandType type, Side actor, int expectedRevision,
            HexCoord destination = default, int declaredSpeed = 0, int factors = 0,
            string targetId = null, bool enabled = false, string id = null,
            string formationId = null, string newFormationId = null, IEnumerable<string> unitIds = null,
            string searchMode = null, IEnumerable<MissileAllocationData> missileAllocations = null,
            IEnumerable<DefensePairData> defensePairs = null,
            IEnumerable<MissileReductionData> missileReductions = null,
            IEnumerable<ShortRangeDefenseData> shortRangeDefenses = null,
            string sourceUnitId = null, IEnumerable<GunPairData> gunPairs = null)
        {
            Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id;
            Type = type;
            Actor = actor;
            ExpectedRevision = expectedRevision;
            Destination = destination;
            DeclaredSpeed = declaredSpeed;
            Factors = factors;
            TargetId = targetId ?? string.Empty;
            SourceUnitId = sourceUnitId ?? string.Empty;
            Enabled = enabled;
            FormationId = formationId ?? string.Empty;
            NewFormationId = newFormationId ?? string.Empty;
            UnitIds = (unitIds ?? Array.Empty<string>()).ToArray();
            SearchMode = searchMode ?? string.Empty;
            MissileAllocations = (missileAllocations ?? Array.Empty<MissileAllocationData>()).ToArray();
            DefensePairs = (defensePairs ?? Array.Empty<DefensePairData>()).ToArray();
            MissileReductions = (missileReductions ?? Array.Empty<MissileReductionData>()).ToArray();
            ShortRangeDefenses = (shortRangeDefenses ?? Array.Empty<ShortRangeDefenseData>()).ToArray();
            GunPairs = (gunPairs ?? Array.Empty<GunPairData>()).ToArray();
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
            sourceUnitId = SourceUnitId,
            enabled = Enabled,
            formationId = FormationId,
            newFormationId = NewFormationId,
            unitIds = UnitIds.ToArray(),
            searchMode = SearchMode,
            missileAllocations = MissileAllocations.ToArray(),
            defensePairs = DefensePairs.ToArray(),
            missileReductions = MissileReductions.ToArray(),
            shortRangeDefenses = ShortRangeDefenses.ToArray()
            ,gunPairs = GunPairs.ToArray()
        };

        public static GameCommand FromData(GameCommandData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            return new GameCommand(data.type, data.actor, data.expectedRevision,
                new HexCoord(data.column, data.row), data.declaredSpeed, data.factors,
                data.targetId, data.enabled, data.id, data.formationId,
                data.newFormationId, data.unitIds, data.searchMode, data.missileAllocations,
                data.defensePairs, data.missileReductions, data.shortRangeDefenses,
                data.sourceUnitId, data.gunPairs);
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
        public int HullDamage { get; }
        public ShipDamageLevel DamageLevel { get; }
        public int EffectiveSpeed { get; }
        public bool IsSunk { get; }
        public int ShortMissiles { get; }
        public int LongMissiles { get; }

        public UnitView(UnitState unit)
        {
            Id = unit.Definition.Id;
            Name = unit.Definition.DisplayName;
            HullRemaining = unit.HullRemaining;
            HullDamage = unit.HullDamage;
            DamageLevel = unit.DamageLevel;
            EffectiveSpeed = unit.EffectiveSpeed;
            IsSunk = unit.IsSunk;
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
        public ContactLevel ContactStatus { get; }
        public DetectionMethod DetectionMethod { get; }

        public FormationViewState(TaskForceState force, bool isKnown, bool includePrivateDetails,
            ContactRecord contact = null)
        {
            ContactStatus = isKnown ? ContactLevel.Classified : contact?.Level ?? ContactLevel.Undetected;
            DetectionMethod = contact?.Method ?? (isKnown ? DetectionMethod.ScenarioKnown : DetectionMethod.None);
            Id = isKnown ? force.Id : $"CONTACT {force.Side}";
            Side = force.Side;
            IsKnown = isKnown;
            // Task-force counters move openly on the board; detection controls whether the
            // counter represents real surface ships and whether its contents can be examined.
            Position = force.Position;
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
        public string ActiveFormationId { get; }
        public int Day { get; }
        public TimeOfDay TimeOfDay { get; }
        public int ChitsRemaining { get; }
        public FormationViewState OwnFormation { get; }
        public FormationViewState OpposingFormation { get; }
        public IReadOnlyList<FormationViewState> OwnFormations { get; }
        public IReadOnlyList<FormationViewState> OpposingFormations { get; }

        public SideGameView(Side viewer, GameState state, bool? opponentKnown)
        {
            Viewer = viewer;
            Revision = state.Revision;
            Turn = state.Turn;
            Phase = state.Phase;
            ActiveSide = state.ActiveSide;
            ActiveFormationId = state.ActiveFormationId;
            Day = state.Day;
            TimeOfDay = state.TimeOfDay;
            ChitsRemaining = state.MovementCup?.Remaining.Count ?? 0;
            OwnFormations = state.Forces.Where(force => force.Side == viewer)
                .Select(force => new FormationViewState(force, true, true)).ToArray();
            OpposingFormations = state.Forces.Where(force => force.Side == Opposing(viewer)).Select(force =>
            {
                var contact = state.Detection.ContactFor(viewer, force.Id);
                var classified = opponentKnown ?? (!state.DetectionRulesEnabled ||
                                 contact.Level == ContactLevel.Classified);
                return new FormationViewState(force, classified, classified, contact);
            }).ToArray();
            OwnFormation = OwnFormations.First();
            OpposingFormation = OpposingFormations.FirstOrDefault();
        }

        private static Side Opposing(Side side) => side == Side.UsNavy ? Side.Plan : Side.UsNavy;
    }
}
