# Commands, events, and authoritative state

The rules simulation lives in `Harpoon.Core`, which has no Unity engine references. Unity renders projected state and submits commands; it does not decide whether an action is legal.

## Command flow

1. A caller creates a `GameCommand` with a unique ID, acting side, payload, and the authoritative revision it expects.
2. `IRulesEngine.Execute` validates game status, revision, duplicate ID, side, phase, terrain, range, and rule-specific payload.
3. A rejected command returns a `RuleViolation` with a stable `RuleViolationCode`. It does not enter the accepted command log or advance the revision.
4. An accepted command mutates `GameState`, advances its revision once, enters the command log, and emits ordered `RuleEvent` records tied to the command ID.
5. Solo AI, local UI, direct TCP, and public Relay all use this same boundary. Multiplayer clients submit commands; the host alone executes them and returns a projected snapshot.

Commands for later rules families already have protocol identities. When a command is not part of the selected scenario, it returns `UnsupportedCommand` instead of silently inventing behavior.

## Events and replay

`RuleEvent` is an immutable record carrying sequence, revision, turn, phase, type, actor, command ID, and detail. The existing human-readable transaction trace remains available for the in-game F3 debugger.

`ScenarioOneGame.Replay(seed, commandLog)` reconstructs a manual-opponent match from its original deterministic seed and accepted commands. Rejections are diagnostic events but are not authoritative mutations and therefore do not enter the replay log.

## Information boundaries

`SideGameView` always includes a side's own formation details. An unknown opposing formation exposes no position, unit list, or formation identity. Scenarios 1–3 pass `opponentKnown: true` because they precede detection; later scenarios use the same projection boundary for hidden contacts and dummy forces.
