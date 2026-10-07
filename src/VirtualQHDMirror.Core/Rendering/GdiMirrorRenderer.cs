using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace VirtualQHDMirror.Core.Rendering;

public sealed record GdiRenderFrameResult(
    bool Success,
    int NativeError,
    bool CursorComposited,
    double FrameCostMilliseconds,
    double SmoothedFrameCostMilliseconds);

public sealed class GdiMirrorRenderer : IDisposable
{
    private const uint Srccopy = 0x00CC0020;
    private const int Halftone = 4;

    private readonly IntPtr _sourceDc;
    private readonly int _sourceLeft;
    private readonly int _sourceTop;
    private readonly int _sourceWidth;
    private readonly int _sourceHeight;
    private readonly GdiCursorCompositor _cursorCompositor = new();

    private IntPtr _destinationWindow;
    private IntPtr _destinationDc;
    private int _destinationOwnerThreadId;
    private double _smoothedFrameCostMilliseconds;
    private bool _disposed;

    public GdiMirrorRenderer(
        string sourceDeviceName,
        int sourceWidth,
        int sourceHeight)
        : this(
            sourceDeviceName,
            sourceLeft: 0,
            sourceTop: 0,
            sourceWidth: sourceWidth,
            sourceHeight: sourceHeight)
    {
    }

