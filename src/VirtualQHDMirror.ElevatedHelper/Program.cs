using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;

namespace VirtualQHDMirror.ElevatedHelper;

internal static class Program
{
    private const int ExitSuccess = 0;
    private const int ExitInvalidArguments = 10;
    private const int ExitNotElevated = 11;
    private const int ExitValidationFailed = 12;
    private const int ExitConfigWriteFailed = 20;
    private const int ExitRestartFailed = 21;
    private const int ExitRollbackFailed = 22;

    private const int MaximumCompatibilityMonitorCount = 4;
    private const string CompatibilityInstancePrefix = @"ROOT\IDDSAMPLEDRIVER\";

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (!IsAdministrator())
            {
                return ExitNotElevated;
            }

            if (!TryParseArguments(
                    args,
                    out var configurationPath,
                    out var deviceInstanceId,
                    out var requestedMonitorCount))
            {
                return ExitInvalidArguments;
            }

            if (!TryValidateRequest(
                    configurationPath,
                    deviceInstanceId,
                    requestedMonitorCount,
                    out var validatedPath))
            {
                return ExitValidationFailed;
            }

            var wroteConfiguration = false;
            string? backupPath = null;

            if (requestedMonitorCount is int monitorCount)
            {
                if (!TryUpdateMonitorCount(
                        validatedPath,
                        monitorCount,
                        out backupPath))
                {
                    return ExitConfigWriteFailed;
                }

                wroteConfiguration = true;
            }

            if (TryRestartDevice(deviceInstanceId))
            {
                return ExitSuccess;
            }

            if (!wroteConfiguration || string.IsNullOrWhiteSpace(backupPath))
            {
                return ExitRestartFailed;
            }

            if (!TryRestoreBackup(
                    validatedPath,
                    backupPath))
            {
                return ExitRollbackFailed;
            }

            // Best effort: after restoring the previous config, restart the same
            // exact IDD instance again so the old configuration can be reloaded.
            return TryRestartDevice(deviceInstanceId)
                ? ExitRestartFailed
                : ExitRollbackFailed;
        }
        catch
        {
            return ExitValidationFailed;
        }
    }

    private static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);

        return principal.IsInRole(
            WindowsBuiltInRole.Administrator);
    }

    private static bool TryParseArguments(
        IReadOnlyList<string> args,
        out string configurationPath,
        out string deviceInstanceId,
        out int? monitorCount)
    {
        configurationPath = string.Empty;
        deviceInstanceId = string.Empty;
        monitorCount = null;

        var values = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < args.Count; index += 2)
        {
            if (index + 1 >= args.Count ||
                !args[index].StartsWith(
                    "--",
                    StringComparison.Ordinal))
            {
                return false;
            }

            values[args[index]] = args[index + 1];
        }

        if (!values.TryGetValue(
                "--config",
                out var parsedConfigurationPath) ||
            !values.TryGetValue(
                "--device-instance",
                out var parsedDeviceInstanceId))
        {
            return false;
        }

        configurationPath = parsedConfigurationPath;
        deviceInstanceId = parsedDeviceInstanceId;

        if (values.TryGetValue(
                "--monitor-count",
                out var monitorCountText))
        {
            if (!int.TryParse(
                    monitorCountText,
                    out var parsedCount))
            {
                return false;
            }

            monitorCount = parsedCount;
        }

        return true;
    }

    private static bool TryValidateRequest(
        string configurationPath,
        string deviceInstanceId,
        int? monitorCount,
        out string validatedPath)
    {
        validatedPath = string.Empty;

        if (string.IsNullOrWhiteSpace(configurationPath) ||
            string.IsNullOrWhiteSpace(deviceInstanceId))
        {
            return false;
        }

        if (!deviceInstanceId.StartsWith(
                CompatibilityInstancePrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (monitorCount is < 1 or > MaximumCompatibilityMonitorCount)
        {
            return false;
        }

        var allowedPath = GetAllowedConfigurationPath();

        string requestedPath;

        try
        {
            requestedPath = Path.GetFullPath(
                configurationPath.Trim());

            allowedPath = Path.GetFullPath(
                allowedPath);
        }
        catch
        {
            return false;
        }

        if (!string.Equals(
                requestedPath,
                allowedPath,
                StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(requestedPath))
        {
            return false;
        }

        string[] lines;

        try
        {
            lines = File.ReadAllLines(requestedPath);
        }
        catch
        {
            return false;
        }

        if (lines.Length == 0 ||
            !int.TryParse(
                lines[0].Trim(),
                out var existingCount) ||
            existingCount <= 0 ||
            !ContainsQhdMode(lines))
        {
            return false;
        }

        if (monitorCount is int requestedCount &&
            requestedCount < existingCount)
        {
            return false;
        }

        validatedPath = requestedPath;
        return true;
    }

    private static string GetAllowedConfigurationPath()
    {
        var configuredPath = Environment.GetEnvironmentVariable(
            "IDD_SAMPLE_DRIVER_CONFIG",
            EnvironmentVariableTarget.Machine);

        return string.IsNullOrWhiteSpace(configuredPath)
            ? @"C:\IddSampleDriver\option.txt"
            : configuredPath.Trim();
    }

    private static bool ContainsQhdMode(
        IEnumerable<string> lines)
    {
        foreach (var rawLine in lines.Skip(1))
        {
            var line = rawLine.Trim();

            if (line.Length == 0 ||
                line.StartsWith(
                    '#'))
            {
                continue;
            }

            var parts = line.Split(
                ',',
                StringSplitOptions.TrimEntries |
                StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length >= 2 &&
                int.TryParse(
                    parts[0],
                    out var width) &&
                int.TryParse(
                    parts[1],
                    out var height) &&
                width == 2560 &&
                height == 1440)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryUpdateMonitorCount(
        string configurationPath,
        int monitorCount,
        out string? backupPath)
    {
        backupPath = null;

        try
        {
            var lines = File.ReadAllLines(
                configurationPath);

            if (lines.Length == 0)
            {
                return false;
            }

            lines[0] = monitorCount.ToString();

            var directory = Path.GetDirectoryName(
                configurationPath);

            if (string.IsNullOrWhiteSpace(directory))
            {
                return false;
            }

            var fileName = Path.GetFileName(
                configurationPath);

            backupPath = Path.Combine(
                directory,
                $"{fileName}.vqm.bak");

            var temporaryPath = Path.Combine(
                directory,
                $"{fileName}.vqm.tmp");

            File.WriteAllLines(
                temporaryPath,
                lines,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false));

            File.Copy(
                configurationPath,
                backupPath,
                overwrite: true);

            File.Move(
                temporaryPath,
                configurationPath,
                overwrite: true);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryRestoreBackup(
        string configurationPath,
        string backupPath)
    {
        try
        {
            if (!File.Exists(backupPath))
            {
                return false;
            }

            File.Copy(
                backupPath,
                configurationPath,
                overwrite: true);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryRestartDevice(
        string deviceInstanceId)
    {
        try
        {
            var pnputilPath = Path.Combine(
                Environment.SystemDirectory,
                "pnputil.exe");

            if (!File.Exists(pnputilPath))
            {
                return false;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = pnputilPath,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            startInfo.ArgumentList.Add(
                "/restart-device");

            startInfo.ArgumentList.Add(
                deviceInstanceId);

            using var process = Process.Start(
                startInfo);

            if (process is null)
            {
                return false;
            }

            if (!process.WaitForExit(
                    milliseconds: 30000))
            {
                try
                {
                    process.Kill(
                        entireProcessTree: true);
                }
                catch
                {
                    // Ignore cleanup failures after timeout.
                }

                return false;
            }

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
