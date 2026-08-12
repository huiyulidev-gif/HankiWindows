using System.Runtime.InteropServices;
using Hanki.Core.Diagnostics;

namespace Hanki.Infrastructure.Windows;

public enum InputInjectionStage
{
    Backspace,
    Text,
    Delimiter,
    PasteShortcut
}

public sealed record InputSendResult(
    InputInjectionStage Stage,
    int RequestedInputs,
    int SentInputs,
    int Win32Error)
{
    public bool IsSuccess => RequestedInputs == SentInputs;
    public bool IsPartial => SentInputs > 0 && SentInputs < RequestedInputs;
}

public interface IInputSender
{
    InputSendResult SendBackspaces(int keyPressCount);
    InputSendResult SendUnicodeText(string text);
    InputSendResult SendDelimiter(DelimiterKey delimiter);
    InputSendResult SendPasteShortcut();
}

public sealed class WindowsInputSender : IInputSender
{
    private const uint InputKeyboard = 1;
    private const uint KeyEventFExtendedKey = 0x0001;
    private const uint KeyEventFKeyUp = 0x0002;
    private const uint KeyEventFUnicode = 0x0004;
    private const ushort VkBack = 0x08;
    private const ushort VkTab = 0x09;
    private const ushort VkReturn = 0x0D;
    private const ushort VkControl = 0x11;
    private const ushort VkSpace = 0x20;
    private const ushort VkV = 0x56;

    public InputSendResult SendBackspaces(int keyPressCount)
    {
        if (keyPressCount < 0)
            throw new ArgumentOutOfRangeException(nameof(keyPressCount));
        var inputs = new List<Input>(keyPressCount * 2);
        for (var index = 0; index < keyPressCount; index++)
        {
            inputs.Add(CreateVirtualKey(VkBack, keyUp: false));
            inputs.Add(CreateVirtualKey(VkBack, keyUp: true));
        }
        return Send(InputInjectionStage.Backspace, inputs);
    }

    public InputSendResult SendUnicodeText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var inputs = new List<Input>(text.Length * 2);
        foreach (var character in text)
        {
            inputs.Add(CreateUnicode(character, keyUp: false));
            inputs.Add(CreateUnicode(character, keyUp: true));
        }
        return Send(InputInjectionStage.Text, inputs);
    }

    public InputSendResult SendDelimiter(DelimiterKey delimiter)
    {
        var virtualKey = delimiter switch
        {
            DelimiterKey.Space => VkSpace,
            DelimiterKey.Enter or DelimiterKey.NumpadEnter => VkReturn,
            DelimiterKey.Tab => VkTab,
            _ => throw new ArgumentOutOfRangeException(nameof(delimiter))
        };
        var extended = delimiter == DelimiterKey.NumpadEnter;
        return Send(
            InputInjectionStage.Delimiter,
            [
                CreateVirtualKey(virtualKey, keyUp: false, extended),
                CreateVirtualKey(virtualKey, keyUp: true, extended)
            ]);
    }

    public InputSendResult SendPasteShortcut() =>
        Send(
            InputInjectionStage.PasteShortcut,
            [
                CreateVirtualKey(VkControl, keyUp: false),
                CreateVirtualKey(VkV, keyUp: false),
                CreateVirtualKey(VkV, keyUp: true),
                CreateVirtualKey(VkControl, keyUp: true)
            ]);

    private static InputSendResult Send(InputInjectionStage stage, IReadOnlyCollection<Input> inputCollection)
    {
        if (inputCollection.Count == 0)
            return new InputSendResult(stage, 0, 0, 0);
        var inputs = inputCollection.ToArray();
        Marshal.SetLastPInvokeError(0);
        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        var error = sent == inputs.Length ? 0 : Marshal.GetLastPInvokeError();
        return new InputSendResult(stage, inputs.Length, checked((int)sent), error);
    }

    private static Input CreateUnicode(char character, bool keyUp) => new()
    {
        Type = InputKeyboard,
        Data = new InputUnion
        {
            Keyboard = new KeyboardInput
            {
                VirtualKey = 0,
                ScanCode = character,
                Flags = KeyEventFUnicode | (keyUp ? KeyEventFKeyUp : 0),
                ExtraInfo = GlobalKeyboardHook.InjectionMarker
            }
        }
    };

    private static Input CreateVirtualKey(ushort virtualKey, bool keyUp, bool extended = false) => new()
    {
        Type = InputKeyboard,
        Data = new InputUnion
        {
            Keyboard = new KeyboardInput
            {
                VirtualKey = virtualKey,
                ScanCode = 0,
                Flags = (keyUp ? KeyEventFKeyUp : 0) | (extended ? KeyEventFExtendedKey : 0),
                ExtraInfo = GlobalKeyboardHook.InjectionMarker
            }
        }
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KeyboardInput Keyboard;
        [FieldOffset(0)] public MouseInput Mouse;
        [FieldOffset(0)] public HardwareInput Hardware;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HardwareInput
    {
        public uint Message;
        public ushort ParameterLow;
        public ushort ParameterHigh;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, Input[] inputs, int size);
}
