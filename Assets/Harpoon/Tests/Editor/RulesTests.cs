using System.Linq;
using NUnit.Framework;

namespace Harpoon.Core.Tests
{
    public sealed class RulesTests
    {
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
            var game = new ScenarioOneGame(8, null, true);
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
            var game = new ScenarioOneGame(8);
            game.TryMovePlayer(new HexCoord(3, 13), out _);
            Assert.That(game.State.Transactions.Count, Is.GreaterThanOrEqualTo(5));
            Assert.That(game.State.Transactions[0].Category, Is.EqualTo("SETUP"));
            Assert.That(game.State.Transactions.Exists(item => item.Category == "DIE"), Is.True);
            Assert.That(game.State.Transactions.Exists(item => item.Category == "REJECTED"), Is.True);
            for (var index = 0; index < game.State.Transactions.Count; index++)
                Assert.That(game.State.Transactions[index].Sequence, Is.EqualTo(index + 1));
        }

        [Test]
        public void MultiplayerSnapshotReproducesAuthoritativeState()
        {
            var host = new ScenarioOneGame(8, null, true);
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
            var game = new ScenarioOneGame(8, null, true);
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
            IRulesEngine engine = new ScenarioOneGame(8, null, true);
            var side = engine.State.ActiveSide;
            var declaration = engine.Execute(new GameCommand(GameCommandType.DeclareSpeed, side, 0,
                declaredSpeed: 1, id: "typed-speed-test"));
            Assert.That(declaration.Accepted, Is.True);
            var destination = engine.State.Map.NavigableNeighbors(engine.State.ForceFor(side).Position, side).First();
            var command = new GameCommand(GameCommandType.Move, side, 1,
                destination, id: "typed-event-test");
            var result = engine.Execute(command);

            Assert.That(result.Accepted, Is.True);
            Assert.That(engine.State.Revision, Is.EqualTo(2));
            Assert.That(engine.State.CommandLog.Count, Is.EqualTo(2));
            Assert.That(result.Events, Has.Some.Matches<RuleEvent>(item =>
                item.Type == RuleEventType.Movement && item.CommandId == command.Id));
            Assert.That(result.Events, Has.Some.Matches<RuleEvent>(item =>
                item.Type == RuleEventType.CommandAccepted && item.CommandId == command.Id));
        }

        [Test]
        public void IllegalCommandsReturnStableViolationCodes()
        {
            IRulesEngine engine = new ScenarioOneGame(8, null, true);
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
            var original = new ScenarioOneGame(8, null, true);
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
            IRulesEngine engine = new ScenarioOneGame(8, null, true);
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
            var game = new ScenarioOneGame(8, null, true);
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
    }
}
