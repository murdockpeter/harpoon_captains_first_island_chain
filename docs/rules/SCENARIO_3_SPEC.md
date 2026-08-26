# Scenario 3 authoritative specification

## Close Aboard

Source: *Harpoon: First Island Chain* p. 25, with platform cards on pp. 15 and 18 and the unchanged naval-gunfire procedure from *Captain's Rules* p. 7.

### Order of battle

- PLAN: three Type 056A (Jiangdao) corvettes at hex `1010`.
- US Navy: one Arleigh Burke Flight IIA and one Constellation-class frigate at hex `1313`.
- Detection is omitted because this introductory scenario precedes the detection rules.
- No turn limit is printed.

### Victory

Only hull hits caused by naval gunfire count. Missile hits still mark hull boxes and apply every normal damage effect, including sinking, but score zero victory points. The side that inflicted more gunfire hull hits when play ends wins; equal totals are a draw.

### Close action

The normal gun engagement procedure remains authoritative: same-hex engagement, relative-speed evade choice, equal-speed engagement roll, player-controlled firing/screened groups, strongest gun factor first, screened-target `−1`, immediate damage, and explicit break-off decisions after each round.

### Damage provenance

Each unit records total hull damage and the subset caused by gunfire. Gunfire credit is capped to hull boxes actually marked, so excess hits against a one-hull corvette cannot inflate the score. The provenance is included in snapshots and therefore survives save/load, replay, direct-IP play, and Relay play.

### Printed setup discrepancy

The prose describes hex `1313` as three hexes southeast of `1010`, while those explicit coordinates are six hexes apart under the established axial topology. As in Scenario 2, the implementation follows the explicit printed hex references and records the inconsistent directional prose rather than silently inventing a replacement setup.
