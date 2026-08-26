using System;
using System.Collections.Generic;
using System.Linq;

namespace Harpoon.Core
{
    public enum MissileCombatPhase
    {
        AllocateFire,
        DefensiveDeployment,
        LongRangeRemoval,
        ShortRangeDefense,
        CounterattackDecision
    }

    [Serializable]
    public sealed class MissileSalvoData
    {
        public string id;
        public string sourceUnitId;
        public string targetUnitId;
        public int shortFactors;
        public int longFactors;
        public int remainingFactors;
    }

    [Serializable]
    public sealed class MissileEngagementData
    {
        public string attackerFormationId;
        public string defenderFormationId;
        public Side movementOwnerSide;
        public string movementOwnerFormationId;
        public ActivationPhase returnPhase;
        public bool isCounterattack;
        public MissileCombatPhase phase;
        public Side decisionSide;
        public int longRangeHits;
        public MissileSalvoData[] salvos;
        public DefensePairData[] defensePairs;
    }

    public sealed class MissileSalvo
    {
        public string Id { get; }
        public string SourceUnitId { get; }
        public string TargetUnitId { get; }
        public int ShortFactors { get; }
        public int LongFactors { get; }
        public int InitialFactors => ShortFactors + LongFactors;
        public int RemainingFactors { get; private set; }

        public MissileSalvo(string id, string sourceUnitId, string targetUnitId,
            int shortFactors, int longFactors, int? remainingFactors = null)
        {
            Id = id ?? string.Empty;
            SourceUnitId = sourceUnitId ?? string.Empty;
            TargetUnitId = targetUnitId ?? string.Empty;
            ShortFactors = shortFactors;
            LongFactors = longFactors;
            RemainingFactors = remainingFactors ?? InitialFactors;
        }

        public int Remove(int factors)
        {
            var removed = Math.Min(Math.Max(0, factors), RemainingFactors);
            RemainingFactors -= removed;
            return removed;
        }

        public MissileSalvoData ToData() => new MissileSalvoData
        {
            id = Id,
            sourceUnitId = SourceUnitId,
            targetUnitId = TargetUnitId,
            shortFactors = ShortFactors,
            longFactors = LongFactors,
            remainingFactors = RemainingFactors
        };
    }

    public sealed class MissileEngagement
    {
        private readonly List<MissileSalvo> _salvos = new List<MissileSalvo>();
        private readonly List<DefensePairData> _defensePairs = new List<DefensePairData>();
        public string AttackerFormationId { get; }
        public string DefenderFormationId { get; }
        public Side MovementOwnerSide { get; }
        public string MovementOwnerFormationId { get; }
        public ActivationPhase ReturnPhase { get; }
        public bool IsCounterattack { get; }
        public MissileCombatPhase Phase { get; internal set; }
        public Side DecisionSide { get; internal set; }
        public int LongRangeHits { get; internal set; }
        public IReadOnlyList<MissileSalvo> Salvos => _salvos;
        public IReadOnlyList<DefensePairData> DefensePairs => _defensePairs;
        public int InitialFactors => _salvos.Sum(salvo => salvo.InitialFactors);
        public int RemainingFactors => _salvos.Sum(salvo => salvo.RemainingFactors);

        public MissileEngagement(string attackerFormationId, string defenderFormationId,
            Side movementOwnerSide, string movementOwnerFormationId, ActivationPhase returnPhase,
            bool isCounterattack = false)
        {
            AttackerFormationId = attackerFormationId ?? string.Empty;
            DefenderFormationId = defenderFormationId ?? string.Empty;
            MovementOwnerSide = movementOwnerSide;
            MovementOwnerFormationId = movementOwnerFormationId ?? string.Empty;
            ReturnPhase = returnPhase;
            IsCounterattack = isCounterattack;
            Phase = MissileCombatPhase.AllocateFire;
        }

        public void SetSalvos(IEnumerable<MissileSalvo> salvos)
        {
            _salvos.Clear();
            _salvos.AddRange(salvos ?? Array.Empty<MissileSalvo>());
        }

        public void SetDefensePairs(IEnumerable<DefensePairData> pairs)
        {
            _defensePairs.Clear();
            _defensePairs.AddRange(pairs ?? Array.Empty<DefensePairData>());
        }

        public string PairMate(string unitId)
        {
            var pair = _defensePairs.FirstOrDefault(item => item.firstUnitId == unitId || item.secondUnitId == unitId);
            if (pair == null) return string.Empty;
            return pair.firstUnitId == unitId ? pair.secondUnitId : pair.firstUnitId;
        }

        public MissileEngagementData ToData() => new MissileEngagementData
        {
            attackerFormationId = AttackerFormationId,
            defenderFormationId = DefenderFormationId,
            movementOwnerSide = MovementOwnerSide,
            movementOwnerFormationId = MovementOwnerFormationId,
            returnPhase = ReturnPhase,
            isCounterattack = IsCounterattack,
            phase = Phase,
            decisionSide = DecisionSide,
            longRangeHits = LongRangeHits,
            salvos = _salvos.Select(salvo => salvo.ToData()).ToArray(),
            defensePairs = _defensePairs.ToArray()
        };

