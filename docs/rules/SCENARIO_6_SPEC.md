# Scenario 6 authoritative specification

## Wolves of the Bashi Channel

Source: *Harpoon: First Island Chain* p. 25, modern submarine and surface cards on pp. 15 and 19, and *Captain's Rules* pp. 10–11.

### Forces and setup

- US Navy surface hunter-killer group: one Arleigh Burke Flight III and two Constellation frigates entering on the east map edge.
- US Navy submarine task force: one Los Angeles (688i), kept separate from the surface group.
- PLAN: two separate Type 039A/B (Yuan) SSK task forces and one Type 093B SSN task force, initially distributed along `0812`–`1212`.
- PLAN submarine movement must remain within two hexes of that patrol line.
- The game lasts seven complete turns.

Submarines and surface vessels may never share a task force. The First Island Chain map has no seasonal ice or ice-shelf hexes, so the general submarine ice exception has no scenario-map effect.

### Detection

SSR and visual searches can never classify a submarine. A successful surface search instead reports **no surface ships present** and leaves a located contact consistent with a submarine or dummy. Sonar uses the best operational sonar, `+1` for multiple sonar ships, range penalties of `−2` at one hex and `−3` at two, target speed, searching-force speed, `+1` for a previously detected contact, and natural-six failure. ESM works in both directions when a submarine radiates its SSR.

Side-private snapshots reveal the location but not the identity or card of a merely located undersea contact. Classification is required before attack.

### Combat

- SSMs and guns cannot attack submarines. A submarine may fire SSMs at a classified surface formation under the normal surface-missile procedure.
- Torpedoes require a classified surface formation in the same hex. Each submarine allocates its complete torpedo factor to one ship or splits it among several ships.
- Torpedo dice use the Torpedo Attack Table: `1–2` miss, `3–5` one hull hit, and `6` two hull hits.
- Torpedo attacks against screening ships resolve first. If the attacking submarine was detected, eligible ASW counterattacks then resolve; torpedoes against screened ships resolve last. A submarine attacking only screening ships may be counterattacked only by a directly attacked screen ship that survived.
- A detected submarine in the same hex may be attacked by one eligible ship using its ASW value. ASW dice use the ASW Attack Table: `1–3` miss, `4–5` one hull hit, and `6` two hull hits.

The current Scenario 6 interface provides explicit per-ship torpedo targeting. Multi-submarine task forces are also supported by the command model: each source submarine contributes and validates its own complete allocation.

### Victory

At the end of turn seven, begin with three PLAN submarines. Each two US ships sunk offsets one PLAN submarine loss. PLAN wins if the adjusted losses leave at least two submarines alive; otherwise the US Navy wins. The UI displays raw PLAN submarine losses, US ship losses, and adjusted PLAN losses separately.

