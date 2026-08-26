# Movement-chit sequence

Section 4 implements the Captain's Rules page 3 movement-chit procedure as authoritative core state rather than UI initiative logic.

## Turn flow

1. At the start of a turn, the cup receives one eligible chit for every task force. The cup API can also receive patrol-aircraft chits when those units are introduced.
2. Before the first draw only, either side may split a multi-ship task force. The new colocated formation receives its own chit before the cup is randomized.
3. `DrawMovementChit` selects one remaining chit uniformly with the match's seeded random source and removes it from the cup.
4. Only the exact named formation becomes active. It declares speed, moves, and resolves its action opportunities normally.
5. Ending an activation draws the next remaining chit. The turn cannot end while a chit remains.
6. After the last activation, all eligible task-force chits return for the next turn.

The first draw of every turn is an explicit player command so multiplayer clients can see and synchronize the transition. Later draws follow automatically when an activation ends. Every draw and split emits an ordered rules transaction and is preserved in snapshots and deterministic replay.

## Time periods

There are three turns per day in this order: `AM`, `PM`, and `NIGHT`. Completing Night advances the day and returns to AM. Visual-search commands reject during Night; other search modes are unaffected by this restriction.

## Presentation

The HUD displays the period, remaining cup count, active formation, and a draw banner. A pre-draw split control is shown when the local side has a multi-ship task force. Split formations receive independent 3D markers and selectable formation cards.
