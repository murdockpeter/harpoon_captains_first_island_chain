using System;
using System.Collections.Generic;
using System.Linq;

namespace Harpoon.Core
{
    public static class GunCombatRules
    {
        public static bool InitialEngagementSucceeds(int attackerSpeed, int defenderSpeed, int roll)
        {
            if (defenderSpeed > attackerSpeed) return false;
            if (defenderSpeed < attackerSpeed) return true;
            return roll >= 1 && roll <= 3;
        }

        public static int BreakOffThreshold(int leavingSpeed, int opposingSpeed)
        {
            if (leavingSpeed > opposingSpeed) return 6;
            return leavingSpeed == opposingSpeed ? 2 : 1;
        }

        public static bool BreakOffSucceeds(int leavingSpeed, int opposingSpeed, int roll) =>
            roll >= 1 && roll <= BreakOffThreshold(leavingSpeed, opposingSpeed);
    }

    public enum GunCombatPhase
    {
        EngageDecision,
        ArrangeAttacker,
        ArrangeDefender,
        Firing,
        BreakOffAttacker,
        BreakOffDefender
    }

    [Serializable]
    public sealed class GunEngagementData
    {
        public string attackerFormationId;
        public string defenderFormationId;
        public Side movementOwnerSide;
        public string movementOwnerFormationId;
        public ActivationPhase returnPhase;
        public GunCombatPhase phase;
        public Side decisionSide;
        public int round;
        public GunPairData[] attackerPairs;
        public GunPairData[] defenderPairs;
        public string[] firingOrder;
        public int firingIndex;
        public int attackerBreakOff;
        public int defenderBreakOff;
    }

    public sealed class GunEngagement
    {
        private readonly List<GunPairData> _attackerPairs = new List<GunPairData>();
        private readonly List<GunPairData> _defenderPairs = new List<GunPairData>();
        private readonly List<string> _firingOrder = new List<string>();
        public string AttackerFormationId { get; }
        public string DefenderFormationId { get; }
        public Side MovementOwnerSide { get; }
        public string MovementOwnerFormationId { get; }
        public ActivationPhase ReturnPhase { get; }
        public GunCombatPhase Phase { get; internal set; }
        public Side DecisionSide { get; internal set; }
        public int Round { get; internal set; } = 1;
        public IReadOnlyList<GunPairData> AttackerPairs => _attackerPairs;
        public IReadOnlyList<GunPairData> DefenderPairs => _defenderPairs;
        public IReadOnlyList<string> FiringOrder => _firingOrder;
        public int FiringIndex { get; internal set; }
        public int AttackerBreakOff { get; internal set; } = -1;
        public int DefenderBreakOff { get; internal set; } = -1;

        public GunEngagement(string attackerFormationId, string defenderFormationId,
            Side movementOwnerSide, string movementOwnerFormationId, ActivationPhase returnPhase)
        {
            AttackerFormationId = attackerFormationId ?? string.Empty;
            DefenderFormationId = defenderFormationId ?? string.Empty;
            MovementOwnerSide = movementOwnerSide;
            MovementOwnerFormationId = movementOwnerFormationId ?? string.Empty;
            ReturnPhase = returnPhase;
            Phase = GunCombatPhase.EngageDecision;
        }

        public void SetPairs(Side side, IEnumerable<GunPairData> pairs, Side attackerSide)
        {
            var target = side == attackerSide ? _attackerPairs : _defenderPairs;
            target.Clear();
            target.AddRange(pairs ?? Array.Empty<GunPairData>());
        }

        public IReadOnlyList<GunPairData> PairsFor(Side side, Side attackerSide) =>
            side == attackerSide ? _attackerPairs : _defenderPairs;

        public void SetFiringOrder(IEnumerable<string> unitIds)
        {
            _firingOrder.Clear();
            _firingOrder.AddRange(unitIds ?? Array.Empty<string>());
            FiringIndex = 0;
        }

