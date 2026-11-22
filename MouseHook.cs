using System.ComponentModel;
using System.Runtime.InteropServices;

namespace WindowsMacroManager;

internal sealed class MouseHook : IDisposable
{
    private readonly Native.HookProc callback;
    private nint handle;
    internal event Action<int, bool, Native.Point>? Button;
    internal Func<int, bool>? Suppress;
    internal MouseHook()
    {
        callback=OnHook;
        handle=Native.SetWindowsHookEx(Native.WhMouseLl, callback, 0, 0);
        if (handle==0) throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not install mouse hook");
    }
    private nint OnHook(int code, nuint message, nint data)
    {
        if (code>=0 && ((int)message==Native.WmXDown || (int)message==Native.WmXUp))
        {
            var info=Marshal.PtrToStructure<Native.MouseHookData>(data);
            if ((info.Flags & Native.LlInjected)==0 && info.ExtraInfo!=Native.Marker)
            {
                int button=(int)((info.MouseData >> 16) & 0xffff);
                if (button is 1 or 2)
                {
                    try { Button?.Invoke(button, (int)message==Native.WmXDown, info.Point); }
                    catch { /* never unwind through a native callback */ }
                    if (Suppress?.Invoke(button)==true) return 1;
                }
            }
        }
        return Native.CallNextHookEx(handle, code, message, data);
    }
    public void Dispose() { if (handle!=0) { Native.UnhookWindowsHookEx(handle); handle=0; } }
}
