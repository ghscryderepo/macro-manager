namespace WindowsMacroManager;

internal sealed class MacroEngine : IDisposable
{
    private readonly object sync=new();
    private Settings settings;
    private int activeButton;
    private CancellationTokenSource? cancellation;
    private Task? running;
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
                if (activeButton==button) StopLocked();
                return;
            }
            if (!settings.Enabled || activeButton!=0) return;
            var kind=button==1 ? settings.Mouse4 : settings.Mouse5;
            if (kind==MacroKind.None) return;
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
                    else await AutoClick(current, foreground, token);
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

    private async Task AutoClick(Settings config, nint foreground, CancellationToken token)
    {
        bool syntheticAlt=false, leftDown=false;
        try
        {
            token.ThrowIfCancellationRequested();
            if (Native.GetForegroundWindow()!=foreground) return;
            if (!Native.AltPhysicallyDown()) { Native.SendKey(Native.VkLAlt, false); syntheticAlt=true; }
            while (true)
            {
                token.ThrowIfCancellationRequested();
                if (Native.GetForegroundWindow()!=foreground) { Report("Focus changed; macro stopped."); return; }
                Native.SendMouse(Native.LeftDown); leftDown=true;
                await Task.Delay(config.DownUpMs, token);
                Native.SendMouse(Native.LeftUp); leftDown=false;
                await Task.Delay(config.BetweenClicksMs, token);
            }
        }
        finally
        {
            if (leftDown) try { Native.SendMouse(Native.LeftUp); } catch (Exception ex) { Report($"Left button release failed: {ex.Message}"); }
            if (syntheticAlt) try { Native.SendKey(Native.VkLAlt, true); } catch (Exception ex) { Report($"Alt release failed: {ex.Message}"); }
        }
    }

    internal void Apply(Settings updated)
    {
        updated.Validate();
        lock(sync) { StopLocked(); settings=updated; }
        Report(updated.Enabled ? "Enabled" : "Disabled");
    }
    internal void Disable()
    {
        lock(sync) { StopLocked(); settings=settings with { Enabled=false }; }
        Report("Disabled (panic)");
    }
    private void StopLocked()
    {
        cancellation?.Cancel();
        // The task owns and disposes its token source after releasing synthetic input.
    }
    private void Report(string message) => Status?.Invoke(message);
    public void Dispose()
    {
        Task? task;
        lock(sync) { disposed=true; StopLocked(); task=running; }
        try { task?.Wait(TimeSpan.FromSeconds(2)); } catch (AggregateException) { }
    }
}
