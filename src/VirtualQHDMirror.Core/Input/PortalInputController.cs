using System;
using System.Collections.Generic;

namespace VirtualQHDMirror.Core.Input;

public enum PortalUpdateState
{
    Inactive,
    Active,
    Exited,
    LostSource
}

public sealed class PortalInputController : IDisposable
{
    private readonly PortalInputGeometry _geometry;
    private readonly IReadOnlyList<PixelRect> _logicalMonitorRects;
    private readonly WindowsPointerInjector _pointer;
    private readonly Action<string>? _diagnostics;

    private PortalEdge _blockedEdge;
    private PixelPoint _lastPortalCursor;
    private bool _lastPortalCursorValid;
    private PixelPoint _lastDestinationLocal;
    private bool _disposed;

    public PortalInputController(
        PortalInputGeometry geometry,
        IReadOnlyList<PixelRect> logicalMonitorRects,
        WindowsPointerInjector? pointer = null,
        Action<string>? diagnostics = null)
    {
        _geometry = geometry ??
            throw new ArgumentNullException(
                nameof(geometry));

        _logicalMonitorRects = logicalMonitorRects ??
            throw new ArgumentNullException(
                nameof(logicalMonitorRects));

        _pointer = pointer ??
            new WindowsPointerInjector();

        _diagnostics = diagnostics;
    }

    public event EventHandler? Activity;

    public bool IsActive { get; private set; }

    public PortalEdge BlockedEdge =>
        _blockedEdge;

    public void UpdateDestinationLocal(
        PixelPoint destinationLocal)
    {
        EnsureNotDisposed();
        _lastDestinationLocal =
            destinationLocal;
    }

    public bool RedirectBackingSourceIfInactive()
    {
        EnsureNotDisposed();

        if (IsActive ||
            !_pointer.TryGetCursorPosition(
                out var current) ||
            !_geometry.IsSourcePoint(
                current))
        {
            return false;
        }

        var destinationPoint =
            _geometry.MapSourceToDestination(
                current);

        Trace(
            $"guard redirect source={FormatPoint(current)} target={FormatPoint(destinationPoint)}");

        if (!MoveTopologyTransition(
                "guard-source-to-destination",
                destinationPoint))
        {
            return false;
        }

        _lastDestinationLocal =
            new PixelPoint(
                destinationPoint.X -
                    _geometry.Destination.Left,
                destinationPoint.Y -
                    _geometry.Destination.Top);

        RaiseActivity();
        return true;
    }

    public bool BeginFromCurrentSource()
    {
        EnsureNotDisposed();

        if (IsActive)
        {
            return true;
        }

        if (!_pointer.TryGetCursorPosition(
                out var sourcePoint) ||
            !_geometry.IsSourcePoint(
                sourcePoint))
        {
            return false;
        }

        var destinationPoint =
            _geometry.MapSourceToDestination(
                sourcePoint);

        var destinationLocal =
            new PixelPoint(
                destinationPoint.X -
                    _geometry.Destination.Left,
                destinationPoint.Y -
                    _geometry.Destination.Top);

        Trace(
            $"begin-from-source source={FormatPoint(sourcePoint)} destinationLocal={FormatPoint(destinationLocal)}");

        return Begin(
            destinationLocal);
    }

    public bool Begin(
        PixelPoint destinationLocal)
    {
        EnsureNotDisposed();

        _lastDestinationLocal =
            destinationLocal;

        if (IsActive)
        {
            return true;
        }

        var sourcePoint =
            _geometry.MapDestinationLocalToSource(
                destinationLocal);

        Trace(
            $"begin request local={FormatPoint(destinationLocal)} mappedSource={FormatPoint(sourcePoint)} edge={_geometry.DetectEntryEdge(destinationLocal)}");

        if (!_pointer.ClipTo(
                _geometry.Source))
        {
            Trace(
                "begin failed: ClipCursor(source) returned false.");
            return false;
        }

        IsActive = true;
        _blockedEdge =
            _geometry.DetectEntryEdge(
                destinationLocal);

        _lastPortalCursor =
            sourcePoint;

        _lastPortalCursorValid = true;

        if (!MoveTopologyTransition(
                "portal-begin",
                sourcePoint))
        {
            _pointer.ReleaseClip();
            ResetState();
            Trace(
                "begin failed: pointer warp to source failed.");
            return false;
        }

        Trace(
            $"begin active blockedEdge={_blockedEdge}");

        RaiseActivity();
        return true;
    }

