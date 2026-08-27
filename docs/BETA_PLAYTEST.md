# Harpoon Captain's Edition — MVP 1.0 beta playtest guide

## What testers receive

Distribute `Harpoon-Captains-Edition-Windows.zip` together with
`Harpoon-Captains-Edition-Windows.zip.sha256`. A tester should extract the complete ZIP to a
writable folder and launch `HarpoonCaptainsEdition.exe`. The executable must remain beside its
`HarpoonCaptainsEdition_Data` folder, `UnityPlayer.dll`, and the other packaged runtime files.

The game is portable: there is no installer and no Unity installation is required. Windows may
show a SmartScreen warning because the community build is not code-signed.

The window title, executable/data filenames, downloads, documentation, and in-game product identity
all use **Harpoon Captain's Edition**. **First Island Chain** is the included scenario module.

## Recommended first session

1. Start with Scenario 1 in solo mode and verify movement, attack allocation, defense, and scoring.
2. Run a direct-IP or hot-seat Scenario 2 match to verify two-player decisions.
3. Select several later scenarios from **Match & System**, including Scenario 10 **First Light**.
4. Use **Pause / Save** and restore once during a match.
5. At game end, use **Export Match** so a deterministic record is available with any report.

For remote public matches, the host needs the repository's configured Unity Lobby/Relay project.
Direct IP is the least complicated option on a trusted LAN; internet direct-IP hosting may require
router/firewall configuration.

## Reporting a problem

Please include:

- game version, scenario, match seed, turn, time of day, and player side;
- the command attempted and the result expected;
- a screenshot;
- the exported match record, when available;
- the Unity player log from `%USERPROFILE%\AppData\LocalLow\Open Source Harpoon Community\Harpoon Captain's Edition\Player.log`.

Rules disagreements should cite the supplement or Captain's Rules page. Do not use the F3 detection
test mode for release acceptance; it is intentionally non-authoritative for Scenarios 1–3.

## Beta scope

MVP 1.0 covers all ten introductory scenarios, the complete modern platform and aircraft card data,
the six shore-base charts, both carrier-wing charts, deterministic saves/replays, hot-seat, direct-IP
and public 1-v-1 networking, chat/soundboard, and opt-in verified updates. The post-MVP secret-mission,
asset-purchase, resupply, base-capture, and 21-turn campaign systems remain outside this beta.
