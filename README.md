# Harpoon: First Island Chain

A Unity 6 operational naval wargame based on the **Harpoon Captain's Edition** rules and the supplied **First Island Chain** fan supplement.

Rules implementation progress is tracked in [`TODO.md`](TODO.md).

## Current playable slice

Scenario 1, **Contact off the Bashi Channel**, is playable as the US Navy against a simple PLAN opponent. It includes:

- a 15 × 20 three-dimensional hex map at 60 nautical miles per hex;
- Captain's Edition task-force speed and random movement-chit initiative;
- a complete surface-detection subsystem covering SSR, ESM, visual search, hidden formation contents, and contact loss;
- short- and long-range surface-to-surface missiles;
- layered long-range SAM, short-range SAM, and point-defense resolution;
- naval gunfire after forces close to the same hex;
- hull damage, mission-kill thresholds, sinking, and objective-ship victory scoring;
- deterministic rules code with Edit Mode tests.

Formation cards can be inspected from the right-side panel by selecting the US Navy or PLAN tab, or by clicking either 3D formation on the map.

Scenario 1 officially omits detection. To exercise the general Section 5 rules on the Scenario 1 board, press **F3** and enable **Detection Test Mode**. This solo-only mode requires an SSR silent/radiating declaration before speed and prohibits attacks until the target has been detected.

Run `play.cmd` from the project root to build and launch the Windows game in one step. Alternatively, open the project in Unity `6000.2.12f1`, open `Assets/Main.unity`, and press Play. Click a highlighted sea hex to move, then attack or end the activation. Use WASD/arrow keys to pan, the mouse wheel to zoom, and right-drag or Q/E to orbit the camera. Press Escape or use **Exit Game** to quit.

## Architecture

`Assets/Harpoon/Core` is independent of Unity and is the authoritative simulation layer. Rendering and input consume its state without changing rules outcomes, supporting later hidden information, replay, saves, stronger AI, and multiplayer.

The supplied PDFs remain in `rules/` as design references. Their original text and art are not copied into the build.

## Multiplayer

Scenario 1 includes host-authoritative one-versus-one play through either direct IP
or encrypted Unity Relay, with join codes, public discovery, chat, and soundboard
cues. See [`docs/MULTIPLAYER.md`](docs/MULTIPLAYER.md) for setup and testing.
