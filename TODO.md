# Harpoon: First Island Chain — Rules Implementation TODO

This is the living implementation checklist. Check an item only when its rules behavior is implemented in `Harpoon.Core`, exposed to players where a decision is required, and covered by deterministic automated tests.

## Release targets

- **MVP 0.1 — Scenario 1 rules-complete:** *Contact off the Bashi Channel* works faithfully from setup through victory, including every core rule it invokes.
- **MVP 1.0 — Introductory game complete:** all ten First Island Chain introductory scenarios are playable and tested.
- **Post-MVP — Advanced game complete:** mission purchase, hidden missions, bases, capture, resupply, and the 21-turn campaign system are playable and tested.

## Already in place

- [x] Unity project targets the installed Unity `6000.2.12f1` editor.
- [x] One-command Windows build and launch via `play.cmd`.
- [x] Deterministic injectable D6 roller in the Unity-independent rules assembly.
- [x] Basic hex-coordinate and distance type.
- [x] Basic unit, task-force, hull-damage, and turn state types.
- [x] Scenario 1 force names, printed combat values, and starting hexes entered.
- [x] First-pass 3D map, low-poly formations, camera, combat effects, and command panel.
- [x] Exit button and Escape-key exit.
- [x] Clickable formation-card panel with Scenario 1 sensors, weapons, speed, hull, and missile state.
- [x] Toggleable rules-debug trace with ordered transactions, individual die results, rejected actions, and copy-to-clipboard export.
- [x] Host-authoritative one-versus-one TCP foundation with IP join, host side selection, synchronized Scenario 1 commands, chat, and spoken soundboard cues.
- [x] Public Unity Relay foundation with DTLS, anonymous authentication, join codes, session discovery/passwords, reconnect, service notices, and opponent mute controls.
- [x] Unity compiles the core, runtime, editor, and test assemblies.
- [x] Batch-safe rules, replay, side-view, and TCP loopback validations run successfully.

> **Current rules status:** Scenario 1 is playable but not yet rules-complete. Several current behaviors are deliberate prototype shortcuts and must not be treated as authoritative.

---

# MVP 0.1 — Scenario 1 rules-complete

## 1. Establish an authoritative rules specification

- [x] Create a rule-to-code coverage matrix with source document, page, rule, implementation class, and test names.
- [x] Transcribe the relevant Captain's Edition tables into human-readable test fixtures.
- [x] Verify every Scenario 1 unit value against the supplement cards:
  - [x] Arleigh Burke Flight IIA
  - [x] Merchant Ship
  - [x] Type 054A
  - [x] Type 071
- [x] Resolve the exact meaning and ammunition bookkeeping of the two-part `SAM` and `SSM` card values from the full rules and roster sheets.
- [x] Confirm whether Scenario 1 begins with both formations detected or uses normal detection rules.
- [x] Confirm the intended Scenario 1 stopping condition; remove the invented seven-turn prototype cap.
- [x] Document any unavoidable interpretation where the source material is ambiguous.

## 2. Make the core engine command/event driven

- [x] Replace scenario-specific control flow with a general `GameState`, `IRulesEngine`, and legal-command boundary.
- [x] Represent player actions as versioned commands, including declare speed, move, radiate radar, search, allocate fire, attack, defend, counterattack, break off, end activation, and concede. Commands outside Scenario 1 reject explicitly until their rules sections are implemented.
- [x] Emit immutable, ordered rule events for setup, commands, movement, detection, dice, ammunition, combat/interception, damage, and victory.
- [x] Make illegal commands return structured `RuleViolationCode` results rather than UI strings.
- [x] Keep the simulation in the Unity-independent `Harpoon.Core` assembly with `noEngineReferences` enabled.
- [x] Support deterministic replay from initial state, seed, and accepted command log.
- [x] Separate public state from side-private views in preparation for hidden contacts and dummies; Scenario 1 deliberately publishes both formations.

## 3. Board and movement

