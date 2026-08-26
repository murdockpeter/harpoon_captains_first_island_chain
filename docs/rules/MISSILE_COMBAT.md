# Surface-to-surface missile combat

Section 6 implements Captain's Rules pages 4–6 and the First Island Chain compressed rules on pages 3–4. Missile combat is an explicit, snapshot-safe command sequence; opening an attack never spends ammunition automatically.

## Range and ammunition

- Short-range SSM factors may fire at range zero or one hex.
- Long-range SSM factors may fire at up to three hexes.
- Each printed factor is one ammunition box and may be fired once before resupply.
- The attacker chooses any legal number of currently available SR and LR factors from each firing ship.
- The engine validates the complete plan before committing any box. An illegal or excessive allocation spends nothing.
- Half-damaged ships cannot allocate long-range SSM; ships at two-thirds damage cannot allocate either type.

## Staged procedure

1. **Open attack:** select an opposing task force. Detection gating applies when the scenario uses detection.
2. **Allocate fire:** assign chosen factors from individual firing ships to individual target ships. Separate allocations become separate salvos.
3. **Defensive deployment:** the defender pairs ships. A pair may protect each other; an odd ship may remain unpaired. All operational long-range SAM batteries then roll against the raid.
4. **Long-range removals:** after seeing the LR-SAM hit total, the defender chooses exactly which incoming salvo factors are removed.
5. **Short-range SAM:** each operational battery may engage one surviving salvo aimed at itself or its designated pair-mate. A battery cannot split between two salvos.
6. **Point defense:** each attacked ship rolls its own PD once against all surviving factors aimed at it. Another ship's PD cannot protect it.
7. **Missile impacts:** every surviving factor rolls separately on the Bombs & SSM column. `M`, `H`, and `2H` produce zero, one, and two hull hits respectively.
8. **Counterattack:** after the moving force's raid resolves, the non-moving force may immediately decline or begin one missile counterattack if it has a detected, in-range target and ammunition. A counterattack cannot itself be counter-counterattacked. The original formation then resumes its interrupted movement/action phase.

Every allocation, die, ammunition change, defense assignment, removal, impact, and timing transition is emitted to the rules-debug trace. Commands, pending salvos, defensive pairs, committed ammunition, and the counterattack state are retained by snapshots and deterministic replay.

## Split fire and rollback interpretation

Captain's Rules page 5 says missile strength may be combined or split as desired and gives examples of one ship dividing factors among several targets. A parenthetical on page 6 says missiles fired by one ship are aimed at one target. The First Island Chain supplement explicitly permits splitting attack strength among any number of targets. Under this project's documented source precedence, the supplement's explicit instruction and the detailed page 5 examples control: a ship may split its factors into multiple indivisible salvos before launch.

That split is also how missile rollback pressure is represented. Allocating factors to both ships in a defensive pair forces the defender to choose which salvo receives that pair's limited short-range SAM fire; concentrating only on the screened objective lets its escort focus entirely on protecting it.

## Interface and feedback

The command panel presents every required decision and displays the current raid strength and decision owner. A central combat ribbon shows the current stage. Launches animate as multiple missiles; intercepted visual missiles are destroyed in cyan SAM bursts before surviving weapons reach the target. The formation cards continue to display remaining SR/LR ammunition after every commitment.
