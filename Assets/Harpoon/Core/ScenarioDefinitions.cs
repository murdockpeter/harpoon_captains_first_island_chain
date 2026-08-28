using System;
using System.Collections.Generic;
using System.Linq;

namespace Harpoon.Core
{
    public enum ScenarioScoringMode
    {
        ObjectiveThenEscort,
        TotalHullHits,
        GunfireHullHits,
        ConvoyArrival,
        SubmarineSurvival,
        ConvoySurvival,
        CarrierEscape,
        SubmarineEscape,
        CarrierPosition
    }

    public enum BoardEdge { None, East, West }

    public sealed class ScenarioUnitDefinition
    {
        public string UnitId { get; }
        public string PlatformId { get; }
        public UnitRole Role { get; }

        public ScenarioUnitDefinition(string unitId, string platformId, UnitRole role)
        {
            UnitId = unitId ?? string.Empty;
            PlatformId = platformId ?? string.Empty;
            Role = role;
        }
    }

    public sealed class ScenarioFormationDefinition
    {
        public string Id { get; }
        public Side Side { get; }
        public HexCoord Start { get; }
        public IReadOnlyList<ScenarioUnitDefinition> Units { get; }
        public int DummyCards { get; }
        public BoardEdge EntryEdge { get; }
        public bool CanDeploy { get; }

        public ScenarioFormationDefinition(string id, Side side, HexCoord start,
            IEnumerable<ScenarioUnitDefinition> units, int dummyCards = 0,
            BoardEdge entryEdge = BoardEdge.None, bool canDeploy = true)
        {
            Id = id ?? string.Empty;
            Side = side;
            Start = start;
            Units = (units ?? Array.Empty<ScenarioUnitDefinition>()).ToArray();
            DummyCards = Math.Max(0, dummyCards);
            EntryEdge = entryEdge;
            CanDeploy = canDeploy;
        }
    }

    public sealed class ScenarioTacticalFlightDefinition
    {
        public string Id { get; }
        public string AircraftId { get; }
        public string BaseId { get; }
        public int Strength { get; }

        public ScenarioTacticalFlightDefinition(string id, string aircraftId, string baseId, int strength = 4)
        {
            Id = id ?? string.Empty;
            AircraftId = aircraftId ?? string.Empty;
            BaseId = baseId ?? string.Empty;
            Strength = Math.Max(1, Math.Min(4, strength));
        }
    }

    public sealed class ScenarioDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public string Subtitle { get; }
        public string Briefing { get; }
        public string VictoryText { get; }
        public string Source { get; }
        public int MaximumTurns { get; }
        public bool DetectionRulesEnabled { get; }
        public string UsObjectiveUnitId { get; }
        public string PlanObjectiveUnitId { get; }
        public string UsTieBreakUnitId { get; }
        public string PlanTieBreakUnitId { get; }
        public ScenarioScoringMode ScoringMode { get; }
        public bool HasUsDestination { get; }
        public HexCoord UsDestination { get; }
        public int PlanDeploymentMinimumDistance { get; }
        public bool HasDeploymentZones { get; }
        public HexCoord DeploymentCenter { get; }
        public int UsDeploymentRadius { get; }
        public int PlanProhibitedRadius { get; }
        public bool HasPatrolLine { get; }
        public HexCoord PatrolLineStart { get; }
        public HexCoord PatrolLineEnd { get; }
        public int PatrolLineRadius { get; }
        public Side PatrolRestrictedSide { get; }
        public BoardEdge VictoryExitEdge { get; }
        public bool HasDistanceDeployment { get; }
        public HexCoord DistanceDeploymentCenter { get; }
        public int UsDeploymentMinimumDistance { get; }
        public int PlanDeploymentMaximumDistance { get; }
        public bool TacticalAirEnabled { get; }
        public bool Ea18gSensorReductionEnabled { get; }
        public HexCoord CarrierObjectiveHex { get; }
        public int CarrierObjectiveRadius { get; }
        public IReadOnlyList<string> AirBaseIds { get; }
        public IReadOnlyList<ScenarioTacticalFlightDefinition> TacticalFlights { get; }
        public IReadOnlyList<ScenarioFormationDefinition> Formations { get; }

