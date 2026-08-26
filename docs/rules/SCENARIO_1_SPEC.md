# Scenario 1 authoritative specification

## Contact off the Bashi Channel

### Setup

- PLAN task force at hex `1010`: one Type 054A frigate screening one Type 071 LPD.
- US task force at hex `0713`: one Arleigh Burke Flight IIA screening one Merchant Ship.
- The source states the formations begin three hexes apart.
- Sources: First Island Chain pp. 25, with unit cards on pp. 15, 18–19, and 21.

### Learning-rule scope

Scenario 1 teaches surface missile combat. Detection is not used. Captain's Rules p. 3 explicitly allows the initial surface-combat section to ignore radar for learning, and p. 6 directs players to play Scenarios 1 and 2 before the detection section begins on p. 8. Both formations therefore begin attackable and their contents are known.

### Card notation and ammunition

- `SAM A-B`: `A` is short-range SAM strength; `B` is long-range SAM strength.
- `SSM A-B`: `A` is short-range SSM strength; `B` is long-range SSM strength.
- Short-range SSM range is one hex; long-range SSM range is three hexes.
- At one hex, short- and long-range factors may be combined or split.
- Every printed SSM factor is one expendable factor. Each may be fired once per game.
- A player may fire any subset and retain the rest. Factors are not automatically replenished.
- Source: Captain's Rules pp. 4–6.

### Combat sequence relevant to the scenario

1. The moving side declares whether it attacks.
2. The non-moving side declares whether it attacks.
3. If only the non-moving side attacks, the mover may then counterattack.
4. Declared attacks caused by the same entered hex are simultaneous, even if a ship is sunk before its dice are rolled.
5. For each missile raid: long-range SAM, short-range SAM, point defense, then surviving missile attacks.
6. Gun combat may follow missile exchange only when formations occupy the same hex.
- Sources: Captain's Rules pp. 3–7.

### Damage

- One hit marks one hull box; no hull boxes remaining means sunk.
- On reaching at least one-half damage: speed `−1`, long-range SSM and SAM become zero, ASR becomes zero, and a carrier cannot launch aircraft.
- On reaching at least two-thirds damage: speed becomes one; all weapons are lost except half guns rounded up; sonar and ESM are lost; SSR remains.
- Thresholds round damage upward. For example, the original rule treats three damage on a five-hull ship as the half-damage state and four damage as the two-thirds state.
- Source: Captain's Rules p. 4. This deliberately overrides the inaccurate compressed statement that all SSM is lost at half damage.

### Victory and termination ruling

- Primary score: hull damage inflicted on the opposing objective ship (Merchant Ship or Type 071).
- If primary damage is equal, compare hull damage inflicted on the opposing surface combatant (Arleigh Burke or Type 054A), following the analogous original Scenario 1 tie-break on Captain's Briefing p. 4.
- If both comparisons are equal, the result is a draw.
- Neither the modern nor analogous original Scenario 1 prints a turn limit. The project therefore uses no arbitrary turn cap.
- The engagement ends when a primary result becomes mathematically fixed, one side has no ships afloat, a player concedes/disengages, or both sides agree to score the current state. The future command-driven implementation must expose disengage/concede; until then, the prototype ends when an objective ship sinks.

### Recorded interpretations

1. **Source precedence:** original full mechanics override compressed supplement mechanics because the supplement says the core rules are unchanged and refers unclear cases to the full booklet.
2. **Tie-break:** import the analogous original Scenario 1 surface-combatant tie-break because the modern reskin omits any equal-damage instruction.
3. **No turn cap:** omission is not permission to invent a duration; use disengagement/concession and fixed-result termination.
4. **Map topology:** the source claims `1010` and `0713` are three hexes apart. The core currently honors that statement with axial distance, but the coordinate/render topology still requires a dedicated map audit before Section 3 can be completed.

