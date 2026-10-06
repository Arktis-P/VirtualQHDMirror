using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using VirtualQHDMirror.Core.Displays;

namespace VirtualQHDMirror.DriverManager;

public sealed class DriverEnvironmentProbe
{
    private const uint DigcfPresent = 0x00000002;
    private const uint SpdrpDeviceDesc = 0x00000000;
    private const uint SpdrpHardwareId = 0x00000001;
    private const uint SpdrpDriver = 0x00000009;
    private const uint SpdrpMfg = 0x0000000B;
    private const uint SpdrpFriendlyName = 0x0000000C;
    private const int ErrorNoMoreItems = 259;

    private static readonly Guid DisplayClassGuid =
        new("4D36E968-E325-11CE-BFC1-08002BE10318");

    public DriverEnvironmentReport Probe(
        IReadOnlyList<DisplayEndpoint> virtualDisplays,
        int physicalFhdTargetCount)
    {
        ArgumentNullException.ThrowIfNull(virtualDisplays);

        if (physicalFhdTargetCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(physicalFhdTargetCount),
                "Physical target count cannot be negative.");
        }

        try
        {
            var devices = EnumeratePresentDisplayDevices();
            var iddCandidates = devices
                .Where(IsIddSampleDriverCandidate)
                .ToArray();

            var activeVirtualCount = virtualDisplays.Count;
            var activeVirtualQhdCount = virtualDisplays.Count(display => display.IsQhd);
            var configuration = ReadIddSampleConfiguration();
            var requirement = AssessVirtualMonitorRequirement(
                physicalFhdTargetCount,
                activeVirtualQhdCount,
                configuration.ConfiguredMonitorCount);

            var status = DetermineStatus(
                iddCandidates.Length,
                activeVirtualCount);

            return new DriverEnvironmentReport(
                Status: status,
                DisplayDevices: devices,
                IddSampleDriverCandidates: iddCandidates,
                ActiveVirtualDisplayCount: activeVirtualCount,
                ActiveVirtualQhdCount: activeVirtualQhdCount,
                Configuration: configuration,
                Requirement: requirement,
                Summary: BuildSummary(
                    status,
                    iddCandidates,
                    activeVirtualCount,
                    activeVirtualQhdCount,
                    configuration));
        }
        catch (Exception exception)
        {
            var configuration = ReadIddSampleConfiguration();
            var requirement = AssessVirtualMonitorRequirement(
                physicalFhdTargetCount,
                virtualDisplays.Count(display => display.IsQhd),
                configuration.ConfiguredMonitorCount);

            return new DriverEnvironmentReport(
                Status: DriverStatus.Error,
                DisplayDevices: Array.Empty<DriverDeviceInfo>(),
                IddSampleDriverCandidates: Array.Empty<DriverDeviceInfo>(),
                ActiveVirtualDisplayCount: virtualDisplays.Count,
                ActiveVirtualQhdCount: virtualDisplays.Count(display => display.IsQhd),
                Configuration: configuration,
                Requirement: requirement,
                Summary: $"Driver probe failed: {exception.Message}");
        }
    }

    private static VirtualMonitorRequirement AssessVirtualMonitorRequirement(
        int physicalFhdTargetCount,
        int activeVirtualQhdCount,
        int? configuredMonitorCount)
    {
        var pairCapacity = Math.Min(
            physicalFhdTargetCount,
            activeVirtualQhdCount);

        var activeDeficit = Math.Max(
            0,
            physicalFhdTargetCount - activeVirtualQhdCount);

        int? configuredDeficit = configuredMonitorCount.HasValue
            ? Math.Max(0, physicalFhdTargetCount - configuredMonitorCount.Value)
            : null;

        var summary = BuildVirtualMonitorRequirementSummary(
            physicalFhdTargetCount,
            activeVirtualQhdCount,
            pairCapacity,
            activeDeficit,
            configuredMonitorCount,
            configuredDeficit);

        return new VirtualMonitorRequirement(
            PhysicalFhdTargetCount: physicalFhdTargetCount,
            ActiveVirtualQhdCount: activeVirtualQhdCount,
            IndependentPairCapacity: pairCapacity,
            ActiveVirtualQhdDeficit: activeDeficit,
            ConfiguredMonitorCount: configuredMonitorCount,
            ConfiguredMonitorDeficit: configuredDeficit,
            FullCoverageTargetCount: physicalFhdTargetCount,
            Summary: summary);
    }

    private static string BuildVirtualMonitorRequirementSummary(
        int physicalFhdTargetCount,
        int activeVirtualQhdCount,
        int pairCapacity,
        int activeDeficit,
        int? configuredMonitorCount,
        int? configuredDeficit)
    {
        if (physicalFhdTargetCount == 0)
        {
            return "No physical FHD target is currently eligible for independent QHD pairing.";
        }

        var parts = new List<string>();

        if (activeDeficit == 0)
        {
            parts.Add(
                $"Full 1:1 QHD coverage is ready: {pairCapacity}/{physicalFhdTargetCount} physical FHD target(s) can be paired independently.");
        }
        else
        {
            parts.Add(
                $"Full 1:1 QHD coverage: {pairCapacity}/{physicalFhdTargetCount} ready; {activeDeficit} additional active virtual QHD source(s) would be needed if every physical FHD target is used independently.");
        }

        if (configuredMonitorCount is null)
        {
            parts.Add("Driver-configured monitor count is unavailable.");
        }
        else if (configuredDeficit > 0)
        {
            parts.Add(
                $"Driver config currently requests {configuredMonitorCount} monitor(s); full 1:1 coverage would require {physicalFhdTargetCount}.");
        }
        else if (configuredMonitorCount == physicalFhdTargetCount)
        {
            parts.Add(
                $"Driver config already requests {configuredMonitorCount} monitor(s), matching the full-coverage target.");
        }
        else
        {
            parts.Add(
                $"Driver config requests {configuredMonitorCount} monitor(s), above the {physicalFhdTargetCount} currently eligible physical FHD target(s).");
        }

        if (configuredMonitorCount is int configured &&
            configured >= physicalFhdTargetCount &&
            activeVirtualQhdCount < physicalFhdTargetCount)
        {
            parts.Add(
                "The config count is sufficient, but not all requested QHD virtual displays are active yet.");
        }

        return string.Join(" ", parts);
    }

    private static DriverStatus DetermineStatus(
        int knownDriverCount,
        int activeVirtualDisplayCount)
    {
        if (knownDriverCount > 0 && activeVirtualDisplayCount > 0)
        {
            return DriverStatus.Active;
        }

        if (knownDriverCount > 0)
        {
            return DriverStatus.Installed;
        }

        if (activeVirtualDisplayCount > 0)
        {
            return DriverStatus.Unknown;
        }

        return DriverStatus.NotInstalled;
    }

    private static string BuildSummary(
        DriverStatus status,
        IReadOnlyList<DriverDeviceInfo> iddCandidates,
        int activeVirtualCount,
        int activeVirtualQhdCount,
        IddSampleConfiguration configuration)
    {
        var parts = new List<string>
        {
            status switch
            {
                DriverStatus.Active => "IddSampleDriver-compatible display device detected and active",
                DriverStatus.Installed => "IddSampleDriver-compatible display device detected but no active virtual display was classified",
                DriverStatus.NotInstalled => "No IddSampleDriver-compatible display device or active virtual display was detected",
                DriverStatus.Unknown => "An active virtual display exists, but its installed display driver was not recognized as IddSampleDriver-compatible",
                DriverStatus.Error => "Driver probe failed",
                _ => "Driver state is unknown"
            }
        };

        if (iddCandidates.Count > 0)
        {
            parts.Add(
                $"device: {string.Join(", ", iddCandidates.Select(device => device.DisplayName).Distinct(StringComparer.OrdinalIgnoreCase))}");
        }

        parts.Add(
            $"active virtual: {activeVirtualCount} ({activeVirtualQhdCount} QHD)");

        if (configuration.Exists)
        {
            if (configuration.ConfiguredMonitorCount is int configuredCount)
            {
                parts.Add($"config requests {configuredCount} monitor(s)");
            }

            parts.Add(
                configuration.IncludesQhd2560x1440
                    ? "2560×1440 mode configured"
                    : "2560×1440 mode not found in config");
        }
        else
        {
            parts.Add($"config not found: {configuration.Path}");
        }

        return string.Join(" · ", parts);
    }

    private static DriverDeviceInfo[] EnumeratePresentDisplayDevices()
    {
        var classGuid = DisplayClassGuid;
        var deviceInfoSet = NativeMethods.SetupDiGetClassDevs(
            ref classGuid,
            null,
            IntPtr.Zero,
            DigcfPresent);

        if (deviceInfoSet == NativeMethods.InvalidHandleValue)
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "SetupDiGetClassDevs failed for the Display device class.");
        }

        try
        {
            var devices = new List<DriverDeviceInfo>();

            for (uint index = 0; ; index++)
            {
                var deviceInfo = new SpDevInfoData
                {
                    CbSize = checked((uint)Marshal.SizeOf<SpDevInfoData>())
                };

                if (!NativeMethods.SetupDiEnumDeviceInfo(
                        deviceInfoSet,
                        index,
                        ref deviceInfo))
                {
                    var error = Marshal.GetLastWin32Error();

                    if (error == ErrorNoMoreItems)
                    {
                        break;
                    }

                    throw new Win32Exception(
                        error,
                        $"SetupDiEnumDeviceInfo failed at index {index}.");
                }

                devices.Add(new DriverDeviceInfo(
                    Description: ReadStringProperty(
                        deviceInfoSet,
                        ref deviceInfo,
                        SpdrpDeviceDesc),
                    FriendlyName: ReadStringProperty(
                        deviceInfoSet,
                        ref deviceInfo,
                        SpdrpFriendlyName),
                    Manufacturer: ReadStringProperty(
                        deviceInfoSet,
                        ref deviceInfo,
                        SpdrpMfg),
                    InstanceId: ReadDeviceInstanceId(
                        deviceInfoSet,
                        ref deviceInfo),
                    HardwareIds: ReadMultiStringProperty(
                        deviceInfoSet,
                        ref deviceInfo,
                        SpdrpHardwareId),
                    DriverKey: ReadStringProperty(
                        deviceInfoSet,
                        ref deviceInfo,
                        SpdrpDriver)));
            }

            return devices.ToArray();
        }
        finally
        {
            NativeMethods.SetupDiDestroyDeviceInfoList(deviceInfoSet);
        }
    }

    private static bool IsIddSampleDriverCandidate(DriverDeviceInfo device)
    {
        static bool ContainsMarker(string? value) =>
            !string.IsNullOrWhiteSpace(value) &&
            (value.Contains("IddSampleDriver", StringComparison.OrdinalIgnoreCase) ||
             value.Contains("Indirect Display Driver", StringComparison.OrdinalIgnoreCase));

        return ContainsMarker(device.Description) ||
               ContainsMarker(device.FriendlyName) ||
               ContainsMarker(device.InstanceId) ||
               device.HardwareIds.Any(ContainsMarker);
    }

    private static IddSampleConfiguration ReadIddSampleConfiguration()
    {
        var configuredPath = Environment.GetEnvironmentVariable(
            "IDD_SAMPLE_DRIVER_CONFIG",
            EnvironmentVariableTarget.Machine);

        var path = string.IsNullOrWhiteSpace(configuredPath)
            ? @"C:\IddSampleDriver\option.txt"
            : configuredPath.Trim();

        if (!File.Exists(path))
        {
            return new IddSampleConfiguration(
                Path: path,
                Exists: false,
                ConfiguredMonitorCount: null,
                IncludesQhd2560x1440: false,
                Modes: Array.Empty<string>(),
                Error: null);
        }

        try
        {
            var meaningfulLines = File.ReadLines(path)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0 && !line.StartsWith('#'))
                .ToArray();

            int? monitorCount = null;

            if (meaningfulLines.Length > 0 &&
                int.TryParse(meaningfulLines[0], out var parsedCount) &&
                parsedCount > 0)
            {
                monitorCount = parsedCount;
            }

            var modes = meaningfulLines
                .Skip(monitorCount.HasValue ? 1 : 0)
                .ToArray();

            var includesQhd = modes.Any(IsQhdModeLine);

            return new IddSampleConfiguration(
                Path: path,
                Exists: true,
                ConfiguredMonitorCount: monitorCount,
                IncludesQhd2560x1440: includesQhd,
                Modes: modes,
                Error: null);
        }
        catch (Exception exception)
        {
            return new IddSampleConfiguration(
                Path: path,
                Exists: true,
                ConfiguredMonitorCount: null,
                IncludesQhd2560x1440: false,
                Modes: Array.Empty<string>(),
                Error: exception.Message);
        }
    }

    private static bool IsQhdModeLine(string line)
    {
        var parts = line.Split(
            ',',
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        return parts.Length >= 2 &&
               int.TryParse(parts[0], out var width) &&
               int.TryParse(parts[1], out var height) &&
               width == 2560 &&
               height == 1440;
    }

    private static string ReadDeviceInstanceId(
        IntPtr deviceInfoSet,
        ref SpDevInfoData deviceInfo)
    {
        var capacity = 512;
        var builder = new StringBuilder(capacity);

        if (NativeMethods.SetupDiGetDeviceInstanceId(
                deviceInfoSet,
                ref deviceInfo,
                builder,
                builder.Capacity,
                out var requiredSize))
        {
            return builder.ToString();
        }

        var error = Marshal.GetLastWin32Error();

        if (requiredSize > capacity)
        {
            builder = new StringBuilder(requiredSize);

            if (NativeMethods.SetupDiGetDeviceInstanceId(
                    deviceInfoSet,
                    ref deviceInfo,
                    builder,
                    builder.Capacity,
                    out _))
            {
                return builder.ToString();
            }

            error = Marshal.GetLastWin32Error();
        }

        return $"<instance-id unavailable: {error}>";
    }

    private static string ReadStringProperty(
        IntPtr deviceInfoSet,
        ref SpDevInfoData deviceInfo,
        uint property)
    {
        var values = ReadRegistryProperty(
            deviceInfoSet,
            ref deviceInfo,
            property);

        return values.FirstOrDefault() ?? string.Empty;
    }

    private static IReadOnlyList<string> ReadMultiStringProperty(
        IntPtr deviceInfoSet,
        ref SpDevInfoData deviceInfo,
        uint property) =>
        ReadRegistryProperty(
            deviceInfoSet,
            ref deviceInfo,
            property);

    private static string[] ReadRegistryProperty(
        IntPtr deviceInfoSet,
        ref SpDevInfoData deviceInfo,
        uint property)
    {
        uint requiredSize = 0;

        NativeMethods.SetupDiGetDeviceRegistryProperty(
            deviceInfoSet,
            ref deviceInfo,
            property,
            out _,
            null,
            0,
            out requiredSize);

        if (requiredSize == 0)
        {
            return Array.Empty<string>();
        }

        var buffer = new byte[checked((int)requiredSize)];

        if (!NativeMethods.SetupDiGetDeviceRegistryProperty(
                deviceInfoSet,
                ref deviceInfo,
                property,
                out _,
                buffer,
                checked((uint)buffer.Length),
                out _))
        {
            return Array.Empty<string>();
        }

        return Encoding.Unicode
            .GetString(buffer)
            .Split(
                '\0',
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SpDevInfoData
    {
        public uint CbSize;
        public Guid ClassGuid;
        public uint DevInst;
        public UIntPtr Reserved;
    }

    private static class NativeMethods
    {
        internal static readonly IntPtr InvalidHandleValue = new(-1);

        [DllImport(
            "setupapi.dll",
            CharSet = CharSet.Unicode,
            SetLastError = true)]
        internal static extern IntPtr SetupDiGetClassDevs(
            ref Guid classGuid,
            string? enumerator,
            IntPtr hwndParent,
            uint flags);

        [DllImport(
            "setupapi.dll",
            SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetupDiEnumDeviceInfo(
            IntPtr deviceInfoSet,
            uint memberIndex,
            ref SpDevInfoData deviceInfoData);

        [DllImport(
            "setupapi.dll",
            CharSet = CharSet.Unicode,
            SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetupDiGetDeviceInstanceId(
            IntPtr deviceInfoSet,
            ref SpDevInfoData deviceInfoData,
            StringBuilder deviceInstanceId,
            int deviceInstanceIdSize,
            out int requiredSize);

        [DllImport(
            "setupapi.dll",
            CharSet = CharSet.Unicode,
            SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetupDiGetDeviceRegistryProperty(
            IntPtr deviceInfoSet,
            ref SpDevInfoData deviceInfoData,
            uint property,
            out uint propertyRegDataType,
            byte[]? propertyBuffer,
            uint propertyBufferSize,
            out uint requiredSize);

        [DllImport(
            "setupapi.dll",
            SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetupDiDestroyDeviceInfoList(
            IntPtr deviceInfoSet);
    }
}
