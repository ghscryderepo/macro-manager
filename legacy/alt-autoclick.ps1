Add-Type @'
using System;
using System.Runtime.InteropServices;
using System.Threading;

public class MouseMacro
{
    [DllImport("user32.dll")]
    static extern short GetAsyncKeyState(int key);

    [DllImport("user32.dll")]
    static extern void keybd_event(
        byte key, byte scan, uint flags, UIntPtr extra
    );

    [DllImport("user32.dll")]
    static extern void mouse_event(
        uint flags, uint dx, uint dy, uint data, UIntPtr extra
    );

    const int VK_XBUTTON2 = 0x06;
    const byte VK_MENU = 0x12;

    const uint KEYUP = 0x0002;
    const uint LEFTDOWN = 0x0002;
    const uint LEFTUP = 0x0004;

    static bool IsPressed()
    {
        return (GetAsyncKeyState(VK_XBUTTON2) & 0x8000) != 0;
    }

    static void Click()
    {
        mouse_event(LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(1);
        mouse_event(LEFTUP, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(1);
    }

    public static void Run()
    {
        Console.WriteLine("Mouse 5 = Alt + AutoClick");
        Console.WriteLine("Ctrl+C to stop");

        try
        {
            while (true)
            {
                if (IsPressed())
                {
                    keybd_event(VK_MENU, 0, 0, UIntPtr.Zero);

                    try
                    {
                        while (IsPressed())
                        {
                            Click();
                        }
                    }
                    finally
                    {
                        keybd_event(VK_MENU, 0, KEYUP, UIntPtr.Zero);
                    }
                }

                Thread.Sleep(1);
            }
        }
        finally
        {
            keybd_event(VK_MENU, 0, KEYUP, UIntPtr.Zero);
        }
    }
}
'@

[MouseMacro]::Run()
