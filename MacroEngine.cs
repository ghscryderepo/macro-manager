namespace WindowsMacroManager;

internal sealed class MacroEngine : IDisposable
{
    private readonly object sync=new();
    private Settings settings;
    private int activeButton;
    private CancellationTokenSource? cancellation;
    private Task? running;
    private CancellationTokenSource? keyboardCancellation;
    private Task? keyboardRunning;
    private readonly bool[] pressed=new bool[3];
    private bool disposed;
    internal event Action<string>? Status;
    internal MacroEngine(Settings initial) => settings=initial;

    internal bool ShouldSuppress(int button) { lock(sync) return settings.Enabled && settings.SuppressButtons && (button==1 ? settings.Mouse4 : settings.Mouse5)!=MacroKind.None; }
    internal void HandleButton(int button, bool down, Native.Point point)
    {
        lock(sync)
        {
            if (disposed) return;
            if (!down)
            {
                pressed[button]=false;
                if (activeButton==button) StopMouseLocked();
                return;
            }
            if (pressed[button]) return; // Hook and Raw Input can report the same press.
            pressed[button]=true;
            if (!settings.Enabled) return;
            var kind=button==1 ? settings.Mouse4 : settings.Mouse5;
            if (kind==MacroKind.None) return;
            if (kind==MacroKind.FunctionKeyCycle) { ToggleKeyboardLocked(); return; }
            if (activeButton!=0) return;
            activeButton=button;
            cancellation=new CancellationTokenSource();
            var source=cancellation;
            var token=source.Token;
            var current=settings;
            var foreground=Native.GetForegroundWindow();
            running=Task.Run(async () =>
            {
                try
                {
                    if (kind==MacroKind.MoveClickRestore) await MoveClickRestore(current, point, foreground, token);
                    else AutoClick(current, foreground, token);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex) { Report($"Macro error: {ex.Message}"); }
                finally
                {
                    lock(sync)
                    {
                        if (activeButton==button && ReferenceEquals(cancellation, source))
                        {
                            activeButton=0;
                            cancellation=null;
                        }
                    }
                    source.Dispose();
                }
            });
        }
    }

