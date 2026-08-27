using System;
using System.Collections.Generic;
using System.Linq;

namespace Harpoon.Core
{
    public enum TacticalAirMission { Ready, Cap, DeckInterceptor, Escort, Flown, Aborted, Destroyed }
    public enum TacticalWeapon { LongAsm, ShortAsm, Bombs }

    public sealed class TacticalAircraftDefinition
    {
        public string Id { get; }
        public string DisplayName { get; }
        public Side Side { get; }
        public bool CarrierCapable { get; }
        public int AirSearchRadar { get; }
        public int SurfaceSearchRadar { get; }
        public int AirToAir { get; }
        public int ShortAsm { get; }
        public int LongAsm { get; }
        public int Bombs { get; }
        public int Sonar { get; }
        public int Radius { get; }
        public int Defense { get; }
        public bool ElectronicAttack { get; }
        public string Source { get; }

        public TacticalAircraftDefinition(string id, string displayName, Side side, bool carrierCapable,
            int airSearchRadar, int surfaceSearchRadar, int airToAir, int shortAsm, int longAsm,
            int bombs, int sonar, int radius, int defense, bool electronicAttack, string source)
        {
            Id = id ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Side = side;
            CarrierCapable = carrierCapable;
            AirSearchRadar = Math.Max(0, airSearchRadar);
            SurfaceSearchRadar = Math.Max(0, surfaceSearchRadar);
            AirToAir = Math.Max(0, airToAir);
            ShortAsm = Math.Max(0, shortAsm);
            LongAsm = Math.Max(0, longAsm);
            Bombs = Math.Max(0, bombs);
            Sonar = Math.Max(0, sonar);
            Radius = Math.Max(0, radius);
            Defense = Math.Max(0, defense);
            ElectronicAttack = electronicAttack;
            Source = source ?? string.Empty;
        }
    }

    public static class ModernTacticalAircraftDatabase
    {
        private static readonly TacticalAircraftDefinition[] Definitions =
        {
            new TacticalAircraftDefinition("us-fa18ef", "F/A-18E/F Super Hornet", Side.UsNavy, true,
                2, 2, 3, 2, 1, 2, 0, 7, 4, false, "First Island Chain p. 17"),
            new TacticalAircraftDefinition("us-f35c", "F-35C Lightning II", Side.UsNavy, true,
                2, 1, 3, 1, 2, 2, 0, 10, 5, false, "First Island Chain p. 17"),
            new TacticalAircraftDefinition("us-ea18g", "EA-18G Growler", Side.UsNavy, true,
                2, 2, 1, 1, 0, 0, 0, 7, 3, true, "First Island Chain pp. 11, 17"),
            new TacticalAircraftDefinition("us-b1b", "B-1B Lancer", Side.UsNavy, false,
                0, 2, 0, 0, 6, 4, 0, 90, 2, false, "First Island Chain p. 17"),
            new TacticalAircraftDefinition("plan-j15", "J-15 / J-15T", Side.Plan, true,
                2, 2, 2, 2, 1, 2, 0, 6, 3, false, "First Island Chain p. 20"),
            new TacticalAircraftDefinition("plan-j35", "J-35", Side.Plan, true,
                2, 1, 3, 1, 2, 1, 0, 5, 5, false, "First Island Chain p. 20"),
            new TacticalAircraftDefinition("plan-j16", "J-16", Side.Plan, false,
                2, 2, 3, 1, 2, 2, 0, 8, 4, false, "First Island Chain p. 20"),
            new TacticalAircraftDefinition("plan-h6j", "H-6J / H-6N", Side.Plan, false,
                0, 2, 0, 0, 5, 3, 0, 60, 1, false, "First Island Chain p. 20")
        };

        public static IReadOnlyList<TacticalAircraftDefinition> All => Definitions;
        public static TacticalAircraftDefinition Get(string id) => Definitions.FirstOrDefault(item =>
            string.Equals(item.Id, id, StringComparison.Ordinal)) ??
            throw new KeyNotFoundException($"Unknown tactical aircraft '{id}'.");

