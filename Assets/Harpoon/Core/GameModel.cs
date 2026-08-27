using System;
using System.Collections.Generic;
using System.Linq;

namespace Harpoon.Core
{
    public enum Side { UsNavy, Plan }
    public enum UnitRole { Escort, Objective }
    public enum DamageSource { Other, Missile, Gunfire, Torpedo, Bomb }
    public enum ShipDamageLevel { Operational, HalfDamage, TwoThirdsDamage, Sunk }
    public enum ActivationPhase { AwaitingChit, DeclareSpeed, PlayerMove, PlayerAction, MissileCombat, GunCombat, GameOver }

    public sealed class RuleTransaction
    {
        public int Sequence { get; }
        public int Turn { get; }
        public ActivationPhase Phase { get; }
        public string Category { get; }
        public string Detail { get; }

        public RuleTransaction(int sequence, int turn, ActivationPhase phase, string category, string detail)
        {
            Sequence = sequence;
            Turn = turn;
            Phase = phase;
            Category = category;
            Detail = detail;
        }

        public override string ToString() =>
            $"#{Sequence:0000}  T{Turn:00}  {Phase,-12}  [{Category}]  {Detail}";
    }

    public sealed class UnitDefinition
    {
        public string Id { get; }
        public string DisplayName { get; }
        public Side Side { get; }
        public UnitRole Role { get; }
        public int ShortSam { get; }
        public int LongSam { get; }
        public int PointDefense { get; }
        public int ShortSsm { get; }
        public int LongSsm { get; }
        public int Guns { get; }
        public int Speed { get; }
        public int Hull { get; }
        public int AirSearchRadar { get; }
        public int SurfaceSearchRadar { get; }
        public int Sonar { get; }
        public int AntiSubmarineWarfare { get; }
        public bool EsmEquipped { get; }
        public bool IsAircraftCarrier { get; }
        public bool IsSubmarine { get; }
        public int Torpedoes { get; }

        public UnitDefinition(string id, string displayName, Side side, UnitRole role, int shortSam,
            int longSam, int pointDefense, int shortSsm, int longSsm, int guns, int speed, int hull,
            int airSearchRadar = 0, int surfaceSearchRadar = 0, int sonar = 0,
            int antiSubmarineWarfare = 0, bool esmEquipped = true, bool isAircraftCarrier = false,
            int torpedoes = 0, bool isSubmarine = false)
        {
            Id = id;
            DisplayName = displayName;
            Side = side;
            Role = role;
            ShortSam = shortSam;
            LongSam = longSam;
            PointDefense = pointDefense;
            ShortSsm = shortSsm;
            LongSsm = longSsm;
            Guns = guns;
            Speed = speed;
            Hull = hull;
            AirSearchRadar = airSearchRadar;
            SurfaceSearchRadar = surfaceSearchRadar;
            Sonar = sonar;
            AntiSubmarineWarfare = antiSubmarineWarfare;
            EsmEquipped = esmEquipped;
            IsAircraftCarrier = isAircraftCarrier;
            Torpedoes = torpedoes;
            IsSubmarine = isSubmarine;
        }
    }

    public readonly struct DamageApplication
    {
        public int RequestedHits { get; }
        public int AppliedHits { get; }
        public int HullBefore { get; }
        public int HullAfter { get; }
        public ShipDamageLevel PreviousLevel { get; }
        public ShipDamageLevel CurrentLevel { get; }
        public bool CrossedThreshold => PreviousLevel != CurrentLevel;
        public bool SunkNow => PreviousLevel != ShipDamageLevel.Sunk && CurrentLevel == ShipDamageLevel.Sunk;

        public DamageApplication(int requestedHits, int appliedHits, int hullBefore, int hullAfter,
            ShipDamageLevel previousLevel, ShipDamageLevel currentLevel)
        {
            RequestedHits = requestedHits;
            AppliedHits = appliedHits;
            HullBefore = hullBefore;
            HullAfter = hullAfter;
            PreviousLevel = previousLevel;
            CurrentLevel = currentLevel;
        }
    }

