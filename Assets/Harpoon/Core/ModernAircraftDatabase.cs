using System;
using System.Collections.Generic;
using System.Linq;

namespace Harpoon.Core
{
    public sealed class ModernAircraftDefinition
    {
        public string Id { get; }
        public string DisplayName { get; }
        public Side Side { get; }
        public int AirSearchRadar { get; }
        public int SurfaceSearchRadar { get; }
        public int ShortAsm { get; }
        public int LongAsm { get; }
        public int Sonar { get; }
        public int AntiSubmarineWarfare { get; }
        public int Radius { get; }
        public int Defense { get; }
        public int ServiceableAircraft { get; }
        public string Source { get; }

        internal ModernAircraftDefinition(string id, string displayName, Side side, int airSearchRadar,
            int surfaceSearchRadar, int shortAsm, int longAsm, int sonar, int antiSubmarineWarfare,
            int radius, int defense, int serviceableAircraft, string source)
        {
            Id = id;
            DisplayName = displayName;
            Side = side;
            AirSearchRadar = airSearchRadar;
            SurfaceSearchRadar = surfaceSearchRadar;
            ShortAsm = shortAsm;
            LongAsm = longAsm;
            Sonar = sonar;
            AntiSubmarineWarfare = antiSubmarineWarfare;
            Radius = radius;
            Defense = defense;
            ServiceableAircraft = serviceableAircraft;
            Source = source;
        }

        public UnitDefinition CreateUnit(UnitRole role, string idOverride = null) => new UnitDefinition(
            idOverride ?? Id, DisplayName, Side, role, 0, 0, 0, ShortAsm, LongAsm, 0, 1, 1,
            AirSearchRadar, SurfaceSearchRadar, Sonar, AntiSubmarineWarfare, esmEquipped: true,
            isPatrolAircraft: true, aircraftRadius: Radius, aircraftDefense: Defense,
            serviceableAircraftCapacity: ServiceableAircraft);
    }

    public static class ModernAircraftDatabase
    {
        private static readonly ModernAircraftDefinition[] Definitions =
        {
            new ModernAircraftDefinition("us-e2d", "E-2D Advanced Hawkeye", Side.UsNavy,
                4, 4, 0, 0, 0, 0, 18, 0, 4, "First Island Chain p. 17"),
            new ModernAircraftDefinition("us-p8a", "P-8A Poseidon", Side.UsNavy,
                1, 3, 0, 2, 4, 5, 20, 0, 4, "First Island Chain pp. 17, 23"),
            new ModernAircraftDefinition("us-mq4c", "MQ-4C Triton", Side.UsNavy,
                0, 4, 0, 0, 0, 0, 45, 0, 4, "First Island Chain p. 17"),
            new ModernAircraftDefinition("plan-kj500-600", "KJ-500/600 AEW", Side.Plan,
                4, 4, 0, 0, 0, 0, 14, 0, 4, "First Island Chain p. 20"),
            new ModernAircraftDefinition("plan-y9gx6", "Y-9 GX6 ASW", Side.Plan,
                0, 3, 0, 1, 3, 4, 16, 0, 4, "First Island Chain p. 20")
        };

        public static IReadOnlyList<ModernAircraftDefinition> All => Definitions;

        public static ModernAircraftDefinition Get(string id) => Definitions.FirstOrDefault(item =>
            string.Equals(item.Id, id, StringComparison.Ordinal)) ??
            throw new KeyNotFoundException($"Unknown First Island Chain aircraft '{id}'.");

        public static bool TryGet(string id, out ModernAircraftDefinition aircraft)
        {
            aircraft = Definitions.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.Ordinal));
            return aircraft != null;
        }
    }

    public sealed class InventoryOnlyAircraftDefinition
    {
        public string Id { get; }
        public string DisplayName { get; }
        public Side Side { get; }
        public string Source { get; }

        internal InventoryOnlyAircraftDefinition(string id, string displayName, Side side, string source)
        {
            Id = id;
            DisplayName = displayName;
            Side = side;
            Source = source;
        }
    }

    /// <summary>
    /// Aircraft named by a base or carrier-wing chart but not given a playable stat card.
    /// Keeping these references explicit prevents silent inventory typos without inventing factors.
    /// </summary>
    public static class InventoryOnlyAircraftDatabase
    {
        private static readonly InventoryOnlyAircraftDefinition[] Definitions =
        {
            new InventoryOnlyAircraftDefinition("plan-z20f", "Z-20F", Side.Plan,
                "First Island Chain p. 14 (carrier-air-wing inventory only)")
        };

        public static IReadOnlyList<InventoryOnlyAircraftDefinition> All => Definitions;

        public static bool TryGet(string id, out InventoryOnlyAircraftDefinition aircraft)
        {
            aircraft = Definitions.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.Ordinal));
            return aircraft != null;
        }
    }
}
