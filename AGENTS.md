# Codex instructions — Windows Macro Manager

## Goal
Build a small native-feeling Windows desktop utility for configurable mouse-button macros, replacing Logitech G HUB Lua macros. Read `SPEC.md` before implementation. Treat the spec as the product contract; ask about unresolved behavior rather than inventing it.

## Stack
- C# and .NET 8 or newer, Windows Forms (WinForms).
- Windows API via P/Invoke for mouse hooks, input injection, and cursor positioning.
- JSON configuration saved in the user's local application data directory.
- No Logitech G HUB, AutoHotkey, or PowerShell runtime dependency.
- Publish as a self-contained Windows x64 executable; document exact build and publish commands.

## Engineering requirements
- Separate UI, configuration, hotkey/mouse hook, and macro execution services.
- Use `WH_MOUSE_LL` with a proper message loop; keep hook callbacks short and non-blocking.
- Run repeating actions on cancellable background tasks; stop immediately on button release, disable, exit, or configuration change.
- Prefer `SendInput` over legacy `mouse_event`/`keybd_event`; track injected events and avoid recursion.
- Do not suppress native mouse buttons unless the user explicitly enables that behavior.
- Always release synthetic held modifiers and mouse buttons in `finally` blocks, including during shutdown.
- Avoid sending input to an unintended target when focus changes; document limitations with games, elevated windows, and anti-cheat.
- Support high-DPI and multi-monitor coordinate systems; don't assume the primary monitor is the entire desktop.
- Keep UI responsive and marshal updates onto the UI thread.
- Validate config values and handle corrupt/missing JSON gracefully.
- Provide clear status/error messages and a panic disable action.

## Workflow
1. Read `SPEC.md` and implement an MVP for the two specified macros.
2. Add GUI, button assignment, capture target coordinates, persistence, and tray controls.
3. Add tests where practical (config serialization, coordinate conversion, macro state transitions).
4. Build and publish for `win-x64`; report tested and untested behavior honestly.
5. Include a README with usage, build commands, and limitations.

Do not claim real-world input behavior was tested if only compilation succeeded.

## Legacy implementation references
Read both `legacy/mouse-position.ps1` and `legacy/alt-autoclick.ps1` before implementation. They reproduce the earlier PowerShell proof-of-concept scripts and clarify precise event order. They are **references, not production code**: do not invoke PowerShell from the app. Preserve the intended behavior, but address their limitations (polling, deprecated input APIs, primary-monitor-only conversion, cleanup on abrupt exit, and possible interaction conflicts) using the native C# design in `SPEC.md`.
