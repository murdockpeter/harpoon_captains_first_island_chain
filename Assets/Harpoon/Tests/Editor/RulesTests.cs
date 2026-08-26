using System.Linq;
using NUnit.Framework;

namespace Harpoon.Core.Tests
{
    public sealed class RulesTests
    {
        [Test]
        public void ScenarioTwoLoadsPrintedFormationAndScoresAllHullHits()
        {
            var definition = FirstIslandChainScenarios.FlagshipDuel;
            var battle = ScenarioOne.Create(false, definition);
            Assert.That(battle.Scenario.Id, Is.EqualTo("fic-02"));
            Assert.That(battle.Player.Position, Is.EqualTo(new HexCoord(12, 13)));
            Assert.That(battle.Enemy.Position, Is.EqualTo(new HexCoord(5, 10)));
            Assert.That(battle.Player.Units.Select(unit => unit.Definition.Id), Is.EquivalentTo(new[]
            {
                "us-burke-iia-1", "us-burke-iia-2", "us-ticonderoga", "us-san-antonio"
            }));
            Assert.That(battle.Enemy.Units.Single().Definition.Id, Is.EqualTo("plan-type-055"));

            var game = new ScenarioOneGame(2, null, true, false, null, definition);
            var snapshot = game.CaptureSnapshot();
            snapshot.units.Single(unit => unit.id == "plan-type-055").hullDamage = 1;
            snapshot.units.Single(unit => unit.id == "us-burke-iia-1").hullDamage = 1;
            snapshot.units.Single(unit => unit.id == "us-san-antonio").hullDamage = 1;
            game.ApplySnapshot(snapshot);
            Assert.That(game.CurrentScore().UsObjectiveDamage, Is.EqualTo(1));
            Assert.That(game.CurrentScore().PlanObjectiveDamage, Is.EqualTo(2));
            Assert.That(game.CurrentScore().Result, Is.EqualTo("PLAN VICTORY"));
        }

        [Test]
        public void ScenarioThreeScoresGunfireButNotMissileDamage()
        {
            var definition = FirstIslandChainScenarios.CloseAboard;
            var battle = ScenarioOne.Create(false, definition);
            Assert.That(battle.Player.Position, Is.EqualTo(new HexCoord(13, 13)));
            Assert.That(battle.Enemy.Position, Is.EqualTo(new HexCoord(10, 10)));
            Assert.That(battle.Player.Units.Count, Is.EqualTo(2));
            Assert.That(battle.Enemy.Units.Count, Is.EqualTo(3));

            var game = new ScenarioOneGame(3, null, true, false, null, definition);
            var snapshot = game.CaptureSnapshot();
            var plan = snapshot.units.Single(unit => unit.id == "plan-type-056a-1");
            plan.hullDamage = 1;
            plan.gunfireHullDamage = 0;
            var us = snapshot.units.Single(unit => unit.id == "us-constellation");
            us.hullDamage = 1;
            us.gunfireHullDamage = 1;
            game.ApplySnapshot(snapshot);
            Assert.That(game.CurrentScore().UsObjectiveDamage, Is.Zero);
            Assert.That(game.CurrentScore().PlanObjectiveDamage, Is.EqualTo(1));
            Assert.That(game.CurrentScore().Result, Is.EqualTo("PLAN VICTORY"));
        }

        [Test]
        public void ScenarioFourRedactsUndetectedPicketAndRecognizesArrival()
        {
            var definition = FirstIslandChainScenarios.PicketLine;
            var game = new ScenarioOneGame(4, null, true, false, null, definition);
            var hidden = game.CaptureSnapshotFor(Side.UsNavy);
            Assert.That(hidden.formations.Single(item => item.side == Side.Plan).unitIds, Is.Empty);
            Assert.That(hidden.formations.Single(item => item.side == Side.Plan).column, Is.Zero);
            Assert.That(hidden.units.Any(item => item.id.StartsWith("plan-")), Is.False);

            game.State.Detection.Detect(Side.UsNavy, game.State.Enemy,
                DetectionMethod.SurfaceSearchRadar, game.State.Turn);
            var known = game.CaptureSnapshotFor(Side.UsNavy);
            Assert.That(known.formations.Single(item => item.side == Side.Plan).unitIds.Length, Is.EqualTo(3));
            Assert.That(known.formations.Single(item => item.side == Side.Plan).column, Is.EqualTo(15));
        }

