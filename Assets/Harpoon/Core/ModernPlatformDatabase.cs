using System;
using System.Collections.Generic;
using System.Linq;

namespace Harpoon.Core
{
    public enum PlatformDomain { SurfaceShip, Submarine, Auxiliary }

    public sealed class ModernPlatformDefinition
    {
        public string Id { get; }
        public string DisplayName { get; }
        public Side? DefaultSide { get; }
        public PlatformDomain Domain { get; }
        public int AirSearchRadar { get; }
        public int ShortSam { get; }
        public int LongSam { get; }
        public int PointDefense { get; }
        public int SurfaceSearchRadar { get; }
        public int ShortSsm { get; }
        public int LongSsm { get; }
        public int Guns { get; }
        public int Torpedoes { get; }
        public int Sonar { get; }
        public int AntiSubmarineWarfare { get; }
        public int Speed { get; }
        public int Hull { get; }
        public bool LaunchesAircraft { get; }
        public string Source { get; }

        internal ModernPlatformDefinition(string id, string displayName, Side? defaultSide,
            PlatformDomain domain, int hull, int speed, int airSearchRadar, int shortSam,
            int longSam, int pointDefense, int surfaceSearchRadar, int shortSsm, int longSsm,
            int guns, int torpedoes, int sonar, int antiSubmarineWarfare,
            bool launchesAircraft, string source)
        {
            Id = id;
            DisplayName = displayName;
            DefaultSide = defaultSide;
            Domain = domain;
            Hull = hull;
            Speed = speed;
            AirSearchRadar = airSearchRadar;
            ShortSam = shortSam;
            LongSam = longSam;
            PointDefense = pointDefense;
            SurfaceSearchRadar = surfaceSearchRadar;
            ShortSsm = shortSsm;
            LongSsm = longSsm;
            Guns = guns;
            Torpedoes = torpedoes;
            Sonar = sonar;
            AntiSubmarineWarfare = antiSubmarineWarfare;
            LaunchesAircraft = launchesAircraft;
            Source = source;
        }

        public UnitDefinition CreateUnit(Side side, UnitRole role, string idOverride = null,
            string displayNameOverride = null, bool? esmEquippedOverride = null) => new UnitDefinition(idOverride ?? Id,
            displayNameOverride ?? DisplayName, side, role, ShortSam, LongSam, PointDefense,
            ShortSsm, LongSsm, Guns, Speed, Hull, AirSearchRadar, SurfaceSearchRadar, Sonar,
            AntiSubmarineWarfare, esmEquipped: esmEquippedOverride ?? Domain != PlatformDomain.Auxiliary,
            isAircraftCarrier: LaunchesAircraft,
            torpedoes: Torpedoes);
    }

