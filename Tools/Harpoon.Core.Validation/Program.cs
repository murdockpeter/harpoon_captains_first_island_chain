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
            ScenarioFiveRoute();
            ScenarioSixRoute();
            ScenarioSevenRoute();
            ScenarioEightRoute();
            ScenarioNineRoute();
            ScenarioTenRoute();
            MvpSharedDataAndCompletionRoute();
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

    private static void MvpSharedDataAndCompletionRoute()
    {
        var errors = MvpDataValidation.Validate();
        Check(errors.Count == 0, "MVP shared data schema and cross-reference validation" +
            (errors.Count == 0 ? string.Empty : ": " + string.Join("; ", errors)));
        Check(ModernAircraftDatabase.All.Count == 5 && ModernTacticalAircraftDatabase.All.Count == 8,
            "All thirteen supplement aircraft stat cards are entered");
        Check(ModernAirBaseDatabase.All.Count(item => !item.IsCarrier) == 6 &&
              ModernAirBaseDatabase.All.Count(item => item.IsCarrier) == 2,
            "All six base and two carrier-wing charts are entered");

        foreach (var scenario in FirstIslandChainScenarios.Introductory)
        {
            var game = new ScenarioOneGame(1000 + int.Parse(scenario.Id.Substring(4)), null,
                true, false, null, scenario);
            Check(game.State.Scenario == scenario && !game.State.IsGameOver,
                $"{scenario.Id} creates from its release definition");
            var result = game.Execute(new GameCommand(GameCommandType.Concede, Side.UsNavy,
                game.State.Revision));
            Check(result.Accepted && game.State.IsGameOver &&
                  game.State.EndReason == ScenarioEndReason.Concession,
                $"{scenario.Id} can reach an authoritative result without debug controls");
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

    private static void ScenarioFiveRoute()
    {
        var definition = FirstIslandChainScenarios.GhostFleet;
        var game = new ScenarioOneGame(5505, null, true, false,
            new SequenceDieRoller(1, 1), definition);
        Check(game.State.Scenario.Id == "fic-05" && game.State.Forces.Count == 4 &&
            game.State.Forces.Where(force => force.Side == Side.UsNavy).Sum(force => force.DummyCards) == 3 &&
            game.State.Forces.Where(force => force.Side == Side.Plan).Sum(force => force.DummyCards) == 5,
            "Scenario 5 printed three-US/five-PLAN dummy allotment");

        Check(game.Execute(new GameCommand(GameCommandType.TransferDummyCards, Side.UsNavy,
            game.State.Revision, factors: 1, formationId: "US Dummy Group",
            newFormationId: "US Dummy Group 2")).Accepted &&
            game.State.Forces.Where(force => force.Side == Side.UsNavy).Sum(force => force.DummyCards) == 3,
            "Scenario 5 creates a dummy force without changing its side's allotment");
        var opponentSnapshot = game.CaptureSnapshotFor(Side.UsNavy);
        Check(opponentSnapshot.formations.Where(item => item.side == Side.Plan)
                .All(item => item.dummyCards == 0) &&
            opponentSnapshot.commands.Any(item => item.type == GameCommandType.TransferDummyCards),
            "Scenario 5 hides enemy dummy counts but publishes verified transfers");

        var sensorGame = new ScenarioOneGame(5506, null, true, false,
            new SequenceDieRoller(1, 1), definition);
        var snapshot = sensorGame.CaptureSnapshot();
        snapshot.activeSide = Side.UsNavy;
        snapshot.activeFormationId = "US Subic Convoy";
        snapshot.phase = ActivationPhase.PlayerAction;
        var observer = snapshot.formations.Single(item => item.id == "US Subic Convoy");
        var dummy = snapshot.formations.Single(item => item.id == "PLAN Dummy Group");
        dummy.column = observer.column;
        dummy.row = observer.row;
        sensorGame.ApplySnapshot(snapshot);
        Check(sensorGame.Execute(new GameCommand(GameCommandType.Search, Side.UsNavy,
                sensorGame.State.Revision, targetId: "PLAN Dummy Group", formationId: "US Subic Convoy",
                searchMode: "visual")).Accepted &&
            sensorGame.State.Detection.ContactFor(Side.UsNavy, "PLAN Dummy Group").Level == ContactLevel.Located,
            "Scenario 5 visual search reports no surface ships without classifying a dummy");
        Check(!sensorGame.Execute(new GameCommand(GameCommandType.Attack, Side.UsNavy,
                sensorGame.State.Revision, targetId: "PLAN Dummy Group")).Accepted,
            "Scenario 5 located dummy contact cannot be attacked");
        Check(sensorGame.Execute(new GameCommand(GameCommandType.Search, Side.UsNavy,
                sensorGame.State.Revision, targetId: "PLAN Dummy Group", formationId: "US Subic Convoy",
                searchMode: "sonar")).Accepted && sensorGame.State.Formation("PLAN Dummy Group") == null &&
            sensorGame.State.Forces.Where(force => force.Side == Side.Plan).Sum(force => force.DummyCards) == 5,
            "Scenario 5 successful sonar clears dummy force and preserves the dummy cards");
    }

    private static void ScenarioSixRoute()
    {
        var definition = FirstIslandChainScenarios.WolvesOfBashiChannel;
        var game = new ScenarioOneGame(6606, null, true, false,
            new SequenceDieRoller(1, 1, 1, 1, 1, 1, 6, 6, 6, 6, 6), definition);
        Check(game.State.MaximumTurns == 7 && game.State.Forces.Count == 5 &&
            game.State.Forces.Where(force => force.Side == Side.Plan).All(force => force.IsSubmarineOnly) &&
            game.State.Formation("US Los Angeles").IsSubmarineOnly,
            "Scenario 6 exact separated surface/submarine order of battle and seven-turn limit");
        var snapshot = game.CaptureSnapshot();
        snapshot.activeSide = Side.UsNavy;
        snapshot.activeFormationId = "US Hunter-Killer Group";
        snapshot.phase = ActivationPhase.PlayerAction;
        var hunter = snapshot.formations.Single(item => item.id == "US Hunter-Killer Group");
        var yuan = snapshot.formations.Single(item => item.id == "PLAN Yuan 1");
        yuan.column = hunter.column;
        yuan.row = hunter.row;
        game.ApplySnapshot(snapshot);
        Check(game.Execute(new GameCommand(GameCommandType.Search, Side.UsNavy, game.State.Revision,
                targetId: "PLAN Yuan 1", searchMode: "visual")).Accepted &&
            game.State.Detection.ContactFor(Side.UsNavy, "PLAN Yuan 1").Level == ContactLevel.Located &&
            game.CaptureSnapshotFor(Side.UsNavy).formations.Single(item => item.id == "PLAN Yuan 1").unitIds.Length == 0,
            "Scenario 6 surface search reports no ships without leaking submarine contents");
        Check(game.Execute(new GameCommand(GameCommandType.Search, Side.UsNavy, game.State.Revision,
                targetId: "PLAN Yuan 1", searchMode: "sonar")).Accepted &&
            game.State.Detection.IsClassified(Side.UsNavy, "PLAN Yuan 1"),
            "Scenario 6 sonar classifies an undersea contact");
        Check(game.Execute(new GameCommand(GameCommandType.Attack, Side.UsNavy, game.State.Revision,
                targetId: "PLAN Yuan 1")).Accepted && game.State.Unit("plan-type-039ab-1").IsSunk,
            "Scenario 6 same-hex ASW attack uses the ASW table");

        var ssmGame = new ScenarioOneGame(6608, null, true, false, null, definition);
        var ssmSnapshot = ssmGame.CaptureSnapshot();
        ssmSnapshot.activeSide = Side.Plan;
        ssmSnapshot.activeFormationId = "PLAN Type 093B";
        ssmSnapshot.phase = ActivationPhase.PlayerAction;
        var ssmSub = ssmSnapshot.formations.Single(item => item.id == "PLAN Type 093B");
        var ssmTarget = ssmSnapshot.formations.Single(item => item.id == "US Hunter-Killer Group");
        ssmSub.column = ssmTarget.column;
        ssmSub.row = ssmTarget.row;
        ssmGame.ApplySnapshot(ssmSnapshot);
        ssmGame.State.Detection.Detect(Side.Plan, ssmGame.State.Formation("US Hunter-Killer Group"),
            DetectionMethod.Sonar, ssmGame.State.Turn);
        Check(ssmGame.Execute(new GameCommand(GameCommandType.Attack, Side.Plan, ssmGame.State.Revision,
                targetId: "US Hunter-Killer Group", enabled: true)).Accepted &&
            ssmGame.State.Phase == ActivationPhase.MissileCombat,
            "Scenario 6 submarine may choose normal SSM combat instead of torpedoes");

        var score = new ScenarioOneGame(6607, null, true, false, null, definition);
        var scoreSnapshot = score.CaptureSnapshot();
        scoreSnapshot.units.Single(unit => unit.id == "plan-type-039ab-1").hullDamage = 2;
        scoreSnapshot.units.Single(unit => unit.id == "plan-type-039ab-2").hullDamage = 2;
        scoreSnapshot.units.Single(unit => unit.id == "us-burke-iii").hullDamage = 2;
        scoreSnapshot.units.Single(unit => unit.id == "us-constellation-1").hullDamage = 1;
        score.ApplySnapshot(scoreSnapshot);
        Check(score.CurrentScore().Result == "PLAN VICTORY" && score.CurrentScore().UsTieBreakDamage == 1,
            "Scenario 6 every two US losses offsets one PLAN submarine loss");
        scoreSnapshot = score.CaptureSnapshot();
        scoreSnapshot.turn = 7;
        scoreSnapshot.activeSide = Side.UsNavy;
        scoreSnapshot.activeFormationId = "US Hunter-Killer Group";
        scoreSnapshot.phase = ActivationPhase.PlayerAction;
        scoreSnapshot.usDeclaredSpeed = 0;
        scoreSnapshot.usMovementSpent = 0;
        scoreSnapshot.formations.Single(item => item.id == "US Hunter-Killer Group").declaredSpeed = 0;
        scoreSnapshot.remainingChits = Array.Empty<MovementChitData>();
        score.ApplySnapshot(scoreSnapshot);
        Check(score.Execute(new GameCommand(GameCommandType.EndActivation, Side.UsNavy,
                score.State.Revision)).Accepted && score.State.IsGameOver &&
            score.State.EndReason == ScenarioEndReason.TurnLimit && score.State.Result == "PLAN VICTORY",
            "Scenario 6 resolves the adjusted survival result after seven complete turns");
    }

    private static void ScenarioSevenRoute()
    {
        var definition = FirstIslandChainScenarios.LifelineToTaiwan;
        var game = new ScenarioOneGame(7707, null, true, false, null, definition);
        Check(game.State.MaximumTurns == 10 && game.State.Forces.Count == 7 &&
            game.State.Forces.Count(force => force.Side == Side.UsNavy) == 4 &&
            game.State.Forces.Count(force => force.Side == Side.Plan) == 3 &&
            game.State.MovementCup.TotalCount == 7,
            "Scenario 7 exact independent convoy/submarine forces and ten-turn limit");
        Check(!game.Execute(new GameCommand(GameCommandType.DeployFormation, Side.UsNavy,
                game.State.Revision, new HexCoord(8, 10), formationId: "US Convoy Alpha")).Accepted &&
            game.Execute(new GameCommand(GameCommandType.DeployFormation, Side.UsNavy,
                game.State.Revision, new HexCoord(9, 12), formationId: "US Convoy Alpha")).Accepted,
            "Scenario 7 US setup stays inside its assembly zone but outside the destination");
        Check(!game.Execute(new GameCommand(GameCommandType.DeployFormation, Side.Plan,
                game.State.Revision, new HexCoord(11, 10), formationId: "PLAN Yuan 1")).Accepted &&
            game.Execute(new GameCommand(GameCommandType.DeployFormation, Side.Plan,
                game.State.Revision, new HexCoord(14, 14), formationId: "PLAN Yuan 1")).Accepted,
            "Scenario 7 PLAN setup excludes Taipei and the 0910 two-hex zone");

        var arrival = new ScenarioOneGame(7708, null, true, false, null, definition);
        var arrivalSnapshot = arrival.CaptureSnapshot();
        arrivalSnapshot.activeSide = Side.UsNavy;
        arrivalSnapshot.activeFormationId = "US Convoy Alpha";
        arrivalSnapshot.phase = ActivationPhase.PlayerMove;
        arrivalSnapshot.usDeclaredSpeed = 1;
        var alpha = arrivalSnapshot.formations.Single(item => item.id == "US Convoy Alpha");
        alpha.column = 9;
        alpha.row = 10;
        alpha.declaredSpeed = 1;
        arrival.ApplySnapshot(arrivalSnapshot);
        Check(arrival.Execute(new GameCommand(GameCommandType.Move, Side.UsNavy,
                arrival.State.Revision, new HexCoord(8, 10))).Accepted &&
            arrival.State.Formation("US Convoy Alpha").HasArrived &&
            arrival.State.Formation("US Convoy Alpha").MovementRemaining == 0,
            "Scenario 7 port entry records arrival and ends on-map movement");
        var refreshedCup = new MovementChitCup(new SeededDieRoller(1));
        refreshedCup.Reset(arrival.State.Forces);
        Check(refreshedCup.Remaining.All(chit => chit.FormationId != "US Convoy Alpha"),
            "Scenario 7 arrived convoy receives no future movement chit");

        var scoring = new ScenarioOneGame(7709, null, true, false, null, definition);
        var scoringSnapshot = scoring.CaptureSnapshot();
        scoringSnapshot.formations.Single(item => item.id == "US Convoy Bravo").arrived = true;
        scoringSnapshot.formations.Single(item => item.id == "US Convoy Charlie").arrived = true;
        scoringSnapshot.units.Single(item => item.id == "us-merchant-1").hullDamage = 4;
        scoringSnapshot.units.Single(item => item.id == "plan-type-039ab-1").hullDamage = 2;
        scoring.ApplySnapshot(scoringSnapshot);
        Check(scoring.CurrentScore().Result == "US NAVY VICTORY" &&
            scoring.CurrentScore().UsObjectiveDamage == 2 && scoring.CurrentScore().PlanObjectiveDamage == 1 &&
            scoring.CurrentScore().UsTieBreakDamage == 1 && scoring.CurrentScore().PlanTieBreakDamage == 1,
            "Scenario 7 one submarine loss offsets one lost merchant");
        var planProjection = new ScenarioOneGame(1, null, true);
        planProjection.ApplySnapshot(scoring.CaptureSnapshotFor(Side.Plan));
        Check(planProjection.CurrentScore().Result == "US NAVY VICTORY" &&
            planProjection.CurrentScore().UsObjectiveDamage == 2 &&
            planProjection.CurrentScore().PlanTieBreakDamage == 1,
            "Scenario 7 redacted multiplayer projection retains the public authoritative score");

        scoringSnapshot = scoring.CaptureSnapshot();
        scoringSnapshot.formations.Single(item => item.id == "US Convoy Bravo").arrived = false;
        scoringSnapshot.turn = 10;
        scoringSnapshot.activeSide = Side.UsNavy;
        scoringSnapshot.activeFormationId = "US Replenishment Group";
        scoringSnapshot.phase = ActivationPhase.PlayerAction;
        scoringSnapshot.usDeclaredSpeed = 0;
        scoringSnapshot.formations.Single(item => item.id == "US Replenishment Group").declaredSpeed = 0;
        scoringSnapshot.remainingChits = Array.Empty<MovementChitData>();
        scoring.ApplySnapshot(scoringSnapshot);
        Check(scoring.Execute(new GameCommand(GameCommandType.EndActivation, Side.UsNavy,
                scoring.State.Revision)).Accepted && scoring.State.IsGameOver &&
            scoring.State.EndReason == ScenarioEndReason.TurnLimit && scoring.State.Result == "PLAN VICTORY",
            "Scenario 7 unresolved lifeline ends with PLAN victory after ten complete turns");
    }

    private static void ReleaseVersionRoute()
    {
        Check(ReleaseVersion.IsNewer("v0.1.1", "0.1.0"), "Release tag patch upgrade");
        Check(ReleaseVersion.IsNewer("v1.0.0", "0.9.9"), "Release tag major upgrade");
        Check(!ReleaseVersion.IsNewer("v0.1.0", "0.1.0"), "Equal release is not an update");
        Check(!ReleaseVersion.IsNewer("invalid", "0.1.0"), "Invalid release tag is rejected");
    }

    private static void ScenarioEightRoute()
    {
        var definition = FirstIslandChainScenarios.HuntTheDragon;
        var game = new ScenarioOneGame(8808, null, true, false, null, definition);
        var fujian = game.State.Unit("plan-fujian");
        Check(game.State.MaximumTurns == 7 && game.State.Forces.Count == 8 &&
            game.State.Forces.Count(force => force.Side == Side.UsNavy && force.IsSubmarineOnly) == 4 &&
            game.State.Forces.Count(force => force.Side == Side.Plan) == 4 &&
            game.State.Forces.Where(force => force.Side == Side.Plan)
                .All(force => force.Position.Row == 12 && force.Position.Column >= 8 &&
                    force.Position.Column <= 12 && game.State.Map.IsNavigable(force.Position, Side.Plan)),
            "Scenario 8 exact four-SSN barrier and four-ship PLAN order of battle");
        Check(fujian.Definition.EmbarkedAircraftCapacity == 1 && fujian.EmbarkedAircraftRemaining == 1 &&
            fujian.CanLaunchAircraft, "Scenario 8 Fujian starts with one full embarked air-group capacity unit");

        var entry = game.CaptureSnapshot();
        entry.activeSide = Side.UsNavy;
        entry.activeFormationId = "US Virginia 1";
        entry.phase = ActivationPhase.PlayerMove;
        entry.usDeclaredSpeed = 1;
        var virginia = entry.formations.Single(item => item.id == "US Virginia 1");
        virginia.declaredSpeed = 1;
        virginia.entered = false;
        game.ApplySnapshot(entry);
        Check(game.Execute(new GameCommand(GameCommandType.Move, Side.UsNavy, game.State.Revision,
                new HexCoord(15, 9))).Accepted && game.State.Formation("US Virginia 1").HasEnteredMap,
            "Scenario 8 US submarine enters through a navigable eastern-edge hex");

        var patrol = new ScenarioOneGame(8809, null, true, false, null, definition);
        var patrolSnapshot = patrol.CaptureSnapshot();
        patrolSnapshot.activeSide = Side.Plan;
        patrolSnapshot.activeFormationId = "PLAN Type 055";
        patrolSnapshot.phase = ActivationPhase.PlayerMove;
        var renhai = patrolSnapshot.formations.Single(item => item.id == "PLAN Type 055");
        renhai.column = 9;
        renhai.row = 10;
        renhai.declaredSpeed = 1;
        patrol.ApplySnapshot(patrolSnapshot);
        var outsideBand = patrol.Execute(new GameCommand(GameCommandType.Move, Side.Plan,
            patrol.State.Revision, new HexCoord(9, 9)));
        Check(!outsideBand.Accepted && outsideBand.Violation.Code == RuleViolationCode.ImpassableTerrain,
            "Scenario 8 PLAN movement remains within two hexes of its westbound patrol axis");

        var escape = new ScenarioOneGame(8810, null, true, false, null, definition);
        var escapeSnapshot = escape.CaptureSnapshot();
        escapeSnapshot.activeSide = Side.Plan;
        escapeSnapshot.activeFormationId = "PLAN Fujian";
        escapeSnapshot.phase = ActivationPhase.PlayerMove;
        escapeSnapshot.planDeclaredSpeed = 1;
        var carrierFormation = escapeSnapshot.formations.Single(item => item.id == "PLAN Fujian");
        carrierFormation.column = 3;
        carrierFormation.row = 12;
        carrierFormation.declaredSpeed = 1;
        carrierFormation.entered = true;
        escape.ApplySnapshot(escapeSnapshot);
        Check(escape.CanExitMap(escape.State.Formation("PLAN Fujian")) &&
            escape.Execute(new GameCommand(GameCommandType.ExitMap, Side.Plan, escape.State.Revision)).Accepted &&
            escape.State.IsGameOver && escape.State.Result == "PLAN VICTORY" &&
            escape.State.EndReason == ScenarioEndReason.BoardEdgeExited,
            "Scenario 8 launch-capable Fujian exits the western navigable edge for PLAN victory");

        var missionKill = new ScenarioOneGame(8811, null, true, false, null, definition);
        var killSnapshot = missionKill.CaptureSnapshot();
        killSnapshot.activeSide = Side.Plan;
        killSnapshot.activeFormationId = "PLAN Fujian";
        killSnapshot.phase = ActivationPhase.PlayerMove;
        killSnapshot.planDeclaredSpeed = 1;
        var killedCarrierFormation = killSnapshot.formations.Single(item => item.id == "PLAN Fujian");
        killedCarrierFormation.column = 3;
        killedCarrierFormation.row = 12;
        killedCarrierFormation.declaredSpeed = 1;
        killSnapshot.units.Single(item => item.id == "plan-fujian").hullDamage = 3;
        missionKill.ApplySnapshot(killSnapshot);
        Check(!missionKill.State.Unit("plan-fujian").CanLaunchAircraft &&
            !missionKill.Execute(new GameCommand(GameCommandType.ExitMap, Side.Plan,
                missionKill.State.Revision)).Accepted,
            "Scenario 8 half-damage mission kill prevents Fujian aircraft launch and victory exit");

        killSnapshot = missionKill.CaptureSnapshot();
        killSnapshot.units.Single(item => item.id == "plan-fujian").hullDamage = 6;
        missionKill.ApplySnapshot(killSnapshot);
        Check(missionKill.CurrentScore().Result == "US NAVY VICTORY",
            "Scenario 8 sinking Fujian is a US victory");
        var mirror = new ScenarioOneGame(1, null, true);
        mirror.ApplySnapshot(escape.CaptureSnapshot());
        Check(mirror.State.Scenario.Id == "fic-08" && mirror.State.Result == "PLAN VICTORY" &&
            mirror.State.Formation("PLAN Fujian").HasArrived,
            "Scenario 8 save/network snapshot retains edge exit and carrier victory state");
    }

    private static void ScenarioNineRoute()
    {
        var definition = FirstIslandChainScenarios.Patroller;
        var game = new ScenarioOneGame(9909, null, true, false,
            new SequenceDieRoller(1, 5, 6), definition);
        var poseidon = game.State.Unit("us-p8a");
        Check(game.State.MaximumTurns == 15 && game.State.Forces.Count == 6 &&
              game.State.Forces.Count(force => force.Side == Side.Plan && force.IsSubmarineOnly) == 4,
            "Scenario 9 exact fifteen-turn, four-PLAN-submarine order of battle");
        Check(poseidon.Definition.IsPatrolAircraft && poseidon.Definition.AircraftRadius == 20 &&
              poseidon.Definition.AirSearchRadar == 1 && poseidon.Definition.SurfaceSearchRadar == 3 &&
              poseidon.Definition.Sonar == 4 && poseidon.Definition.AntiSubmarineWarfare == 5 &&
              poseidon.Definition.LongSsm == 2 && poseidon.ServiceableAircraftRemaining == 4,
            "Scenario 9 modern P-8A card and four-box serviceability roster");
        Check(CombatTables.AircraftDamage(1) == AircraftDamageResult.NoEffect &&
              CombatTables.AircraftDamage(2) == AircraftDamageResult.Abort &&
              CombatTables.AircraftDamage(3) == AircraftDamageResult.Abort &&
              CombatTables.AircraftDamage(4) == AircraftDamageResult.ShotDown &&
              CombatTables.AircraftDamage(6) == AircraftDamageResult.ShotDown,
            "Printed patrol-aircraft damage table 1/2-3/4-6");
        Check(ScenarioOneGame.IsLegalDeploymentHex(definition, game.State.Map, Side.Plan, new HexCoord(5, 10)) &&
              !ScenarioOneGame.IsLegalDeploymentHex(definition, game.State.Map, Side.Plan, new HexCoord(15, 20)) &&
              ScenarioOneGame.IsLegalDeploymentHex(definition, game.State.Map, Side.UsNavy, new HexCoord(15, 20)),
            "Scenario 9 Xiamen distance deployment limits");
        Check(!game.Execute(new GameCommand(GameCommandType.DeployFormation, Side.UsNavy,
                game.State.Revision, new HexCoord(15, 20), formationId: "US P-8A Poseidon")).Accepted,
            "Scenario 9 Kadena-based P-8A cannot be redeployed during setup");

        var snapshot = game.CaptureSnapshot();
        snapshot.activeSide = Side.UsNavy;
        snapshot.activeFormationId = "US P-8A Poseidon";
        snapshot.phase = ActivationPhase.AircraftAction;
        var p8Force = snapshot.formations.Single(item => item.id == "US P-8A Poseidon");
        p8Force.column = 9;
        p8Force.row = 4;
        var yuan = snapshot.formations.Single(item => item.id == "PLAN Yuan 1");
        yuan.column = 14;
        yuan.row = 10;
        game.ApplySnapshot(snapshot);
        Check(game.Execute(new GameCommand(GameCommandType.Move, Side.UsNavy, game.State.Revision,
                new HexCoord(14, 10))).Accepted && game.State.Formation("US P-8A Poseidon").Position == new HexCoord(14, 10),
            "P-8A makes an unlimited non-adjacent relocation inside its twenty-hex radius");
        Check(!game.Execute(new GameCommand(GameCommandType.Move, Side.UsNavy, game.State.Revision,
                new HexCoord(15, 20))).Accepted,
            "P-8A patrol model relocates only once on its movement chit");
        Check(game.Execute(new GameCommand(GameCommandType.Search, Side.UsNavy, game.State.Revision,
                targetId: "PLAN Yuan 1", searchMode: "sonar")).Accepted &&
              game.State.Detection.IsClassified(Side.UsNavy, "PLAN Yuan 1"),
            "P-8A dips sonar in its final hex and classifies a submarine contact");
        Check(!game.Execute(new GameCommand(GameCommandType.Search, Side.UsNavy, game.State.Revision,
                targetId: "PLAN Yuan 1", searchMode: "sonar")).Accepted,
            "Each patrol-aircraft sensor searches only once from its station");
        Check(game.Execute(new GameCommand(GameCommandType.Attack, Side.UsNavy, game.State.Revision,
                targetId: "PLAN Yuan 1")).Accepted &&
              game.State.Unit("us-p8a").AircraftLastAttackTurn == game.State.Turn,
            "P-8A conducts an ASW attack against its classified same-hex submarine");
        var secondSortie = game.CaptureSnapshot();
        secondSortie.hasAttacked = false;
        secondSortie.phase = ActivationPhase.AircraftAction;
        secondSortie.activeSide = Side.UsNavy;
        secondSortie.activeFormationId = "US P-8A Poseidon";
        game.ApplySnapshot(secondSortie);
        Check(!game.Execute(new GameCommand(GameCommandType.Attack, Side.UsNavy, game.State.Revision,
                targetId: "PLAN Yuan 1")).Accepted,
            "P-8A cannot make a second ASM or ASW attack in the same turn");

        var mirror = new ScenarioOneGame(1, null, true);
        mirror.ApplySnapshot(game.CaptureSnapshot());
        var restoredP8 = mirror.State.Unit("us-p8a");
        Check(mirror.State.Scenario.Id == "fic-09" && restoredP8.ServiceableAircraftRemaining == 4 &&
              mirror.State.Formation("US P-8A Poseidon").HasUsedAircraftSearch("sonar"),
            "Scenario 9 multiplayer/save snapshot retains aircraft pool and sensor transactions");

        var escape = new ScenarioOneGame(9910, null, true, false, null, definition);
        var eastEdge = escape.State.Map.AllHexes.First(hex =>
            ScenarioOneGame.IsBoardEdgeHex(escape.State.Map, hex, BoardEdge.East, Side.Plan));
        foreach (var id in new[] { "PLAN Yuan 1", "PLAN Yuan 2", "PLAN Yuan 3" })
        {
            var exitSnapshot = escape.CaptureSnapshot();
            exitSnapshot.activeSide = Side.Plan;
            exitSnapshot.activeFormationId = id;
            exitSnapshot.phase = ActivationPhase.PlayerMove;
            var formation = exitSnapshot.formations.Single(item => item.id == id);
            formation.column = eastEdge.Column;
            formation.row = eastEdge.Row;
            formation.declaredSpeed = 1;
            formation.movementSpent = 0;
            escape.ApplySnapshot(exitSnapshot);
            var exitResult = escape.Execute(new GameCommand(GameCommandType.ExitMap, Side.Plan,
                escape.State.Revision));
            Check(exitResult.Accepted, $"Scenario 9 {id} exits the east edge ({exitResult.Summary})");
        }
        Check(escape.State.IsGameOver && escape.State.Result == "PLAN VICTORY" &&
              escape.State.EndReason == ScenarioEndReason.BoardEdgeExited &&
              escape.CurrentScore().UsObjectiveDamage == 3,
            "Three PLAN submarine escapes immediately resolve Scenario 9 victory");

        var turnLimit = new ScenarioOneGame(9911, null, true, false, null, definition);
        var final = turnLimit.CaptureSnapshot();
        final.turn = 15;
        final.activeSide = Side.UsNavy;
        final.activeFormationId = "US Los Angeles";
        final.phase = ActivationPhase.PlayerAction;
        final.remainingChits = Array.Empty<MovementChitData>();
        var la = final.formations.Single(item => item.id == "US Los Angeles");
        la.declaredSpeed = 0;
        la.movementSpent = 0;
        turnLimit.ApplySnapshot(final);
        Check(turnLimit.Execute(new GameCommand(GameCommandType.EndActivation, Side.UsNavy,
                turnLimit.State.Revision)).Accepted && turnLimit.State.IsGameOver &&
              turnLimit.State.EndReason == ScenarioEndReason.TurnLimit &&
              turnLimit.State.Result == "US NAVY VICTORY",
            "Fewer than three escapes after Turn 15 gives Scenario 9 victory to the US");
    }

    private static void ScenarioTenRoute()
    {
        var definition = FirstIslandChainScenarios.FirstLight;
        var game = new ScenarioOneGame(1010, null, true, false, new SequenceDieRoller(1, 1), definition);
        Check(game.State.MaximumTurns == 12 && game.State.Forces.Count == 3 &&
              game.State.Unit("us-ford") != null && game.State.Unit("plan-type-093b-first-light-2") != null,
            "Scenario 10 printed carrier group, two Type 093B submarines, and twelve-turn limit");
        Check(game.State.TacticalFlights.Count == 15 && game.State.AirBases.Count == 2 &&
              game.State.TacticalFlights.Count(item => item.Side == Side.UsNavy) == 12,
            "Scenario 10 carrier wing and Ningbo tactical-flight pools");
        Check(ModernTacticalAircraftDatabase.Get("us-f35c").Radius == 10 &&
              ModernTacticalAircraftDatabase.Get("plan-h6j").LongAsm == 5 &&
              ModernAirBaseDatabase.Get("plan-ningbo").LongSam == 10 &&
              ModernAirBaseDatabase.Get("us-ford-wing").FlightCapacity == 14,
            "Scenario 10 modernized tactical aircraft, base defenses, and deck capacity");
        Check(CombatTables.AirToAirHits(2) == 0 && CombatTables.AirToAirHits(3) == 1 &&
              CombatTables.AirToAirHits(7) == 1 && CombatTables.AirToAirHits(8) == 2,
            "Complete printed air-to-air modified-result table");
        Check(game.Execute(new GameCommand(GameCommandType.AssignCap, Side.UsNavy, game.State.Revision,
                sourceUnitId: "FORD-F35-1", enabled: true)).Accepted &&
              game.State.TacticalFlight("FORD-F35-1").Mission == TacticalAirMission.Cap,
            "Full ready fighter flight declares persistent radar CAP before first chit");
        Check(game.Execute(new GameCommand(GameCommandType.AssignDeckInterceptor, Side.UsNavy, game.State.Revision,
                sourceUnitId: "FORD-F35-2")).Accepted &&
              game.State.TacticalFlight("FORD-F35-2").Mission == TacticalAirMission.DeckInterceptor,
            "Full ready carrier fighter flight declares DLI before first chit");
        var mirror = new ScenarioOneGame(1, null, true);
        mirror.ApplySnapshot(game.CaptureSnapshot());
        Check(mirror.State.Scenario.Id == "fic-10" &&
              mirror.State.TacticalFlight("FORD-F35-1").Mission == TacticalAirMission.Cap &&
              mirror.State.TacticalFlight("FORD-F35-2").Mission == TacticalAirMission.DeckInterceptor,
            "Scenario 10 save/network snapshot retains flight missions and radar state");
        var planProjection = game.CaptureSnapshotFor(Side.Plan);
        var hiddenUsCap = planProjection.tacticalFlights.Single(item => item.id == "FORD-F35-1");
        Check(hiddenUsCap.mission == TacticalAirMission.Ready && !hiddenUsCap.radarOn &&
              hiddenUsCap.flownAircraft == 0,
            "Scenario 10 side-private snapshot hides opposing CAP, DLI, and sortie state");

        var rolls = Enumerable.Repeat(1, 20).Concat(new[] { 6, 6 }).ToArray();
        var bombing = new ScenarioOneGame(1011, null, true, false, new SequenceDieRoller(rolls), definition);
        var strikeState = bombing.CaptureSnapshot();
        strikeState.activeSide = Side.UsNavy;
        strikeState.activeFormationId = "US Ford Strike Group";
        strikeState.phase = ActivationPhase.PlayerAction;
        var fordForce = strikeState.formations.Single(item => item.id == "US Ford Strike Group");
        fordForce.declaredSpeed = 0;
        bombing.ApplySnapshot(strikeState);
        var strike = bombing.Execute(new GameCommand(GameCommandType.LaunchTacticalStrike, Side.UsNavy,
            bombing.State.Revision, factors: 1, targetId: "plan-ningbo", sourceUnitId: "FORD-F18-1",
            searchMode: TacticalWeapon.Bombs.ToString()));
        Check(strike.Accepted && bombing.State.LastTacticalStrike.AircraftLaunched == 1 &&
              bombing.State.LastTacticalStrike.RunwayHits == 4,
            $"Bomb strike follows LR/SR aircraft defenses, omits point defense, and damages runway " +
            $"(accepted={strike.Accepted}, summary={strike.Summary}, runway={bombing.State.LastTacticalStrike?.RunwayHits})");
        Check(bombing.State.TacticalFlight("FORD-F18-1").Mission == TacticalAirMission.Flown &&
              !bombing.Execute(new GameCommand(GameCommandType.LaunchTacticalStrike, Side.UsNavy,
                  bombing.State.Revision, factors: 1, targetId: "plan-ningbo", sourceUnitId: "FORD-F18-1",
                  searchMode: TacticalWeapon.Bombs.ToString())).Accepted,
            "A tactical aircraft attacks only once per turn and returns to its base pool");
        var bombMirror = new ScenarioOneGame(1, null, true);
        bombMirror.ApplySnapshot(bombing.CaptureSnapshot());
        Check(bombMirror.State.AirBase("plan-ningbo").RunwayHits == 4 &&
              bombMirror.State.TacticalFlight("FORD-F18-1").Mission == TacticalAirMission.Flown,
            "Runway damage and flown aircraft survive save/network synchronization");

        var capDice = new[] { 1, 5 }.Concat(Enumerable.Repeat(1, 24)).ToArray();
        var capDefense = new ScenarioOneGame(10115, null, true, false,
            new SequenceDieRoller(capDice), definition);
        Check(capDefense.Execute(new GameCommand(GameCommandType.AssignCap, Side.Plan,
            capDefense.State.Revision, sourceUnitId: "NINGBO-J16-1", enabled: true)).Accepted,
            "PLAN declares a persistent Ningbo CAP");
        var capState = capDefense.CaptureSnapshot();
        capState.activeSide = Side.UsNavy;
        capState.activeFormationId = "US Ford Strike Group";
        capState.phase = ActivationPhase.PlayerAction;
        capState.formations.Single(item => item.id == "US Ford Strike Group").declaredSpeed = 0;
        capDefense.ApplySnapshot(capState);
        var capStrike = capDefense.Execute(new GameCommand(GameCommandType.LaunchTacticalStrike,
            Side.UsNavy, capDefense.State.Revision, factors: 1, targetId: "plan-ningbo",
            sourceUnitId: "FORD-F35-1", searchMode: TacticalWeapon.LongAsm.ToString()));
        Check(capStrike.Accepted && capDefense.State.LastTacticalStrike.MissileFactors == 2 &&
              capDefense.State.LastTacticalStrike.MissileFactorsIntercepted >= 2 &&
              capDefense.State.Transactions.Any(item => item.Detail.Contains("Defense 0")),
            "Persistent CAP attacks tactical aircraft and then air-launched missiles at Defense zero");

        var detection = new ScenarioOneGame(10116, null, true, false,
            new SequenceDieRoller(6, 6), definition);
        var detectionState = detection.CaptureSnapshot();
        detectionState.activeSide = Side.UsNavy;
        detectionState.activeFormationId = "US Ford Strike Group";
        detectionState.phase = ActivationPhase.PlayerAction;
        detectionState.formations.Single(item => item.id == "US Ford Strike Group").declaredSpeed = 0;
        detection.ApplySnapshot(detectionState);
        var hiddenStrike = detection.Execute(new GameCommand(GameCommandType.LaunchTacticalStrike,
            Side.UsNavy, detection.State.Revision, factors: 1, targetId: "PLAN Type 093B 1",
            sourceUnitId: "FORD-F35-1", formationId: "plan-type-093b-first-light-1",
            searchMode: TacticalWeapon.LongAsm.ToString()));
        Check(!hiddenStrike.Accepted && hiddenStrike.Violation.Code == RuleViolationCode.TargetUndetected,
            "Tactical strike against a formation requires a friendly detection");
        detection.State.Detection.Detect(Side.UsNavy, detection.State.Formation("PLAN Type 093B 1"),
            DetectionMethod.Esm, detection.State.Turn);
        var detectedStrike = detection.Execute(new GameCommand(GameCommandType.LaunchTacticalStrike,
            Side.UsNavy, detection.State.Revision, factors: 1, targetId: "PLAN Type 093B 1",
            sourceUnitId: "FORD-F35-1", formationId: "plan-type-093b-first-light-1",
            searchMode: TacticalWeapon.LongAsm.ToString()));
        Check(detectedStrike.Accepted && detection.State.Unit("plan-type-093b-first-light-1").IsSunk,
            "Detected tactical target accepts air-launched ASM and normal impact damage");

        var arrival = new ScenarioOneGame(1012, null, true, false, null, definition);
        var arrivalState = arrival.CaptureSnapshot();
        arrivalState.activeSide = Side.UsNavy;
        arrivalState.activeFormationId = "US Ford Strike Group";
        arrivalState.phase = ActivationPhase.PlayerAction;
        arrivalState.remainingChits = Array.Empty<MovementChitData>();
        arrivalState.formations.Single(item => item.id == "US Ford Strike Group").column = 4;
        arrivalState.formations.Single(item => item.id == "US Ford Strike Group").row = 6;
        arrivalState.formations.Single(item => item.id == "US Ford Strike Group").declaredSpeed = 0;
        arrival.ApplySnapshot(arrivalState);
        var arrivalResult = arrival.Execute(new GameCommand(GameCommandType.EndActivation, Side.UsNavy,
                arrival.State.Revision));
        Check(arrivalResult.Accepted && arrival.State.Result == "US NAVY VICTORY" &&
              arrival.State.EndReason == ScenarioEndReason.DestinationReached,
            $"Launch-capable Ford within two hexes of 0206 wins First Light (accepted={arrivalResult.Accepted}, " +
            $"summary={arrivalResult.Summary}, result={arrival.State.Result}, reason={arrival.State.EndReason}, " +
            $"distance={arrival.State.Formation("US Ford Strike Group").Position.DistanceTo(definition.CarrierObjectiveHex)}, " +
            $"canLaunch={arrival.State.Unit("us-ford").CanLaunchAircraft}, cup={arrival.State.MovementCup.Remaining.Count})");

        var missionKill = new ScenarioOneGame(1013, null, true, false, null, definition);
        var finalState = missionKill.CaptureSnapshot();
        finalState.turn = 12;
        finalState.activeSide = Side.UsNavy;
        finalState.activeFormationId = "US Ford Strike Group";
        finalState.phase = ActivationPhase.PlayerAction;
        finalState.remainingChits = Array.Empty<MovementChitData>();
        var finalFord = finalState.formations.Single(item => item.id == "US Ford Strike Group");
        finalFord.column = 4;
        finalFord.row = 6;
        finalFord.declaredSpeed = 0;
        finalState.units.Single(item => item.id == "us-ford").hullDamage = 3;
        missionKill.ApplySnapshot(finalState);
        Check(!missionKill.State.Unit("us-ford").CanLaunchAircraft &&
              missionKill.Execute(new GameCommand(GameCommandType.EndActivation, Side.UsNavy,
                  missionKill.State.Revision)).Accepted && missionKill.State.Result == "PLAN VICTORY" &&
              missionKill.State.EndReason == ScenarioEndReason.TurnLimit,
            "Half-damaged launch-prohibited Ford cannot satisfy the objective at the Turn 12 limit");
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
