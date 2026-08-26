# Harpoon: First Island Chain

A Unity 6 operational naval wargame based on the **Harpoon Captain's Edition** rules and the supplied **First Island Chain** fan supplement.

Rules implementation progress is tracked in [`TODO.md`](TODO.md).

## Current playable slice

Scenario 1, **Contact off the Bashi Channel**, is playable as the US Navy against a simple PLAN opponent. It includes:

- a 15 × 20 three-dimensional hex map at 60 nautical miles per hex;
- Captain's Edition task-force speed and random movement-chit initiative;
- a complete surface-detection subsystem covering SSR, ESM, visual search, hidden formation contents, and contact loss;
- player-allocated short- and long-range surface-to-surface missile salvos with limited ammunition and split targeting;
- staged defensive pairing, defender-directed long-range SAM, pair-limited short-range SAM, self-only point defense, and counterattacks;
- staged same-hex naval gunfire with speed-based engage/evade, firing and screened ships, explicit rollback targeting, strongest-battery order, and multi-round break-off decisions;
- exact hull-1-through-6 damage thresholds, mission kills, carrier launch restrictions, sinking, and objective-ship victory scoring across the supplement's modern platform database;
- deterministic rules code with Edit Mode tests.

Formation cards can be inspected from the right-side panel by selecting the US Navy or PLAN tab, or by clicking either 3D formation on the map.

Close action has its own orange combat ribbon and command panel. Gun attacks use distinct muzzle flashes, arcing tracers, impact smoke, and a heavier procedural report than missile launches. The full Section 7 implementation is documented in [`docs/rules/GUN_COMBAT.md`](docs/rules/GUN_COMBAT.md).

Damage is shown using effective—not merely printed—card values, with `printed→effective` reductions, amber/red card states, pulsing damage rings, smoke, persistent wreck markers, and a distinct sinking effect. See [`docs/rules/SHIP_DAMAGE.md`](docs/rules/SHIP_DAMAGE.md).

Scenario 1 officially omits detection. To exercise the general Section 5 rules on the Scenario 1 board, press **F3** and enable **Detection Test Mode**. This solo-only mode requires an SSR silent/radiating declaration before speed and prohibits attacks until the target has been detected.

Run `play.cmd` from the project root to build and launch the Windows game in one step. Alternatively, open the project in Unity `6000.2.12f1`, open `Assets/Main.unity`, and press Play. Click a highlighted sea hex to move, then attack or end the activation. Use WASD/arrow keys to pan, the mouse wheel to zoom, and right-drag or Q/E to orbit the camera. Press Escape or use **Exit Game** to quit.

## Architecture

`Assets/Harpoon/Core` is independent of Unity and is the authoritative simulation layer. Rendering and input consume its state without changing rules outcomes, supporting later hidden information, replay, saves, stronger AI, and multiplayer.

The supplied PDFs remain in `rules/` as design references. Their original text and art are not copied into the build.

## Multiplayer

Scenario 1 includes host-authoritative one-versus-one play through either direct IP
or encrypted Unity Relay, with join codes, public discovery, chat, and soundboard
cues. See [`docs/MULTIPLAYER.md`](docs/MULTIPLAYER.md) for setup and testing.
