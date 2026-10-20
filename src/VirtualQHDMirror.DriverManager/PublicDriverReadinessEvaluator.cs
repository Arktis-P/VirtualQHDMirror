using System;
using System.Linq;

namespace VirtualQHDMirror.DriverManager;

/// <summary>Read-only detection of a VQM-owned adapter, never an install authorization.</summary>
public enum PublicDriverReadinessState
{
    Missing,
    DeviceDetectedUnverified,
    Ambiguous,
    ProbeError
}

public sealed record PublicDriverReadiness(
    PublicDriverReadinessState State,
    string Message);

public static class PublicDriverReadinessEvaluator
{
    public static PublicDriverReadiness Evaluate(DriverEnvironmentReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        if (report.Status == DriverStatus.Error)
        {
            return new(PublicDriverReadinessState.ProbeError,
                "VQM driver readiness unknown: Windows driver probe failed. Installation is disabled.");
        }

        var candidates = report.DisplayDevices.Where(device =>
            device.HardwareIds.Any(id => string.Equals(
                id.Trim(), PublicVirtualDisplayIdentity.HardwareId,
                StringComparison.OrdinalIgnoreCase))).ToArray();

        if (candidates.Length == 0)
        {
            return new(PublicDriverReadinessState.Missing,
                "VQM-owned production driver not detected. The compatibility driver can continue " +
                "running, but automatic public installation is not yet available.");
        }

        if (candidates.Length > 1)
        {
            return new(PublicDriverReadinessState.Ambiguous,
                $"Detected {candidates.Length} adapters with the VQM hardware ID. " +
                "Lifecycle operations are blocked until identity is unambiguous.");
        }

        return new(PublicDriverReadinessState.DeviceDetectedUnverified,
            "VQM-owned hardware ID detected (" + candidates[0].DisplayName + "). " +
            "Device presence does not prove production signing, QHD endpoint ownership, or lifecycle safety. " +
            "Installation and repair remain locked.");
    }
}
