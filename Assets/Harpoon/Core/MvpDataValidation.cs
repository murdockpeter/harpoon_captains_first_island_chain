using System;
using System.Collections.Generic;
using System.Linq;

namespace Harpoon.Core
{
    /// <summary>Cross-file release validation for the First Island Chain MVP data set.</summary>
    public static class MvpDataValidation
    {
        private static readonly string[] ExpectedUsPlatforms =
        {
            "us-ford", "us-nimitz", "us-ticonderoga", "us-burke-iia", "us-burke-iii",
            "us-constellation", "us-independence-lcs", "us-los-angeles", "us-virginia",
            "us-virginia-vpm", "us-seawolf", "us-ohio-ssgn", "us-america-lha",
            "us-san-antonio", "us-fleet-oiler"
        };

        private static readonly string[] ExpectedPlanPlatforms =
        {
            "plan-fujian", "plan-shandong", "plan-liaoning", "plan-type-055", "plan-type-052d",
            "plan-type-054a", "plan-type-054b", "plan-type-056a", "plan-type-093b",
            "plan-type-093a", "plan-type-039ab", "plan-type-075", "plan-type-076",
            "plan-type-071", "plan-type-901", "plan-type-903a"
        };

        private static readonly string[] ExpectedGenericPlatforms =
            { "generic-merchant", "generic-tanker", "generic-amphibious" };

        private static readonly string[] ExpectedUsAircraft =
            { "us-e2d", "us-p8a", "us-mq4c", "us-fa18ef", "us-f35c", "us-ea18g", "us-b1b" };

        private static readonly string[] ExpectedPlanAircraft =
            { "plan-kj500-600", "plan-y9gx6", "plan-j15", "plan-j35", "plan-j16", "plan-h6j" };

        private static readonly string[] ExpectedBases =
        {
            "us-kadena", "us-taipei", "us-subic", "plan-ningbo", "plan-xiamen", "plan-yulin",
            "us-ford-wing", "plan-fujian-wing"
        };

        public static IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();
            ValidatePlatforms(errors);
            ValidateAircraft(errors);
            ValidateBases(errors);
            ValidateScenarios(errors);
            return errors;
        }

        public static void ValidateOrThrow()
        {
            var errors = Validate();
            if (errors.Count > 0)
                throw new InvalidOperationException("MVP DATA VALIDATION FAILED:\n - " +
                    string.Join("\n - ", errors));
        }

        private static void ValidatePlatforms(ICollection<string> errors)
        {
            var all = ModernPlatformDatabase.All;
            RequireUnique(all.Select(item => item.Id), "platform", errors);
            RequireExactSet(all.Where(item => item.DefaultSide == Side.UsNavy).Select(item => item.Id),
                ExpectedUsPlatforms, "US platform cards", errors);
            RequireExactSet(all.Where(item => item.DefaultSide == Side.Plan).Select(item => item.Id),
                ExpectedPlanPlatforms, "PLAN platform cards", errors);
            RequireExactSet(all.Where(item => item.DefaultSide == null).Select(item => item.Id),
                ExpectedGenericPlatforms, "generic auxiliary cards", errors);
            foreach (var item in all)
            {
                RequireText(item.Id, "platform ID", errors);
                RequireText(item.DisplayName, $"{item.Id} display name", errors);
                RequireSource(item.Source, item.Id, errors);
                if (item.Hull < 1 || item.Hull > 6) errors.Add($"{item.Id} hull {item.Hull} is outside 1-6.");
                if (item.Speed < 1 || item.Speed > 3) errors.Add($"{item.Id} speed {item.Speed} is outside 1-3.");
                var factors = new[] { item.AirSearchRadar, item.ShortSam, item.LongSam, item.PointDefense,
                    item.SurfaceSearchRadar, item.ShortSsm, item.LongSsm, item.Guns, item.Torpedoes,
                    item.Sonar, item.AntiSubmarineWarfare };
                if (factors.Any(value => value < 0 || value > 16))
                    errors.Add($"{item.Id} has a combat factor outside 0-16.");
                if (item.LaunchesAircraft != (item.EmbarkedAircraftCapacity > 0))
                    errors.Add($"{item.Id} has inconsistent aircraft-launch capacity.");
            }
        }

