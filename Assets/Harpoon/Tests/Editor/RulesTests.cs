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
        public void ScenarioFiveDistributesTransfersAndPrivatelyRedactsDummyCards()
        {
            var game = new ScenarioOneGame(5, null, true, false, null,
                FirstIslandChainScenarios.GhostFleet);
            Assert.That(game.State.Forces.Where(force => force.Side == Side.UsNavy).Sum(force => force.DummyCards),
                Is.EqualTo(3));
            Assert.That(game.State.Forces.Where(force => force.Side == Side.Plan).Sum(force => force.DummyCards),
                Is.EqualTo(5));
            var transfer = game.Execute(new GameCommand(GameCommandType.TransferDummyCards, Side.UsNavy,
                game.State.Revision, factors: 1, formationId: "US Dummy Group",
                newFormationId: "US Dummy Group 2"));
            Assert.That(transfer.Accepted, Is.True);
            Assert.That(game.State.Formation("US Dummy Group 2").DummyCards, Is.EqualTo(1));
            Assert.That(game.State.Forces.Where(force => force.Side == Side.UsNavy).Sum(force => force.DummyCards),
                Is.EqualTo(3));
            Assert.That(game.State.Log.Last(), Does.Contain("no real ships transferred"));

            var usView = game.CaptureSnapshotFor(Side.UsNavy);
            Assert.That(usView.formations.Where(item => item.side == Side.Plan).All(item => item.dummyCards == 0), Is.True);
            var planView = game.CaptureSnapshotFor(Side.Plan);
            Assert.That(planView.formations.Where(item => item.side == Side.Plan).Sum(item => item.dummyCards), Is.EqualTo(5));
        }

        [Test]
        public void ScenarioFiveSurfaceSearchLocatesButSonarClearsDummyContact()
        {
            var game = new ScenarioOneGame(55, null, true, false,
                new SequenceDieRoller(1, 1), FirstIslandChainScenarios.GhostFleet);
            var snapshot = game.CaptureSnapshot();
            snapshot.activeSide = Side.UsNavy;
            snapshot.activeFormationId = "US Subic Convoy";
            snapshot.phase = ActivationPhase.PlayerAction;
            var observer = snapshot.formations.Single(item => item.id == "US Subic Convoy");
            var dummy = snapshot.formations.Single(item => item.id == "PLAN Dummy Group");
            dummy.column = observer.column;
            dummy.row = observer.row;
            game.ApplySnapshot(snapshot);

            var visual = game.Execute(new GameCommand(GameCommandType.Search, Side.UsNavy,
                game.State.Revision, targetId: "PLAN Dummy Group", formationId: "US Subic Convoy",
                searchMode: "visual"));
            Assert.That(visual.Accepted, Is.True);
            Assert.That(game.State.Detection.ContactFor(Side.UsNavy, "PLAN Dummy Group").Level,
                Is.EqualTo(ContactLevel.Located));
            var attack = game.Execute(new GameCommand(GameCommandType.Attack, Side.UsNavy,
                game.State.Revision, targetId: "PLAN Dummy Group"));
            Assert.That(attack.Violation.Code, Is.EqualTo(RuleViolationCode.TargetUndetected));

            var sonar = game.Execute(new GameCommand(GameCommandType.Search, Side.UsNavy,
                game.State.Revision, targetId: "PLAN Dummy Group", formationId: "US Subic Convoy",
                searchMode: "sonar"));
            Assert.That(sonar.Accepted, Is.True);
            Assert.That(game.State.Formation("PLAN Dummy Group"), Is.Null);
            Assert.That(game.State.Forces.Where(force => force.Side == Side.Plan).Sum(force => force.DummyCards),
                Is.EqualTo(5));
        }

        [Test]
        public void ScenarioSixLoadsModernSubmarinesAndRejectsMixedTaskForces()
        {
            var state = ScenarioOne.Create(false, FirstIslandChainScenarios.WolvesOfBashiChannel);
            Assert.That(state.MaximumTurns, Is.EqualTo(7));
            Assert.That(state.Forces.Count, Is.EqualTo(5));
            Assert.That(state.Forces.Where(force => force.Side == Side.Plan)
                .SelectMany(force => force.Units).Count(unit => unit.Definition.IsSubmarine), Is.EqualTo(3));
            Assert.That(state.Unit("us-los-angeles").Definition.Torpedoes, Is.EqualTo(4));
            Assert.That(state.Unit("plan-type-093b").Definition.Sonar, Is.EqualTo(5));
            Assert.Throws<System.InvalidOperationException>(() => new TaskForceState("Illegal", Side.UsNavy,
                new HexCoord(1, 1), new[] { state.Unit("us-los-angeles"), state.Unit("us-burke-iii") }));
        }

        [Test]
        public void ScenarioSixSonarUsesCompleteModifierMatrixAndNaturalSixFails()
        {
            var state = ScenarioOne.Create(false, FirstIslandChainScenarios.WolvesOfBashiChannel);
            var observer = state.Formation("US Hunter-Killer Group");
            var target = state.Formation("PLAN Yuan 1");
            observer.MoveTo(new HexCoord(9, 12));
            target.MoveTo(new HexCoord(10, 12));
            observer.DeclareSpeed(1);
            target.DeclareSpeed(2);
            Assert.That(new DetectionResolver(new SequenceDieRoller(4))
                .ResolveSonar(observer, target, false), Is.True);
            Assert.That(new DetectionResolver(new SequenceDieRoller(5))
                .ResolveSonar(observer, target, false), Is.False);
            Assert.That(new DetectionResolver(new SequenceDieRoller(5))
                .ResolveSonar(observer, target, true), Is.True);
            Assert.That(new DetectionResolver(new SequenceDieRoller(6))
                .ResolveSonar(observer, target, true), Is.False);
            target.MoveTo(new HexCoord(11, 12));
            observer.DeclareSpeed(0);
            target.DeclareSpeed(0);
            Assert.That(new DetectionResolver(new SequenceDieRoller(2))
                .ResolveSonar(observer, target, false), Is.True);
            Assert.That(new DetectionResolver(new SequenceDieRoller(3))
                .ResolveSonar(observer, target, false), Is.False);
        }

        [Test]
        public void ScenarioSixSurfaceSearchCannotClassifySubButSonarAndAswCanSinkIt()
        {
            var game = new ScenarioOneGame(66, null, true, false,
                new SequenceDieRoller(1, 1, 1, 1, 1, 1, 6, 6, 6, 6, 6),
                FirstIslandChainScenarios.WolvesOfBashiChannel);
            var snapshot = game.CaptureSnapshot();
            snapshot.activeSide = Side.UsNavy;
            snapshot.activeFormationId = "US Hunter-Killer Group";
            snapshot.phase = ActivationPhase.PlayerAction;
            var hunter = snapshot.formations.Single(item => item.id == "US Hunter-Killer Group");
            var yuan = snapshot.formations.Single(item => item.id == "PLAN Yuan 1");
            yuan.column = hunter.column;
            yuan.row = hunter.row;
            game.ApplySnapshot(snapshot);
            Assert.That(game.Execute(new GameCommand(GameCommandType.Search, Side.UsNavy,
                game.State.Revision, targetId: "PLAN Yuan 1", searchMode: "visual")).Accepted, Is.True);
            Assert.That(game.State.Detection.ContactFor(Side.UsNavy, "PLAN Yuan 1").Level,
                Is.EqualTo(ContactLevel.Located));
            var located = game.CaptureSnapshotFor(Side.UsNavy);
            Assert.That(located.formations.Single(item => item.id == "PLAN Yuan 1").unitIds, Is.Empty);
            Assert.That(game.Execute(new GameCommand(GameCommandType.Search, Side.UsNavy,
                game.State.Revision, targetId: "PLAN Yuan 1", searchMode: "sonar")).Accepted, Is.True);
            Assert.That(game.State.Detection.IsClassified(Side.UsNavy, "PLAN Yuan 1"), Is.True);
            var attack = game.Execute(new GameCommand(GameCommandType.Attack, Side.UsNavy,
                game.State.Revision, targetId: "PLAN Yuan 1"));
            Assert.That(attack.Accepted, Is.True);
            Assert.That(game.State.Unit("plan-type-039ab-1").IsSunk, Is.True);
        }

        [Test]
        public void ScenarioSixSevenTurnScoreAppliesTwoShipLossOffset()
        {
            var game = new ScenarioOneGame(67, null, true, false, null,
                FirstIslandChainScenarios.WolvesOfBashiChannel);
            var snapshot = game.CaptureSnapshot();
            snapshot.units.Single(unit => unit.id == "plan-type-039ab-1").hullDamage = 2;
            snapshot.units.Single(unit => unit.id == "plan-type-039ab-2").hullDamage = 2;
            snapshot.units.Single(unit => unit.id == "us-burke-iii").hullDamage = 2;
            snapshot.units.Single(unit => unit.id == "us-constellation-1").hullDamage = 1;
            game.ApplySnapshot(snapshot);
            Assert.That(game.CurrentScore().Result, Is.EqualTo("PLAN VICTORY"));
            snapshot = game.CaptureSnapshot();
            snapshot.units.Single(unit => unit.id == "us-constellation-1").hullDamage = 0;
            game.ApplySnapshot(snapshot);
            Assert.That(game.CurrentScore().Result, Is.EqualTo("US NAVY VICTORY"));

            snapshot = game.CaptureSnapshot();
            snapshot.turn = 7;
            snapshot.activeSide = Side.UsNavy;
            snapshot.activeFormationId = "US Hunter-Killer Group";
            snapshot.phase = ActivationPhase.PlayerAction;
            snapshot.usDeclaredSpeed = 0;
            snapshot.usMovementSpent = 0;
            snapshot.formations.Single(item => item.id == "US Hunter-Killer Group").declaredSpeed = 0;
            snapshot.remainingChits = System.Array.Empty<MovementChitData>();
            game.ApplySnapshot(snapshot);
            Assert.That(game.Execute(new GameCommand(GameCommandType.EndActivation, Side.UsNavy,
                game.State.Revision)).Accepted, Is.True);
            Assert.That(game.State.EndReason, Is.EqualTo(ScenarioEndReason.TurnLimit));
            Assert.That(game.State.Result, Is.EqualTo("US NAVY VICTORY"));
        }

        [Test]
        public void ScenarioSixTorpedoesResolveBeforeScreenCounterattackAndSsmCannotTargetSubs()
        {
            var game = new ScenarioOneGame(68, null, true, false,
                new SequenceDieRoller(1, 1, 1, 1, 6, 6, 6, 6, 6, 6),
                FirstIslandChainScenarios.WolvesOfBashiChannel);
            var snapshot = game.CaptureSnapshot();
            snapshot.activeSide = Side.Plan;
            snapshot.activeFormationId = "PLAN Type 093B";
            snapshot.phase = ActivationPhase.PlayerAction;
            var plan = snapshot.formations.Single(item => item.id == "PLAN Type 093B");
            var surface = snapshot.formations.Single(item => item.id == "US Hunter-Killer Group");
            plan.column = surface.column;
            plan.row = surface.row;
            game.ApplySnapshot(snapshot);
            game.State.Detection.Detect(Side.Plan, game.State.Formation("US Hunter-Killer Group"),
                DetectionMethod.Sonar, game.State.Turn);
            var torpedo = game.Execute(new GameCommand(GameCommandType.Attack, Side.Plan,
                game.State.Revision, targetId: "US Hunter-Killer Group"));
            Assert.That(torpedo.Accepted, Is.True);
            Assert.That(game.State.Unit("us-burke-iii").IsSunk, Is.True);
            Assert.That(game.State.Unit("plan-type-093b").HullDamage, Is.Zero,
                "A screening ship sunk by the torpedo cannot counterattack.");

            var ranged = new ScenarioOneGame(69, null, true, false, null,
                FirstIslandChainScenarios.WolvesOfBashiChannel);
            var rangedSnapshot = ranged.CaptureSnapshot();
            rangedSnapshot.activeSide = Side.UsNavy;
            rangedSnapshot.activeFormationId = "US Hunter-Killer Group";
            rangedSnapshot.phase = ActivationPhase.PlayerAction;
            var hunter = rangedSnapshot.formations.Single(item => item.id == "US Hunter-Killer Group");
            var submarine = rangedSnapshot.formations.Single(item => item.id == "PLAN Yuan 1");
            submarine.column = hunter.column - 1;
            submarine.row = hunter.row;
            ranged.ApplySnapshot(rangedSnapshot);
            ranged.State.Detection.Detect(Side.UsNavy, ranged.State.Formation("PLAN Yuan 1"),
                DetectionMethod.Sonar, ranged.State.Turn);
            var illegalSsm = ranged.Execute(new GameCommand(GameCommandType.Attack, Side.UsNavy,
                ranged.State.Revision, targetId: "PLAN Yuan 1"));
            Assert.That(illegalSsm.Violation.Code, Is.EqualTo(RuleViolationCode.NoLegalWeapon));
        }

        [Test]
        public void ScenarioSevenLoadsIndependentConvoysAndEnforcesBothDeploymentZones()
        {
            var definition = FirstIslandChainScenarios.LifelineToTaiwan;
            var game = new ScenarioOneGame(77, null, true, false, null, definition);
            Assert.That(game.State.MaximumTurns, Is.EqualTo(10));
            Assert.That(game.State.Forces.Count, Is.EqualTo(7));
            Assert.That(game.State.Forces.Count(force => force.Side == Side.UsNavy), Is.EqualTo(4));
            Assert.That(game.State.Forces.Count(force => force.Side == Side.Plan), Is.EqualTo(3));
            Assert.That(game.State.Forces.SelectMany(force => force.Units)
                .Count(unit => unit.Definition.Role == UnitRole.Objective), Is.EqualTo(3));
            Assert.That(game.State.MovementCup.TotalCount, Is.EqualTo(7));

            var usInvalid = game.Execute(new GameCommand(GameCommandType.DeployFormation, Side.UsNavy,
                game.State.Revision, new HexCoord(8, 10), formationId: "US Convoy Alpha"));
            Assert.That(usInvalid.Violation.Code, Is.EqualTo(RuleViolationCode.InvalidFormation));
            Assert.That(game.Execute(new GameCommand(GameCommandType.DeployFormation, Side.UsNavy,
                game.State.Revision, new HexCoord(9, 12), formationId: "US Convoy Alpha")).Accepted, Is.True);
            var planInvalid = game.Execute(new GameCommand(GameCommandType.DeployFormation, Side.Plan,
                game.State.Revision, new HexCoord(11, 10), formationId: "PLAN Yuan 1"));
            Assert.That(planInvalid.Violation.Code, Is.EqualTo(RuleViolationCode.InvalidFormation));
            Assert.That(game.Execute(new GameCommand(GameCommandType.DeployFormation, Side.Plan,
                game.State.Revision, new HexCoord(14, 14), formationId: "PLAN Yuan 1")).Accepted, Is.True);
        }

        [Test]
        public void ScenarioSevenPortEntryRemovesConvoyFromFutureChits()
        {
            var game = new ScenarioOneGame(78, null, true, false, null,
                FirstIslandChainScenarios.LifelineToTaiwan);
            var snapshot = game.CaptureSnapshot();
            snapshot.activeSide = Side.UsNavy;
            snapshot.activeFormationId = "US Convoy Alpha";
            snapshot.phase = ActivationPhase.PlayerMove;
            snapshot.usDeclaredSpeed = 1;
            snapshot.usMovementSpent = 0;
            var convoy = snapshot.formations.Single(item => item.id == "US Convoy Alpha");
            convoy.column = 9;
            convoy.row = 10;
            convoy.declaredSpeed = 1;
            convoy.movementSpent = 0;
            game.ApplySnapshot(snapshot);
            Assert.That(game.Execute(new GameCommand(GameCommandType.Move, Side.UsNavy,
                game.State.Revision, new HexCoord(8, 10))).Accepted, Is.True);
            Assert.That(game.State.Formation("US Convoy Alpha").HasArrived, Is.True);
            Assert.That(game.State.Formation("US Convoy Alpha").MovementRemaining, Is.Zero);
            var cup = new MovementChitCup(new SeededDieRoller(1));
            cup.Reset(game.State.Forces);
            Assert.That(cup.Remaining.Any(chit => chit.FormationId == "US Convoy Alpha"), Is.False);
            Assert.That(game.CurrentScore().UsObjectiveDamage, Is.EqualTo(1));
        }

        [Test]
        public void ScenarioSevenSubmarineLossOffsetsOneLostMerchantAndTurnTenEndsTheGame()
        {
            var game = new ScenarioOneGame(79, null, true, false, null,
                FirstIslandChainScenarios.LifelineToTaiwan);
            var snapshot = game.CaptureSnapshot();
            snapshot.formations.Single(item => item.id == "US Convoy Bravo").arrived = true;
            snapshot.formations.Single(item => item.id == "US Convoy Charlie").arrived = true;
            snapshot.units.Single(item => item.id == "us-merchant-1").hullDamage = 4;
            snapshot.units.Single(item => item.id == "plan-type-039ab-1").hullDamage = 2;
            game.ApplySnapshot(snapshot);
            Assert.That(game.CurrentScore().UsObjectiveDamage, Is.EqualTo(2));
            Assert.That(game.CurrentScore().PlanObjectiveDamage, Is.EqualTo(1));
            Assert.That(game.CurrentScore().UsTieBreakDamage, Is.EqualTo(1));
            Assert.That(game.CurrentScore().PlanTieBreakDamage, Is.EqualTo(1));
            Assert.That(game.CurrentScore().Result, Is.EqualTo("US NAVY VICTORY"));

            snapshot = game.CaptureSnapshot();
            snapshot.formations.Single(item => item.id == "US Convoy Bravo").arrived = false;
            snapshot.turn = 10;
            snapshot.activeSide = Side.UsNavy;
            snapshot.activeFormationId = "US Replenishment Group";
            snapshot.phase = ActivationPhase.PlayerAction;
            snapshot.usDeclaredSpeed = 0;
            snapshot.usMovementSpent = 0;
            snapshot.formations.Single(item => item.id == "US Convoy Alpha").declaredSpeed = 0;
            snapshot.formations.Single(item => item.id == "US Replenishment Group").declaredSpeed = 0;
            snapshot.remainingChits = System.Array.Empty<MovementChitData>();
            game.ApplySnapshot(snapshot);
            Assert.That(game.Execute(new GameCommand(GameCommandType.EndActivation, Side.UsNavy,
                game.State.Revision)).Accepted, Is.True);
            Assert.That(game.State.EndReason, Is.EqualTo(ScenarioEndReason.TurnLimit));
            Assert.That(game.State.Result, Is.EqualTo("PLAN VICTORY"));
        }

        [Test]
        public void ScenarioEightLoadsPrintedForcesAndEastEdgeEntry()
        {
            var game = new ScenarioOneGame(88, null, true, false, null,
                FirstIslandChainScenarios.HuntTheDragon);
            Assert.That(game.State.MaximumTurns, Is.EqualTo(7));
            Assert.That(game.State.Forces.Count, Is.EqualTo(8));
            Assert.That(game.State.Forces.Count(force => force.Side == Side.UsNavy && force.IsSubmarineOnly),
                Is.EqualTo(4));
            Assert.That(game.State.Forces.Where(force => force.Side == Side.Plan).All(force =>
                force.Position.Row == 12 && force.Position.Column >= 8 && force.Position.Column <= 12 &&
                game.State.Map.IsNavigable(force.Position, Side.Plan)), Is.True);
            Assert.That(game.State.Forces.Where(force => force.Side == Side.UsNavy)
                .All(force => !force.HasEnteredMap), Is.True);
            Assert.That(game.State.Unit("plan-fujian").Definition.EmbarkedAircraftCapacity, Is.EqualTo(1));

            var snapshot = game.CaptureSnapshot();
            snapshot.activeSide = Side.UsNavy;
            snapshot.activeFormationId = "US Virginia 1";
            snapshot.phase = ActivationPhase.PlayerMove;
            snapshot.usDeclaredSpeed = 1;
            snapshot.formations.Single(item => item.id == "US Virginia 1").declaredSpeed = 1;
            game.ApplySnapshot(snapshot);
            Assert.That(game.Execute(new GameCommand(GameCommandType.Move, Side.UsNavy,
                game.State.Revision, new HexCoord(15, 9))).Accepted, Is.True);
            Assert.That(game.State.Formation("US Virginia 1").HasEnteredMap, Is.True);
        }

        [Test]
        public void ScenarioEightPatrolBandAndCarrierDamageGateTheWestExit()
        {
            var game = new ScenarioOneGame(89, null, true, false, null,
                FirstIslandChainScenarios.HuntTheDragon);
            Assert.That(ScenarioOneGame.DistanceToPatrolLine(game.State.Scenario, new HexCoord(3, 12)),
                Is.Zero);
            Assert.That(ScenarioOneGame.DistanceToPatrolLine(game.State.Scenario, new HexCoord(9, 9)),
                Is.EqualTo(3));

            var snapshot = game.CaptureSnapshot();
            snapshot.activeSide = Side.Plan;
            snapshot.activeFormationId = "PLAN Fujian";
            snapshot.phase = ActivationPhase.PlayerMove;
            snapshot.planDeclaredSpeed = 1;
            var carrier = snapshot.formations.Single(item => item.id == "PLAN Fujian");
            carrier.column = 3;
            carrier.row = 12;
            carrier.declaredSpeed = 1;
            snapshot.units.Single(item => item.id == "plan-fujian").hullDamage = 3;
            game.ApplySnapshot(snapshot);
            Assert.That(game.State.Unit("plan-fujian").CanLaunchAircraft, Is.False);
            Assert.That(game.Execute(new GameCommand(GameCommandType.ExitMap, Side.Plan,
                game.State.Revision)).Violation.Code, Is.EqualTo(RuleViolationCode.ExitUnavailable));
        }

        [Test]
        public void ScenarioEightLaunchCapableWestExitAndFujianLossResolveVictory()
        {
            var escape = new ScenarioOneGame(90, null, true, false, null,
                FirstIslandChainScenarios.HuntTheDragon);
            var snapshot = escape.CaptureSnapshot();
            snapshot.activeSide = Side.Plan;
            snapshot.activeFormationId = "PLAN Fujian";
            snapshot.phase = ActivationPhase.PlayerMove;
            snapshot.planDeclaredSpeed = 1;
            var carrier = snapshot.formations.Single(item => item.id == "PLAN Fujian");
            carrier.column = 3;
            carrier.row = 12;
            carrier.declaredSpeed = 1;
            escape.ApplySnapshot(snapshot);
            Assert.That(escape.Execute(new GameCommand(GameCommandType.ExitMap, Side.Plan,
                escape.State.Revision)).Accepted, Is.True);
            Assert.That(escape.State.Result, Is.EqualTo("PLAN VICTORY"));
            Assert.That(escape.State.EndReason, Is.EqualTo(ScenarioEndReason.BoardEdgeExited));

            var sunk = new ScenarioOneGame(91, null, true, false, null,
                FirstIslandChainScenarios.HuntTheDragon);
            snapshot = sunk.CaptureSnapshot();
            snapshot.units.Single(item => item.id == "plan-fujian").hullDamage = 6;
            sunk.ApplySnapshot(snapshot);
            Assert.That(sunk.CurrentScore().Result, Is.EqualTo("US NAVY VICTORY"));
        }

        [Test]
        public void ScenarioNineLoadsPrintedSubmarinesAndP8A()
        {
            var game = new ScenarioOneGame(99, null, true, false, null,
                FirstIslandChainScenarios.Patroller);
            var p8 = game.State.Unit("us-p8a");
            Assert.That(game.State.MaximumTurns, Is.EqualTo(15));
            Assert.That(game.State.Forces.Count(force => force.Side == Side.Plan && force.IsSubmarineOnly),
                Is.EqualTo(4));
            Assert.That(p8.Definition.IsPatrolAircraft, Is.True);
            Assert.That(p8.Definition.AircraftRadius, Is.EqualTo(20));
            Assert.That(p8.Definition.SurfaceSearchRadar, Is.EqualTo(3));
            Assert.That(p8.Definition.Sonar, Is.EqualTo(4));
            Assert.That(p8.Definition.AntiSubmarineWarfare, Is.EqualTo(5));
            Assert.That(p8.ServiceableAircraftRemaining, Is.EqualTo(4));
        }

        [Test]
        public void ScenarioNineP8RelocatesAndSearchesFromFinalStation()
        {
            var game = new ScenarioOneGame(100, null, true, false,
                new SequenceDieRoller(1), FirstIslandChainScenarios.Patroller);
            var snapshot = game.CaptureSnapshot();
            snapshot.activeSide = Side.UsNavy;
            snapshot.activeFormationId = "US P-8A Poseidon";
            snapshot.phase = ActivationPhase.AircraftAction;
            var yuan = snapshot.formations.Single(item => item.id == "PLAN Yuan 1");
            yuan.column = 14;
            yuan.row = 10;
            game.ApplySnapshot(snapshot);
            Assert.That(game.Execute(new GameCommand(GameCommandType.Move, Side.UsNavy,
                game.State.Revision, new HexCoord(14, 10))).Accepted, Is.True);
            Assert.That(game.Execute(new GameCommand(GameCommandType.Search, Side.UsNavy,
                game.State.Revision, targetId: "PLAN Yuan 1", searchMode: "sonar")).Accepted, Is.True);
            Assert.That(game.State.Detection.IsClassified(Side.UsNavy, "PLAN Yuan 1"), Is.True);
            Assert.That(game.Execute(new GameCommand(GameCommandType.Search, Side.UsNavy,
                game.State.Revision, targetId: "PLAN Yuan 1", searchMode: "sonar")).Violation.Code,
                Is.EqualTo(RuleViolationCode.AlreadyActed));
        }

        [Test]
        public void ScenarioNineThirdEastEdgeEscapeWinsForPlan()
        {
            var game = new ScenarioOneGame(101, null, true, false, null,
                FirstIslandChainScenarios.Patroller);
            var eastEdge = game.State.Map.AllHexes.First(hex =>
                ScenarioOneGame.IsBoardEdgeHex(game.State.Map, hex, BoardEdge.East, Side.Plan));
            foreach (var id in new[] { "PLAN Yuan 1", "PLAN Yuan 2", "PLAN Yuan 3" })
            {
                var snapshot = game.CaptureSnapshot();
                snapshot.activeSide = Side.Plan;
                snapshot.activeFormationId = id;
                snapshot.phase = ActivationPhase.PlayerMove;
                var formation = snapshot.formations.Single(item => item.id == id);
                formation.column = eastEdge.Column;
                formation.row = eastEdge.Row;
                formation.declaredSpeed = 1;
                formation.movementSpent = 0;
                game.ApplySnapshot(snapshot);
                Assert.That(game.Execute(new GameCommand(GameCommandType.ExitMap, Side.Plan,
                    game.State.Revision)).Accepted, Is.True);
            }
            Assert.That(game.State.Result, Is.EqualTo("PLAN VICTORY"));
            Assert.That(game.State.EndReason, Is.EqualTo(ScenarioEndReason.BoardEdgeExited));
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

        [Test]
        public void ScenarioTenLoadsFirstLightOrderOfBattleAndAirInventories()
        {
            var game = new ScenarioOneGame(1010, null, true, false, null,
                FirstIslandChainScenarios.FirstLight);
            Assert.That(game.State.MaximumTurns, Is.EqualTo(12));
            Assert.That(game.State.Forces.Count, Is.EqualTo(3));
            Assert.That(game.State.Unit("us-ford"), Is.Not.Null);
            Assert.That(game.State.Forces.Count(force => force.Side == Side.Plan && force.IsSubmarineOnly),
                Is.EqualTo(2));
            Assert.That(game.State.TacticalFlights.Count, Is.EqualTo(15));
            Assert.That(game.State.TacticalFlights.Count(flight => flight.Side == Side.UsNavy), Is.EqualTo(12));
            Assert.That(ModernAirBaseDatabase.Get("us-ford-wing").FlightCapacity, Is.EqualTo(14));
            Assert.That(ModernAirBaseDatabase.Get("plan-ningbo").LongSam, Is.EqualTo(10));
        }

        [Test]
        public void ScenarioTenAirToAirAndAircraftDamageTablesMatchPrintedRules()
        {
            Assert.That(Enumerable.Range(-2, 5).Select(CombatTables.AirToAirHits),
                Is.All.EqualTo(0));
            Assert.That(Enumerable.Range(3, 5).Select(CombatTables.AirToAirHits),
                Is.All.EqualTo(1));
            Assert.That(CombatTables.AirToAirHits(8), Is.EqualTo(2));
            Assert.That(CombatTables.AircraftDamage(1), Is.EqualTo(AircraftDamageResult.NoEffect));
            Assert.That(CombatTables.AircraftDamage(2), Is.EqualTo(AircraftDamageResult.Abort));
            Assert.That(CombatTables.AircraftDamage(6), Is.EqualTo(AircraftDamageResult.ShotDown));
        }

        [Test]
        public void ScenarioTenDefensiveFlightsAndSnapshotsRetainMissionState()
        {
            var game = new ScenarioOneGame(1011, null, true, false, null,
                FirstIslandChainScenarios.FirstLight);
            Assert.That(game.Execute(new GameCommand(GameCommandType.AssignCap, Side.UsNavy,
                game.State.Revision, sourceUnitId: "FORD-F35-1", enabled: true)).Accepted, Is.True);
            Assert.That(game.Execute(new GameCommand(GameCommandType.AssignDeckInterceptor, Side.UsNavy,
                game.State.Revision, sourceUnitId: "FORD-F35-2")).Accepted, Is.True);
            var restored = new ScenarioOneGame(1, null, true);
            restored.ApplySnapshot(game.CaptureSnapshot());
            Assert.That(restored.State.TacticalFlight("FORD-F35-1").Mission, Is.EqualTo(TacticalAirMission.Cap));
            Assert.That(restored.State.TacticalFlight("FORD-F35-1").RadarOn, Is.True);
            Assert.That(restored.State.TacticalFlight("FORD-F35-2").Mission,
                Is.EqualTo(TacticalAirMission.DeckInterceptor));
            var planView = game.CaptureSnapshotFor(Side.Plan);
            var hiddenCap = planView.tacticalFlights.Single(flight => flight.id == "FORD-F35-1");
            Assert.That(hiddenCap.mission, Is.EqualTo(TacticalAirMission.Ready));
            Assert.That(hiddenCap.radarOn, Is.False);
        }

        [Test]
        public void ScenarioTenBombingDamagesRunwayAndFlightCannotAttackTwice()
        {
            var dice = Enumerable.Repeat(1, 20).Concat(new[] { 6, 6 }).ToArray();
            var game = new ScenarioOneGame(1012, null, true, false, new SequenceDieRoller(dice),
                FirstIslandChainScenarios.FirstLight);
            var snapshot = game.CaptureSnapshot();
            snapshot.activeSide = Side.UsNavy;
            snapshot.activeFormationId = "US Ford Strike Group";
            snapshot.phase = ActivationPhase.PlayerAction;
            snapshot.formations.Single(force => force.id == "US Ford Strike Group").declaredSpeed = 0;
            game.ApplySnapshot(snapshot);
            var strike = game.Execute(new GameCommand(GameCommandType.LaunchTacticalStrike, Side.UsNavy,
                game.State.Revision, factors: 1, targetId: "plan-ningbo", sourceUnitId: "FORD-F18-1",
                searchMode: TacticalWeapon.Bombs.ToString()));
            Assert.That(strike.Accepted, Is.True);
            Assert.That(game.State.AirBase("plan-ningbo").RunwayHits, Is.EqualTo(4));
            Assert.That(game.State.TacticalFlight("FORD-F18-1").Mission, Is.EqualTo(TacticalAirMission.Flown));
            var repeat = game.Execute(new GameCommand(GameCommandType.LaunchTacticalStrike, Side.UsNavy,
                game.State.Revision, factors: 1, targetId: "plan-ningbo", sourceUnitId: "FORD-F18-1",
                searchMode: TacticalWeapon.Bombs.ToString()));
            Assert.That(repeat.Violation.Code, Is.EqualTo(RuleViolationCode.AircraftUnavailable));
        }

        [Test]
        public void ScenarioTenEnforcesRadiusAndLaunchCapableFordObjective()
        {
            var radius = new ScenarioOneGame(1013, null, true, false, null,
                FirstIslandChainScenarios.FirstLight);
            var far = radius.CaptureSnapshot();
            far.activeSide = Side.UsNavy;
            far.activeFormationId = "US Ford Strike Group";
            far.phase = ActivationPhase.PlayerAction;
            var farFord = far.formations.Single(force => force.id == "US Ford Strike Group");
            farFord.column = 15;
            farFord.row = 20;
            farFord.declaredSpeed = 0;
            radius.ApplySnapshot(far);
            var rejected = radius.Execute(new GameCommand(GameCommandType.LaunchTacticalStrike, Side.UsNavy,
                radius.State.Revision, factors: 1, targetId: "plan-ningbo", sourceUnitId: "FORD-F18-1",
                searchMode: TacticalWeapon.Bombs.ToString()));
            Assert.That(rejected.Violation.Code, Is.EqualTo(RuleViolationCode.RadiusExceeded));

            var arrival = new ScenarioOneGame(1014, null, true, false, null,
                FirstIslandChainScenarios.FirstLight);
            var objective = arrival.CaptureSnapshot();
            objective.activeSide = Side.UsNavy;
            objective.activeFormationId = "US Ford Strike Group";
            objective.phase = ActivationPhase.PlayerAction;
            objective.remainingChits = System.Array.Empty<MovementChitData>();
            var ford = objective.formations.Single(force => force.id == "US Ford Strike Group");
            ford.column = 4;
            ford.row = 6;
            ford.declaredSpeed = 0;
            arrival.ApplySnapshot(objective);
            Assert.That(arrival.Execute(new GameCommand(GameCommandType.EndActivation, Side.UsNavy,
                arrival.State.Revision)).Accepted, Is.True);
            Assert.That(arrival.State.Result, Is.EqualTo("US NAVY VICTORY"));
            Assert.That(arrival.State.EndReason, Is.EqualTo(ScenarioEndReason.DestinationReached));
        }
    }
}
