# Rules coverage matrix

This matrix is the authoritative bridge between the supplied source documents, the deterministic core, and automated tests. Page numbers are printed PDF page numbers.

## Source precedence

When sources disagree, use this order:

1. A First Island Chain scenario's explicit setup, forces, special rules, and victory text.
2. The original *Captain's Rules* for core mechanics. The supplement says those mechanics are unchanged and directs unclear cases back to the full booklet.
3. The supplement's compressed rules as a convenient summary.
4. A documented project interpretation when neither source supplies an answer.

Modern unit cards and the optional EA-18G rule necessarily come from the supplement. All 34 hull-bearing modern platform cards on pages 15–21 are transcribed into `ModernPlatformDatabase`; scenario definitions select from that database rather than duplicating card values.

## Scenario 1 coverage

| Rule/data | Authoritative source | Current code | Automated evidence | Status |
|---|---|---|---|---|
| Scenario name, forces, setup, objective | First Island Chain pp. 25; cards pp. 15, 18–19, 21 | `ScenarioDefinition`, `ScenarioOne.Create` | Section 9 data/setup batch audit | Complete and data-driven |
| Task-force grouping and hidden stacks | Captain's Rules p. 3 | `TaskForceState`, `ScenarioOneGame.SplitTaskForceInternal` | pre-draw split and snapshot tests | Grouping and splitting complete; hidden stacks await detection rules |
| Random movement chits and period sequence | Captain's Rules p. 3 | `MovementChitCup`, `ScenarioOneGame.DrawMovementChitInternal`, `BeginTurn` | no-replacement, named activation, split-window, turn-rollover, Night, and seeded-order tests | Complete for surface task forces; cup also accepts patrol-aircraft chits for later air rules |
| Declared speed, slowest ship, one-hex movement | Captain's Rules p. 3 | `TaskForceState`, `ScenarioOneGame.DeclareSpeedInternal`, `TryMoveInternal` | declaration, adjacency, incomplete-movement, damaged-speed tests | Complete for Scenario 1 |
| Map topology, terrain, bases, and land restriction | Captain's Rules p. 3; supplement map p. 6 | `OperationalMap`, `FirstIslandChainMap`, `HexCoord` | 15×20 bounds, axial setup, base, coastline, land, and edge tests | Complete for Scenario 1 |
| Per-entered-hex combat/search timing | Captain's Rules p. 3 | movement-opportunity events; attack/search commands remain legal between steps | per-hex action-window batch test | Complete for Scenario 1 missile and same-hex gun engagements |
| Surface detection, SSR, ESM, and visual search | Captain's Rules p. 8; supplement p. 4 | `DetectionTracker`, `DetectionResolver`, `ScenarioOneGame` sensor commands/triggers | radar declaration/automatic detection, ESM 1–5, visual 1–2/Night/repeat, attack-gating, private-view, snapshot tests | Complete; available through solo Detection Test Mode because Scenario 1 itself omits detection |
| Attack/counterattack timing and simultaneity | Captain's Rules pp. 3–4 | `ScenarioOneGame`, `MissileEngagement` | accepted/declined counterattack and interrupted-activation restoration tests | Complete for missile exchanges; gunfire follows in section 7 |
| Ship damage | Captain's Rules p. 4; modern hulls on supplement pp. 15–21 | `UnitState`, `DamageApplication`, `ModernPlatformDatabase` | hull 1–6 threshold matrix; all 34 modern cards; half/two-thirds/sunk capability, movement, sensor, carrier, snapshot tests | Complete for every modern ship/submarine/auxiliary hull |
| SSM values, range, splitting, ammunition | Captain's Rules pp. 4–5; supplement pp. 3–4 | `UnitState.TryCommitMissiles`, `MissileEngagement`, `ScenarioOneGame.AllocateMissileFireInternal` | partial/over-fire, range, split-target, exhausted-ammunition, replay tests | Complete |
| Defensive pairing and rollback pressure | Captain's Rules pp. 5–7; supplement p. 4 | staged defensive deployment and SR-SAM assignment | pairing, duplicate/illegal SR assignment, split fire across pair tests | Complete for missiles |
| Missile procedure and combat table | Captain's Rules p. 6; supplement pp. 4, 11 | `MissileCombatResolver`, staged defense commands | every table row, LR choice, SR SAM, self-only PD, mixed defense, per-target impact tests | Complete |
| Naval gunfire | Captain's Rules p. 7 | `GunEngagement`, `GunCombatResolver`, staged gun commands | engage/evade thresholds, legal firing formation, strongest-first order, explicit targets, screened modifier, Guns table, snapshot, repeated-round tests | Complete for Scenario 1 |
| Detection exemption for learning scenarios 1–2 | Captain's Rules pp. 3 and 6 | default Scenario 1 configuration bypasses detection; debug mode enables the general subsystem | exemption and attack-gating tests | Complete for Scenario 1 |
| Scenario 1 victory and tie-break | First Island Chain p. 25; analogous original scenario on Briefing p. 4 | `ScenarioOneGame.CurrentScore`, `EndByScore`, `CompareScore` | deterministic US/PLAN/draw/escort-tie-break tests | Complete; escort damage never enters the primary score |
| Scenario stopping condition | No explicit limit in either Scenario 1 text | `CheckGameOver`, `Disengage`, `RequestScoring`, `Concede`; unlimited `MaximumTurns = 0` | objective sink, fixed result, force destruction, mutual score, disengagement tests | Complete for Scenario 1 |

