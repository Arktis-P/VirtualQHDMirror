using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Numerics;
using System.Runtime.InteropServices;
using SharpGen.Runtime;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using static Vortice.Direct3D11.D3D11;
using static Vortice.DXGI.DXGI;

namespace VirtualQHDMirror.Session;

internal readonly record struct D3D11RenderFrameResult(
    bool Success,
    bool DesktopFrameUpdated,
    double FrameCostMilliseconds,
    double SmoothedFrameCostMilliseconds);

internal sealed class D3D11MirrorRenderer : IDisposable
{
    private const uint CursorShowing = 0x00000001;
    private const uint DiNormal = 0x0003;
    private const double CursorTextureRefreshMilliseconds = 125.0;

    private static readonly FeatureLevel[] FeatureLevels =
    [
        FeatureLevel.Level_11_1,
        FeatureLevel.Level_11_0,
        FeatureLevel.Level_10_1,
        FeatureLevel.Level_10_0
    ];

    private const string ShaderSource = """
        Texture2D FrameTexture : register(t0);
        Texture2D CursorTexture : register(t1);
        SamplerState LinearSampler : register(s0);

        cbuffer CursorState : register(b0)
        {
            float4 CursorRect;
            float CursorVisible;
            float3 CursorPadding;
        };

        struct VSOutput
        {
            float4 Position : SV_Position;
            float2 Uv : TEXCOORD0;
        };

        VSOutput VSMain(uint vertexId : SV_VertexID)
        {
            VSOutput output;
            float2 uv = float2((vertexId << 1) & 2, vertexId & 2);
            output.Uv = uv;
            output.Position = float4(
                uv.x * 2.0f - 1.0f,
                1.0f - uv.y * 2.0f,
                0.0f,
                1.0f);
            return output;
        }

        float4 PSMain(VSOutput input) : SV_Target
        {
            float4 background =
                FrameTexture.Sample(LinearSampler, input.Uv);

            if (CursorVisible > 0.5f &&
                CursorRect.z > 0.0f &&
                CursorRect.w > 0.0f)
            {
                float2 cursorUv =
                    (input.Position.xy - CursorRect.xy) /
                    CursorRect.zw;

                if (cursorUv.x >= 0.0f &&
                    cursorUv.x <= 1.0f &&
                    cursorUv.y >= 0.0f &&
                    cursorUv.y <= 1.0f)
                {
                    float4 cursor =
                        CursorTexture.Sample(
                            LinearSampler,
                            cursorUv);

                    background.rgb =
                        cursor.rgb +
                        background.rgb *
                        (1.0f - cursor.a);
                }
            }

            background.a = 1.0f;
            return background;
        }
        """;

    private readonly string _sourceDeviceName;
    private readonly int _sourceLeft;
    private readonly int _sourceTop;
    private readonly int _sourceWidth;
    private readonly int _sourceHeight;

    private IDXGIFactory2? _factory;
    private IDXGIAdapter1? _adapter;
    private IDXGIOutput1? _output;
    private IDXGIOutputDuplication? _duplication;
    private ID3D11Device? _device;
    private ID3D11DeviceContext? _context;
    private IDXGISwapChain1? _swapChain;
    private ID3D11Texture2D? _backBuffer;
    private ID3D11RenderTargetView? _renderTarget;
    private ID3D11Texture2D? _frameTexture;
    private ID3D11ShaderResourceView? _frameView;
    private ID3D11VertexShader? _vertexShader;
    private ID3D11PixelShader? _pixelShader;
    private ID3D11SamplerState? _sampler;
    private ID3D11Buffer? _cursorConstants;
    private ID3D11Texture2D? _cursorTexture;
    private ID3D11ShaderResourceView? _cursorView;

    private IntPtr _destinationWindow;
    private int _destinationWidth;
    private int _destinationHeight;
    private bool _hasDesktopFrame;
    private bool _disposed;

