using System;
using System.Threading;

namespace VirtualQHDMirror.Session;

internal readonly record struct D3D11PresentationStats(
    long PresentedFrames,
    long DesktopFrames,
    long AcquireTimeouts,
    long PointerShapeUpdates,
    bool PointerVisible,
    bool LastDesktopFrameUpdated,
    double FrameCostMilliseconds,
    double SmoothedFrameCostMilliseconds);

internal sealed class D3D11PresentationLoop : IDisposable
{
    private readonly string _sourceDeviceName;
    private readonly int _sourceLeft;
    private readonly int _sourceTop;
    private readonly int _sourceWidth;
    private readonly int _sourceHeight;
    private readonly IntPtr _destinationWindow;
    private readonly int _destinationWidth;
    private readonly int _destinationHeight;
    private readonly AutoResetEvent _wake = new(false);
    private readonly ManualResetEventSlim _startup = new(false);
    private readonly object _stateGate = new();

    private Thread? _thread;
    private D3D11PresentationStats _stats;
    private Exception? _lastError;
    private int _stopRequested;
    private int _continuous;
    private int _faulted;
    private bool _disposed;

    public D3D11PresentationLoop(
        string sourceDeviceName,
        int sourceLeft,
        int sourceTop,
        int sourceWidth,
        int sourceHeight,
        IntPtr destinationWindow,
        int destinationWidth,
        int destinationHeight)
    {
        _sourceDeviceName = sourceDeviceName;
        _sourceLeft = sourceLeft;
        _sourceTop = sourceTop;
        _sourceWidth = sourceWidth;
        _sourceHeight = sourceHeight;
        _destinationWindow = destinationWindow;
        _destinationWidth = destinationWidth;
        _destinationHeight = destinationHeight;
    }

    public bool IsFaulted =>
        Volatile.Read(ref _faulted) != 0;

    public Exception? LastError
    {
        get
        {
            lock (_stateGate)
            {
                return _lastError;
            }
        }
    }

    public D3D11PresentationStats Stats
    {
        get
        {
            lock (_stateGate)
            {
                return _stats;
            }
        }
    }

    public bool Start(
        TimeSpan timeout,
        out Exception? startupError)
    {
        EnsureNotDisposed();

        if (_thread is not null)
        {
            throw new InvalidOperationException(
                "The D3D11 presentation loop has already been started.");
        }

        _thread =
            new Thread(ThreadMain)
            {
                IsBackground = true,
                Name = "VirtualQHDMirror D3D11 Presentation"
            };

        _thread.Start();

        if (!_startup.Wait(timeout))
        {
            startupError =
                new TimeoutException(
                    "The D3D11 presentation thread did not initialize in time.");

            StopThread();
            return false;
        }

        startupError = LastError;
        return !IsFaulted;
    }

    public void RequestFrame()
    {
        if (_disposed ||
            IsFaulted)
        {
            return;
        }

        _wake.Set();
    }

    public void SetContinuous(
        bool enabled)
    {
        if (_disposed ||
            IsFaulted)
        {
            return;
        }

        Interlocked.Exchange(
            ref _continuous,
            enabled ? 1 : 0);

        _wake.Set();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopThread();

        if (_thread is null ||
            !_thread.IsAlive)
        {
            _startup.Dispose();
            _wake.Dispose();
        }
    }

    private void ThreadMain()
    {
        D3D11MirrorRenderer? renderer =
            null;

        try
        {
            renderer =
                new D3D11MirrorRenderer(
                    _sourceDeviceName,
                    _sourceLeft,
                    _sourceTop,
                    _sourceWidth,
                    _sourceHeight);

            renderer.AttachDestination(
                _destinationWindow,
                _destinationWidth,
                _destinationHeight);

            _startup.Set();

            var firstFrame =
                true;

            while (Volatile.Read(
                       ref _stopRequested) == 0)
            {
                if (!firstFrame &&
                    Volatile.Read(
                        ref _continuous) == 0)
                {
                    _wake.WaitOne();
                }

                if (Volatile.Read(
                        ref _stopRequested) != 0)
                {
                    break;
                }

                firstFrame = false;

                var result =
                    renderer.RenderFrame(
                        _destinationWidth,
                        _destinationHeight);

                lock (_stateGate)
                {
                    _stats =
                        new D3D11PresentationStats(
                            renderer.PresentedFrames,
                            renderer.DesktopFrames,
                            renderer.AcquireTimeouts,
                            renderer.PointerShapeUpdates,
                            renderer.PointerVisible,
                            result.DesktopFrameUpdated,
                            result.FrameCostMilliseconds,
                            result.SmoothedFrameCostMilliseconds);
                }
            }
        }
        catch (Exception exception)
        {
            lock (_stateGate)
            {
                _lastError =
                    exception;
            }

            Interlocked.Exchange(
                ref _faulted,
                1);

            _startup.Set();
        }
        finally
        {
            try
            {
                renderer?.Dispose();
            }
            catch
            {
                // The render thread is already shutting down.
            }

            _startup.Set();
        }
    }

    private void StopThread()
    {
        Interlocked.Exchange(
            ref _stopRequested,
            1);

        _wake.Set();

        var thread =
            _thread;

        if (thread is null ||
            thread ==
            Thread.CurrentThread)
        {
            return;
        }

        thread.Join(
            millisecondsTimeout: 2000);
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(
                nameof(D3D11PresentationLoop));
        }
    }
}