        private static void ValidateAircraft(ICollection<string> errors)
        {
            var patrol = ModernAircraftDatabase.All;
            var tactical = ModernTacticalAircraftDatabase.All;
            RequireUnique(patrol.Select(item => item.Id).Concat(tactical.Select(item => item.Id))
                .Concat(InventoryOnlyAircraftDatabase.All.Select(item => item.Id)), "aircraft", errors);
            RequireExactSet(patrol.Where(item => item.Side == Side.UsNavy).Select(item => item.Id)
                    .Concat(tactical.Where(item => item.Side == Side.UsNavy).Select(item => item.Id)),
                ExpectedUsAircraft, "US aircraft cards", errors);
            RequireExactSet(patrol.Where(item => item.Side == Side.Plan).Select(item => item.Id)
                    .Concat(tactical.Where(item => item.Side == Side.Plan).Select(item => item.Id)),
                ExpectedPlanAircraft, "PLAN aircraft cards", errors);
            foreach (var item in patrol)
            {
                RequireText(item.DisplayName, $"{item.Id} display name", errors);
                RequireSource(item.Source, item.Id, errors);
                var factors = new[] { item.AirSearchRadar, item.SurfaceSearchRadar, item.ShortAsm,
                    item.LongAsm, item.Sonar, item.AntiSubmarineWarfare, item.Defense };
                if (factors.Any(value => value < 0 || value > 6))
                    errors.Add($"{item.Id} has an aircraft factor outside 0-6.");
                if (item.Radius < 1 || item.Radius > 90) errors.Add($"{item.Id} has invalid radius {item.Radius}.");
                if (item.ServiceableAircraft < 1 || item.ServiceableAircraft > 4)
                    errors.Add($"{item.Id} has invalid serviceable-aircraft capacity {item.ServiceableAircraft}.");
            }
            foreach (var item in tactical)
            {
                RequireText(item.DisplayName, $"{item.Id} display name", errors);
                RequireSource(item.Source, item.Id, errors);
                var factors = new[] { item.AirSearchRadar, item.SurfaceSearchRadar, item.AirToAir,
                    item.ShortAsm, item.LongAsm, item.Bombs, item.Sonar, item.Defense };
                if (factors.Any(value => value < 0 || value > 6))
                    errors.Add($"{item.Id} has a tactical-air factor outside 0-6.");
                if (item.Radius < 1 || item.Radius > 90) errors.Add($"{item.Id} has invalid radius {item.Radius}.");
            }
            foreach (var item in InventoryOnlyAircraftDatabase.All)
                RequireSource(item.Source, item.Id, errors);
        }

        private static void ValidateBases(ICollection<string> errors)
        {
            var all = ModernAirBaseDatabase.All;
            RequireUnique(all.Select(item => item.Id), "air base/carrier wing", errors);
            RequireExactSet(all.Select(item => item.Id), ExpectedBases, "base and carrier-wing charts", errors);
            if (all.Count(item => !item.IsCarrier) != 6 || all.Count(item => item.IsCarrier) != 2)
                errors.Add("The supplement requires exactly six shore-base and two carrier-wing charts.");
            foreach (var item in all)
            {
                RequireSource(item.Source, item.Id, errors);
                if (item.Inventory.Count == 0) errors.Add($"{item.Id} inventory is empty.");
                if (item.IsCarrier && item.Inventory.Count != item.FlightCapacity)
                    errors.Add($"{item.Id} inventory count does not equal deck capacity.");
                foreach (var aircraftId in item.Inventory)
                {
                    if (!TryAircraftSide(aircraftId, out var side))
                        errors.Add($"{item.Id} references unknown aircraft {aircraftId}.");
                    else if (side != item.Side)
                        errors.Add($"{item.Id} references opposing aircraft {aircraftId}.");
                }
            }
        }