- [x] Verify the supplement map's 15×20 axial coordinate topology against the printed map and Scenario 1's `1010`→`0713` three-hex setup.
- [x] Store map bounds, land, sea, all six bases, and an explicit restricted-terrain layer in the core rather than the renderer.
- [x] Use one shared `HexCoord` adjacency/distance/layout implementation for movement, range, AI, path previews, and rendering.
- [x] Reject movement through land or off-map hexes in the core.
- [x] Move task forces one adjacent hex per command instead of teleporting directly to a destination.
- [x] Open attack, search, and reaction opportunities in every entered hex. Scenario 1 search is recorded but bypasses detection; detailed counterattack resolution remains in section 6.
- [x] Require speed declaration before movement or other activation completion.
- [x] Enforce maximum declared speed and slowest-active-ship task-force speed.
- [x] Preserve the rule that an otherwise eligible task force can always move one hex.
- [x] Apply half-hull and two-thirds-loss speed penalties at the correct thresholds.
- [x] Allow enemy formations to coexist in the same hex and open the combat/reaction window without displacement.
- [x] Add deterministic movement-path tests covering coastlines, adjacency, land, map edges, declared movement, occupied hexes, and damaged ships.

## 4. Movement-chit sequence of play

- [x] Represent one movement chit per task force or patrol-aircraft unit.
- [x] Randomize draws without replacement from the complete cup.
- [x] Activate exactly the unit named by the drawn chit.
- [x] Allow pre-draw task-force splitting at the proper time.
- [x] End the turn only when the cup is empty.
- [x] Return all eligible chits for the next turn.
- [x] Implement AM, PM, and Night periods with three turns per day.
- [x] Apply the Night restriction to visual searches.
- [x] Replace the current 50/50 two-side initiative shortcut.
- [x] Test draw order, no duplicate activation, turn rollover, and seeded reproducibility.

## 5. Surface detection (deferred beyond Scenario 1)

> Scenario 1 explicitly omits detection. This section is foundational follow-on work, but it is not an MVP 0.1 release gate for this scenario.

- [x] Represent undetected, located, classified, and lost-contact states as required by the rules.
- [x] Prevent attacks against undetected targets.
- [x] Allow task forces to radiate or silence surface-search radar.
- [x] Implement automatic same-hex/range-one surface-search radar detection exactly as printed.
- [x] Implement passive ESM detection of radiating enemies on the correct trigger and `1–5` roll.
- [x] Implement daytime visual search on `1–2` while entering/remaining in an enemy hex.
- [x] Permit repeated visual attempts for remaining movement points where allowed.
- [x] Lose or retain contacts according to the full rules.
- [x] Show only information the US player is entitled to know.
- [x] Give the PLAN AI the same detection restrictions as the human player.
- [x] Test radar silent/radiating, ESM success/failure, visual success/failure, Night, and attack gating.

## 6. Surface-to-surface missile combat

- [x] Model short-range SSM range as one hex and long-range SSM range as three hexes.
- [x] Let the attacker choose how many available missile factors to fire.
- [x] Let the attacker split factors among legal targets.
- [x] Track short- and long-range ammunition using the roster-sheet rules.
- [x] Prevent firing more factors than remain.
- [x] Do not automatically expend every in-range missile factor.
- [x] Resolve long-range SAM first.
- [x] Let the defender choose which attacking factors are removed by long-range SAM hits where relevant.
- [x] Resolve short-range SAM second with self/pair defense restrictions.
- [x] Resolve point defense last and only for the ship itself.
- [x] Resolve surviving missile factors on the Bombs & SSM combat-table column.
- [x] Implement `M`, `H`, and `2H` results exactly for every D6 result.
- [x] Implement target selection and ship defensive pairing.
- [x] Implement rollback/split-fire decisions across a pair.
- [x] Permit the non-moving force's counterattack at the correct point.
- [x] Replace the current automatic objective-ship targeting.
- [x] Test each combat-table row, each defense layer, mixed defenses, exhausted ammunition, pairing, rollback, and counterattack timing.

## 7. Naval gunfire

