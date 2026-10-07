using System;
using System.Runtime.InteropServices;

namespace VirtualQHDMirror.Core.Input;

public enum NativeMouseButton
{
    Left,
    Right,
    Middle,
    XButton1,
    XButton2
}

public sealed class WindowsPointerInjector
{
    internal static readonly UIntPtr InjectionMarker =
        new(0x56514D50u);

    private const uint InputMouse = 0;

    private const uint MouseEventMove = 0x0001;
    private const uint MouseEventLeftDown = 0x0002;
    private const uint MouseEventLeftUp = 0x0004;
    private const uint MouseEventRightDown = 0x0008;
    private const uint MouseEventRightUp = 0x0010;
    private const uint MouseEventMiddleDown = 0x0020;
    private const uint MouseEventMiddleUp = 0x0040;
    private const uint MouseEventXDown = 0x0080;
    private const uint MouseEventXUp = 0x0100;
    private const uint MouseEventWheel = 0x0800;
    private const uint MouseEventVirtualDesk = 0x4000;
    private const uint MouseEventAbsolute = 0x8000;

    private const uint XButton1 = 1;
    private const uint XButton2 = 2;

    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;

    public bool TryGetCursorPosition(
        out PixelPoint point)
    {
        if (!NativeMethods.GetCursorPos(
                out var nativePoint))
        {
            point = default;
            return false;
        }

        point = new PixelPoint(
            nativePoint.X,
            nativePoint.Y);

        return true;
    }

    public bool ClipTo(
        PixelRect rect)
    {
        var nativeRect = new NativeRect
        {
            Left = rect.Left,
            Top = rect.Top,
            Right = rect.Right,
            Bottom = rect.Bottom
        };

        return NativeMethods.ClipCursor(
            ref nativeRect);
    }

    public bool ReleaseClip() =>
        NativeMethods.ReleaseCursorClip(
            IntPtr.Zero);

    public bool MoveAbsolute(
        PixelPoint point)
    {
        var virtualX = NativeMethods.GetSystemMetrics(
            SmXVirtualScreen);

        var virtualY = NativeMethods.GetSystemMetrics(
            SmYVirtualScreen);

        var virtualWidth = NativeMethods.GetSystemMetrics(
            SmCxVirtualScreen);

        var virtualHeight = NativeMethods.GetSystemMetrics(
            SmCyVirtualScreen);

        if (virtualWidth <= 1 ||
            virtualHeight <= 1)
        {
            return NativeMethods.SetCursorPos(
                point.X,
                point.Y);
        }

        var normalizedX =
            (int)Math.Round(
                (point.X - virtualX) *
                65535.0 /
                (virtualWidth - 1));

        var normalizedY =
            (int)Math.Round(
                (point.Y - virtualY) *
                65535.0 /
                (virtualHeight - 1));

        normalizedX = Math.Clamp(
            normalizedX,
            0,
            65535);

        normalizedY = Math.Clamp(
            normalizedY,
            0,
            65535);

        var input = CreateMouseInput(
            normalizedX,
            normalizedY,
            0,
            MouseEventMove |
            MouseEventAbsolute |
            MouseEventVirtualDesk);

        if (NativeMethods.SendInput(
                1,
                [input],
                Marshal.SizeOf<NativeInput>()) == 1)
        {
            return true;
        }

        return NativeMethods.SetCursorPos(
            point.X,
            point.Y);
    }

    public bool SendButton(
        NativeMouseButton button,
        bool down)
    {
        var (flags, data) =
            button switch
            {
                NativeMouseButton.Right =>
                    (
                        down
                            ? MouseEventRightDown
                            : MouseEventRightUp,
                        0u
                    ),

                NativeMouseButton.Middle =>
                    (
                        down
                            ? MouseEventMiddleDown
                            : MouseEventMiddleUp,
                        0u
                    ),

                NativeMouseButton.XButton1 =>
                    (
                        down
                            ? MouseEventXDown
                            : MouseEventXUp,
                        XButton1
                    ),

                NativeMouseButton.XButton2 =>
                    (
                        down
                            ? MouseEventXDown
                            : MouseEventXUp,
                        XButton2
                    ),

                _ =>
                    (
                        down
                            ? MouseEventLeftDown
                            : MouseEventLeftUp,
                        0u
                    )
            };

        var input = CreateMouseInput(
            0,
            0,
            data,
            flags);

        return NativeMethods.SendInput(
            1,
            [input],
            Marshal.SizeOf<NativeInput>()) == 1;
    }

    public bool SendWheel(
        int delta)
    {
        var input = CreateMouseInput(
            0,
            0,
            unchecked((uint)delta),
            MouseEventWheel);

        return NativeMethods.SendInput(
            1,
            [input],
            Marshal.SizeOf<NativeInput>()) == 1;
    }

    private static NativeInput CreateMouseInput(
        int x,
        int y,
        uint data,
        uint flags) =>
        new()
        {
            Type = InputMouse,
            Union = new NativeInputUnion
            {
                Mouse = new NativeMouseInput
                {
                    X = x,
                    Y = y,
                    MouseData = data,
                    Flags = flags,
                    ExtraInfo = InjectionMarker
                }
            }
        };

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeInput
    {
        public uint Type;
        public NativeInputUnion Union;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct NativeInputUnion
    {
        [FieldOffset(0)]
        public NativeMouseInput Mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMouseInput
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    private static class NativeMethods
    {
        [DllImport(
            "user32.dll",
            SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCursorPos(
            out NativePoint point);

        [DllImport(
            "user32.dll",
            SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetCursorPos(
            int x,
            int y);

        [DllImport(
            "user32.dll",
            SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ClipCursor(
            ref NativeRect rect);

        [DllImport(
            "user32.dll",
            EntryPoint = "ClipCursor",
            SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ReleaseCursorClip(
            IntPtr rect);

        [DllImport(
            "user32.dll",
            SetLastError = true)]
        internal static extern uint SendInput(
            uint inputCount,
            [In] NativeInput[] inputs,
            int inputSize);

        [DllImport(
            "user32.dll")]
        internal static extern int GetSystemMetrics(
            int index);
    }
}
