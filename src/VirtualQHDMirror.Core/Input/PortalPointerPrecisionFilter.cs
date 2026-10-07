using System;
using System.Runtime.InteropServices;

namespace VirtualQHDMirror.Core.Input;

internal sealed class PortalPointerPrecisionFilter : IDisposable
{
    private const int WhMouseLl = 14;
    private const uint WmMouseMove = 0x0200;
    private const uint LlMhfInjected = 0x00000001;

    private const double FullStrengthDistance = 2.0;
    private const double ZeroStrengthDistance = 12.0;
    private const double MaximumGain = 1.5;

    private readonly PortalInputGeometry _geometry;
    private readonly WindowsPointerInjector _pointer;
    private readonly LowLevelMouseProc _hookCallback;

    private IntPtr _hook;
    private PixelPoint _lastFinalPoint;
    private PixelPoint? _expectedSyntheticPoint;
    private double _residualX;
    private double _residualY;
    private bool _hasBaseline;
    private bool _disposed;

    public PortalPointerPrecisionFilter(
        PortalInputGeometry geometry,
        WindowsPointerInjector pointer)
    {
        _geometry = geometry ??
            throw new ArgumentNullException(
                nameof(geometry));

        _pointer = pointer ??
            throw new ArgumentNullException(
                nameof(pointer));

        _hookCallback =
            HookCallback;
    }

    public bool IsActive =>
        _hook != IntPtr.Zero;

    public bool Start(
        PixelPoint baseline)
    {
        EnsureNotDisposed();

        Stop();

        _lastFinalPoint =
            ClampToSource(
                baseline);

        _hasBaseline = true;
        ResetResidual();

        _hook =
            NativeMethods.SetWindowsHookEx(
                WhMouseLl,
                _hookCallback,
                NativeMethods.GetModuleHandle(
                    null),
                0);

        if (_hook != IntPtr.Zero)
        {
            return true;
        }

        _hasBaseline = false;
        return false;
    }

    public void Stop()
    {
        if (_hook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(
                _hook);

            _hook = IntPtr.Zero;
        }

        _hasBaseline = false;
        _expectedSyntheticPoint = null;
        ResetResidual();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();
        _disposed = true;
    }