    public sealed class UnitState
    {
        public UnitDefinition Definition { get; }
        public int HullDamage { get; private set; }
        public int GunfireHullDamage { get; private set; }
        public int ShortMissilesRemaining { get; private set; }
        public int LongMissilesRemaining { get; private set; }
        public bool IsSunk => HullDamage >= Definition.Hull;
        public int HullRemaining => Math.Max(0, Definition.Hull - HullDamage);
        public int HalfDamageThreshold => HalfDamageThresholdFor(Definition.Hull);
        public int TwoThirdsDamageThreshold => TwoThirdsDamageThresholdFor(Definition.Hull);
        public bool HasHalfDamage => !IsSunk && HullDamage >= HalfDamageThreshold;
        public bool HasTwoThirdsDamage => !IsSunk && HullDamage >= TwoThirdsDamageThreshold;
        public ShipDamageLevel DamageLevel => IsSunk ? ShipDamageLevel.Sunk : HasTwoThirdsDamage
            ? ShipDamageLevel.TwoThirdsDamage : HasHalfDamage
                ? ShipDamageLevel.HalfDamage : ShipDamageLevel.Operational;

        public UnitState(UnitDefinition definition)
        {
            Definition = definition;
            ShortMissilesRemaining = definition.ShortSsm;
            LongMissilesRemaining = definition.LongSsm;
        }

        public int EffectiveSpeed
        {
            get
            {
                if (IsSunk) return 0;
                if (HasTwoThirdsDamage) return 1;
                if (HasHalfDamage) return Math.Max(1, Definition.Speed - 1);
                return Definition.Speed;
            }
        }

        public bool WeaponsDisabled => HasTwoThirdsDamage;
        public int EffectiveLongSam => HasHalfDamage || IsSunk ? 0 : Definition.LongSam;
        public int EffectiveShortSam => HasTwoThirdsDamage || IsSunk ? 0 : Definition.ShortSam;
        public int EffectivePointDefense => HasTwoThirdsDamage || IsSunk ? 0 : Definition.PointDefense;
        public int EffectiveGuns => IsSunk ? 0 : HasTwoThirdsDamage ? (Definition.Guns + 1) / 2 : Definition.Guns;
        public int EffectiveAirSearchRadar => HasHalfDamage || IsSunk ? 0 : Definition.AirSearchRadar;
        public int EffectiveSonar => HasTwoThirdsDamage || IsSunk ? 0 : Definition.Sonar;
        public int EffectiveAntiSubmarineWarfare => HasTwoThirdsDamage || IsSunk
            ? 0 : Definition.AntiSubmarineWarfare;
        public int EffectiveTorpedoes => HasTwoThirdsDamage || IsSunk ? 0 : Definition.Torpedoes;
        public int EffectiveSurfaceSearchRadar => IsSunk ? 0 : Definition.SurfaceSearchRadar;
        public bool EffectiveEsm => !HasTwoThirdsDamage && !IsSunk && Definition.EsmEquipped;
        public bool CanLaunchAircraft => Definition.IsAircraftCarrier && !HasHalfDamage && !IsSunk;
        public int AvailableShortSsm => WeaponsDisabled || IsSunk ? 0 : ShortMissilesRemaining;
        public int AvailableLongSsm => HasHalfDamage || IsSunk ? 0 : LongMissilesRemaining;

        public DamageApplication ApplyDamage(int hits, DamageSource source = DamageSource.Other)
        {
            if (hits < 0) throw new ArgumentOutOfRangeException(nameof(hits));
            var before = HullRemaining;
            var previousLevel = DamageLevel;
            HullDamage = Math.Min(Definition.Hull, HullDamage + hits);
            var applied = before - HullRemaining;
            if (source == DamageSource.Gunfire) GunfireHullDamage += applied;
            return new DamageApplication(hits, applied, before, HullRemaining,
                previousLevel, DamageLevel);
        }

        public static int HalfDamageThresholdFor(int hull)
        {
            if (hull <= 0) throw new ArgumentOutOfRangeException(nameof(hull));
            return (hull + 1) / 2;
        }

        public static int TwoThirdsDamageThresholdFor(int hull)
        {
            if (hull <= 0) throw new ArgumentOutOfRangeException(nameof(hull));
            return (hull * 2 + 2) / 3;
        }

        public int CommitMissiles(int range)
        {
            if (WeaponsDisabled || IsSunk) return 0;
            var factors = 0;
            if (range <= 1)
            {
                factors += ShortMissilesRemaining;
                ShortMissilesRemaining = 0;
            }
            if (range <= 3 && !HasHalfDamage)
            {
                factors += LongMissilesRemaining;
                LongMissilesRemaining = 0;
            }
            return factors;
        }