        private static void ValidateScenarios(ICollection<string> errors)
        {
            var all = FirstIslandChainScenarios.Introductory;
            RequireUnique(all.Select(item => item.Id), "scenario", errors);
            RequireExactSet(all.Select(item => item.Id), Enumerable.Range(1, 10).Select(value => $"fic-{value:00}"),
                "introductory scenarios", errors);
            foreach (var scenario in all)
            {
                RequireText(scenario.Name, $"{scenario.Id} name", errors);
                RequireText(scenario.Subtitle, $"{scenario.Id} subtitle", errors);
                RequireText(scenario.Briefing, $"{scenario.Id} briefing", errors);
                RequireText(scenario.VictoryText, $"{scenario.Id} victory text", errors);
                RequireSource(scenario.Source, scenario.Id, errors);
                if (!scenario.Formations.Any(item => item.Side == Side.UsNavy) ||
                    !scenario.Formations.Any(item => item.Side == Side.Plan))
                    errors.Add($"{scenario.Id} must contain formations for both sides.");
                RequireUnique(scenario.Formations.Select(item => item.Id), $"{scenario.Id} formation", errors);
                RequireUnique(scenario.Formations.SelectMany(item => item.Units).Select(item => item.UnitId),
                    $"{scenario.Id} unit", errors);
                var scenarioUnitIds = new HashSet<string>(scenario.Formations.SelectMany(item => item.Units)
                    .Select(item => item.UnitId), StringComparer.Ordinal);
                foreach (var formation in scenario.Formations)
                {
                    if (formation.Units.Count == 0 && formation.DummyCards == 0)
                        errors.Add($"{scenario.Id}/{formation.Id} has no units or dummy cards.");
                    foreach (var unit in formation.Units)
                    {
                        if (ModernPlatformDatabase.TryGet(unit.PlatformId, out var platform))
                        {
                            if (platform.DefaultSide.HasValue && platform.DefaultSide.Value != formation.Side)
                                errors.Add($"{scenario.Id}/{unit.UnitId} uses an opposing platform card.");
                        }
                        else if (ModernAircraftDatabase.TryGet(unit.PlatformId, out var patrolAircraft))
                        {
                            if (patrolAircraft.Side != formation.Side)
                                errors.Add($"{scenario.Id}/{unit.UnitId} uses an opposing aircraft card.");
                        }
                        else
                            errors.Add($"{scenario.Id}/{unit.UnitId} references unknown platform or patrol-aircraft card {unit.PlatformId}.");
                    }
                }
                ValidateOptionalUnitReference(scenario.Id, "US objective", scenario.UsObjectiveUnitId,
                    scenarioUnitIds, errors);
                ValidateOptionalUnitReference(scenario.Id, "PLAN objective", scenario.PlanObjectiveUnitId,
                    scenarioUnitIds, errors);
                ValidateOptionalUnitReference(scenario.Id, "US tie-break", scenario.UsTieBreakUnitId,
                    scenarioUnitIds, errors);
                ValidateOptionalUnitReference(scenario.Id, "PLAN tie-break", scenario.PlanTieBreakUnitId,
                    scenarioUnitIds, errors);
                RequireUnique(scenario.AirBaseIds, $"{scenario.Id} air-base", errors);
                RequireUnique(scenario.TacticalFlights.Select(item => item.Id), $"{scenario.Id} tactical-flight", errors);
                foreach (var baseId in scenario.AirBaseIds)
                    if (!allBase(baseId)) errors.Add($"{scenario.Id} references unknown base {baseId}.");
                foreach (var flight in scenario.TacticalFlights)
                {
                    if (!ModernTacticalAircraftDatabase.TryGet(flight.AircraftId, out var aircraft))
                        errors.Add($"{scenario.Id}/{flight.Id} references unknown tactical aircraft {flight.AircraftId}.");
                    if (!scenario.AirBaseIds.Contains(flight.BaseId))
                        errors.Add($"{scenario.Id}/{flight.Id} base {flight.BaseId} is not active in the scenario.");
                    else if (aircraft != null && ModernAirBaseDatabase.Get(flight.BaseId).Side != aircraft.Side)
                        errors.Add($"{scenario.Id}/{flight.Id} aircraft and base sides disagree.");
                }
                if (!scenario.TacticalAirEnabled && (scenario.AirBaseIds.Count > 0 || scenario.TacticalFlights.Count > 0))
                    errors.Add($"{scenario.Id} has tactical-air data but tactical air is disabled.");
            }

            bool allBase(string id) => ModernAirBaseDatabase.All.Any(item => item.Id == id);
        }

        private static bool TryAircraftSide(string id, out Side side)
        {
            if (ModernAircraftDatabase.TryGet(id, out var patrol)) { side = patrol.Side; return true; }
            if (ModernTacticalAircraftDatabase.TryGet(id, out var tactical)) { side = tactical.Side; return true; }
            if (InventoryOnlyAircraftDatabase.TryGet(id, out var inventoryOnly))
            { side = inventoryOnly.Side; return true; }
            side = default;
            return false;
        }

        private static void ValidateOptionalUnitReference(string scenarioId, string label, string id,
            ISet<string> unitIds, ICollection<string> errors)
        {
            if (!string.IsNullOrWhiteSpace(id) && !unitIds.Contains(id))
                errors.Add($"{scenarioId} {label} references unknown unit {id}.");
        }

        private static void RequireExactSet(IEnumerable<string> actual, IEnumerable<string> expected,
            string label, ICollection<string> errors)
        {
            var actualSet = new HashSet<string>(actual, StringComparer.Ordinal);
            var expectedSet = new HashSet<string>(expected, StringComparer.Ordinal);
            foreach (var missing in expectedSet.Except(actualSet)) errors.Add($"{label} missing {missing}.");
            foreach (var extra in actualSet.Except(expectedSet)) errors.Add($"{label} has unexpected {extra}.");
        }

        private static void RequireUnique(IEnumerable<string> ids, string label, ICollection<string> errors)
        {
            var duplicate = ids.GroupBy(id => id, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
            if (duplicate != null) errors.Add($"Duplicate {label} ID {duplicate.Key}.");
        }

        private static void RequireSource(string source, string id, ICollection<string> errors)
        {
            if (string.IsNullOrWhiteSpace(source) || source.IndexOf("First Island Chain p", StringComparison.Ordinal) < 0)
                errors.Add($"{id} lacks a First Island Chain page reference.");
        }

        private static void RequireText(string value, string label, ICollection<string> errors)
        {
            if (string.IsNullOrWhiteSpace(value)) errors.Add($"{label} is blank.");
        }
    }
}