    public PortalUpdateState Update()
    {
        EnsureNotDisposed();

        if (!IsActive)
        {
            return PortalUpdateState.Inactive;
        }

        RestoreSourceClipIfNeeded();

        if (!_pointer.TryGetCursorPosition(
                out var sourcePoint))
        {
            return PortalUpdateState.Active;
        }

        if (!_geometry.IsSourcePoint(
                sourcePoint))
        {
            Trace(
                $"lost-source actual={FormatPoint(sourcePoint)} blockedEdge={_blockedEdge}");

            _pointer.ReleaseClip();
            ResetState();
            return PortalUpdateState.LostSource;
        }

        _blockedEdge =
            _geometry.UpdateBlockedEdge(
                _blockedEdge,
                sourcePoint);

        var exitEdge =
            _geometry.DetectExitEdge(
                sourcePoint,
                _blockedEdge);

        if (exitEdge != PortalEdge.None &&
            _geometry.TryGetExitTarget(
                exitEdge,
                sourcePoint,
                _logicalMonitorRects,
                out var exitTarget))
        {
            Trace(
                $"exit edge={exitEdge} source={FormatPoint(sourcePoint)} target={FormatPoint(exitTarget)} blockedEdge={_blockedEdge}");

            _pointer.ReleaseClip();
            ResetState();

            MoveTopologyTransition(
                $"portal-exit-{exitEdge}",
                exitTarget);

            RaiseActivity();
            return PortalUpdateState.Exited;
        }

        if (!_lastPortalCursorValid ||
            sourcePoint != _lastPortalCursor)
        {
            _lastPortalCursor =
                sourcePoint;

            _lastPortalCursorValid = true;
            RaiseActivity();
        }

        return PortalUpdateState.Active;
    }

    public void EndToDestination()
    {
        EnsureNotDisposed();

        if (!IsActive)
        {
            return;
        }

        var destinationPoint =
            _geometry.DestinationScreenPointFromLocal(
                _lastDestinationLocal);

        if (_pointer.TryGetCursorPosition(
                out var current) &&
            _geometry.IsSourcePoint(
                current))
        {
            destinationPoint =
                _geometry.MapSourceToDestination(
                    current);
        }

        Trace(
            $"end-to-destination target={FormatPoint(destinationPoint)}");

        _pointer.ReleaseClip();
        ResetState();

        MoveTopologyTransition(
            "portal-end",
            destinationPoint);

        RaiseActivity();
    }

    public bool SendButtonDown(
        NativeMouseButton button)
    {
        EnsureNotDisposed();
        RaiseActivity();

        return _pointer.SendButton(
            button,
            down: true);
    }

    public bool SendWheel(
        int delta)
    {
        EnsureNotDisposed();
        RaiseActivity();

        return _pointer.SendWheel(
            delta);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (IsActive)
        {
            EndToDestination();
        }

        _disposed = true;
    }

    private void RestoreSourceClipIfNeeded()
    {
        if (!_pointer.TryGetClipRect(
                out var currentClip))
        {
            Trace(
                "active clip check unavailable.");
            return;
        }

        if (currentClip ==
            _geometry.Source)
        {
            return;
        }

        Trace(
            $"active clip lost; expected={FormatRect(_geometry.Source)} actual={FormatRect(currentClip)}; restoring source clip.");

        if (_pointer.ClipTo(
                _geometry.Source))
        {
            Trace(
                "active clip restored to backing source.");
        }
        else
        {
            Trace(
                "active clip restore failed.");
        }
    }

    private bool MoveTopologyTransition(
        string reason,
        PixelPoint point)
    {
        var hadBefore =
            _pointer.TryGetCursorPosition(
                out var before);

        var setPositionSucceeded =
            _pointer.SetPosition(
                point);

        var fallbackSucceeded =
            false;

        if (!setPositionSucceeded)
        {
            fallbackSucceeded =
                _pointer.MoveAbsolute(
                    point);
        }

        var hadAfter =
            _pointer.TryGetCursorPosition(
                out var after);

        Trace(
            $"warp reason={reason}; before={(hadBefore ? FormatPoint(before) : "unavailable")}; target={FormatPoint(point)}; setCursorPos={setPositionSucceeded}; fallbackSendInput={fallbackSucceeded}; after={(hadAfter ? FormatPoint(after) : "unavailable")}");

        return setPositionSucceeded ||
               fallbackSucceeded;
    }

    private void Trace(
        string message) =>
        _diagnostics?.Invoke(
            message);

    private static string FormatPoint(
        PixelPoint point) =>
        $"({point.X},{point.Y})";

    private static string FormatRect(
        PixelRect rect) =>
        $"({rect.Left},{rect.Top}) {rect.Width}x{rect.Height}";

    private void ResetState()
    {
        IsActive = false;
        _blockedEdge = PortalEdge.None;
        _lastPortalCursorValid = false;
    }

    private void RaiseActivity() =>
        Activity?.Invoke(
            this,
            EventArgs.Empty);

    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(
                nameof(PortalInputController));
        }
    }
}
