using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace VirtualQHDMirror.Core.Rendering;

internal readonly record struct GdiCursorPlacement(
    IntPtr Cursor,
    int X,
    int Y,
    int Width,
    int Height);

internal sealed class GdiCursorCompositor
{
    private const uint CursorShowing = 0x00000001;
    private const uint DiNormal = 0x0003;

    private readonly Dictionary<IntPtr, CursorShape>
        _shapeCache = new();

    public bool TryGetPlacement(
        int sourceLeft,
        int sourceTop,
        int sourceWidth,
        int sourceHeight,
        int destinationWidth,
        int destinationHeight,
        out GdiCursorPlacement placement)
    {
        placement = default;

        var cursorInfo = new CursorInfo
        {
            Size = checked(
                (uint)Marshal.SizeOf<CursorInfo>())
        };

        if (!NativeMethods.GetCursorInfo(
                ref cursorInfo) ||
            (cursorInfo.Flags & CursorShowing) == 0 ||
            cursorInfo.Cursor == IntPtr.Zero)
        {
            return false;
        }

        var cursorX =
            cursorInfo.ScreenPosition.X;

        var cursorY =
            cursorInfo.ScreenPosition.Y;

        if (cursorX < sourceLeft ||
            cursorX >= sourceLeft + sourceWidth ||
            cursorY < sourceTop ||
            cursorY >= sourceTop + sourceHeight)
        {
            return false;
        }

        var shape =
            GetShape(
                cursorInfo.Cursor);

        var scaleX =
            destinationWidth /
            (double)sourceWidth;

        var scaleY =
            destinationHeight /
            (double)sourceHeight;

        var hotspotX =
            (int)Math.Round(
                shape.HotspotX *
                scaleX);

        var hotspotY =
            (int)Math.Round(
                shape.HotspotY *
                scaleY);

        var drawWidth =
            Math.Max(
                1,
                (int)Math.Round(
                    shape.Width *
                    scaleX));

        var drawHeight =
            Math.Max(
                1,
                (int)Math.Round(
                    shape.Height *
                    scaleY));

        var destinationX =
            (int)Math.Round(
                (cursorX - sourceLeft) *
                scaleX) -
            hotspotX;

        var destinationY =
            (int)Math.Round(
                (cursorY - sourceTop) *
                scaleY) -
            hotspotY;

        placement =
            new GdiCursorPlacement(
                cursorInfo.Cursor,
                destinationX,
                destinationY,
                drawWidth,
                drawHeight);

        return true;
    }

    public bool Draw(
        IntPtr destinationDc,
        GdiCursorPlacement placement) =>
        NativeMethods.DrawIconEx(
            destinationDc,
            placement.X,
            placement.Y,
            placement.Cursor,
            placement.Width,
            placement.Height,
            0,
            IntPtr.Zero,
            DiNormal);

    private CursorShape GetShape(
        IntPtr cursor)
    {
        if (_shapeCache.TryGetValue(
                cursor,
                out var cached))
        {
            return cached;
        }

        var shape =
            new CursorShape(
                Width: 32,
                Height: 32,
                HotspotX: 0,
                HotspotY: 0);

        if (NativeMethods.GetIconInfo(
                cursor,
                out var iconInfo))
        {
            var width = shape.Width;
            var height = shape.Height;

            try
            {
                var bitmap =
                    new BitmapInfo();

                if (iconInfo.ColorBitmap != IntPtr.Zero &&
                    NativeMethods.GetObject(
                        iconInfo.ColorBitmap,
                        Marshal.SizeOf<BitmapInfo>(),
                        ref bitmap) > 0)
                {
                    width = Math.Max(
                        1,
                        bitmap.Width);

                    height = Math.Max(
                        1,
                        Math.Abs(
                            bitmap.Height));
                }
                else if (
                    iconInfo.MaskBitmap != IntPtr.Zero &&
                    NativeMethods.GetObject(
                        iconInfo.MaskBitmap,
                        Marshal.SizeOf<BitmapInfo>(),
                        ref bitmap) > 0)
                {
                    width = Math.Max(
                        1,
                        bitmap.Width);

                    height = Math.Max(
                        1,
                        Math.Abs(
                            bitmap.Height) /
                        2);
                }
            }
            finally
            {
                if (iconInfo.MaskBitmap != IntPtr.Zero)
                {
                    NativeMethods.DeleteObject(
                        iconInfo.MaskBitmap);
                }

                if (iconInfo.ColorBitmap != IntPtr.Zero)
                {
                    NativeMethods.DeleteObject(
                        iconInfo.ColorBitmap);
                }
            }

            shape =
                new CursorShape(
                    Width: width,
                    Height: height,
                    HotspotX: checked(
                        (int)iconInfo.HotspotX),
                    HotspotY: checked(
                        (int)iconInfo.HotspotY));
        }

        _shapeCache[cursor] =
            shape;

        return shape;
    }

    private readonly record struct CursorShape(
        int Width,
        int Height,
        int HotspotX,
        int HotspotY);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CursorInfo
    {
        public uint Size;
        public uint Flags;
        public IntPtr Cursor;
        public NativePoint ScreenPosition;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IconInfo
    {
        [MarshalAs(UnmanagedType.Bool)]
        public bool IsIcon;

        public uint HotspotX;
        public uint HotspotY;
        public IntPtr MaskBitmap;
        public IntPtr ColorBitmap;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public int Type;
        public int Width;
        public int Height;
        public int WidthBytes;
        public ushort Planes;
        public ushort BitsPixel;
        public IntPtr Bits;
    }

    private static class NativeMethods
    {
        [DllImport(
            "user32.dll",
            SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCursorInfo(
            ref CursorInfo cursorInfo);

        [DllImport(
            "user32.dll",
            SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetIconInfo(
            IntPtr icon,
            out IconInfo iconInfo);

        [DllImport(
            "gdi32.dll",
            SetLastError = true)]
        internal static extern int GetObject(
            IntPtr gdiObject,
            int bufferSize,
            ref BitmapInfo bitmap);

        [DllImport(
            "gdi32.dll",
            SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DeleteObject(
            IntPtr gdiObject);

        [DllImport(
            "user32.dll",
            SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DrawIconEx(
            IntPtr deviceContext,
            int x,
            int y,
            IntPtr icon,
            int width,
            int height,
            uint step,
            IntPtr brush,
            uint flags);
    }
}
