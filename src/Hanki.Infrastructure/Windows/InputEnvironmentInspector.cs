using System.Diagnostics;
using System.Runtime.InteropServices;
using Hanki.Core.Diagnostics;

namespace Hanki.Infrastructure.Windows;

public sealed class InputEnvironmentInspector
{
    private const int VkCapital = 0x14;
    private const int VkNumLock = 0x90;
    private const uint GcsCompStr = 0x0008;

    public InputEnvironmentState Capture(DelimiterKey? delimiter = null)
    {
        var window = GetForegroundWindow();
        var threadId = window == IntPtr.Zero ? 0 : GetWindowThreadProcessId(window, out _);
        var layout = GetKeyboardLayout(threadId);
        var layoutCode = unchecked((uint)layout.ToInt64()).ToString("X8");
        var ime = GetImeState(window);
        return new InputEnvironmentState(
            layoutCode,
            ime.Open,
            ime.Composing,
            (GetKeyState(VkCapital) & 1) != 0,
            (GetKeyState(VkNumLock) & 1) != 0,
            Process.GetCurrentProcess().SessionId,
            delimiter);
    }

    private static (bool? Open, bool? Composing) GetImeState(IntPtr window)
    {
        if (window == IntPtr.Zero)
            return (null, null);
        var context = ImmGetContext(window);
        if (context == IntPtr.Zero)
            return (null, null);
        try
        {
            var open = ImmGetOpenStatus(context);
            var compositionBytes = ImmGetCompositionString(context, GcsCompStr, IntPtr.Zero, 0);
            return (open, compositionBytes > 0);
        }
        finally
        {
            _ = ImmReleaseContext(window, context);
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(uint threadId);

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int virtualKey);

    [DllImport("imm32.dll")]
    private static extern IntPtr ImmGetContext(IntPtr window);

    [DllImport("imm32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ImmGetOpenStatus(IntPtr inputContext);

    [DllImport("imm32.dll", CharSet = CharSet.Unicode)]
    private static extern int ImmGetCompositionString(
        IntPtr inputContext,
        uint index,
        IntPtr buffer,
        uint bufferLength);

    [DllImport("imm32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ImmReleaseContext(IntPtr window, IntPtr inputContext);
}
