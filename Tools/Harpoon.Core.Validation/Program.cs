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
            ScenarioTwoRoute();
            ScenarioThreeRoute();
            ScenarioFourRoute();
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

    private static void ScenarioTwoRoute()
    {
        var definition = FirstIslandChainScenarios.FlagshipDuel;
        var state = ScenarioOne.Create(false, definition);
        Check(state.Scenario.Id == "fic-02" && state.Player.Position == new HexCoord(12, 13) &&
            state.Enemy.Position == new HexCoord(5, 10), "Scenario 2 printed setup");
        Check(state.Player.Units.Count == 4 && state.Enemy.Units.Count == 1 &&
            state.Unit("us-burke-iia-1") != null && state.Unit("us-burke-iia-2") != null &&
            state.Unit("us-ticonderoga") != null && state.Unit("us-san-antonio") != null &&
            state.Unit("plan-type-055") != null, "Scenario 2 exact order of battle");

        var game = new ScenarioOneGame(2202, null, true, false,
            new SequenceDieRoller(1, 1, 1, 1, 1, 1), definition);
        var snapshot = game.CaptureSnapshot();
        snapshot.activeSide = Side.Plan;
        snapshot.activeFormationId = "PLAN Renhai Group";
        snapshot.phase = ActivationPhase.PlayerAction;
        var plan = snapshot.formations.First(item => item.side == Side.Plan);
        plan.column = 9;
        plan.row = 13;
        game.ApplySnapshot(snapshot);
        Check(game.Execute(new GameCommand(GameCommandType.Attack, Side.Plan, game.State.Revision,
            targetId: "US Flagship Group")).Accepted, "Scenario 2 opens formation-scale missile attack");
        Check(game.Execute(new GameCommand(GameCommandType.AllocateMissileFire, Side.Plan,
            game.State.Revision, missileAllocations: new[]
            {
                new MissileAllocationData { id = "S2-A", sourceUnitId = "plan-type-055",
                    targetUnitId = "us-burke-iia-1", longFactors = 2 },
                new MissileAllocationData { id = "S2-B", sourceUnitId = "plan-type-055",
                    targetUnitId = "us-ticonderoga", longFactors = 2 }
            })).Accepted, "Scenario 2 splits missile factors across multiple targets");
        Check(game.Execute(new GameCommand(GameCommandType.Defend, Side.UsNavy,
            game.State.Revision, defensePairs: new[]
            {
                new DefensePairData { firstUnitId = "us-burke-iia-1", secondUnitId = "us-burke-iia-2" },
                new DefensePairData { firstUnitId = "us-ticonderoga", secondUnitId = "us-san-antonio" }
            })).Accepted && game.State.Player.DefensePairs.Count == 2,
            "Scenario 2 deploys multiple player-controlled defensive pairs");

        var scoring = new ScenarioOneGame(2203, null, true, false, null, definition);
        var scoreSnapshot = scoring.CaptureSnapshot();
        scoreSnapshot.activeSide = Side.UsNavy;
        scoreSnapshot.activeFormationId = "US Flagship Group";
        scoreSnapshot.phase = ActivationPhase.PlayerAction;
        foreach (var unit in scoreSnapshot.units)
        {
            if (unit.id == "plan-type-055") unit.hullDamage = 2;
            if (unit.id == "us-burke-iia-1") unit.hullDamage = 1;
            if (unit.id == "us-san-antonio") unit.hullDamage = 2;
        }
        scoring.ApplySnapshot(scoreSnapshot);
        Check(scoring.CurrentScore().UsObjectiveDamage == 2 &&
            scoring.CurrentScore().PlanObjectiveDamage == 3, "Scenario 2 totals hull hits across all warships");
        Check(scoring.Execute(new GameCommand(GameCommandType.Disengage, Side.UsNavy,
            scoring.State.Revision)).Accepted && scoring.State.Result == "PLAN VICTORY",
            "Scenario 2 deterministic scoring acceptance route");

        var networkMirror = new ScenarioOneGame(1, null, true);
        networkMirror.ApplySnapshot(scoring.CaptureSnapshot());
        Check(networkMirror.State.Scenario.Id == "fic-02" && networkMirror.State.Player.Units.Count == 4,
            "Scenario 2 snapshot switches a client from the Scenario 1 schema");

        var replaySource = new ScenarioOneGame(2204, null, true, false, null, definition);
        Check(replaySource.Execute(new GameCommand(GameCommandType.Concede, Side.Plan,
            replaySource.State.Revision)).Accepted, "Scenario 2 replay source command");
        var replay = ScenarioOneGame.Replay(replaySource.Seed, replaySource.State.CommandLog,
            scenario: definition);
        Check(replay.State.Scenario.Id == "fic-02" && replay.State.Result == "US NAVY VICTORY",
            "Scenario 2 deterministic replay retains scenario identity");
    }

    private static void ScenarioThreeRoute()
    {
        var definition = FirstIslandChainScenarios.CloseAboard;
        var state = ScenarioOne.Create(false, definition);
        Check(state.Scenario.Id == "fic-03" && state.Player.Position == new HexCoord(13, 13) &&
            state.Enemy.Position == new HexCoord(10, 10), "Scenario 3 printed setup");
        Check(state.Player.Units.Select(unit => unit.Definition.Id).SequenceEqual(new[]
            { "us-burke-iia", "us-constellation" }) &&
            state.Enemy.Units.Select(unit => unit.Definition.Id).SequenceEqual(new[]
            { "plan-type-056a-1", "plan-type-056a-2", "plan-type-056a-3" }),
            "Scenario 3 exact order of battle");

        var provenanceTarget = ScenarioOne.Create(false, definition).Unit("plan-type-056a-1");
        provenanceTarget.ApplyDamage(1, DamageSource.Missile);
        Check(provenanceTarget.HullDamage == 1 && provenanceTarget.GunfireHullDamage == 0,
            "Scenario 3 missile damage changes hull but earns no gunfire score");

        var game = new ScenarioOneGame(3303, null, true, false,
            new SequenceDieRoller(1, 6, 6, 6, 6, 6, 6, 6), definition);
        var snapshot = game.CaptureSnapshot();
        snapshot.activeSide = Side.UsNavy;
        snapshot.activeFormationId = "US Close Action Group";
        snapshot.phase = ActivationPhase.PlayerAction;
        var us = snapshot.formations.First(item => item.side == Side.UsNavy);
        var plan = snapshot.formations.First(item => item.side == Side.Plan);
        us.column = plan.column;
        us.row = plan.row;
        foreach (var unit in snapshot.units) { unit.shortMissiles = 0; unit.longMissiles = 0; }
        game.ApplySnapshot(snapshot);
        Check(game.Execute(new GameCommand(GameCommandType.Attack, Side.UsNavy, game.State.Revision,
            targetId: "PLAN Corvette Group")).Accepted &&
            game.Execute(new GameCommand(GameCommandType.ArrangeGunfire, Side.UsNavy, game.State.Revision,
                gunPairs: ScenarioOneGame.DefaultGunPairs(game.State.Player))).Accepted &&
            game.Execute(new GameCommand(GameCommandType.ArrangeGunfire, Side.Plan, game.State.Revision,
                gunPairs: ScenarioOneGame.DefaultGunPairs(game.State.Enemy))).Accepted,
            "Scenario 3 enters legal same-hex close action");
        var shot = game.Execute(new GameCommand(GameCommandType.FireGuns, Side.UsNavy,
            game.State.Revision, targetId: "plan-type-056a-1", sourceUnitId: "us-burke-iia"));
        Check(shot.Accepted && game.State.Unit("plan-type-056a-1").GunfireHullDamage == 1 &&
            game.CurrentScore().UsObjectiveDamage == 1, "Scenario 3 legal gun shot updates gunfire-only score");
        var mirror = new ScenarioOneGame(1, null, true);
        mirror.ApplySnapshot(game.CaptureSnapshot());
        Check(mirror.State.Scenario.Id == "fic-03" && mirror.CurrentScore().UsObjectiveDamage == 1,
            "Scenario 3 snapshot retains damage provenance and scenario identity");

        var scoring = new ScenarioOneGame(3304, null, true, false, null, definition);
        var scoringSnapshot = scoring.CaptureSnapshot();
        scoringSnapshot.activeSide = Side.UsNavy;
        scoringSnapshot.activeFormationId = "US Close Action Group";
        scoringSnapshot.phase = ActivationPhase.PlayerAction;
        var damagedPlan = scoringSnapshot.units.Single(unit => unit.id == "plan-type-056a-1");
        damagedPlan.hullDamage = 1;
        damagedPlan.gunfireHullDamage = 1;
        var damagedUs = scoringSnapshot.units.Single(unit => unit.id == "us-burke-iia");
        damagedUs.hullDamage = 2;
        damagedUs.gunfireHullDamage = 2;
        scoring.ApplySnapshot(scoringSnapshot);
        Check(scoring.Execute(new GameCommand(GameCommandType.Disengage, Side.UsNavy,
            scoring.State.Revision)).Accepted && scoring.State.Result == "PLAN VICTORY",
            "Scenario 3 deterministic gunfire-score acceptance route");
    }

    private static void ScenarioFourRoute()
    {
        var definition = FirstIslandChainScenarios.PicketLine;
        var state = ScenarioOne.Create(false, definition);
        Check(state.Scenario.Id == "fic-04" && state.DetectionRulesEnabled &&
            state.Player.Position == new HexCoord(7, 16) && state.Enemy.Position == new HexCoord(15, 10),
            "Scenario 4 Subic convoy and hidden picket setup");
        Check(state.Player.Units.Count == 5 && state.Enemy.Units.Count == 3 &&
            state.Player.Units.Count(unit => unit.Definition.Role == UnitRole.Objective) == 2,
            "Scenario 4 exact convoy and PLAN order of battle");

        var deployment = new ScenarioOneGame(4404, null, true, false, null, definition);
        var invalid = deployment.Execute(new GameCommand(GameCommandType.DeployFormation, Side.Plan,
            deployment.State.Revision, new HexCoord(10, 15), formationId: "PLAN Picket Group"));
        Check(!invalid.Accepted && invalid.Violation.Code == RuleViolationCode.InvalidFormation,
            "Scenario 4 rejects PLAN setup inside a four-hex exclusion zone");
        Check(deployment.Execute(new GameCommand(GameCommandType.DeployFormation, Side.Plan,
            deployment.State.Revision, new HexCoord(15, 10), formationId: "PLAN Picket Group")).Accepted,
            "Scenario 4 accepts legal player-controlled PLAN deployment");

        var redacted = deployment.CaptureSnapshotFor(Side.UsNavy);
        var hiddenPlan = redacted.formations.Single(item => item.side == Side.Plan);
        Check(hiddenPlan.column == 0 && hiddenPlan.row == 0 && hiddenPlan.unitIds.Length == 0 &&
            redacted.units.All(unit => !unit.id.StartsWith("plan-")) && redacted.transactions.Length == 0,
            "Scenario 4 US snapshot redacts hidden position, contents, units, and trace");
        deployment.State.Detection.Detect(Side.UsNavy, deployment.State.Enemy,
            DetectionMethod.SurfaceSearchRadar, deployment.State.Turn);
        var detected = deployment.CaptureSnapshotFor(Side.UsNavy);
        Check(detected.formations.Single(item => item.side == Side.Plan).column == 15 &&
            detected.formations.Single(item => item.side == Side.Plan).unitIds.Length == 3,
            "Scenario 4 classified contact publishes position and formation contents");

        var arrival = new ScenarioOneGame(4405, null, true, false, null, definition);
        var arrivalSnapshot = arrival.CaptureSnapshot();
        arrivalSnapshot.activeSide = Side.UsNavy;
        arrivalSnapshot.activeFormationId = "US Subic Convoy";
        arrivalSnapshot.phase = ActivationPhase.PlayerMove;
        var convoy = arrivalSnapshot.formations.Single(item => item.side == Side.UsNavy);
        convoy.column = 9;
        convoy.row = 10;
        convoy.declaredSpeed = 1;
        convoy.movementSpent = 0;
        arrivalSnapshot.usDeclaredSpeed = 1;
        arrivalSnapshot.usMovementSpent = 0;
        arrival.ApplySnapshot(arrivalSnapshot);
        Check(arrival.Execute(new GameCommand(GameCommandType.Move, Side.UsNavy,
            arrival.State.Revision, new HexCoord(8, 10))).Accepted && arrival.State.IsGameOver &&
            arrival.State.EndReason == ScenarioEndReason.DestinationReached &&
            arrival.State.Result == "US NAVY VICTORY", "Scenario 4 convoy arrival acceptance route");
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
