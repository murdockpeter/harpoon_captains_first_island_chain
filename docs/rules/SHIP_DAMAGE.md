# Ship damage and the modern platform database

Section 8 applies the damage procedure from *Captain's Rules* page 4 to the modern platform values in *First Island Chain* pages 15–21. The project does not substitute legacy ship values: `ModernPlatformDatabase` contains all 34 hull-bearing US, PLAN, submarine, amphibious, replenishment, and generic auxiliary cards supplied in the supplement. Scenario 1 now obtains its four platform definitions from that database.

Aircraft cards and bases are not included in the ship database because they do not have hull boxes and use different damage procedures.

## Hull boxes and rounded thresholds

Each `H` marks one hull box and each `2H` marks two. Damage is capped at the printed hull value; excess hits do not create extra score. A ship with no remaining hull boxes is sunk.

The thresholds round the required damage upward:

| Printed hull | Half damage | Two-thirds damage |
|---:|---:|---:|
| 1 | 1 (sunk) | 1 (sunk) |
| 2 | 1 | 2 (sunk) |
| 3 | 2 | 2 |
| 4 | 2 | 3 |
| 5 | 3 | 4 |
| 6 | 3 | 4 |

This intentionally means that some small hulls skip a degraded state: a one-hull ship sinks on its first hit, a two-hull ship sinks at its two-thirds threshold, and a three-hull ship reaches half and two-thirds damage on the same second hit.

## Half damage

At the rounded half-damage threshold, a surviving ship:

- loses one point of speed, to a minimum of one;
- loses long-range SSM and long-range SAM capability;
- retains unexpended short-range SSM and other short-range weapons;
- loses air-search radar;
- may no longer launch aircraft if it is an aviation-capable carrier, LHA, LHD, or drone carrier.

The missile boxes remain recorded on the roster even when damage makes them unavailable.

## Two-thirds damage

At the rounded two-thirds threshold, a surviving ship:

- has speed one;
- loses SAM, point defense, SSM, torpedoes, ASW, sonar, air-search radar, and ESM;
- retains half its printed gun factor, rounded upward;
- retains its printed surface-search radar.

The retained SSR may radiate normally. Sinking subsequently removes SSR and every other capability.

## Immediate propagation

Damage-derived values are calculated from the marked hull boxes, so a hit takes effect without a cleanup phase:

- remaining movement is clamped to the task force's newly reduced effective speed;
- sunk ships leave task-force speed calculations and future movement chits;
- sunk or disabled weapons do not fire later in gun order or contribute missile defense;
- destroyed radar and ESM sources stop contributing to detection immediately;
- damage persists through missile counterattacks, repeated gun rounds, snapshots, multiplayer synchronization, and replay;
- Scenario 1 scoring retains the capped marked hull boxes even though sunk ships take no further actions.

Every damage trace records the previous and resulting damage state. Attack reports also distinguish a threshold crossing from a sinking event.

## Presentation

Formation cards show the rounded thresholds and effective values. A changed stat uses `printed→effective`, so retained and lost capabilities can be audited directly. Damaged formations gain an amber-to-red pulsing ring and progressively heavier smoke; mission-killed models darken and list; sunk formations remain as low, dark wreck markers instead of vanishing. A sinking adds water rings, a steam column, and a low structural-failure sound.
