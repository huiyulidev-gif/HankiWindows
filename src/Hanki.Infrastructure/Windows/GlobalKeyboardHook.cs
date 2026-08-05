using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Hanki.Infrastructure.Windows;

[Flags]
public enum HookModifierKeys
{
    None = 0,
    Shift = 1,
    Control = 2,
    Alt = 4,
    Windows = 8
}

public sealed record HookKeyEvent(
    DateTimeOffset OccurredAtUtc,
    uint VirtualKeyCode,
    uint ScanCode,
    bool IsKeyDown,
    bool IsExtended,
    bool IsInjected,
    bool IsHankiInjected,
    HookModifierKeys Modifiers);

public static class HookEventPolicy
{
    private const uint VkTab = 0x09;
    private const uint VkReturn = 0x0D;
    private const uint VkSpace = 0x20;

    public static bool TryGetDelimiter(HookKeyEvent keyEvent, out Hanki.Core.Diagnostics.DelimiterKey delimiter)
    {
        delimiter = default;
        if (keyEvent.IsKeyDown ||
            keyEvent.IsHankiInjected ||
            (keyEvent.Modifiers &
             (HookModifierKeys.Control | HookModifierKeys.Alt | HookModifierKeys.Windows)) != 0)
        {
            return false;
        }

        switch (keyEvent.VirtualKeyCode)
        {
            case VkSpace:
                delimiter = Hanki.Core.Diagnostics.DelimiterKey.Space;
                return true;
            case VkReturn when keyEvent.IsExtended:
                delimiter = Hanki.Core.Diagnostics.DelimiterKey.NumpadEnter;
                return true;
            case VkReturn:
                delimiter = Hanki.Core.Diagnostics.DelimiterKey.Enter;
                return true;
            case VkTab:
                delimiter = Hanki.Core.Diagnostics.DelimiterKey.Tab;
                return true;
            default:
                return false;
        }
    }
}

public sealed class GlobalKeyboardHook : IDisposable
{
    public const nuint InjectionMarker = 0x48414E4B;
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const uint WmQuit = 0x0012;
    private const uint LlkhfExtended = 0x01;
    private const uint LlkhfInjected = 0x10;
    private const int VkShift = 0x10;
    private const int VkControl = 0x11;
    private const int VkMenu = 0x12;
    private const int VkLWin = 0x5B;
    private const int VkRWin = 0x5C;

    private readonly HookProc _callback;
    private readonly object _lifecycleLock = new();
    private Thread? _thread;
    private IntPtr _hook;
    private uint _threadId;
    private bool _disposed;
    private Exception? _registrationException;

    public GlobalKeyboardHook() => _callback = OnHook;

    public event EventHandler<HookKeyEvent>? KeyEventReceived;

    public bool IsRegistered => Volatile.Read(ref _hook) != IntPtr.Zero;
    public bool IsThreadAlive => _thread?.IsAlive == true;
    public long HookHandleValue => Volatile.Read(ref _hook).ToInt64();
    public int ThreadId => unchecked((int)Volatile.Read(ref _threadId));
    public DateTimeOffset? RegisteredAtUtc { get; private set; }
    public DateTimeOffset? LastCallbackAtUtc { get; private set; }

    public void Start()
    {
        lock (_lifecycleLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (IsRegistered && IsThreadAlive)
                return;

            StopCore();
            using var ready = new ManualResetEventSlim(false);
            _registrationException = null;
            _thread = new Thread(() => RunMessageLoop(ready))
            {
                IsBackground = true,
                Name = "Hanki.KeyboardHook"
            };
            _thread.Start();

            if (!ready.Wait(TimeSpan.FromSeconds(5)))
            {
                StopCore();
                throw new TimeoutException("전역 키보드 감지 스레드가 준비되지 않았습니다.");
            }

            if (_registrationException is not null)
            {
                var exception = _registrationException;
                StopCore();
                throw exception;
            }

            if (!IsRegistered)
            {
                StopCore();
                throw new InvalidOperationException("전역 키보드 감지 핸들이 만들어지지 않았습니다.");
            }
        }
    }

    public void Restart()
    {
        lock (_lifecycleLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            StopCore();
            Start();
        }
    }