        public bool TryCommitMissiles(int shortFactors, int longFactors)
        {
            if (shortFactors < 0 || longFactors < 0 || shortFactors > AvailableShortSsm ||
                longFactors > AvailableLongSsm) return false;
            ShortMissilesRemaining -= shortFactors;
            LongMissilesRemaining -= longFactors;
            return true;
        }

        internal void Restore(int hullDamage, int shortMissiles, int longMissiles,
            int gunfireHullDamage = 0)
        {
            HullDamage = Math.Max(0, Math.Min(Definition.Hull, hullDamage));
            GunfireHullDamage = Math.Max(0, Math.Min(HullDamage, gunfireHullDamage));
            ShortMissilesRemaining = Math.Max(0, shortMissiles);
            LongMissilesRemaining = Math.Max(0, longMissiles);
        }
    }

    public sealed class TaskForceState
    {
        private readonly List<UnitState> _units;
        private readonly List<HexCoord> _movementPath = new List<HexCoord>();
        private readonly List<DefensePairData> _defensePairs = new List<DefensePairData>();
        public string Id { get; }
        public Side Side { get; }
        public HexCoord Position { get; private set; }
        public int DeclaredSpeed { get; private set; } = -1;
        public int MovementPointsSpent { get; private set; }
        public int MovementAllowance => DeclaredSpeed < 0 ? 0 : Math.Min(DeclaredSpeed, EffectiveSpeed);
        public int MovementRemaining => Math.Max(0, MovementAllowance - MovementPointsSpent);
        public IReadOnlyList<HexCoord> MovementPath => _movementPath;
        public IReadOnlyList<UnitState> Units => _units;
        public IReadOnlyList<DefensePairData> DefensePairs => _defensePairs;
        public bool HasArrived { get; private set; }
        public int DummyCards { get; private set; }
        public bool IsDummyOnly => _units.Count == 0 && DummyCards > 0;
        public bool IsSubmarineOnly => _units.Count > 0 && _units.All(unit => unit.Definition.IsSubmarine);
        public bool IsSurfaceOnly => _units.Count > 0 && _units.All(unit => !unit.Definition.IsSubmarine);
        public IEnumerable<UnitState> ActiveUnits => _units.Where(unit => !unit.IsSunk);
        public bool IsDestroyed => _units.Count > 0 && _units.All(unit => unit.IsSunk);
        public UnitState Objective => _units.FirstOrDefault(unit => unit.Definition.Role == UnitRole.Objective);
        private bool _radarRadiating;
        public bool RadarRadiating => _radarRadiating && CanRadiateRadar;
        public bool RadarDeclaredThisActivation { get; private set; }
        public bool CanRadiateRadar => ActiveUnits.Any(unit => unit.EffectiveSurfaceSearchRadar > 0);
        public bool CanUseEsm => ActiveUnits.Any(unit => unit.EffectiveEsm);

        public TaskForceState(string id, Side side, HexCoord position, IEnumerable<UnitState> units,
            int dummyCards = 0)
        {
            Id = id;
            Side = side;
            Position = position;
            _units = new List<UnitState>(units);
            if (_units.Any(unit => unit.Definition.IsSubmarine) &&
                _units.Any(unit => !unit.Definition.IsSubmarine))
                throw new InvalidOperationException("Submarines may not be grouped with surface vessels.");
            DummyCards = Math.Max(0, dummyCards);
        }

        public int EffectiveSpeed
        {
            get
            {
                var active = ActiveUnits.ToArray();
                return active.Length == 0 ? (IsDummyOnly ? 3 : 0) : Math.Max(1, active.Min(unit => unit.EffectiveSpeed));
            }
        }

        public void DeclareSpeed(int speed)
        {
            DeclaredSpeed = speed;
            MovementPointsSpent = 0;
            _movementPath.Clear();
            _movementPath.Add(Position);
        }

        public void MoveOneHex(HexCoord destination)
        {
            Position = destination;
            MovementPointsSpent++;
            _movementPath.Add(destination);
        }

