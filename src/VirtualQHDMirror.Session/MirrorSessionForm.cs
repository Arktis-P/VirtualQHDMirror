using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using VirtualQHDMirror.Core.Displays;
using VirtualQHDMirror.Core.Input;
using VirtualQHDMirror.Core.Rendering;

namespace VirtualQHDMirror.Session;

internal sealed class MirrorSessionForm : Form
{
    internal const string SessionWindowTitle =
        "VirtualQHDMirror .NET Session";

    private const uint WmHotKey = 0x0312;

    private const int HotKeyExit = 1;
    private const int HotKeyMode = 4;

    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;

    private const uint VkF9 = 0x78;
    private const uint VkF12 = 0x7B;

    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;

    private const int DwmwaDisallowPeek = 11;
    private const int DwmwaExcludedFromPeek = 12;

    private const int MinimumFullFrameIntervalMilliseconds = 16;
    private const int BackingGuardPollIntervalMilliseconds = 16;
    private const int CursorPollIntervalMilliseconds = 8;

    private readonly DisplayEndpoint _source;
    private readonly DisplayEndpoint _destination;
    private readonly GdiMirrorRenderer _gdiRenderer;
    private D3D11PresentationLoop? _d3dLoop;
    private readonly WindowsPointerInjector _pointer;
    private readonly PortalInputController _portal;
    private readonly System.Windows.Forms.Timer _frameTimer;
    private readonly System.Windows.Forms.Timer _cursorTimer;

    private MirrorRenderMode _renderMode;
    private DateTimeOffset _lastInput =
        DateTimeOffset.UtcNow;

    private int _lastInterval = -1;
    private bool _timerResolutionRaised;
    private bool _cleanupStarted;
    private bool _cursorUpdateDisabled;
    private bool _usingD3D11;
    private int _consecutiveRenderFailures;
    private DateTimeOffset _lastD3DStats =
        DateTimeOffset.UtcNow;

    public MirrorSessionForm(
        DisplayEndpoint source,
        DisplayEndpoint destination,
        IReadOnlyList<PixelRect> logicalMonitorRects,
        MirrorRenderMode renderMode)
    {
        _source = source ??
            throw new ArgumentNullException(
                nameof(source));

        _destination = destination ??
            throw new ArgumentNullException(
                nameof(destination));

        _renderMode = renderMode;

        var sourceRect = new PixelRect(
            source.X,
            source.Y,
            source.Width,
            source.Height);

        var destinationRect = new PixelRect(
            destination.X,
            destination.Y,
            destination.Width,
            destination.Height);

        _pointer =
            new WindowsPointerInjector();

        _portal =
            new PortalInputController(
                new PortalInputGeometry(
                    sourceRect,
                    destinationRect),
                logicalMonitorRects,
                _pointer);

        _portal.Activity +=
            Portal_Activity;

        SessionDiagnostics.Write(
            $"Creating renderer for source {source.SourceName}.");

        _gdiRenderer =
            new GdiMirrorRenderer(
                source.SourceName,
                source.X,
                source.Y,
                source.Width,
                source.Height);

        Text = SessionWindowTitle;
        FormBorderStyle =
            FormBorderStyle.None;

        StartPosition =
            FormStartPosition.Manual;

        Bounds = new Rectangle(
            destination.X,
            destination.Y,
            destination.Width,
            destination.Height);

        BackColor = Color.Black;
        TopMost = true;
        ShowInTaskbar = false;
        DoubleBuffered = false;

        _frameTimer =
            new System.Windows.Forms.Timer();

        _frameTimer.Tick +=
            FrameTimer_Tick;

        _cursorTimer =
            new System.Windows.Forms.Timer
            {
                Interval =
                    BackingGuardPollIntervalMilliseconds
            };

        _cursorTimer.Tick +=
            CursorTimer_Tick;

        Shown +=
            MirrorSessionForm_Shown;

        FormClosing +=
            MirrorSessionForm_FormClosing;
    }

