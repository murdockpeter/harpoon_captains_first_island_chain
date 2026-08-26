# Surface detection

Section 5 implements the surface-detection procedure from Captain's Rules page 8 and the compressed supplement page 4. Introductory Scenarios 1–3 precede the detection scenario and use the learning-game exemption; any current board can run the general subsystem in solo **Detection Test Mode** from the F3 debug panel.

## Contact state

Each side keeps its own contact record for every opposing formation. Friendly task forces share successful detections immediately.

- `Undetected`: current position and ship details are hidden; attacks are prohibited.
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

Side-private projections expose friendly formations completely and classified enemy contacts completely. Undetected formations expose neither current position nor contents; lost contacts retain only their last-known position. Scenario 4 additionally redacts opposing commands, paths, contacts, logs, and rules transactions from network snapshots. Cyan pulsing rings indicate radiating radar; amber rings mark known enemy contacts. The authoritative debug trace is sealed during a hidden-information match and becomes available at game end.

Scenario 4 supplies a redacted authoritative multiplayer snapshot schema, so hidden contacts are supported over direct IP and Relay. Scenarios 1–3 remain fully public because those learning scenarios omit detection.
