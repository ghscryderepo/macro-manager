# Windows Macro Manager

The application icon uses the Lineage II Wings of Destiny Circlet (Hero Tiara) item icon, sourced from [ElmoreLab's icon archive](https://resources.elmorelab.com/images/accessory_hero_cap_i00.jpg). Credits are available from **About** in the window or tray menu.

Small Windows Forms utility for two mouse button macros. Requires Windows 10/11 x64 to run. No PowerShell, Logitech G HUB, or AutoHotkey runtime is used.

## Use

1. Run `WindowsMacroManager.exe`.
2. Assign `None`, `Move + Click + Restore`, `Alt + AutoClick`, or `F-key cycle (toggle)` independently to Windows `XBUTTON1` and `XBUTTON2`. The window labels these Mouse 4 and Mouse 5, but mouse software may name the physical buttons in the opposite order. Press a side button and check **Last side button** to see which Windows event it sends.
3. For the move macro, enter desktop pixel coordinates or click **Capture position**, move the pointer, and press **F8**. Press **Esc** in the window or click Cancel to abandon capture.
4. Set the two auto click delays (1–10000 ms) and clicks per Alt cycle (default 17), then click **Save / Apply**. The initial setting is 1 ms for each delay; Windows scheduling does not guarantee exact 1 ms intervals.
5. For the F-key cycle, choose the last key (F1–F24), key hold duration, and interval between keys. The first press of its assigned side button starts `F1 → F2 → … → Fx → F1`; the next press stops it. Releasing the side button does not stop this mode.
6. **Panic Disable** or global **Ctrl+Shift+F12** stops active macros and disables the app. Use the tray menu to enable/disable, open, or exit. Closing the window hides it in the tray.

Configuration is stored in `%LOCALAPPDATA%\WindowsMacroManager\settings.json`. Missing or invalid JSON loads defaults with a warning. The default bindings are XBUTTON2 for move/click/restore, matching `legacy/mouse-position.ps1`, and XBUTTON1 for Alt auto click. In an existing installation, click **Use PowerShell setup** to restore those bindings, the original target, and enable the app. The default target is `(37364, 48092)` in Logitech's 0–65535 coordinates, converted to pixels across the Windows virtual desktop. You can capture a new target in physical desktop pixels.

## Behavior

- Move macro: on press, save pointer position, move to target, click once; on release, restore the saved position even if the pointer moved manually while held.
- Auto click: while the side button is held, repeat the recorded G HUB sequence: Left Alt down, 1 ms delay, 17 left down/up pairs separated by 1 ms delays, Left Alt up. On release, stop and release synthetic inputs. If Left Alt was already down, the macro does not synthesize or release it. The click count and delays are configurable.
- F-key cycle: toggle a repeating sequence from F1 through the selected function key. Each key receives a synthetic down/up pair. The key hold and between-key intervals are configurable; the between-key interval also applies from Fx back to F1. The cycle stops on the next toggle press, focus change, disable, configuration change, or exit.
- The F-key cycle can run alongside one mouse macro. The two held mouse macros still use a single activation slot, so pressing the other mouse macro during one activation is ignored.
- One held mouse macro activation runs at a time. If another held mouse macro button is pressed while one is active, that press is ignored until the active macro finishes cleanup; it does not start later automatically. The same macro may be assigned to both buttons.
- Native Mouse 4/5 events pass through by default. The optional suppression setting blocks assigned button events from reaching other applications.
- The app listens for side buttons with both a low-level mouse hook and Raw Input. The **Last side button** line shows the detected Windows button and source. Raw Input can see some buttons consumed by mouse software; it cannot override a device configuration that sends no Windows side-button event.
- A focus change stops auto click before the next click. The move macro checks focus before its click, and restores the original pointer location on release. Input already sent before a focus change cannot be recalled.
- Save/Apply, disable, and exit cancel the active action. Synthetic held inputs are released in cleanup.

## Build and publish

GitHub Actions builds the Windows x64 executable on every push and pull request, and can also be started from the **Actions** tab with **Run workflow**. Tagged versions (`v*`) also publish `WindowsMacroManager.exe` to GitHub Releases for direct download. The repository must be pushed to GitHub for this workflow to run.

Install the .NET 8 SDK (or newer) on Windows. From this directory in PowerShell:

```powershell
dotnet restore .\WindowsMacroManager.csproj
dotnet build .\WindowsMacroManager.csproj -c Release
dotnet publish .\WindowsMacroManager.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o .\publish
```

Run `publish\WindowsMacroManager.exe`. No .NET runtime installation is needed on the target Windows x64 machine.

## Limitations

`SendInput` may be ignored by some games or anti-cheat systems. A normal process cannot reliably inject input into elevated windows; launch at the appropriate integrity level if needed. Windows and applications may process injected input with different timing. The panic shortcut requires global hotkey registration; the window reports if it is unavailable. A process killed abruptly by the OS cannot run cleanup, although Windows releases its hook. Real mouse and game compatibility require testing on the target machine.
