using System;
using System.Collections.Generic;
using System.Linq;

namespace Harpoon.Core
{
    public enum Side { UsNavy, Plan }
    public enum UnitRole { Escort, Objective }
    public enum ActivationPhase { DeclareSpeed, PlayerMove, PlayerAction, GameOver }

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

        public UnitDefinition(string id, string displayName, Side side, UnitRole role, int shortSam,
            int longSam, int pointDefense, int shortSsm, int longSsm, int guns, int speed, int hull,
            int airSearchRadar = 0, int surfaceSearchRadar = 0, int sonar = 0,
            int antiSubmarineWarfare = 0)
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
        }
    }

    public sealed class UnitState
    {
        public UnitDefinition Definition { get; }
        public int HullDamage { get; private set; }
        public int ShortMissilesRemaining { get; private set; }
        public int LongMissilesRemaining { get; private set; }
        public bool IsSunk => HullDamage >= Definition.Hull;
        public int HullRemaining => Math.Max(0, Definition.Hull - HullDamage);
        public bool HasHalfDamage => !IsSunk && HullDamage * 2 >= Definition.Hull;
        public bool HasTwoThirdsDamage => !IsSunk && HullDamage * 3 >= Definition.Hull * 2;

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

        public void ApplyDamage(int hits)
        {
            if (hits < 0) throw new ArgumentOutOfRangeException(nameof(hits));
            HullDamage = Math.Min(Definition.Hull, HullDamage + hits);
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

        internal void Restore(int hullDamage, int shortMissiles, int longMissiles)
        {
            HullDamage = Math.Max(0, Math.Min(Definition.Hull, hullDamage));
            ShortMissilesRemaining = Math.Max(0, shortMissiles);
            LongMissilesRemaining = Math.Max(0, longMissiles);
        }
    }

    public sealed class TaskForceState
    {
        private readonly List<UnitState> _units;
        private readonly List<HexCoord> _movementPath = new List<HexCoord>();
        public string Id { get; }
        public Side Side { get; }
        public HexCoord Position { get; private set; }
        public int DeclaredSpeed { get; private set; } = -1;
        public int MovementPointsSpent { get; private set; }
        public int MovementRemaining => DeclaredSpeed < 0 ? 0 : Math.Max(0, DeclaredSpeed - MovementPointsSpent);
        public IReadOnlyList<HexCoord> MovementPath => _movementPath;
        public IReadOnlyList<UnitState> Units => _units;
        public IEnumerable<UnitState> ActiveUnits => _units.Where(unit => !unit.IsSunk);
        public bool IsDestroyed => _units.All(unit => unit.IsSunk);
        public UnitState Objective => _units.First(unit => unit.Definition.Role == UnitRole.Objective);

        public TaskForceState(string id, Side side, HexCoord position, IEnumerable<UnitState> units)
        {
            Id = id;
            Side = side;
            Position = position;
            _units = new List<UnitState>(units);
        }

        public int EffectiveSpeed
        {
            get
            {
                var active = ActiveUnits.ToArray();
                return active.Length == 0 ? 0 : Math.Max(1, active.Min(unit => unit.EffectiveSpeed));
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

        public void MoveTo(HexCoord destination) => Position = destination;

        internal void RestoreMovement(int declaredSpeed, int movementPointsSpent, IEnumerable<HexCoord> path)
        {
            DeclaredSpeed = declaredSpeed;
            MovementPointsSpent = Math.Max(0, movementPointsSpent);
            _movementPath.Clear();
            _movementPath.AddRange(path ?? Array.Empty<HexCoord>());
        }
    }

    public sealed class GameState
    {
        public TaskForceState Player { get; }
        public TaskForceState Enemy { get; }
        public OperationalMap Map { get; }
        public int Turn { get; internal set; } = 1;
        public int MaximumTurns { get; }
        public ActivationPhase Phase { get; internal set; } = ActivationPhase.DeclareSpeed;
        public bool PlayerHasMoved { get; internal set; }
        public bool PlayerHasAttacked { get; internal set; }
        public bool PlayerHasSearched { get; internal set; }
        public bool EnemyActivatedFirst { get; internal set; }
        public Side ActiveSide { get; internal set; } = Side.UsNavy;
        public bool UsActivated { get; internal set; }
        public bool PlanActivated { get; internal set; }
        public bool IsGameOver { get; internal set; }
        public string Result { get; internal set; } = string.Empty;
        public int Revision { get; internal set; }
        public List<string> Log { get; } = new List<string>();
        public List<RuleTransaction> Transactions { get; } = new List<RuleTransaction>();
        public List<RuleEvent> Events { get; } = new List<RuleEvent>();
        public List<GameCommandData> CommandLog { get; } = new List<GameCommandData>();
        internal GameCommand CurrentCommand { get; set; }

        public GameState(TaskForceState player, TaskForceState enemy, int maximumTurns, OperationalMap map = null)
        {
            Player = player;
            Enemy = enemy;
            MaximumTurns = maximumTurns;
            Map = map ?? FirstIslandChainMap.Instance;
        }

        public void Trace(string category, string detail)
        {
            Transactions.Add(new RuleTransaction(Transactions.Count + 1, Turn, Phase, category, detail));
            Events.Add(new RuleEvent(Events.Count + 1, Revision, Turn, Phase, EventTypeFor(category),
                CurrentCommand?.Actor ?? ActiveSide, CurrentCommand?.Id, detail));
        }

        public TaskForceState ForceFor(Side side) => side == Side.UsNavy ? Player : Enemy;

        public SideGameView ViewFor(Side viewer, bool opponentKnown = true) =>
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
