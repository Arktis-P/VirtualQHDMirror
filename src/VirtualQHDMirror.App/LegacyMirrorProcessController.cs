using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace VirtualQHDMirror.App;

internal enum LegacyMirrorSessionState
{
    None,
    Attached,
    Multiple
}

internal sealed class LegacyMirrorProcessController : IDisposable
{
    private const uint WmHotKey = 0x0312;
    private const int ExitHotKeyId = 1;
    private const string LegacyWindowTitle = "VirtualQHDMirror v0.14.1";

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
                return _process is { HasExited: false };
            }
        }
    }

    public int? ProcessId
    {
        get
        {
            lock (_gate)
            {
                return _process is { HasExited: false } process
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
                return _process is { HasExited: false } &&
                       _attachedFromDiscovery;
            }
        }
    }

    public LegacyMirrorSessionState SessionState
    {
        get
        {
            lock (_gate)
            {
                if (_process is { HasExited: false })
                {
                    return LegacyMirrorSessionState.Attached;
                }

                return _detectedSessionCount > 1
                    ? LegacyMirrorSessionState.Multiple
                    : LegacyMirrorSessionState.None;
            }
        }
    }

    public LegacyMirrorSessionState DetectExistingSession()
    {
        lock (_gate)
        {
            if (_process is { HasExited: false })
            {
                _detectedSessionCount = 1;
                return LegacyMirrorSessionState.Attached;
            }

            ClearTrackedProcessLocked();
        }

        var processIds = FindLegacyMirrorProcessIds();

        if (processIds.Count != 1)
        {
            lock (_gate)
            {
                _detectedSessionCount = processIds.Count;
                _attachedFromDiscovery = false;
            }

            StateChanged?.Invoke(this, EventArgs.Empty);

            return processIds.Count > 1
                ? LegacyMirrorSessionState.Multiple
                : LegacyMirrorSessionState.None;
        }

        Process? process = null;

        try
        {
            process = Process.GetProcessById(processIds[0]);

            if (process.HasExited)
            {
                process.Dispose();

                lock (_gate)
                {
                    _detectedSessionCount = 0;
                    _attachedFromDiscovery = false;
                }

                StateChanged?.Invoke(this, EventArgs.Empty);
                return LegacyMirrorSessionState.None;
            }

            process.EnableRaisingEvents = true;
            process.Exited += MirrorProcess_Exited;

            lock (_gate)
            {
                if (_process is { HasExited: false })
                {
                    process.Exited -= MirrorProcess_Exited;
                    process.Dispose();
                    _detectedSessionCount = 1;
                    return LegacyMirrorSessionState.Attached;
                }

                ClearTrackedProcessLocked();
                _process = process;
                _detectedSessionCount = 1;
                _attachedFromDiscovery = true;
                process = null;
            }

            StateChanged?.Invoke(this, EventArgs.Empty);
            return LegacyMirrorSessionState.Attached;
        }
        catch (Exception exception)
            when (exception is ArgumentException or
                  InvalidOperationException or
                  Win32Exception)
        {
            process?.Dispose();

            lock (_gate)
            {
                _detectedSessionCount = 0;
                _attachedFromDiscovery = false;
            }

            StateChanged?.Invoke(this, EventArgs.Empty);
            return LegacyMirrorSessionState.None;
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
        if (string.IsNullOrWhiteSpace(sourceDevice))
        {
            throw new ArgumentException(
                "Source display device name is missing.",
                nameof(sourceDevice));
        }

        if (string.IsNullOrWhiteSpace(destinationDevice))
        {
            throw new ArgumentException(
                "Destination display device name is missing.",
                nameof(destinationDevice));
        }

        lock (_gate)
        {
            if (_process is { HasExited: false })
            {
                throw new InvalidOperationException(
                    "A mirror session is already running.");
            }
        }

        var existingState = DetectExistingSession();

        if (existingState != LegacyMirrorSessionState.None)
        {
            throw new InvalidOperationException(
                existingState == LegacyMirrorSessionState.Attached
                    ? "An existing v0.14.1 mirror session was found and attached. Use Stop Mirror to control it instead of starting a duplicate."
                    : $"{DetectedSessionCount} existing v0.14.1 mirror sessions are already running. VQM will not start another session automatically.");
        }

        lock (_gate)
        {
            ClearTrackedProcessLocked();

            var scriptPath = ResolveLegacyEnginePath();
            var workingDirectory = Path.GetDirectoryName(scriptPath)
                ?? throw new InvalidOperationException(
                    "Could not resolve the v0.14.1 engine directory.");

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(scriptPath);
            startInfo.ArgumentList.Add("-SourceDevice");
            startInfo.ArgumentList.Add(sourceDevice);
            startInfo.ArgumentList.Add("-DestinationDevice");
            startInfo.ArgumentList.Add(destinationDevice);
            startInfo.ArgumentList.Add("-Mode");
            startInfo.ArgumentList.Add("adaptive");
            startInfo.ArgumentList.Add("-InputMode");
            startInfo.ArgumentList.Add("portal");
            startInfo.ArgumentList.Add("-CursorHz");
            startInfo.ArgumentList.Add("120");

            var process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };

            process.Exited += MirrorProcess_Exited;

            if (!process.Start())
            {
                process.Exited -= MirrorProcess_Exited;
                process.Dispose();

                throw new InvalidOperationException(
                    "PowerShell did not start the v0.14.1 mirror engine.");
            }

            _process = process;
            _detectedSessionCount = 1;
            _attachedFromDiscovery = false;
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
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

            if (process is null || process.HasExited)
            {
                return false;
            }
        }

        // The v0.14.1 mirror handles WM_HOTKEY id 1 using the same cleanup
        // path as Ctrl+Alt+F12. Post it only to windows owned by this process.
        var posted = false;

        NativeMethods.EnumWindows((window, _) =>
        {
            NativeMethods.GetWindowThreadProcessId(
                window,
                out var processId);

            if (processId == (uint)process.Id)
            {
                posted |= NativeMethods.PostMessage(
                    window,
                    WmHotKey,
                    new IntPtr(ExitHotKeyId),
                    IntPtr.Zero);
            }

            return true;
        }, IntPtr.Zero);

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

    private void MirrorProcess_Exited(
        object? sender,
        EventArgs e)
    {
        lock (_gate)
        {
            if (sender is Process process &&
                ReferenceEquals(_process, process))
            {
                process.Exited -= MirrorProcess_Exited;
                process.Dispose();
                _process = null;
                _detectedSessionCount = 0;
                _attachedFromDiscovery = false;
            }
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ClearTrackedProcessLocked()
    {
        if (_process is null)
        {
            return;
        }

        _process.Exited -= MirrorProcess_Exited;
        _process.Dispose();
        _process = null;
    }

    private static IReadOnlyList<int> FindLegacyMirrorProcessIds()
    {
        var processIds = new HashSet<int>();

        NativeMethods.EnumWindows((window, _) =>
        {
            var titleLength = NativeMethods.GetWindowTextLength(window);

            if (titleLength <= 0)
            {
                return true;
            }

            var title = new StringBuilder(titleLength + 1);

            if (NativeMethods.GetWindowText(
                    window,
                    title,
                    title.Capacity) <= 0 ||
                !string.Equals(
                    title.ToString(),
                    LegacyWindowTitle,
                    StringComparison.Ordinal))
            {
                return true;
            }

            NativeMethods.GetWindowThreadProcessId(
                window,
                out var processId);

            if (processId == 0 ||
                processId > int.MaxValue)
            {
                return true;
            }

            try
            {
                using var process = Process.GetProcessById(
                    checked((int)processId));

                if (IsPowerShellProcess(process.ProcessName))
                {
                    processIds.Add(process.Id);
                }
            }
            catch (ArgumentException)
            {
                // The process exited between window enumeration and lookup.
            }
            catch (InvalidOperationException)
            {
                // The process became unavailable while reading its metadata.
            }
            catch (Win32Exception)
            {
                // Do not fail the whole probe if one unrelated process cannot be inspected.
            }

            return true;
        }, IntPtr.Zero);

        return processIds
            .OrderBy(processId => processId)
            .ToArray();
    }

    private static bool IsPowerShellProcess(string processName) =>
        string.Equals(
            processName,
            "powershell",
            StringComparison.OrdinalIgnoreCase) ||
        string.Equals(
            processName,
            "pwsh",
            StringComparison.OrdinalIgnoreCase);

    private static string ResolveLegacyEnginePath()
    {
        var configuredRoot = Environment.GetEnvironmentVariable(
            "VQM_REPO_ROOT");

        if (!string.IsNullOrWhiteSpace(configuredRoot))
        {
            var configuredPath = Path.Combine(
                configuredRoot,
                "archive",
                "v0.14.1",
                "VirtualQHDMirror.ps1");

            if (File.Exists(configuredPath))
            {
                return Path.GetFullPath(configuredPath);
            }
        }

        foreach (var start in new[]
                 {
                     Environment.CurrentDirectory,
                     AppContext.BaseDirectory
                 })
        {
            var directory = new DirectoryInfo(start);

            for (var depth = 0;
                 directory is not null && depth < 10;
                 depth++, directory = directory.Parent)
            {
                var candidate = Path.Combine(
                    directory.FullName,
                    "archive",
                    "v0.14.1",
                    "VirtualQHDMirror.ps1");

                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        throw new FileNotFoundException(
            "The frozen v0.14.1 mirror engine was not found. Run v0.20 from the repository or set VQM_REPO_ROOT.");
    }

    private static class NativeMethods
    {
        internal delegate bool EnumWindowsProc(
            IntPtr window,
            IntPtr parameter);

        [DllImport("user32.dll")]
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

        [DllImport("user32.dll")]
        internal static extern uint GetWindowThreadProcessId(
            IntPtr window,
            out uint processId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool PostMessage(
            IntPtr window,
            uint message,
            IntPtr wParam,
            IntPtr lParam);
    }
}
