using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

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

        var buildExecutablePath =
            ResolveSessionExecutablePath();

        var executablePath =
            StageSessionRuntime(
                buildExecutablePath);

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

        using var startupProcess =
            new Process
            {
                StartInfo =
                    startInfo
            };

        if (!startupProcess.Start())
        {
            throw new InvalidOperationException(
                "The .NET mirror session process did not start.");
        }

        if (!WaitForSessionWindow(
                startupProcess,
                TimeSpan.FromSeconds(5)))
        {
            var exitCode =
                startupProcess.HasExited
                    ? startupProcess.ExitCode
                    : (int?)null;

            if (!startupProcess.HasExited)
            {
                TryTerminateFailedStartup(
                    startupProcess);
            }

            throw new InvalidOperationException(
                BuildStartupFailureMessage(
                    exitCode));
        }

        var process =
            Process.GetProcessById(
                startupProcess.Id);

        process.Exited +=
            SessionProcess_Exited;

        lock (_gate)
        {
            ClearTrackedProcessLocked();
            _process = process;
            _detectedSessionCount = 1;
            _attachedFromDiscovery = false;
        }

        process.EnableRaisingEvents = true;

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

    private static string StageSessionRuntime(
        string buildExecutablePath)
    {
        var sourceDirectory =
            Path.GetDirectoryName(
                buildExecutablePath);

        if (string.IsNullOrWhiteSpace(
                sourceDirectory) ||
            !Directory.Exists(
                sourceDirectory))
        {
            throw new DirectoryNotFoundException(
                "The built .NET session output directory was not found.");
        }

        var runtimeRoot =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "VirtualQHDMirror",
                "runtime",
                "sessions");

        Directory.CreateDirectory(
            runtimeRoot);

        var runtimeDirectory =
            Path.Combine(
                runtimeRoot,
                $"{DateTimeOffset.Now:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}");

        CopyDirectory(
            sourceDirectory,
            runtimeDirectory);

        var stagedExecutablePath =
            Path.Combine(
                runtimeDirectory,
                Path.GetFileName(
                    buildExecutablePath));

        if (!File.Exists(
                stagedExecutablePath))
        {
            throw new FileNotFoundException(
                "The staged .NET session executable was not created.",
                stagedExecutablePath);
        }

        return stagedExecutablePath;
    }

    private static void CopyDirectory(
        string sourceDirectory,
        string destinationDirectory)
    {
        Directory.CreateDirectory(
            destinationDirectory);

        foreach (var sourceFile in
                 Directory.EnumerateFiles(
                     sourceDirectory,
                     "*",
                     SearchOption.AllDirectories))
        {
            var relativePath =
                Path.GetRelativePath(
                    sourceDirectory,
                    sourceFile);

            var destinationFile =
                Path.Combine(
                    destinationDirectory,
                    relativePath);

            var destinationParent =
                Path.GetDirectoryName(
                    destinationFile);

            if (!string.IsNullOrWhiteSpace(
                    destinationParent))
            {
                Directory.CreateDirectory(
                    destinationParent);
            }

            File.Copy(
                sourceFile,
                destinationFile,
                overwrite: true);
        }
    }

    private static string ResolveSessionExecutablePath()
    {
        const string executableName =
            "VirtualQHDMirror.Session.exe";

        var repositoryRoot =
            Environment.GetEnvironmentVariable(
                "VQM_REPO_ROOT");

        if (!string.IsNullOrWhiteSpace(
                repositoryRoot))
        {
            var requestedConfiguration =
                Environment.GetEnvironmentVariable(
                    "VQM_BUILD_CONFIGURATION");

            var configurations =
                string.IsNullOrWhiteSpace(
                    requestedConfiguration)
                    ? new[]
                    {
                        "Debug",
                        "Release"
                    }
                    : new[]
                    {
                        requestedConfiguration.Trim()
                    };

            foreach (var configuration in
                     configurations)
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

        var installedCandidate =
            Path.Combine(
                AppContext.BaseDirectory,
                executableName);

        if (File.Exists(
                installedCandidate))
        {
            return installedCandidate;
        }

        throw new FileNotFoundException(
            $"VirtualQHDMirror.Session.exe was not found. Expected a freshly built session binary. Run run-v020.cmd again. Diagnostics: {GetSessionLogPath()}");
    }

    private static bool WaitForSessionWindow(
        Process process,
        TimeSpan timeout)
    {
        var deadline =
            Stopwatch.GetTimestamp() +
            (long)(timeout.TotalSeconds *
                   Stopwatch.Frequency);

        while (Stopwatch.GetTimestamp() <
               deadline)
        {
            try
            {
                if (process.HasExited)
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }

            if (FindSessionWindowForProcess(
                    process.Id) != IntPtr.Zero)
            {
                return true;
            }

            Thread.Sleep(50);
        }

        return false;
    }

    private static IntPtr FindSessionWindowForProcess(
        int processId)
    {
        var result =
            IntPtr.Zero;

        NativeMethods.EnumWindows(
            (window, _) =>
            {
                NativeMethods.GetWindowThreadProcessId(
                    window,
                    out var windowProcessId);

                if (windowProcessId !=
                    (uint)processId)
                {
                    return true;
                }

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
                        title.Capacity) > 0 &&
                    string.Equals(
                        title.ToString(),
                        SessionWindowTitle,
                        StringComparison.Ordinal))
                {
                    result = window;
                    return false;
                }

                return true;
            },
            IntPtr.Zero);

        return result;
    }

    private static void TryTerminateFailedStartup(
        Process process)
    {
        try
        {
            process.Kill(
                entireProcessTree: true);

            process.WaitForExit(
                milliseconds: 1000);
        }
        catch
        {
            // The process may already have exited.
        }
    }

    private static string BuildStartupFailureMessage(
        int? exitCode)
    {
        var reason =
            exitCode switch
            {
                10 =>
                    "The session rejected its startup arguments.",

                11 =>
                    "The requested source or destination display could not be rediscovered inside the session process.",

                20 =>
                    "The GDI renderer failed repeatedly during startup.",

                99 =>
                    "The .NET session hit an unhandled startup exception.",

                null =>
                    "The .NET session process stayed alive but did not create its mirror window within 5 seconds.",

                _ =>
                    $"The .NET session exited during startup with code {exitCode}."
            };

        return
            $"{reason}{Environment.NewLine}{Environment.NewLine}" +
            $"Session diagnostics: {GetSessionLogPath()}";
    }

    private static string GetSessionLogPath() =>
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "VirtualQHDMirror",
            "logs",
            "native-session.log");

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
