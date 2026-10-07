using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace VirtualQHDMirror.App;

internal enum NativeMirrorSessionState
{
    None,
    Attached,
    Multiple
}

internal sealed class NativeMirrorSessionProcessController : IDisposable
{
    private const string SessionWindowTitle =
        "VirtualQHDMirror .NET Session";

    private const uint WmClose = 0x0010;

    private readonly object _gate = new();
    private Process? _process;
    private int _detectedSessionCount;
    private bool _attachedFromDiscovery;

    public event EventHandler? StateChanged;

    public bool IsRunning
    {
        get
        {
            lock (_gate)
            {
                return _process is
                {
                    HasExited: false
                };
            }
        }
    }

    public int? ProcessId
    {
        get
        {
            lock (_gate)
            {
                return _process is
                {
                    HasExited: false
                } process
                    ? process.Id
                    : null;
            }
        }
    }

    public int DetectedSessionCount
    {
        get
        {
            lock (_gate)
            {
                return _detectedSessionCount;
            }
        }
    }

    public bool AttachedFromDiscovery
    {
        get
        {
            lock (_gate)
            {
                return _process is
                {
                    HasExited: false
                } &&
                _attachedFromDiscovery;
            }
        }
    }

    public NativeMirrorSessionState SessionState
    {
        get
        {
            lock (_gate)
            {
                if (_process is
                    {
                        HasExited: false
                    })
                {
                    return NativeMirrorSessionState.Attached;
                }

                return _detectedSessionCount > 1
                    ? NativeMirrorSessionState.Multiple
                    : NativeMirrorSessionState.None;
            }
        }
    }

    public NativeMirrorSessionState DetectExistingSession()
    {
        lock (_gate)
        {
            if (_process is
                {
                    HasExited: false
                })
            {
                _detectedSessionCount = 1;
                return NativeMirrorSessionState.Attached;
            }

            ClearTrackedProcessLocked();
        }

        var processIds =
            FindSessionProcessIds();

        if (processIds.Count != 1)
        {
            lock (_gate)
            {
                _detectedSessionCount =
                    processIds.Count;

                _attachedFromDiscovery =
                    false;
            }

            StateChanged?.Invoke(
                this,
                EventArgs.Empty);

            return processIds.Count > 1
                ? NativeMirrorSessionState.Multiple
                : NativeMirrorSessionState.None;
        }

        Process? process = null;

        try
        {
            process =
                Process.GetProcessById(
                    processIds[0]);

            if (process.HasExited)
            {
                process.Dispose();
                process = null;

                lock (_gate)
                {
                    _detectedSessionCount = 0;
                    _attachedFromDiscovery = false;
                }

                return NativeMirrorSessionState.None;
            }

            process.EnableRaisingEvents = true;
            process.Exited +=
                SessionProcess_Exited;

            lock (_gate)
            {
                ClearTrackedProcessLocked();
                _process = process;
                _detectedSessionCount = 1;
                _attachedFromDiscovery = true;
                process = null;
            }

            StateChanged?.Invoke(
                this,
                EventArgs.Empty);

            return NativeMirrorSessionState.Attached;
        }
        catch (Exception exception)
            when (exception is
                ArgumentException or
                InvalidOperationException or
                Win32Exception)
        {
            process?.Dispose();

            lock (_gate)
            {
                _detectedSessionCount = 0;
                _attachedFromDiscovery = false;
            }

            return NativeMirrorSessionState.None;
        }
        finally
        {
            process?.Dispose();
        }
    }

    public void Start(
        string sourceDevice,
        string destinationDevice)
    {
        if (string.IsNullOrWhiteSpace(
                sourceDevice) ||
            string.IsNullOrWhiteSpace(
                destinationDevice))
        {
            throw new ArgumentException(
                "Source and destination device names are required.");
        }

        var existingState =
            DetectExistingSession();

        if (existingState !=
            NativeMirrorSessionState.None)
        {
            throw new InvalidOperationException(
                existingState ==
                NativeMirrorSessionState.Attached
                    ? "An existing .NET mirror session was found and attached."
                    : $"{DetectedSessionCount} .NET mirror sessions are already running.");
        }

        var executablePath =
            ResolveSessionExecutablePath();

        var startInfo =
            new ProcessStartInfo
            {
                FileName =
                    executablePath,
                WorkingDirectory =
                    Path.GetDirectoryName(
                        executablePath) ??
                    AppContext.BaseDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            };

        startInfo.ArgumentList.Add(
            "--source-device");

        startInfo.ArgumentList.Add(
            sourceDevice);

        startInfo.ArgumentList.Add(
            "--destination-device");

        startInfo.ArgumentList.Add(
            destinationDevice);

        startInfo.ArgumentList.Add(
            "--render-mode");

        startInfo.ArgumentList.Add(
            "adaptive");

        var process =
            new Process
            {
                StartInfo =
                    startInfo,
                EnableRaisingEvents =
                    true
            };

        process.Exited +=
            SessionProcess_Exited;

        if (!process.Start())
        {
            process.Exited -=
                SessionProcess_Exited;

            process.Dispose();

            throw new InvalidOperationException(
                "The .NET mirror session process did not start.");
        }

        lock (_gate)
        {
            ClearTrackedProcessLocked();
            _process = process;
            _detectedSessionCount = 1;
            _attachedFromDiscovery = false;
        }

        StateChanged?.Invoke(
            this,
            EventArgs.Empty);
    }