## Scenario 2 coverage

| Rule/data | Authoritative source | Current code | Automated evidence | Status |
|---|---|---|---|---|
| Type 055 versus two Burke IIAs, Ticonderoga, and San Antonio | First Island Chain p. 25; cards pp. 15–16, 18 | `FirstIslandChainScenarios.FlagshipDuel` and `ModernPlatformDatabase` | exact ID, platform, side, count, and printed-coordinate checks | Complete |
| Multiple defensive/screening pairs | Captain's Rules pp. 5–7 | `DefensePairData`, `TaskForceState`, staged defense and gun arrangement commands | two-pair formation deployment test | Complete |
| Formation-scale split fire and rollback | Captain's Rules pp. 4–7 | `MissileEngagement`, explicit allocations, defensive pairing, LR/SR removal assignment | two-salvo/two-target Scenario 2 route plus general table tests | Complete |
| Total warship hull-hit victory | First Island Chain p. 25 | `ScenarioScoringMode.TotalHullHits`, `CurrentScore`, `CheckGameOver` | multi-ship damage aggregation and final-score route | Complete |
| Printed setup wording conflict | First Island Chain p. 25 | exact coordinates `0510` and `1213` retained | setup validation | The phrase “three hexes southeast” conflicts with those coordinates under the map topology; explicit hex references take precedence |

## Scenario 3 coverage

| Rule/data | Authoritative source | Current code | Automated evidence | Status |
|---|---|---|---|---|
| Three Type 056As versus Burke IIA and Constellation | First Island Chain p. 25; cards pp. 15, 18 | `FirstIslandChainScenarios.CloseAboard` and `ModernPlatformDatabase` | exact ID, platform, side, count, and printed-coordinate checks | Complete |
| Full gun engagement, firing formation, rollback, and break-off | Captain's Rules p. 7 | `GunEngagement`, `GunCombatResolver`, staged gun commands | section 7 matrix plus Scenario 3 same-hex command route | Complete |
| Gunfire-only hull-hit victory | First Island Chain p. 25 | `DamageSource`, `UnitState.GunfireHullDamage`, `ScenarioScoringMode.GunfireHullHits` | missile exclusion, legal gun hit, snapshot, final scoring tests | Complete |
| Printed setup wording conflict | First Island Chain p. 25 | exact coordinates `1010` and `1313` retained | setup validation | “Three hexes southeast” conflicts with the explicit coordinates under the established axial topology; explicit hex references take precedence |

## Scenario 4 coverage

| Rule/data | Authoritative source | Current code | Automated evidence | Status |
|---|---|---|---|---|
| Five-ship Subic convoy and three-ship PLAN picket | First Island Chain p. 25; cards pp. 15, 18, 21 | `FirstIslandChainScenarios.PicketLine` | exact platforms, counts, roles, and base setup | Complete |
| PLAN free deployment outside two exclusion zones | First Island Chain p. 25 | `DeployFormation`, seeded solo setup, map placement UI | legal/illegal boundary tests | Complete |
| Mandatory SSR, ESM, visual search, and attack gating | Captain's Rules pp. 8–9 | detection command/resolver pipeline | Section 5 matrix and Scenario 4 projection tests | Complete |
| Side-private hidden task force | Captain's Rules pp. 8–9 | `SideGameView`, redacted side snapshots, sealed trace/UI | undetected versus classified snapshot tests | Complete |
| Convoy destination and multiple merchants | First Island Chain p. 25 | arrival state, `ConvoyArrival` scoring, Taipei destination | deterministic arrival route | Complete |
| Victory wording conflict | First Island Chain p. 25 | arrival-or-destroy versus both-merchants-sunk ruling | scoring route and specification | Resolved and documented |

## Rules-family source index

| Rules family | Captain's Rules | Supplement |
|---|---:|---:|
| Components and card anatomy | p. 2 | pp. 13–24 |
| Surface task forces, movement, split/combine | p. 3 | p. 3 |
| Damage | p. 4 | p. 3 |
| Surface missiles and ammunition | pp. 4–6 | pp. 3–4, 11 |
| Naval gunfire and rollback | p. 7 | p. 4, 11 |
| Radar, ESM, visual, sonar | pp. 8–9 | p. 4, 11 |
| Dummy units | p. 9 | pp. 4, 22 |
| Submarines | pp. 10–11 | p. 4 |
| Patrol aircraft | pp. 12–13 | p. 4 |
| Tactical aircraft and air combat | pp. 14–16 | p. 5, 11 |
| Introductory scenarios | Captain's Briefing pp. 4–6 | pp. 25–26 |
| Advanced mission system | Captain's Briefing pp. 7–9 | pp. 27–28 |

## Known source conflicts

| Topic | Original rule | Compressed supplement | Project ruling |
|---|---|---|---|
| Half-damage weapons | Lose long-range SSM/SAM and ASR; carrier cannot launch (Captain's Rules p. 4) | Says lose all SSM and ASR (supplement p. 3) | Use the original full rule |
| Gun engagement/break-off | Separate initial engagement and later break-off rolls with different thresholds (Captain's Rules p. 7) | Compresses these into a shorter summary (supplement p. 4) | Use the original full procedure |
| Scenario 1 tie | Analogous original scenario uses surface-combatant damage as tie-break (Briefing p. 4) | Modern scenario omits tie text (supplement p. 25) | Use the original tie-break, then draw if still equal |
