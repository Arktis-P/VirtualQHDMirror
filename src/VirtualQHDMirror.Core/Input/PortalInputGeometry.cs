using System;
using System.Collections.Generic;

namespace VirtualQHDMirror.Core.Input;

public readonly record struct PixelPoint(
    int X,
    int Y);

public readonly record struct PixelRect(
    int Left,
    int Top,
    int Width,
    int Height)
{
    public int Right => checked(Left + Width);
    public int Bottom => checked(Top + Height);

    public bool Contains(
        PixelPoint point) =>
        point.X >= Left &&
        point.X < Right &&
        point.Y >= Top &&
        point.Y < Bottom;
}

public enum PortalEdge
{
    None,
    Left,
    Right,
    Top,
    Bottom
}

public sealed class PortalInputGeometry
{
    public const int EntryThreshold = 16;
    public const int UnblockMargin = 40;

    public PortalInputGeometry(
        PixelRect source,
        PixelRect destination)
    {
        ValidateRect(source, nameof(source));
        ValidateRect(destination, nameof(destination));

        Source = source;
        Destination = destination;
    }

    public PixelRect Source { get; }
    public PixelRect Destination { get; }

    public PixelPoint MapDestinationLocalToSource(
        PixelPoint localPoint)
    {
        var sourceX =
            ((localPoint.X + 0.5) *
             Source.Width /
             (double)Destination.Width) -
            0.5;

        var sourceY =
            ((localPoint.Y + 0.5) *
             Source.Height /
             (double)Destination.Height) -
            0.5;

        var x =
            Source.Left +
            (int)Math.Round(sourceX);

        var y =
            Source.Top +
            (int)Math.Round(sourceY);

        return new PixelPoint(
            Math.Clamp(
                x,
                Source.Left,
                Source.Right - 1),
            Math.Clamp(
                y,
                Source.Top,
                Source.Bottom - 1));
    }

    public PixelPoint MapSourceToDestination(
        PixelPoint sourcePoint)
    {
        var destinationX =
            ((sourcePoint.X - Source.Left + 0.5) *
             Destination.Width /
             (double)Source.Width) -
            0.5;

        var destinationY =
            ((sourcePoint.Y - Source.Top + 0.5) *
             Destination.Height /
             (double)Source.Height) -
            0.5;

        var x =
            Destination.Left +
            (int)Math.Round(destinationX);

        var y =
            Destination.Top +
            (int)Math.Round(destinationY);

        return new PixelPoint(
            Math.Clamp(
                x,
                Destination.Left,
                Destination.Right - 1),
            Math.Clamp(
                y,
                Destination.Top,
                Destination.Bottom - 1));
    }

    public PixelPoint DestinationScreenPointFromLocal(
        PixelPoint localPoint) =>
        new(
            Destination.Left +
            Math.Clamp(
                localPoint.X,
                0,
                Destination.Width - 1),
            Destination.Top +
            Math.Clamp(
                localPoint.Y,
                0,
                Destination.Height - 1));

    public bool IsSourcePoint(
        PixelPoint point) =>
        Source.Contains(point);

    public PortalEdge DetectEntryEdge(
        PixelPoint destinationLocalPoint)
    {
        if (destinationLocalPoint.X <= EntryThreshold)
        {
            return PortalEdge.Left;
        }

        if (destinationLocalPoint.X >=
            Destination.Width - 1 - EntryThreshold)
        {
            return PortalEdge.Right;
        }

        if (destinationLocalPoint.Y <= EntryThreshold)
        {
            return PortalEdge.Top;
        }

        if (destinationLocalPoint.Y >=
            Destination.Height - 1 - EntryThreshold)
        {
            return PortalEdge.Bottom;
        }

        return PortalEdge.None;
    }

    public PortalEdge UpdateBlockedEdge(
        PortalEdge blockedEdge,
        PixelPoint sourcePoint) =>
        blockedEdge switch
        {
            PortalEdge.Left
                when sourcePoint.X >=
                     Source.Left + UnblockMargin =>
                PortalEdge.None,

            PortalEdge.Right
                when sourcePoint.X <=
                     Source.Right - 1 - UnblockMargin =>
                PortalEdge.None,

            PortalEdge.Top
                when sourcePoint.Y >=
                     Source.Top + UnblockMargin =>
                PortalEdge.None,

            PortalEdge.Bottom
                when sourcePoint.Y <=
                     Source.Bottom - 1 - UnblockMargin =>
                PortalEdge.None,

            _ => blockedEdge
        };