- [x] Allow gunfire only in the same hex.
- [x] Determine which force may engage or evade using relative effective speed.
- [x] Implement mutual agreement to break off.
- [x] Implement the faster force's automatic break-off choice.
- [x] Implement equal-speed contested continuation (`1–3` for attacker where applicable).
- [x] Form legal firing/target pairs.
- [x] Permit only the eligible firing ship from each pair, strongest gun factor first.
- [x] Apply the `−1` die-roll modifier when firing at a screened ship.
- [x] Resolve gun dice on the Guns combat-table column.
- [x] Implement rollback during gun combat.
- [x] Replace the current behavior that simply sums every active gun factor.
- [x] Test engage/evade, pairing, screened modifiers, firing order, rollback, and break-off.

## 8. Ship damage and capability loss

- [x] Mark one hull box per hit and sink at zero remaining boxes.
- [x] At one-half hull remaining, apply exactly:
  - [x] Speed reduced by one.
  - [x] Long-range SSM and long-range SAM capability lost; short-range SSM remains available.
  - [x] Air-search radar lost.
  - [x] Carrier aircraft launch prohibited.
- [x] At two-thirds hull lost, apply exactly:
  - [x] Speed becomes one.
  - [x] All weapons except half guns, rounded up, are lost.
  - [x] Sonar and ESM are lost.
  - [x] Surface-search radar remains.
- [x] Define threshold rounding explicitly for hull values 1 through 6.
- [x] Remove sunk ships from movement speed, firing, defense, sensors, and scoring at the correct time.
- [x] Preserve damage consistently through counterattacks and multi-step combat.
- [x] Test every threshold for every possible hull rating used by the supplement.

## 9. Scenario 1 setup, AI, and victory

- [ ] Load Scenario 1 from data instead of hard-coded constructors.
- [ ] Set the exact US and PLAN formations and starting hexes.
- [ ] Implement the printed objective: compare damage to the Merchant Ship and Type 071.
- [ ] Implement the authoritative stopping condition determined during the rules audit.
- [ ] Handle ties exactly as the rules specify.
- [ ] Ensure escort damage does not incorrectly count toward the objective score.
- [ ] Make the AI choose only legal movement, detection, targeting, firing, defense, counterattack, and break-off actions.
- [ ] Add a hot-seat/manual-opponent mode for testing every decision branch without relying on AI.
- [ ] Add scenario restart with a selectable/random seed.
- [ ] Display the seed and exportable command/event log.
- [ ] Add end-to-end deterministic tests for US win, PLAN win, tie, sunk objective, ammunition exhaustion, and disengagement.

## 10. MVP 0.1 usability and release gate

- [ ] Present required decisions instead of making hidden automatic choices for the player.
- [ ] Clearly highlight legal movement paths and legal targets.
- [ ] Show detection/radar state, declared speed, ammunition, defenses, damage effects, and victory progress.
- [ ] Provide a concise in-game Scenario 1 briefing and rules reference.
- [ ] Add pause, restart confirmation, exit, and game-over flow.
- [ ] Add save/load only if a Scenario 1 match proves too long for one sitting.
- [ ] Establish a test runner independent of Unity's currently unavailable headless-test entitlement.
- [ ] Run core tests from one command and fail with a nonzero exit code.
- [ ] Run a Windows-player build smoke test from one command.
- [ ] Run at least one scripted full match for every supported decision route.
- [ ] Record all remaining rule interpretations in release notes.
- [ ] Remove or label every prototype-only rule, especially the seven-turn cap.
- [ ] Tag MVP 0.1 only when every Scenario 1-applicable item in sections 1–10 is complete; section 5 is explicitly deferred by the scenario rules.

---

# MVP 1.0 — All ten introductory scenarios

Each scenario is both playable content and the acceptance test for a new rules family.

## Scenario 2 — Flagship Duel: larger surface formations

- [ ] Support more than one escort/screening pair per task force.
- [ ] Implement player-controlled ship pairing and re-pairing at legal times.
- [ ] Implement multi-target missile allocation and rollback at formation scale.
- [ ] Enter and verify the Scenario 2 order of battle, setup, and victory conditions.
- [ ] Add deterministic Scenario 2 acceptance tests.

## Scenario 3 — Close Aboard: complete gun combat

- [ ] Complete every naval-gunfire and break-off rule from section 7.
- [ ] Enter and verify the Scenario 3 order of battle, setup, and victory conditions.
- [ ] Score only hull hits caused by gunfire where required.
- [ ] Add deterministic Scenario 3 acceptance tests.

