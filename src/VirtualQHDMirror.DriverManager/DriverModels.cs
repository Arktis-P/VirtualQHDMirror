using System;
using System.Collections.Generic;

namespace VirtualQHDMirror.DriverManager;

public sealed record DriverDeviceInfo(
    string Description,
    string FriendlyName,
    string Manufacturer,
    string InstanceId,
    IReadOnlyList<string> HardwareIds,
    string DriverKey)
{
    public string DisplayName =>
        !string.IsNullOrWhiteSpace(FriendlyName)
            ? FriendlyName
            : !string.IsNullOrWhiteSpace(Description)
                ? Description
                : InstanceId;
}

public sealed record IddSampleConfiguration(
    string Path,
    bool Exists,
    int? ConfiguredMonitorCount,
    bool IncludesQhd2560x1440,
    IReadOnlyList<string> Modes,
    string? Error);

public sealed record VirtualMonitorRequirement(
    int PhysicalFhdTargetCount,
    int ActiveVirtualQhdCount,
    int IndependentPairCapacity,
    int ActiveVirtualQhdDeficit,
    int? ConfiguredMonitorCount,
    int? ConfiguredMonitorDeficit,
    int FullCoverageTargetCount,
    string Summary)
{
    public bool HasFullActiveCoverage =>
        PhysicalFhdTargetCount > 0 &&
        ActiveVirtualQhdDeficit == 0;
}

public sealed record DriverEnvironmentReport(
    DriverStatus Status,
    IReadOnlyList<DriverDeviceInfo> DisplayDevices,
    IReadOnlyList<DriverDeviceInfo> IddSampleDriverCandidates,
    int ActiveVirtualDisplayCount,
    int ActiveVirtualQhdCount,
    IddSampleConfiguration Configuration,
    VirtualMonitorRequirement Requirement,
    string Summary);