    public bool RenderFailureOccurred { get; private set; }

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters =
                base.CreateParams;

            parameters.ExStyle |=
                WsExNoActivate |
                WsExToolWindow;

            return parameters;
        }
    }

    protected override bool ShowWithoutActivation =>
        true;

    protected override void WndProc(
        ref Message message)
    {
        if (message.Msg == WmHotKey)
        {
            var id =
                message.WParam.ToInt32();

            if (id == HotKeyExit)
            {
                Close();
                return;
            }

            if (id == HotKeyMode)
            {
                _renderMode =
                    _renderMode switch
                    {
                        MirrorRenderMode.Adaptive =>
                            MirrorRenderMode.Fixed30,

                        MirrorRenderMode.Fixed30 =>
                            MirrorRenderMode.Fixed60,

                        MirrorRenderMode.Fixed60 =>
                            MirrorRenderMode.Adaptive,

                        _ =>
                            MirrorRenderMode.Adaptive
                    };

                MarkInput();
                return;
            }
        }

        base.WndProc(
            ref message);
    }

    protected override void OnPaintBackground(
        PaintEventArgs e)
    {
        // The frame renderer owns the full destination surface.
    }

    protected override void OnPaint(
        PaintEventArgs e)
    {
        DrawFrame();
    }

    protected override void OnMouseMove(
        MouseEventArgs e)
    {
        MarkInput();

        var local =
            new PixelPoint(
                e.X,
                e.Y);

        _portal.UpdateDestinationLocal(
            local);

        if (!_portal.IsActive &&
            _portal.Begin(local))
        {
            BeginPortalCursorPolling();
            DrawFrame();
        }

        base.OnMouseMove(
            e);
    }

    protected override void OnMouseDown(
        MouseEventArgs e)
    {
        MarkInput();

        var local =
            new PixelPoint(
                e.X,
                e.Y);

        _portal.UpdateDestinationLocal(
            local);

        if (!_portal.IsActive &&
            _portal.Begin(local))
        {
            BeginPortalCursorPolling();
            DrawFrame();

            if (TryMapMouseButton(
                    e.Button,
                    out var button))
            {
                _portal.SendButtonDown(
                    button);
            }
        }

        base.OnMouseDown(
            e);
    }

    protected override void OnMouseWheel(
        MouseEventArgs e)
    {
        MarkInput();

        var local =
            new PixelPoint(
                e.X,
                e.Y);

        _portal.UpdateDestinationLocal(
            local);

        if (!_portal.IsActive &&
            _portal.Begin(local))
        {
            BeginPortalCursorPolling();
            DrawFrame();

            _portal.SendWheel(
                e.Delta);
        }

        base.OnMouseWheel(
            e);
    }

    private void MirrorSessionForm_Shown(
        object? sender,
        EventArgs e)
    {
        SessionDiagnostics.Write(
            $"Session window shown. hwnd=0x{Handle.ToInt64():X}");

        Bounds = new Rectangle(
            _destination.X,
            _destination.Y,
            _destination.Width,
            _destination.Height);

        ApplyDwmIsolation();

        if (NativeMethods.timeBeginPeriod(1) == 0)
        {
            _timerResolutionRaised = true;
        }

        if (!TryAttachD3D11Renderer())
        {
            _gdiRenderer.AttachDestination(
                Handle);
        }

        SessionDiagnostics.Write(
            $"Presentation backend={(_usingD3D11 ? "D3D11/DesktopDuplication/VSync" : "GDI fallback")}; client={ClientSize.Width}x{ClientSize.Height}");

        NativeMethods.RegisterHotKey(
            Handle,
            HotKeyExit,
            ModControl | ModAlt,
            VkF12);

        NativeMethods.RegisterHotKey(
            Handle,
            HotKeyMode,
            ModControl | ModAlt,
            VkF9);

        ApplyInterval(
            MinimumFullFrameIntervalMilliseconds);

        _frameTimer.Start();
        _cursorTimer.Start();

        SessionDiagnostics.Write(
            "Logical pointer topology guard started; backing source is hidden from normal cursor traversal.");
    }

    private void MirrorSessionForm_FormClosing(
        object? sender,
        FormClosingEventArgs e)
    {
        Cleanup();
    }

    private void FrameTimer_Tick(
        object? sender,
        EventArgs e)
    {
        AdjustInterval();

        if (_usingD3D11)
        {
            if (!EnsureD3D11Healthy())
            {
                DrawFrame();
                return;
            }

            if (_portal.IsActive)
            {
                MaybeLogD3D11Stats();
                return;
            }

            _d3dLoop?.RequestFrame();
            MaybeLogD3D11Stats();
            return;
        }

        DrawFrame();
    }

    private void CursorTimer_Tick(
        object? sender,
        EventArgs e)
    {
        if (_cleanupStarted)
        {
            _cursorTimer.Stop();
            return;
        }

        if (!_portal.IsActive)
        {
            if (_cursorTimer.Interval !=
                BackingGuardPollIntervalMilliseconds)
            {
                _cursorTimer.Interval =
                    BackingGuardPollIntervalMilliseconds;
            }

            if (_portal.RedirectBackingSourceIfInactive())
            {
                SessionDiagnostics.Write(
                    "Pointer entered the hidden backing source outside Portal mode; redirected to the mirrored destination.");
            }

            return;
        }

        var portalState =
            _portal.Update();

        if (portalState !=
            PortalUpdateState.Active)
        {
            _cursorTimer.Interval =
                BackingGuardPollIntervalMilliseconds;

            if (_usingD3D11)
            {
                _d3dLoop?.SetContinuous(
                    false);
            }

            DrawFrame();
            return;
        }

        if (_usingD3D11)
        {
            if (!EnsureD3D11Healthy())
            {
                DrawFrame();
                return;
            }

            MaybeLogD3D11Stats();
            return;
        }

        if (_cursorUpdateDisabled)
        {
            return;
        }

        try
        {
            _gdiRenderer.RenderCursorUpdate(
                ClientSize.Width,
                ClientSize.Height);
        }
        catch (Exception exception)
        {
            _cursorUpdateDisabled = true;

            SessionDiagnostics.Write(
                $"Cursor-only update disabled after an error; Portal polling remains active: {exception.Message}");

            DrawFrame();
        }
    }

    private void DrawFrame()
    {
        if (_cleanupStarted ||
            ClientSize.Width <= 0 ||
            ClientSize.Height <= 0)
        {
            return;
        }

        if (_usingD3D11)
        {
            if (EnsureD3D11Healthy())
            {
                _d3dLoop?.RequestFrame();
                MaybeLogD3D11Stats();
                _consecutiveRenderFailures = 0;
                return;
            }
        }

        GdiRenderFrameResult result;

        try
        {
            result =
                _gdiRenderer.RenderFrame(
                    ClientSize.Width,
                    ClientSize.Height);
        }
        catch
        {
            RegisterRenderFailure();
            return;
        }

        if (!result.Success)
        {
            RegisterRenderFailure();
            return;
        }

        _consecutiveRenderFailures = 0;
    }

    private void RegisterRenderFailure()
    {
        _consecutiveRenderFailures++;

        if (_consecutiveRenderFailures < 5)
        {
            return;
        }

        RenderFailureOccurred = true;
        SessionDiagnostics.Write(
            "Five consecutive GDI fallback render failures detected; closing session.");
        Close();
    }

    private void AdjustInterval()
    {
        var idleTime =
            DateTimeOffset.UtcNow -
            _lastInput;

        var requestedInterval =
            MirrorFrameTimingPolicy
                .GetIntervalMilliseconds(
                    _renderMode,
                    idleTime);

        ApplyInterval(
            Math.Max(
                MinimumFullFrameIntervalMilliseconds,
                requestedInterval));
    }

    private void ApplyInterval(
        int milliseconds)
    {
        milliseconds =
            Math.Max(
                4,
                milliseconds);

        if (_lastInterval ==
            milliseconds)
        {
            return;
        }

        _lastInterval =
            milliseconds;

        _frameTimer.Interval =
            milliseconds;
    }

    private void MarkInput()
    {
        _lastInput =
            DateTimeOffset.UtcNow;

        if (_renderMode ==
            MirrorRenderMode.Adaptive)
        {
            ApplyInterval(
                MinimumFullFrameIntervalMilliseconds);
        }
    }

    private void Portal_Activity(
        object? sender,
        EventArgs e)
    {
        MarkInput();
    }

    private void BeginPortalCursorPolling()
    {
        var interval =
            _usingD3D11
                ? 4
                : CursorPollIntervalMilliseconds;

        if (_cursorTimer.Interval !=
            interval)
        {
            _cursorTimer.Interval =
                interval;
        }

        if (_usingD3D11)
        {
            _d3dLoop?.SetContinuous(
                true);
        }

        if (!_cursorTimer.Enabled)
        {
            _cursorTimer.Start();
        }
    }

    private bool TryAttachD3D11Renderer()
    {
        if (string.Equals(
                Environment.GetEnvironmentVariable(
                    "VQM_RENDER_BACKEND"),
                "gdi",
                StringComparison.OrdinalIgnoreCase))
        {
            SessionDiagnostics.Write(
                "VQM_RENDER_BACKEND=gdi requested; skipping the D3D11 prototype.");
            return false;
        }

        D3D11PresentationLoop? loop =
            null;

        try
        {
            loop =
                new D3D11PresentationLoop(
                    _source.SourceName,
                    _source.X,
                    _source.Y,
                    _source.Width,
                    _source.Height,
                    Handle,
                    ClientSize.Width,
                    ClientSize.Height);

            if (!loop.Start(
                    TimeSpan.FromSeconds(3),
                    out var startupError))
            {
                throw startupError ??
                    new InvalidOperationException(
                        "The D3D11 presentation loop could not start.");
            }

            _d3dLoop =
                loop;

            _usingD3D11 =
                true;

            _d3dLoop.RequestFrame();

            SessionDiagnostics.Write(
                "D3D11 Desktop Duplication presentation thread initialized; DXGI pointer metadata is used and MaximumFrameLatency=1.");

            return true;
        }
        catch (Exception exception)
        {
            loop?.Dispose();

            SessionDiagnostics.Write(
                $"D3D11 renderer unavailable; retaining GDI fallback: {exception}");

            return false;
        }
    }

    private bool EnsureD3D11Healthy()
    {
        if (!_usingD3D11)
        {
            return false;
        }

        var loop =
            _d3dLoop;

        if (loop is not null &&
            !loop.IsFaulted)
        {
            return true;
        }

        var error =
            loop?.LastError;

        SessionDiagnostics.Write(
            $"D3D11 presentation thread failed; switching to GDI fallback: {error}");

        try
        {
            SwitchToGdiFallback();
        }
        catch (Exception fallbackException)
        {
            SessionDiagnostics.Write(
                $"GDI fallback activation failed: {fallbackException}");

            RegisterRenderFailure();
        }

        return false;
    }

    private void SwitchToGdiFallback()
    {
        try
        {
            _d3dLoop?.Dispose();
        }
        catch (Exception exception)
        {
            SessionDiagnostics.Write(
                $"D3D11 cleanup during fallback reported: {exception.Message}");
        }

        _d3dLoop =
            null;

        _usingD3D11 =
            false;

        _cursorTimer.Interval =
            CursorPollIntervalMilliseconds;

        if (!_gdiRenderer.IsDestinationAttached)
        {
            _gdiRenderer.AttachDestination(
                Handle);
        }

        SessionDiagnostics.Write(
            "GDI presentation fallback is active.");
    }

    private void MaybeLogD3D11Stats()
    {
        var loop =
            _d3dLoop;

        if (loop is null)
        {
            return;
        }

        var now =
            DateTimeOffset.UtcNow;

        if (now - _lastD3DStats <
            TimeSpan.FromSeconds(10))
        {
            return;
        }

        _lastD3DStats =
            now;

        var stats =
            loop.Stats;

        using var process =
            Process.GetCurrentProcess();

        var gdiObjects =
            NativeMethods.GetGuiResources(
                process.Handle,
                0);

        SessionDiagnostics.Write(
            $"D3D11 stats: presents={stats.PresentedFrames}; desktopFrames={stats.DesktopFrames}; acquireTimeouts={stats.AcquireTimeouts}; pointerShapes={stats.PointerShapeUpdates}; pointerVisible={stats.PointerVisible}; frameUpdated={stats.LastDesktopFrameUpdated}; frameMs={stats.FrameCostMilliseconds:F2}; smoothMs={stats.SmoothedFrameCostMilliseconds:F2}; privateMB={process.PrivateMemorySize64 / 1048576.0:F1}; workingMB={process.WorkingSet64 / 1048576.0:F1}; handles={process.HandleCount}; gdiObjects={gdiObjects}.");
    }

    private void ApplyDwmIsolation()
    {
        try
        {
            var enabled = 1;

            NativeMethods.DwmSetWindowAttribute(
                Handle,
                DwmwaDisallowPeek,
                ref enabled,
                sizeof(int));

            NativeMethods.DwmSetWindowAttribute(
                Handle,
                DwmwaExcludedFromPeek,
                ref enabled,
                sizeof(int));
        }
        catch
        {
            // Peek isolation is non-fatal.
        }
    }

    private void Cleanup()
    {
        if (_cleanupStarted)
        {
            return;
        }

        _cleanupStarted = true;

        _frameTimer.Stop();
        _cursorTimer.Stop();

        NativeMethods.UnregisterHotKey(
            Handle,
            HotKeyExit);

        NativeMethods.UnregisterHotKey(
            Handle,
            HotKeyMode);

        _portal.Activity -=
            Portal_Activity;

        _portal.Dispose();

        try
        {
            _d3dLoop?.Dispose();
        }
        catch
        {
            // Renderer cleanup must not block session shutdown.
        }

        _d3dLoop =
            null;

        _gdiRenderer.Dispose();

        if (_timerResolutionRaised)
        {
            NativeMethods.timeEndPeriod(1);
            _timerResolutionRaised = false;
        }

        _cursorTimer.Dispose();
        _frameTimer.Dispose();
    }

    private static bool TryMapMouseButton(
        MouseButtons buttons,
        out NativeMouseButton button)
    {
        button = buttons switch
        {
            MouseButtons.Left =>
                NativeMouseButton.Left,

            MouseButtons.Right =>
                NativeMouseButton.Right,

            MouseButtons.Middle =>
                NativeMouseButton.Middle,

            MouseButtons.XButton1 =>
                NativeMouseButton.XButton1,

            MouseButtons.XButton2 =>
                NativeMouseButton.XButton2,

            _ =>
                NativeMouseButton.Left
        };

        return buttons is
            MouseButtons.Left or
            MouseButtons.Right or
            MouseButtons.Middle or
            MouseButtons.XButton1 or
            MouseButtons.XButton2;
    }

    private static class NativeMethods
    {
        [DllImport(
            "user32.dll",
            SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool RegisterHotKey(
            IntPtr window,
            int id,
            uint modifiers,
            uint virtualKey);

        [DllImport(
            "user32.dll",
            SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UnregisterHotKey(
            IntPtr window,
            int id);

        [DllImport(
            "dwmapi.dll")]
        internal static extern int DwmSetWindowAttribute(
            IntPtr window,
            int attribute,
            ref int attributeValue,
            int attributeSize);

        [DllImport(
            "winmm.dll")]
        internal static extern uint timeBeginPeriod(
            uint period);

        [DllImport(
            "winmm.dll")]
        internal static extern uint timeEndPeriod(
            uint period);

        [DllImport(
            "user32.dll")]
        internal static extern uint GetGuiResources(
            IntPtr process,
            uint flags);
    }
}