    public PortalEdge DetectExitEdge(
        PixelPoint sourcePoint,
        PortalEdge blockedEdge)
    {
        if (blockedEdge != PortalEdge.Left &&
            sourcePoint.X <= Source.Left + 1)
        {
            return PortalEdge.Left;
        }

        if (blockedEdge != PortalEdge.Right &&
            sourcePoint.X >= Source.Right - 2)
        {
            return PortalEdge.Right;
        }

        if (blockedEdge != PortalEdge.Top &&
            sourcePoint.Y <= Source.Top + 1)
        {
            return PortalEdge.Top;
        }

        if (blockedEdge != PortalEdge.Bottom &&
            sourcePoint.Y >= Source.Bottom - 2)
        {
            return PortalEdge.Bottom;
        }

        return PortalEdge.None;
    }

    public bool TryGetExitTarget(
        PortalEdge edge,
        PixelPoint sourcePoint,
        IReadOnlyList<PixelRect> logicalMonitorRects,
        out PixelPoint target)
    {
        ArgumentNullException.ThrowIfNull(
            logicalMonitorRects);

        target = MapSourceToDestination(
            sourcePoint);

        target = edge switch
        {
            PortalEdge.Left =>
                target with
                {
                    X = Destination.Left - 1
                },

            PortalEdge.Right =>
                target with
                {
                    X = Destination.Right
                },

            PortalEdge.Top =>
                target with
                {
                    Y = Destination.Top - 1
                },

            PortalEdge.Bottom =>
                target with
                {
                    Y = Destination.Bottom
                },

            _ => target
        };

        if (edge == PortalEdge.None)
        {
            return false;
        }

        if (TryFindContainingMonitor(
                target,
                logicalMonitorRects,
                out _))
        {
            return true;
        }

        // The selected source display is a hidden backing workspace from the
        // user's point of view. If it physically occupies the pixel directly
        // beyond the destination edge, step through it once and look for a
        // visible monitor on the far side. This keeps the Windows display
        // topology intact while making the pointer topology behave as though
        // the backing display did not exist.
        if (!Source.Contains(
                target))
        {
            return false;
        }

        var beyondBackingSource =
            edge switch
            {
                PortalEdge.Left =>
                    target with
                    {
                        X = Source.Left - 1
                    },

                PortalEdge.Right =>
                    target with
                    {
                        X = Source.Right
                    },

                PortalEdge.Top =>
                    target with
                    {
                        Y = Source.Top - 1
                    },

                PortalEdge.Bottom =>
                    target with
                    {
                        Y = Source.Bottom
                    },

                _ => target
            };

        if (!TryFindContainingMonitor(
                beyondBackingSource,
                logicalMonitorRects,
                out var neighbor))
        {
            return false;
        }

        target =
            edge switch
            {
                PortalEdge.Left =>
                    new PixelPoint(
                        neighbor.Right - 1,
                        Math.Clamp(
                            beyondBackingSource.Y,
                            neighbor.Top,
                            neighbor.Bottom - 1)),

                PortalEdge.Right =>
                    new PixelPoint(
                        neighbor.Left,
                        Math.Clamp(
                            beyondBackingSource.Y,
                            neighbor.Top,
                            neighbor.Bottom - 1)),

                PortalEdge.Top =>
                    new PixelPoint(
                        Math.Clamp(
                            beyondBackingSource.X,
                            neighbor.Left,
                            neighbor.Right - 1),
                        neighbor.Bottom - 1),

                PortalEdge.Bottom =>
                    new PixelPoint(
                        Math.Clamp(
                            beyondBackingSource.X,
                            neighbor.Left,
                            neighbor.Right - 1),
                        neighbor.Top),

                _ => beyondBackingSource
            };

        return true;
    }

    private static bool TryFindContainingMonitor(
        PixelPoint point,
        IReadOnlyList<PixelRect> monitorRects,
        out PixelRect monitor)
    {
        foreach (var candidate in monitorRects)
        {
            if (candidate.Contains(
                    point))
            {
                monitor = candidate;
                return true;
            }
        }

        monitor = default;
        return false;
    }

    private static void ValidateRect(
        PixelRect rect,
        string parameterName)
    {
        if (rect.Width <= 0 ||
            rect.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Pixel rectangles must have positive dimensions.");
        }
    }
}