        [Test]
        public void ScenarioStartsThreeHexesApart()
        {
            var battle = ScenarioOne.Create();
            Assert.That(battle.Player.Position.DistanceTo(battle.Enemy.Position), Is.EqualTo(3));
        }

        [Test]
        public void TaskForceSpeedIsLimitedBySlowestShip()
        {
            Assert.That(ScenarioOne.Create().Player.EffectiveSpeed, Is.EqualTo(2));
        }

        [Test]
        public void LayeredDefenseCanStopEntireRaid()
        {
            var battle = ScenarioOne.Create();
            battle.Player.MoveTo(new HexCoord(9, 10));
            var resolver = new CombatResolver(new SequenceDieRoller(4, 4, 4, 4, 4, 4, 4, 4));
            var report = resolver.Attack(battle.Player, battle.Enemy);
            Assert.That(report.AttackFactors, Is.EqualTo(3));
            Assert.That(report.InterceptedFactors, Is.EqualTo(3));
            Assert.That(report.HullHits, Is.Zero);
        }

        [Test]
        public void PlayerCannotMoveBeyondSlowestShipSpeed()
        {
            var game = new ScenarioOneGame(1, null, true);
            Assert.That(game.DrawMovementChit().Accepted, Is.True);
            Assert.That(game.DeclareSpeed(game.State.ActiveSide, 2).Accepted, Is.True);
            var moved = game.TryMovePlayer(new HexCoord(9, 11), out var reason);
            Assert.That(moved, Is.False);
            StringAssert.Contains("adjacent", reason.ToLowerInvariant());
        }

        [Test]
        public void ScenarioOneCardDataMatchesSupplement()
        {
            var battle = ScenarioOne.Create();
            var burke = battle.Player.Units[0].Definition;
            Assert.That(burke.AirSearchRadar, Is.EqualTo(2));
            Assert.That(burke.ShortSam, Is.EqualTo(3));
            Assert.That(burke.LongSam, Is.EqualTo(8));
            Assert.That(burke.ShortSsm, Is.EqualTo(2));
            Assert.That(burke.LongSsm, Is.EqualTo(1));
            Assert.That(burke.Sonar, Is.EqualTo(4));
            Assert.That(burke.AntiSubmarineWarfare, Is.EqualTo(5));
            Assert.That(battle.Enemy.Units[1].Definition.Guns, Is.EqualTo(1));
            Assert.That(battle.MaximumTurns, Is.Zero);
        }

        [Test]
        public void DamageThresholdsFollowFullRules()
        {
            var definition = new UnitDefinition("test", "Test", Side.UsNavy, UnitRole.Escort,
                4, 6, 3, 2, 4, 3, 3, 5, 2, 1, 4, 2);
            var ship = new UnitState(definition);
            ship.ApplyDamage(3);
            Assert.That(ship.HasHalfDamage, Is.True);
            Assert.That(ship.HasTwoThirdsDamage, Is.False);
            Assert.That(ship.EffectiveLongSam, Is.Zero);
            Assert.That(ship.EffectiveShortSam, Is.EqualTo(4));
            Assert.That(ship.EffectiveGuns, Is.EqualTo(3));
            ship.ApplyDamage(1);
            Assert.That(ship.HasTwoThirdsDamage, Is.True);
            Assert.That(ship.EffectiveShortSam, Is.Zero);
            Assert.That(ship.EffectivePointDefense, Is.Zero);
            Assert.That(ship.EffectiveGuns, Is.EqualTo(2));
            Assert.That(ship.EffectiveSonar, Is.Zero);
            Assert.That(ship.EffectiveAntiSubmarineWarfare, Is.Zero);
            Assert.That(ship.EffectiveSurfaceSearchRadar, Is.EqualTo(1));
        }