        public void ResetActivation()
        {
            DeclaredSpeed = -1;
            MovementPointsSpent = 0;
            _movementPath.Clear();
        }

        public void BeginSensorDeclaration() => RadarDeclaredThisActivation = false;

        public void DeclareRadar(bool enabled)
        {
            _radarRadiating = enabled && CanRadiateRadar;
            RadarDeclaredThisActivation = true;
        }

        public void SpendMovementPointSearching()
        {
            if (MovementRemaining <= 0)
                throw new InvalidOperationException("No movement point remains for another search.");
            MovementPointsSpent++;
            _movementPath.Add(Position);
        }

        public void MoveTo(HexCoord destination) => Position = destination;
        public void MarkArrived()
        {
            HasArrived = true;
            MovementPointsSpent = MovementAllowance;
        }
        public void AddDummyCards(int count) => DummyCards += Math.Max(0, count);
        public bool TryRemoveDummyCards(int count)
        {
            if (count <= 0 || count > DummyCards) return false;
            DummyCards -= count;
            return true;
        }

        public TaskForceState SplitOff(string newId, IEnumerable<string> unitIds)
        {
            var selectedIds = new HashSet<string>(unitIds ?? Array.Empty<string>());
            var selected = _units.Where(unit => selectedIds.Contains(unit.Definition.Id)).ToArray();
            if (selected.Length == 0 || selected.Length >= _units.Count)
                throw new InvalidOperationException("A split must move at least one unit and leave at least one unit behind.");
            foreach (var unit in selected) _units.Remove(unit);
            return new TaskForceState(newId, Side, Position, selected);
        }

        public void SetDefensePairs(IEnumerable<DefensePairData> pairs)
        {
            _defensePairs.Clear();
            _defensePairs.AddRange(pairs ?? Array.Empty<DefensePairData>());
        }

        internal void RestoreMovement(int declaredSpeed, int movementPointsSpent, IEnumerable<HexCoord> path)
        {
            DeclaredSpeed = declaredSpeed;
            MovementPointsSpent = Math.Max(0, movementPointsSpent);
            _movementPath.Clear();
            _movementPath.AddRange(path ?? Array.Empty<HexCoord>());
        }

        internal void RestoreSensors(bool radarRadiating, bool radarDeclared)
        {
            _radarRadiating = radarRadiating;
            RadarDeclaredThisActivation = radarDeclared;
        }

        internal void RestoreArrival(bool arrived) => HasArrived = arrived;
    }

    public sealed class GameState
    {
        private readonly List<TaskForceState> _forces;
        public TaskForceState Player { get; private set; }
        public TaskForceState Enemy { get; private set; }
        public IReadOnlyList<TaskForceState> Forces => _forces;
        public OperationalMap Map { get; }
        public int Turn { get; internal set; } = 1;
        public int MaximumTurns { get; }
        public ActivationPhase Phase { get; internal set; } = ActivationPhase.AwaitingChit;
        public bool PlayerHasMoved { get; internal set; }
        public bool PlayerHasAttacked { get; internal set; }
        public bool PlayerHasSearched { get; internal set; }
        public bool EnemyActivatedFirst { get; internal set; }
        public Side ActiveSide { get; internal set; } = Side.UsNavy;
        public string ActiveFormationId { get; internal set; } = string.Empty;
        public TaskForceState ActiveForce => _forces.FirstOrDefault(force => force.Id == ActiveFormationId);
        public MovementChitCup MovementCup { get; internal set; }
        public bool DetectionRulesEnabled { get; }
        public ScenarioDefinition Scenario { get; }
        public DetectionTracker Detection { get; } = new DetectionTracker();
        public MissileEngagement PendingMissileCombat { get; internal set; }
        public GunEngagement PendingGunCombat { get; internal set; }
        public int Day => ((Turn - 1) / 3) + 1;
        public TimeOfDay TimeOfDay => (TimeOfDay)((Turn - 1) % 3);
        public string TimeLabel => $"Day {Day} " + (TimeOfDay == TimeOfDay.Am ? "AM" :
            TimeOfDay == TimeOfDay.Pm ? "PM" : "Night");
        public bool UsActivated { get; internal set; }
        public bool PlanActivated { get; internal set; }
        public bool IsGameOver { get; internal set; }
        public string Result { get; internal set; } = string.Empty;
        public ScenarioEndReason EndReason { get; internal set; }
        public bool UsRequestedScoring { get; internal set; }
        public bool PlanRequestedScoring { get; internal set; }
        public int Revision { get; internal set; }
        public List<string> Log { get; } = new List<string>();
        public List<RuleTransaction> Transactions { get; } = new List<RuleTransaction>();
        public List<RuleEvent> Events { get; } = new List<RuleEvent>();
        public List<GameCommandData> CommandLog { get; } = new List<GameCommandData>();
        internal GameCommand CurrentCommand { get; set; }

