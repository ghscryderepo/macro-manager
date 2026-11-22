# Windows Macro Manager — Product Specification

## Background
The user wants to replace Logitech G HUB Lua scripts/macros with one configurable Windows desktop program, with a GUI to assign behaviors to Mouse 4 and Mouse 5. The original Lua macro is:

```lua
local WATCH_GAME_X = 18285
local WATCH_GAME_Y = 42081
local FIRST_ARENA_X = 34394
local FIRST_ARENA_Y = 31515
local BUTTON5_X = 37364
local BUTTON5_Y = 48092

local saved_x, saved_y = 0, 0

function OnEvent(event, arg)
    if (event == "MOUSE_BUTTON_PRESSED" and arg == 5) then
        saved_x, saved_y = GetMousePosition()
        OutputLogMessage("Mouse is at %d, %d\n", saved_x, saved_y)
        MoveMouseTo(BUTTON5_X, BUTTON5_Y)
        PressAndReleaseMouseButton(1)
    end
    if (event == "MOUSE_BUTTON_RELEASED" and arg == 5) then
        MoveMouseTo(saved_x, saved_y)
    end
end
```

The Logitech coordinates are normalized approximately to 0–65535. `WATCH_GAME` and `FIRST_ARENA` are currently unused constants, not additional required macro actions.

A second G HUB macro shown in a screenshot is named `ALTCELKA`: it holds Alt while repeatedly pressing/releasing left mouse button, with recorded 1 ms delays, and is configured for repeat while held.

## MVP behaviors

### Macro A — Move, click, restore
- On the assigned mouse button **press**, record current cursor location in physical desktop pixels.
- Move cursor to a configurable target location and send one left click.
- On the assigned button **release**, restore the cursor to its original location.
- Execute once per press, not continuously.
- Default target is the original G HUB position `(37364, 48092)` in Logitech-normalized coordinates; convert accurately and allow the user to re-record the target in Windows pixel coordinates.
- Preserve the original position for each activation; don't overwrite it while held.

### Macro B — Alt + AutoClick while held
- On assigned mouse button **press**, hold Left Alt and repeatedly send left mouse down/up until the button is released.
- On **release**, stop clicking and release synthetic Alt promptly.
- Configurable delay between mouse down/up and between clicks, with an initial 1 ms setting. Explain that Windows scheduling does not guarantee exact 1 ms timing.
- Do not move the cursor.
- Avoid leaving Alt or left mouse down after cancellation or exceptions.

### Button bindings
- GUI drop-down for Mouse 4 (`XBUTTON1`) and Mouse 5 (`XBUTTON2`), each with `None`, `Move + Click + Restore`, `Alt + AutoClick`.
- Suggested defaults: Mouse 4 -> Move + Click + Restore; Mouse 5 -> Alt + AutoClick. These are suggestions, not a requirement that either button be permanently fixed.
- Resolve collisions/conflicts deterministically; the same macro may be assignable to both buttons if state handling supports it safely, otherwise prevent duplicate assignment with a clear explanation.
- Native button event suppression is optional, controlled by an explicit setting; default OFF.

## GUI
- Compact WinForms window with enabled/disabled status, Mouse 4 and Mouse 5 assignment controls, settings for the selected macro, Save/Apply, and panic Disable.
- For Move macro: show target position in desktop pixels; add `Capture position` mode. After clicking capture, user moves pointer and presses F8 to store its location. Provide Cancel/Esc and a visible capture state.
- For AutoClick: configure down/up and inter-click delays (milliseconds), display effective behavior.
- Tray icon with Enable/Disable, Open, Exit. Closing window may minimize to tray; Exit must fully release held inputs and unregister hooks.
- Persist settings as JSON under `%LOCALAPPDATA%/WindowsMacroManager/settings.json`.
- The UI should remain usable without requiring admin privileges for ordinary desktop applications.

## Implementation details and edge cases
- Use low-level mouse hook `WH_MOUSE_LL` for button press/release detection; handle `WM_XBUTTONDOWN` and `WM_XBUTTONUP` and distinguish XBUTTON1/XBUTTON2 via high word of `mouseData`.
- Avoid blocking hook callbacks or invoking UI code synchronously from hooks.
- Use `SendInput` for keyboard/mouse events. Synthetic Left Alt is `VK_LMENU` or corresponding scan code. Consider modifier state and don't release a physically held Alt key.
- Provide a single consistent policy for simultaneous activations and when disabling mid-macro; document it.
- Ensure injected input isn't interpreted as a trigger for another macro.
- Windows coordinate conversion must support virtual desktop bounds, multiple monitors, and DPI scaling; validate normalized coordinate conversion against Logitech behavior.
- Add emergency disable shortcut (proposed `Ctrl+Shift+F12`) and make it configurable if easy.
- Handle unexpected hook failure or application shutdown safely.

## Out of scope for MVP
- Full G HUB-style arbitrary sequence editor.
- Per-game profiles and cloud sync.
- Kernel drivers or bypasses for game anti-cheat.
- Automated background updates.

## Post-MVP addition — Function-key cycle

- A third assignable macro, `F-key cycle (toggle)`, sends F1, F2, and so on through a user-selected final key (F1–F24), then repeats from F1.
- The first press of the assigned side button starts the cycle; the next press stops it. Releasing the side button does not stop this macro.
- GUI fields set the final F-key, key hold duration, and interval between keys. The interval also applies between the last key and F1 of the next cycle.
- Stop on disable, configuration change, focus change, or exit, and release any synthetic held key during cleanup.
- The keyboard cycle may run alongside one held mouse macro. Its toggle state is independent of button release.

## Acceptance criteria
1. GUI lets the user assign either macro or None to Mouse 4/Mouse 5 without code changes.
2. Move macro clicks once on press and restores saved cursor position on release.
3. AutoClick macro operates only while its button is held and stops promptly on release.
4. Alt and left mouse never remain synthetically held after disable/exit.
5. Settings survive restart.
6. Capture target via F8 works and can be canceled.
7. Tray enable/disable and exit work; emergency disable works.
8. Self-contained Windows x64 executable can be built from documented instructions.

## Open questions for the implementer
- Should the app suppress Mouse 4/5 events from reaching the active application? Default: no.
- Should the move macro restore the cursor immediately if the user manually moves it during the hold? Original behavior: yes, on release.
- Does the user's specific game accept `SendInput`? This requires real testing; do not promise compatibility.

## Original PowerShell prototypes
The complete scripts previously supplied in ChatGPT are preserved verbatim in `legacy/mouse-position.ps1` and `legacy/alt-autoclick.ps1`. Both originally listen to Mouse 5; the finished app must make their actions assignable to Mouse 4 or Mouse 5. The original scripts use polling and legacy injection APIs; these are reference implementations rather than required architectural choices.