    private IntPtr _lastCursorHandle;
    private int _cursorNativeWidth = 1;
    private int _cursorNativeHeight = 1;
    private int _cursorHotspotX;
    private int _cursorHotspotY;
    private long _lastCursorTextureRefreshTimestamp;

    private long _presentedFrames;
    private long _desktopFrames;
    private long _acquireTimeouts;
    private double _smoothedFrameCostMilliseconds;

    public D3D11MirrorRenderer(
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

        _sourceDeviceName = sourceDeviceName;
        _sourceLeft = sourceLeft;
        _sourceTop = sourceTop;
        _sourceWidth = sourceWidth;
        _sourceHeight = sourceHeight;
    }

    public long PresentedFrames => _presentedFrames;
    public long DesktopFrames => _desktopFrames;
    public long AcquireTimeouts => _acquireTimeouts;
    public double SmoothedFrameCostMilliseconds =>
        _smoothedFrameCostMilliseconds;

    public void AttachDestination(
        IntPtr destinationWindow,
        int destinationWidth,
        int destinationHeight)
    {
        EnsureNotDisposed();

        if (destinationWindow == IntPtr.Zero)
        {
            throw new ArgumentException(
                "Destination window handle is required.",
                nameof(destinationWindow));
        }

        if (destinationWidth <= 0 ||
            destinationHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(destinationWidth),
                "Destination dimensions must be positive.");
        }

        if (_swapChain is not null)
        {
            throw new InvalidOperationException(
                "The D3D11 renderer is already attached.");
        }

        _factory =
            CreateDXGIFactory1<IDXGIFactory2>();

        (_adapter, _output) =
            FindSourceOutput(
                _factory,
                _sourceDeviceName);

        D3D11CreateDevice(
            _adapter,
            DriverType.Unknown,
            DeviceCreationFlags.BgraSupport,
            FeatureLevels,
            out ID3D11Device device,
            out _,
            out ID3D11DeviceContext context)
            .CheckError();

        _device = device;
        _context = context;

        _duplication =
            _output.DuplicateOutput(
                _device);

        var swapChainDescription =
            new SwapChainDescription1
            {
                Width =
                    checked((uint)destinationWidth),
                Height =
                    checked((uint)destinationHeight),
                Format =
                    Format.B8G8R8A8_UNorm,
                BufferCount = 2,
                BufferUsage =
                    Usage.RenderTargetOutput,
                SampleDescription =
                    SampleDescription.Default,
                Scaling =
                    Scaling.Stretch,
                SwapEffect =
                    SwapEffect.FlipDiscard,
                AlphaMode =
                    AlphaMode.Ignore
            };

        var fullscreenDescription =
            new SwapChainFullscreenDescription
            {
                Windowed = true
            };

        _swapChain =
            _factory.CreateSwapChainForHwnd(
                _device,
                destinationWindow,
                swapChainDescription,
                fullscreenDescription);

        _factory.MakeWindowAssociation(
            destinationWindow,
            WindowAssociationFlags.IgnoreAltEnter);

        _backBuffer =
            _swapChain.GetBuffer<ID3D11Texture2D>(
                0);

        _renderTarget =
            _device.CreateRenderTargetView(
                _backBuffer);

        var vertexShaderBytecode =
            Compiler.Compile(
                ShaderSource,
                "VSMain",
                "VirtualQHDMirror.CursorComposite.hlsl",
                "vs_5_0",
                ShaderFlags.OptimizationLevel3);

        var pixelShaderBytecode =
            Compiler.Compile(
                ShaderSource,
                "PSMain",
                "VirtualQHDMirror.CursorComposite.hlsl",
                "ps_5_0",
                ShaderFlags.OptimizationLevel3);

        _vertexShader =
            _device.CreateVertexShader(
                vertexShaderBytecode.Span);

        _pixelShader =
            _device.CreatePixelShader(
                pixelShaderBytecode.Span);

