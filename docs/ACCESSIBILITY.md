# Accessibility and voice commands

Harpoon Captain's Edition includes an optional accessible command mode for blind and low-vision
players. It runs alongside the graphical interface and submits actions through the same authoritative
rules commands used by mouse controls, multiplayer, saves, and replay.

## Starting accessible mode

- Press **F2** anywhere, including the opening tribute, to toggle accessible command mode.
- Add `-accessibility` to the game command line to enable it before the opening screen.
- The setting is remembered after it is enabled.
- Press **F1** while accessible mode is active for spoken and visible help.

## Keyboard controls

- **Tab / Shift+Tab**: next or previous currently legal action.
- **Enter**: perform the selected action.
- **F5**: announce turn, phase, active formation, movement, and current status.
- **F6**: announce the currently legal actions.
- **F7**: announce location and visible contacts with range and bearing.
- **Ctrl+R**: repeat the last announcement.
- **Numeric keypad 8, 9, 6, 2, 1, 4**: north, northeast, southeast, south, southwest, northwest.
- **F8**: opt in to or disable voice commands for the current run.
- **Hold F4**: push to talk after voice commands have been enabled; release after speaking.

Map-cursor actions provide keyboard deployment and patrol-aircraft relocation. The cursor announces
its hex, terrain, and any formation the local side is allowed to see. Hidden-information rules remain
authoritative in solo, hot-seat, and network games.

## Voice vocabulary

Useful commands include `status`, `location`, `legal actions`, `next action`, `previous action`,
`activate`, `choose five`, `speed two`, `move northeast`, `cursor south`, `inspect cursor`,
`deploy here`, `relocate here`, `attack`, `end activation`, `repeat`, and `help`.

Attacks, strike launches, combat resolution, deployment, splitting, and ending an activation require
a second spoken `confirm`. Say `cancel` to discard a pending voice order. The recognized phrase and
confidence are displayed so a sighted assistant can diagnose recognition.

Voice input uses a constrained legal-command vocabulary rather than open dictation. This improves
recognition and prevents conversation from becoming arbitrary game input.

## Speech and privacy

Self-voicing and recognition use `HarpoonAccessibilitySpeech.exe`, included beside the Windows game.
It uses installed Windows speech services. Harpoon does not transmit, record, save, or include
microphone audio in multiplayer traffic. Voice input is off at every launch until the player presses
F8, and the microphone is active only while F4 is held.

If Windows has no installed voice, recognizer, or usable microphone, the game reports the problem and
retains all keyboard accessibility.

## Low-vision presentation

Accessible actions can set text to 100, 125, or 150 percent, enable high-contrast text, and enable or
disable self-voicing. Important state is expressed in text and speech rather than color alone.

## Testing expectations

The Windows smoke gate compiles and starts the speech companion without opening the microphone. A
release review must also complete every scenario decision family by keyboard and voice. Human
acceptance testing with blind players remains essential even though the player is self-voicing.