    public void Dispose()
    {
        lock (_lifecycleLock)
        {
            if (_disposed)
                return;
            _disposed = true;
            StopCore();
        }
        GC.SuppressFinalize(this);
    }

    private void RunMessageLoop(ManualResetEventSlim ready)
    {
        _threadId = GetCurrentThreadId();
        try
        {
            var hook = SetWindowsHookEx(WhKeyboardLl, _callback, GetModuleHandle(null), 0);
            if (hook == IntPtr.Zero)
            {
                _registrationException = new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "전역 키보드 감지를 시작하지 못했습니다.");
                return;
            }

            Volatile.Write(ref _hook, hook);
            RegisteredAtUtc = DateTimeOffset.UtcNow;
            ready.Set();

            while (GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
            {
                _ = TranslateMessage(ref message);
                _ = DispatchMessage(ref message);
            }
        }
        catch (Exception exception)
        {
            _registrationException ??= exception;
        }
        finally
        {
            var hook = Interlocked.Exchange(ref _hook, IntPtr.Zero);
            if (hook != IntPtr.Zero)
                _ = UnhookWindowsHookEx(hook);
            _threadId = 0;
            ready.Set();
        }
    }

    private void StopCore()
    {
        var thread = _thread;
        var threadId = Volatile.Read(ref _threadId);
        if (threadId != 0)
            _ = PostThreadMessage(threadId, WmQuit, IntPtr.Zero, IntPtr.Zero);

        if (thread is { IsAlive: true } && thread != Thread.CurrentThread)
            _ = thread.Join(TimeSpan.FromSeconds(2));

        var hook = Interlocked.Exchange(ref _hook, IntPtr.Zero);
        if (hook != IntPtr.Zero)
            _ = UnhookWindowsHookEx(hook);
        _thread = null;
        _threadId = 0;
        RegisteredAtUtc = null;
    }

    private IntPtr OnHook(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 &&
            (wParam == (IntPtr)WmKeyDown ||
             wParam == (IntPtr)WmKeyUp ||
             wParam == (IntPtr)WmSysKeyDown ||
             wParam == (IntPtr)WmSysKeyUp))
        {
            LastCallbackAtUtc = DateTimeOffset.UtcNow;
            var data = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
            var hankiInjected = data.ExtraInfo == InjectionMarker;
            var injected = hankiInjected || (data.Flags & LlkhfInjected) != 0;
            var keyEvent = new HookKeyEvent(
                LastCallbackAtUtc.Value,
                data.VirtualKeyCode,
                data.ScanCode,
                wParam == (IntPtr)WmKeyDown || wParam == (IntPtr)WmSysKeyDown,
                (data.Flags & LlkhfExtended) != 0,
                injected,
                hankiInjected,
                GetModifiers());
            KeyEventReceived?.Invoke(this, keyEvent);
        }
        return CallNextHookEx(Volatile.Read(ref _hook), code, wParam, lParam);
    }

    private static HookModifierKeys GetModifiers()
    {
        var modifiers = HookModifierKeys.None;
        if ((GetAsyncKeyState(VkShift) & 0x8000) != 0)
            modifiers |= HookModifierKeys.Shift;
        if ((GetAsyncKeyState(VkControl) & 0x8000) != 0)
            modifiers |= HookModifierKeys.Control;
        if ((GetAsyncKeyState(VkMenu) & 0x8000) != 0)
            modifiers |= HookModifierKeys.Alt;
        if ((GetAsyncKeyState(VkLWin) & 0x8000) != 0 || (GetAsyncKeyState(VkRWin) & 0x8000) != 0)
            modifiers |= HookModifierKeys.Windows;
        return modifiers;
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

    [StructLayout(LayoutKind.Sequential)]
    private struct Message
    {
        public IntPtr Window;
        public uint Value;
        public nuint WParam;
        public nint LParam;
        public uint Time;
        public Point Point;
        public uint Private;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
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

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostThreadMessage(uint threadId, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int GetMessage(out Message message, IntPtr window, uint min, uint max);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TranslateMessage([In] ref Message message);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage([In] ref Message message);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);
}