        public static MissileEngagement FromData(MissileEngagementData data)
        {
            if (data == null) return null;
            var result = new MissileEngagement(data.attackerFormationId, data.defenderFormationId,
                data.movementOwnerSide, data.movementOwnerFormationId, data.returnPhase, data.isCounterattack)
            {
                Phase = data.phase,
                DecisionSide = data.decisionSide,
                LongRangeHits = data.longRangeHits
            };
            result.SetSalvos((data.salvos ?? Array.Empty<MissileSalvoData>()).Select(item =>
                new MissileSalvo(item.id, item.sourceUnitId, item.targetUnitId,
                    item.shortFactors, item.longFactors, item.remainingFactors)));
            result.SetDefensePairs(data.defensePairs);
            return result;
        }
    }

    public sealed class MissileCombatResolver
    {
        private readonly IDieRoller _dice;
        private readonly Action<string, string> _trace;

        public MissileCombatResolver(IDieRoller dice, Action<string, string> trace = null)
        {
            _dice = dice ?? throw new ArgumentNullException(nameof(dice));
            _trace = trace;
        }

        public int RollDefense(string label, int dice, CombatTableColumn column)
            => RollHits(label, dice, column);

        public AttackReport ResolvePointDefenseAndImpacts(MissileEngagement engagement,
            TaskForceState defender)
        {
            var report = new AttackReport
            {
                Fired = true,
                AttackFactors = engagement.InitialFactors
            };
            var interceptedBeforePointDefense = engagement.InitialFactors - engagement.RemainingFactors;
            foreach (var targetGroup in engagement.Salvos.Where(salvo => salvo.RemainingFactors > 0)
                         .GroupBy(salvo => salvo.TargetUnitId).ToArray())
            {
                var target = defender.Units.FirstOrDefault(unit => unit.Definition.Id == targetGroup.Key && !unit.IsSunk);
                if (target == null) continue;
                var salvos = targetGroup.OrderByDescending(salvo => salvo.RemainingFactors).ToArray();
                var beforeDefense = salvos.Sum(salvo => salvo.RemainingFactors);
                var pointDefenseHits = RollHits($"PD {target.Definition.DisplayName}",
                    target.EffectivePointDefense, CombatTableColumn.PointDefense);
                var removedByPd = RemoveFactors(salvos, pointDefenseHits);
                report.InterceptedFactors += removedByPd;
                _trace?.Invoke("DEFENSE", $"{target.Definition.DisplayName} point defense removed " +
                    $"{removedByPd}/{beforeDefense} incoming factor(s).");

                var surviving = salvos.Sum(salvo => salvo.RemainingFactors);
                var hullBefore = target.HullRemaining;
                var hullHits = RollHits($"SSM impact {target.Definition.DisplayName}",
                    surviving, CombatTableColumn.BombsAndSsm);
                var damage = target.ApplyDamage(hullHits);
                report.HullHits += hullHits;
                report.Strikes.Add(new MissileStrikeReport(target.Definition.Id, beforeDefense,
                    removedByPd, surviving, hullHits, damage.CurrentLevel, target.IsSunk));
                report.CausedDamageThreshold |= damage.CrossedThreshold;
                report.SankAnyShip |= damage.SunkNow;
                _trace?.Invoke("DAMAGE", $"{target.Definition.DisplayName}: surviving SSM factors={surviving}, " +
                    $"hull hits={hullHits}, hull {hullBefore}->{target.HullRemaining}, " +
                    $"state {damage.PreviousLevel}->{damage.CurrentLevel}, sunk={target.IsSunk}.");
            }
            report.InterceptedFactors = interceptedBeforePointDefense +
                                        report.Strikes.Sum(strike => strike.PointDefenseIntercepts);
            report.Summary = $"Missile raid: {report.AttackFactors} factor(s), " +
                $"{report.InterceptedFactors} intercepted, {report.HullHits} hull hit(s) across " +
                $"{report.Strikes.Count} target(s).";
            return report;
        }

        public static int RemoveFactors(IEnumerable<MissileSalvo> salvos, int factors)
        {
            var remaining = Math.Max(0, factors);
            var removed = 0;
            foreach (var salvo in (salvos ?? Array.Empty<MissileSalvo>())
                         .OrderByDescending(item => item.RemainingFactors))
            {
                if (remaining == 0) break;
                var amount = salvo.Remove(remaining);
                removed += amount;
                remaining -= amount;
            }
            return removed;
        }

        private int RollHits(string label, int dice, CombatTableColumn column)
        {
            var hits = 0;
            for (var index = 0; index < Math.Max(0, dice); index++)
            {
                var roll = _dice.RollD6();
                var result = CombatTables.Hits(column, roll);
                hits += result;
                _trace?.Invoke("DIE", $"{label}: D6={roll}; {column}=" +
                    (result == 0 ? "M" : result == 1 ? "H" : "2H") + ".");
            }
            return hits;
        }
    }
}
