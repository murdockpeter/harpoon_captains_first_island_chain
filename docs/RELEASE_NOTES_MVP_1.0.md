# Harpoon Captain's Edition — MVP 1.0 beta release notes

MVP 1.0 makes all ten introductory scenarios from *Harpoon: First Island Chain* selectable,
playable, saveable, replayable, and capable of reaching an authoritative result without debug
controls.

## Included

- Scenario-specific setups, briefings, deployment rules, objectives, scoring, and turn limits for
  Scenarios 1–10.
- Surface missile and gun combat, detection and deception, submarines and torpedoes, convoy and
  carrier objectives, patrol aircraft, and Scenario 10 tactical air/base operations.
- All 34 hull-bearing platform cards, all 13 aircraft stat cards, the three generic auxiliary cards,
  six shore-base charts, and two carrier-wing charts from the supplement.
- Solo, hot-seat, direct-IP, and public Relay/Lobby 1-v-1 modes, with chat and a soundboard.
- Deterministic match seeds, save/restore, replay/export, debug transactions, Windows audio cues,
  a certified Windows packaging command, and opt-in SHA-256-verified GitHub updates.
- Formation cards restyled after the supplement's printed cards for quicker sensor, weapon, speed,
  and hull reading.

## Release acceptance

`beta-package.cmd` runs the standalone core suite, Unity rules validation, Windows build/player smoke,
and updater integration before creating the distribution archive. The shared data validator rejects
duplicate IDs, missing supplement references, illegal numeric ranges, unresolved scenario/platform/
aircraft/base links, incomplete chart sets, and side mismatches.

Post-MVP campaign and mission-purchase systems are intentionally excluded. See `TODO.md` for that
work and `BETA_PLAYTEST.md` for tester instructions.