## Scenario 4 — Picket Line: detection and convoy movement

- [ ] Complete radar, ESM, visual search, contact loss, and hidden task-force presentation.
- [ ] Support destination/base arrival objectives.
- [ ] Support convoy protection and multiple merchant ships.
- [ ] Enter and verify the Scenario 4 setup and reconcile its printed victory wording with its listed forces.
- [ ] Add deterministic Scenario 4 acceptance tests.

## Scenario 5 — Ghost Fleet: dummy units

- [ ] Add three US and five PLAN dummy cards.
- [ ] Allow legal dummy distribution and transfer between task forces.
- [ ] Reveal transfers as required without leaking unrelated contents.
- [ ] Resolve radar/visual searches of dummy-only forces as “no surface ships present.”
- [ ] Remove a dummy after successful sonar detection.
- [ ] Add side-private UI and AI knowledge boundaries.
- [ ] Add deterministic Scenario 5 acceptance tests.

## Scenario 6 — Wolves of the Bashi Channel: submarines

- [ ] Add submarine-only task forces and prohibit mixing with surface ships.
- [ ] Implement submarine terrain movement exceptions for ice/reef-restricted hexes where applicable.
- [ ] Prevent direct radar and visual detection of submarines.
- [ ] Implement sonar detection and every modifier:
  - [ ] Best sonar value.
  - [ ] Multiple searching ships `+1`.
  - [ ] One-hex range `−2`.
  - [ ] Two-hex range `−3`.
  - [ ] Searching task-force speed penalty.
  - [ ] Target task-force speed bonus.
  - [ ] Previously detected target `+1`.
  - [ ] Natural six always fails.
- [ ] Implement submarine ESM detection.
- [ ] Implement torpedo target restrictions and Torpedo Attack Table resolution.
- [ ] Implement submarine-launched SSM restrictions.
- [ ] Implement ASW counterattack eligibility and ASW Attack Table resolution.
- [ ] Implement seven-turn survival/offset victory scoring.
- [ ] Add deterministic Scenario 6 acceptance tests.

## Scenario 7 — Lifeline to Taiwan: convoy and arrival scoring

- [ ] Support multiple independent task forces per side.
- [ ] Support scenario deployment zones and prohibited setup zones.
- [ ] Implement base/port entry and arrival state.
- [ ] Implement merchant survival scoring and submarine-loss offsets.
- [ ] Implement the printed ten-turn limit.
- [ ] Add deterministic Scenario 7 acceptance tests.

## Scenario 8 — Hunt the Dragon: carrier capability

- [ ] Model carriers as ships with embarked-aircraft capacity/state.
- [ ] Apply damage effects that prevent aircraft launch.
- [ ] Implement board-edge entry and exit.
- [ ] Implement patrol-line setup and movement restrictions.
- [ ] Implement “carrier exits while still capable of launching aircraft” victory.
- [ ] Add deterministic Scenario 8 acceptance tests.

## Scenario 9 — Patroller: patrol aircraft

- [ ] Add patrol-aircraft movement chits and one-model-per-type abstraction.
- [ ] Implement unlimited on-map relocation within base/carrier radius each activation.
- [ ] Implement patrol ASR, SSR, sonar, ESM, and visual searches.
- [ ] Implement once-per-turn patrol ASM and ASW attacks.
- [ ] Implement air-search-radar detection of patrol aircraft.
- [ ] Implement Aircraft Damage Table: no effect, abort, and shot down.
- [ ] Implement abort/return-to-base and serviceability state.
- [ ] Implement Kadena basing and P-8A radius.
- [ ] Implement east-edge submarine exit objective and fifteen-turn limit.
- [ ] Add deterministic Scenario 9 acceptance tests.

## Scenario 10 — First Light: tactical aircraft and bases