        public GameState(TaskForceState player, TaskForceState enemy, int maximumTurns, OperationalMap map = null,
            bool detectionRulesEnabled = false, ScenarioDefinition scenario = null)
        {
            Player = player;
            Enemy = enemy;
            _forces = new List<TaskForceState> { player, enemy };
            MaximumTurns = maximumTurns;
            Map = map ?? FirstIslandChainMap.Instance;
            DetectionRulesEnabled = detectionRulesEnabled;
            Scenario = scenario;
        }

        public void Trace(string category, string detail)
        {
            Transactions.Add(new RuleTransaction(Transactions.Count + 1, Turn, Phase, category, detail));
            Events.Add(new RuleEvent(Events.Count + 1, Revision, Turn, Phase, EventTypeFor(category),
                CurrentCommand?.Actor ?? ActiveSide, CurrentCommand?.Id, detail));
        }

        public TaskForceState ForceFor(Side side) => ActiveForce != null && ActiveForce.Side == side
            ? ActiveForce : _forces.First(force => force.Side == side);

        public TaskForceState Formation(string id) =>
            _forces.FirstOrDefault(force => string.Equals(force.Id, id, StringComparison.Ordinal));

        public void AddForce(TaskForceState force)
        {
            if (force == null) throw new ArgumentNullException(nameof(force));
            if (_forces.Any(item => item.Id == force.Id))
                throw new InvalidOperationException($"Formation ID '{force.Id}' already exists.");
            _forces.Add(force);
        }

        public void RemoveForce(TaskForceState force)
        {
            if (force == null || force == Player || force == Enemy) return;
            _forces.Remove(force);
        }

        internal void ReplaceForces(IEnumerable<TaskForceState> forces)
        {
            _forces.Clear();
            _forces.AddRange(forces ?? Array.Empty<TaskForceState>());
            Player = _forces.First(force => force.Side == Side.UsNavy);
            Enemy = _forces.First(force => force.Side == Side.Plan);
        }

        public UnitState ObjectiveFor(Side side) => _forces.Where(force => force.Side == side)
            .SelectMany(force => force.Units)
            .First(unit => unit.Definition.Role == UnitRole.Objective);

        public UnitState Unit(string id) => _forces.SelectMany(force => force.Units)
            .FirstOrDefault(unit => string.Equals(unit.Definition.Id, id, StringComparison.Ordinal));

        public SideGameView ViewFor(Side viewer, bool? opponentKnown = null) =>
            new SideGameView(viewer, this, opponentKnown);

        private static RuleEventType EventTypeFor(string category)
        {
            switch (category)
            {
                case "SETUP": return RuleEventType.Setup;
                case "COMMAND": return RuleEventType.CommandAccepted;
                case "REJECTED": return RuleEventType.CommandRejected;
                case "TURN": return RuleEventType.Turn;
                case "ACTIVATION": return RuleEventType.Activation;
                case "MOVEMENT": return RuleEventType.Movement;
                case "SPEED": return RuleEventType.SpeedDeclared;
                case "OPPORTUNITY": return RuleEventType.MovementOpportunity;
                case "CHIT": return RuleEventType.ChitDrawn;
                case "SPLIT": return RuleEventType.FormationSplit;
                case "DIE": return RuleEventType.DieRolled;
                case "AMMUNITION": return RuleEventType.Ammunition;
                case "COMBAT": return RuleEventType.Combat;
                case "DEFENSE": return RuleEventType.Defense;
                case "DAMAGE": return RuleEventType.Damage;
                case "VICTORY": return RuleEventType.Victory;
                case "DETECTION": return RuleEventType.Detection;
                case "NETWORK": return RuleEventType.Network;
                default: return RuleEventType.Information;
            }
        }
    }
}
