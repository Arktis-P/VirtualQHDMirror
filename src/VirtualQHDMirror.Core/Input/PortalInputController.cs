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

    private PortalEdge _blockedEdge;
    private PixelPoint _lastPortalCursor;
    private bool _lastPortalCursorValid;
    private PixelPoint _lastDestinationLocal;
    private bool _disposed;

    public PortalInputController(
        PortalInputGeometry geometry,
        IReadOnlyList<PixelRect> logicalMonitorRects,
        WindowsPointerInjector? pointer = null)
    {
        _geometry = geometry ??
            throw new ArgumentNullException(
                nameof(geometry));

        _logicalMonitorRects = logicalMonitorRects ??
            throw new ArgumentNullException(
                nameof(logicalMonitorRects));

        _pointer = pointer ??
            new WindowsPointerInjector();
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

        if (!MoveTopologyTransition(
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

        if (!_pointer.ClipTo(
                _geometry.Source))
        {
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
                sourcePoint))
        {
            _pointer.ReleaseClip();
            ResetState();
            return false;
        }

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

        if (!_pointer.TryGetCursorPosition(
                out var sourcePoint))
        {
            return PortalUpdateState.Active;
        }

        if (!_geometry.IsSourcePoint(
                sourcePoint))
        {
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
            _pointer.ReleaseClip();
            ResetState();
            MoveTopologyTransition(
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

        _pointer.ReleaseClip();
        ResetState();
        MoveTopologyTransition(
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

    private bool MoveTopologyTransition(
        PixelPoint point) =>
        _pointer.SetPosition(
            point) ||
        _pointer.MoveAbsolute(
            point);

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