        public bool IsScreened(string unitId) => _attackerPairs.Concat(_defenderPairs)
            .Any(pair => pair.screenedUnitId == unitId);

        public GunEngagementData ToData() => new GunEngagementData
        {
            attackerFormationId = AttackerFormationId,
            defenderFormationId = DefenderFormationId,
            movementOwnerSide = MovementOwnerSide,
            movementOwnerFormationId = MovementOwnerFormationId,
            returnPhase = ReturnPhase,
            phase = Phase,
            decisionSide = DecisionSide,
            round = Round,
            attackerPairs = _attackerPairs.ToArray(),
            defenderPairs = _defenderPairs.ToArray(),
            firingOrder = _firingOrder.ToArray(),
            firingIndex = FiringIndex,
            attackerBreakOff = AttackerBreakOff,
            defenderBreakOff = DefenderBreakOff
        };

        public static GunEngagement FromData(GunEngagementData data)
        {
            if (data == null) return null;
            var result = new GunEngagement(data.attackerFormationId, data.defenderFormationId,
                data.movementOwnerSide, data.movementOwnerFormationId, data.returnPhase)
            {
                Phase = data.phase,
                DecisionSide = data.decisionSide,
                Round = Math.Max(1, data.round),
                FiringIndex = data.firingIndex,
                AttackerBreakOff = data.attackerBreakOff,
                DefenderBreakOff = data.defenderBreakOff
            };
            result._attackerPairs.AddRange(data.attackerPairs ?? Array.Empty<GunPairData>());
            result._defenderPairs.AddRange(data.defenderPairs ?? Array.Empty<GunPairData>());
            result._firingOrder.AddRange(data.firingOrder ?? Array.Empty<string>());
            return result;
        }
    }

    public sealed class GunCombatResolver
    {
        private readonly IDieRoller _dice;
        private readonly Action<string, string> _trace;
        public GunCombatResolver(IDieRoller dice, Action<string, string> trace = null)
        {
            _dice = dice ?? throw new ArgumentNullException(nameof(dice));
            _trace = trace;
        }

        public AttackReport Fire(UnitState firingShip, UnitState target, bool screened)
        {
            var factors = firingShip.EffectiveGuns;
            var hits = 0;
            for (var index = 0; index < factors; index++)
            {
                var raw = _dice.RollD6();
                var modified = Math.Max(0, raw - (screened ? 1 : 0));
                var result = modified == 0 ? 0 : CombatTables.Hits(CombatTableColumn.Guns, modified);
                hits += result;
                _trace?.Invoke("DIE", $"{firingShip.Definition.DisplayName} guns: D6={raw}" +
                    (screened ? $" -1 screened={modified}" : string.Empty) +
                    $"; Guns={(result == 0 ? "M" : result == 1 ? "H" : "2H")}.");
            }
            var before = target.HullRemaining;
            var damage = target.ApplyDamage(hits, DamageSource.Gunfire);
            _trace?.Invoke("DAMAGE", $"{target.Definition.DisplayName}: gun hits={hits}, hull " +
                $"{before}->{target.HullRemaining}, state {damage.PreviousLevel}->{damage.CurrentLevel}, " +
                $"sunk={target.IsSunk}.");
            return new AttackReport
            {
                Fired = true,
                IsGunfire = true,
                SourceUnitId = firingShip.Definition.Id,
                TargetUnitId = target.Definition.Id,
                TargetWasScreened = screened,
                CausedDamageThreshold = damage.CrossedThreshold,
                SankAnyShip = damage.SunkNow,
                AttackFactors = factors,
                HullHits = hits,
                Summary = $"{firingShip.Definition.DisplayName} fired {factors} gun factor(s) at " +
                    $"{target.Definition.DisplayName}{(screened ? " through its screen (-1)" : string.Empty)}: " +
                    $"{hits} hull hit(s)."
            };
        }
    }
}
