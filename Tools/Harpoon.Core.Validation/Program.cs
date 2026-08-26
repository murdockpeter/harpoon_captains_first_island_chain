using Harpoon.Core;

static class Program
{
    private static int _checks;

    public static int Main()
    {
        try
        {
            ScenarioDataAndMovement();
            ScoringAndStoppingRoutes();
            MissileDecisionRoute();
            CounterattackDecisionRoute();
            GunfireObjectiveRoute();
            GunfireBreakOffRoute();
            ReplayAndSaveRoute();
            ReleaseVersionRoute();
            Console.WriteLine($"HARPOON CORE VALIDATION PASSED: {_checks} checks; scripted movement, missile, gunfire, scoring, stopping, and replay routes complete.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("HARPOON CORE VALIDATION FAILED");
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void ScenarioDataAndMovement()
    {
        var state = ScenarioOne.Create();
        Check(state.Scenario.Id == "fic-01", "Scenario definition ID");
        Check(state.Player.Position.Equals(new HexCoord(7, 13)), "US start 0713");
        Check(state.Enemy.Position.Equals(new HexCoord(10, 10)), "PLAN start 1010");
        Check(state.Player.Position.DistanceTo(state.Enemy.Position) == 3, "Printed three-hex separation");
        Check(state.MaximumTurns == 0, "No invented turn limit");
        Check(ModernPlatformDatabase.All.Count == 34, "Complete modern hull database");

        var game = new ScenarioOneGame(11, null, true);
        Check(game.DrawMovementChit().Accepted, "Movement chit draw");
        var side = game.State.ActiveSide;
        var force = game.State.ForceFor(side);
        Check(game.DeclareSpeed(side, 1).Accepted, "Explicit speed decision");
        var step = game.State.Map.NavigableNeighbors(force.Position, side).First();
        Check(game.TryMove(side, step, out _), "Legal adjacent movement step");
        game.EndActivation(side);
        Check(game.State.ActiveSide != side || game.State.Phase == ActivationPhase.AwaitingChit,
            "Activation completion");
    }

    private static void ScoringAndStoppingRoutes()
    {
        EndAndCheck(ScoringGame(1, 0, 0, 0), GameCommandType.Disengage,
            Side.UsNavy, "US NAVY VICTORY", ScenarioEndReason.Disengagement);
        EndAndCheck(ScoringGame(0, 1, 0, 0), GameCommandType.Disengage,
            Side.UsNavy, "PLAN VICTORY", ScenarioEndReason.Disengagement);
        EndAndCheck(ScoringGame(1, 1, 0, 0), GameCommandType.Disengage,
            Side.UsNavy, "DRAW", ScenarioEndReason.Disengagement);
        EndAndCheck(ScoringGame(0, 0, 1, 0), GameCommandType.Disengage,
            Side.UsNavy, "US NAVY VICTORY", ScenarioEndReason.Disengagement);

        var mutual = ScoringGame(0, 0, 0, 0);
        Check(mutual.Execute(new GameCommand(GameCommandType.RequestScoring, Side.UsNavy,
            mutual.State.Revision)).Accepted && !mutual.State.IsGameOver, "First mutual-score vote");
        Check(mutual.Execute(new GameCommand(GameCommandType.RequestScoring, Side.Plan,
            mutual.State.Revision)).Accepted && mutual.State.EndReason == ScenarioEndReason.MutualScoring,
            "Second mutual-score vote ends match");

        var concession = ScoringGame(2, 0, 0, 0);
        Check(concession.Execute(new GameCommand(GameCommandType.Concede, Side.UsNavy,
            concession.State.Revision)).Accepted && concession.State.Result == "PLAN VICTORY" &&
            concession.State.EndReason == ScenarioEndReason.Concession, "Concession overrides current score");
    }

    private static void MissileDecisionRoute()
    {
        var game = ScoringGame(0, 0, 0, 0, sameRangeOne: true,
            dice: new SequenceDieRoller(4, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1));
        Check(game.Execute(new GameCommand(GameCommandType.Attack, Side.UsNavy,
            game.State.Revision, targetId: game.State.Enemy.Id)).Accepted, "Open missile attack");
        Check(game.Execute(new GameCommand(GameCommandType.AllocateMissileFire, Side.UsNavy,
            game.State.Revision, missileAllocations: new[]
            {
                new MissileAllocationData
                {
                    id = "MVP-RAID", sourceUnitId = "us-burke-iia", targetUnitId = "plan-type-071",
                    shortFactors = 2, longFactors = 1
                }
            })).Accepted, "Explicit missile allocation");
        Check(game.Execute(new GameCommand(GameCommandType.Defend, Side.Plan, game.State.Revision,
            defensePairs: new[] { new DefensePairData
            {
                firstUnitId = "plan-type-054a", secondUnitId = "plan-type-071"
            } })).Accepted, "Explicit defensive pairing");
        if (game.State.PendingMissileCombat.Phase == MissileCombatPhase.LongRangeRemoval)
            Check(game.Execute(new GameCommand(GameCommandType.Defend, Side.Plan, game.State.Revision,
                missileReductions: new[] { new MissileReductionData
                {
                    salvoId = "MVP-RAID", factors = game.State.PendingMissileCombat.LongRangeHits
                } })).Accepted, "Defender-directed LR SAM removal");
        var resolution = game.Execute(new GameCommand(GameCommandType.Defend, Side.Plan,
            game.State.Revision, shortRangeDefenses: new[] { new ShortRangeDefenseData
            {
                defendingUnitId = "plan-type-054a", salvoId = "MVP-RAID"
            } }));
        Check(resolution.Accepted && resolution.AttackReport != null, "SR SAM, point defense, and impact resolution");
        if (!game.State.IsGameOver && game.State.PendingMissileCombat?.Phase == MissileCombatPhase.CounterattackDecision)
            Check(game.Execute(new GameCommand(GameCommandType.Counterattack, Side.Plan,
                game.State.Revision, enabled: false)).Accepted, "Explicit counterattack decline");
        if (!game.State.IsGameOver)
            EndAndCheck(game, GameCommandType.Disengage, Side.UsNavy,
                game.CurrentScore().Result, ScenarioEndReason.Disengagement);
    }

    private static void GunfireObjectiveRoute()
    {
        var game = ScoringGame(0, 0, 0, 0, exhaustMissiles: true, type071Damage: 2,
            sameHex: true, dice: new SequenceDieRoller(1, 5, 5, 5));
        Check(game.Execute(new GameCommand(GameCommandType.Attack, Side.UsNavy,
            game.State.Revision, targetId: game.State.Enemy.Id)).Accepted, "Open same-hex gun engagement");
        Check(game.Execute(new GameCommand(GameCommandType.ArrangeGunfire, Side.UsNavy,
            game.State.Revision, gunPairs: ScenarioOneGame.DefaultGunPairs(game.State.Player))).Accepted,
            "US firing/screening decision");
        Check(game.Execute(new GameCommand(GameCommandType.ArrangeGunfire, Side.Plan,
            game.State.Revision, gunPairs: ScenarioOneGame.DefaultGunPairs(game.State.Enemy))).Accepted,
            "PLAN firing/screening decision");
        Check(game.Execute(new GameCommand(GameCommandType.FireGuns, Side.UsNavy,
            game.State.Revision, targetId: "plan-type-071", sourceUnitId: "us-burke-iia")).Accepted,
            "Explicit gun target and firing ship");
        Check(game.State.IsGameOver && game.State.EndReason == ScenarioEndReason.ObjectiveSunk &&
            game.State.Result == "US NAVY VICTORY", "Gunfire objective sinking completes match");
    }

    private static void CounterattackDecisionRoute()
    {
        var rolls = Enumerable.Repeat(1, 8).Concat(Enumerable.Repeat(4, 12)).ToArray();
        var game = ScoringGame(0, 0, 0, 0, sameRangeOne: true, dice: new SequenceDieRoller(rolls));
        Check(game.Execute(new GameCommand(GameCommandType.Attack, Side.UsNavy,
            game.State.Revision, targetId: game.State.Enemy.Id)).Accepted, "Counterattack route opens raid");
        Check(game.Execute(new GameCommand(GameCommandType.AllocateMissileFire, Side.UsNavy,
            game.State.Revision, missileAllocations: new[] { new MissileAllocationData
            {
                id = "OPENING", sourceUnitId = "us-burke-iia", targetUnitId = "plan-type-071",
                shortFactors = 2, longFactors = 1
            } })).Accepted, "Opening allocation");
        Check(game.Execute(new GameCommand(GameCommandType.Defend, Side.Plan, game.State.Revision,
            defensePairs: new[] { new DefensePairData
            {
                firstUnitId = "plan-type-054a", secondUnitId = "plan-type-071"
            } })).Accepted, "Opening defensive pair");
        Check(game.Execute(new GameCommand(GameCommandType.Defend, Side.Plan, game.State.Revision,
            shortRangeDefenses: new[] { new ShortRangeDefenseData
            {
                defendingUnitId = "plan-type-054a", salvoId = "OPENING"
            } })).Accepted && game.State.PendingMissileCombat.Phase == MissileCombatPhase.CounterattackDecision,
            "Opening raid reaches counterattack decision");
        Check(game.Execute(new GameCommand(GameCommandType.Counterattack, Side.Plan,
            game.State.Revision, enabled: true)).Accepted, "Accept counterattack");
        Check(game.Execute(new GameCommand(GameCommandType.AllocateMissileFire, Side.Plan,
            game.State.Revision, missileAllocations: new[] { new MissileAllocationData
            {
                id = "COUNTER", sourceUnitId = "plan-type-054a", targetUnitId = "us-merchant",
                shortFactors = 2
            } })).Accepted, "Counterattack allocation");
        Check(game.Execute(new GameCommand(GameCommandType.Defend, Side.UsNavy, game.State.Revision,
            defensePairs: new[] { new DefensePairData
            {
                firstUnitId = "us-burke-iia", secondUnitId = "us-merchant"
            } })).Accepted && game.State.PendingMissileCombat.Phase == MissileCombatPhase.LongRangeRemoval,
            "US long-range defense creates removal decision");
        var removals = game.State.PendingMissileCombat.LongRangeHits;
        Check(game.Execute(new GameCommand(GameCommandType.Defend, Side.UsNavy, game.State.Revision,
            missileReductions: new[] { new MissileReductionData { salvoId = "COUNTER", factors = removals } })).Accepted,
            "Assign all long-range removals");
        Check(game.Execute(new GameCommand(GameCommandType.Defend, Side.UsNavy, game.State.Revision,
            shortRangeDefenses: Array.Empty<ShortRangeDefenseData>())).Accepted &&
            game.State.PendingMissileCombat == null, "Counterattack resolves once without counter-counterattack");
        EndAndCheck(game, GameCommandType.Disengage, Side.UsNavy,
            game.CurrentScore().Result, ScenarioEndReason.Disengagement);
    }

    private static void GunfireBreakOffRoute()
    {
        var game = ScoringGame(0, 0, 0, 0, exhaustMissiles: true, sameHex: true,
            dice: new SequenceDieRoller(1, 1, 1, 1, 1, 1, 1, 1));
        Check(game.Execute(new GameCommand(GameCommandType.Attack, Side.UsNavy,
            game.State.Revision, targetId: game.State.Enemy.Id)).Accepted, "Break-off route opens gun engagement");
        Check(game.Execute(new GameCommand(GameCommandType.ArrangeGunfire, Side.UsNavy,
            game.State.Revision, gunPairs: ScenarioOneGame.DefaultGunPairs(game.State.Player))).Accepted &&
            game.Execute(new GameCommand(GameCommandType.ArrangeGunfire, Side.Plan,
            game.State.Revision, gunPairs: ScenarioOneGame.DefaultGunPairs(game.State.Enemy))).Accepted,
            "Break-off route arrangements");
        while (game.State.PendingGunCombat?.Phase == GunCombatPhase.Firing)
        {
            var engagement = game.State.PendingGunCombat;
            var shooterId = engagement.FiringOrder[engagement.FiringIndex];
            var shooter = game.State.Unit(shooterId);
            var actor = shooter.Definition.Side;
            var target = actor == Side.UsNavy ? "plan-type-071" : "us-merchant";
            Check(game.Execute(new GameCommand(GameCommandType.FireGuns, actor, game.State.Revision,
                targetId: target, sourceUnitId: shooterId)).Accepted, "Complete ordered gun shot");
        }
        var breakingSide = game.State.PendingGunCombat.DecisionSide;
        Check(game.Execute(new GameCommand(GameCommandType.BreakOff, breakingSide,
            game.State.Revision, enabled: true)).Accepted, "Attacker requests break-off");
        var respondingSide = game.State.PendingGunCombat.DecisionSide;
        Check(game.Execute(new GameCommand(GameCommandType.BreakOff, respondingSide,
            game.State.Revision, enabled: false)).Accepted && game.State.PendingGunCombat == null,
            "Explicit successful break-off ends close action");
        EndAndCheck(game, GameCommandType.Disengage, Side.UsNavy,
            game.CurrentScore().Result, ScenarioEndReason.Disengagement);
    }

    private static void ReplayAndSaveRoute()
    {
        var original = new ScenarioOneGame(60601, null, true);
        Check(original.Execute(new GameCommand(GameCommandType.Concede, Side.Plan,
            original.State.Revision)).Accepted, "Replay source command");
        var replay = ScenarioOneGame.Replay(original.Seed, original.State.CommandLog);
        Check(replay.State.Result == original.State.Result && replay.State.EndReason == original.State.EndReason &&
            replay.State.Revision == original.State.Revision, "Seeded command replay reproduces result");
        var snapshot = original.CaptureSnapshot();
        var mirror = new ScenarioOneGame(1, null, true);
        mirror.ApplySnapshot(snapshot);
        Check(mirror.Seed == original.Seed && mirror.State.Result == original.State.Result &&
            mirror.State.Transactions.Count == original.State.Transactions.Count, "Snapshot/export restore");
    }

    private static void ReleaseVersionRoute()
    {
        Check(ReleaseVersion.IsNewer("v0.1.1", "0.1.0"), "Release tag patch upgrade");
        Check(ReleaseVersion.IsNewer("v1.0.0", "0.9.9"), "Release tag major upgrade");
        Check(!ReleaseVersion.IsNewer("v0.1.0", "0.1.0"), "Equal release is not an update");
        Check(!ReleaseVersion.IsNewer("invalid", "0.1.0"), "Invalid release tag is rejected");
    }

    private static ScenarioOneGame ScoringGame(int usObjectiveDamage, int planObjectiveDamage,
        int usEscortDamage, int planEscortDamage, bool exhaustMissiles = false,
        int type071Damage = -1, bool sameHex = false, bool sameRangeOne = false, IDieRoller dice = null)
    {
        var game = new ScenarioOneGame(60601, null, true, false, dice);
        var snapshot = game.CaptureSnapshot();
        snapshot.activeSide = Side.UsNavy;
        snapshot.activeFormationId = "US Task Force";
        snapshot.phase = ActivationPhase.PlayerAction;
        foreach (var unit in snapshot.units)
        {
            if (unit.id == "plan-type-071") unit.hullDamage = type071Damage >= 0 ? type071Damage : usObjectiveDamage;
            else if (unit.id == "us-merchant") unit.hullDamage = planObjectiveDamage;
            else if (unit.id == "plan-type-054a") unit.hullDamage = usEscortDamage;
            else if (unit.id == "us-burke-iia") unit.hullDamage = planEscortDamage;
            if (exhaustMissiles) { unit.shortMissiles = 0; unit.longMissiles = 0; }
        }
        var us = snapshot.formations.First(item => item.side == Side.UsNavy);
        var plan = snapshot.formations.First(item => item.side == Side.Plan);
        if (sameHex) { us.column = plan.column; us.row = plan.row; }
        else if (sameRangeOne) { us.column = plan.column - 1; us.row = plan.row; }
        game.ApplySnapshot(snapshot);
        return game;
    }

    private static void EndAndCheck(ScenarioOneGame game, GameCommandType type, Side side,
        string result, ScenarioEndReason reason)
    {
        Check(game.Execute(new GameCommand(type, side, game.State.Revision)).Accepted,
            $"Accepted {type}");
        Check(game.State.IsGameOver && game.State.Result == result && game.State.EndReason == reason,
            $"{type} result {result}/{reason}");
    }

    private static void Check(bool condition, string label)
    {
        _checks++;
        if (!condition) throw new InvalidOperationException($"Check {_checks} failed: {label}");
    }
}
