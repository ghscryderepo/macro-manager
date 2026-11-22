using System.Runtime.InteropServices;

namespace WindowsMacroManager;

internal static class Native
{
    internal const int WhMouseLl = 14, WmXDown = 0x020B, WmXUp = 0x020C;
    internal const int WmKeyDown = 0x0100, WmSysKeyDown = 0x0104;
    internal const int VkF8 = 0x77, VkEscape = 0x1B, VkF12 = 0x7B;
    internal const int VkControl = 0x11, VkShift = 0x10, VkLAlt = 0xA4;
    internal const int SmXVirtual = 76, SmYVirtual = 77, SmCxVirtual = 78, SmCyVirtual = 79;
    internal const uint InputMouse = 0, InputKeyboard = 1;
    internal const uint LeftDown = 0x0002, LeftUp = 0x0004, KeyUp = 0x0002;
    internal const uint LlInjected = 0x00000001;
    internal static readonly nuint Marker = (nuint)0x574D4D31;

    [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; public Point(int x, int y) { X=x; Y=y; } }
    [StructLayout(LayoutKind.Sequential)] internal struct MouseHookData { public Point Point; public uint MouseData, Flags, Time; public nuint ExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] internal struct Input { public uint Type; public InputUnion Data; }
    [StructLayout(LayoutKind.Explicit)] internal struct InputUnion { [FieldOffset(0)] public MouseInput Mouse; [FieldOffset(0)] public KeyboardInput Key; }
    [StructLayout(LayoutKind.Sequential)] internal struct MouseInput { public int Dx, Dy; public uint MouseData, Flags, Time; public nuint ExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] internal struct KeyboardInput { public ushort Vk, Scan; public uint Flags, Time; public nuint ExtraInfo; }
    internal delegate nint HookProc(int code, nuint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError=true)] internal static extern nint SetWindowsHookEx(int id, HookProc proc, nint module, uint threadId);
    [DllImport("user32.dll", SetLastError=true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] internal static extern nint CallNextHookEx(nint hook, int code, nuint wParam, nint lParam);
    [DllImport("user32.dll", SetLastError=true)] internal static extern uint SendInput(uint count, [In] Input[] inputs, int size);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] internal static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] internal static extern nint GetForegroundWindow();
    [DllImport("winmm.dll")] internal static extern uint timeBeginPeriod(uint period);
    [DllImport("winmm.dll")] internal static extern uint timeEndPeriod(uint period);
    [DllImport("user32.dll", SetLastError=true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool UnregisterHotKey(nint window, int id);

    internal static void SendMouse(uint flag) => Send(new Input { Type=InputMouse, Data=new InputUnion { Mouse=new MouseInput { Flags=flag, ExtraInfo=Marker } } });
    internal static void SendKey(ushort vk, bool up) => Send(new Input { Type=InputKeyboard, Data=new InputUnion { Key=new KeyboardInput { Vk=vk, Flags=up ? KeyUp : 0, ExtraInfo=Marker } } });
    private static void Send(Input input)
    {
        if (SendInput(1, [input], Marshal.SizeOf<Input>()) != 1)
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "SendInput failed");
    }

    internal static Point LogitechTarget() => new(
        GetSystemMetrics(SmXVirtual) + (int)Math.Round(37364d * (GetSystemMetrics(SmCxVirtual)-1) / 65535),
        GetSystemMetrics(SmYVirtual) + (int)Math.Round(48092d * (GetSystemMetrics(SmCyVirtual)-1) / 65535));
    internal static bool AltPhysicallyDown() => (GetAsyncKeyState(VkLAlt) & 0x8000) != 0;
}