    private IntPtr HookCallback(
        int code,
        IntPtr wParam,
        IntPtr lParam)
    {
        try
        {
            if (code < 0 ||
                _hook == IntPtr.Zero ||
                unchecked((uint)wParam.ToInt64()) !=
                    WmMouseMove)
            {
                return CallNext(
                    code,
                    wParam,
                    lParam);
            }

            var data =
                Marshal.PtrToStructure<LowLevelMouseHookData>(
                    lParam);

            var point =
                new PixelPoint(
                    data.Point.X,
                    data.Point.Y);

            if (_expectedSyntheticPoint is
                    PixelPoint expected &&
                point == expected)
            {
                _lastFinalPoint =
                    point;

                _hasBaseline = true;
                _expectedSyntheticPoint = null;

                return CallNext(
                    code,
                    wParam,
                    lParam);
            }

            if ((data.Flags & LlMhfInjected) != 0 ||
                data.ExtraInfo ==
                    WindowsPointerInjector.InjectionMarker)
            {
                return CallNext(
                    code,
                    wParam,
                    lParam);
            }

            // A genuine hardware move supersedes any synthetic point that
            // was expected but never observed by the low-level hook.
            _expectedSyntheticPoint = null;

            if (!_geometry.IsSourcePoint(
                    point))
            {
                _hasBaseline = false;
                ResetResidual();

                return CallNext(
                    code,
                    wParam,
                    lParam);
            }

            if (!_hasBaseline)
            {
                _lastFinalPoint = point;
                _hasBaseline = true;

                return CallNext(
                    code,
                    wParam,
                    lParam);
            }

            var deltaX =
                point.X -
                _lastFinalPoint.X;

            var deltaY =
                point.Y -
                _lastFinalPoint.Y;

            if (deltaX == 0 &&
                deltaY == 0)
            {
                return CallNext(
                    code,
                    wParam,
                    lParam);
            }

            var movement =
                Math.Sqrt(
                    ((double)deltaX * deltaX) +
                    ((double)deltaY * deltaY));

            var strength =
                GetPrecisionStrength(
                    movement);

            if (strength <= 0)
            {
                _lastFinalPoint = point;
                ResetResidual();

                return CallNext(
                    code,
                    wParam,
                    lParam);
            }

            var sourceToDestinationX =
                Math.Clamp(
                    _geometry.Source.Width /
                    (double)_geometry.Destination.Width,
                    1.0,
                    MaximumGain);

            var sourceToDestinationY =
                Math.Clamp(
                    _geometry.Source.Height /
                    (double)_geometry.Destination.Height,
                    1.0,
                    MaximumGain);

            var gainX =
                1.0 +
                ((sourceToDestinationX - 1.0) *
                 strength);

            var gainY =
                1.0 +
                ((sourceToDestinationY - 1.0) *
                 strength);

            var extraXWithResidual =
                (deltaX * (gainX - 1.0)) +
                _residualX;

            var extraYWithResidual =
                (deltaY * (gainY - 1.0)) +
                _residualY;

            var extraX =
                (int)Math.Round(
                    extraXWithResidual,
                    MidpointRounding.AwayFromZero);

            var extraY =
                (int)Math.Round(
                    extraYWithResidual,
                    MidpointRounding.AwayFromZero);

            _residualX =
                extraXWithResidual -
                extraX;

            _residualY =
                extraYWithResidual -
                extraY;

            if (extraX == 0 &&
                extraY == 0)
            {
                _lastFinalPoint = point;

                return CallNext(
                    code,
                    wParam,
                    lParam);
            }

            var adjustedPoint =
                ClampToSource(
                    new PixelPoint(
                        point.X + extraX,
                        point.Y + extraY));

            if (adjustedPoint ==
                point)
            {
                _lastFinalPoint = point;
                ResetResidual();

                return CallNext(
                    code,
                    wParam,
                    lParam);
            }

            _expectedSyntheticPoint =
                adjustedPoint;

            _lastFinalPoint =
                adjustedPoint;

            if (!_pointer.SetPosition(
                    adjustedPoint))
            {
                _expectedSyntheticPoint = null;
                _lastFinalPoint = point;
                ResetResidual();
            }

            return CallNext(
                code,
                wParam,
                lParam);
        }
        catch
        {
            // A low-level hook must never destabilize the mirror session.
            _hasBaseline = false;
            _expectedSyntheticPoint = null;
            ResetResidual();

            return CallNext(
                code,
                wParam,
                lParam);
        }
    }

    private static double GetPrecisionStrength(
        double movement)
    {
        if (movement <=
            FullStrengthDistance)
        {
            return 1.0;
        }

        if (movement >=
            ZeroStrengthDistance)
        {
            return 0.0;
        }

        var t =
            (movement -
             FullStrengthDistance) /
            (ZeroStrengthDistance -
             FullStrengthDistance);

        var smoothStep =
            t *
            t *
            (3.0 -
             (2.0 * t));

        return 1.0 -
            smoothStep;
    }

    private PixelPoint ClampToSource(
        PixelPoint point) =>
        new(
            Math.Clamp(
                point.X,
                _geometry.Source.Left,
                _geometry.Source.Right - 1),
            Math.Clamp(
                point.Y,
                _geometry.Source.Top,
                _geometry.Source.Bottom - 1));

    private void ResetResidual()
    {
        _residualX = 0;
        _residualY = 0;
    }

    private static IntPtr CallNext(
        int code,
        IntPtr wParam,
        IntPtr lParam) =>
        NativeMethods.CallNextHookEx(
            IntPtr.Zero,
            code,
            wParam,
            lParam);

    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(
                nameof(PortalPointerPrecisionFilter));
        }
    }

    private delegate IntPtr LowLevelMouseProc(
        int code,
        IntPtr wParam,
        IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LowLevelMouseHookData
    {
        public NativePoint Point;
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
        internal static extern IntPtr SetWindowsHookEx(
            int hookId,
            LowLevelMouseProc hookProc,
            IntPtr module,
            uint threadId);

        [DllImport(
            "user32.dll",
            SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UnhookWindowsHookEx(
            IntPtr hook);

        [DllImport(
            "user32.dll",
            SetLastError = true)]
        internal static extern IntPtr CallNextHookEx(
            IntPtr hook,
            int code,
            IntPtr wParam,
            IntPtr lParam);

        [DllImport(
            "kernel32.dll",
            CharSet = CharSet.Unicode,
            SetLastError = true)]
        internal static extern IntPtr GetModuleHandle(
            string? moduleName);
    }
}
