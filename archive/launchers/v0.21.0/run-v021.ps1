$ErrorActionPreference = "Stop"

$startupForm = $null
$startupLabel = $null
$nl = [Environment]::NewLine

try {
    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing

    [System.Windows.Forms.Application]::EnableVisualStyles()
    [System.Windows.Forms.Application]::SetCompatibleTextRenderingDefault($false)

    $root = Split-Path -Parent $MyInvocation.MyCommand.Path
    $solutionPath = Join-Path $root "VirtualQHDMirror.sln"
    $appExePath = Join-Path $root "src\VirtualQHDMirror.App\bin\Debug\net8.0-windows\VirtualQHDMirror.exe"

    $logDirectory = Join-Path $env:LOCALAPPDATA "VirtualQHDMirror\logs"
    $buildLogPath = Join-Path $logDirectory "launcher-v021-build.log"
    New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null

    [Environment]::SetEnvironmentVariable("VQM_REPO_ROOT", $root, [EnvironmentVariableTarget]::Process)
    [Environment]::SetEnvironmentVariable("VQM_BUILD_CONFIGURATION", "Debug", [EnvironmentVariableTarget]::Process)
    [Environment]::SetEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", "en-US", [EnvironmentVariableTarget]::Process)

    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

public static class VqmLauncherWindow
{
    private const int SwRestore = 9;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindowAsync(IntPtr window, int command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr window);

    public static void Activate(IntPtr window)
    {
        if (window == IntPtr.Zero)
        {
            return;
        }

        if (IsIconic(window))
        {
            ShowWindowAsync(window, SwRestore);
        }

        SetForegroundWindow(window);
    }
}
'@

    $existingProcesses = @(
        Get-Process -Name "VirtualQHDMirror" -ErrorAction SilentlyContinue
    )

    $existingWindowProcess = $existingProcesses |
        Where-Object { $_.MainWindowHandle -ne [IntPtr]::Zero } |
        Select-Object -First 1

    if ($existingWindowProcess) {
        [VqmLauncherWindow]::Activate(
            $existingWindowProcess.MainWindowHandle)

        exit 0
    }

    if ($existingProcesses.Count -gt 0) {
        throw (
            "VirtualQHDMirror v0.21 is already running, but its setup window is not available yet." +
            $nl + $nl +
            "Wait a moment and run it again, or close the existing VirtualQHDMirror process before rebuilding.")
    }


    $startupForm = New-Object System.Windows.Forms.Form
    $startupForm.Text = "VirtualQHDMirror v0.21"
    $startupForm.StartPosition = "CenterScreen"
    $startupForm.FormBorderStyle = [System.Windows.Forms.FormBorderStyle]::FixedDialog
    $startupForm.MaximizeBox = $false
    $startupForm.MinimizeBox = $false
    $startupForm.ControlBox = $false
    $startupForm.ShowInTaskbar = $false
    $startupForm.TopMost = $true
    $startupForm.ClientSize = New-Object System.Drawing.Size(430, 136)

    $startupTitle = New-Object System.Windows.Forms.Label
    $startupTitle.Text = "VirtualQHDMirror"
    $startupTitle.Font = New-Object System.Drawing.Font("Segoe UI", 15, [System.Drawing.FontStyle]::Bold)
    $startupTitle.AutoSize = $true
    $startupTitle.Location = New-Object System.Drawing.Point(22, 18)
    $startupForm.Controls.Add($startupTitle)

    $startupLabel = New-Object System.Windows.Forms.Label
    $startupLabel.Text = "Preparing v0.21 cursor presentation prototype..."
    $startupLabel.Font = New-Object System.Drawing.Font("Segoe UI", 9)
    $startupLabel.AutoSize = $true
    $startupLabel.Location = New-Object System.Drawing.Point(24, 60)
    $startupForm.Controls.Add($startupLabel)

    $startupProgress = New-Object System.Windows.Forms.ProgressBar
    $startupProgress.Style = [System.Windows.Forms.ProgressBarStyle]::Marquee
    $startupProgress.MarqueeAnimationSpeed = 24
    $startupProgress.Location = New-Object System.Drawing.Point(25, 90)
    $startupProgress.Size = New-Object System.Drawing.Size(380, 18)
    $startupForm.Controls.Add($startupProgress)

    $startupForm.Show()
    [System.Windows.Forms.Application]::DoEvents()

    $dotnet = Get-Command dotnet.exe -ErrorAction SilentlyContinue
    if (-not $dotnet) {
        throw ".NET SDK was not found. Install the .NET 8 SDK and run VirtualQHDMirror again."
    }

    $startupLabel.Text = "Building v0.21 application..."
    [System.Windows.Forms.Application]::DoEvents()

    $buildStartInfo = New-Object System.Diagnostics.ProcessStartInfo
    $buildStartInfo.FileName = $dotnet.Source
    $buildStartInfo.Arguments = "build " + [char]34 + $solutionPath + [char]34 + " --configuration Debug --nologo"
    $buildStartInfo.WorkingDirectory = $root
    $buildStartInfo.UseShellExecute = $false
    $buildStartInfo.CreateNoWindow = $true
    $buildStartInfo.RedirectStandardOutput = $true
    $buildStartInfo.RedirectStandardError = $true

    $buildProcess = New-Object System.Diagnostics.Process
    $buildProcess.StartInfo = $buildStartInfo

    if (-not $buildProcess.Start()) {
        throw "The v0.21 build process could not be started."
    }

    $stdoutTask = $buildProcess.StandardOutput.ReadToEndAsync()
    $stderrTask = $buildProcess.StandardError.ReadToEndAsync()

    while (-not $buildProcess.HasExited) {
        [System.Windows.Forms.Application]::DoEvents()
        Start-Sleep -Milliseconds 50
    }

    $buildProcess.WaitForExit()

    $stdout = $stdoutTask.Result
    $stderr = $stderrTask.Result

    $commandText = "dotnet build " + [char]34 + $solutionPath + [char]34 + " --configuration Debug --nologo"

    $combinedLog = @(
        "[VirtualQHDMirror v0.21 launcher]"
        "Timestamp: $([DateTimeOffset]::Now.ToString('O'))"
        "Command: $commandText"
        "Exit code: $($buildProcess.ExitCode)"
        ""
        "STDOUT:"
        $stdout
        ""
        "STDERR:"
        $stderr
    ) -join $nl

    [System.IO.File]::WriteAllText(
        $buildLogPath,
        $combinedLog,
        [System.Text.Encoding]::UTF8)

    if ($buildProcess.ExitCode -ne 0) {
        $errorLines = @(($stderr -split "\r?\n") + ($stdout -split "\r?\n")) |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

        $tail = ($errorLines | Select-Object -Last 16) -join $nl

        throw (
            "v0.21 build failed." +
            $nl + $nl +
            $tail +
            $nl + $nl +
            "Build log: " +
            $buildLogPath)
    }

    if (-not (Test-Path -LiteralPath $appExePath)) {
        throw (
            "The v0.21 application executable was not produced." +
            $nl + $nl +
            "Expected: " +
            $appExePath +
            $nl +
            "Build log: " +
            $buildLogPath)
    }

    $startupLabel.Text = "Starting setup UI..."
    [System.Windows.Forms.Application]::DoEvents()

    $appStartInfo = New-Object System.Diagnostics.ProcessStartInfo
    $appStartInfo.FileName = $appExePath
    $appStartInfo.WorkingDirectory = Split-Path -Parent $appExePath
    $appStartInfo.UseShellExecute = $false
    $appStartInfo.CreateNoWindow = $true

    $appProcess = [System.Diagnostics.Process]::Start($appStartInfo)

    if (-not $appProcess) {
        throw "The v0.21 setup UI could not be started."
    }

    if ($startupForm -and -not $startupForm.IsDisposed) {
        $startupForm.Close()
        $startupForm.Dispose()
        $startupForm = $null
    }

    exit 0
}
catch {
    try {
        if ($startupForm -and -not $startupForm.IsDisposed) {
            $startupForm.Close()
            $startupForm.Dispose()
            $startupForm = $null
        }
    }
    catch {}

    $message = $_.Exception.Message
    if ([string]::IsNullOrWhiteSpace($message)) {
        $message = $_.Exception.ToString()
    }

    try {
        [System.Windows.Forms.MessageBox]::Show(
            "VirtualQHDMirror v0.21 could not start." + $nl + $nl + $message,
            "VirtualQHDMirror v0.21",
            [System.Windows.Forms.MessageBoxButtons]::OK,
            [System.Windows.Forms.MessageBoxIcon]::Error
        ) | Out-Null
    }
    catch {}

    exit 1
}
finally {
    try {
        if ($startupForm -and -not $startupForm.IsDisposed) {
            $startupForm.Close()
            $startupForm.Dispose()
        }
    }
    catch {}
}
