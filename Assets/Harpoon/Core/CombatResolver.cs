using System;
using System.Collections.Generic;
using System.Linq;

namespace Harpoon.Core
{
    public interface IDieRoller { int RollD6(); }

    public sealed class SeededDieRoller : IDieRoller, IRandomSource
    {
        private readonly Random _random;
        public SeededDieRoller(int seed) => _random = new Random(seed);
        public int RollD6() => _random.Next(1, 7);
        public int Next(int maximumExclusive) => _random.Next(maximumExclusive);
    }

    public sealed class SequenceDieRoller : IDieRoller, IRandomSource
    {
        private readonly Queue<int> _rolls;
        public SequenceDieRoller(params int[] rolls) => _rolls = new Queue<int>(rolls);
        public int RollD6() => _rolls.Count > 0 ? _rolls.Dequeue() : 1;
        public int Next(int maximumExclusive) => (RollD6() - 1) % maximumExclusive;
    }

    public sealed class AttackReport
    {
        public bool Fired { get; internal set; }
        public int AttackFactors { get; internal set; }
        public int InterceptedFactors { get; internal set; }
        public int HullHits { get; internal set; }
        public string Summary { get; internal set; } = string.Empty;
    }

    public sealed class CombatResolver
    {
        private readonly IDieRoller _dice;
        private readonly Action<string, string> _trace;
        public CombatResolver(IDieRoller dice, Action<string, string> trace = null)
        {
            _dice = dice;
            _trace = trace;
        }

        public AttackReport Attack(TaskForceState attacker, TaskForceState defender)
        {
            var range = attacker.Position.DistanceTo(defender.Position);
            Trace("COMBAT", $"{attacker.Id} attacks {defender.Id}; range={range}.");
            var target = defender.Objective;
            if (target.IsSunk) target = defender.ActiveUnits.FirstOrDefault();
            if (target == null) return NoAttackTraced("No targets remain.");

            var missileFactors = 0;
            foreach (var unit in attacker.ActiveUnits)
            {
                var shortBefore = unit.ShortMissilesRemaining;
                var longBefore = unit.LongMissilesRemaining;
                var committed = unit.CommitMissiles(range);
                missileFactors += committed;
                Trace("AMMUNITION", $"{unit.Definition.DisplayName}: committed={committed}, " +
                      $"SR {shortBefore}->{unit.ShortMissilesRemaining}, LR {longBefore}->{unit.LongMissilesRemaining}.");
            }
            if (missileFactors > 0) return ResolveMissiles(attacker, defender, target, missileFactors, range);
            if (range == 0) return ResolveGuns(attacker, target);
            return NoAttackTraced("No weapons are in range.");
        }

        private AttackReport ResolveMissiles(TaskForceState attacker, TaskForceState defender,
            UnitState target, int missileFactors, int range)
        {
            var surviving = missileFactors;
            var intercepted = 0;
            foreach (var unit in defender.ActiveUnits)
            {
                var removed = ApplyDefenseDice(unit.EffectiveLongSam, ref surviving,
                    roll => CombatTables.Hits(CombatTableColumn.Sam, roll));
                intercepted += removed;
                Trace("DEFENSE", $"{unit.Definition.DisplayName} LR SAM: dice={unit.EffectiveLongSam}, " +
                      $"removed={removed}, missiles remaining={surviving}.");
                if (surviving == 0) break;
            }
            if (surviving > 0)
            {
                var screen = defender.ActiveUnits.FirstOrDefault(unit => unit != target);
                if (screen != null)
                {
                    var removed = ApplyDefenseDice(screen.EffectiveShortSam, ref surviving,
                        roll => CombatTables.Hits(CombatTableColumn.Sam, roll));
                    intercepted += removed;
                    Trace("DEFENSE", $"{screen.Definition.DisplayName} SR SAM: dice={screen.EffectiveShortSam}, " +
                          $"removed={removed}, missiles remaining={surviving}.");
                }
            }
            if (surviving > 0)
            {
                var removed = ApplyDefenseDice(target.EffectivePointDefense, ref surviving,
                    roll => CombatTables.Hits(CombatTableColumn.PointDefense, roll));
                intercepted += removed;
                Trace("DEFENSE", $"{target.Definition.DisplayName} PD: dice={target.EffectivePointDefense}, " +
                      $"removed={removed}, missiles remaining={surviving}.");
            }

            var hullHits = RollHits(surviving,
                roll => CombatTables.Hits(CombatTableColumn.BombsAndSsm, roll));
            var hullBefore = target.HullRemaining;
            target.ApplyDamage(hullHits);
            Trace("DAMAGE", $"{target.Definition.DisplayName}: hits={hullHits}, hull {hullBefore}->{target.HullRemaining}, " +
                  $"sunk={target.IsSunk}.");
            return new AttackReport
            {
                Fired = true,
                AttackFactors = missileFactors,
                InterceptedFactors = intercepted,
                HullHits = hullHits,
                Summary = $"{attacker.Side} fired {missileFactors} missile factor(s) at {range} hex(es): " +
                          $"{intercepted} intercepted, {hullHits} hull hit(s) on {target.Definition.DisplayName}."
            };
        }

        private AttackReport ResolveGuns(TaskForceState attacker, UnitState target)
        {
            var gunFactors = attacker.ActiveUnits.Sum(unit => unit.EffectiveGuns);
            if (gunFactors == 0) return NoAttackTraced("No guns remain operational.");
            var hullHits = RollHits(gunFactors, roll => CombatTables.Hits(CombatTableColumn.Guns, roll));
            var hullBefore = target.HullRemaining;
            target.ApplyDamage(hullHits);
            Trace("DAMAGE", $"{target.Definition.DisplayName}: gun hits={hullHits}, hull " +
                  $"{hullBefore}->{target.HullRemaining}, sunk={target.IsSunk}.");
            return new AttackReport
            {
                Fired = true,
                AttackFactors = gunFactors,
                HullHits = hullHits,
                Summary = $"{attacker.Side} fired {gunFactors} gun factor(s): " +
                          $"{hullHits} hull hit(s) on {target.Definition.DisplayName}."
            };
        }

        private int ApplyDefenseDice(int dice, ref int surviving, Func<int, int> table)
        {
            var hits = Math.Min(surviving, RollHits(dice, table));
            surviving -= hits;
            return hits;
        }

        private int RollHits(int dice, Func<int, int> table)
        {
            var hits = 0;
            for (var i = 0; i < dice; i++)
            {
                var roll = _dice.RollD6();
                var result = table(roll);
                hits += result;
                Trace("DIE", $"D6={roll}; table result={result} hit(s).");
            }
            return hits;
        }

        private static AttackReport NoAttack(string summary) => new AttackReport { Summary = summary };
        private AttackReport NoAttackTraced(string summary)
        {
            Trace("COMBAT", summary);
            return NoAttack(summary);
        }
        private void Trace(string category, string detail) => _trace?.Invoke(category, detail);
    }
}