        [Test]
        public void DamageThresholdRoundingCoversEveryModernHullRating()
        {
            var expected = new[,]
            {
                { 1, 1 }, { 1, 2 }, { 2, 2 }, { 2, 3 }, { 3, 4 }, { 3, 4 }
            };
            for (var hull = 1; hull <= 6; hull++)
            {
                Assert.That(UnitState.HalfDamageThresholdFor(hull), Is.EqualTo(expected[hull - 1, 0]));
                Assert.That(UnitState.TwoThirdsDamageThresholdFor(hull), Is.EqualTo(expected[hull - 1, 1]));
            }
        }

        [Test]
        public void ModernPlatformDatabaseCoversEveryHullBearingSupplementCard()
        {
            Assert.That(ModernPlatformDatabase.All.Count, Is.EqualTo(34));
            Assert.That(ModernPlatformDatabase.All.Select(item => item.Id).Distinct().Count(), Is.EqualTo(34));
            Assert.That(ModernPlatformDatabase.All.Select(item => item.Hull).Distinct().OrderBy(value => value),
                Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6 }));
            Assert.That(ModernPlatformDatabase.Get("us-ford").Hull, Is.EqualTo(6));
            Assert.That(ModernPlatformDatabase.Get("plan-type-093b").Torpedoes, Is.EqualTo(5));
            Assert.That(ModernPlatformDatabase.All.Count(item => item.LaunchesAircraft), Is.EqualTo(8));
        }

        [Test]
        public void TwoThirdsDamageLeavesOnlyHalfGunsAndSurfaceRadar()
        {
            var definition = new UnitDefinition("cripple", "Cripple Test", Side.UsNavy,
                UnitRole.Escort, 4, 6, 3, 2, 4, 3, 3, 6, 2, 1, 4, 5,
                esmEquipped: true, isAircraftCarrier: true, torpedoes: 4);
            var ship = new UnitState(definition);
            ship.ApplyDamage(4);
            Assert.That(ship.DamageLevel, Is.EqualTo(ShipDamageLevel.TwoThirdsDamage));
            Assert.That(ship.EffectiveSpeed, Is.EqualTo(1));
            Assert.That(ship.EffectiveGuns, Is.EqualTo(2));
            Assert.That(ship.EffectiveSurfaceSearchRadar, Is.EqualTo(1));
            Assert.That(ship.EffectiveShortSam, Is.Zero);
            Assert.That(ship.EffectiveLongSam, Is.Zero);
            Assert.That(ship.EffectivePointDefense, Is.Zero);
            Assert.That(ship.AvailableShortSsm, Is.Zero);
            Assert.That(ship.AvailableLongSsm, Is.Zero);
            Assert.That(ship.EffectiveTorpedoes, Is.Zero);
            Assert.That(ship.EffectiveAntiSubmarineWarfare, Is.Zero);
            Assert.That(ship.EffectiveSonar, Is.Zero);
            Assert.That(ship.EffectiveEsm, Is.False);
            Assert.That(ship.CanLaunchAircraft, Is.False);
        }

        [Test]
        public void EveryCombatTableRowMatchesTheRulesFixture()
        {
            var expected = new[,]
            {
                { 0, 0, 0, 1, 1, 2 }, // SAM
                { 0, 1, 1, 1, 1, 2 }, // point defense
                { 0, 0, 1, 1, 1, 2 }, // bombs and SSM
                { 0, 0, 0, 1, 1, 1 }, // guns
                { 0, 0, 1, 1, 1, 2 }, // torpedoes
                { 0, 0, 0, 1, 1, 2 }  // ASW
            };
            for (var column = 0; column < expected.GetLength(0); column++)
            for (var roll = 1; roll <= 6; roll++)
                Assert.That(CombatTables.Hits((CombatTableColumn)column, roll),
                    Is.EqualTo(expected[column, roll - 1]), $"column {column}, roll {roll}");
        }

        [Test]
        public void ScreenedGunTargetSubtractsOneFromEveryDie()
        {
            var firing = new UnitState(new UnitDefinition("gun", "Gun Ship", Side.UsNavy,
                UnitRole.Escort, 0, 0, 0, 0, 0, 2, 2, 4));
            var target = new UnitState(new UnitDefinition("target", "Screened Ship", Side.Plan,
                UnitRole.Objective, 0, 0, 0, 0, 0, 0, 2, 4));
            var report = new GunCombatResolver(new SequenceDieRoller(4, 4)).Fire(firing, target, true);
            Assert.That(report.HullHits, Is.Zero);
            Assert.That(report.TargetWasScreened, Is.True);
            Assert.That(target.HullRemaining, Is.EqualTo(4));
        }

        [Test]
        public void GunEngageAndBreakOffThresholdsMatchCaptainRules()
        {
            Assert.That(GunCombatRules.InitialEngagementSucceeds(2, 2, 3), Is.True);
            Assert.That(GunCombatRules.InitialEngagementSucceeds(2, 2, 4), Is.False);
            Assert.That(GunCombatRules.InitialEngagementSucceeds(3, 2, 6), Is.True);
            Assert.That(GunCombatRules.InitialEngagementSucceeds(2, 3, 1), Is.False);
            Assert.That(GunCombatRules.BreakOffThreshold(3, 2), Is.EqualTo(6));
            Assert.That(GunCombatRules.BreakOffThreshold(2, 2), Is.EqualTo(2));
            Assert.That(GunCombatRules.BreakOffThreshold(1, 2), Is.EqualTo(1));
        }

        [Test]
        public void ScenarioOneUsesCombatantDamageOnlyAsATieBreak()
        {
            Assert.That(ScenarioOneGame.CompareScore(2, 1, 0, 5), Is.EqualTo("US NAVY VICTORY"));
            Assert.That(ScenarioOneGame.CompareScore(1, 1, 2, 0), Is.EqualTo("US NAVY VICTORY"));
            Assert.That(ScenarioOneGame.CompareScore(1, 1, 0, 2), Is.EqualTo("PLAN VICTORY"));
            Assert.That(ScenarioOneGame.CompareScore(0, 0, 0, 0), Is.EqualTo("DRAW"));
        }

        [Test]
        public void DebugTraceCapturesDiceAndRejectedCommandsInOrder()
        {
            var game = new ScenarioOneGame(1, null, true);
            game.TryMovePlayer(new HexCoord(3, 13), out _);
            Assert.That(game.State.Transactions.Count, Is.GreaterThanOrEqualTo(5));
            Assert.That(game.State.Transactions[0].Category, Is.EqualTo("SETUP"));
            Assert.That(game.State.Transactions.Exists(item => item.Category == "TURN"), Is.True);
            Assert.That(game.State.Transactions.Exists(item => item.Category == "REJECTED"), Is.True);
            for (var index = 0; index < game.State.Transactions.Count; index++)
                Assert.That(game.State.Transactions[index].Sequence, Is.EqualTo(index + 1));
        }

        [Test]
        public void MultiplayerSnapshotReproducesAuthoritativeState()
        {
            var host = new ScenarioOneGame(1, null, true);
            Assert.That(host.DrawMovementChit().Accepted, Is.True);
            var force = host.State.ForceFor(host.State.ActiveSide);
            Assert.That(host.DeclareSpeed(host.State.ActiveSide, 1).Accepted, Is.True);
            var destination = host.State.Map.NavigableNeighbors(force.Position, host.State.ActiveSide).First();
            Assert.That(host.TryMove(host.State.ActiveSide, destination, out _), Is.True);
            var client = new ScenarioOneGame(99, null, true);
            client.ApplySnapshot(host.CaptureSnapshot());
            Assert.That(client.State.ActiveSide, Is.EqualTo(host.State.ActiveSide));
            Assert.That(client.State.Phase, Is.EqualTo(host.State.Phase));
            Assert.That(client.State.Player.Position, Is.EqualTo(host.State.Player.Position));
            Assert.That(client.State.Transactions.Count, Is.EqualTo(host.State.Transactions.Count));
        }

        [Test]
        public void ManualOpponentPassesActivationBetweenBothSides()
        {
            var game = new ScenarioOneGame(1, null, true);
            Assert.That(game.DrawMovementChit().Accepted, Is.True);
            var firstSide = game.State.ActiveSide;
            var force = game.State.ForceFor(firstSide);
            Assert.That(game.DeclareSpeed(firstSide, 1).Accepted, Is.True);
            var destination = game.State.Map.NavigableNeighbors(force.Position, firstSide).First();
            Assert.That(game.TryMove(firstSide, destination, out _), Is.True);
            game.EndActivation(firstSide);
            Assert.That(game.State.ActiveSide, Is.Not.EqualTo(firstSide));
            Assert.That(game.State.Phase, Is.EqualTo(ActivationPhase.DeclareSpeed));
        }

        [Test]
        public void CommandsAreRevisionedAndEmitTypedEvents()
        {
            IRulesEngine engine = new ScenarioOneGame(1, null, true);
            Assert.That(engine.Execute(new GameCommand(GameCommandType.DrawMovementChit,
                Side.UsNavy, 0)).Accepted, Is.True);
            var side = engine.State.ActiveSide;
            var declaration = engine.Execute(new GameCommand(GameCommandType.DeclareSpeed, side, 1,
                declaredSpeed: 1, id: "typed-speed-test"));
            Assert.That(declaration.Accepted, Is.True);
            var destination = engine.State.Map.NavigableNeighbors(engine.State.ForceFor(side).Position, side).First();
            var command = new GameCommand(GameCommandType.Move, side, 2,
                destination, id: "typed-event-test");
            var result = engine.Execute(command);

            Assert.That(result.Accepted, Is.True);
            Assert.That(engine.State.Revision, Is.EqualTo(3));
            Assert.That(engine.State.CommandLog.Count, Is.EqualTo(3));
            Assert.That(result.Events, Has.Some.Matches<RuleEvent>(item =>
                item.Type == RuleEventType.Movement && item.CommandId == command.Id));
            Assert.That(result.Events, Has.Some.Matches<RuleEvent>(item =>
                item.Type == RuleEventType.CommandAccepted && item.CommandId == command.Id));
        }

        [Test]
        public void IllegalCommandsReturnStableViolationCodes()
        {
            IRulesEngine engine = new ScenarioOneGame(1, null, true);
            var side = engine.State.ActiveSide;
            var stale = engine.Execute(new GameCommand(GameCommandType.Move, side, 99,
                engine.State.ForceFor(side).Position));
            var other = side == Side.UsNavy ? Side.Plan : Side.UsNavy;
            var wrongSide = engine.Execute(new GameCommand(GameCommandType.Move, other, 0,
                engine.State.ForceFor(other).Position));

            Assert.That(stale.Violation.Code, Is.EqualTo(RuleViolationCode.StaleRevision));
            Assert.That(wrongSide.Violation.Code, Is.EqualTo(RuleViolationCode.WrongSide));
            Assert.That(engine.State.Revision, Is.Zero);
        }

        [Test]
        public void SeedAndAcceptedCommandLogReplayAuthoritativeState()
        {
            var original = new ScenarioOneGame(1, null, true);
            original.DrawMovementChit();
            var side = original.State.ActiveSide;
            original.Execute(new GameCommand(GameCommandType.DeclareSpeed, side, original.State.Revision,
                declaredSpeed: 1, id: "replay-speed"));
            var destination = original.State.Map.NavigableNeighbors(original.State.ForceFor(side).Position, side).First();
            original.Execute(new GameCommand(GameCommandType.Move, side, original.State.Revision,
                destination, id: "replay-move"));
            original.Execute(new GameCommand(GameCommandType.EndActivation, side,
                original.State.Revision, id: "replay-end"));

            var replay = ScenarioOneGame.Replay(original.Seed, original.State.CommandLog);
            Assert.That(replay.State.Revision, Is.EqualTo(original.State.Revision));
            Assert.That(replay.State.ActiveSide, Is.EqualTo(original.State.ActiveSide));
            Assert.That(replay.State.Phase, Is.EqualTo(original.State.Phase));
            Assert.That(replay.State.Player.Position, Is.EqualTo(original.State.Player.Position));
            Assert.That(replay.State.Enemy.Position, Is.EqualTo(original.State.Enemy.Position));
        }

        [Test]
        public void SideViewCanHideUnknownOpponentDetails()
        {
            IRulesEngine engine = new ScenarioOneGame(1, null, true);
            var view = engine.ViewFor(Side.UsNavy, false);
            Assert.That(view.OwnFormation.IsKnown, Is.True);
            Assert.That(view.OwnFormation.Units, Is.Not.Empty);
            Assert.That(view.OpposingFormation.IsKnown, Is.False);
            Assert.That(view.OpposingFormation.Id, Is.EqualTo("UNKNOWN CONTACT"));
            Assert.That(view.OpposingFormation.Units, Is.Empty);
        }

        [Test]
        public void SupplementMapUsesAxialTopologyAndCompleteBounds()
        {
            var map = FirstIslandChainMap.Instance;
            Assert.That(map.AllHexes.Count(), Is.EqualTo(300));
            Assert.That(new HexCoord(10, 10).DistanceTo(new HexCoord(7, 13)), Is.EqualTo(3));
            Assert.That(map.TerrainAt(new HexCoord(8, 13)), Is.EqualTo(TerrainType.Land));
            Assert.That(map.Bases.Count, Is.EqualTo(6));
        }

        [Test]
        public void MovementRequiresDeclarationAndOneAdjacentNavigableHex()
        {
            var game = new ScenarioOneGame(1, null, true);
            Assert.That(game.DrawMovementChit().Accepted, Is.True);
            var side = game.State.ActiveSide;
            var beforeDeclaration = game.Execute(new GameCommand(GameCommandType.Move, side,
                game.State.Revision, new HexCoord(7, 12)));
            Assert.That(beforeDeclaration.Violation.Code, Is.EqualTo(RuleViolationCode.SpeedNotDeclared));
            Assert.That(game.DeclareSpeed(side, 2).Accepted, Is.True);
            var teleport = game.Execute(new GameCommand(GameCommandType.Move, side,
                game.State.Revision, new HexCoord(9, 11)));
            var land = game.Execute(new GameCommand(GameCommandType.Move, side,
                game.State.Revision, new HexCoord(8, 13)));
            Assert.That(teleport.Violation.Code, Is.EqualTo(RuleViolationCode.NotAdjacent));
            Assert.That(land.Violation.Code, Is.EqualTo(RuleViolationCode.ImpassableTerrain));
            Assert.That(game.TryMove(side, new HexCoord(7, 12), out _), Is.True);
            Assert.That(game.State.ForceFor(side).MovementRemaining, Is.EqualTo(1));
        }

        [Test]
        public void CoastlinePathUsesOnlyAdjacentNavigableSteps()
        {
            var map = FirstIslandChainMap.Instance;
            var origin = new HexCoord(7, 13);
            var destination = new HexCoord(9, 13);
            var path = map.FindPath(origin, destination, Side.UsNavy);
            Assert.That(path.Count - 1, Is.GreaterThan(origin.DistanceTo(destination)));
            Assert.That(path, Has.All.Matches<HexCoord>(hex => map.IsNavigable(hex, Side.UsNavy)));
            for (var index = 1; index < path.Count; index++)
                Assert.That(path[index - 1].IsAdjacentTo(path[index]), Is.True);
        }

        [Test]
        public void MovementChitsDrawWithoutReplacementAndAreSeedReproducible()
        {
            var chits = new[]
            {
                new MovementChit("US TF", Side.UsNavy),
                new MovementChit("PLAN TF", Side.Plan),
                new MovementChit("Patrol Aircraft", Side.UsNavy)
            };
            var first = new MovementChitCup(new SeededDieRoller(77));
            var second = new MovementChitCup(new SeededDieRoller(77));
            first.Reset(chits);
            second.Reset(chits);
            var firstOrder = Enumerable.Range(0, 3).Select(_ => first.Draw().FormationId).ToArray();
            var secondOrder = Enumerable.Range(0, 3).Select(_ => second.Draw().FormationId).ToArray();
            Assert.That(firstOrder, Is.EqualTo(secondOrder));
            Assert.That(firstOrder.Distinct().Count(), Is.EqualTo(3));
            Assert.That(first.IsEmpty, Is.True);
        }

        [Test]
        public void TurnEndsOnlyAfterEveryChitActivates()
        {
            var game = new ScenarioOneGame(1, null, true);
            Assert.That(game.State.Phase, Is.EqualTo(ActivationPhase.AwaitingChit));
            Assert.That(game.DrawMovementChit().Accepted, Is.True);
            var firstFormation = game.State.ActiveFormationId;
            Assert.That(game.DeclareSpeed(game.State.ActiveSide, 0).Accepted, Is.True);
            game.EndActivation(game.State.ActiveSide);
            Assert.That(game.State.Turn, Is.EqualTo(1));
            Assert.That(game.State.ActiveFormationId, Is.Not.EqualTo(firstFormation));
            Assert.That(game.DeclareSpeed(game.State.ActiveSide, 0).Accepted, Is.True);
            game.EndActivation(game.State.ActiveSide);
            Assert.That(game.State.Turn, Is.EqualTo(2));
            Assert.That(game.State.TimeOfDay, Is.EqualTo(TimeOfDay.Pm));
            Assert.That(game.State.MovementCup.Remaining.Count, Is.EqualTo(2));
            Assert.That(game.State.MovementCup.Drawn, Is.Empty);
        }

        [Test]
        public void SplitIsLegalOnlyBeforeFirstChitDraw()
        {
            var game = new ScenarioOneGame(1, null, true);
            var split = game.Execute(new GameCommand(GameCommandType.SplitTaskForce, Side.UsNavy,
                game.State.Revision, formationId: "US Task Force", newFormationId: "US Task Force 2",
                unitIds: new[] { "us-burke-iia" }));
            Assert.That(split.Accepted, Is.True);
            Assert.That(game.State.Forces.Count, Is.EqualTo(3));
            Assert.That(game.State.MovementCup.TotalCount, Is.EqualTo(3));
            Assert.That(game.DrawMovementChit().Accepted, Is.True);
            var lateSplit = game.Execute(new GameCommand(GameCommandType.SplitTaskForce, Side.Plan,
                game.State.Revision, formationId: "PLAN Task Force", newFormationId: "PLAN Task Force 2",
                unitIds: new[] { "plan-type-054a" }));
            Assert.That(lateSplit.Violation.Code, Is.EqualTo(RuleViolationCode.SplitWindowClosed));
        }

        [Test]
        public void EsmAndVisualSearchUsePrintedDetectionNumbers()
        {
            var state = ScenarioOne.Create(true);
            state.Player.MoveTo(new HexCoord(7, 13));
            state.Enemy.MoveTo(new HexCoord(8, 13));
            state.Enemy.DeclareRadar(true);
            Assert.That(new DetectionResolver(new SequenceDieRoller(5))
                .ResolveEsm(state.Player, state.Enemy), Is.True);
            Assert.That(new DetectionResolver(new SequenceDieRoller(6))
                .ResolveEsm(state.Player, state.Enemy), Is.False);
            state.Enemy.MoveTo(state.Player.Position);
            state.Enemy.DeclareRadar(false);
            Assert.That(new DetectionResolver(new SequenceDieRoller(2))
                .ResolveVisual(state.Player, state.Enemy, TimeOfDay.Am), Is.True);
            Assert.That(new DetectionResolver(new SequenceDieRoller(3))
                .ResolveVisual(state.Player, state.Enemy, TimeOfDay.Am), Is.False);
            Assert.That(new DetectionResolver(new SequenceDieRoller(1))
                .ResolveVisual(state.Player, state.Enemy, TimeOfDay.Night), Is.False);
        }

        [Test]
        public void UndetectedTargetsCannotBeAttackedOrInspected()
        {
            var game = new ScenarioOneGame(1, null, true, true,
                new SequenceDieRoller(1, 1, 1, 1, 1, 1));
            Assert.That(game.DrawMovementChit().Accepted, Is.True);
            Assert.That(game.State.ActiveSide, Is.EqualTo(Side.UsNavy));
            Assert.That(game.Execute(new GameCommand(GameCommandType.RadiateRadar, Side.UsNavy,
                game.State.Revision, enabled: false)).Accepted, Is.True);
            Assert.That(game.DeclareSpeed(Side.UsNavy, 0).Accepted, Is.True);
            Assert.That(game.ViewFor(Side.UsNavy).OpposingFormation.IsKnown, Is.False);
            Assert.That(game.ViewFor(Side.UsNavy).OpposingFormation.Units, Is.Empty);
            var hiddenAttack = game.Execute(new GameCommand(GameCommandType.Attack, Side.UsNavy,
                game.State.Revision));
            Assert.That(hiddenAttack.Violation.Code, Is.EqualTo(RuleViolationCode.TargetUndetected));
            game.State.Detection.Detect(Side.UsNavy, game.State.Enemy,
                DetectionMethod.Esm, game.State.Turn);
            Assert.That(game.ViewFor(Side.UsNavy).OpposingFormation.Units.Count, Is.EqualTo(2));
            Assert.That(game.Execute(new GameCommand(GameCommandType.Attack, Side.UsNavy,
                game.State.Revision)).Accepted, Is.True);
        }

        [Test]
        public void SurfaceRadarAutomaticallyDetectsSameHexShips()
        {
            var game = new ScenarioOneGame(1, null, true, true, new SequenceDieRoller(1));
            game.State.Enemy.MoveTo(game.State.Player.Position);
            Assert.That(game.DrawMovementChit().Accepted, Is.True);
            var declaration = game.Execute(new GameCommand(GameCommandType.RadiateRadar,
                Side.UsNavy, game.State.Revision, enabled: true));
            Assert.That(declaration.Accepted, Is.True);
            Assert.That(game.State.Detection.IsDetected(Side.UsNavy, game.State.Enemy.Id), Is.True);
            Assert.That(game.State.Detection.ContactFor(Side.UsNavy, game.State.Enemy.Id).Method,
                Is.EqualTo(DetectionMethod.SurfaceSearchRadar));
        }

        [Test]
        public void MissileAllocationRejectsOverfireAndCommitsOnlyChosenFactors()
        {
            var game = new ScenarioOneGame(1, null, true, false, new SequenceDieRoller(1));
            Assert.That(game.DrawMovementChit().Accepted, Is.True);
            Assert.That(game.DeclareSpeed(Side.UsNavy, 0).Accepted, Is.True);
            Assert.That(game.Execute(new GameCommand(GameCommandType.Attack, Side.UsNavy,
                game.State.Revision)).Accepted, Is.True);
            var excessive = game.Execute(new GameCommand(GameCommandType.AllocateMissileFire,
                Side.UsNavy, game.State.Revision, missileAllocations: new[]
                {
                    new MissileAllocationData
                    {
                        id = "EXCESS", sourceUnitId = "us-burke-iia",
                        targetUnitId = "plan-type-071", longFactors = 2
                    }
                }));
            Assert.That(excessive.Violation.Code, Is.EqualTo(RuleViolationCode.InsufficientAmmunition));
            Assert.That(game.State.Player.Units[0].LongMissilesRemaining, Is.EqualTo(1));
            var legal = game.Execute(new GameCommand(GameCommandType.AllocateMissileFire,
                Side.UsNavy, game.State.Revision, missileAllocations: new[]
                {
                    new MissileAllocationData
                    {
                        id = "LEGAL", sourceUnitId = "us-burke-iia",
                        targetUnitId = "plan-type-071", longFactors = 1
                    }
                }));
            Assert.That(legal.Accepted, Is.True);
            Assert.That(game.State.Player.Units[0].LongMissilesRemaining, Is.Zero);
            Assert.That(game.State.Player.Units[0].ShortMissilesRemaining, Is.EqualTo(2));
        }

        [Test]
        public void PointDefenseProtectsOnlyTheTargetShip()
        {
            var escort = new UnitState(new UnitDefinition("escort", "Escort", Side.Plan,
                UnitRole.Escort, 0, 0, 5, 0, 0, 0, 2, 2));
            var target = new UnitState(new UnitDefinition("target", "Target", Side.Plan,
                UnitRole.Objective, 0, 0, 0, 0, 0, 0, 2, 3));
            var defender = new TaskForceState("Defender", Side.Plan, new HexCoord(5, 5),
                new[] { escort, target });
            var engagement = new MissileEngagement("Attacker", defender.Id, Side.UsNavy,
                "Attacker", ActivationPhase.PlayerAction);
            engagement.SetSalvos(new[] { new MissileSalvo("SALVO", "source", "target", 1, 0) });
            var report = new MissileCombatResolver(new SequenceDieRoller(3))
                .ResolvePointDefenseAndImpacts(engagement, defender);
            Assert.That(report.InterceptedFactors, Is.Zero);
            Assert.That(target.HullDamage, Is.EqualTo(1));
            Assert.That(escort.HullDamage, Is.Zero);
        }
    }
}
