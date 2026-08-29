# Harpoon Captain's Edition: First Island Chain

A community Unity 6 implementation of **Harpoon Captain's Edition**, currently featuring the supplied **First Island Chain** fan supplement.

The MVP 1.0 beta scope is recorded in [`docs/RELEASE_NOTES_MVP_1.0.md`](docs/RELEASE_NOTES_MVP_1.0.md), with tester setup and reporting instructions in [`docs/BETA_PLAYTEST.md`](docs/BETA_PLAYTEST.md). Future implementation progress is tracked in [`TODO.md`](TODO.md).

## Current playable slice

Scenarios 1–10 are playable from the **Match & System** scenario selector. The first three teach surface combat; Scenarios 4–8 add detection, deception, undersea warfare, convoy arrival, and carrier escape; Scenario 9 adds patrol aircraft; and Scenario 10, **First Light**, adds tactical flights, CAP/DLI, escorts, air combat, bombing, runway damage, and Ford's arrival objective. They include:

- a 15 × 20 three-dimensional hex map at 60 nautical miles per hex;
- Captain's Edition task-force speed and random movement-chit initiative;
- a complete surface-detection subsystem covering SSR, ESM, visual search, hidden formation contents, and contact loss;
- player-allocated short- and long-range surface-to-surface missile salvos with limited ammunition and split targeting;
- staged defensive pairing, defender-directed long-range SAM, pair-limited short-range SAM, self-only point defense, and counterattacks;
- staged same-hex naval gunfire with speed-based engage/evade, firing and screened ships, explicit rollback targeting, strongest-battery order, and multi-round break-off decisions;
- exact hull-1-through-6 damage thresholds, mission kills, carrier launch restrictions, sinking, and objective-ship victory scoring across the supplement's modern platform database;
- data-driven scenario setup and scoring, hot-seat play, selectable deterministic seeds, scenario-specific saves, and exportable command/event traces;
- deterministic rules code with Edit Mode tests.

Formation cards can be inspected from the right-side panel by selecting the US Navy or PLAN tab, or by clicking either 3D formation on the map.

Close action has its own orange combat ribbon and command panel. Gun attacks use distinct muzzle flashes, arcing tracers, impact smoke, and a heavier procedural report than missile launches. The full Section 7 implementation is documented in [`docs/rules/GUN_COMBAT.md`](docs/rules/GUN_COMBAT.md).

Damage is shown using effective—not merely printed—card values, with `printed→effective` reductions, amber/red card states, pulsing damage rings, smoke, persistent wreck markers, and a distinct sinking effect. See [`docs/rules/SHIP_DAMAGE.md`](docs/rules/SHIP_DAMAGE.md).

Scenarios 1–3 precede the detection learning scenario and omit detection. To exercise Section 5 early on those boards, press **F3** and enable **Detection Test Mode**. Scenarios 4–10 enable detection authoritatively and seal the complete debug trace until game end. Scenario 10's collapsible **Air Operations** panel handles CAP/DLI declarations and strike packages; see [`docs/rules/SCENARIO_10_SPEC.md`](docs/rules/SCENARIO_10_SPEC.md).

Run `play.cmd` from the project root to build and launch the Windows game in one step. Alternatively, open the project in Unity `6000.2.12f1`, open `Assets/Main.unity`, and press Play. Click a highlighted sea hex to move, then attack or end the activation. Use WASD/arrow keys to pan, the mouse wheel to zoom, and right-drag or Q/E to orbit the camera. Press Escape or use **Exit Game** to quit.

Use **Hot-seat 1 vs 1** for two players on one computer. The command panel shows which side is active. The scenario panel can restart with a typed or random seed, request mutual scoring, or disengage and score. **Debug Trace** can copy or export the complete seed, snapshot, command log, event history, and rules transactions.

Press **F1** for the selected scenario's operational briefing and quick reference. Press **P** or Escape to pause; the pause menu provides deterministic save/load and confirmed restart/exit controls.

Press **F2** anywhere to enable the self-voicing accessible command mode for blind and low-vision
players. It provides keyboard action navigation, spoken state/location/legal-action reports,
hex-direction movement, high-contrast and scalable text, and opt-in push-to-talk Windows voice commands.
See [`docs/ACCESSIBILITY.md`](docs/ACCESSIBILITY.md) for controls, privacy behavior, and testing guidance.

Release validation is also one command: `release-check.cmd`. Use `test.cmd` for the standalone .NET core suite or `smoke.cmd` for only the Windows build/player smoke gate. Each command returns a nonzero exit code when its gate fails.

Windows builds check published GitHub Releases for verified updates. Release tags drive the tested build/publish workflow, while installation remains an explicit player choice under **Match & System**. See [`docs/AUTO_UPDATES.md`](docs/AUTO_UPDATES.md) for the release contract, one-time Unity license setup, security checks, and recovery path.

Authored model provenance is recorded in [`THIRD_PARTY_ASSETS.md`](THIRD_PARTY_ASSETS.md).
Platform art currently remains procedurally generated; the separately supplied P-8A STL is
retained but suppressed.

To create a fully certified beta ZIP locally, run `beta-package.cmd`. The distributable and its SHA-256 checksum are written to `Artifacts/`.

## Architecture

`Assets/Harpoon/Core` is independent of Unity and is the authoritative simulation layer. Rendering and input consume its state without changing rules outcomes, supporting later hidden information, replay, saves, stronger AI, and multiplayer.

The supplied PDFs remain in `rules/` as design references. Their original text and art are not copied into the build.

## Multiplayer

Scenarios 1–10 include host-authoritative one-versus-one play through either direct IP
or encrypted Unity Relay, with join codes, public discovery, chat, and soundboard
cues. Scenarios 4–10 redact undetected opposing formations from network clients; Scenario 5 additionally protects dummy-card counts, while Scenarios 6–10 keep located-but-unclassified submarine cards private. Scenario 10 snapshots synchronize tactical-flight missions, aircraft losses, radar declarations, and runway damage. See [`docs/MULTIPLAYER.md`](docs/MULTIPLAYER.md) for setup and testing.
