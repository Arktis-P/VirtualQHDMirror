using System;
namespace VirtualQHDMirror.DriverManager;

/// <summary>User-facing, conservative setup state. An unobserved disabled device is not inferred.</summary>
public enum VirtualDisplaySetupState
{
    Ready,
    Missing,
    Disabled,
    NeedsQhd,
    NeedsRestart,
    Error
}

public sealed record VirtualDisplaySetupResult(
    VirtualDisplaySetupState State,
    string Headline,
    string Guidance);

public static class VirtualDisplaySetupEvaluator
{
    public static VirtualDisplaySetupResult Evaluate(
        DriverEnvironmentReport report,
        DriverConfigurationPlan plan)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(plan);

        if (report.Status == DriverStatus.Error ||
            !string.IsNullOrWhiteSpace(report.Configuration.Error))
        {
            return new(VirtualDisplaySetupState.Error, "Error — driver status unavailable",
                "Check the driver configuration and Windows display state, then select Recheck. " +
                (report.Configuration.Error ?? report.Summary));
        }

        // A usable active source takes precedence over optional full-coverage recommendations.
        if (report.ActiveVirtualQhdCount > 0)
        {
            return new(VirtualDisplaySetupState.Ready, "Ready — QHD virtual display detected",
                "At least one active 2560×1440 virtual source is available. Select a physical FHD destination to start mirroring. " +
                "Additional virtual displays are optional for independent multi-monitor pairing.");
        }

        if (report.Status == DriverStatus.NotInstalled)
        {
            return new(VirtualDisplaySetupState.Missing, "Missing — compatible display driver not found",
                "No supported development IDD or active virtual display was detected. " +
                "Install a trusted, appropriately signed virtual display driver through its official instructions, " +
                "then return to VQM and select Recheck. VQM does not distribute the legacy ge9 driver.");
        }

        if (report.Status == DriverStatus.Unknown)
        {
            return new(VirtualDisplaySetupState.Error, "Unrecognized virtual display",
                "Windows reports a virtual display, but its driver identity is not recognized and no active QHD mode is available. " +
                "Check Windows display settings, verify the driver publisher, and select Recheck.");
        }

        if (report.Configuration.Exists &&
            !report.Configuration.IncludesQhd2560x1440)
        {
            return new(VirtualDisplaySetupState.NeedsQhd, "Needs QHD — required mode unavailable",
                "The compatibility-driver config does not advertise 2560×1440. " +
                "Review the trusted driver configuration/source and add a supported QHD mode before Recheck. " +
                "VQM will not edit physical monitor EDID or force an unsafe mode.");
        }

        if (plan.State == DriverConfigurationPlanState.RestartRecommended)
        {
            return new(VirtualDisplaySetupState.NeedsRestart, "Needs restart — QHD display not active",
                "The driver requests enough monitors but none is active at QHD. " +
                "Stop mirroring, then use Restart virtual display below (administrator approval required), " +
                "or enable the virtual display in Windows display settings. Select Recheck afterward.");
        }

        if (plan.State == DriverConfigurationPlanState.ReadyToIncrease)
        {
            return new(VirtualDisplaySetupState.NeedsRestart, "Needs setup — virtual display count insufficient",
                "Use Apply & restart driver below after stopping mirroring. " +
                "The compatibility helper will ask for administrator approval; then select Recheck.");
        }

        return new(VirtualDisplaySetupState.NeedsRestart, "Not ready — no active QHD virtual source",
            "The driver may be installed but its virtual display is inactive or not configured. " +
            "Check Windows display settings and the driver configuration, then select Recheck. " +
            (plan.BlockReason ?? report.Summary));
    }
}
