using System;
using System.IO;
using System.Linq;
using System.Threading;
using Harpoon.Core;
using Harpoon.Runtime;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Harpoon.Editor
{
    public static class ProjectSetup
    {
        [MenuItem("Harpoon/Public Multiplayer Setup")]
        public static void OpenPublicMultiplayerSetup()
        {
            SettingsService.OpenProjectSettings("Project/Services");
            Debug.Log("Link this project to a Unity Cloud project, then enable Authentication, Lobby, and Relay. " +
                      "The game uses anonymous authentication and the Multiplayer Services SDK.");
        }

        [MenuItem("Harpoon/Build Main Scene")]
        public static void BuildMainScene()
        {
            EnsureRenderingAssets();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, "Assets/Main.unity");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Main.unity", true) };
            PlayerSettings.productName = "Harpoon: First Island Chain";
            PlayerSettings.companyName = "Open Source Harpoon Community";
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;
            AssetDatabase.SaveAssets();
            Debug.Log("Harpoon main scene and build settings created.");
        }

        public static void ValidateRules()
        {
            var battle = ScenarioOne.Create();
            Require(battle.Player.Position.DistanceTo(battle.Enemy.Position) == 3,
                "Scenario task forces must start three hexes apart.");
            Require(battle.Player.EffectiveSpeed == 2,
                "The merchant must limit the US task force to speed two.");

            battle.Player.MoveTo(new HexCoord(9, 10));
            var combat = new CombatResolver(new SequenceDieRoller(4, 4, 4, 4, 4, 4, 4, 4));
            var report = combat.Attack(battle.Player, battle.Enemy);
            Require(report.AttackFactors == 3, "The Burke must commit two short- and one long-range factor.");
            Require(report.InterceptedFactors == 3 && report.HullHits == 0,
                "Layered defenses must be able to stop the full raid.");

            var game = new ScenarioOneGame(1, null, true);
            Require(!game.TryMovePlayer(new HexCoord(3, 13), out _),
                "Movement beyond the slowest ship's speed must be rejected.");
            Require(game.State.Transactions.Count >= 5 &&
                    game.State.Transactions.Exists(item => item.Category == "TURN") &&
                    game.State.Transactions.Exists(item => item.Category == "REJECTED"),
                "The debug trace must retain setup, turn, and rejected-command transactions.");

            var manual = new ScenarioOneGame(1, null, true);
            Require(manual.DrawMovementChit().Accepted, "The first movement chit must be drawable.");
            var activeForce = manual.State.ForceFor(manual.State.ActiveSide);
            Require(manual.DeclareSpeed(manual.State.ActiveSide, 1).Accepted,
                "A manually controlled active side must declare legal speed.");
            var manualDestination = manual.State.Map.NavigableNeighbors(activeForce.Position,
                manual.State.ActiveSide).First();
            Require(manual.TryMove(manual.State.ActiveSide, manualDestination, out _),
                "A manually controlled active side must be able to issue a legal move command.");
            var firstSide = manual.State.ActiveSide;
            manual.EndActivation(firstSide);
            Require(manual.State.ActiveSide != firstSide,
                "Manual multiplayer activation must pass to the opposing human-controlled side.");
            var snapshot = manual.CaptureSnapshot();
            var mirror = new ScenarioOneGame(99, null, true);
            mirror.ApplySnapshot(snapshot);
            Require(mirror.State.ActiveSide == manual.State.ActiveSide &&
                    mirror.State.Phase == manual.State.Phase &&
                    mirror.State.Player.Position.Equals(manual.State.Player.Position) &&
                    mirror.State.Player.DeclaredSpeed == manual.State.Player.DeclaredSpeed &&
                    mirror.State.Player.MovementPointsSpent == manual.State.Player.MovementPointsSpent &&
                    mirror.State.Transactions.Count == manual.State.Transactions.Count,
                "A multiplayer snapshot must reproduce authoritative state and transaction order.");

            var burke = battle.Player.Units[0].Definition;
            Require(burke.AirSearchRadar == 2 && burke.ShortSam == 3 && burke.LongSam == 8 &&
                    burke.PointDefense == 4 && burke.SurfaceSearchRadar == 1 && burke.ShortSsm == 2 &&
                    burke.LongSsm == 1 && burke.Guns == 2 && burke.Sonar == 4 &&
                    burke.AntiSubmarineWarfare == 5 && burke.Speed == 3 && burke.Hull == 2,
                "Arleigh Burke Flight IIA values must match supplement page 15.");
            var type071 = battle.Enemy.Units[1].Definition;
            Require(type071.AirSearchRadar == 1 && type071.PointDefense == 2 &&
                    type071.SurfaceSearchRadar == 1 && type071.Guns == 1 &&
                    type071.AntiSubmarineWarfare == 1 && type071.Speed == 2 && type071.Hull == 3,
                "Type 071 values must match supplement page 19.");
            Require(battle.MaximumTurns == 0, "Scenario 1 must not use an invented turn cap.");

            var thresholdDefinition = new UnitDefinition("threshold", "Threshold Test", Side.UsNavy,
                UnitRole.Escort, 4, 6, 3, 2, 4, 3, 3, 5, 2, 1, 4, 2);
            var thresholdShip = new UnitState(thresholdDefinition);
            thresholdShip.ApplyDamage(3);
            Require(thresholdShip.HasHalfDamage && !thresholdShip.HasTwoThirdsDamage &&
                    thresholdShip.EffectiveLongSam == 0 && thresholdShip.ShortMissilesRemaining == 2 &&
                    thresholdShip.EffectiveGuns == 3 && thresholdShip.EffectiveSpeed == 2,
                "Half-damage capability loss must follow Captain's Rules page 4.");
            thresholdShip.ApplyDamage(1);
            Require(thresholdShip.HasTwoThirdsDamage && thresholdShip.EffectiveShortSam == 0 &&
                    thresholdShip.EffectivePointDefense == 0 && thresholdShip.EffectiveGuns == 2 &&
                    thresholdShip.EffectiveSonar == 0 && thresholdShip.EffectiveSpeed == 1,
                "Two-thirds-damage capability loss must follow Captain's Rules page 4.");

            ValidateCommandArchitecture();
            ValidateBoardAndMovement();
            ValidateMovementChitSequence();
            ValidateSurfaceDetection();

            ValidateLoopbackTransport();
            Debug.Log("HARPOON RULE VALIDATION PASSED (Sections 3-5 movement, chits/time, surface detection, replay/private-view, and TCP loopback included).");
        }

        public static void BuildWindowsPlayer()
        {
            EnsureRenderingAssets();
            const string outputPath = "Build/Windows/HarpoonFirstIslandChain.exe";
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? "Build/Windows");
            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Main.unity" },
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"Windows build failed: {report.summary.result}");
            Debug.Log($"HARPOON WINDOWS BUILD PASSED: {outputPath}");
        }

        private static void EnsureRenderingAssets()
        {
            const string resourcesFolder = "Assets/Harpoon/Runtime/Resources";
            const string materialPath = resourcesFolder + "/OperationalMaterial.mat";
            if (!AssetDatabase.IsValidFolder(resourcesFolder))
                AssetDatabase.CreateFolder("Assets/Harpoon/Runtime", "Resources");
            var shader = Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("Unity Standard shader was not found.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "Operational Material" };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            else
            {
                material.shader = shader;
                EditorUtility.SetDirty(material);
            }
            material.SetFloat("_Metallic", 0.1f);
            material.SetFloat("_Glossiness", 0.55f);
            AssetDatabase.SaveAssets();
        }

        private static void ValidateLoopbackTransport()
        {
            var port = 48000 + System.Diagnostics.Process.GetCurrentProcess().Id % 10000;
            using (var host = new MultiplayerNetwork())
            using (var client = new MultiplayerNetwork())
            {
                host.StartHost(port);
                client.StartClient("127.0.0.1", port);
                var deadline = DateTime.UtcNow.AddSeconds(4);
                while ((!host.IsConnected || !client.IsConnected) && DateTime.UtcNow < deadline)
                    Thread.Sleep(10);
                Require(host.IsConnected && client.IsConnected,
                    $"TCP loopback opponents must connect. Host='{host.Status}', Client='{client.Status}'.");
                client.Send(new NetworkMessage
                {
                    kind = "command",
                    command = new GameCommand(GameCommandType.Move, Side.UsNavy, 0,
                        new HexCoord(7, 13), id: "loopback-command").ToData()
                });
                NetworkMessage received = null;
                while (received == null && DateTime.UtcNow < deadline)
                {
                    if (!host.TryReceive(out received)) Thread.Sleep(10);
                }
                Require(received != null && received.kind == "command" &&
                        received.command.id == "loopback-command" &&
                        received.command.type == GameCommandType.Move &&
                        received.command.actor == Side.UsNavy &&
                        received.command.column == 7 && received.command.row == 13,
                    "TCP loopback must preserve a complete side-bearing command.");
            }
        }

        private static void ValidateCommandArchitecture()
        {
            IRulesEngine engine = new ScenarioOneGame(1, null, true);
            var draw = engine.Execute(new GameCommand(GameCommandType.DrawMovementChit, Side.UsNavy,
                engine.State.Revision, id: "architecture-draw"));
            Require(draw.Accepted && draw.Events.Any(item => item.Type == RuleEventType.ChitDrawn),
                "The first activation must be selected by a typed movement-chit event.");
            var active = engine.State.ActiveSide;
            var force = engine.State.ForceFor(active);
            var declaration = engine.Execute(new GameCommand(GameCommandType.DeclareSpeed, active,
                engine.State.Revision, declaredSpeed: 1, id: "architecture-speed"));
            Require(declaration.Accepted && declaration.Events.Any(item => item.Type == RuleEventType.SpeedDeclared),
                "Speed declaration must pass through the command/event boundary.");
            var destination = engine.State.Map.NavigableNeighbors(force.Position, active).First();
            var move = new GameCommand(GameCommandType.Move, active, engine.State.Revision,
                destination, id: "architecture-move");
            var moveResult = engine.Execute(move);
            Require(moveResult.Accepted && engine.State.Revision == 3 &&
                    engine.State.CommandLog.Count == 3,
                "A legal command must advance the authoritative revision and enter the command log.");
            Require(moveResult.Events.Count > 0 &&
                    moveResult.Events.All(item => item.CommandId == move.Id) &&
                    moveResult.Events.Any(item => item.Type == RuleEventType.Movement) &&
                    moveResult.Events.Any(item => item.Type == RuleEventType.CommandAccepted),
                "A command must emit ordered immutable typed events tied to its command ID.");

            var stale = engine.Execute(new GameCommand(GameCommandType.EndActivation, active, 0,
                id: "stale-command"));
            Require(!stale.Accepted && stale.Violation.Code == RuleViolationCode.StaleRevision &&
                    engine.State.Revision == 3,
                "A stale command must return a structured violation without mutating revision state.");
            var wrongSide = active == Side.UsNavy ? Side.Plan : Side.UsNavy;
            var wrong = engine.Execute(new GameCommand(GameCommandType.EndActivation, wrongSide,
                engine.State.Revision, id: "wrong-side-command"));
            Require(!wrong.Accepted && wrong.Violation.Code == RuleViolationCode.WrongSide,
                "A command from the inactive side must return a WrongSide violation.");
            var unsupported = engine.Execute(new GameCommand(GameCommandType.RadiateRadar, active,
                engine.State.Revision, id: "future-radar-command"));
            Require(!unsupported.Accepted && unsupported.Violation.Code == RuleViolationCode.UnsupportedCommand,
                "Future rules actions must remain represented and reject with a typed Scenario 1 violation.");

            var duplicateId = "duplicate-command";
            var acceptedEnd = engine.Execute(new GameCommand(GameCommandType.EndActivation, active,
                engine.State.Revision, id: duplicateId));
            Require(acceptedEnd.Accepted, "The active side must be able to end its activation through the command API.");
            var duplicate = engine.Execute(new GameCommand(GameCommandType.EndActivation,
                engine.State.ActiveSide, engine.State.Revision, id: duplicateId));
            Require(!duplicate.Accepted && duplicate.Violation.Code == RuleViolationCode.DuplicateCommand,
                "A repeated command ID must be rejected independently of its claimed actor.");

            var hiddenView = engine.ViewFor(active, false);
            Require(hiddenView.OwnFormation.IsKnown && hiddenView.OwnFormation.Units.Count > 0 &&
                    !hiddenView.OpposingFormation.IsKnown && hiddenView.OpposingFormation.Units.Count == 0,
                "Side projections must retain private friendly details and hide unknown opponents.");
            var scenarioView = engine.ViewFor(active, true);
            Require(scenarioView.OpposingFormation.IsKnown && scenarioView.OpposingFormation.Units.Count > 0,
                "Scenario 1's explicit no-detection setup must publish both formations.");

            var planFirstSolo = new ScenarioOneGame(2);
            Require(planFirstSolo.DrawMovementChit().Accepted &&
                    planFirstSolo.State.ActiveSide == Side.UsNavy && planFirstSolo.State.Revision >= 4,
                "A PLAN-first solo turn must complete through the same command API without recursive AI activation.");

            var original = (ScenarioOneGame)engine;
            var replay = ScenarioOneGame.Replay(original.Seed, original.State.CommandLog);
            Require(replay.State.Revision == original.State.Revision &&
                    replay.State.ActiveSide == original.State.ActiveSide &&
                    replay.State.Phase == original.State.Phase &&
                    replay.State.Player.Position.Equals(original.State.Player.Position) &&
                    replay.State.Enemy.Position.Equals(original.State.Enemy.Position) &&
                    replay.State.CommandLog.Count == original.State.CommandLog.Count,
                "Initial seed plus command log must deterministically reconstruct authoritative state.");
        }

        private static void ValidateBoardAndMovement()
        {
            var map = FirstIslandChainMap.Instance;
            Require(map.MinimumColumn == 1 && map.MaximumColumn == 15 &&
                    map.MinimumRow == 1 && map.MaximumRow == 20 && map.AllHexes.Count() == 300,
                "The supplement map must use its complete 15-by-20 axial coordinate field.");
            Require(new HexCoord(10, 10).DistanceTo(new HexCoord(7, 13)) == 3 &&
                    new HexCoord(10, 10).Neighbors().All(hex => hex.DistanceTo(new HexCoord(10, 10)) == 1),
                "Scenario setup, adjacency, movement, range, AI, and rendering must share axial topology.");
            Require(map.TerrainAt(new HexCoord(8, 13)) == TerrainType.Land &&
                    map.TerrainAt(new HexCoord(9, 13)) == TerrainType.Sea &&
                    !map.Contains(new HexCoord(16, 10)),
                "Core terrain must distinguish printed land, sea, and off-map coordinates.");
            Require(map.Bases.Count == 6 && map.BaseAt(new HexCoord(9, 4))?.Name == "Kadena AB" &&
                    map.BaseAt(new HexCoord(7, 16))?.Name == "Subic Bay / Clark",
                "All six marked First Island Chain bases must live in core map data.");

            var coastPath = map.FindPath(new HexCoord(7, 13), new HexCoord(9, 13), Side.UsNavy);
            Require(coastPath.Count > 0 && coastPath.Count - 1 > new HexCoord(7, 13).DistanceTo(new HexCoord(9, 13)) &&
                    coastPath.Zip(coastPath.Skip(1), (left, right) => left.IsAdjacentTo(right)).All(value => value) &&
                    coastPath.All(hex => map.IsNavigable(hex, Side.UsNavy)),
                "Core pathfinding must route step-by-step around the Taiwan coastline.");

            var terrainGame = new ScenarioOneGame(1, null, true);
            Require(terrainGame.DrawMovementChit().Accepted, "Terrain test must draw the US chit.");
            var side = terrainGame.State.ActiveSide;
            Require(terrainGame.DeclareSpeed(side, 1).Accepted, "A legal speed must be accepted.");
            var landMove = terrainGame.Execute(new GameCommand(GameCommandType.Move, side,
                terrainGame.State.Revision, new HexCoord(8, 13)));
            Require(!landMove.Accepted && landMove.Violation.Code == RuleViolationCode.ImpassableTerrain,
                "Movement into a printed land hex must be rejected by the core.");

            var edgeGame = new ScenarioOneGame(1, null, true);
            Require(edgeGame.DrawMovementChit().Accepted, "Edge test must draw the US chit.");
            side = edgeGame.State.ActiveSide;
            edgeGame.State.ForceFor(side).MoveTo(new HexCoord(15, 10));
            Require(edgeGame.DeclareSpeed(side, 1).Accepted, "Edge movement test must declare speed.");
            var offMapMove = edgeGame.Execute(new GameCommand(GameCommandType.Move, side,
                edgeGame.State.Revision, new HexCoord(16, 10)));
            Require(!offMapMove.Accepted && offMapMove.Violation.Code == RuleViolationCode.OutsideMap,
                "An adjacent step beyond the printed map edge must be rejected by the core.");

            var farGame = new ScenarioOneGame(1, null, true);
            Require(farGame.DrawMovementChit().Accepted, "Adjacency test must draw the US chit.");
            side = farGame.State.ActiveSide;
            Require(farGame.DeclareSpeed(side, 2).Accepted, "Speed two must be legal for the US formation.");
            var farMove = farGame.Execute(new GameCommand(GameCommandType.Move, side,
                farGame.State.Revision, new HexCoord(9, 11)));
            Require(!farMove.Accepted && farMove.Violation.Code == RuleViolationCode.NotAdjacent,
                "A movement command must never teleport across multiple hexes.");
            var incompleteEnd = farGame.Execute(new GameCommand(GameCommandType.EndActivation, side,
                farGame.State.Revision));
            Require(!incompleteEnd.Accepted && incompleteEnd.Violation.Code == RuleViolationCode.MovementIncomplete,
                "A task force must spend every movement point it declared before ending activation.");

            var speedGame = new ScenarioOneGame(1, null, true);
            Require(speedGame.DrawMovementChit().Accepted, "Speed test must draw the US chit.");
            side = speedGame.State.ActiveSide;
            var excessive = speedGame.DeclareSpeed(side, 3);
            Require(!excessive.Accepted && excessive.Violation.Code == RuleViolationCode.InvalidSpeed,
                "Declared speed must not exceed the slowest active ship's effective speed.");

            var actionGame = new ScenarioOneGame(1, null, true);
            Require(actionGame.DrawMovementChit().Accepted, "Action-window test must draw the US chit.");
            side = actionGame.State.ActiveSide;
            Require(actionGame.DeclareSpeed(side, 2).Accepted, "Action-window test must declare speed.");
            Require(actionGame.TryMove(side, new HexCoord(7, 12), out _), "First movement step must be legal.");
            var firstSearch = actionGame.Execute(new GameCommand(GameCommandType.Search, side,
                actionGame.State.Revision));
            var repeatedSearch = actionGame.Execute(new GameCommand(GameCommandType.Search, side,
                actionGame.State.Revision));
            Require(firstSearch.Accepted && !repeatedSearch.Accepted &&
                    repeatedSearch.Violation.Code == RuleViolationCode.AlreadyActed,
                "Each entered hex must open exactly one Scenario 1 search opportunity.");
            var enteredHexAttack = actionGame.Execute(new GameCommand(GameCommandType.Attack, side,
                actionGame.State.Revision));
            Require(enteredHexAttack.Accepted && actionGame.State.Phase == ActivationPhase.PlayerMove,
                "An attack must be legal between movement steps without ending movement.");
            Require(actionGame.TryMove(side, new HexCoord(7, 11), out _) &&
                    actionGame.Execute(new GameCommand(GameCommandType.Search, side,
                        actionGame.State.Revision)).Accepted,
                "Entering the next hex must open a fresh action/search opportunity.");

            var coexistence = new ScenarioOneGame(1, null, true);
            Require(coexistence.DrawMovementChit().Accepted, "Coexistence test must draw the US chit.");
            side = coexistence.State.ActiveSide;
            coexistence.State.ForceFor(side == Side.UsNavy ? Side.Plan : Side.UsNavy)
                .MoveTo(new HexCoord(7, 12));
            Require(coexistence.DeclareSpeed(side, 1).Accepted &&
                    coexistence.TryMove(side, new HexCoord(7, 12), out _) &&
                    coexistence.State.Player.Position == coexistence.State.Enemy.Position &&
                    coexistence.State.Events.Any(item => item.Type == RuleEventType.MovementOpportunity &&
                        item.Detail.Contains("enemy-occupied")),
                "Enemy forces must coexist in one hex and open the combat/reaction window.");

            var oneHexDefinition = new UnitDefinition("minimum-speed", "Minimum Speed", Side.UsNavy,
                UnitRole.Objective, 0, 0, 0, 0, 0, 0, 0, 1);
            var oneHexForce = new TaskForceState("Minimum Speed Force", Side.UsNavy,
                new HexCoord(5, 5), new[] { new UnitState(oneHexDefinition) });
            Require(oneHexForce.EffectiveSpeed == 1,
                "An otherwise eligible task force must retain the rules minimum of one movement hex.");
        }

        private static void ValidateMovementChitSequence()
        {
            var customChits = new[]
            {
                new MovementChit("US Surface TF", Side.UsNavy),
                new MovementChit("PLAN Surface TF", Side.Plan),
                new MovementChit("P-8A Patrol", Side.UsNavy)
            };
            var firstCup = new MovementChitCup(new SeededDieRoller(44));
            var secondCup = new MovementChitCup(new SeededDieRoller(44));
            firstCup.Reset(customChits);
            secondCup.Reset(customChits);
            var firstOrder = Enumerable.Range(0, customChits.Length).Select(_ => firstCup.Draw().FormationId).ToArray();
            var secondOrder = Enumerable.Range(0, customChits.Length).Select(_ => secondCup.Draw().FormationId).ToArray();
            Require(firstOrder.SequenceEqual(secondOrder) && firstOrder.Distinct().Count() == customChits.Length &&
                    firstCup.IsEmpty,
                "Task-force and patrol-aircraft chits must draw reproducibly without replacement.");

            var splitGame = new ScenarioOneGame(1, null, true);
            Require(splitGame.State.Phase == ActivationPhase.AwaitingChit &&
                    splitGame.State.MovementCup.TotalCount == 2 && splitGame.State.MovementCup.Drawn.Count == 0,
                "Each turn must begin with one eligible chit per task force and no preselected activation.");
            var split = splitGame.Execute(new GameCommand(GameCommandType.SplitTaskForce, Side.UsNavy,
                splitGame.State.Revision, id: "split-before-draw", formationId: "US Task Force",
                newFormationId: "US Task Force 2", unitIds: new[] { "us-burke-iia" }));
            Require(split.Accepted && splitGame.State.Forces.Count == 3 &&
                    splitGame.State.MovementCup.TotalCount == 3 &&
                    splitGame.State.Formation("US Task Force 2").Position == splitGame.State.Formation("US Task Force").Position,
                "A legal pre-draw split must form a colocated force and add exactly one chit to the cup.");
            Require(splitGame.DrawMovementChit().Accepted, "The split cup must permit its first random draw.");
            var lateSplit = splitGame.Execute(new GameCommand(GameCommandType.SplitTaskForce, Side.Plan,
                splitGame.State.Revision, formationId: "PLAN Task Force", newFormationId: "PLAN Task Force 2",
                unitIds: new[] { "plan-type-054a" }));
            Require(!lateSplit.Accepted && lateSplit.Violation.Code == RuleViolationCode.SplitWindowClosed,
                "Task-force splitting must close immediately after the first chit is drawn.");
            var splitMirror = new ScenarioOneGame(99, null, true);
            splitMirror.ApplySnapshot(splitGame.CaptureSnapshot());
            Require(splitMirror.State.Forces.Count == 3 && splitMirror.State.MovementCup.TotalCount == 3 &&
                    splitMirror.State.ActiveFormationId == splitGame.State.ActiveFormationId,
                "Snapshots must preserve split formations, cup state, and the named active formation.");

            var sequence = new ScenarioOneGame(1, null, true);
            Require(sequence.State.TimeOfDay == TimeOfDay.Am && sequence.State.Day == 1 &&
                    sequence.DrawMovementChit().Accepted,
                "Turn 1 must begin on Day 1 AM and draw its first chit from the full cup.");
            var firstFormation = sequence.State.ActiveFormationId;
            Require(firstFormation == sequence.State.MovementCup.Drawn.Last().FormationId,
                "Only the formation named on the drawn chit may activate.");
            var inactiveSide = sequence.State.ActiveSide == Side.UsNavy ? Side.Plan : Side.UsNavy;
            var wrongFormation = sequence.DeclareSpeed(inactiveSide, 0);
            Require(!wrongFormation.Accepted && wrongFormation.Violation.Code == RuleViolationCode.WrongSide,
                "A side whose formation was not drawn must not activate.");
            Require(sequence.DeclareSpeed(sequence.State.ActiveSide, 0).Accepted,
                "The drawn formation must be able to declare speed zero.");
            var firstSide = sequence.State.ActiveSide;
            sequence.EndActivation(firstSide);
            Require(sequence.State.Turn == 1 && sequence.State.Phase == ActivationPhase.DeclareSpeed &&
                    sequence.State.ActiveFormationId != firstFormation && sequence.State.MovementCup.IsEmpty,
                "Ending the first activation must draw the sole remaining chit without ending the turn.");
            Require(sequence.DeclareSpeed(sequence.State.ActiveSide, 0).Accepted,
                "The second drawn formation must activate.");
            sequence.EndActivation(sequence.State.ActiveSide);
            Require(sequence.State.Turn == 2 && sequence.State.TimeOfDay == TimeOfDay.Pm &&
                    sequence.State.Phase == ActivationPhase.AwaitingChit &&
                    sequence.State.MovementCup.Remaining.Count == 2 && sequence.State.MovementCup.Drawn.Count == 0,
                "The turn must advance to PM only after the cup is empty and return every eligible chit.");

            CompleteZeroSpeedTurn(sequence);
            Require(sequence.State.Turn == 3 && sequence.State.Day == 1 &&
                    sequence.State.TimeOfDay == TimeOfDay.Night,
                "Three eight-hour turns must progress AM, PM, then Night within one day.");
            Require(sequence.DrawMovementChit().Accepted &&
                    sequence.DeclareSpeed(sequence.State.ActiveSide, 0).Accepted,
                "Night search test must activate a formation.");
            var visualAtNight = sequence.Execute(new GameCommand(GameCommandType.Search,
                sequence.State.ActiveSide, sequence.State.Revision, targetId: "visual"));
            Require(!visualAtNight.Accepted && visualAtNight.Violation.Code == RuleViolationCode.NightRestricted,
                "Visual searches must be prohibited during Night turns.");
        }

        private static void CompleteZeroSpeedTurn(ScenarioOneGame game)
        {
            Require(game.State.Phase == ActivationPhase.AwaitingChit && game.DrawMovementChit().Accepted,
                "A complete turn helper must draw the first chit.");
            Require(game.DeclareSpeed(game.State.ActiveSide, 0).Accepted,
                "First formation must hold at speed zero.");
            game.EndActivation(game.State.ActiveSide);
            Require(game.State.Phase == ActivationPhase.DeclareSpeed &&
                    game.DeclareSpeed(game.State.ActiveSide, 0).Accepted,
                "Second formation must be drawn and hold at speed zero.");
            game.EndActivation(game.State.ActiveSide);
        }

        private static void ValidateSurfaceDetection()
        {
            var direct = ScenarioOne.Create(true);
            direct.Player.MoveTo(new HexCoord(7, 13));
            direct.Enemy.MoveTo(new HexCoord(8, 13));
            direct.Enemy.DeclareRadar(true);
            var located = direct.Detection.Detect(Side.UsNavy, direct.Enemy,
                DetectionMethod.Esm, direct.Turn, classified: false);
            Require(located.Level == ContactLevel.Located && located.IsDetected,
                "A sensor contact must support a located state before its contents are classified.");
            direct.Detection.Restore(Array.Empty<ContactSnapshotData>());
            var esmSuccess = new DetectionResolver(new SequenceDieRoller(5));
            var esmFailure = new DetectionResolver(new SequenceDieRoller(6));
            Require(esmSuccess.ResolveEsm(direct.Player, direct.Enemy) &&
                    !esmFailure.ResolveEsm(direct.Player, direct.Enemy),
                "Adjacent ESM must detect a radiating enemy on 1-5 and fail on 6.");

            direct.Enemy.MoveTo(direct.Player.Position);
            direct.Enemy.DeclareRadar(false);
            Require(new DetectionResolver(new SequenceDieRoller(2)).ResolveVisual(
                        direct.Player, direct.Enemy, TimeOfDay.Am) &&
                    !new DetectionResolver(new SequenceDieRoller(3)).ResolveVisual(
                        direct.Player, direct.Enemy, TimeOfDay.Am) &&
                    !new DetectionResolver(new SequenceDieRoller(1)).ResolveVisual(
                        direct.Player, direct.Enemy, TimeOfDay.Night),
                "Visual search must detect on 1-2 by day, fail on 3-6, and be unavailable at Night.");

            var attackGate = new ScenarioOneGame(1, null, true, true,
                new SequenceDieRoller(1, 1, 1, 1, 1, 1, 1, 1));
            Require(attackGate.DrawMovementChit().Accepted && attackGate.State.ActiveSide == Side.UsNavy,
                "Detection attack-gate test must activate the US formation.");
            Require(attackGate.Execute(new GameCommand(GameCommandType.RadiateRadar, Side.UsNavy,
                        attackGate.State.Revision, enabled: false)).Accepted,
                "A formation must be able to declare radar silent.");
            Require(attackGate.DeclareSpeed(Side.UsNavy, 0).Accepted,
                "Radar declaration must permit the following speed declaration.");
            var hiddenAttack = attackGate.Execute(new GameCommand(GameCommandType.Attack, Side.UsNavy,
                attackGate.State.Revision));
            Require(!hiddenAttack.Accepted && hiddenAttack.Violation.Code == RuleViolationCode.TargetUndetected,
                "Attacks must reject against undetected task forces.");
            Require(!attackGate.ViewFor(Side.UsNavy).OpposingFormation.IsKnown &&
                    attackGate.ViewFor(Side.UsNavy).OpposingFormation.Units.Count == 0,
                "An undetected opponent must expose no formation contents in its enemy's private view.");
            attackGate.State.Detection.Detect(Side.UsNavy, attackGate.State.Enemy,
                DetectionMethod.Esm, attackGate.State.Turn);
            Require(attackGate.ViewFor(Side.UsNavy).OpposingFormation.Units.Count == 2,
                "A classified contact must reveal its task-force contents to all friendly forces.");
            var detectedAttack = attackGate.Execute(new GameCommand(GameCommandType.Attack, Side.UsNavy,
                attackGate.State.Revision));
            Require(detectedAttack.Accepted,
                "A detected target in weapon range must become attackable.");

            var radar = new ScenarioOneGame(1, null, true, true, new SequenceDieRoller(1));
            radar.State.Enemy.MoveTo(radar.State.Player.Position);
            Require(radar.DrawMovementChit().Accepted && radar.State.ActiveSide == Side.UsNavy,
                "SSR test must activate the US formation.");
            var missingRadarDeclaration = radar.DeclareSpeed(Side.UsNavy, 0);
            Require(!missingRadarDeclaration.Accepted &&
                    missingRadarDeclaration.Violation.Code == RuleViolationCode.RadarDeclarationRequired,
                "Detection play must require the beginning-of-activation radar declaration.");
            Require(radar.Execute(new GameCommand(GameCommandType.RadiateRadar, Side.UsNavy,
                        radar.State.Revision, enabled: true)).Accepted &&
                    radar.State.Detection.IsDetected(Side.UsNavy, radar.State.Enemy.Id),
                "Radiating SSR must automatically classify surface ships in the same hex.");

            var repeatVisual = new ScenarioOneGame(1, null, true, true,
                new SequenceDieRoller(1, 6, 1));
            repeatVisual.State.Enemy.MoveTo(repeatVisual.State.Player.Position);
            Require(repeatVisual.DrawMovementChit().Accepted && repeatVisual.State.ActiveSide == Side.UsNavy,
                "Repeat-visual test must activate the US formation.");
            Require(repeatVisual.Execute(new GameCommand(GameCommandType.RadiateRadar, Side.UsNavy,
                        repeatVisual.State.Revision, enabled: false)).Accepted &&
                    repeatVisual.DeclareSpeed(Side.UsNavy, 2).Accepted,
                "Visual search test must begin radar silent with movement available.");
            var firstVisual = repeatVisual.Execute(new GameCommand(GameCommandType.Search, Side.UsNavy,
                repeatVisual.State.Revision, targetId: repeatVisual.State.Enemy.Id, searchMode: "visual"));
            var secondVisual = repeatVisual.Execute(new GameCommand(GameCommandType.Search, Side.UsNavy,
                repeatVisual.State.Revision, targetId: repeatVisual.State.Enemy.Id, searchMode: "visual"));
            Require(firstVisual.Accepted && secondVisual.Accepted &&
                    repeatVisual.State.Player.MovementPointsSpent == 1 &&
                    repeatVisual.State.Detection.IsDetected(Side.UsNavy, repeatVisual.State.Enemy.Id),
                "A failed visual search may spend another remaining movement point and roll again.");

            repeatVisual.State.Detection.Lose(Side.UsNavy, repeatVisual.State.Enemy,
                DetectionMethod.Sonar, repeatVisual.State.Turn);
            var lost = repeatVisual.State.Detection.ContactFor(Side.UsNavy, repeatVisual.State.Enemy.Id);
            Require(lost.Level == ContactLevel.LostContact && !lost.IsDetected &&
                    repeatVisual.ViewFor(Side.UsNavy).OpposingFormation.Units.Count == 0,
                "Lost contact must retain only the last known location and hide formation contents again.");

            var snapshot = repeatVisual.CaptureSnapshot();
            var mirror = new ScenarioOneGame(99, null, true, true);
            mirror.ApplySnapshot(snapshot);
            Require(mirror.State.Detection.ContactFor(Side.UsNavy, mirror.State.Enemy.Id).Level ==
                    ContactLevel.LostContact && mirror.State.Player.RadarDeclaredThisActivation,
                "Snapshots must preserve contacts, lost-contact state, and radar declarations.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
