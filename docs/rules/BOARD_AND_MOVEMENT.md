# Board and movement specification

Sources: *Captain's Rules* p. 3 and *First Island Chain Supplement* pp. 3 and 6.

## Coordinate ruling

The supplement map contains columns 1–15 and rows 1–20. Coordinates are stored as axial `(column, row)` values. This is confirmed by Scenario 1: `1010` to `0713` changes by `(-3,+3)` and is exactly three southwest neighbors, matching the scenario's explicit three-hex separation.

`HexCoord` owns distance, six-neighbor adjacency, and engine-independent layout coordinates. Movement validation, combat range, AI choice, breadth-first pathfinding, Unity tile placement, and the hover preview all consume this shared topology.

## Terrain

`FirstIslandChainMap` transcribes the printed land chain and six marked bases: Ningbo-Zhoushan, Xiamen, Yulin/Sanya, Kadena AB, Taipei/Zuoying, and Subic Bay/Clark. The map has an explicit restricted-water collection; it is empty because the green Hainan area on this map is land/off-map access rather than ice or restricted water.

Surface formations may enter sea and friendly naval-base hexes. They may not enter full land, restricted water, an opposing base, or coordinates outside the printed bounds.

## Activation movement

When a formation activates it must declare an integer speed from zero through the slowest active ship's effective speed. A task force retains a minimum effective speed of one while eligible. A move command enters exactly one adjacent navigable hex and spends one declared movement point. The formation cannot end its activation until every declared point is spent; declaring zero represents holding position.

Every entered hex resets the per-hex attack/search flags and emits a movement-opportunity event. Attacks and Scenario 1's recorded search action may therefore occur between movement steps. Entering an enemy-occupied hex leaves both formations in that coordinate and explicitly opens the combat/reaction window. Detailed missile allocation and counterattack resolution are owned by the later combat section.