    public bool RequestStop()
    {
        if (!IsRunning)
        {
            DetectExistingSession();
        }

        Process? process;

        lock (_gate)
        {
            process = _process;

            if (process is null ||
                process.HasExited)
            {
                return false;
            }
        }

        var posted = false;

        NativeMethods.EnumWindows(
            (window, _) =>
            {
                NativeMethods.GetWindowThreadProcessId(
                    window,
                    out var processId);

                if (processId ==
                    (uint)process.Id)
                {
                    posted |=
                        NativeMethods.PostMessage(
                            window,
                            WmClose,
                            IntPtr.Zero,
                            IntPtr.Zero);
                }

                return true;
            },
            IntPtr.Zero);

        return posted;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            ClearTrackedProcessLocked();
            _detectedSessionCount = 0;
            _attachedFromDiscovery = false;
        }
    }

    private void SessionProcess_Exited(
        object? sender,
        EventArgs e)
    {
        lock (_gate)
        {
            if (sender is Process process &&
                ReferenceEquals(
                    _process,
                    process))
            {
                process.Exited -=
                    SessionProcess_Exited;

                process.Dispose();
                _process = null;
                _detectedSessionCount = 0;
                _attachedFromDiscovery = false;
            }
        }

        StateChanged?.Invoke(
            this,
            EventArgs.Empty);
    }

    private void ClearTrackedProcessLocked()
    {
        if (_process is null)
        {
            return;
        }

        _process.Exited -=
            SessionProcess_Exited;

        _process.Dispose();
        _process = null;
    }

    private static string ResolveSessionExecutablePath()
    {
        const string executableName =
            "VirtualQHDMirror.Session.exe";

        var installedCandidate =
            Path.Combine(
                AppContext.BaseDirectory,
                executableName);

        if (File.Exists(
                installedCandidate))
        {
            return installedCandidate;
        }

        var repositoryRoot =
            Environment.GetEnvironmentVariable(
                "VQM_REPO_ROOT");

        if (!string.IsNullOrWhiteSpace(
                repositoryRoot))
        {
            foreach (var configuration in
                     new[]
                     {
                         "Debug",
                         "Release"
                     })
            {
                var candidate =
                    Path.Combine(
                        repositoryRoot,
                        "src",
                        "VirtualQHDMirror.Session",
                        "bin",
                        configuration,
                        "net8.0-windows",
                        executableName);

                if (File.Exists(
                        candidate))
                {
                    return candidate;
                }
            }
        }

        throw new FileNotFoundException(
            "VirtualQHDMirror.Session.exe was not found. Rebuild the v0.20 solution before using the .NET preview engine.");
    }

    private static IReadOnlyList<int> FindSessionProcessIds()
    {
        var processIds =
            new HashSet<int>();

        NativeMethods.EnumWindows(
            (window, _) =>
            {
                var titleLength =
                    NativeMethods.GetWindowTextLength(
                        window);

                if (titleLength <= 0)
                {
                    return true;
                }

                var title =
                    new StringBuilder(
                        titleLength + 1);

                if (NativeMethods.GetWindowText(
                        window,
                        title,
                        title.Capacity) <= 0 ||
                    !string.Equals(
                        title.ToString(),
                        SessionWindowTitle,
                        StringComparison.Ordinal))
                {
                    return true;
                }

                NativeMethods.GetWindowThreadProcessId(
                    window,
                    out var processId);

                if (processId == 0 ||
                    processId >
                    int.MaxValue)
                {
                    return true;
                }

                try
                {
                    using var process =
                        Process.GetProcessById(
                            checked(
                                (int)processId));

                    if (string.Equals(
                            process.ProcessName,
                            "VirtualQHDMirror.Session",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        processIds.Add(
                            process.Id);
                    }
                }
                catch
                {
                    // Process ended or became unavailable.
                }

                return true;
            },
            IntPtr.Zero);

        return processIds
            .OrderBy(
                processId =>
                    processId)
            .ToArray();
    }

    private static class NativeMethods
    {
        internal delegate bool EnumWindowsProc(
            IntPtr window,
            IntPtr parameter);

        [DllImport(
            "user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EnumWindows(
            EnumWindowsProc callback,
            IntPtr parameter);

        [DllImport(
            "user32.dll",
            CharSet = CharSet.Unicode)]
        internal static extern int GetWindowTextLength(
            IntPtr window);

        [DllImport(
            "user32.dll",
            CharSet = CharSet.Unicode)]
        internal static extern int GetWindowText(
            IntPtr window,
            StringBuilder text,
            int maximumCount);

        [DllImport(
            "user32.dll")]
        internal static extern uint GetWindowThreadProcessId(
            IntPtr window,
            out uint processId);

        [DllImport(
            "user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool PostMessage(
            IntPtr window,
            uint message,
            IntPtr wParam,
            IntPtr lParam);
    }
}
