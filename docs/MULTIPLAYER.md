# One-versus-one multiplayer

## Current architecture

The host chooses US Navy or PLAN and owns the canonical rules state, random seed,
dice, transaction log, and victory result. The joining player is assigned the
opposing side. A joining player sends side-bearing requested commands; only the host
validates and applies them. Commands claiming the wrong side are rejected. The host
then broadcasts a selected-scenario snapshot. Its scenario ID switches clients to the host's order of battle automatically. Scenarios 1–3 are public-information learning games; Scenario 4 omits undetected opposing positions, units, paths, commands, contacts, logs, and rules transactions.

Chat messages and soundboard cue IDs share the TCP connection but never enter the
rules command stream. Soundboard phrases are spoken by the local Windows speech
voice, with a generated cue tone as a fallback.

## Public Relay mode

The recommended public mode uses Unity Multiplayer Services `2.3.1`, Netcode for
GameObjects `2.13.2`, Unity Relay, and DTLS. It provides:

- anonymous player authentication;
- encrypted Relay traffic without exposing player IP addresses;
- six-character join codes;
- discoverable or private two-player sessions;
- optional session passwords of 8-64 characters;
- a public-session browser;
- reconnect to the most recently joined session;
- host-authoritative rules commands and snapshots;
- opponent chat/sound muting and basic outbound rate limits;
- display of Unity service/DSA notifications returned for the signed-in player.

Before Public Relay can contact Unity's servers, this local Unity project must be
linked to a Unity Cloud project. Open the project in Unity and choose
**Harpoon > Public Multiplayer Setup**, link or create a Cloud project, and enable
Authentication, Lobby, and Relay. This account-level operation cannot be performed
by the source code or Windows build.

Once linked, choose a host side and then **Multiplayer > Host Public**. Share the
displayed join code, or leave the session discoverable. The other player can enter
that code or use **Refresh Browser** and is assigned the opposing side.

## Local two-instance test

1. Run `multiplayer-local.cmd`. It builds once and opens two windowed instances.
2. In the first instance, choose **Multiplayer**, select a side, retain port `7777`,
   and choose **Host Direct**.
3. In the second, choose **Multiplayer**, enter `127.0.0.1`, retain port `7777`,
   and choose **Join Direct**. It receives the opposing side.
4. The active side can move, attack, and end its activation. Use the comms panel
   for chat and soundboard cues. F3 shows network activity in the rules trace.

## Direct-IP LAN mode

For LAN play, join using the host computer's private IPv4 address. Windows may ask
the host to permit the executable through the firewall.

The original direct TCP mode remains available for localhost and trusted LAN/VPN
testing. It has no encryption or authentication and must not be exposed directly to
the public internet. Use Public Relay for internet play.

Public Relay currently does not provide dedicated servers, automatic skill-based
matchmaking, host migration, player accounts that survive uninstall, or a custom
abuse-report backend. Those remain later production-release tasks.
