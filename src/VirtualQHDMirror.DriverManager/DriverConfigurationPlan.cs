using System;

namespace VirtualQHDMirror.DriverManager;

public enum DriverConfigurationPlanState
{
    NotApplicable,
    NoChangeRequired,
    RestartRecommended,
    ReadyToIncrease,
    Blocked
}

public sealed record DriverConfigurationPlan(
    DriverConfigurationPlanState State,
    int DesiredMonitorCount,
    int? ConfiguredMonitorCount,
    int ActiveVirtualQhdCount,
    string? DeviceInstanceId,
    string ConfigurationPath,
    bool RequiresConfigurationWrite,
    bool RequiresDeviceRestart,
    bool RequiresElevation,
    string? BlockReason,
    string Summary)
{
    public bool CanProceed =>
        State is DriverConfigurationPlanState.NoChangeRequired or
        DriverConfigurationPlanState.RestartRecommended or
        DriverConfigurationPlanState.ReadyToIncrease;
}
