# MVP 0.1 release notes

Released 2026-08-26 as `v0.1.0`.

## Scope

MVP 0.1 makes First Island Chain Scenario 1, **Contact off the Bashi Channel**, playable from its printed setup through an authoritative result. It is intentionally not an implementation of the supplement's later submarine, aircraft, base, dummy, or campaign rules.

The scenario uses the modern platform cards in the supplied First Island Chain supplement. Scenario definitions reference the shared modern platform database; they do not duplicate or silently substitute platform values.

## Player-facing release features

- Solo play against a rules-constrained PLAN opponent, local hot-seat play, direct-IP multiplayer, and public Relay sessions.
- Explicit movement-chit, speed, radar-test, missile allocation, defensive pairing, SAM allocation, counterattack, gun formation, targeting, and break-off decisions.
- Cyan legal movement steps and previews, pulsing red legal-target rings, green active-formation rings, and gold selection rings.
- Formation cards showing detection/radar state, speed, movement, defensive pairs, ammunition, effective capabilities, hull thresholds, and damage.
- Operational briefing and concise Scenario 1 rules reference available with `F1`.
- Pause, deterministic save/load, restart confirmation, exit confirmation, and scored game-over flow.
- Selectable/random seeds plus copyable and exportable snapshots, commands, events, and complete rule transactions.

## Recorded rule interpretations

1. **Source precedence.** Explicit scenario text takes precedence, then the full Captain's Rules, then the supplement's compressed summary, then a documented project ruling.
2. **Scenario detection.** Captain's Rules introduces the first learning scenarios before the detection section. Scenario 1 therefore publishes both formations and does not require detection. The clearly labeled Detection Test Mode is a non-scenario rules harness.
3. **SAM/SSM notation.** The two printed values are short- and long-range factors. Every SSM factor is expendable once; a player may retain unfired factors.
4. **Half damage.** The full rule is used: lose long-range SSM/SAM and air-search radar, reduce speed by one, and prohibit carrier launch. The supplement's compressed statement that all SSM is lost is not used.
5. **Two-thirds damage.** Speed becomes one; weapons are lost except half guns rounded up; sonar and ESM are lost while surface-search radar remains.
6. **Gun combat.** The full initial engage/evade and subsequent break-off procedures are used instead of treating the supplement's compression as a replacement procedure.
7. **Victory ties.** The modern Scenario 1 text compares damage to the opposing merchant/amphibious ship but omits a tie instruction. The analogous original Scenario 1 surface-combatant damage comparison is used as a tie-break; equality after both comparisons is a draw.
8. **Stopping.** Neither source prints a Scenario 1 turn limit. There is no prototype seven-turn cap. A match ends on an objective sinking, force destruction, a mathematically fixed result, concession, disengagement, or mutual agreement to score.
9. **Map topology.** The shared axial coordinate system honors the source's statement that `0713` and `1010` are three hexes apart and is used by range, movement, pathfinding, AI, and rendering.
10. **Save fidelity.** Loading replays the accepted command log from its seed before play resumes. This preserves the deterministic die stream; a raw visual snapshot alone would not.

## Validation and release commands

- `test.cmd` — compiles and runs the core validation suite without Unity Test Framework or a headless-test entitlement.
- `smoke.cmd` — builds the Windows development player, launches it headlessly, and rejects runtime exception signatures.
- `release-check.cmd` — runs both release gates in order and returns a nonzero exit code on failure.
- `play.cmd` — builds and launches the normal Windows player.

The standalone scripted suite covers movement activation, missile defense with both counterattack choices, long-range-removal and local-defense decisions, close-action objective sinking, close-action break-off, US/PLAN/draw scoring, analogous escort tie-break, mutual scoring, concession, disengagement, command replay, and snapshot restoration. Unity's extended validator additionally covers illegal commands, every combat-table row, detection-test branches, damage thresholds, multi-round gun continuation, private views, networking snapshots, and TCP loopback.

## Deliberate non-authoritative/debug features

- **Detection Test Mode** is labeled as a test harness because printed Scenario 1 omits detection.
- **Rules Transaction Trace** is a debugging/audit view and does not change simulation state.
- Procedural low-poly ships, sounds, effects, and terrain are presentation abstractions; formation cards and core state are authoritative.
- No seven-turn or other invented Scenario 1 duration remains.
