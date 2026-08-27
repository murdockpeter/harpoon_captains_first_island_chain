# Scenario 10 authoritative specification

## First Light Over the East China Sea

Source: *Harpoon: First Island Chain* p. 26, the base and carrier-air-wing charts on pp. 13–14 and 23–24, modern aircraft cards on pp. 17 and 20, and tactical-aircraft rules on pp. 14–16 of *Captain's Rules*.

### Forces, setup, and objective

- The US Ford Strike Group starts at Kadena (`0904`): Gerald R. Ford, one Ticonderoga, one Arleigh Burke Flight III, and two Constellation-class frigates.
- Its tactical carrier wing contains six F/A-18E/F flights, four F-35C flights, and two EA-18G flights. The E-2D and MQ-4C inventory entries remain on the wing chart but are not tactical strike flights.
- PLAN operates two independent Type 093B SSN task forces within ten hexes of Ningbo and three Ningbo tactical flights: two J-16 and one H-6J. Ningbo's KJ-500 inventory entry remains on its base chart.
- The US wins immediately when Gerald R. Ford is within two hexes of `0206` and remains capable of launching aircraft. A sunk or half-damaged launch-prohibited Ford cannot satisfy the objective. If this has not occurred by the end of Turn 12, PLAN wins.

### Tactical flights and basing

A tactical flight contains one through four aircraft. Every aircraft begins a turn ready and can fly once that turn. A strike removes participating aircraft from the ready pool and records them as flown; aborts and shot-down aircraft are tracked separately. Survivors return to their assigned base or carrier and become ready at the next turn boundary.

Shore bases have unlimited parking capacity. Carrier wings enforce printed flight capacity and carrier-capable types. A carrier at half damage cannot launch. Runway hits degrade a shore base at half capacity and close it at full capacity. The supplied rules do not assign intermediate runway thresholds, so this project interprets the six-box track as: 0–2 hits normal (four aircraft per strike), 3–5 degraded (two), and 6 closed.

Flights attack from their base or carrier position. The route to the launch point must fit inside printed aircraft radius; long ASM then reaches three hexes, while short ASM and bombs reach one. This represents adjacent delivery without adding tactical flights to the movement-chit cup.

### Defensive aircraft and air combat

Before the first movement chit, a full four-aircraft fighter flight may be assigned to CAP. Carrier fighters may instead be assigned as deck-launched interceptors (DLI). CAP can radiate radar and intercept within ASR range; it always defends its own base or carrier group and persists after interception. DLI protects its own carrier group and is expended after use.

Interceptors engage escorts before mission aircraft. Escort and interceptor attacks are simultaneous. CAP contributes one attacking aircraft per interception; DLI uses its available aircraft once. Air combat rolls `D6 + ATA - target Defense`: modified 2 or less misses, 3–7 scores one hit, and 8 or more scores two. Each hit then uses the Aircraft Damage table: 1 no effect, 2–3 abort, 4–6 shot down.

Surviving CAP may engage air-launched missile factors before long-range SAM, treating missiles as Defense 0. Each hit removes one missile factor. The trace records every air-combat, aircraft-damage, SAM, point-defense, and impact die.

### Strike resolution

An enemy formation must first be detected by a friendly force. Printed shore bases are scenario-known targets. Each strike selects one weapon type:

- Long ASM launches outside aircraft-defense range. Missiles face aircraft interception, summed long-range SAM, short-range SAM from the target and a pair-mate, target point defense, and then the Bomb/SSM impact table.
- Short ASM aircraft first face long-range SAM. Survivors launch missiles, which face short-range SAM and point defense before impact.
- Bombing aircraft face long- and short-range SAM. Bombs do not face point defense and use the Bomb/SSM table against a selected ship or runway.

The optional First Island Chain EA-18G rule is enabled: a Growler escort reduces defending ASR and SSR by one, minimum zero, for that strike. The reduction is recorded in the trace.

### Player, AI, visual, and network presentation

The collapsible **Air Operations** panel exposes flight, strike strength, weapon, detected formation or known-base target, escort, radar CAP, and carrier DLI. Hot-seat setup can switch between US and PLAN declarations before the first chit. Solo PLAN uses the same command API: it establishes Ningbo CAP and launches a legal long-range strike only after gaining a US contact.

The board marks `0206` with a gold objective ring. Tactical strikes use animated low-poly jets, procedural audio, and impact effects. Flight missions, ready/flown/aborted/lost aircraft, radar declarations, and runway hits are included in authoritative save and multiplayer snapshots.
