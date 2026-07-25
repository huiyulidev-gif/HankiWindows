using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Hanki.Infrastructure.Windows;

public sealed class GlobalKeyboardHook : IDisposable
{
    public const nuint InjectionMarker = 0x48414E4B;
    private const int WhKeyboardLl = 13;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyUp = 0x0105;
    private const uint VkSpace = 0x20;

    private readonly HookProc _callback;
    private IntPtr _hook;

    public GlobalKeyboardHook() => _callback = OnHook;

    public event EventHandler? SpacePressed;

    public void Start()
    {
        if (_hook != IntPtr.Zero)
            return;
        _hook = SetWindowsHookEx(WhKeyboardLl, _callback, GetModuleHandle(null), 0);
        if (_hook == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "전역 키보드 감지를 시작하지 못했습니다.");
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
        GC.SuppressFinalize(this);
    }

    private IntPtr OnHook(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && (wParam == (IntPtr)WmKeyUp || wParam == (IntPtr)WmSysKeyUp))
        {
            var data = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
            if (data.VirtualKeyCode == VkSpace && data.ExtraInfo != InjectionMarker)
                SpacePressed?.Invoke(this, EventArgs.Empty);
        }
        return CallNextHookEx(_hook, code, wParam, lParam);
    }

    private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct KbdLlHookStruct
    {
        public readonly uint VirtualKeyCode;
        public readonly uint ScanCode;
        public readonly uint Flags;
        public readonly uint Time;
        public readonly nuint ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc callback, IntPtr module, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? moduleName);
}
