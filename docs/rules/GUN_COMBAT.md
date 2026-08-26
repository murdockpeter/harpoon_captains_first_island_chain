# Naval gunfire

Section 7 implements the close-action procedure from *Captain's Rules* page 7. Gunfire is a persistent, command-driven engagement, so every pairing, target, shot, die, and break-off choice is retained in the debug trace, snapshots, network state, and deterministic replay.

## Entering close action

- A moving task force can attempt gunfire only after entering the opposing surface formation's hex.
- Missile fire and the non-moving force's eligible missile counterattack resolve before gunfire begins.
- If the defender is faster, it chooses whether to evade or accept gunfire.
- A slower defender cannot escape the initial engagement.
- At equal effective speed, the attacker maneuvers into gunfire on a D6 result of `1–3`; `4–6` ends the attempt.

Effective speed includes current damage effects and ignores sunk ships.

## Firing formation and rollback

Each side accounts for every operational ship in groups of one or two. A two-ship group has exactly one **firing ship** and one **screened ship**. If ships were paired during the preceding missile exchange, those ships must remain together for gunfire, although either gun-capable member may be placed on top as the firing ship.

Only nominated firing ships receive a gun attack. They act from the highest current gun factor to the lowest; an initiative die orders equal opposing factors. Damage is immediate, so a firing ship sunk before its place in the sequence is skipped.

When its turn arrives, each firing ship chooses one operational enemy ship:

- firing at the opposing firing ship uses the printed die result;
- firing at its screened mate subtracts `1` from **each** die before reading the Guns column;
- a modified result of zero is a miss.

This explicit exposed-versus-screened target choice implements gun rollback: a player can attack the escort/firing ship without penalty or accept the penalty to reach the protected objective directly. Gun factors are never pooled across the task force.

## Resolution and break-off

Each gun factor rolls one D6 on the Guns combat-table column. Hull damage is applied before the next firing ship acts.

Damage retains its source as authoritative state. `GunfireHullDamage` increases only by hull boxes actually marked by `GunCombatResolver`; missile damage can degrade or sink a ship but never enters a gunfire-only scenario score. This provenance survives snapshots, networking, saves, and deterministic replay.

After every eligible ship has had its opportunity, both sides choose break off or continue:

- if both choose break off, the engagement ends;
- a faster force that chooses break off succeeds automatically;
- at equal speed, the sole force trying to break off succeeds on `1–2`;
- a slower force trying to break off succeeds on `1`;
- otherwise another round begins with the surviving nominated firing ships, again in strongest-first order.

## Interface feedback

The command panel names the firing and screened ship in each pair and labels every target as **EXPOSED** or **SCREENED −1**. A central orange close-action ribbon shows the round and stage. Gunfire has a distinct two-blast sound, muzzle flash, short arcing tracers, and lingering impact smoke so it cannot be confused with the longer missile animation.
