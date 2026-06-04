using System.Runtime.InteropServices;

namespace RemoteTouchpad.Input;

public sealed class MouseController
{
    private const int InputMouse = 0;
    private const uint MouseeventfMove = 0x0001;
    private const uint MouseeventfLeftDown = 0x0002;
    private const uint MouseeventfLeftUp = 0x0004;
    private const uint MouseeventfRightDown = 0x0008;
    private const uint MouseeventfRightUp = 0x0010;
    private const uint MouseeventfWheel = 0x0800;

    public void Move(double dx, double dy, double sensitivity)
    {
        var x = ClampToInt(dx * sensitivity);
        var y = ClampToInt(dy * sensitivity);

        if (x == 0 && y == 0)
        {
            return;
        }

        SendMouseInput(x, y, 0, MouseeventfMove);
    }

    public void Scroll(double delta)
    {
        var wheelDelta = ClampToInt(-delta);
        if (wheelDelta == 0)
        {
            return;
        }

        SendMouseInput(0, 0, wheelDelta, MouseeventfWheel);
    }

    public void Click(MouseButton button)
    {
        Down(button);
        Thread.Sleep(20);
        Up(button);
    }

    public void DoubleClick(MouseButton button)
    {
        Click(button);
        Thread.Sleep(60);
        Click(button);
    }

    public void Down(MouseButton button)
    {
        SendMouseInput(0, 0, 0, button == MouseButton.Left ? MouseeventfLeftDown : MouseeventfRightDown);
    }

    public void Up(MouseButton button)
    {
        SendMouseInput(0, 0, 0, button == MouseButton.Left ? MouseeventfLeftUp : MouseeventfRightUp);
    }

    private static int ClampToInt(double value)
    {
        if (value > int.MaxValue)
        {
            return int.MaxValue;
        }

        if (value < int.MinValue)
        {
            return int.MinValue;
        }

        return (int)Math.Round(value);
    }

    private static void SendMouseInput(int dx, int dy, int mouseData, uint flags)
    {
        var input = new Input
        {
            Type = InputMouse,
            Data = new InputUnion
            {
                Mouse = new MouseInput
                {
                    Dx = dx,
                    Dy = dy,
                    MouseData = mouseData,
                    Flags = flags
                }
            }
        };

        var sent = SendInput(1, [input], Marshal.SizeOf<Input>());
        if (sent == 0)
        {
            throw new InvalidOperationException($"SendInput failed with error {Marshal.GetLastWin32Error()}.");
        }
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
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int Dx;
        public int Dy;
        public int MouseData;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }
}
