using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VirtualQHDMirror.DriverManager;

namespace VirtualQHDMirror.App;

internal sealed record DriverPlanApplyResult(
    bool Success,
    bool Cancelled,
    int? ExitCode,
    string Message);

internal sealed class DriverPlanElevatedRunner
{
    private const int ErrorCancelled = 1223;

    public async Task<DriverPlanApplyResult> ApplyAsync(
        DriverConfigurationPlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (!plan.CanProceed)
        {
            return new DriverPlanApplyResult(
                Success: false,
                Cancelled: false,
                ExitCode: null,
                Message: "The current driver plan is blocked and cannot be applied.");
        }

        if (plan.State == DriverConfigurationPlanState.NoChangeRequired)
        {
            return new DriverPlanApplyResult(
                Success: true,
                Cancelled: false,
                ExitCode: 0,
                Message: "No driver change is required.");
        }

        if (string.IsNullOrWhiteSpace(
                plan.DeviceInstanceId))
        {
            return new DriverPlanApplyResult(
                Success: false,
                Cancelled: false,
                ExitCode: null,
                Message: "The IDD device instance could not be resolved.");
        }

        string helperPath;

        try
        {
            helperPath = ResolveHelperPath();
        }
        catch (Exception exception)
        {
            return new DriverPlanApplyResult(
                Success: false,
                Cancelled: false,
                ExitCode: null,
                Message: exception.Message);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = helperPath,
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = Path.GetDirectoryName(helperPath)
                ?? AppContext.BaseDirectory
        };

        startInfo.ArgumentList.Add(
            "--config");
        startInfo.ArgumentList.Add(
            plan.ConfigurationPath);
        startInfo.ArgumentList.Add(
            "--device-instance");
        startInfo.ArgumentList.Add(
            plan.DeviceInstanceId);

        if (plan.RequiresConfigurationWrite)
        {
            startInfo.ArgumentList.Add(
                "--monitor-count");
            startInfo.ArgumentList.Add(
                plan.DesiredMonitorCount.ToString());
        }

        try
        {
            using var process = Process.Start(
                startInfo);

            if (process is null)
            {
                return new DriverPlanApplyResult(
                    Success: false,
                    Cancelled: false,
                    ExitCode: null,
                    Message: "The elevated helper did not start.");
            }

            await process.WaitForExitAsync(
                cancellationToken);

            if (process.ExitCode == 0)
            {
                return new DriverPlanApplyResult(
                    Success: true,
                    Cancelled: false,
                    ExitCode: 0,
                    Message: plan.RequiresConfigurationWrite
                        ? $"Driver monitor count was updated to {plan.DesiredMonitorCount} and the IDD device was restarted."
                        : "The IDD device was restarted without changing the monitor count.");
            }

            return new DriverPlanApplyResult(
                Success: false,
                Cancelled: false,
                ExitCode: process.ExitCode,
                Message: DescribeExitCode(
                    process.ExitCode));
        }
        catch (Win32Exception exception)
            when (exception.NativeErrorCode == ErrorCancelled)
        {
            return new DriverPlanApplyResult(
                Success: false,
                Cancelled: true,
                ExitCode: null,
                Message: "Administrator approval was cancelled. No driver change was applied.");
        }
        catch (OperationCanceledException)
        {
            return new DriverPlanApplyResult(
                Success: false,
                Cancelled: true,
                ExitCode: null,
                Message: "Driver configuration was cancelled while waiting for the elevated helper.");
        }
        catch (Exception exception)
        {
            return new DriverPlanApplyResult(
                Success: false,
                Cancelled: false,
                ExitCode: null,
                Message: $"Could not start the elevated driver helper: {exception.Message}");
        }
    }

    private static string ResolveHelperPath()
    {
        const string executableName =
            "VirtualQHDMirror.ElevatedHelper.exe";

        var installedCandidate = Path.Combine(
            AppContext.BaseDirectory,
            executableName);

        if (File.Exists(installedCandidate))
        {
            return installedCandidate;
        }

        var repositoryRoot = Environment.GetEnvironmentVariable(
            "VQM_REPO_ROOT");

        if (!string.IsNullOrWhiteSpace(repositoryRoot))
        {
            foreach (var configuration in new[]
                     {
                         "Debug",
                         "Release"
                     })
            {
                var developmentCandidate = Path.Combine(
                    repositoryRoot,
                    "src",
                    "VirtualQHDMirror.ElevatedHelper",
                    "bin",
                    configuration,
                    "net8.0-windows",
                    executableName);

                if (File.Exists(developmentCandidate))
                {
                    return developmentCandidate;
                }
            }
        }

        throw new FileNotFoundException(
            "VirtualQHDMirror.ElevatedHelper.exe was not found. Rebuild the v0.20 solution before applying a driver configuration change.");
    }

    private static string DescribeExitCode(
        int exitCode) =>
        exitCode switch
        {
            10 => "The elevated helper rejected its arguments.",
            11 => "The helper was not running with administrator rights.",
            12 => "The helper rejected the config path, device identity, monitor count, or QHD mode validation.",
            20 => "The driver configuration file could not be updated safely.",
            21 => "The IDD device restart failed. If the config was changed, the helper restored the previous config before returning.",
            22 => "The IDD restart failed and rollback could not be fully confirmed. Check the driver config before retrying.",
            _ => $"The elevated helper failed with exit code {exitCode}."
        };
}
