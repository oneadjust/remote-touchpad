using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace RemoteTouchpad.Input;

public sealed class KeyboardController
{
    private const int InputKeyboard = 1;
    private const uint KeyeventfKeyup = 0x0002;

    private const ushort VkEscape = 0x1B;
    private const ushort VkLeftWin = 0x5B;
    private const ushort VkMediaNextTrack = 0xB0;
    private const ushort VkMediaPreviousTrack = 0xB1;
    private const ushort VkMediaPlayPause = 0xB3;
    private const ushort VkVolumeMute = 0xAD;
    private const ushort VkVolumeDown = 0xAE;
    private const ushort VkVolumeUp = 0xAF;
    private const ushort VkOemPlus = 0xBB;
    private const ushort VkOemMinus = 0xBD;

    public void MediaPlayPause() => Press(VkMediaPlayPause);

    public void MediaPrevious() => Press(VkMediaPreviousTrack);

    public void MediaNext() => Press(VkMediaNextTrack);

    public void VolumeMute() => Press(VkVolumeMute, "volumeMute");

    public void VolumeDown() => Press(VkVolumeDown, "volumeDown");

    public void VolumeUp() => Press(VkVolumeUp, "volumeUp");

    public void ExecuteBinding(string binding, string action = "media")
    {
        AppLogger.Info($"Keyboard binding execute requested. Action={action}, Binding={binding}");
        switch (binding)
        {
            case MediaBinding.SystemPlayPause:
                MediaPlayPause();
                break;
            case MediaBinding.SystemPrevious:
                MediaPrevious();
                break;
            case MediaBinding.SystemNext:
                MediaNext();
                break;
            case MediaBinding.SystemVolumeDown:
                VolumeDown();
                break;
            case MediaBinding.SystemVolumeUp:
                VolumeUp();
                break;
            case var keyBinding when MediaBinding.TryParseKeyBinding(keyBinding, out var parsed):
                PressParsedKey(parsed, action);
                break;
            default:
                MediaPlayPause();
                break;
        }
    }

    public void StartMagnifier()
    {
        try
        {
            Process.Start(new ProcessStartInfo("magnify.exe") { UseShellExecute = true });
            AppLogger.Info("Magnifier process start requested.");
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to start magnify.exe.", ex);
        }
    }

    public void MagnifierOpenOrZoomIn() => PressChord(VkLeftWin, VkOemPlus);

    public void MagnifierZoomOut() => PressChord(VkLeftWin, VkOemMinus);

    public void MagnifierClose() => PressChord(VkLeftWin, VkEscape);

    private static void Press(ushort virtualKey, string action = "key")
    {
        AppLogger.Info($"Keyboard virtual key press. Action={action}, VirtualKey=0x{virtualKey:X2}");
        SendKeyboardInputs(
        [
            KeyDown(virtualKey),
            KeyUp(virtualKey)
        ], action);
    }

    private static void PressChord(ushort modifier, ushort virtualKey)
    {
        SendKeyboardInputs(
        [
            KeyDown(modifier),
            KeyDown(virtualKey),
            KeyUp(virtualKey),
            KeyUp(modifier)
        ], $"chord:0x{modifier:X2}+0x{virtualKey:X2}");
    }

    private static void PressParsedKey(ParsedKeyBinding parsed, string action)
    {
        var inputs = new List<Input>();
        if (parsed.Control)
        {
            inputs.Add(KeyDown((ushort)Keys.ControlKey));
        }

        if (parsed.Shift)
        {
            inputs.Add(KeyDown((ushort)Keys.ShiftKey));
        }

        if (parsed.Alt)
        {
            inputs.Add(KeyDown((ushort)Keys.Menu));
        }

        inputs.Add(KeyDown((ushort)parsed.Key));
        inputs.Add(KeyUp((ushort)parsed.Key));

        if (parsed.Alt)
        {
            inputs.Add(KeyUp((ushort)Keys.Menu));
        }

        if (parsed.Shift)
        {
            inputs.Add(KeyUp((ushort)Keys.ShiftKey));
        }

        if (parsed.Control)
        {
            inputs.Add(KeyUp((ushort)Keys.ControlKey));
        }

        SendKeyboardInputs([.. inputs], $"{action}:{parsed.DisplayName}");
    }

    private static Input KeyDown(ushort virtualKey)
    {
        return new Input
        {
            Type = InputKeyboard,
            Data = new InputUnion
            {
                Keyboard = new KeyboardInput
                {
                    VirtualKey = virtualKey
                }
            }
        };
    }

    private static Input KeyUp(ushort virtualKey)
    {
        var input = KeyDown(virtualKey);
        input.Data.Keyboard.Flags = KeyeventfKeyup;
        return input;
    }

    private static void SendKeyboardInputs(Input[] inputs, string action)
    {
        var inputSize = Marshal.SizeOf<Input>();
        var sent = SendInput((uint)inputs.Length, inputs, inputSize);
        if (sent != inputs.Length)
        {
            var error = Marshal.GetLastWin32Error();
            AppLogger.Error($"SendInput keyboard failed. Action={action}, Sent={sent}, Expected={inputs.Length}, CbSize={inputSize}, Error={error}");
            throw new InvalidOperationException($"SendInput keyboard failed with error {error}.");
        }

        AppLogger.Info($"SendInput keyboard succeeded. Action={action}, Count={inputs.Length}, CbSize={inputSize}");
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, Input[] inputs, int size);

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public int Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MouseInput Mouse;

        [FieldOffset(0)]
        public KeyboardInput Keyboard;

        [FieldOffset(0)]
        public HardwareInput Hardware;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HardwareInput
    {
        public uint Msg;
        public ushort ParamL;
        public ushort ParamH;
    }
}