    public static class ModernPlatformDatabase
    {
        private static readonly ModernPlatformDefinition[] Definitions =
        {
            // First Island Chain pp. 15-16: US Navy surface ships and submarines.
            P("us-ford", "Gerald R. Ford", Side.UsNavy, PlatformDomain.SurfaceShip, 6, 3,
                asr: 3, asw: 4, carrier: true, page: 15),
            P("us-nimitz", "Nimitz", Side.UsNavy, PlatformDomain.SurfaceShip, 5, 3,
                asr: 3, asw: 4, carrier: true, page: 15),
            P("us-ticonderoga", "Ticonderoga", Side.UsNavy, PlatformDomain.SurfaceShip, 3, 3,
                asr: 2, shortSam: 4, longSam: 10, pd: 4, ssr: 1, shortSsm: 2,
                longSsm: 2, guns: 2, sonar: 4, asw: 4, page: 15),
            P("us-burke-iia", "Arleigh Burke Flight IIA", Side.UsNavy, PlatformDomain.SurfaceShip, 2, 3,
                asr: 2, shortSam: 3, longSam: 8, pd: 4, ssr: 1, shortSsm: 2,
                longSsm: 1, guns: 2, sonar: 4, asw: 5, page: 15),
            P("us-burke-iii", "Arleigh Burke Flight III", Side.UsNavy, PlatformDomain.SurfaceShip, 2, 3,
                asr: 3, shortSam: 4, longSam: 10, pd: 5, ssr: 1, shortSsm: 2,
                longSsm: 2, guns: 2, sonar: 4, asw: 5, page: 15),
            P("us-constellation", "Constellation", Side.UsNavy, PlatformDomain.SurfaceShip, 1, 2,
                asr: 2, shortSam: 3, longSam: 4, pd: 3, ssr: 1, shortSsm: 4,
                guns: 1, sonar: 5, asw: 4, page: 15),
            P("us-independence-lcs", "Independence LCS", Side.UsNavy, PlatformDomain.SurfaceShip, 1, 3,
                asr: 1, pd: 2, ssr: 1, shortSsm: 2, guns: 1, sonar: 1, asw: 1, page: 15),
            P("us-los-angeles", "Los Angeles (688i)", Side.UsNavy, PlatformDomain.Submarine, 2, 3,
                ssr: 1, shortSsm: 1, longSsm: 4, torpedoes: 4, sonar: 5, asw: 5, page: 15),
            P("us-virginia", "Virginia (Block III/IV)", Side.UsNavy, PlatformDomain.Submarine, 2, 3,
                ssr: 1, shortSsm: 1, longSsm: 4, torpedoes: 4, sonar: 6, asw: 5, page: 16),
            P("us-virginia-vpm", "Virginia (Block V, VPM)", Side.UsNavy, PlatformDomain.Submarine, 3, 3,
                ssr: 1, shortSsm: 1, longSsm: 10, torpedoes: 4, sonar: 6, asw: 5, page: 16),
            P("us-seawolf", "Seawolf", Side.UsNavy, PlatformDomain.Submarine, 3, 3,
                ssr: 1, shortSsm: 1, longSsm: 6, torpedoes: 6, sonar: 6, asw: 5, page: 16),
            P("us-ohio-ssgn", "Ohio SSGN", Side.UsNavy, PlatformDomain.Submarine, 4, 3,
                ssr: 1, longSsm: 16, torpedoes: 3, sonar: 5, asw: 3, page: 16),
            P("us-america-lha", "America-class LHA", Side.UsNavy, PlatformDomain.SurfaceShip, 4, 2,
                asr: 2, pd: 3, ssr: 1, asw: 3, carrier: true, page: 16),
            P("us-san-antonio", "San Antonio LPD", Side.UsNavy, PlatformDomain.SurfaceShip, 3, 2,
                asr: 1, pd: 2, ssr: 1, guns: 1, asw: 1, page: 16),
            P("us-fleet-oiler", "Fleet Oiler (USN)", Side.UsNavy, PlatformDomain.Auxiliary, 4, 2,
                ssr: 1, page: 16),

            // First Island Chain pp. 18-19: PLA Navy surface ships and submarines.
            P("plan-fujian", "Fujian (Type 003)", Side.Plan, PlatformDomain.SurfaceShip, 6, 3,
                asr: 3, asw: 3, carrier: true, page: 18),
            P("plan-shandong", "Shandong (Type 002)", Side.Plan, PlatformDomain.SurfaceShip, 5, 3,
                asr: 2, asw: 3, carrier: true, page: 18),
            P("plan-liaoning", "Liaoning (Type 001)", Side.Plan, PlatformDomain.SurfaceShip, 4, 3,
                asr: 2, asw: 2, carrier: true, page: 18),
            P("plan-type-055", "Type 055 (Renhai)", Side.Plan, PlatformDomain.SurfaceShip, 3, 3,
                asr: 3, shortSam: 4, longSam: 12, pd: 5, ssr: 1, longSsm: 4,
                guns: 3, sonar: 5, asw: 4, page: 18),
            P("plan-type-052d", "Type 052D/DL", Side.Plan, PlatformDomain.SurfaceShip, 2, 3,
                asr: 2, shortSam: 2, longSam: 8, pd: 4, ssr: 1, longSsm: 3,
                guns: 2, sonar: 3, asw: 3, page: 18),
            P("plan-type-054a", "Type 054A Frigate", Side.Plan, PlatformDomain.SurfaceShip, 1, 2,
                asr: 1, shortSam: 3, pd: 3, ssr: 1, shortSsm: 2, guns: 1,
                sonar: 3, asw: 3, page: 18),
            P("plan-type-054b", "Type 054B", Side.Plan, PlatformDomain.SurfaceShip, 2, 3,
                asr: 2, shortSam: 3, pd: 3, ssr: 1, shortSsm: 2, guns: 2,
                sonar: 4, asw: 4, page: 18),
            P("plan-type-056a", "Type 056A (Jiangdao)", Side.Plan, PlatformDomain.SurfaceShip, 1, 2,
                asr: 1, shortSam: 1, pd: 2, ssr: 1, shortSsm: 1, guns: 1,
                sonar: 2, asw: 1, page: 18),
            P("plan-type-093b", "Type 093B", Side.Plan, PlatformDomain.Submarine, 3, 3,
                ssr: 1, longSsm: 3, torpedoes: 5, sonar: 5, asw: 4, page: 19),
            P("plan-type-093a", "Type 093/093A", Side.Plan, PlatformDomain.Submarine, 2, 3,
                ssr: 1, shortSsm: 1, torpedoes: 4, sonar: 4, asw: 3, page: 19),
            P("plan-type-039ab", "Type 039A/B", Side.Plan, PlatformDomain.Submarine, 2, 2,
                ssr: 1, shortSsm: 1, torpedoes: 4, sonar: 4, asw: 2, page: 19),
            P("plan-type-075", "Type 075 (Yushen)", Side.Plan, PlatformDomain.SurfaceShip, 4, 2,
                asr: 1, pd: 3, ssr: 1, asw: 2, carrier: true, page: 19),
            P("plan-type-076", "Type 076 (drone carrier)", Side.Plan, PlatformDomain.SurfaceShip, 4, 2,
                asr: 1, pd: 2, ssr: 1, asw: 1, carrier: true, page: 19),
            P("plan-type-071", "Type 071 LPD", Side.Plan, PlatformDomain.SurfaceShip, 3, 2,
                asr: 1, pd: 2, ssr: 1, guns: 1, asw: 1, page: 19),
            P("plan-type-901", "Type 901 Fuyu AOE", Side.Plan, PlatformDomain.Auxiliary, 4, 3,
                pd: 1, ssr: 1, page: 19),
            P("plan-type-903a", "Type 903A Oiler", Side.Plan, PlatformDomain.Auxiliary, 3, 2,
                ssr: 1, page: 19),

            // First Island Chain p. 21: generic auxiliary cards usable by either side.
            P("generic-merchant", "Merchant Ship", null, PlatformDomain.Auxiliary, 4, 2,
                ssr: 1, page: 21),
            P("generic-tanker", "Tanker", null, PlatformDomain.Auxiliary, 4, 2,
                ssr: 1, page: 21),
            P("generic-amphibious", "Generic Amphibious Ship", null, PlatformDomain.Auxiliary, 3, 3,
                ssr: 1, page: 21)
        };

        public static IReadOnlyList<ModernPlatformDefinition> All => Definitions;

        public static ModernPlatformDefinition Get(string id) => Definitions.FirstOrDefault(item =>
            string.Equals(item.Id, id, StringComparison.Ordinal)) ??
            throw new KeyNotFoundException($"Unknown First Island Chain platform '{id}'.");

        private static ModernPlatformDefinition P(string id, string name, Side? side,
            PlatformDomain domain, int hull, int speed, int asr = 0, int shortSam = 0,
            int longSam = 0, int pd = 0, int ssr = 0, int shortSsm = 0, int longSsm = 0,
            int guns = 0, int torpedoes = 0, int sonar = 0, int asw = 0,
            bool carrier = false, int page = 0) => new ModernPlatformDefinition(id, name, side,
            domain, hull, speed, asr, shortSam, longSam, pd, ssr, shortSsm, longSsm,
            guns, torpedoes, sonar, asw, carrier, $"First Island Chain p. {page}");
    }
}
