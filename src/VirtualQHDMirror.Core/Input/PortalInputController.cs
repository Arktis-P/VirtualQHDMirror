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
    private const int PrecisionCompensationThresholdPixels = 6;
    private const double MaximumPrecisionGain = 1.5;

    private readonly PortalInputGeometry _geometry;
    private readonly IReadOnlyList<PixelRect> _activeMonitorRects;
    private readonly WindowsPointerInjector _pointer;

    private PortalEdge _blockedEdge;
    private PixelPoint _lastPortalCursor;
    private bool _lastPortalCursorValid;
    private PixelPoint _lastDestinationLocal;
    private double _precisionResidualX;
    private double _precisionResidualY;
    private bool _disposed;

    public PortalInputController(
        PortalInputGeometry geometry,
        IReadOnlyList<PixelRect> activeMonitorRects,
        WindowsPointerInjector? pointer = null)
    {
        _geometry = geometry ??
            throw new ArgumentNullException(
                nameof(geometry));

        _activeMonitorRects = activeMonitorRects ??
            throw new ArgumentNullException(
                nameof(activeMonitorRects));

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

        if (!_pointer.MoveAbsolute(
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

        sourcePoint =
            ApplyPrecisionCompensation(
                sourcePoint);

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
                _activeMonitorRects,
                out var exitTarget))
        {
            _pointer.ReleaseClip();
            ResetState();
            _pointer.MoveAbsolute(
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
        _pointer.MoveAbsolute(
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

    private PixelPoint ApplyPrecisionCompensation(
        PixelPoint sourcePoint)
    {
        if (!_lastPortalCursorValid)
        {
            ResetPrecisionResidual();
            return sourcePoint;
        }

        var deltaX =
            sourcePoint.X -
            _lastPortalCursor.X;

        var deltaY =
            sourcePoint.Y -
            _lastPortalCursor.Y;

        if (deltaX == 0 &&
            deltaY == 0)
        {
            return sourcePoint;
        }

        var movementMagnitudeSquared =
            ((long)deltaX * deltaX) +
            ((long)deltaY * deltaY);

        var precisionThresholdSquared =
            (long)PrecisionCompensationThresholdPixels *
            PrecisionCompensationThresholdPixels;

        if (movementMagnitudeSquared >
            precisionThresholdSquared)
        {
            ResetPrecisionResidual();
            return sourcePoint;
        }

        var gainX =
            Math.Clamp(
                _geometry.Source.Width /
                (double)_geometry.Destination.Width,
                1.0,
                MaximumPrecisionGain);

        var gainY =
            Math.Clamp(
                _geometry.Source.Height /
                (double)_geometry.Destination.Height,
                1.0,
                MaximumPrecisionGain);

        if (gainX <= 1.0 &&
            gainY <= 1.0)
        {
            ResetPrecisionResidual();
            return sourcePoint;
        }

        var extraXWithResidual =
            (deltaX * (gainX - 1.0)) +
            _precisionResidualX;

        var extraYWithResidual =
            (deltaY * (gainY - 1.0)) +
            _precisionResidualY;

        var extraX =
            (int)Math.Round(
                extraXWithResidual,
                MidpointRounding.AwayFromZero);

        var extraY =
            (int)Math.Round(
                extraYWithResidual,
                MidpointRounding.AwayFromZero);

        _precisionResidualX =
            extraXWithResidual -
            extraX;

        _precisionResidualY =
            extraYWithResidual -
            extraY;

        if (extraX == 0 &&
            extraY == 0)
        {
            return sourcePoint;
        }

        var adjustedPoint =
            new PixelPoint(
                Math.Clamp(
                    sourcePoint.X + extraX,
                    _geometry.Source.Left,
                    _geometry.Source.Right - 1),
                Math.Clamp(
                    sourcePoint.Y + extraY,
                    _geometry.Source.Top,
                    _geometry.Source.Bottom - 1));

        if (adjustedPoint ==
            sourcePoint)
        {
            ResetPrecisionResidual();
            return sourcePoint;
        }

        if (!_pointer.MoveAbsolute(
                adjustedPoint))
        {
            ResetPrecisionResidual();
            return sourcePoint;
        }

        return adjustedPoint;
    }

    private void ResetPrecisionResidual()
    {
        _precisionResidualX = 0;
        _precisionResidualY = 0;
    }

    private void ResetState()
    {
        IsActive = false;
        _blockedEdge = PortalEdge.None;
        _lastPortalCursorValid = false;
        ResetPrecisionResidual();
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
