# Surface detection

Section 5 implements the surface-detection procedure from Captain's Rules page 8 and the compressed supplement page 4. Scenario 1 continues to use its printed learning-game exemption; the same board can run the general subsystem in solo **Detection Test Mode** from the F3 debug panel.

## Contact state

Each side keeps its own contact record for every opposing formation. Friendly task forces share successful detections immediately.

- `Undetected`: the openly moved task-force counter is visible, but whether it represents real surface ships and all ship details remain unknown; attacks are prohibited.
- `Located`: a sensor has supplied a last-known position without classified contents. This is available for later sensor types that locate without classification.
- `Classified`: position and formation contents are known; the formation may be attacked if otherwise legal.
- `LostContact`: the last-known position is retained, current position and contents are hidden, and attacks are prohibited again.

The surface rules do not prescribe routine contact decay after SSR, ESM, or visual detection, so those contacts are retained. Captain's Rules page 9 explicitly describes losing an existing contact through a failed sonar check; the lost-contact transition is implemented for that rule and future sonar work rather than inventing a surface-contact timer.

## Sensors

### Surface-search radar

At the beginning of its activation, a task force declares its SSR radiating or silent before declaring speed. An operational radiating task force automatically detects and classifies enemy surface ships in the same hex. The radar remains in the declared state until that formation's next activation. A ship at two-thirds damage retains SSR as printed.

### ESM

When movement creates adjacency with a radiating enemy, an opposing task force with operational ESM makes the passive check. A D6 result of 1–5 detects and classifies the radiating formation; 6 produces no contact. ESM is unavailable when every suitable ship in the observing formation has lost the capability at two-thirds damage.

### Visual search

A radar-silent task force sharing an enemy hex may search visually during AM or PM. A D6 result of 1–2 detects and classifies; 3–6 fails. The first attempt follows entering or remaining in the hex. After failure, the moving formation may spend one remaining movement point per additional attempt. Visual search is prohibited at Night. A nonmoving radar-silent force receives the same visual opportunity when enemy movement enters its hex.

Entering an enemy base hex automatically establishes visual contact even at Night, matching the full rule. Normal map-entry restrictions may make that trigger scenario-dependent.

## Information and presentation

Side-private projections expose friendly formations completely and classified enemy contacts completely. Undetected/lost counters retain their openly visible board position but expose no ship contents. In Detection Test Mode the runtime replaces undetected status summaries with an unknown-contact label and hides formation cards until classification. Cyan pulsing rings indicate radiating radar; amber rings mark known enemy contacts. Sensor declarations, rolls, failures, detections, and rejected attacks are recorded in the rules-debug trace.

Detection Test Mode is intentionally solo-only until a scenario that uses hidden contacts supplies a redacted authoritative multiplayer snapshot schema. Scenario 1 multiplayer remains fully public because its rules explicitly omit detection.