        _sampler =
            _device.CreateSamplerState(
                SamplerDescription.LinearClamp);

        _cursorConstants =
            _device.CreateConstantBuffer<CursorConstants>();

        _destinationWindow =
            destinationWindow;
        _destinationWidth =
            destinationWidth;
        _destinationHeight =
            destinationHeight;
    }

    public D3D11RenderFrameResult RenderFrame(
        int destinationWidth,
        int destinationHeight)
    {
        EnsureReady();

        if (destinationWidth != _destinationWidth ||
            destinationHeight != _destinationHeight)
        {
            throw new InvalidOperationException(
                "Destination size changed after the D3D11 swap chain was created.");
        }

        var start =
            Stopwatch.GetTimestamp();

        var desktopFrameUpdated =
            TryAcquireDesktopFrame(
                _hasDesktopFrame
                    ? 0u
                    : 100u);

        if (!_hasDesktopFrame ||
            _frameView is null)
        {
            throw new InvalidOperationException(
                "Desktop Duplication did not provide an initial frame.");
        }

        UpdateCursorConstants(
            BuildCursorConstants(
                destinationWidth,
                destinationHeight));

        var context =
            _context!;

        context.OMSetRenderTargets(
            _renderTarget!);
        context.RSSetViewport(
            new Viewport(
                destinationWidth,
                destinationHeight));
        context.IASetPrimitiveTopology(
            PrimitiveTopology.TriangleList);
        context.VSSetShader(
            _vertexShader!);
        context.PSSetShader(
            _pixelShader!);
        context.PSSetShaderResource(
            0,
            _frameView);
        context.PSSetShaderResource(
            1,
            _cursorView!);
        context.PSSetSampler(
            0,
            _sampler!);
        context.PSSetConstantBuffer(
            0,
            _cursorConstants!);
        context.Draw(
            3,
            0);
        context.PSUnsetShaderResources(
            0,
            2);

        _swapChain!.Present(
            1,
            PresentFlags.None)
            .CheckError();

        _presentedFrames++;

        var end =
            Stopwatch.GetTimestamp();

        var frameCostMilliseconds =
            (end - start) *
            1000.0 /
            Stopwatch.Frequency;

        _smoothedFrameCostMilliseconds =
            _smoothedFrameCostMilliseconds <= 0
                ? frameCostMilliseconds
                : (_smoothedFrameCostMilliseconds * 0.85) +
                  (frameCostMilliseconds * 0.15);

        return new D3D11RenderFrameResult(
            Success: true,
            DesktopFrameUpdated:
                desktopFrameUpdated,
            FrameCostMilliseconds:
                frameCostMilliseconds,
            SmoothedFrameCostMilliseconds:
                _smoothedFrameCostMilliseconds);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _context?.ClearState();
        _context?.Flush();

        _cursorView?.Dispose();
        _cursorTexture?.Dispose();
        _cursorConstants?.Dispose();
        _sampler?.Dispose();
        _pixelShader?.Dispose();
        _vertexShader?.Dispose();
        _frameView?.Dispose();
        _frameTexture?.Dispose();
        _renderTarget?.Dispose();
        _backBuffer?.Dispose();
        _swapChain?.Dispose();
        _duplication?.Dispose();
        _output?.Dispose();
        _context?.Dispose();
        _device?.Dispose();
        _adapter?.Dispose();
        _factory?.Dispose();

        _cursorView = null;
        _cursorTexture = null;
        _cursorConstants = null;
        _sampler = null;
        _pixelShader = null;
        _vertexShader = null;
        _frameView = null;
        _frameTexture = null;
        _renderTarget = null;
        _backBuffer = null;
        _swapChain = null;
        _duplication = null;
        _output = null;
        _context = null;
        _device = null;
        _adapter = null;
        _factory = null;
        _destinationWindow = IntPtr.Zero;
        _disposed = true;
    }

    private bool TryAcquireDesktopFrame(
        uint timeoutMilliseconds)
    {
        var duplication =
            _duplication!;

        var acquireResult =
            duplication.AcquireNextFrame(
                timeoutMilliseconds,
                out OutduplFrameInfo frameInfo,
                out IDXGIResource? desktopResource);

        if (acquireResult ==
            Vortice.DXGI.ResultCode.WaitTimeout)
        {
            _acquireTimeouts++;
            return false;
        }

        acquireResult.CheckError();

        try
        {
            if (desktopResource is null)
            {
                return false;
            }

            using var desktopTexture =
                desktopResource
                    .QueryInterface<ID3D11Texture2D>();

            EnsureFrameTexture(
                desktopTexture.Description);

            if (!_hasDesktopFrame ||
                frameInfo.LastPresentTime != 0)
            {
                _context!.CopyResource(
                    _frameTexture!,
                    desktopTexture);

                _hasDesktopFrame = true;
                _desktopFrames++;
                return true;
            }

            return false;
        }
        finally
        {
            desktopResource?.Dispose();
            duplication.ReleaseFrame();
        }
    }

    private void EnsureFrameTexture(
        Texture2DDescription sourceDescription)
    {
        if (_frameTexture is not null &&
            _frameTexture.Description.Width ==
                sourceDescription.Width &&
            _frameTexture.Description.Height ==
                sourceDescription.Height &&
            _frameTexture.Description.Format ==
                sourceDescription.Format)
        {
            return;
        }

        _frameView?.Dispose();
        _frameTexture?.Dispose();

        sourceDescription.BindFlags =
            BindFlags.ShaderResource;
        sourceDescription.Usage =
            ResourceUsage.Default;
        sourceDescription.CPUAccessFlags =
            CpuAccessFlags.None;
        sourceDescription.MiscFlags =
            ResourceOptionFlags.None;

        _frameTexture =
            _device!.CreateTexture2D(
                sourceDescription);

        _frameView =
            _device.CreateShaderResourceView(
                _frameTexture);
    }

    private CursorConstants BuildCursorConstants(
        int destinationWidth,
        int destinationHeight)
    {
        var cursorInfo =
            new CursorInfo
            {
                Size =
                    checked(
                        (uint)Marshal.SizeOf<CursorInfo>())
            };

        if (!NativeMethods.GetCursorInfo(
                ref cursorInfo) ||
            (cursorInfo.Flags & CursorShowing) == 0 ||
            cursorInfo.Cursor == IntPtr.Zero)
        {
            return CursorConstants.Hidden;
        }

        var cursorX =
            cursorInfo.ScreenPosition.X;
        var cursorY =
            cursorInfo.ScreenPosition.Y;

        if (cursorX < _sourceLeft ||
            cursorX >= _sourceLeft + _sourceWidth ||
            cursorY < _sourceTop ||
            cursorY >= _sourceTop + _sourceHeight)
        {
            return CursorConstants.Hidden;
        }

        if (!EnsureCursorTexture(
                cursorInfo.Cursor))
        {
            return CursorConstants.Hidden;
        }

        var scaleX =
            destinationWidth /
            (double)_sourceWidth;
        var scaleY =
            destinationHeight /
            (double)_sourceHeight;

        var destinationX =
            (cursorX - _sourceLeft) *
            scaleX -
            _cursorHotspotX *
            scaleX;
        var destinationY =
            (cursorY - _sourceTop) *
            scaleY -
            _cursorHotspotY *
            scaleY;

        var drawWidth =
            _cursorNativeWidth *
            scaleX;
        var drawHeight =
            _cursorNativeHeight *
            scaleY;

        return new CursorConstants(
            new Vector4(
                (float)destinationX,
                (float)destinationY,
                (float)drawWidth,
                (float)drawHeight),
            1.0f,
            Vector3.Zero);
    }

    private bool EnsureCursorTexture(
        IntPtr cursorHandle)
    {
        var now =
            Stopwatch.GetTimestamp();

        var refreshElapsedMilliseconds =
            _lastCursorTextureRefreshTimestamp == 0
                ? double.MaxValue
                : (now -
                   _lastCursorTextureRefreshTimestamp) *
                  1000.0 /
                  Stopwatch.Frequency;

        if (_cursorView is not null &&
            cursorHandle == _lastCursorHandle &&
            refreshElapsedMilliseconds <
                CursorTextureRefreshMilliseconds)
        {
            return true;
        }

        if (!TryCaptureCursorBitmap(
                cursorHandle,
                out var pixels,
                out var width,
                out var height,
                out var hotspotX,
                out var hotspotY))
        {
            return _cursorView is not null &&
                   cursorHandle ==
                       _lastCursorHandle;
        }

        var newTexture =
            _device!.CreateTexture2D(
                pixels,
                Format.B8G8R8A8_UNorm,
                checked((uint)width),
                checked((uint)height),
                bindFlags:
                    BindFlags.ShaderResource,
                usage:
                    ResourceUsage.Immutable);

        var newView =
            _device.CreateShaderResourceView(
                newTexture);

        _cursorView?.Dispose();
        _cursorTexture?.Dispose();

        _cursorTexture =
            newTexture;
        _cursorView =
            newView;
        _lastCursorHandle =
            cursorHandle;
        _cursorNativeWidth =
            width;
        _cursorNativeHeight =
            height;
        _cursorHotspotX =
            hotspotX;
        _cursorHotspotY =
            hotspotY;
        _lastCursorTextureRefreshTimestamp =
            now;

        return true;
    }

    private static bool TryCaptureCursorBitmap(
        IntPtr cursorHandle,
        out byte[] pixels,
        out int width,
        out int height,
        out int hotspotX,
        out int hotspotY)
    {
        pixels = [];
        width = 0;
        height = 0;
        hotspotX = 0;
        hotspotY = 0;

        if (!NativeMethods.GetIconInfo(
                cursorHandle,
                out var iconInfo))
        {
            return false;
        }

        try
        {
            width = 32;
            height = 32;

            var bitmapInfo =
                new BitmapInfo();

            if (iconInfo.ColorBitmap != IntPtr.Zero &&
                NativeMethods.GetObject(
                    iconInfo.ColorBitmap,
                    Marshal.SizeOf<BitmapInfo>(),
                    ref bitmapInfo) > 0)
            {
                width =
                    Math.Max(
                        1,
                        bitmapInfo.Width);
                height =
                    Math.Max(
                        1,
                        Math.Abs(
                            bitmapInfo.Height));
            }
            else if (
                iconInfo.MaskBitmap != IntPtr.Zero &&
                NativeMethods.GetObject(
                    iconInfo.MaskBitmap,
                    Marshal.SizeOf<BitmapInfo>(),
                    ref bitmapInfo) > 0)
            {
                width =
                    Math.Max(
                        1,
                        bitmapInfo.Width);
                height =
                    Math.Max(
                        1,
                        Math.Abs(
                            bitmapInfo.Height) /
                        2);
            }

            hotspotX =
                checked(
                    (int)iconInfo.HotspotX);
            hotspotY =
                checked(
                    (int)iconInfo.HotspotY);

            using var bitmap =
                new Bitmap(
                    width,
                    height,
                    PixelFormat.Format32bppPArgb);

            using (var graphics =
                   Graphics.FromImage(
                       bitmap))
            {
                graphics.Clear(
                    System.Drawing.Color.Transparent);

                var deviceContext =
                    graphics.GetHdc();

                try
                {
                    if (!NativeMethods.DrawIconEx(
                            deviceContext,
                            0,
                            0,
                            cursorHandle,
                            width,
                            height,
                            0,
                            IntPtr.Zero,
                            DiNormal))
                    {
                        return false;
                    }
                }
                finally
                {
                    graphics.ReleaseHdc(
                        deviceContext);
                }
            }

            var rectangle =
                new Rectangle(
                    0,
                    0,
                    width,
                    height);

            var bitmapData =
                bitmap.LockBits(
                    rectangle,
                    ImageLockMode.ReadOnly,
                    PixelFormat.Format32bppPArgb);

            try
            {
                var rowBytes =
                    checked(
                        width * 4);

                pixels =
                    new byte[
                        checked(
                            rowBytes *
                            height)];

                for (var row = 0;
                     row < height;
                     row++)
                {
                    var source =
                        IntPtr.Add(
                            bitmapData.Scan0,
                            row *
                            bitmapData.Stride);

                    Marshal.Copy(
                        source,
                        pixels,
                        row *
                        rowBytes,
                        rowBytes);
                }
            }
            finally
            {
                bitmap.UnlockBits(
                    bitmapData);
            }

            return true;
        }
        finally
        {
            if (iconInfo.MaskBitmap !=
                IntPtr.Zero)
            {
                NativeMethods.DeleteObject(
                    iconInfo.MaskBitmap);
            }

            if (iconInfo.ColorBitmap !=
                IntPtr.Zero)
            {
                NativeMethods.DeleteObject(
                    iconInfo.ColorBitmap);
            }
        }
    }

    private void UpdateCursorConstants(
        CursorConstants constants)
    {
        var mapped =
            _context!.Map(
                _cursorConstants!,
                MapMode.WriteDiscard);

        Marshal.StructureToPtr(
            constants,
            mapped.DataPointer,
            false);

        _context.Unmap(
            _cursorConstants!,
            0);
    }

    private static (
        IDXGIAdapter1 Adapter,
        IDXGIOutput1 Output)
        FindSourceOutput(
            IDXGIFactory2 factory,
            string sourceDeviceName)
    {
        for (uint adapterIndex = 0;
             factory.EnumAdapters1(
                 adapterIndex,
                 out IDXGIAdapter1? adapter)
                 .Success;
             adapterIndex++)
        {
            if (adapter is null)
            {
                continue;
            }

            var keepAdapter =
                false;

            try
            {
                for (uint outputIndex = 0;
                     adapter.EnumOutputs(
                         outputIndex,
                         out IDXGIOutput? output)
                         .Success;
                     outputIndex++)
                {
                    if (output is null)
                    {
                        continue;
                    }

                    using (output)
                    {
                        var description =
                            output.Description;

                        if (!string.Equals(
                                description.DeviceName,
                                sourceDeviceName,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var output1 =
                            output.QueryInterface<IDXGIOutput1>();

                        keepAdapter =
                            true;

                        return (
                            adapter,
                            output1);
                    }
                }
            }
            finally
            {
                if (!keepAdapter)
                {
                    adapter.Dispose();
                }
            }
        }

        throw new InvalidOperationException(
            $"DXGI output {sourceDeviceName} was not found.");
    }

    private void EnsureReady()
    {
        EnsureNotDisposed();

        if (_destinationWindow ==
                IntPtr.Zero ||
            _device is null ||
            _context is null ||
            _duplication is null ||
            _swapChain is null ||
            _renderTarget is null)
        {
            throw new InvalidOperationException(
                "The D3D11 renderer must be attached before rendering.");
        }
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(
                nameof(D3D11MirrorRenderer));
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct CursorConstants
    {
        public static CursorConstants Hidden =>
            new(
                Vector4.Zero,
                0.0f,
                Vector3.Zero);

        public CursorConstants(
            Vector4 rect,
            float visible,
            Vector3 padding)
        {
            Rect = rect;
            Visible = visible;
            Padding = padding;
        }

        public readonly Vector4 Rect;
        public readonly float Visible;
        public readonly Vector3 Padding;
    }

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
    }
}
