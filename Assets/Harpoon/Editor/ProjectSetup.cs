using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DiagnosticsProcess = System.Diagnostics.Process;
using DiagnosticsProcessStartInfo = System.Diagnostics.ProcessStartInfo;
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
        public const string DefaultReleaseVersion = "1.0.2";
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
            PlayerSettings.productName = "Harpoon Captain's Edition";
            PlayerSettings.companyName = "Open Source Harpoon Community";
            PlayerSettings.bundleVersion = ReleaseVersionFromCommandLine();
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
            ValidateSurfaceMissileCombat();
            ValidateNavalGunfire();
            ValidateShipDamageAndModernPlatforms();
            ValidateScenarioOneRelease();
            ValidateScenarioTwoRelease();
            ValidateScenarioThreeRelease();
            ValidateScenarioFourRelease();
            ValidateScenarioFiveRelease();
            ValidateScenarioSixRelease();
            ValidateScenarioSevenRelease();
            ValidateScenarioEightRelease();
            ValidateScenarioNineRelease();
            ValidateScenarioTenRelease();
            MvpDataValidation.ValidateOrThrow();

            ValidateLoopbackTransport();
            Debug.Log("HARPOON RULE VALIDATION PASSED (Scenarios 1-10 including tactical air, hidden contacts, dummy deception, undersea combat, convoy arrival, carrier objectives, redacted snapshots, legal scoring, replay, hot-seat flow, and TCP loopback).");
        }

        public static void BuildWindowsPlayer()
        {
            EnsureRenderingAssets();
            PlayerSettings.bundleVersion = ReleaseVersionFromCommandLine();
            const string outputDirectory = "Build/Windows";
            const string outputPath = outputDirectory + "/HarpoonCaptainsEdition.exe";
            // Build/Windows is generated output. Clearing it prevents a renamed release from
            // accidentally packaging stale executables or data directories from an older build.
            if (Directory.Exists(outputDirectory)) Directory.Delete(outputDirectory, true);
            Directory.CreateDirectory(outputDirectory);
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
            BuildAccessibilitySpeechHelper(outputDirectory);
            File.WriteAllText("Build/Windows/harpoon-version.txt", PlayerSettings.bundleVersion);
            Debug.Log($"HARPOON WINDOWS BUILD PASSED: {outputPath}");
        }

        private static void BuildAccessibilitySpeechHelper(string outputDirectory)
        {
            var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var framework = Path.Combine(windows, "Microsoft.NET", "Framework64", "v4.0.30319");
            var compiler = Path.Combine(framework, "csc.exe");
            var speechAssembly = Path.Combine(framework, "WPF", "System.Speech.dll");
            var source = Path.GetFullPath("Tools/AccessibilitySpeech/Program.cs");
            var output = Path.GetFullPath(Path.Combine(outputDirectory, "HarpoonAccessibilitySpeech.exe"));
            if (!File.Exists(compiler) || !File.Exists(speechAssembly) || !File.Exists(source))
                throw new InvalidOperationException("Windows accessibility speech build prerequisites are missing.");
            var startInfo = new DiagnosticsProcessStartInfo(compiler,
                $"/nologo /target:exe /optimize+ /reference:\"{speechAssembly}\" /out:\"{output}\" \"{source}\"")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using (var process = DiagnosticsProcess.Start(startInfo))
            {
                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0)
                    throw new InvalidOperationException("Accessibility speech helper build failed. " + stdout + stderr);
            }
        }

        private static string ReleaseVersionFromCommandLine()
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length - 1; index++)
                if (string.Equals(arguments[index], "-releaseVersion", StringComparison.OrdinalIgnoreCase))
                    return NormalizeVersion(arguments[index + 1]);
            return DefaultReleaseVersion;
        }

        private static string NormalizeVersion(string value)
        {
            var clean = (value ?? string.Empty).Trim().TrimStart('v', 'V');
            if (!ReleaseVersion.TryParse(clean, out var parsed))
                throw new InvalidOperationException($"Invalid release version '{value}'. Expected major.minor.patch.");
            return parsed.ToString();
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

        private static void ValidateNavalGunfire()
        {
            Require(GunCombatRules.InitialEngagementSucceeds(2, 2, 3) &&
                    !GunCombatRules.InitialEngagementSucceeds(2, 2, 4) &&
                    GunCombatRules.InitialEngagementSucceeds(3, 2, 6) &&
                    !GunCombatRules.InitialEngagementSucceeds(2, 3, 1),
                "Initial gun engagement must honor faster evasion and the equal-speed attacker roll of 1-3.");
            Require(GunCombatRules.BreakOffThreshold(3, 2) == 6 &&
                    GunCombatRules.BreakOffThreshold(2, 2) == 2 &&
                    GunCombatRules.BreakOffThreshold(1, 2) == 1,
                "Gun break-off must be automatic for the faster force, 1-2 at equal speed, and 1 for the slower force.");

            var gun = new UnitDefinition("gun-test", "Gun Test", Side.UsNavy, UnitRole.Escort,
                0, 0, 0, 0, 0, 2, 2, 4);
            var targetDefinition = new UnitDefinition("target-test", "Target Test", Side.Plan, UnitRole.Objective,
                0, 0, 0, 0, 0, 0, 2, 4);
            var screenedTarget = new UnitState(targetDefinition);
            var screened = new GunCombatResolver(new SequenceDieRoller(4, 4)).Fire(
                new UnitState(gun), screenedTarget, true);
            Require(screened.HullHits == 0 && screened.TargetWasScreened,
                "A screened target must subtract one from every gun die before consulting the Guns column.");
            var exposedTarget = new UnitState(targetDefinition);
            var exposed = new GunCombatResolver(new SequenceDieRoller(4, 4)).Fire(
                new UnitState(gun), exposedTarget, false);
            Require(exposed.HullHits == 2 && exposedTarget.HullRemaining == 2,
                "Every gun factor must roll independently on the Guns combat-table column.");

            var game = new ScenarioOneGame(7, null, true, false,
                new SequenceDieRoller(1, 4, 4, 4, 4, 4, 4));
            var closeAction = game.CaptureSnapshot();
            closeAction.activeSide = Side.UsNavy;
            closeAction.activeFormationId = game.State.Player.Id;
            closeAction.phase = ActivationPhase.PlayerAction;
            closeAction.usColumn = closeAction.planColumn;
            closeAction.usRow = closeAction.planRow;
            foreach (var formation in closeAction.formations.Where(item => item.side == Side.UsNavy))
            {
                formation.column = closeAction.planColumn;
                formation.row = closeAction.planRow;
            }
            foreach (var unit in closeAction.units)
            {
                unit.shortMissiles = 0;
                unit.longMissiles = 0;
            }
            game.ApplySnapshot(closeAction);
            Require(game.Execute(new GameCommand(GameCommandType.Attack, Side.UsNavy,
                        game.State.Revision, targetId: game.State.Enemy.Id)).Accepted &&
                    game.State.Phase == ActivationPhase.GunCombat &&
                    game.State.PendingGunCombat.Phase == GunCombatPhase.ArrangeAttacker,
                "A same-hex attack without missiles must open staged gunfire after passing the equal-speed engage roll.");
            Require(game.Execute(new GameCommand(GameCommandType.ArrangeGunfire, Side.UsNavy,
                    game.State.Revision, gunPairs: ScenarioOneGame.DefaultGunPairs(game.State.Player))).Accepted,
                "The attacker must be able to nominate one firing ship and one screened ship per pair.");
            Require(game.Execute(new GameCommand(GameCommandType.ArrangeGunfire, Side.Plan,
                    game.State.Revision, gunPairs: ScenarioOneGame.DefaultGunPairs(game.State.Enemy))).Accepted,
                "The defender must be able to lock its firing formation.");
            var engagement = game.State.PendingGunCombat;
            Require(engagement.Phase == GunCombatPhase.Firing &&
                    engagement.FiringOrder.First() == "us-burke-iia",
                "The strongest eligible gun factor must fire first rather than summing the formation's guns.");
            var snapshot = game.CaptureSnapshot();
            var mirror = new ScenarioOneGame(99, null, true);
            mirror.ApplySnapshot(snapshot);
            Require(mirror.State.PendingGunCombat != null &&
                    mirror.State.PendingGunCombat.FiringOrder.SequenceEqual(engagement.FiringOrder),
                "Snapshots must preserve gun pairings, firing order, round, and current decision.");
            var firstShot = game.Execute(new GameCommand(GameCommandType.FireGuns, Side.UsNavy,
                game.State.Revision, targetId: "plan-type-071", sourceUnitId: "us-burke-iia"));
            Require(firstShot.Accepted && firstShot.AttackReport.IsGunfire &&
                    firstShot.AttackReport.TargetWasScreened && firstShot.AttackReport.HullHits == 0,
                "Gunfire must retain explicit firing/target pairs and apply rollback screening to the selected ship.");
            Require(game.Execute(new GameCommand(GameCommandType.FireGuns, Side.Plan,
                game.State.Revision, targetId: "us-merchant", sourceUnitId: "plan-type-054a")).Accepted,
                "The next eligible opposing firing ship must act in gun-factor order.");
            Require(game.Execute(new GameCommand(GameCommandType.BreakOff, Side.UsNavy,
                    game.State.Revision, enabled: false)).Accepted &&
                    game.Execute(new GameCommand(GameCommandType.BreakOff, Side.Plan,
                    game.State.Revision, enabled: false)).Accepted &&
                    game.State.PendingGunCombat.Round == 2,
                "If neither force breaks off, the same engagement must continue into another firing round.");
        }

        private static void ValidateScenarioOneRelease()
        {
            var definition = FirstIslandChainScenarios.ContactOffBashiChannel;
            var state = ScenarioOne.Create();
            Require(state.Scenario == definition && state.MaximumTurns == 0 &&
                    state.Player.Position.Equals(new HexCoord(7, 13)) &&
                    state.Enemy.Position.Equals(new HexCoord(10, 10)) &&
                    state.Player.Units.Select(unit => unit.Definition.Id)
                        .SequenceEqual(new[] { "us-burke-iia", "us-merchant" }) &&
                    state.Enemy.Units.Select(unit => unit.Definition.Id)
                        .SequenceEqual(new[] { "plan-type-054a", "plan-type-071" }),
                "Scenario 1 must load its exact formations and starting hexes from the scenario definition.");

            var usWin = ScoringGame(1, 0, 0, 0);
            Require(usWin.Execute(new GameCommand(GameCommandType.Disengage, Side.UsNavy,
                        usWin.State.Revision)).Accepted && usWin.State.Result == "US NAVY VICTORY" &&
                    usWin.State.EndReason == ScenarioEndReason.Disengagement,
                "A legal disengagement must score a US objective-damage win.");
            var planWin = ScoringGame(0, 1, 0, 0);
            Require(planWin.Execute(new GameCommand(GameCommandType.Disengage, Side.UsNavy,
                        planWin.State.Revision)).Accepted && planWin.State.Result == "PLAN VICTORY",
                "A legal disengagement must score a PLAN objective-damage win.");
            var draw = ScoringGame(1, 1, 0, 0);
            Require(draw.Execute(new GameCommand(GameCommandType.Disengage, Side.UsNavy,
                        draw.State.Revision)).Accepted && draw.State.Result == "DRAW",
                "Equal objective and analogous escort damage must score a draw.");

            var escortTieBreak = ScoringGame(0, 0, 1, 0);
            var escortScore = escortTieBreak.CurrentScore();
            Require(escortScore.UsObjectiveDamage == 0 && escortScore.PlanObjectiveDamage == 0 &&
                    escortTieBreak.Execute(new GameCommand(GameCommandType.Disengage, Side.UsNavy,
                        escortTieBreak.State.Revision)).Accepted &&
                    escortTieBreak.State.Result == "US NAVY VICTORY",
                "Escort damage must remain outside the printed objective score and apply only as the documented tie-break.");

            var mutual = ScoringGame(0, 0, 0, 0);
            Require(mutual.Execute(new GameCommand(GameCommandType.RequestScoring, Side.UsNavy,
                        mutual.State.Revision)).Accepted && !mutual.State.IsGameOver &&
                    mutual.Execute(new GameCommand(GameCommandType.RequestScoring, Side.Plan,
                        mutual.State.Revision)).Accepted && mutual.State.IsGameOver &&
                    mutual.State.EndReason == ScenarioEndReason.MutualScoring,
                "Both players must be able to agree to score the current position.");

            var exhausted = ScoringGame(0, 0, 0, 0, exhaustAmmunition: true);
            var noWeapon = exhausted.Execute(new GameCommand(GameCommandType.Attack, Side.UsNavy,
                exhausted.State.Revision, targetId: exhausted.State.Enemy.Id));
            Require(!noWeapon.Accepted && noWeapon.Violation.Code == RuleViolationCode.NoLegalWeapon &&
                    exhausted.Execute(new GameCommand(GameCommandType.Disengage, Side.UsNavy,
                        exhausted.State.Revision)).Accepted,
                "Ammunition exhaustion must reject an illegal attack but retain the explicit disengagement route.");

            var sunk = ScoringGame(0, 0, 0, 0, exhaustAmmunition: true, type071Damage: 2, sameHex: true,
                dieRoller: new SequenceDieRoller(1, 5, 5, 5));
            Require(sunk.Execute(new GameCommand(GameCommandType.Attack, Side.UsNavy,
                        sunk.State.Revision, targetId: sunk.State.Enemy.Id)).Accepted &&
                    sunk.Execute(new GameCommand(GameCommandType.ArrangeGunfire, Side.UsNavy,
                        sunk.State.Revision, gunPairs: ScenarioOneGame.DefaultGunPairs(sunk.State.Player))).Accepted &&
                    sunk.Execute(new GameCommand(GameCommandType.ArrangeGunfire, Side.Plan,
                        sunk.State.Revision, gunPairs: ScenarioOneGame.DefaultGunPairs(sunk.State.Enemy))).Accepted,
                "The objective-sinking test must enter a legal close-action firing sequence.");
            var sinkingShot = sunk.Execute(new GameCommand(GameCommandType.FireGuns, Side.UsNavy,
                sunk.State.Revision, targetId: "plan-type-071", sourceUnitId: "us-burke-iia"));
            Require(sinkingShot.Accepted && sunk.State.Unit("plan-type-071").IsSunk &&
                    sunk.State.IsGameOver && sunk.State.EndReason == ScenarioEndReason.ObjectiveSunk &&
                    sunk.State.Result == "US NAVY VICTORY",
                "Sinking the printed objective through legal combat must end and score the scenario immediately.");

            var seeded = new ScenarioOneGame(918273, null, true);
            var seededMirror = new ScenarioOneGame(1, null, true);
            seededMirror.ApplySnapshot(seeded.CaptureSnapshot());
            Require(seededMirror.Seed == 918273,
                "The selected deterministic seed must survive snapshot export and multiplayer restore.");
        }

        private static void ValidateScenarioTwoRelease()
        {
            var definition = FirstIslandChainScenarios.FlagshipDuel;
            var state = ScenarioOne.Create(false, definition);
            Require(state.Scenario == definition && state.MaximumTurns == 0 &&
                    state.Player.Position == new HexCoord(12, 13) &&
                    state.Enemy.Position == new HexCoord(5, 10) &&
                    state.Player.Units.Select(unit => unit.Definition.Id).SequenceEqual(new[]
                    {
                        "us-burke-iia-1", "us-burke-iia-2", "us-ticonderoga", "us-san-antonio"
                    }) && state.Enemy.Units.Single().Definition.Id == "plan-type-055",
                "Scenario 2 must load its exact modern order of battle and printed hex references.");

            var game = new ScenarioOneGame(2202, null, true, false, null, definition);
            var snapshot = game.CaptureSnapshot();
            snapshot.activeSide = Side.UsNavy;
            snapshot.activeFormationId = "US Flagship Group";
            snapshot.phase = ActivationPhase.PlayerAction;
            snapshot.units.Single(unit => unit.id == "plan-type-055").hullDamage = 2;
            snapshot.units.Single(unit => unit.id == "us-burke-iia-1").hullDamage = 1;
            snapshot.units.Single(unit => unit.id == "us-san-antonio").hullDamage = 2;
            game.ApplySnapshot(snapshot);
            Require(game.CurrentScore().UsObjectiveDamage == 2 &&
                    game.CurrentScore().PlanObjectiveDamage == 3 &&
                    game.Execute(new GameCommand(GameCommandType.Disengage, Side.UsNavy,
                        game.State.Revision)).Accepted && game.State.Result == "PLAN VICTORY",
                "Scenario 2 must aggregate hull hits across every opposing warship and score deterministically.");
        }

        private static void ValidateScenarioThreeRelease()
        {
            var definition = FirstIslandChainScenarios.CloseAboard;
            var state = ScenarioOne.Create(false, definition);
            Require(state.Scenario == definition && state.Player.Position == new HexCoord(13, 13) &&
                    state.Enemy.Position == new HexCoord(10, 10) && state.Player.Units.Count == 2 &&
                    state.Enemy.Units.Count == 3 &&
                    state.Player.Units.Select(unit => unit.Definition.Id).SequenceEqual(new[]
                        { "us-burke-iia", "us-constellation" }) &&
                    state.Enemy.Units.Select(unit => unit.Definition.Id).SequenceEqual(new[]
                        { "plan-type-056a-1", "plan-type-056a-2", "plan-type-056a-3" }),
                "Scenario 3 must load its exact modern order of battle and printed hex references.");

            var game = new ScenarioOneGame(3303, null, true, false, null, definition);
            var snapshot = game.CaptureSnapshot();
            snapshot.activeSide = Side.UsNavy;
            snapshot.activeFormationId = "US Close Action Group";
            snapshot.phase = ActivationPhase.PlayerAction;
            var plan = snapshot.units.Single(unit => unit.id == "plan-type-056a-1");
            plan.hullDamage = 1;
            plan.gunfireHullDamage = 0;
            var us = snapshot.units.Single(unit => unit.id == "us-constellation");
            us.hullDamage = 1;
            us.gunfireHullDamage = 1;
            game.ApplySnapshot(snapshot);
            Require(game.CurrentScore().UsObjectiveDamage == 0 &&
                    game.CurrentScore().PlanObjectiveDamage == 1,
                "Scenario 3 must exclude missile damage and preserve gunfire damage in its score.");
        }

        private static void ValidateScenarioFourRelease()
        {
            var definition = FirstIslandChainScenarios.PicketLine;
            var game = new ScenarioOneGame(4404, null, true, false, null, definition);
            Require(game.State.DetectionRulesEnabled && game.State.Player.Position == new HexCoord(7, 16) &&
                    game.State.Player.Units.Count == 5 && game.State.Enemy.Units.Count == 3,
                "Scenario 4 must load the full Subic convoy and PLAN picket with detection enabled.");
            var hidden = game.CaptureSnapshotFor(Side.UsNavy);
            Require(hidden.formations.Single(item => item.side == Side.Plan).column == 0 &&
                    hidden.formations.Single(item => item.side == Side.Plan).unitIds.Length == 0 &&
                    hidden.units.All(item => !item.id.StartsWith("plan-")) && hidden.transactions.Length == 0,
                "Scenario 4 must redact every undetected PLAN position, unit, and transaction from the US snapshot.");
            Require(!game.Execute(new GameCommand(GameCommandType.DeployFormation, Side.Plan,
                    game.State.Revision, new HexCoord(10, 15), formationId: "PLAN Picket Group")).Accepted &&
                    game.Execute(new GameCommand(GameCommandType.DeployFormation, Side.Plan,
                    game.State.Revision, new HexCoord(15, 10), formationId: "PLAN Picket Group")).Accepted,
                "Scenario 4 PLAN deployment must enforce both four-hex exclusion zones.");
        }

        private static void ValidateScenarioFiveRelease()
        {
            var game = new ScenarioOneGame(5505, null, true, false,
                new SequenceDieRoller(1), FirstIslandChainScenarios.GhostFleet);
            Require(game.State.Forces.Where(force => force.Side == Side.UsNavy).Sum(force => force.DummyCards) == 3 &&
                    game.State.Forces.Where(force => force.Side == Side.Plan).Sum(force => force.DummyCards) == 5,
                "Scenario 5 must load the printed three US and five PLAN dummy-card allotments.");
            Require(game.Execute(new GameCommand(GameCommandType.TransferDummyCards, Side.UsNavy,
                    game.State.Revision, factors: 1, formationId: "US Dummy Group",
                    newFormationId: "US Dummy Group 2")).Accepted &&
                    game.State.Forces.Where(force => force.Side == Side.UsNavy).Sum(force => force.DummyCards) == 3,
                "Scenario 5 dummy transfers must preserve the side's allotment.");
            var usSnapshot = game.CaptureSnapshotFor(Side.UsNavy);
            Require(usSnapshot.formations.Where(item => item.side == Side.Plan).All(item => item.dummyCards == 0),
                "Scenario 5 must not leak opposing dummy-card counts in side-private snapshots.");
        }

        private static void ValidateScenarioSixRelease()
        {
            var game = new ScenarioOneGame(6606, null, true, false, null,
                FirstIslandChainScenarios.WolvesOfBashiChannel);
            Require(game.State.MaximumTurns == 7 && game.State.Forces.Count == 5 &&
                    game.State.Forces.Where(force => force.Side == Side.Plan).All(force => force.IsSubmarineOnly) &&
                    game.State.Formation("US Los Angeles").IsSubmarineOnly,
                "Scenario 6 must load its exact separated surface/submarine forces and seven-turn limit.");
            var usView = game.CaptureSnapshotFor(Side.UsNavy);
            Require(usView.formations.Where(item => item.side == Side.Plan).All(item => item.unitIds.Length == 0),
                "Scenario 6 must redact every undetected PLAN submarine's identity and contents.");
            var mixed = new[] { game.State.Unit("us-los-angeles"), game.State.Unit("us-burke-iii") };
            var rejected = false;
            try { new TaskForceState("Illegal mixed force", Side.UsNavy, new HexCoord(15, 12), mixed); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "Scenario 6 must prohibit grouping submarines with surface vessels.");
        }

        private static void ValidateScenarioSevenRelease()
        {
            var game = new ScenarioOneGame(7707, null, true, false, null,
                FirstIslandChainScenarios.LifelineToTaiwan);
            Require(game.State.MaximumTurns == 10 && game.State.Forces.Count == 7 &&
                    game.State.Forces.Count(force => force.Side == Side.UsNavy) == 4 &&
                    game.State.Forces.Count(force => force.Side == Side.Plan) == 3 &&
                    game.State.Forces.SelectMany(force => force.Units)
                        .Count(unit => unit.Definition.Role == UnitRole.Objective) == 3,
                "Scenario 7 must load four independent US groups, three PLAN submarines, three merchants, and ten turns.");
            Require(!ScenarioOneGame.IsLegalDeploymentHex(game.State.Scenario, game.State.Map,
                    Side.UsNavy, new HexCoord(8, 10)) &&
                    ScenarioOneGame.IsLegalDeploymentHex(game.State.Scenario, game.State.Map,
                        Side.UsNavy, new HexCoord(9, 12)) &&
                    !ScenarioOneGame.IsLegalDeploymentHex(game.State.Scenario, game.State.Map,
                        Side.Plan, new HexCoord(11, 10)) &&
                    ScenarioOneGame.IsLegalDeploymentHex(game.State.Scenario, game.State.Map,
                        Side.Plan, new HexCoord(14, 14)),
                "Scenario 7 deployment zones and prohibited setup zones must match the printed setup.");
            var hidden = game.CaptureSnapshotFor(Side.UsNavy);
            Require(hidden.formations.Where(item => item.side == Side.Plan)
                    .All(item => item.column == 0 && item.unitIds.Length == 0),
                "Scenario 7 must keep every undetected PLAN submarine position and card private.");
        }

        private static void ValidateScenarioEightRelease()
        {
            var game = new ScenarioOneGame(8808, null, true, false, null,
                FirstIslandChainScenarios.HuntTheDragon);
            var fujian = game.State.Unit("plan-fujian");
            Require(game.State.MaximumTurns == 7 && game.State.Forces.Count == 8 &&
                    game.State.Forces.Count(force => force.Side == Side.UsNavy && force.IsSubmarineOnly) == 4 &&
                    game.State.Forces.Count(force => force.Side == Side.Plan) == 4 &&
                    game.State.Forces.Where(force => force.Side == Side.Plan)
                        .All(force => force.Position.Row == 12 && force.Position.Column >= 8 &&
                            force.Position.Column <= 12 && game.State.Map.IsNavigable(force.Position, Side.Plan)),
                "Scenario 8 must load four independent US SSNs and the four-ship Fujian group for seven turns.");
            Require(fujian.Definition.IsAircraftCarrier && fujian.Definition.EmbarkedAircraftCapacity == 1 &&
                    fujian.CanLaunchAircraft,
                "Fujian must begin with one scenario-scale embarked air group and intact launch capability.");
            Require(ScenarioOneGame.DistanceToPatrolLine(game.State.Scenario, new HexCoord(3, 12)) == 0 &&
                    ScenarioOneGame.DistanceToPatrolLine(game.State.Scenario, new HexCoord(9, 9)) == 3 &&
                    ScenarioOneGame.IsBoardEdgeHex(game.State.Map, new HexCoord(15, 9), BoardEdge.East, Side.UsNavy) &&
                    ScenarioOneGame.IsBoardEdgeHex(game.State.Map, new HexCoord(3, 12), BoardEdge.West, Side.Plan),
                "Scenario 8 east entry, west exit, and two-hex patrol-axis geometry must be authoritative.");
            fujian.ApplyDamage(3);
            Require(!fujian.CanLaunchAircraft,
                "Half damage to Fujian must prohibit aircraft launch and therefore its victory exit.");
        }

        private static void ValidateScenarioNineRelease()
        {
            var game = new ScenarioOneGame(9909, null, true, false, null,
                FirstIslandChainScenarios.Patroller);
            var p8 = game.State.Unit("us-p8a");
            Require(game.State.MaximumTurns == 15 && game.State.Forces.Count == 6 &&
                    game.State.Forces.Count(force => force.Side == Side.Plan && force.IsSubmarineOnly) == 4,
                "Scenario 9 must load four independent PLAN submarines, the US SSN, P-8A, and fifteen turns.");
            Require(p8.Definition.IsPatrolAircraft && p8.Definition.AircraftRadius == 20 &&
                    p8.Definition.SurfaceSearchRadar == 3 && p8.Definition.Sonar == 4 &&
                    p8.Definition.AntiSubmarineWarfare == 5 && p8.ServiceableAircraftRemaining == 4,
                "Scenario 9 must use the printed modern P-8A card and four-box roster.");
            Require(CombatTables.AircraftDamage(1) == AircraftDamageResult.NoEffect &&
                    CombatTables.AircraftDamage(2) == AircraftDamageResult.Abort &&
                    CombatTables.AircraftDamage(4) == AircraftDamageResult.ShotDown,
                "Scenario 9 must use the printed Aircraft Damage table.");
            Require(ScenarioOneGame.IsLegalDeploymentHex(game.State.Scenario, game.State.Map,
                    Side.Plan, new HexCoord(5, 10)) &&
                    ScenarioOneGame.IsLegalDeploymentHex(game.State.Scenario, game.State.Map,
                        Side.UsNavy, new HexCoord(15, 20)),
                "Scenario 9 must enforce Xiamen-centered setup distances.");
        }

        private static void ValidateScenarioTenRelease()
        {
            var game = new ScenarioOneGame(1010, null, true, false, null,
                FirstIslandChainScenarios.FirstLight);
            Require(game.State.MaximumTurns == 12 && game.State.Forces.Count == 3 &&
                    game.State.TacticalFlights.Count == 15 && game.State.AirBases.Count == 2,
                "Scenario 10 must load Ford, two Type 093Bs, fifteen tactical flights, two active bases, and twelve turns.");
            Require(ModernTacticalAircraftDatabase.Get("us-f35c").Radius == 10 &&
                    ModernTacticalAircraftDatabase.Get("plan-h6j").LongAsm == 5 &&
                    ModernAirBaseDatabase.Get("us-ford-wing").FlightCapacity == 14 &&
                    ModernAirBaseDatabase.Get("plan-ningbo").LongSam == 10,
                "Scenario 10 tactical aircraft, deck capacity, and Ningbo defenses must match the supplement.");
            Require(CombatTables.AirToAirHits(2) == 0 && CombatTables.AirToAirHits(3) == 1 &&
                    CombatTables.AirToAirHits(7) == 1 && CombatTables.AirToAirHits(8) == 2,
                "Scenario 10 must use every printed Air-to-Air table boundary.");
            Require(game.Execute(new GameCommand(GameCommandType.AssignCap, Side.UsNavy,
                    game.State.Revision, sourceUnitId: "FORD-F35-1", enabled: true)).Accepted &&
                    game.Execute(new GameCommand(GameCommandType.AssignDeckInterceptor, Side.UsNavy,
                        game.State.Revision, sourceUnitId: "FORD-F35-2")).Accepted,
                "Scenario 10 must accept pre-chit carrier CAP and DLI declarations.");
            var mirror = new ScenarioOneGame(1, null, true);
            mirror.ApplySnapshot(game.CaptureSnapshot());
            Require(mirror.State.TacticalFlight("FORD-F35-1").Mission == TacticalAirMission.Cap &&
                    mirror.State.TacticalFlight("FORD-F35-2").Mission == TacticalAirMission.DeckInterceptor,
                "Scenario 10 snapshots must preserve tactical defensive missions.");
        }

        private static ScenarioOneGame ScoringGame(int usObjectiveDamage, int planObjectiveDamage,
            int usTieBreakDamage, int planTieBreakDamage, bool exhaustAmmunition = false,
            int type071Damage = -1, bool sameHex = false, IDieRoller dieRoller = null)
        {
            var game = new ScenarioOneGame(4242, null, true, false, dieRoller);
            var snapshot = game.CaptureSnapshot();
            snapshot.activeSide = Side.UsNavy;
            snapshot.activeFormationId = "US Task Force";
            snapshot.phase = ActivationPhase.PlayerAction;
            foreach (var unit in snapshot.units)
            {
                if (unit.id == "plan-type-071") unit.hullDamage = type071Damage >= 0 ? type071Damage : usObjectiveDamage;
                else if (unit.id == "us-merchant") unit.hullDamage = planObjectiveDamage;
                else if (unit.id == "plan-type-054a") unit.hullDamage = usTieBreakDamage;
                else if (unit.id == "us-burke-iia") unit.hullDamage = planTieBreakDamage;
                if (exhaustAmmunition)
                {
                    unit.shortMissiles = 0;
                    unit.longMissiles = 0;
                }
            }
            if (sameHex)
            {
                var us = snapshot.formations.First(item => item.side == Side.UsNavy);
                var plan = snapshot.formations.First(item => item.side == Side.Plan);
                us.column = plan.column;
                us.row = plan.row;
            }
            game.ApplySnapshot(snapshot);
            return game;
        }

        private static void ValidateShipDamageAndModernPlatforms()
        {
            var platforms = ModernPlatformDatabase.All;
            Require(platforms.Count == 34 && platforms.Select(item => item.Id).Distinct().Count() == 34,
                "The modern platform database must contain all 34 hull-bearing cards from supplement pages 15-21.");
            Require(platforms.Select(item => item.Hull).Distinct().OrderBy(value => value)
                    .SequenceEqual(new[] { 1, 2, 3, 4, 5, 6 }) &&
                    platforms.All(item => item.Hull >= 1 && item.Hull <= 6),
                "The modern database must exercise every printed ship hull rating from one through six.");
            Require(platforms.Count(item => item.LaunchesAircraft) == 8 &&
                    ModernPlatformDatabase.Get("us-ford").Hull == 6 &&
                    ModernPlatformDatabase.Get("us-nimitz").Hull == 5 &&
                    ModernPlatformDatabase.Get("plan-type-055").Guns == 3 &&
                    ModernPlatformDatabase.Get("us-ohio-ssgn").LongSsm == 16 &&
                    ModernPlatformDatabase.Get("plan-type-093b").Torpedoes == 5,
                "Modern carrier, surface-combatant, and submarine card values must match the supplied supplement.");

            var expectedThresholds = new[,]
            {
                { 1, 1 }, { 1, 2 }, { 2, 2 }, { 2, 3 }, { 3, 4 }, { 3, 4 }
            };
            for (var hull = 1; hull <= 6; hull++)
            {
                Require(UnitState.HalfDamageThresholdFor(hull) == expectedThresholds[hull - 1, 0] &&
                        UnitState.TwoThirdsDamageThresholdFor(hull) == expectedThresholds[hull - 1, 1],
                    $"Hull {hull} threshold rounding must be explicit and match Captain's Rules page 4.");
                var definition = new UnitDefinition($"damage-{hull}", $"Hull {hull} Damage Test",
                    Side.UsNavy, UnitRole.Escort, 4, 6, 3, 2, 4, 3, 3, hull,
                    airSearchRadar: 2, surfaceSearchRadar: 1, sonar: 4,
                    antiSubmarineWarfare: 5, esmEquipped: true, isAircraftCarrier: true,
                    torpedoes: 4);
                var halfShip = new UnitState(definition);
                var halfResult = halfShip.ApplyDamage(expectedThresholds[hull - 1, 0]);
                if (!halfShip.IsSunk && !halfShip.HasTwoThirdsDamage)
                    Require(halfShip.HasHalfDamage && halfShip.EffectiveSpeed == 2 &&
                            halfShip.AvailableLongSsm == 0 && halfShip.AvailableShortSsm == 2 &&
                            halfShip.EffectiveLongSam == 0 && halfShip.EffectiveAirSearchRadar == 0 &&
                            !halfShip.CanLaunchAircraft && halfResult.CrossedThreshold,
                        $"Hull {hull} must apply every half-damage capability loss at its rounded threshold.");

                var crippled = new UnitState(definition);
                var crippledResult = crippled.ApplyDamage(expectedThresholds[hull - 1, 1]);
                if (!crippled.IsSunk)
                    Require(crippled.HasTwoThirdsDamage && crippled.EffectiveSpeed == 1 &&
                            crippled.EffectiveShortSam == 0 && crippled.EffectiveLongSam == 0 &&
                            crippled.EffectivePointDefense == 0 && crippled.AvailableShortSsm == 0 &&
                            crippled.AvailableLongSsm == 0 && crippled.EffectiveTorpedoes == 0 &&
                            crippled.EffectiveAntiSubmarineWarfare == 0 && crippled.EffectiveSonar == 0 &&
                            !crippled.EffectiveEsm && crippled.EffectiveSurfaceSearchRadar == 1 &&
                            crippled.EffectiveGuns == 2 && !crippled.CanLaunchAircraft &&
                            crippledResult.CrossedThreshold,
                        $"Hull {hull} must lose all weapons except half guns and retain SSR at two-thirds damage.");

                var sunk = new UnitState(definition);
                var sunkResult = sunk.ApplyDamage(hull + 3);
                Require(sunk.IsSunk && sunk.HullRemaining == 0 && sunk.HullDamage == hull &&
                        sunkResult.AppliedHits == hull && sunkResult.SunkNow &&
                        sunk.EffectiveSpeed == 0 && sunk.EffectiveGuns == 0 &&
                        sunk.EffectiveSurfaceSearchRadar == 0 && sunk.EffectiveTorpedoes == 0,
                    $"Hull {hull} sinking must cap overkill and remove every capability.");
            }

            foreach (var platform in platforms)
            {
                var ship = new UnitState(platform.CreateUnit(platform.DefaultSide ?? Side.UsNavy,
                    UnitRole.Escort));
                Require(ship.HalfDamageThreshold == expectedThresholds[platform.Hull - 1, 0] &&
                        ship.TwoThirdsDamageThreshold == expectedThresholds[platform.Hull - 1, 1],
                    $"{platform.DisplayName} must use the threshold row for its printed hull {platform.Hull}.");
            }

            var slowDefinition = new UnitDefinition("damaged-slow", "Damaged Slow Ship", Side.UsNavy,
                UnitRole.Escort, 0, 0, 0, 0, 0, 0, 3, 5, surfaceSearchRadar: 1);
            var fastDefinition = new UnitDefinition("fast", "Fast Ship", Side.UsNavy,
                UnitRole.Escort, 0, 0, 0, 0, 0, 0, 4, 2);
            var slow = new UnitState(slowDefinition);
            var fast = new UnitState(fastDefinition);
            var force = new TaskForceState("Damage Movement", Side.UsNavy, new HexCoord(4, 4),
                new[] { slow, fast });
            force.DeclareSpeed(3);
            force.MoveOneHex(new HexCoord(5, 4));
            force.DeclareRadar(true);
            slow.ApplyDamage(3);
            Require(force.EffectiveSpeed == 2 && force.MovementAllowance == 2 &&
                    force.MovementRemaining == 1 && force.RadarRadiating,
                "Mid-activation half damage must immediately clamp remaining task-force movement.");
            slow.ApplyDamage(2);
            Require(force.ActiveUnits.Count() == 1 && force.EffectiveSpeed == 4 &&
                    !force.RadarRadiating,
                "A sunk ship must leave task-force speed and sensor calculations immediately.");
            var destroyedForce = new TaskForceState("Destroyed Chit", Side.Plan, new HexCoord(7, 7),
                new[] { new UnitState(new UnitDefinition("doomed", "Doomed Ship", Side.Plan,
                    UnitRole.Escort, 0, 0, 0, 0, 0, 0, 2, 1)) });
            var cup = new MovementChitCup(new SequenceDieRoller(1));
            cup.Reset(new[] { force, destroyedForce });
            destroyedForce.Units.First().ApplyDamage(1);
            Require(cup.RemoveUndrawnFormation(destroyedForce.Id) &&
                    cup.Remaining.All(item => item.FormationId != destroyedForce.Id),
                "A formation sunk before its activation must lose its undrawn movement chit immediately.");

            var snapshotGame = new ScenarioOneGame(808, null, true);
            snapshotGame.State.Player.Units.First().ApplyDamage(1);
            var snapshot = snapshotGame.CaptureSnapshot();
            var snapshotMirror = new ScenarioOneGame(909, null, true);
            snapshotMirror.ApplySnapshot(snapshot);
            var restored = snapshotMirror.State.Player.Units.First();
            Require(restored.HullDamage == 1 && restored.HasHalfDamage &&
                    restored.EffectiveSpeed == 2 && restored.AvailableLongSsm == 0,
                "Snapshots must preserve hull boxes and recompute all derived damage effects.");
        }

        private static void ValidateLoopbackTransport()
        {
            using (var host = new MultiplayerNetwork())
            using (var client = new MultiplayerNetwork())
            {
                // Port zero asks the OS for a free ephemeral port. StartHost binds synchronously,
                // so the client cannot race the listener or collide with an unrelated service.
                host.StartHost(0);
                Require(host.ListeningPort > 0, "TCP loopback host must bind an ephemeral port.");
                client.StartClient("127.0.0.1", host.ListeningPort);
                // Batch-mode startup can briefly starve the accept thread while Unity finishes
                // asset cleanup; allow enough time to distinguish that from a transport failure.
                var deadline = DateTime.UtcNow.AddSeconds(12);
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
            Require(map.TerrainAt(new HexCoord(8, 12)) == TerrainType.Land &&
                    map.TerrainAt(new HexCoord(9, 8)) == TerrainType.Land &&
                    map.TerrainAt(new HexCoord(7, 12)) == TerrainType.Land &&
                    map.TerrainAt(new HexCoord(9, 9)) == TerrainType.Land &&
                    map.TerrainAt(new HexCoord(7, 11)) == TerrainType.Land &&
                    map.TerrainAt(new HexCoord(8, 13)) == TerrainType.Sea &&
                    map.TerrainAt(new HexCoord(7, 14)) == TerrainType.Sea &&
                    map.TerrainAt(new HexCoord(9, 13)) == TerrainType.Sea &&
                    map.TerrainAt(new HexCoord(9, 3)) == TerrainType.Sea &&
                    map.TerrainAt(new HexCoord(8, 7)) == TerrainType.Land &&
                    map.TerrainAt(new HexCoord(9, 19)) == TerrainType.Land &&
                    map.TerrainAt(new HexCoord(7, 19)) == TerrainType.Sea &&
                    map.TerrainAt(new HexCoord(7, 20)) == TerrainType.Sea &&
                    map.TerrainAt(new HexCoord(2, 17)) == TerrainType.Land &&
                    !map.Contains(new HexCoord(16, 10)),
                "Core terrain must preserve separated Ryukyus, proportioned Taiwan and Luzon, a continuous China coast, the Bashi sea passage, and off-map coordinates.");
            Require(map.Bases.Count == 6 && map.BaseAt(new HexCoord(9, 4))?.Name == "Kadena AB" &&
                    map.BaseAt(new HexCoord(8, 16))?.Name == "Subic Bay / Clark",
                "All six marked First Island Chain bases must live in core map data.");
            var groundedNavalForces = FirstIslandChainScenarios.Introductory
                .SelectMany(scenario => ScenarioOne.Create(false, scenario).Forces
                    .Where(force => !force.IsAircraftOnly && !force.IsDummyOnly)
                    .Select(force => new { Scenario = scenario.Id, Force = force }))
                .Where(item => map.TerrainAt(item.Force.Position) != TerrainType.Sea)
                .ToArray();
            Require(groundedNavalForces.Length == 0,
                "Every scenario naval formation must begin at sea: " +
                string.Join(", ", groundedNavalForces.Select(item =>
                    $"{item.Scenario} {item.Force.Id} {item.Force.Position}")));

            var coastPath = map.FindPath(new HexCoord(7, 13), new HexCoord(8, 14), Side.UsNavy);
            Require(coastPath.Count == 3 &&
                    coastPath.Any(hex => hex == new HexCoord(8, 13) || hex == new HexCoord(7, 14)) &&
                    coastPath.Zip(coastPath.Skip(1), (left, right) => left.IsAdjacentTo(right)).All(value => value) &&
                    coastPath.All(hex => map.IsNavigable(hex, Side.UsNavy)),
                "Core pathfinding must cross the open Bashi passage south of Taiwan.");

            var terrainGame = new ScenarioOneGame(1, null, true);
            Require(terrainGame.DrawMovementChit().Accepted, "Terrain test must draw the US chit.");
            var side = terrainGame.State.ActiveSide;
            Require(terrainGame.DeclareSpeed(side, 1).Accepted, "A legal speed must be accepted.");
            var landMove = terrainGame.Execute(new GameCommand(GameCommandType.Move, side,
                terrainGame.State.Revision, new HexCoord(8, 12)));
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
            Require(actionGame.TryMove(side, new HexCoord(8, 13), out _), "First movement step must be legal.");
            var firstSearch = actionGame.Execute(new GameCommand(GameCommandType.Search, side,
                actionGame.State.Revision));
            var repeatedSearch = actionGame.Execute(new GameCommand(GameCommandType.Search, side,
                actionGame.State.Revision));
            Require(firstSearch.Accepted && !repeatedSearch.Accepted &&
                    repeatedSearch.Violation.Code == RuleViolationCode.AlreadyActed,
                "Each entered hex must open exactly one Scenario 1 search opportunity.");
            var enteredHexAttack = actionGame.Execute(new GameCommand(GameCommandType.Attack, side,
                actionGame.State.Revision));
            if (enteredHexAttack.Accepted) ResolveDefaultMissileExchange(actionGame, declineCounterattack: true);
            Require(enteredHexAttack.Accepted && actionGame.State.Phase == ActivationPhase.PlayerMove,
                "An attack must be legal between movement steps without ending movement.");
            Require(actionGame.TryMove(side, new HexCoord(8, 14), out _) &&
                    actionGame.Execute(new GameCommand(GameCommandType.Search, side,
                        actionGame.State.Revision)).Accepted,
                "Entering the next hex must open a fresh action/search opportunity.");

            var coexistence = new ScenarioOneGame(1, null, true);
            Require(coexistence.DrawMovementChit().Accepted, "Coexistence test must draw the US chit.");
            side = coexistence.State.ActiveSide;
            coexistence.State.ForceFor(side == Side.UsNavy ? Side.Plan : Side.UsNavy)
                .MoveTo(new HexCoord(8, 13));
            Require(coexistence.DeclareSpeed(side, 1).Accepted &&
                    coexistence.TryMove(side, new HexCoord(8, 13), out _) &&
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

        private static void ValidateSurfaceMissileCombat()
        {
            var overfire = new ScenarioOneGame(1, null, true, false, new SequenceDieRoller(1));
            Require(overfire.DrawMovementChit().Accepted && overfire.State.ActiveSide == Side.UsNavy &&
                    overfire.DeclareSpeed(Side.UsNavy, 0).Accepted &&
                    overfire.Execute(new GameCommand(GameCommandType.Attack, Side.UsNavy,
                        overfire.State.Revision)).Accepted,
                "Missile allocation test must open a legal range-three US raid.");
            var overAllocation = overfire.Execute(new GameCommand(GameCommandType.AllocateMissileFire,
                Side.UsNavy, overfire.State.Revision, missileAllocations: new[]
                {
                    new MissileAllocationData
                    {
                        id = "OVER", sourceUnitId = "us-burke-iia", targetUnitId = "plan-type-071",
                        longFactors = 2
                    }
                }));
            Require(!overAllocation.Accepted && overAllocation.Violation.Code == RuleViolationCode.InsufficientAmmunition &&
                    overfire.State.Player.Units[0].LongMissilesRemaining == 1,
                "A raid must reject over-allocation without spending ammunition.");
            var partial = overfire.Execute(new GameCommand(GameCommandType.AllocateMissileFire,
                Side.UsNavy, overfire.State.Revision, missileAllocations: new[]
                {
                    new MissileAllocationData
                    {
                        id = "PARTIAL", sourceUnitId = "us-burke-iia", targetUnitId = "plan-type-071",
                        longFactors = 1
                    }
                }));
            Require(partial.Accepted && overfire.State.Player.Units[0].ShortMissilesRemaining == 2 &&
                    overfire.State.Player.Units[0].LongMissilesRemaining == 0,
                "A player must be able to fire only selected in-range factors and retain the rest.");
            var missileSnapshot = overfire.CaptureSnapshot();
            var missileMirror = new ScenarioOneGame(99, null, true);
            missileMirror.ApplySnapshot(missileSnapshot);
            Require(missileMirror.State.Phase == ActivationPhase.MissileCombat &&
                    missileMirror.State.PendingMissileCombat?.Salvos.Count == 1 &&
                    missileMirror.State.Player.Units[0].LongMissilesRemaining == 0,
                "Snapshots must preserve a pending staged raid, its salvos, and committed ammunition.");
            var missileReplay = ScenarioOneGame.Replay(overfire.Seed, overfire.State.CommandLog);
            Require(missileReplay.State.Phase == ActivationPhase.MissileCombat &&
                    missileReplay.State.PendingMissileCombat?.Salvos.Count == 1 &&
                    missileReplay.State.Player.Units[0].LongMissilesRemaining == 0,
                "Replay must reproduce explicit missile allocations and ammunition commitment.");
            ResolveDefaultMissileExchange(overfire, true);

            var splitFire = new ScenarioOneGame(1, null, true, false,
                new SequenceDieRoller(1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1));
            splitFire.State.Enemy.MoveTo(new HexCoord(8, 13));
            Require(splitFire.DrawMovementChit().Accepted && splitFire.State.ActiveSide == Side.UsNavy &&
                    splitFire.DeclareSpeed(Side.UsNavy, 0).Accepted &&
                    splitFire.Execute(new GameCommand(GameCommandType.Attack, Side.UsNavy,
                        splitFire.State.Revision)).Accepted,
                "Split-fire test must open a range-one missile raid.");
            var splitAllocation = splitFire.Execute(new GameCommand(GameCommandType.AllocateMissileFire,
                Side.UsNavy, splitFire.State.Revision, missileAllocations: new[]
                {
                    new MissileAllocationData
                    {
                        id = "ESCORT", sourceUnitId = "us-burke-iia", targetUnitId = "plan-type-054a",
                        shortFactors = 1
                    },
                    new MissileAllocationData
                    {
                        id = "ROLLBACK", sourceUnitId = "us-burke-iia", targetUnitId = "plan-type-071",
                        shortFactors = 1, longFactors = 1
                    }
                }));
            Require(splitAllocation.Accepted && splitFire.State.PendingMissileCombat.Salvos.Count == 2 &&
                    splitFire.State.Player.Units[0].ShortMissilesRemaining == 0 &&
                    splitFire.State.Player.Units[0].LongMissilesRemaining == 0,
                "One firing ship must be able to split legal factors across both ships in a defensive pair.");
            var planPair = new[] { new DefensePairData
            {
                firstUnitId = "plan-type-054a", secondUnitId = "plan-type-071"
            } };
            Require(splitFire.Execute(new GameCommand(GameCommandType.Defend, Side.Plan,
                splitFire.State.Revision, defensePairs: planPair)).Accepted,
                "The defender must choose its ship pairing before local SAM fire.");
            var illegalDoubleFire = splitFire.Execute(new GameCommand(GameCommandType.Defend, Side.Plan,
                splitFire.State.Revision, shortRangeDefenses: new[]
                {
                    new ShortRangeDefenseData { defendingUnitId = "plan-type-054a", salvoId = "ESCORT" },
                    new ShortRangeDefenseData { defendingUnitId = "plan-type-054a", salvoId = "ROLLBACK" }
                }));
            Require(!illegalDoubleFire.Accepted && illegalDoubleFire.Violation.Code == RuleViolationCode.InvalidDefense,
                "A short-range SAM battery must not defend two salvos or ships outside itself/its pair-mate.");
            var splitResolution = splitFire.Execute(new GameCommand(GameCommandType.Defend, Side.Plan,
                splitFire.State.Revision, shortRangeDefenses: new[]
                {
                    new ShortRangeDefenseData { defendingUnitId = "plan-type-054a", salvoId = "ROLLBACK" }
                }));
            Require(splitResolution.Accepted && splitResolution.AttackReport != null &&
                    splitResolution.AttackReport.Strikes.Count == 2 &&
                    splitFire.State.PendingMissileCombat.Phase == MissileCombatPhase.CounterattackDecision,
                "Split fire must resolve each surviving target separately before offering the non-moving counterattack.");
            var movementOwner = splitFire.State.PendingMissileCombat.MovementOwnerFormationId;
            Require(splitFire.Execute(new GameCommand(GameCommandType.Counterattack, Side.Plan,
                        splitFire.State.Revision, enabled: false)).Accepted &&
                    splitFire.State.PendingMissileCombat == null &&
                    splitFire.State.ActiveFormationId == movementOwner &&
                    splitFire.State.Phase == ActivationPhase.PlayerAction,
                "Declining a counterattack must restore the moving formation's interrupted activation.");

            var longDefense = new ScenarioOneGame(1, null, true, false,
                new SequenceDieRoller(2, 4, 1, 1, 1, 1, 1, 1, 1, 4, 1, 1, 1, 1));
            longDefense.State.Player.MoveTo(longDefense.State.Enemy.Position);
            Require(longDefense.DrawMovementChit().Accepted && longDefense.State.ActiveSide == Side.Plan &&
                    longDefense.DeclareSpeed(Side.Plan, 0).Accepted &&
                    longDefense.Execute(new GameCommand(GameCommandType.Attack, Side.Plan,
                        longDefense.State.Revision)).Accepted,
                "Long-range defense test must open a PLAN raid against the US pair.");
            Require(longDefense.Execute(new GameCommand(GameCommandType.AllocateMissileFire, Side.Plan,
                longDefense.State.Revision, missileAllocations: new[]
                {
                    new MissileAllocationData
                    {
                        id = "AT-ESCORT", sourceUnitId = "plan-type-054a", targetUnitId = "us-burke-iia",
                        shortFactors = 1
                    },
                    new MissileAllocationData
                    {
                        id = "AT-MERCHANT", sourceUnitId = "plan-type-054a", targetUnitId = "us-merchant",
                        shortFactors = 1
                    }
                })).Accepted && longDefense.Execute(new GameCommand(GameCommandType.Defend, Side.UsNavy,
                longDefense.State.Revision, defensePairs: new[] { new DefensePairData
                {
                    firstUnitId = "us-burke-iia", secondUnitId = "us-merchant"
                } })).Accepted && longDefense.State.PendingMissileCombat.LongRangeHits == 1,
                "All operational long-range SAM dice must fire before local defenses.");
            var wrongRemoval = longDefense.Execute(new GameCommand(GameCommandType.Defend, Side.UsNavy,
                longDefense.State.Revision, missileReductions: Array.Empty<MissileReductionData>()));
            Require(!wrongRemoval.Accepted && wrongRemoval.Violation.Code == RuleViolationCode.InvalidDefense,
                "The defender must assign every long-range SAM hit to a surviving attacker-selected salvo.");
            Require(longDefense.Execute(new GameCommand(GameCommandType.Defend, Side.UsNavy,
                longDefense.State.Revision, missileReductions: new[]
                {
                    new MissileReductionData { salvoId = "AT-MERCHANT", factors = 1 }
                })).Accepted &&
                    longDefense.State.PendingMissileCombat.Salvos.First(item => item.Id == "AT-MERCHANT").RemainingFactors == 0 &&
                    longDefense.State.PendingMissileCombat.Salvos.First(item => item.Id == "AT-ESCORT").RemainingFactors == 1,
                "The defender must choose which incoming missile factors long-range SAM removes.");
            var layeredResult = longDefense.Execute(new GameCommand(GameCommandType.Defend, Side.UsNavy,
                longDefense.State.Revision, shortRangeDefenses: new[]
                {
                    new ShortRangeDefenseData { defendingUnitId = "us-burke-iia", salvoId = "AT-ESCORT" }
                }));
            Require(layeredResult.Accepted && layeredResult.AttackReport.InterceptedFactors == 2 &&
                    layeredResult.AttackReport.Strikes.Count == 0,
                "Short-range SAM must resolve after defender-directed long-range removals and may stop the surviving salvo.");

            var targetWithPd = new UnitState(new UnitDefinition("pd", "PD Escort", Side.Plan,
                UnitRole.Escort, 0, 0, 4, 0, 0, 0, 2, 2));
            var targetWithoutPd = new UnitState(new UnitDefinition("no-pd", "Undefended Target", Side.Plan,
                UnitRole.Objective, 0, 0, 0, 0, 0, 0, 2, 3));
            var pdFormation = new TaskForceState("PD Test", Side.Plan, new HexCoord(5, 5),
                new[] { targetWithPd, targetWithoutPd });
            var pdEngagement = new MissileEngagement("Attackers", pdFormation.Id, Side.UsNavy,
                "Attackers", ActivationPhase.PlayerAction);
            pdEngagement.SetSalvos(new[] { new MissileSalvo("PD-SELF", "source", "no-pd", 1, 0) });
            var pdReport = new MissileCombatResolver(new SequenceDieRoller(3))
                .ResolvePointDefenseAndImpacts(pdEngagement, pdFormation);
            Require(pdReport.InterceptedFactors == 0 && pdReport.HullHits == 1 &&
                    targetWithoutPd.HullDamage == 1 && targetWithPd.HullDamage == 0,
                "Point defense must protect only its own ship; surviving SSM factors use the Bombs & SSM table.");
            var pointDefenseEngagement = new MissileEngagement("Attackers", pdFormation.Id,
                Side.UsNavy, "Attackers", ActivationPhase.PlayerAction);
            pointDefenseEngagement.SetSalvos(new[] { new MissileSalvo("PD-HIT", "source", "pd", 1, 0) });
            var pointDefenseReport = new MissileCombatResolver(new SequenceDieRoller(2))
                .ResolvePointDefenseAndImpacts(pointDefenseEngagement, pdFormation);
            Require(pointDefenseReport.InterceptedFactors == 1 && pointDefenseReport.HullHits == 0,
                "A target ship's own point defense must resolve after short-range SAM and before SSM impacts.");

            var counter = new ScenarioOneGame(1, null, true, false,
                new SequenceDieRoller(1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1));
            counter.State.Enemy.MoveTo(new HexCoord(8, 13));
            Require(counter.DrawMovementChit().Accepted && counter.DeclareSpeed(Side.UsNavy, 0).Accepted &&
                    counter.Execute(new GameCommand(GameCommandType.Attack, Side.UsNavy,
                        counter.State.Revision)).Accepted,
                "Counterattack test must open a legal moving-force raid.");
            ResolveDefaultMissileExchange(counter, declineCounterattack: false);
            Require(counter.State.PendingMissileCombat == null && counter.State.ActiveSide == Side.UsNavy &&
                    counter.State.Phase == ActivationPhase.PlayerAction,
                "An accepted non-moving counterattack must resolve once, without a counter-counterattack, then restore movement.");
        }

        private static AttackReport ResolveDefaultMissileExchange(ScenarioOneGame game,
            bool declineCounterattack)
        {
            AttackReport report = null;
            while (!game.State.IsGameOver && game.State.Phase == ActivationPhase.MissileCombat)
            {
                var engagement = game.State.PendingMissileCombat;
                Require(engagement != null, "A missile-combat phase must have engagement state.");
                var attacker = game.State.Formation(engagement.AttackerFormationId);
                var defender = game.State.Formation(engagement.DefenderFormationId);
                CommandResult result;
                switch (engagement.Phase)
                {
                    case MissileCombatPhase.AllocateFire:
                    {
                        var range = attacker.Position.DistanceTo(defender.Position);
                        var target = defender.Objective != null && !defender.Objective.IsSunk
                            ? defender.Objective : defender.ActiveUnits.First();
                        var allocations = attacker.ActiveUnits.Select((unit, index) => new MissileAllocationData
                        {
                            id = $"TEST-{game.State.Revision}-{index}",
                            sourceUnitId = unit.Definition.Id,
                            targetUnitId = target.Definition.Id,
                            shortFactors = range <= 1 ? unit.AvailableShortSsm : 0,
                            longFactors = range <= 3 ? unit.AvailableLongSsm : 0
                        }).Where(item => item.shortFactors + item.longFactors > 0).ToArray();
                        result = game.Execute(new GameCommand(GameCommandType.AllocateMissileFire,
                            engagement.DecisionSide, game.State.Revision, missileAllocations: allocations));
                        break;
                    }
                    case MissileCombatPhase.DefensiveDeployment:
                    {
                        var units = defender.ActiveUnits.ToArray();
                        var pairs = units.Length >= 2 ? new[] { new DefensePairData
                        {
                            firstUnitId = units[0].Definition.Id,
                            secondUnitId = units[1].Definition.Id
                        } } : Array.Empty<DefensePairData>();
                        result = game.Execute(new GameCommand(GameCommandType.Defend,
                            engagement.DecisionSide, game.State.Revision, defensePairs: pairs));
                        break;
                    }
                    case MissileCombatPhase.LongRangeRemoval:
                    {
                        var remaining = engagement.LongRangeHits;
                        var reductions = new List<MissileReductionData>();
                        foreach (var salvo in engagement.Salvos.Where(item => item.RemainingFactors > 0))
                        {
                            var amount = Math.Min(remaining, salvo.RemainingFactors);
                            if (amount > 0) reductions.Add(new MissileReductionData
                                { salvoId = salvo.Id, factors = amount });
                            remaining -= amount;
                            if (remaining == 0) break;
                        }
                        result = game.Execute(new GameCommand(GameCommandType.Defend,
                            engagement.DecisionSide, game.State.Revision, missileReductions: reductions));
                        break;
                    }
                    case MissileCombatPhase.ShortRangeDefense:
                    {
                        var assignments = new List<ShortRangeDefenseData>();
                        foreach (var ship in defender.ActiveUnits.Where(unit => unit.EffectiveShortSam > 0))
                        {
                            var mate = engagement.PairMate(ship.Definition.Id);
                            var salvo = engagement.Salvos.FirstOrDefault(item => item.RemainingFactors > 0 &&
                                (item.TargetUnitId == ship.Definition.Id || item.TargetUnitId == mate));
                            if (salvo != null) assignments.Add(new ShortRangeDefenseData
                                { defendingUnitId = ship.Definition.Id, salvoId = salvo.Id });
                        }
                        result = game.Execute(new GameCommand(GameCommandType.Defend,
                            engagement.DecisionSide, game.State.Revision,
                            shortRangeDefenses: assignments));
                        report = result.AttackReport ?? report;
                        break;
                    }
                    default:
                        result = game.Execute(new GameCommand(GameCommandType.Counterattack,
                            engagement.DecisionSide, game.State.Revision,
                            enabled: !declineCounterattack));
                        if (!declineCounterattack) declineCounterattack = true;
                        break;
                }
                Require(result.Accepted, "Default missile exchange command failed: " + result.Summary);
            }
            return report;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