        public ScenarioDefinition(string id, string name, string subtitle, string briefing,
            string victoryText, string source, int maximumTurns, bool detectionRulesEnabled,
            string usObjectiveUnitId, string planObjectiveUnitId, string usTieBreakUnitId,
            string planTieBreakUnitId, IEnumerable<ScenarioFormationDefinition> formations,
            ScenarioScoringMode scoringMode = ScenarioScoringMode.ObjectiveThenEscort,
            bool hasUsDestination = false, HexCoord usDestination = default,
            int planDeploymentMinimumDistance = 0, bool hasDeploymentZones = false,
            HexCoord deploymentCenter = default, int usDeploymentRadius = 0,
            int planProhibitedRadius = 0, bool hasPatrolLine = false,
            HexCoord patrolLineStart = default, HexCoord patrolLineEnd = default,
            int patrolLineRadius = 0, Side patrolRestrictedSide = Side.Plan,
            BoardEdge victoryExitEdge = BoardEdge.None, bool hasDistanceDeployment = false,
            HexCoord distanceDeploymentCenter = default, int usDeploymentMinimumDistance = 0,
            int planDeploymentMaximumDistance = 0, bool tacticalAirEnabled = false,
            bool ea18gSensorReductionEnabled = false, HexCoord carrierObjectiveHex = default,
            int carrierObjectiveRadius = 0, IEnumerable<string> airBaseIds = null,
            IEnumerable<ScenarioTacticalFlightDefinition> tacticalFlights = null)
        {
            Id = id ?? string.Empty;
            Name = name ?? string.Empty;
            Subtitle = subtitle ?? string.Empty;
            Briefing = briefing ?? string.Empty;
            VictoryText = victoryText ?? string.Empty;
            Source = source ?? string.Empty;
            MaximumTurns = Math.Max(0, maximumTurns);
            DetectionRulesEnabled = detectionRulesEnabled;
            UsObjectiveUnitId = usObjectiveUnitId ?? string.Empty;
            PlanObjectiveUnitId = planObjectiveUnitId ?? string.Empty;
            UsTieBreakUnitId = usTieBreakUnitId ?? string.Empty;
            PlanTieBreakUnitId = planTieBreakUnitId ?? string.Empty;
            ScoringMode = scoringMode;
            HasUsDestination = hasUsDestination;
            UsDestination = usDestination;
            PlanDeploymentMinimumDistance = Math.Max(0, planDeploymentMinimumDistance);
            HasDeploymentZones = hasDeploymentZones;
            DeploymentCenter = deploymentCenter;
            UsDeploymentRadius = Math.Max(0, usDeploymentRadius);
            PlanProhibitedRadius = Math.Max(0, planProhibitedRadius);
            HasPatrolLine = hasPatrolLine;
            PatrolLineStart = patrolLineStart;
            PatrolLineEnd = patrolLineEnd;
            PatrolLineRadius = Math.Max(0, patrolLineRadius);
            PatrolRestrictedSide = patrolRestrictedSide;
            VictoryExitEdge = victoryExitEdge;
            HasDistanceDeployment = hasDistanceDeployment;
            DistanceDeploymentCenter = distanceDeploymentCenter;
            UsDeploymentMinimumDistance = Math.Max(0, usDeploymentMinimumDistance);
            PlanDeploymentMaximumDistance = Math.Max(0, planDeploymentMaximumDistance);
            TacticalAirEnabled = tacticalAirEnabled;
            Ea18gSensorReductionEnabled = ea18gSensorReductionEnabled;
            CarrierObjectiveHex = carrierObjectiveHex;
            CarrierObjectiveRadius = Math.Max(0, carrierObjectiveRadius);
            AirBaseIds = (airBaseIds ?? Array.Empty<string>()).ToArray();
            TacticalFlights = (tacticalFlights ?? Array.Empty<ScenarioTacticalFlightDefinition>()).ToArray();
            Formations = (formations ?? Array.Empty<ScenarioFormationDefinition>()).ToArray();
        }
    }

    public static class FirstIslandChainScenarios
    {
        public static readonly ScenarioDefinition ContactOffBashiChannel = new ScenarioDefinition(
            "fic-01", "Contact off the Bashi Channel", "FIRST ISLAND CHAIN · SCENARIO 1",
            "An Arleigh Burke Flight IIA escorts a merchant toward Subic Bay and encounters " +
            "a PLAN Type 054A screening a Type 071 LPD. The formations begin three hexes apart.",
            "Whichever side inflicts the most damage on the opposing merchant/amphibious ship wins.",
            "First Island Chain p. 25; platform cards pp. 15, 18-19, 21", 0, false,
            "us-merchant", "plan-type-071", "us-burke-iia", "plan-type-054a",
            new[]
            {
                new ScenarioFormationDefinition("US Task Force", Side.UsNavy, new HexCoord(7, 13),
                    new[]
                    {
                        new ScenarioUnitDefinition("us-burke-iia", "us-burke-iia", UnitRole.Escort),
                        new ScenarioUnitDefinition("us-merchant", "generic-merchant", UnitRole.Objective)
                    }),
                new ScenarioFormationDefinition("PLAN Task Force", Side.Plan, new HexCoord(10, 10),
                    new[]
                    {
                        new ScenarioUnitDefinition("plan-type-054a", "plan-type-054a", UnitRole.Escort),
                        new ScenarioUnitDefinition("plan-type-071", "plan-type-071", UnitRole.Objective)
                    })
            });

        public static readonly ScenarioDefinition FlagshipDuel = new ScenarioDefinition(
            "fic-02", "Flagship Duel", "FIRST ISLAND CHAIN · SCENARIO 2",
            "A PLAN Type 055 Renhai attacks a heavily defended US formation built around a " +
            "San Antonio LPD, two Arleigh Burke Flight IIA destroyers, and one Ticonderoga cruiser.",
            "Whichever side inflicts the most hull hits on the opposing warships wins.",
            "First Island Chain p. 25; platform cards pp. 15-16, 18", 0, false,
            "us-san-antonio", "plan-type-055", string.Empty, string.Empty,
            new[]
            {
                new ScenarioFormationDefinition("US Flagship Group", Side.UsNavy, new HexCoord(12, 13),
                    new[]
                    {
                        new ScenarioUnitDefinition("us-burke-iia-1", "us-burke-iia", UnitRole.Escort),
                        new ScenarioUnitDefinition("us-burke-iia-2", "us-burke-iia", UnitRole.Escort),
                        new ScenarioUnitDefinition("us-ticonderoga", "us-ticonderoga", UnitRole.Escort),
                        new ScenarioUnitDefinition("us-san-antonio", "us-san-antonio", UnitRole.Objective)
                    }),
                new ScenarioFormationDefinition("PLAN Renhai Group", Side.Plan, new HexCoord(5, 10),
                    new[]
                    {
                        new ScenarioUnitDefinition("plan-type-055", "plan-type-055", UnitRole.Objective)
                    })
            }, ScenarioScoringMode.TotalHullHits);

        public static readonly ScenarioDefinition CloseAboard = new ScenarioDefinition(
            "fic-03", "Close Aboard", "FIRST ISLAND CHAIN · SCENARIO 3",
            "Three PLAN Type 056A corvettes scouting the Bashi Channel encounter an Arleigh Burke " +
            "Flight IIA and a Constellation-class frigate. Both sides close for a gun action.",
            "Whichever side inflicts the most hull hits with gunfire wins.",
            "First Island Chain p. 25; platform cards pp. 15, 18", 0, false,
            "us-burke-iia", "plan-type-056a-1", string.Empty, string.Empty,
            new[]
            {
                new ScenarioFormationDefinition("US Close Action Group", Side.UsNavy, new HexCoord(13, 13),
                    new[]
                    {
                        new ScenarioUnitDefinition("us-burke-iia", "us-burke-iia", UnitRole.Objective),
                        new ScenarioUnitDefinition("us-constellation", "us-constellation", UnitRole.Escort)
                    }),
                new ScenarioFormationDefinition("PLAN Corvette Group", Side.Plan, new HexCoord(10, 10),
                    new[]
                    {
                        new ScenarioUnitDefinition("plan-type-056a-1", "plan-type-056a", UnitRole.Objective),
                        new ScenarioUnitDefinition("plan-type-056a-2", "plan-type-056a", UnitRole.Escort),
                        new ScenarioUnitDefinition("plan-type-056a-3", "plan-type-056a", UnitRole.Escort)
                    })
            }, ScenarioScoringMode.GunfireHullHits);

        public static readonly ScenarioDefinition PicketLine = new ScenarioDefinition(
            "fic-04", "Picket Line", "FIRST ISLAND CHAIN · SCENARIO 4",
            "A US convoy must move from Subic Bay to Taipei / Zuoying while finding, avoiding, " +
            "or fighting through a concealed PLAN picket force.",
            "The US wins by bringing at least one merchant to Taipei / Zuoying or destroying the listed PLAN force. " +
            "PLAN wins by sinking both merchants before either arrives.",
            "First Island Chain p. 25; platform cards pp. 15, 18, 21", 0, true,
            "us-merchant-1", "plan-type-052d", string.Empty, string.Empty,
            new[]
            {
                new ScenarioFormationDefinition("US Subic Convoy", Side.UsNavy, new HexCoord(8, 16),
                    new[]
                    {
                        new ScenarioUnitDefinition("us-burke-iia", "us-burke-iia", UnitRole.Escort),
                        new ScenarioUnitDefinition("us-constellation-1", "us-constellation", UnitRole.Escort),
                        new ScenarioUnitDefinition("us-constellation-2", "us-constellation", UnitRole.Escort),
                        new ScenarioUnitDefinition("us-merchant-1", "generic-merchant", UnitRole.Objective),
                        new ScenarioUnitDefinition("us-merchant-2", "generic-merchant", UnitRole.Objective)
                    }),
                new ScenarioFormationDefinition("PLAN Picket Group", Side.Plan, new HexCoord(15, 10),
                    new[]
                    {
                        new ScenarioUnitDefinition("plan-type-054a-1", "plan-type-054a", UnitRole.Escort),
                        new ScenarioUnitDefinition("plan-type-054a-2", "plan-type-054a", UnitRole.Escort),
                        new ScenarioUnitDefinition("plan-type-052d", "plan-type-052d", UnitRole.Objective)
                    })
            }, ScenarioScoringMode.ConvoyArrival, true, new HexCoord(8, 10), 4);

        public static readonly ScenarioDefinition GhostFleet = new ScenarioDefinition(
            "fic-05", "Ghost Fleet", "FIRST ISLAND CHAIN · SCENARIO 5",
            "Picket Line is replayed with three US and five PLAN dummy cards, allowing both sides " +
            "to create false task forces and transfer deception assets.",
            "The US wins by bringing at least one merchant to Taipei / Zuoying or destroying the listed PLAN force. " +
            "PLAN wins by sinking both merchants before either arrives.",
            "First Island Chain p. 25; dummy cards p. 22; Captain's Rules p. 9", 0, true,
            "us-merchant-1", "plan-type-052d", string.Empty, string.Empty,
            new[]
            {
                new ScenarioFormationDefinition("US Subic Convoy", Side.UsNavy, new HexCoord(8, 16),
                    PicketLine.Formations.First(item => item.Side == Side.UsNavy).Units),
                new ScenarioFormationDefinition("US Dummy Group", Side.UsNavy, new HexCoord(8, 16),
                    Array.Empty<ScenarioUnitDefinition>(), 3),
                new ScenarioFormationDefinition("PLAN Picket Group", Side.Plan, new HexCoord(15, 10),
                    PicketLine.Formations.First(item => item.Side == Side.Plan).Units),
                new ScenarioFormationDefinition("PLAN Dummy Group", Side.Plan, new HexCoord(15, 10),
                    Array.Empty<ScenarioUnitDefinition>(), 5)
            }, ScenarioScoringMode.ConvoyArrival, true, new HexCoord(8, 10), 4);

        public static readonly ScenarioDefinition WolvesOfBashiChannel = new ScenarioDefinition(
            "fic-06", "Wolves of the Bashi Channel", "FIRST ISLAND CHAIN · SCENARIO 6",
            "A PLAN wolf pack patrols the Bashi Channel while a US hunter-killer surface group and " +
            "Los Angeles-class submarine enter from the east to clear the strait.",
            "After seven turns PLAN wins if its adjusted losses leave at least two submarines alive; " +
            "every two US ships sunk offsets one PLAN submarine loss. Otherwise the US wins.",
            "First Island Chain p. 25; Captain's Rules pp. 10-11", 7, true,
            string.Empty, string.Empty, string.Empty, string.Empty,
            new[]
            {
                new ScenarioFormationDefinition("US Hunter-Killer Group", Side.UsNavy, new HexCoord(15, 12),
                    new[]
                    {
                        new ScenarioUnitDefinition("us-burke-iii", "us-burke-iii", UnitRole.Escort),
                        new ScenarioUnitDefinition("us-constellation-1", "us-constellation", UnitRole.Escort),
                        new ScenarioUnitDefinition("us-constellation-2", "us-constellation", UnitRole.Escort)
                    }),
                new ScenarioFormationDefinition("US Los Angeles", Side.UsNavy, new HexCoord(15, 14),
                    new[] { new ScenarioUnitDefinition("us-los-angeles", "us-los-angeles", UnitRole.Escort) }),
                new ScenarioFormationDefinition("PLAN Yuan 1", Side.Plan, new HexCoord(9, 12),
                    new[] { new ScenarioUnitDefinition("plan-type-039ab-1", "plan-type-039ab", UnitRole.Escort) }),
                new ScenarioFormationDefinition("PLAN Yuan 2", Side.Plan, new HexCoord(10, 12),
                    new[] { new ScenarioUnitDefinition("plan-type-039ab-2", "plan-type-039ab", UnitRole.Escort) }),
                new ScenarioFormationDefinition("PLAN Type 093B", Side.Plan, new HexCoord(12, 12),
                    new[] { new ScenarioUnitDefinition("plan-type-093b", "plan-type-093b", UnitRole.Escort) })
            }, ScenarioScoringMode.SubmarineSurvival, hasPatrolLine: true,
            patrolLineStart: new HexCoord(8, 12), patrolLineEnd: new HexCoord(12, 12),
            patrolLineRadius: 2, patrolRestrictedSide: Side.Plan);

        public static readonly ScenarioDefinition LifelineToTaiwan = new ScenarioDefinition(
            "fic-07", "Lifeline to Taiwan", "FIRST ISLAND CHAIN · SCENARIO 7",
            "Four independent US convoy elements must cross the submarine screen and reach Taipei / Zuoying. " +
            "PLAN deploys three submarines outside the convoy assembly area.",
            "After ten turns the US wins if three merchant ships survive and arrive. Each PLAN submarine sunk " +
            "offsets one merchant loss; otherwise PLAN wins.",
            "First Island Chain p. 25; Captain's Rules pp. 10-11", 10, true,
            string.Empty, string.Empty, string.Empty, string.Empty,
            new[]
            {
                new ScenarioFormationDefinition("US Convoy Alpha", Side.UsNavy, new HexCoord(9, 10),
                    new[]
                    {
                        new ScenarioUnitDefinition("us-constellation-1", "us-constellation", UnitRole.Escort),
                        new ScenarioUnitDefinition("us-merchant-1", "generic-merchant", UnitRole.Objective)
                    }),
                new ScenarioFormationDefinition("US Convoy Bravo", Side.UsNavy, new HexCoord(9, 11),
                    new[]
                    {
                        new ScenarioUnitDefinition("us-constellation-2", "us-constellation", UnitRole.Escort),
                        new ScenarioUnitDefinition("us-merchant-2", "generic-merchant", UnitRole.Objective)
                    }),
                new ScenarioFormationDefinition("US Convoy Charlie", Side.UsNavy, new HexCoord(10, 10),
                    new[]
                    {
                        new ScenarioUnitDefinition("us-burke-iia", "us-burke-iia", UnitRole.Escort),
                        new ScenarioUnitDefinition("us-merchant-3", "generic-merchant", UnitRole.Objective)
                    }),
                new ScenarioFormationDefinition("US Replenishment Group", Side.UsNavy, new HexCoord(10, 11),
                    new[] { new ScenarioUnitDefinition("us-tanker", "generic-tanker", UnitRole.Escort) }),
                new ScenarioFormationDefinition("PLAN Yuan 1", Side.Plan, new HexCoord(14, 14),
                    new[] { new ScenarioUnitDefinition("plan-type-039ab-1", "plan-type-039ab", UnitRole.Escort) }),
                new ScenarioFormationDefinition("PLAN Yuan 2", Side.Plan, new HexCoord(15, 12),
                    new[] { new ScenarioUnitDefinition("plan-type-039ab-2", "plan-type-039ab", UnitRole.Escort) }),
                new ScenarioFormationDefinition("PLAN Type 093B", Side.Plan, new HexCoord(13, 18),
                    new[] { new ScenarioUnitDefinition("plan-type-093b", "plan-type-093b", UnitRole.Escort) })
            }, ScenarioScoringMode.ConvoySurvival, true, new HexCoord(8, 10), 0,
            true, new HexCoord(9, 10), 2, 2);

        public static readonly ScenarioDefinition HuntTheDragon = new ScenarioDefinition(
            "fic-08", "Hunt the Dragon", "FIRST ISLAND CHAIN · SCENARIO 8",
            "A barrier of US submarines in the Bashi Channel must stop the PLAN carrier battle group " +
            "built around Fujian from reaching the western edge.",
            "The US wins by sinking Fujian. PLAN wins if Fujian exits the west edge while still capable " +
            "of launching aircraft. If neither side fulfills its condition after seven turns, the game is a draw.",
            "First Island Chain p. 26; platform cards pp. 15-16, 18", 7, true,
            string.Empty, "plan-fujian", string.Empty, string.Empty,
            new[]
            {
                new ScenarioFormationDefinition("US Virginia 1", Side.UsNavy, new HexCoord(15, 8),
                    new[] { new ScenarioUnitDefinition("us-virginia-1", "us-virginia", UnitRole.Escort) },
                    entryEdge: BoardEdge.East),
                new ScenarioFormationDefinition("US Virginia 2", Side.UsNavy, new HexCoord(15, 11),
                    new[] { new ScenarioUnitDefinition("us-virginia-2", "us-virginia", UnitRole.Escort) },
                    entryEdge: BoardEdge.East),
                new ScenarioFormationDefinition("US Los Angeles 1", Side.UsNavy, new HexCoord(15, 14),
                    new[] { new ScenarioUnitDefinition("us-los-angeles-1", "us-los-angeles", UnitRole.Escort) },
                    entryEdge: BoardEdge.East),
                new ScenarioFormationDefinition("US Los Angeles 2", Side.UsNavy, new HexCoord(15, 17),
                    new[] { new ScenarioUnitDefinition("us-los-angeles-2", "us-los-angeles", UnitRole.Escort) },
                    entryEdge: BoardEdge.East),
                new ScenarioFormationDefinition("PLAN Fujian", Side.Plan, new HexCoord(9, 12),
                    new[] { new ScenarioUnitDefinition("plan-fujian", "plan-fujian", UnitRole.Objective) }),
                new ScenarioFormationDefinition("PLAN Type 055", Side.Plan, new HexCoord(10, 12),
                    new[] { new ScenarioUnitDefinition("plan-type-055", "plan-type-055", UnitRole.Escort) }),
                new ScenarioFormationDefinition("PLAN Type 052D", Side.Plan, new HexCoord(11, 12),
                    new[] { new ScenarioUnitDefinition("plan-type-052d", "plan-type-052d", UnitRole.Escort) }),
                new ScenarioFormationDefinition("PLAN Type 054B", Side.Plan, new HexCoord(12, 12),
                    new[] { new ScenarioUnitDefinition("plan-type-054b", "plan-type-054b", UnitRole.Escort) })
            }, ScenarioScoringMode.CarrierEscape, hasPatrolLine: true,
            patrolLineStart: new HexCoord(8, 12), patrolLineEnd: new HexCoord(12, 12),
            patrolLineRadius: 2, patrolRestrictedSide: Side.Plan,
            victoryExitEdge: BoardEdge.West);

        public static readonly ScenarioDefinition Patroller = new ScenarioDefinition(
            "fic-09", "Patroller", "FIRST ISLAND CHAIN · SCENARIO 9",
            "Four PLAN submarines departing Xiamen must cross the Philippine Sea while a Kadena-based " +
            "P-8A Poseidon and a Los Angeles-class SSN hunt them.",
            "PLAN wins by exiting three or more submarines from the east edge within fifteen turns. " +
            "If fewer than three escape, the US wins.",
            "First Island Chain pp. 17, 23, 26; Captain's Rules pp. 12-13", 15, true,
            string.Empty, string.Empty, string.Empty, string.Empty,
            new[]
            {
                new ScenarioFormationDefinition("US Los Angeles", Side.UsNavy, new HexCoord(15, 20),
                    new[] { new ScenarioUnitDefinition("us-los-angeles", "us-los-angeles", UnitRole.Escort) }),
                new ScenarioFormationDefinition("US P-8A Poseidon", Side.UsNavy, new HexCoord(9, 4),
                    new[] { new ScenarioUnitDefinition("us-p8a", "us-p8a", UnitRole.Escort) }, canDeploy: false),
                new ScenarioFormationDefinition("PLAN Yuan 1", Side.Plan, new HexCoord(5, 10),
                    new[] { new ScenarioUnitDefinition("plan-type-039ab-1", "plan-type-039ab", UnitRole.Escort) }),
                new ScenarioFormationDefinition("PLAN Yuan 2", Side.Plan, new HexCoord(6, 9),
                    new[] { new ScenarioUnitDefinition("plan-type-039ab-2", "plan-type-039ab", UnitRole.Escort) }),
                new ScenarioFormationDefinition("PLAN Yuan 3", Side.Plan, new HexCoord(6, 11),
                    new[] { new ScenarioUnitDefinition("plan-type-039ab-3", "plan-type-039ab", UnitRole.Escort) }),
                new ScenarioFormationDefinition("PLAN Type 093B", Side.Plan, new HexCoord(7, 10),
                    new[] { new ScenarioUnitDefinition("plan-type-093b", "plan-type-093b", UnitRole.Escort) })
            }, ScenarioScoringMode.SubmarineEscape, victoryExitEdge: BoardEdge.East,
            hasDistanceDeployment: true, distanceDeploymentCenter: new HexCoord(2, 10),
            usDeploymentMinimumDistance: 15, planDeploymentMaximumDistance: 10);

        public static readonly ScenarioDefinition FirstLight = new ScenarioDefinition(
            "fic-10", "First Light Over the East China Sea", "FIRST ISLAND CHAIN · SCENARIO 10",
            "A Gerald R. Ford carrier strike group must push from Kadena into the East China Sea while " +
            "PLAN submarines and Ningbo-based maritime strike aircraft attempt to stop it.",
            "The US wins by moving Ford, still capable of launching aircraft, within two hexes of 0206 " +
            "by the end of Turn 12. Otherwise PLAN wins.",
            "First Island Chain pp. 13-15, 17, 19-20, 26; Captain's Rules pp. 14-16", 12, true,
            "us-ford", string.Empty, string.Empty, string.Empty,
            new[]
            {
                new ScenarioFormationDefinition("US Ford Strike Group", Side.UsNavy, new HexCoord(10, 4),
                    new[]
                    {
                        new ScenarioUnitDefinition("us-ford", "us-ford", UnitRole.Objective),
                        new ScenarioUnitDefinition("us-ticonderoga", "us-ticonderoga", UnitRole.Escort),
                        new ScenarioUnitDefinition("us-burke-flt-iii", "us-burke-iii", UnitRole.Escort),
                        new ScenarioUnitDefinition("us-constellation-1", "us-constellation", UnitRole.Escort),
                        new ScenarioUnitDefinition("us-constellation-2", "us-constellation", UnitRole.Escort)
                    }, canDeploy: false),
                new ScenarioFormationDefinition("PLAN Type 093B 1", Side.Plan, new HexCoord(4, 8),
                    new[] { new ScenarioUnitDefinition("plan-type-093b-first-light-1", "plan-type-093b", UnitRole.Escort) }),
                new ScenarioFormationDefinition("PLAN Type 093B 2", Side.Plan, new HexCoord(5, 9),
                    new[] { new ScenarioUnitDefinition("plan-type-093b-first-light-2", "plan-type-093b", UnitRole.Escort) })
            }, ScenarioScoringMode.CarrierPosition, hasDistanceDeployment: true,
            distanceDeploymentCenter: new HexCoord(2, 8), planDeploymentMaximumDistance: 10,
            tacticalAirEnabled: true, ea18gSensorReductionEnabled: true,
            carrierObjectiveHex: new HexCoord(2, 6), carrierObjectiveRadius: 2,
            airBaseIds: new[] { "us-ford-wing", "plan-ningbo" },
            tacticalFlights: new[]
            {
                new ScenarioTacticalFlightDefinition("FORD-F18-1", "us-fa18ef", "us-ford-wing"),
                new ScenarioTacticalFlightDefinition("FORD-F18-2", "us-fa18ef", "us-ford-wing"),
                new ScenarioTacticalFlightDefinition("FORD-F18-3", "us-fa18ef", "us-ford-wing"),
                new ScenarioTacticalFlightDefinition("FORD-F18-4", "us-fa18ef", "us-ford-wing"),
                new ScenarioTacticalFlightDefinition("FORD-F18-5", "us-fa18ef", "us-ford-wing"),
                new ScenarioTacticalFlightDefinition("FORD-F18-6", "us-fa18ef", "us-ford-wing"),
                new ScenarioTacticalFlightDefinition("FORD-F35-1", "us-f35c", "us-ford-wing"),
                new ScenarioTacticalFlightDefinition("FORD-F35-2", "us-f35c", "us-ford-wing"),
                new ScenarioTacticalFlightDefinition("FORD-F35-3", "us-f35c", "us-ford-wing"),
                new ScenarioTacticalFlightDefinition("FORD-F35-4", "us-f35c", "us-ford-wing"),
                new ScenarioTacticalFlightDefinition("FORD-EA18G-1", "us-ea18g", "us-ford-wing"),
                new ScenarioTacticalFlightDefinition("FORD-EA18G-2", "us-ea18g", "us-ford-wing"),
                new ScenarioTacticalFlightDefinition("NINGBO-J16-1", "plan-j16", "plan-ningbo"),
                new ScenarioTacticalFlightDefinition("NINGBO-J16-2", "plan-j16", "plan-ningbo"),
                new ScenarioTacticalFlightDefinition("NINGBO-H6J-1", "plan-h6j", "plan-ningbo")
            });

        public static IReadOnlyList<ScenarioDefinition> Introductory { get; } =
            new[] { ContactOffBashiChannel, FlagshipDuel, CloseAboard, PicketLine, GhostFleet,
                WolvesOfBashiChannel, LifelineToTaiwan, HuntTheDragon, Patroller, FirstLight };


        public static ScenarioDefinition Get(string id) => Introductory.FirstOrDefault(item =>
            string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    public enum ScenarioEndReason
    {
        None,
        ObjectiveSunk,
        ForceDestroyed,
        FixedResult,
        Disengagement,
        MutualScoring,
        Concession,
        TurnLimit,
        DestinationReached,
        BoardEdgeExited
    }

    public sealed class ScenarioScore
    {
        public int UsObjectiveDamage { get; }
        public int PlanObjectiveDamage { get; }
        public int UsTieBreakDamage { get; }
        public int PlanTieBreakDamage { get; }
        public string Result { get; }

        public ScenarioScore(int usObjectiveDamage, int planObjectiveDamage,
            int usTieBreakDamage, int planTieBreakDamage, string resultOverride = null)
        {
            UsObjectiveDamage = usObjectiveDamage;
            PlanObjectiveDamage = planObjectiveDamage;
            UsTieBreakDamage = usTieBreakDamage;
            PlanTieBreakDamage = planTieBreakDamage;
            Result = resultOverride ?? ScenarioOneGame.CompareScore(usObjectiveDamage, planObjectiveDamage,
                usTieBreakDamage, planTieBreakDamage);
        }
    }
}
