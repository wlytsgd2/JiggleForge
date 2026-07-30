using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

internal sealed class MouseWheelHook : IDisposable
{
    private WheelBridgeConfig config;
    private readonly KeyPulseQueue pulses;
    private readonly NativeMethods.LowLevelMouseProc callback;
    private IntPtr hook;
    private int wheelRemainder;

    internal MouseWheelHook(WheelBridgeConfig config, KeyPulseQueue pulses)
    {
        this.config = config;
        this.pulses = pulses;
        callback = HookCallback;
        hook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_MOUSE_LL,
            callback,
            NativeMethods.GetModuleHandle(null),
            0);
        if (hook == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    private IntPtr HookCallback(int code, IntPtr message, IntPtr data)
    {
        if (code >= 0 && message.ToInt32() == NativeMethods.WM_MOUSEWHEEL)
        {
            config = config.ReloadIfChanged();
            bool dragKeyHeld = config.DragVirtualKeys.Exists(
                key => (NativeMethods.GetAsyncKeyState(key) & 0x8000) != 0);
            if ((!config.RequireDragButton || dragKeyHeld) && config.AllowsForegroundProcess())
            {
                NativeMethods.MSLLHOOKSTRUCT input =
                    (NativeMethods.MSLLHOOKSTRUCT)Marshal.PtrToStructure(data, typeof(NativeMethods.MSLLHOOKSTRUCT));
                int delta = (short)((input.mouseData >> 16) & 0xffff);
                wheelRemainder += delta;
                while (wheelRemainder >= NativeMethods.WHEEL_DELTA)
                {
                    pulses.Enqueue(NativeMethods.VK_F13);
                    wheelRemainder -= NativeMethods.WHEEL_DELTA;
                }
                while (wheelRemainder <= -NativeMethods.WHEEL_DELTA)
                {
                    pulses.Enqueue(NativeMethods.VK_F14);
                    wheelRemainder += NativeMethods.WHEEL_DELTA;
                }

                if (config.BlockWheelWhileDragging)
                {
                    return new IntPtr(1);
                }
            }
            else
            {
                wheelRemainder = 0;
            }
        }

        return NativeMethods.CallNextHookEx(hook, code, message, data);
    }

    public void Dispose()
    {
        if (hook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(hook);
            hook = IntPtr.Zero;
        }
    }
}
