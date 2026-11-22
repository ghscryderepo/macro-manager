# Windows Macro Manager

Small Windows Forms utility for two mouse button macros. Requires Windows 10/11 x64 to run. No PowerShell, Logitech G HUB, or AutoHotkey runtime is used.

## Use

1. Run `WindowsMacroManager.exe`.
2. Assign `None`, `Move + Click + Restore`, or `Alt + AutoClick` independently to Windows `XBUTTON1` and `XBUTTON2`. The window labels these Mouse 4 and Mouse 5, but mouse software may name the physical buttons in the opposite order. Press a side button and check **Last side button** to see which Windows event it sends.
3. For the move macro, enter desktop pixel coordinates or click **Capture position**, move the pointer, and press **F8**. Press **Esc** in the window or click Cancel to abandon capture.
4. Set the two auto click delays (1–10000 ms), then click **Save / Apply**. The initial setting is 1 ms for each delay; Windows scheduling does not guarantee exact 1 ms intervals.
5. **Panic Disable** or global **Ctrl+Shift+F12** stops the active macro and disables the app. Use the tray menu to enable/disable, open, or exit. Closing the window hides it in the tray.

Configuration is stored in `%LOCALAPPDATA%\WindowsMacroManager\settings.json`. Missing or invalid JSON loads defaults with a warning. The default bindings are Mouse 4 for move/click/restore and Mouse 5 for Alt auto click. The default target is `(37364, 48092)` in Logitech's 0–65535 coordinates, converted to pixels across the Windows virtual desktop. You can capture a new target in physical desktop pixels.

## Behavior

- Move macro: on press, save pointer position, move to target, click once; on release, restore the saved position even if the pointer moved manually while held.
- Auto click: on press, hold synthetic Left Alt, then repeat left down, delay, left up, delay. On release, stop and release synthetic inputs. If Left Alt was already down, the macro does not synthesize or release it.
- One macro activation runs at a time. If another assigned button is pressed while one is active, that press is ignored until the active macro finishes cleanup; it does not start later automatically. Either macro may be assigned to both buttons.
- Native Mouse 4/5 events pass through by default. The optional suppression setting blocks assigned button events from reaching other applications.
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
