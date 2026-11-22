using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;

namespace WindowsMacroManager;

internal sealed class MainForm : Form
{
    private const int HotkeyCapture=1, HotkeyPanic=2, WmHotkey=0x0312;
    private readonly ComboBox mouse4=new(), mouse5=new();
    private readonly NumericUpDown x=new(), y=new(), downUp=new(), between=new(), clicks=new();
    private readonly CheckBox enabled=new(), suppress=new();
    private readonly Label status=new(), targetHint=new(), lastButton=new();
    private readonly Button capture=new();
    private readonly NotifyIcon tray=new();
    private readonly MouseHook? hook;
    private readonly MacroEngine engine;
    private Settings settings;
    private bool capturing, exiting;

    internal MainForm()
    {
        Text="Windows Macro Manager"; Width=480; Height=480; MinimumSize=new Size(460, 440);
        StartPosition=FormStartPosition.CenterScreen; KeyPreview=true;
        settings=SettingsStore.Load(out var warning);
        engine=new MacroEngine(settings);
        engine.Status+=message => { if (!IsDisposed && IsHandleCreated) BeginInvoke(() => status.Text=message); };
        var panel=new TableLayoutPanel { Dock=DockStyle.Fill, Padding=new Padding(12), ColumnCount=2, RowCount=10, AutoScroll=true };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45)); panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        Controls.Add(panel);
        Add(panel, 0, "Enabled", enabled); enabled.Checked=settings.Enabled;
        ConfigureCombo(mouse4, settings.Mouse4); ConfigureCombo(mouse5, settings.Mouse5);
        Add(panel, 1, "Mouse 4 / XBUTTON1", mouse4); Add(panel, 2, "Mouse 5 / XBUTTON2", mouse5);
        ConfigureCoordinate(x, settings.TargetX ?? Native.LogitechTarget().X);
        ConfigureCoordinate(y, settings.TargetY ?? Native.LogitechTarget().Y);
        Add(panel, 3, "Target X (pixels)", x); Add(panel, 4, "Target Y (pixels)", y);
        targetHint.Text=settings.TargetX.HasValue ? "Saved desktop pixel position" : "Default converted from Logitech coordinates";
        targetHint.AutoSize=true; panel.Controls.Add(targetHint, 0, 5); panel.SetColumnSpan(targetHint, 2);
        capture.Text="Capture position (F8)"; capture.AutoSize=true; capture.Click+=(_, _) => ToggleCapture();
        panel.Controls.Add(capture, 0, 6); panel.SetColumnSpan(capture, 2);
        ConfigureDelay(downUp, settings.DownUpMs); ConfigureDelay(between, settings.BetweenClicksMs);
        clicks.Minimum=1; clicks.Maximum=100; clicks.Value=settings.ClicksPerCycle;
        Add(panel, 7, "Down → up (ms)", downUp); Add(panel, 8, "Between clicks (ms)", between);
        Add(panel, 9, "Clicks per Alt cycle", clicks);
        Add(panel, 10, "Suppress Mouse 4/5", suppress); suppress.Checked=settings.SuppressButtons;
        var buttons=new FlowLayoutPanel { AutoSize=true, Dock=DockStyle.Fill };
        var save=new Button { Text="Save / Apply", AutoSize=true }; save.Click+=(_, _) => Save();
        var prototype=new Button { Text="Use PowerShell setup", AutoSize=true };
        prototype.Click+=(_, _) =>
        {
            mouse4.SelectedIndex=(int)MacroKind.AltAutoClick;
            mouse5.SelectedIndex=(int)MacroKind.MoveClickRestore;
            var target=Native.LogitechTarget();
            x.Value=target.X; y.Value=target.Y;
            targetHint.Text="Default target converted from Logitech coordinates";
            enabled.Checked=true;
            Save();
        };
        var panic=new Button { Text="Panic Disable", AutoSize=true }; panic.Click+=(_, _) => Panic();
        buttons.Controls.Add(save); buttons.Controls.Add(prototype); buttons.Controls.Add(panic); panel.Controls.Add(buttons, 0, 11); panel.SetColumnSpan(buttons, 2);
        status.AutoSize=true; status.Text=warning ?? (settings.Enabled ? "Enabled" : "Disabled");
        panel.Controls.Add(status, 0, 12); panel.SetColumnSpan(status, 2);
        lastButton.AutoSize=true; lastButton.Text="Last side button: none";
        panel.Controls.Add(lastButton, 0, 13); panel.SetColumnSpan(lastButton, 2);
        panel.RowCount=14;
        var menu=new ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => { Show(); WindowState=FormWindowState.Normal; Activate(); });
        menu.Items.Add("Enable / Disable", null, (_, _) => { enabled.Checked=!settings.Enabled; Save(); });
        menu.Items.Add("Exit", null, (_, _) => Exit());
        tray.Icon=SystemIcons.Application; tray.Text="Windows Macro Manager"; tray.ContextMenuStrip=menu; tray.Visible=true;
        tray.DoubleClick+=(_, _) => { Show(); WindowState=FormWindowState.Normal; Activate(); };
        try
        {
            hook=new MouseHook();
            hook.Button+=engine.HandleButton;
            hook.Button+=(button, down, _) =>
            {
                if (down && IsHandleCreated)
                    BeginInvoke(() => lastButton.Text=$"Last side button: XBUTTON{button} → {(button==1 ? settings.Mouse4 : settings.Mouse5)}");
            };
            hook.Suppress=engine.ShouldSuppress;
        }
        catch (Exception ex) { settings=settings with { Enabled=false }; engine.Disable(); status.Text=$"Mouse hook failed: {ex.Message}"; }
        if (!Native.RegisterHotKey(Handle, HotkeyPanic, 0x0002|0x0004, Native.VkF12)) status.Text += " | Ctrl+Shift+F12 unavailable";
    }

    private static void Add(TableLayoutPanel panel, int row, string label, Control control)
    {
        panel.Controls.Add(new Label { Text=label, AutoSize=true, Anchor=AnchorStyles.Left }, 0, row);
        control.Dock=DockStyle.Fill; panel.Controls.Add(control, 1, row);
    }
    private static void ConfigureCombo(ComboBox combo, MacroKind value)
    {
        combo.DropDownStyle=ComboBoxStyle.DropDownList;
        combo.Items.AddRange(["None", "Move + Click + Restore", "Alt + AutoClick"]);
        combo.SelectedIndex=(int)value;
    }
    private static void ConfigureCoordinate(NumericUpDown box, int value)
    { box.Minimum=int.MinValue; box.Maximum=int.MaxValue; box.Value=value; }
    private static void ConfigureDelay(NumericUpDown box, int value)
    { box.Minimum=1; box.Maximum=10000; box.Value=value; }

    private void Save()
    {
        try
        {
            var next=new Settings { Enabled=enabled.Checked, Mouse4=(MacroKind)mouse4.SelectedIndex, Mouse5=(MacroKind)mouse5.SelectedIndex,
                TargetX=(int)x.Value, TargetY=(int)y.Value, DownUpMs=(int)downUp.Value, BetweenClicksMs=(int)between.Value,
                ClicksPerCycle=(int)clicks.Value, SuppressButtons=suppress.Checked };
            next.Validate(); SettingsStore.Save(next); settings=next; engine.Apply(next);
            status.Text="Settings saved. " + (next.Enabled ? "Enabled" : "Disabled");
        }
        catch (Exception ex) { status.Text=$"Save failed: {ex.Message}"; }
    }
    private void Panic()
    {
        engine.Disable(); settings=settings with { Enabled=false }; enabled.Checked=false;
        try { SettingsStore.Save(settings); } catch (Exception ex) { status.Text=$"Disabled; save failed: {ex.Message}"; return; }
        status.Text="Disabled (panic)";
    }
    private void ToggleCapture()
    {
        if (capturing) { EndCapture(); return; }
        if (!Native.RegisterHotKey(Handle, HotkeyCapture, 0, Native.VkF8)) { status.Text="F8 unavailable for capture."; return; }
        capturing=true; capture.Text="Cancel capture (Esc)"; status.Text="Move pointer to target, then press F8. Esc cancels.";
    }
    private void EndCapture()
    {
        if (capturing) Native.UnregisterHotKey(Handle, HotkeyCapture);
        capturing=false; capture.Text="Capture position (F8)";
    }
    protected override void WndProc(ref Message message)
    {
        if (message.Msg==WmHotkey)
        {
            if (message.WParam==(nint)HotkeyPanic) { Panic(); return; }
            if (message.WParam==(nint)HotkeyCapture && capturing)
            {
                if (Native.GetCursorPos(out var point)) { x.Value=point.X; y.Value=point.Y; targetHint.Text="Captured; click Save / Apply to persist."; status.Text=$"Captured ({point.X}, {point.Y})"; }
                else status.Text="Could not read cursor position.";
                EndCapture(); return;
            }
        }
        base.WndProc(ref message);
    }
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (capturing && keyData==Keys.Escape) { EndCapture(); status.Text="Capture canceled."; return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!exiting && e.CloseReason==CloseReason.UserClosing) { e.Cancel=true; Hide(); return; }
        base.OnFormClosing(e);
    }
    private void Exit() { exiting=true; Close(); }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { EndCapture(); Native.UnregisterHotKey(Handle, HotkeyPanic); hook?.Dispose(); engine.Dispose(); tray.Visible=false; tray.Dispose(); }
        base.Dispose(disposing);
    }
}