    private void ToggleKeyboardLocked()
    {
        if (keyboardCancellation is not null)
        {
            keyboardCancellation.Cancel();
            Report("F-key cycle stopping.");
            return;
        }
        var source=new CancellationTokenSource();
        keyboardCancellation=source;
        var current=settings;
        var foreground=Native.GetForegroundWindow();
        keyboardRunning=Task.Run(() =>
        {
            try { RunKeyboardCycle(current, foreground, source.Token); }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Report($"F-key cycle error: {ex.Message}"); }
            finally
            {
                lock(sync)
                {
                    if (ReferenceEquals(keyboardCancellation, source)) keyboardCancellation=null;
                }
                source.Dispose();
            }
        });
    }

    private void RunKeyboardCycle(Settings config, nint foreground, CancellationToken token)
    {
        ushort heldKey=0;
        bool timerRaised=Native.timeBeginPeriod(1)==0;
        long completedCycles=0;
        long lastStatus=Environment.TickCount64;
        string stopReason="toggle, disable, or exit";
        try
        {
            Report($"F-key cycle running: F1–F{config.LastFunctionKey}.");
            while (true)
            {
                for (int number=1; number<=config.LastFunctionKey; number++)
                {
                    token.ThrowIfCancellationRequested();
                    if (Native.GetForegroundWindow()!=foreground)
                    {
                        stopReason="focus changed";
                        return;
                    }
                    ushort key=(ushort)(Native.VkF1+number-1);
                    // Avoid releasing a function key held by the user.
                    if ((Native.GetAsyncKeyState(key) & 0x8000)==0)
                    {
                        Native.SendKey(key, false); heldKey=key;
                        Wait(config.FunctionKeyHoldMs, token);
                        Native.SendKey(key, true); heldKey=0;
                    }
                    Wait(config.FunctionKeyIntervalMs, token);
                }
                completedCycles++;
                if (Environment.TickCount64-lastStatus>=1000)
                {
                    Report($"F-key cycle running: {completedCycles} passes through F1–F{config.LastFunctionKey}.");
                    lastStatus=Environment.TickCount64;
                }
            }
        }
        finally
        {
            if (heldKey!=0) try { Native.SendKey(heldKey, true); }
            catch (Exception ex) { Report($"F-key release failed: {ex.Message}"); }
            if (timerRaised) Native.timeEndPeriod(1);
            Report($"F-key cycle stopped ({stopReason}) after {completedCycles} passes.");
        }
    }

    private async Task MoveClickRestore(Settings config, Native.Point original, nint foreground, CancellationToken token)
    {
        bool moved=false, leftDown=false;
        try
        {
            if (Native.GetForegroundWindow()!=foreground) return;
            var target=config.TargetX.HasValue ? new Native.Point(config.TargetX.Value, config.TargetY!.Value) : Native.LogitechTarget();
            if (!Native.SetCursorPos(target.X, target.Y)) throw new InvalidOperationException("Could not move cursor.");
            moved=true;
            if (Native.GetForegroundWindow()!=foreground) return;
            Native.SendMouse(Native.LeftDown); leftDown=true;
            Native.SendMouse(Native.LeftUp); leftDown=false;
            await Task.Delay(Timeout.Infinite, token);
        }
        finally
        {
            if (leftDown) try { Native.SendMouse(Native.LeftUp); } catch (Exception ex) { Report($"Left button release failed: {ex.Message}"); }
            if (moved && !Native.SetCursorPos(original.X, original.Y)) Report("Could not restore cursor position.");
        }
    }

    private void AutoClick(Settings config, nint foreground, CancellationToken token)
    {
        bool syntheticAlt=false, leftDown=false;
        bool timerRaised=Native.timeBeginPeriod(1)==0;
        int completedClicks=0;
        string stopReason="button released or macro disabled";
        try
        {
            Report("Alt + AutoClick started; no click sent yet.");
            while (true)
            {
                token.ThrowIfCancellationRequested();
                if (Native.GetForegroundWindow()!=foreground) { stopReason="focus changed"; return; }
                // G HUB's recorded sequence presses Alt once, clicks 17 times,
                // then releases Alt. Repeat While Holding repeats that sequence.
                if (!Native.AltPhysicallyDown()) { Native.SendKey(Native.VkLAlt, false); syntheticAlt=true; }
                Wait(config.BetweenClicksMs, token);
                for (int click=0; click<config.ClicksPerCycle; click++)
                {
                    token.ThrowIfCancellationRequested();
                    if (Native.GetForegroundWindow()!=foreground) { stopReason="focus changed"; return; }
                    Native.SendMouse(Native.LeftDown); leftDown=true;
                    Wait(config.DownUpMs, token);
                    Native.SendMouse(Native.LeftUp); leftDown=false;
                    completedClicks++;
                    if (completedClicks==1) Report("Alt + AutoClick sent its first click.");
                    Wait(config.BetweenClicksMs, token);
                }
                if (syntheticAlt) { Native.SendKey(Native.VkLAlt, true); syntheticAlt=false; }
                Report($"Alt + AutoClick running: {completedClicks} clicks sent.");
            }
        }
        finally
        {
            if (leftDown) try { Native.SendMouse(Native.LeftUp); } catch (Exception ex) { Report($"Left button release failed: {ex.Message}"); }
            if (syntheticAlt) try { Native.SendKey(Native.VkLAlt, true); } catch (Exception ex) { Report($"Alt release failed: {ex.Message}"); }
            if (timerRaised) Native.timeEndPeriod(1);
            Report($"Alt + AutoClick stopped ({stopReason}): {completedClicks} clicks sent.");
        }
    }

    private static void Wait(int milliseconds, CancellationToken token)
    {
        if (token.WaitHandle.WaitOne(milliseconds)) token.ThrowIfCancellationRequested();
    }

    internal void Apply(Settings updated)
    {
        updated.Validate();
        lock(sync) { StopAllLocked(); settings=updated; }
        Report(updated.Enabled ? "Enabled" : "Disabled");
    }
    internal void Disable()
    {
        lock(sync) { StopAllLocked(); settings=settings with { Enabled=false }; }
        Report("Disabled (panic)");
    }
    private void StopMouseLocked() => cancellation?.Cancel();
    private void StopAllLocked()
    {
        StopMouseLocked();
        keyboardCancellation?.Cancel();
        // The task owns and disposes its token source after releasing synthetic input.
    }
    private void Report(string message) => Status?.Invoke(message);
    public void Dispose()
    {
        Task? task, keyboardTask;
        lock(sync) { disposed=true; StopAllLocked(); task=running; keyboardTask=keyboardRunning; }
        try { Task.WaitAll([task ?? Task.CompletedTask, keyboardTask ?? Task.CompletedTask], TimeSpan.FromSeconds(2)); }
        catch (AggregateException) { }
    }
}