- [ ] Add tactical aircraft flights of one to four aircraft.
- [ ] Enforce shore/carrier basing and carrier deck capacity.
- [ ] Track ready, flown, aborted, and destroyed aircraft.
- [ ] Enforce one flight/attack per aircraft per turn and return to base.
- [ ] Enforce tactical-aircraft radius.
- [ ] Require a friendly detected target before tactical strike launch, except where overridden.
- [ ] Implement air-launched ASM with normal layered missile defenses.
- [ ] Implement bomb attacks, adjacency, and their distinct defense sequence.
- [ ] Implement fighter ATA versus target Defense and the complete Air-to-Air table.
- [ ] Implement CAP interception radius and persistence.
- [ ] Implement deck-launched interceptors.
- [ ] Implement strike escorts and engagement priority.
- [ ] Implement aircraft-vs-missile combat with missile Defense zero.
- [ ] Implement the optional EA-18G sensor-reduction rule as a scenario option.
- [ ] Implement bases, base air defenses, runway damage, and launch restrictions.
- [ ] Enter carrier air-wing and land-base aircraft inventories.
- [ ] Implement Scenario 10 movement objective and twelve-turn limit.
- [ ] Add deterministic Scenario 10 acceptance tests.

## MVP 1.0 shared data and release work

- [ ] Enter all US Navy surface ships, submarines, and aircraft from the supplement.
- [ ] Enter all PLAN surface ships, submarines, and aircraft from the supplement.
- [ ] Enter merchant, tanker, and generic amphibious auxiliary units.
- [ ] Enter all six base charts and both carrier-air-wing charts.
- [ ] Validate all stats with schema checks and source-page references.
- [ ] Add a scenario-selection menu for Scenarios 1–10.
- [ ] Add scenario-specific briefings, setup validation, turn limits, objectives, and scoring.
- [ ] Add automated data-validation tests for unique IDs, valid ranges, legal references, and complete inventories.
- [ ] Add end-to-end acceptance coverage for all ten scenarios.
- [ ] Tag MVP 1.0 only when every introductory scenario can be completed without debug controls.

---

# Post-MVP — Advanced mission system

- [ ] Implement secret mission draw for both sides.
- [ ] Implement mission-specific asset-point budgets.
- [ ] Implement standard, high-cost, and low-cost purchase formulas.
- [ ] Purchase carrier air wings separately from carriers.
- [ ] Validate force purchases against budget and availability restrictions.
- [ ] Implement all nine US mission objectives.
- [ ] Implement all nine PLAN mission objectives.
- [ ] Implement Kadena and Ningbo-Zhoushan as permanent home bases.
- [ ] Implement capturable forward bases.
- [ ] Implement runway destruction/neutralization.
- [ ] Implement amphibious landing and two-full-turn occupation objectives.
- [ ] Implement resupply ships and replenishment.
- [ ] Implement asset-point loss scoring and percentage-destruction objectives.
- [ ] Implement the 21-turn/seven-day campaign limit and concession.
- [ ] Reveal and compare missions at game end.
- [ ] Add deterministic acceptance tests for every mission pairing or a justified pairwise coverage set.

---

# Test strategy and definition of done

Every rule item is complete only when all applicable boxes below are true:

- [ ] The rule has a cited source page in the coverage matrix.
- [ ] The rule is represented in the Unity-independent core.
- [ ] Legal and illegal cases are deterministic and unit tested.
- [ ] Every D6 table result and modifier boundary is tested.
- [ ] Required human choices are exposed in the UI or hot-seat test harness.
- [ ] AI uses the same legal-command API and receives no hidden information.
- [ ] The event log explains enough to reproduce the outcome.
- [ ] Save/replay serialization, when introduced, preserves the rule state.
- [ ] The Windows player builds without compiler or shader errors.
- [ ] At least one complete scenario acceptance test passes from setup to final scoring.

## Recommended implementation order

1. Build the coverage matrix and external core test runner.
2. Correct map topology, movement paths, and movement-chit activation.
3. Implement detection and side-private state.
4. Rebuild missile combat around explicit allocation, pairing, defenses, and counterattack.
5. Complete gun combat and damage thresholds.
6. Finish and certify Scenario 1 as MVP 0.1.
7. Add rules scenario-by-scenario from Scenario 2 through Scenario 10.
8. Add the advanced mission system only after the introductory rules are stable.
