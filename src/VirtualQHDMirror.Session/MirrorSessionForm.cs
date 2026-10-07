using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
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

    private readonly DisplayEndpoint _source;
    private readonly DisplayEndpoint _destination;
    private readonly GdiMirrorRenderer _renderer;
    private readonly WindowsPointerInjector _pointer;
    private readonly PortalInputController _portal;
    private readonly System.Windows.Forms.Timer _timer;

    private MirrorRenderMode _renderMode;
    private DateTimeOffset _lastInput =
        DateTimeOffset.UtcNow;

    private int _lastInterval = -1;
    private bool _timerResolutionRaised;
    private bool _cleanupStarted;
    private int _consecutiveRenderFailures;

    public MirrorSessionForm(
        DisplayEndpoint source,
        DisplayEndpoint destination,
        IReadOnlyList<PixelRect> activeMonitorRects,
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
                activeMonitorRects,
                _pointer);

        _portal.Activity +=
            Portal_Activity;

        _renderer =
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

        _timer =
            new System.Windows.Forms.Timer();

        _timer.Tick +=
            Timer_Tick;

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
                            MirrorRenderMode.Experimental120,

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

        if (!_portal.IsActive)
        {
            _portal.Begin(
                local);
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
            Application.DoEvents();
            Thread.Sleep(10);

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
            Application.DoEvents();
            Thread.Sleep(10);
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

        _renderer.AttachDestination(
            Handle);

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

        ApplyInterval(16);
        _timer.Start();
    }

    private void MirrorSessionForm_FormClosing(
        object? sender,
        FormClosingEventArgs e)
    {
        Cleanup();
    }

    private void Timer_Tick(
        object? sender,
        EventArgs e)
    {
        AdjustInterval();
        _portal.Update();
        DrawFrame();
    }

    private void DrawFrame()
    {
        if (_cleanupStarted ||
            ClientSize.Width <= 0 ||
            ClientSize.Height <= 0)
        {
            return;
        }

        GdiRenderFrameResult result;

        try
        {
            result =
                _renderer.RenderFrame(
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
        Close();
    }

    private void AdjustInterval()
    {
        var idleTime =
            DateTimeOffset.UtcNow -
            _lastInput;

        ApplyInterval(
            MirrorFrameTimingPolicy
                .GetIntervalMilliseconds(
                    _renderMode,
                    idleTime));
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

        _timer.Interval =
            milliseconds;
    }

    private void MarkInput()
    {
        _lastInput =
            DateTimeOffset.UtcNow;

        if (_renderMode ==
            MirrorRenderMode.Adaptive)
        {
            ApplyInterval(16);
        }
    }

    private void Portal_Activity(
        object? sender,
        EventArgs e)
    {
        MarkInput();
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

        _timer.Stop();

        NativeMethods.UnregisterHotKey(
            Handle,
            HotKeyExit);

        NativeMethods.UnregisterHotKey(
            Handle,
            HotKeyMode);

        _portal.Activity -=
            Portal_Activity;

        _portal.Dispose();
        _renderer.Dispose();

        if (_timerResolutionRaised)
        {
            NativeMethods.timeEndPeriod(1);
            _timerResolutionRaised = false;
        }

        _timer.Dispose();
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
    }
}