    public GdiMirrorRenderer(
        string sourceDeviceName,
        int sourceLeft,
        int sourceTop,
        int sourceWidth,
        int sourceHeight)
    {
        if (string.IsNullOrWhiteSpace(sourceDeviceName))
        {
            throw new ArgumentException(
                "Source display device name is required.",
                nameof(sourceDeviceName));
        }

        if (sourceWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceWidth));
        }

        if (sourceHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceHeight));
        }

        _sourceLeft = sourceLeft;
        _sourceTop = sourceTop;
        _sourceWidth = sourceWidth;
        _sourceHeight = sourceHeight;

        _sourceDc = NativeMethods.CreateDC(
            "DISPLAY",
            sourceDeviceName,
            null,
            IntPtr.Zero);

        if (_sourceDc == IntPtr.Zero)
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                $"CreateDC failed for source display {sourceDeviceName}.");
        }
    }

    public bool IsDestinationAttached =>
        _destinationDc != IntPtr.Zero;

    public double SmoothedFrameCostMilliseconds =>
        _smoothedFrameCostMilliseconds;

    public void AttachDestination(
        IntPtr destinationWindow)
    {
        EnsureNotDisposed();

        if (destinationWindow == IntPtr.Zero)
        {
            throw new ArgumentException(
                "Destination window handle is required.",
                nameof(destinationWindow));
        }

        DetachDestination();

        var destinationDc = NativeMethods.GetDC(
            destinationWindow);

        if (destinationDc == IntPtr.Zero)
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "GetDC failed for the mirror destination window.");
        }

        if (NativeMethods.SetStretchBltMode(
                destinationDc,
                Halftone) == 0)
        {
            var error = Marshal.GetLastWin32Error();
            NativeMethods.ReleaseDC(
                destinationWindow,
                destinationDc);

            throw new Win32Exception(
                error,
                "SetStretchBltMode(HALFTONE) failed.");
        }

        if (!NativeMethods.SetBrushOrgEx(
                destinationDc,
                0,
                0,
                IntPtr.Zero))
        {
            var error = Marshal.GetLastWin32Error();
            NativeMethods.ReleaseDC(
                destinationWindow,
                destinationDc);

            throw new Win32Exception(
                error,
                "SetBrushOrgEx failed for the mirror destination.");
        }

        _destinationWindow = destinationWindow;
        _destinationDc = destinationDc;
        _destinationOwnerThreadId =
            Environment.CurrentManagedThreadId;
    }

    public GdiRenderFrameResult RenderFrame(
        int destinationWidth,
        int destinationHeight)
    {
        EnsureNotDisposed();
        EnsureDestinationAttached();
        EnsureDestinationThread();

        if (destinationWidth <= 0 ||
            destinationHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(destinationWidth),
                "Destination dimensions must be positive.");
        }

        var start = Stopwatch.GetTimestamp();

        var success = NativeMethods.StretchBlt(
            _destinationDc,
            0,
            0,
            destinationWidth,
            destinationHeight,
            _sourceDc,
            0,
            0,
            _sourceWidth,
            _sourceHeight,
            Srccopy);

        var nativeError = success
            ? 0
            : Marshal.GetLastWin32Error();

        var cursorComposited =
            success &&
            _cursorCompositor.TryDraw(
                _destinationDc,
                _sourceLeft,
                _sourceTop,
                _sourceWidth,
                _sourceHeight,
                destinationWidth,
                destinationHeight);

        var end = Stopwatch.GetTimestamp();
        var frameCostMilliseconds =
            (end - start) *
            1000.0 /
            Stopwatch.Frequency;

        _smoothedFrameCostMilliseconds =
            _smoothedFrameCostMilliseconds <= 0
                ? frameCostMilliseconds
                : (_smoothedFrameCostMilliseconds * 0.85) +
                  (frameCostMilliseconds * 0.15);

        return new GdiRenderFrameResult(
            Success: success,
            NativeError: nativeError,
            CursorComposited: cursorComposited,
            FrameCostMilliseconds: frameCostMilliseconds,
            SmoothedFrameCostMilliseconds:
                _smoothedFrameCostMilliseconds);
    }

    public void DetachDestination()
    {
        if (_destinationDc == IntPtr.Zero)
        {
            _destinationWindow = IntPtr.Zero;
            _destinationOwnerThreadId = 0;
            return;
        }

        EnsureDestinationThread();

        NativeMethods.ReleaseDC(
            _destinationWindow,
            _destinationDc);

        _destinationDc = IntPtr.Zero;
        _destinationWindow = IntPtr.Zero;
        _destinationOwnerThreadId = 0;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        DetachDestination();

        if (_sourceDc != IntPtr.Zero)
        {
            NativeMethods.DeleteDC(
                _sourceDc);
        }

        _disposed = true;
    }

    private void EnsureDestinationAttached()
    {
        if (_destinationDc == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                "A destination window must be attached before rendering.");
        }
    }

    private void EnsureDestinationThread()
    {
        if (_destinationOwnerThreadId !=
            Environment.CurrentManagedThreadId)
        {
            throw new InvalidOperationException(
                "GDI destination rendering must stay on the thread that attached the destination window.");
        }
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(
                nameof(GdiMirrorRenderer));
        }
    }

    private static class NativeMethods
    {
        [DllImport(
            "gdi32.dll",
            CharSet = CharSet.Unicode,
            SetLastError = true)]
        internal static extern IntPtr CreateDC(
            string driver,
            string device,
            string? output,
            IntPtr initData);

        [DllImport(
            "gdi32.dll",
            SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DeleteDC(
            IntPtr deviceContext);

        [DllImport(
            "gdi32.dll",
            SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool StretchBlt(
            IntPtr destinationDc,
            int destinationX,
            int destinationY,
            int destinationWidth,
            int destinationHeight,
            IntPtr sourceDc,
            int sourceX,
            int sourceY,
            int sourceWidth,
            int sourceHeight,
            uint rasterOperation);

        [DllImport(
            "gdi32.dll",
            SetLastError = true)]
        internal static extern int SetStretchBltMode(
            IntPtr deviceContext,
            int mode);

        [DllImport(
            "gdi32.dll",
            SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetBrushOrgEx(
            IntPtr deviceContext,
            int x,
            int y,
            IntPtr previousPoint);

        [DllImport(
            "user32.dll",
            SetLastError = true)]
        internal static extern IntPtr GetDC(
            IntPtr window);

        [DllImport(
            "user32.dll",
            SetLastError = true)]
        internal static extern int ReleaseDC(
            IntPtr window,
            IntPtr deviceContext);
    }
}
