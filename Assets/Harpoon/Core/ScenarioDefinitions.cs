using System;
using System.Collections.Generic;
using System.Linq;

namespace Harpoon.Core
{
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

        public ScenarioFormationDefinition(string id, Side side, HexCoord start,
            IEnumerable<ScenarioUnitDefinition> units)
        {
            Id = id ?? string.Empty;
            Side = side;
            Start = start;
            Units = (units ?? Array.Empty<ScenarioUnitDefinition>()).ToArray();
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
        public IReadOnlyList<ScenarioFormationDefinition> Formations { get; }

        public ScenarioDefinition(string id, string name, string subtitle, string briefing,
            string victoryText, string source, int maximumTurns, bool detectionRulesEnabled,
            string usObjectiveUnitId, string planObjectiveUnitId, string usTieBreakUnitId,
            string planTieBreakUnitId, IEnumerable<ScenarioFormationDefinition> formations)
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
        TurnLimit
    }

    public sealed class ScenarioScore
    {
        public int UsObjectiveDamage { get; }
        public int PlanObjectiveDamage { get; }
        public int UsTieBreakDamage { get; }
        public int PlanTieBreakDamage { get; }
        public string Result { get; }

        public ScenarioScore(int usObjectiveDamage, int planObjectiveDamage,
            int usTieBreakDamage, int planTieBreakDamage)
        {
            UsObjectiveDamage = usObjectiveDamage;
            PlanObjectiveDamage = planObjectiveDamage;
            UsTieBreakDamage = usTieBreakDamage;
            PlanTieBreakDamage = planTieBreakDamage;
            Result = ScenarioOneGame.CompareScore(usObjectiveDamage, planObjectiveDamage,
                usTieBreakDamage, planTieBreakDamage);
        }
    }
}
