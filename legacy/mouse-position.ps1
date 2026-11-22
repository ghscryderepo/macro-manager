Add-Type @'
using System;
using System.Runtime.InteropServices;

public static class MouseNative
{
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int key);

    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out POINT point);

    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    public static extern void mouse_event(
        uint flags, uint dx, uint dy,
        uint data, UIntPtr extraInfo
    );

    public const int VK_XBUTTON2 = 0x06;
    public const uint LEFTDOWN = 0x0002;
    public const uint LEFTUP = 0x0004;

    public static void Click()
    {
        mouse_event(LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
        mouse_event(LEFTUP, 0, 0, 0, UIntPtr.Zero);
    }
}
'@

# Logitech uses normalized coordinates 0..65535
$targetX = 37364
$targetY = 48092

Add-Type -AssemblyName System.Windows.Forms

$screen = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds

$x = [int][Math]::Round($targetX * ($screen.Width - 1) / 65535)
$y = [int][Math]::Round($targetY * ($screen.Height - 1) / 65535)

$pressed = $false
$saved = New-Object MouseNative+POINT

Write-Host "Running. Press Ctrl+C to stop."

while ($true) {
    $down = ([MouseNative]::GetAsyncKeyState(0x06) -band 0x8000) -ne 0

    if ($down -and -not $pressed) {
        [MouseNative]::GetCursorPos([ref]$saved) | Out-Null
        [MouseNative]::SetCursorPos($x, $y) | Out-Null
        [MouseNative]::Click()
        $pressed = $true
    }

    if (-not $down -and $pressed) {
        [MouseNative]::SetCursorPos($saved.X, $saved.Y) | Out-Null
        $pressed = $false
    }

    Start-Sleep -Milliseconds 5
}
