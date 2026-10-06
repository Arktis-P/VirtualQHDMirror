using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace VirtualQHDMirror.App;

internal sealed class LegacyMirrorProcessController : IDisposable
{
    private const uint WmHotKey = 0x0312;
    private const int ExitHotKeyId = 1;

    private readonly object _gate = new();
    private Process? _process;

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
                return _process is { HasExited: false } process ? process.Id : null;
            }
        }
    }

    public void Start(string sourceDevice, string destinationDevice)
    {
        if (string.IsNullOrWhiteSpace(sourceDevice))
        {
            throw new ArgumentException("Source display device name is missing.", nameof(sourceDevice));
        }

        if (string.IsNullOrWhiteSpace(destinationDevice))
        {
            throw new ArgumentException("Destination display device name is missing.", nameof(destinationDevice));
        }

        lock (_gate)
        {
            if (_process is { HasExited: false })
            {
                throw new InvalidOperationException("A mirror session is already running.");
            }

            _process?.Dispose();
            _process = null;

            var scriptPath = ResolveLegacyEnginePath();
            var workingDirectory = Path.GetDirectoryName(scriptPath)
                ?? throw new InvalidOperationException("Could not resolve the v0.14.1 engine directory.");

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
                throw new InvalidOperationException("PowerShell did not start the v0.14.1 mirror engine.");
            }

            _process = process;
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool RequestStop()
    {
        Process? process;

        lock (_gate)
        {
            process = _process;

            if (process is null || process.HasExited)
            {
                return true;
            }
        }

        // The v0.14.1 mirror handles WM_HOTKEY id 1 using the same cleanup
        // path as Ctrl+Alt+F12. Post it only to windows owned by this process.
        var posted = false;

        NativeMethods.EnumWindows((window, _) =>
        {
            NativeMethods.GetWindowThreadProcessId(window, out var processId);

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
            if (_process is not null)
            {
                _process.Exited -= MirrorProcess_Exited;
                _process.Dispose();
                _process = null;
            }
        }
    }

    private void MirrorProcess_Exited(object? sender, EventArgs e)
    {
        lock (_gate)
        {
            if (sender is Process process && ReferenceEquals(_process, process))
            {
                process.Exited -= MirrorProcess_Exited;
                process.Dispose();
                _process = null;
            }
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private static string ResolveLegacyEnginePath()
    {
        var configuredRoot = Environment.GetEnvironmentVariable("VQM_REPO_ROOT");

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

        foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(start);

            for (var depth = 0; directory is not null && depth < 10; depth++, directory = directory.Parent)
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
        internal delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EnumWindows(
            EnumWindowsProc callback,
            IntPtr parameter);

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