        public static bool TryGet(string id, out TacticalAircraftDefinition aircraft)
        {
            aircraft = Definitions.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.Ordinal));
            return aircraft != null;
        }
    }

    public sealed class AirBaseDefinition
    {
        public string Id { get; }
        public string DisplayName { get; }
        public Side Side { get; }
        public HexCoord Position { get; }
        public int AirSearchRadar { get; }
        public int ShortSam { get; }
        public int LongSam { get; }
        public int PointDefense { get; }
        public int RunwayCapacity { get; }
        public IReadOnlyList<string> Inventory { get; }
        public bool IsCarrier { get; }
        public int FlightCapacity { get; }
        public string Source { get; }

        public AirBaseDefinition(string id, string displayName, Side side, HexCoord position,
            int airSearchRadar, int shortSam, int longSam, int pointDefense,
            IEnumerable<string> inventory, bool isCarrier = false, int flightCapacity = 0,
            int runwayCapacity = 6, string source = "First Island Chain pp. 13-14, 23-24")
        {
            Id = id ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Side = side;
            Position = position;
            AirSearchRadar = Math.Max(0, airSearchRadar);
            ShortSam = Math.Max(0, shortSam);
            LongSam = Math.Max(0, longSam);
            PointDefense = Math.Max(0, pointDefense);
            Inventory = (inventory ?? Array.Empty<string>()).ToArray();
            IsCarrier = isCarrier;
            FlightCapacity = isCarrier ? Math.Max(1, flightCapacity) : int.MaxValue;
            RunwayCapacity = isCarrier ? 0 : Math.Max(1, runwayCapacity);
            Source = source ?? string.Empty;
        }
    }

    public static class ModernAirBaseDatabase
    {
        private static readonly AirBaseDefinition[] Definitions =
        {
            new AirBaseDefinition("us-kadena", "Kadena AB", Side.UsNavy, new HexCoord(9, 4), 3, 8, 8, 4,
                new[] { "us-f35c", "us-f35c", "us-fa18ef", "us-fa18ef", "us-fa18ef", "us-fa18ef", "us-ea18g", "us-p8a", "us-p8a" }),
            new AirBaseDefinition("us-taipei", "Taipei / Zuoying", Side.UsNavy, new HexCoord(8, 10), 2, 6, 6, 3,
                new[] { "us-fa18ef", "us-fa18ef", "us-fa18ef", "us-fa18ef", "us-f35c", "us-f35c" }),
            new AirBaseDefinition("us-subic", "Subic Bay / Clark", Side.UsNavy, new HexCoord(7, 16), 2, 4, 4, 3,
                new[] { "us-fa18ef", "us-fa18ef", "us-fa18ef", "us-fa18ef", "us-p8a" }),
            new AirBaseDefinition("plan-ningbo", "Ningbo-Zhoushan", Side.Plan, new HexCoord(2, 8), 3, 10, 10, 4,
                new[] { "plan-j16", "plan-j16", "plan-j16", "plan-j16", "plan-h6j", "plan-h6j", "plan-kj500-600" }),
            new AirBaseDefinition("plan-xiamen", "Xiamen", Side.Plan, new HexCoord(2, 10), 2, 6, 6, 3,
                new[] { "plan-j16", "plan-j16", "plan-j16", "plan-j16", "plan-j15", "plan-j15" }),
            new AirBaseDefinition("plan-yulin", "Yulin / Sanya", Side.Plan, new HexCoord(2, 18), 2, 8, 4, 3,
                new[] { "plan-j16", "plan-j16", "plan-j16", "plan-j16", "plan-y9gx6" }),
            new AirBaseDefinition("us-ford-wing", "Gerald R. Ford Carrier Air Wing", Side.UsNavy, default,
                0, 0, 0, 0, new[] { "us-fa18ef", "us-fa18ef", "us-fa18ef", "us-fa18ef", "us-fa18ef", "us-fa18ef",
                    "us-f35c", "us-f35c", "us-f35c", "us-f35c", "us-ea18g", "us-ea18g", "us-e2d", "us-mq4c" }, true, 14),
            new AirBaseDefinition("plan-fujian-wing", "Fujian Carrier Air Wing", Side.Plan, default,
                0, 0, 0, 0, new[] { "plan-j15", "plan-j15", "plan-j15", "plan-j15",
                    "plan-j35", "plan-j35", "plan-j35", "plan-j35", "plan-kj500-600", "plan-z20f", "plan-z20f" }, true, 11)
        };

        public static IReadOnlyList<AirBaseDefinition> All => Definitions;
        public static AirBaseDefinition Get(string id) => Definitions.FirstOrDefault(item =>
            string.Equals(item.Id, id, StringComparison.Ordinal)) ??
            throw new KeyNotFoundException($"Unknown air base or carrier wing '{id}'.");
    }

    [Serializable]
    public sealed class TacticalFlightSnapshot
    {
        public string id;
        public string aircraftId;
        public Side side;
        public string baseId;
        public int aircraftRemaining;
        public int readyAircraft;
        public int flownAircraft;
        public int abortedAircraft;
        public TacticalAirMission mission;
        public bool radarOn;
    }

    public sealed class TacticalFlightState
    {
        public string Id { get; }
        public TacticalAircraftDefinition Definition { get; }
        public Side Side => Definition.Side;
        public string BaseId { get; }
        public int AircraftRemaining { get; private set; }
        public int ReadyAircraft { get; private set; }
        public int FlownAircraft { get; private set; }
        public int AbortedAircraft { get; private set; }
        public int DestroyedAircraft => 4 - AircraftRemaining;
        public TacticalAirMission Mission { get; private set; }
        public bool RadarOn { get; private set; }
        public bool IsFighter => Definition.AirToAir > 0;

        public TacticalFlightState(string id, TacticalAircraftDefinition definition, string baseId, int strength = 4)
        {
            Id = id ?? string.Empty;
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            BaseId = baseId ?? string.Empty;
            AircraftRemaining = Math.Max(1, Math.Min(4, strength));
            ReadyAircraft = AircraftRemaining;
            Mission = TacticalAirMission.Ready;
        }

        public bool AssignDefensiveMission(TacticalAirMission mission, bool radarOn)
        {
            if (!IsFighter || ReadyAircraft != 4 || AircraftRemaining != 4 ||
                (mission != TacticalAirMission.Cap && mission != TacticalAirMission.DeckInterceptor)) return false;
            Mission = mission;
            RadarOn = radarOn;
            return true;
        }

        public int Launch(int requested, TacticalAirMission mission = TacticalAirMission.Flown)
        {
            if (Mission != TacticalAirMission.Ready && Mission != TacticalAirMission.Escort) return 0;
            var launched = Math.Min(Math.Max(0, requested), ReadyAircraft);
            ReadyAircraft -= launched;
            FlownAircraft += launched;
            Mission = mission;
            return launched;
        }

        public void MarkInterceptorUsed()
        {
            if (Mission != TacticalAirMission.DeckInterceptor) return;
            FlownAircraft += ReadyAircraft;
            ReadyAircraft = 0;
            Mission = TacticalAirMission.Flown;
        }

        public void ApplyAirDamage(int shotDown, int aborted)
        {
            var defensiveMission = Mission == TacticalAirMission.Cap || Mission == TacticalAirMission.DeckInterceptor;
            shotDown = Math.Min(Math.Max(0, shotDown), AircraftRemaining);
            aborted = Math.Min(Math.Max(0, aborted), AircraftRemaining - shotDown);
            AircraftRemaining -= shotDown;
            ReadyAircraft = defensiveMission
                ? Math.Max(0, Math.Min(AircraftRemaining, ReadyAircraft - shotDown - aborted))
                : Math.Min(ReadyAircraft, AircraftRemaining);
            AbortedAircraft += Math.Min(aborted, AircraftRemaining - AbortedAircraft);
            if (AircraftRemaining == 0) Mission = TacticalAirMission.Destroyed;
            else if (defensiveMission && ReadyAircraft > 0) { }
            else if (aborted > 0) Mission = TacticalAirMission.Aborted;
        }

        public void ReturnAborted(int aircraft)
        {
            AbortedAircraft += Math.Max(0, aircraft);
            if (AircraftRemaining > 0) Mission = TacticalAirMission.Aborted;
        }

        public void BeginTurn()
        {
            ReadyAircraft = AircraftRemaining;
            FlownAircraft = 0;
            AbortedAircraft = 0;
            RadarOn = false;
            Mission = AircraftRemaining == 0 ? TacticalAirMission.Destroyed : TacticalAirMission.Ready;
        }

        public TacticalFlightSnapshot Capture() => new TacticalFlightSnapshot
        {
            id = Id,
            aircraftId = Definition.Id,
            side = Side,
            baseId = BaseId,
            aircraftRemaining = AircraftRemaining,
            readyAircraft = ReadyAircraft,
            flownAircraft = FlownAircraft,
            abortedAircraft = AbortedAircraft,
            mission = Mission,
            radarOn = RadarOn
        };

        public static TacticalFlightState Restore(TacticalFlightSnapshot data)
        {
            var result = new TacticalFlightState(data.id, ModernTacticalAircraftDatabase.Get(data.aircraftId),
                data.baseId, Math.Max(1, data.aircraftRemaining));
            result.AircraftRemaining = Math.Max(0, Math.Min(4, data.aircraftRemaining));
            result.ReadyAircraft = Math.Max(0, Math.Min(result.AircraftRemaining, data.readyAircraft));
            result.FlownAircraft = Math.Max(0, data.flownAircraft);
            result.AbortedAircraft = Math.Max(0, data.abortedAircraft);
            result.Mission = result.AircraftRemaining == 0 ? TacticalAirMission.Destroyed : data.mission;
            result.RadarOn = data.radarOn;
            return result;
        }
    }

    [Serializable]
    public sealed class AirBaseSnapshot
    {
        public string id;
        public int runwayHits;
    }

    public sealed class AirBaseState
    {
        public AirBaseDefinition Definition { get; }
        public int RunwayHits { get; private set; }
        public bool CanLaunch => Definition.IsCarrier || RunwayHits < Definition.RunwayCapacity;
        public bool IsDegraded => !Definition.IsCarrier && RunwayHits >= (Definition.RunwayCapacity + 1) / 2;
        public int MaximumStrikeSize => !CanLaunch ? 0 : IsDegraded ? 2 : 4;

        public AirBaseState(AirBaseDefinition definition) => Definition = definition;
        public int ApplyRunwayHits(int hits)
        {
            var before = RunwayHits;
            RunwayHits = Math.Min(Definition.RunwayCapacity, RunwayHits + Math.Max(0, hits));
            return RunwayHits - before;
        }
        public void Restore(int hits) => RunwayHits = Math.Max(0, Math.Min(Definition.RunwayCapacity, hits));
    }

    public sealed class TacticalStrikeReport
    {
        public bool Launched { get; internal set; }
        public string FlightId { get; internal set; } = string.Empty;
        public string TargetId { get; internal set; } = string.Empty;
        public TacticalWeapon Weapon { get; internal set; }
        public int AircraftLaunched { get; internal set; }
        public int AircraftAborted { get; internal set; }
        public int AircraftShotDown { get; internal set; }
        public int MissileFactors { get; internal set; }
        public int MissileFactorsIntercepted { get; internal set; }
        public int HullHits { get; internal set; }
        public int RunwayHits { get; internal set; }
        public string Summary { get; internal set; } = string.Empty;
    }

    public static class TacticalAirTables
    {
        public static int AirToAirHits(int modifiedRoll) => modifiedRoll <= 2 ? 0 : modifiedRoll <= 7 ? 1 : 2;
        public static int BombHits(int roll) => CombatTables.Hits(CombatTableColumn.BombsAndSsm, roll);
    }
}
