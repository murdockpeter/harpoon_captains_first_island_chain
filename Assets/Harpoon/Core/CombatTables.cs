using System;

namespace Harpoon.Core
{
    public enum CombatTableColumn
    {
        Sam,
        PointDefense,
        BombsAndSsm,
        Guns,
        Torpedoes,
        Asw
    }

    public enum AircraftDamageResult
    {
        NoEffect,
        Abort,
        ShotDown
    }

    /// <summary>Authoritative Captain's Edition D6 result tables.</summary>
    public static class CombatTables
    {
        public static int Hits(CombatTableColumn column, int roll)
        {
            RequireD6(roll);
            switch (column)
            {
                case CombatTableColumn.Sam:
                    return roll == 6 ? 2 : roll >= 4 ? 1 : 0;
                case CombatTableColumn.PointDefense:
                    return roll == 6 ? 2 : roll >= 2 ? 1 : 0;
                case CombatTableColumn.BombsAndSsm:
                    return roll == 6 ? 2 : roll >= 3 ? 1 : 0;
                case CombatTableColumn.Guns:
                    return roll >= 4 ? 1 : 0;
                case CombatTableColumn.Torpedoes:
                    return roll == 6 ? 2 : roll >= 3 ? 1 : 0;
                case CombatTableColumn.Asw:
                    return roll == 6 ? 2 : roll >= 4 ? 1 : 0;
                default:
                    throw new ArgumentOutOfRangeException(nameof(column), column, null);
            }
        }

        public static AircraftDamageResult AircraftDamage(int roll)
        {
            RequireD6(roll);
            return roll == 6 ? AircraftDamageResult.ShotDown :
                roll >= 4 ? AircraftDamageResult.Abort : AircraftDamageResult.NoEffect;
        }

        public static int AirToAirHits(int modifiedResult)
        {
            return modifiedResult >= 9 ? 2 : modifiedResult >= 6 ? 1 : 0;
        }

        private static void RequireD6(int roll)
        {
            if (roll < 1 || roll > 6)
                throw new ArgumentOutOfRangeException(nameof(roll), "A D6 result must be from 1 through 6.");
        }
    }
}
