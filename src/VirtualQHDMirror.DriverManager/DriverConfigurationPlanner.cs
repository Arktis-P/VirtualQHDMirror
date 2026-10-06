using System;
using System.Linq;

namespace VirtualQHDMirror.DriverManager;

public sealed class DriverConfigurationPlanner
{
    // ge9/IddSampleDriver's option.txt recommends a small count below 5.
    // Keep the compatibility bridge conservative until VQM owns the IDD.
    private const int MaximumCompatibilityMonitorCount = 4;

    public DriverConfigurationPlan BuildFullCoveragePlan(
        DriverEnvironmentReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var desiredCount = report.Requirement.FullCoverageTargetCount;
        var configuredCount = report.Configuration.ConfiguredMonitorCount;
        var activeQhdCount = report.ActiveVirtualQhdCount;

        if (desiredCount <= 0)
        {
            return CreatePlan(
                DriverConfigurationPlanState.NotApplicable,
                desiredCount,
                configuredCount,
                activeQhdCount,
                null,
                report.Configuration.Path,
                requiresConfigurationWrite: false,
                requiresDeviceRestart: false,
                requiresElevation: false,
                blockReason: null,
                summary: "No eligible physical FHD target currently requires a virtual QHD workspace.");
        }

        if (desiredCount > MaximumCompatibilityMonitorCount)
        {
            return Block(
                report,
                desiredCount,
                $"The compatibility driver plan is capped at {MaximumCompatibilityMonitorCount} virtual monitors. {desiredCount} physical FHD targets were detected.");
        }

        if (report.Status is DriverStatus.NotInstalled or DriverStatus.Error)
        {
            return Block(
                report,
                desiredCount,
                "A usable IddSampleDriver-compatible display device was not confirmed.");
        }

        if (report.IddSampleDriverCandidates.Count != 1)
        {
            return Block(
                report,
                desiredCount,
                report.IddSampleDriverCandidates.Count == 0
                    ? "The active virtual display could not be tied to exactly one IddSampleDriver-compatible Display-class device."
                    : $"{report.IddSampleDriverCandidates.Count} IddSampleDriver-compatible Display-class devices were found; VQM will not guess which one to restart.");
        }

        var device = report.IddSampleDriverCandidates[0];

        if (!report.Configuration.Exists)
        {
            return Block(
                report,
                desiredCount,
                $"The driver configuration file was not found at {report.Configuration.Path}.");
        }

        if (!string.IsNullOrWhiteSpace(report.Configuration.Error))
        {
            return Block(
                report,
                desiredCount,
                $"The driver configuration could not be read safely: {report.Configuration.Error}");
        }

        if (!report.Configuration.IncludesQhd2560x1440)
        {
            return Block(
                report,
                desiredCount,
                "The current driver configuration does not advertise a 2560×1440 mode.");
        }

        if (configuredCount is null)
        {
            return Block(
                report,
                desiredCount,
                "The configured virtual-monitor count could not be parsed.");
        }

        if (configuredCount >= desiredCount)
        {
            if (activeQhdCount >= desiredCount)
            {
                var extraText = configuredCount > desiredCount
                    ? $" The driver config requests {configuredCount} monitor(s); VQM will not reduce that count automatically."
                    : string.Empty;

                return CreatePlan(
                    DriverConfigurationPlanState.NoChangeRequired,
                    desiredCount,
                    configuredCount,
                    activeQhdCount,
                    device.InstanceId,
                    report.Configuration.Path,
                    requiresConfigurationWrite: false,
                    requiresDeviceRestart: false,
                    requiresElevation: false,
                    blockReason: null,
                    summary: $"No driver change is required. {desiredCount}/{desiredCount} independent QHD source(s) are active and the configured monitor count is sufficient.{extraText}");
            }

            return CreatePlan(
                DriverConfigurationPlanState.RestartRecommended,
                desiredCount,
                configuredCount,
                activeQhdCount,
                device.InstanceId,
                report.Configuration.Path,
                requiresConfigurationWrite: false,
                requiresDeviceRestart: true,
                requiresElevation: true,
                blockReason: null,
                summary: $"The config already requests {configuredCount} monitor(s), but only {activeQhdCount}/{desiredCount} required QHD source(s) are active. A controlled restart of the IDD device should be attempted before changing the config.");
        }

        return CreatePlan(
            DriverConfigurationPlanState.ReadyToIncrease,
            desiredCount,
            configuredCount,
            activeQhdCount,
            device.InstanceId,
            report.Configuration.Path,
            requiresConfigurationWrite: true,
            requiresDeviceRestart: true,
            requiresElevation: true,
            blockReason: null,
            summary: $"Full 1:1 coverage would increase the driver monitor count from {configuredCount} to {desiredCount}, then restart only the detected IDD device so it reloads the configuration. No physical monitor EDID or registry override is required.");
    }

    private static DriverConfigurationPlan Block(
        DriverEnvironmentReport report,
        int desiredCount,
        string reason)
    {
        var deviceInstanceId = report.IddSampleDriverCandidates
            .Select(device => device.InstanceId)
            .FirstOrDefault();

        return CreatePlan(
            DriverConfigurationPlanState.Blocked,
            desiredCount,
            report.Configuration.ConfiguredMonitorCount,
            report.ActiveVirtualQhdCount,
            deviceInstanceId,
            report.Configuration.Path,
            requiresConfigurationWrite: false,
            requiresDeviceRestart: false,
            requiresElevation: false,
            blockReason: reason,
            summary: $"Driver configuration change is blocked: {reason}");
    }

    private static DriverConfigurationPlan CreatePlan(
        DriverConfigurationPlanState state,
        int desiredCount,
        int? configuredCount,
        int activeQhdCount,
        string? deviceInstanceId,
        string configurationPath,
        bool requiresConfigurationWrite,
        bool requiresDeviceRestart,
        bool requiresElevation,
        string? blockReason,
        string summary) =>
        new(
            State: state,
            DesiredMonitorCount: desiredCount,
            ConfiguredMonitorCount: configuredCount,
            ActiveVirtualQhdCount: activeQhdCount,
            DeviceInstanceId: deviceInstanceId,
            ConfigurationPath: configurationPath,
            RequiresConfigurationWrite: requiresConfigurationWrite,
            RequiresDeviceRestart: requiresDeviceRestart,
            RequiresElevation: requiresElevation,
            BlockReason: blockReason,
            Summary: summary);
}
