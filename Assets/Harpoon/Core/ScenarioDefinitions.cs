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
        ConvoyArrival
    }

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

        public ScenarioFormationDefinition(string id, Side side, HexCoord start,
            IEnumerable<ScenarioUnitDefinition> units, int dummyCards = 0)
        {
            Id = id ?? string.Empty;
            Side = side;
            Start = start;
            Units = (units ?? Array.Empty<ScenarioUnitDefinition>()).ToArray();
            DummyCards = Math.Max(0, dummyCards);
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
        public IReadOnlyList<ScenarioFormationDefinition> Formations { get; }

        public ScenarioDefinition(string id, string name, string subtitle, string briefing,
            string victoryText, string source, int maximumTurns, bool detectionRulesEnabled,
            string usObjectiveUnitId, string planObjectiveUnitId, string usTieBreakUnitId,
            string planTieBreakUnitId, IEnumerable<ScenarioFormationDefinition> formations,
            ScenarioScoringMode scoringMode = ScenarioScoringMode.ObjectiveThenEscort,
            bool hasUsDestination = false, HexCoord usDestination = default,
            int planDeploymentMinimumDistance = 0)
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
                new ScenarioFormationDefinition("US Subic Convoy", Side.UsNavy, new HexCoord(7, 16),
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
                new ScenarioFormationDefinition("US Subic Convoy", Side.UsNavy, new HexCoord(7, 16),
                    PicketLine.Formations.First(item => item.Side == Side.UsNavy).Units),
                new ScenarioFormationDefinition("US Dummy Group", Side.UsNavy, new HexCoord(7, 16),
                    Array.Empty<ScenarioUnitDefinition>(), 3),
                new ScenarioFormationDefinition("PLAN Picket Group", Side.Plan, new HexCoord(15, 10),
                    PicketLine.Formations.First(item => item.Side == Side.Plan).Units),
                new ScenarioFormationDefinition("PLAN Dummy Group", Side.Plan, new HexCoord(15, 10),
                    Array.Empty<ScenarioUnitDefinition>(), 5)
            }, ScenarioScoringMode.ConvoyArrival, true, new HexCoord(8, 10), 4);

        public static IReadOnlyList<ScenarioDefinition> Introductory { get; } =
            new[] { ContactOffBashiChannel, FlagshipDuel, CloseAboard, PicketLine, GhostFleet };

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
        DestinationReached
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
