using System.ComponentModel;
using System.Runtime.InteropServices;

namespace VirtualQHDMirror.Core.Displays;

public sealed class DisplayDiscoveryService
{
    static DisplayDiscoveryService()
    {
        ValidateNativeLayouts();
    }

    private const uint QdcOnlyActivePaths = 0x00000002;
    private const int ErrorSuccess = 0;
    private const int ErrorInsufficientBuffer = 122;
    private const uint InvalidModeInfoIndex = 0xFFFFFFFF;

    private const uint DeviceInfoGetSourceName = 1;
    private const uint DeviceInfoGetTargetName = 2;
    private const uint DeviceInfoGetAdapterName = 4;

    private const uint TargetFlagEdidIdsValid = 0x00000004;

    private const uint OutputOther = 0xFFFFFFFF;
    private const uint OutputHd15 = 0;
    private const uint OutputSVideo = 1;
    private const uint OutputComposite = 2;
    private const uint OutputComponent = 3;
    private const uint OutputDvi = 4;
    private const uint OutputHdmi = 5;
    private const uint OutputLvds = 6;
    private const uint OutputDJpn = 8;
    private const uint OutputSdi = 9;
    private const uint OutputDisplayPortExternal = 10;
    private const uint OutputDisplayPortEmbedded = 11;
    private const uint OutputUdiExternal = 12;
    private const uint OutputUdiEmbedded = 13;
    private const uint OutputSdtvDongle = 14;
    private const uint OutputMiracast = 15;
    private const uint OutputIndirectWired = 16;
    private const uint OutputIndirectVirtual = 17;
    private const uint OutputDisplayPortUsbTunnel = 18;
    private const uint OutputInternalFlag = 0x80000000;

    private static readonly string[] VirtualMarkers =
    [
        "virtual",
        "indirect display",
        "idd",
        "spacedesk",
        "parsec"
    ];

    public DisplayDiscoveryResult DiscoverActiveDisplays()
    {
        var (paths, modes) = QueryActiveConfiguration();
        var displays = new List<DisplayEndpoint>(paths.Length);

        foreach (var path in paths)
        {
            var sourceName = GetSourceName(path.SourceInfo.AdapterId, path.SourceInfo.Id);
            var targetName = GetTargetName(path.TargetInfo.AdapterId, path.TargetInfo.Id);
            var adapterName = GetAdapterName(path.TargetInfo.AdapterId);
            var sourceMode = GetSourceMode(path.SourceInfo, modes);

            var outputTechnology = path.TargetInfo.OutputTechnology;
            var hasValidEdid = (targetName.Flags & TargetFlagEdidIdsValid) != 0;
            var classification = Classify(
                outputTechnology,
                hasValidEdid,
                targetName.MonitorFriendlyDeviceName,
                targetName.MonitorDevicePath,
                adapterName.AdapterDevicePath);

            var friendlyName = FirstNonEmpty(
                targetName.MonitorFriendlyDeviceName,
                sourceName.ViewGdiDeviceName,
                $"Display target {path.TargetInfo.Id}");

            var stableId = BuildStableId(
                path.TargetInfo.AdapterId,
                path.TargetInfo.Id,
                targetName.ConnectorInstance,
                targetName.MonitorDevicePath,
                adapterName.AdapterDevicePath);

            displays.Add(new DisplayEndpoint(
                StableId: stableId,
                SourceName: sourceName.ViewGdiDeviceName ?? string.Empty,
                FriendlyName: friendlyName,
                MonitorDevicePath: targetName.MonitorDevicePath ?? string.Empty,
                AdapterDevicePath: adapterName.AdapterDevicePath ?? string.Empty,
                SourceId: path.SourceInfo.Id,
                TargetId: path.TargetInfo.Id,
                X: sourceMode.X,
                Y: sourceMode.Y,
                Width: sourceMode.Width,
                Height: sourceMode.Height,
                OutputTechnology: FormatOutputTechnology(outputTechnology),
                OutputTechnologyValue: outputTechnology,
                IsInternal: IsInternalOutput(outputTechnology),
                HasValidEdid: hasValidEdid,
                Kind: classification.Kind,
                ClassificationReason: classification.Reason));
        }

        var orderedDisplays = displays
            .GroupBy(display => display.StableId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(display => display.X)
            .ThenBy(display => display.Y)
            .ThenBy(display => display.FriendlyName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var virtualDisplays = orderedDisplays
            .Where(display => display.Kind == DisplayDeviceKind.Virtual)
            .ToArray();

        var physicalDisplays = orderedDisplays
            .Where(display => display.Kind == DisplayDeviceKind.Physical)
            .ToArray();

        var virtualQhdCandidates = virtualDisplays
            .Where(display => display.IsQhd)
            .ToArray();

        var physicalFhdCandidates = physicalDisplays
            .Where(IsPhysicalDestinationCandidate)
            .Where(display => display.IsFhd)
            .ToArray();

        var virtualState = GetSelectionState(virtualQhdCandidates.Length);
        var physicalState = GetSelectionState(physicalFhdCandidates.Length);

        var recommendedVirtualSource = virtualState == CandidateSelectionState.SingleCandidate
            ? virtualQhdCandidates[0]
            : null;

        var recommendedPhysicalDestination = physicalState == CandidateSelectionState.SingleCandidate
            ? physicalFhdCandidates[0]
            : null;

        return new DisplayDiscoveryResult(
            Displays: orderedDisplays,
            VirtualDisplays: virtualDisplays,
            PhysicalDisplays: physicalDisplays,
            VirtualQhdCandidates: virtualQhdCandidates,
            PhysicalFhdCandidates: physicalFhdCandidates,
            VirtualSourceState: virtualState,
            PhysicalDestinationState: physicalState,
            RecommendedVirtualSource: recommendedVirtualSource,
            RecommendedPhysicalDestination: recommendedPhysicalDestination,
            Messages: BuildMessages(virtualQhdCandidates, physicalFhdCandidates));
    }

    private static void ValidateNativeLayouts()
    {
        var checks = new (string Name, int Actual, int Expected)[]
        {
            (nameof(DisplayConfigPathInfo), Marshal.SizeOf<DisplayConfigPathInfo>(), 72),
            (nameof(DisplayConfigModeInfo), Marshal.SizeOf<DisplayConfigModeInfo>(), 64),
            (nameof(DisplayConfigSourceDeviceName), Marshal.SizeOf<DisplayConfigSourceDeviceName>(), 84),
            (nameof(DisplayConfigTargetDeviceName), Marshal.SizeOf<DisplayConfigTargetDeviceName>(), 420),
            (nameof(DisplayConfigAdapterName), Marshal.SizeOf<DisplayConfigAdapterName>(), 276)
        };

        var invalid = checks.FirstOrDefault(check => check.Actual != check.Expected);

        if (invalid != default)
        {
            throw new InvalidOperationException(
                $"Unexpected Win32 display structure size for {invalid.Name}: {invalid.Actual} (expected {invalid.Expected}).");
        }
    }

    private static (DisplayConfigPathInfo[] Paths, DisplayConfigModeInfo[] Modes) QueryActiveConfiguration()
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var status = NativeMethods.GetDisplayConfigBufferSizes(
                QdcOnlyActivePaths,
                out var pathCount,
                out var modeCount);

            if (status != ErrorSuccess)
            {
                throw new Win32Exception(status, "GetDisplayConfigBufferSizes failed.");
            }

            var paths = new DisplayConfigPathInfo[checked((int)pathCount)];
            var modes = new DisplayConfigModeInfo[checked((int)modeCount)];

            status = NativeMethods.QueryDisplayConfig(
                QdcOnlyActivePaths,
                ref pathCount,
                paths,
                ref modeCount,
                modes,
                IntPtr.Zero);

            if (status == ErrorInsufficientBuffer)
            {
                continue;
            }

            if (status != ErrorSuccess)
            {
                throw new Win32Exception(status, "QueryDisplayConfig failed.");
            }

            if (pathCount != (uint)paths.Length)
            {
                Array.Resize(ref paths, checked((int)pathCount));
            }

            if (modeCount != (uint)modes.Length)
            {
                Array.Resize(ref modes, checked((int)modeCount));
            }

            return (paths, modes);
        }

        throw new Win32Exception(
            ErrorInsufficientBuffer,
            "The display topology changed repeatedly while VQM was reading it.");
    }

    private static SourceModeSnapshot GetSourceMode(
        DisplayConfigPathSourceInfo sourceInfo,
        DisplayConfigModeInfo[] modes)
    {
        if (sourceInfo.ModeInfoIdx == InvalidModeInfoIndex ||
            sourceInfo.ModeInfoIdx >= (uint)modes.Length)
        {
            return SourceModeSnapshot.Empty;
        }

        var mode = modes[checked((int)sourceInfo.ModeInfoIdx)];

        if (mode.InfoType != DisplayConfigModeInfoType.Source ||
            mode.Id != sourceInfo.Id ||
            !mode.AdapterId.Equals(sourceInfo.AdapterId))
        {
            return SourceModeSnapshot.Empty;
        }

        return new SourceModeSnapshot(
            X: mode.ModeInfo.SourceMode.Position.X,
            Y: mode.ModeInfo.SourceMode.Position.Y,
            Width: checked((int)mode.ModeInfo.SourceMode.Width),
            Height: checked((int)mode.ModeInfo.SourceMode.Height));
    }

    private static DisplayConfigSourceDeviceName GetSourceName(Luid adapterId, uint sourceId)
    {
        var request = new DisplayConfigSourceDeviceName
        {
            Header = CreateHeader(DeviceInfoGetSourceName, adapterId, sourceId, Marshal.SizeOf<DisplayConfigSourceDeviceName>())
        };

        var status = NativeMethods.DisplayConfigGetDeviceInfo(ref request);
        return status == ErrorSuccess ? request : default;
    }

    private static DisplayConfigTargetDeviceName GetTargetName(Luid adapterId, uint targetId)
    {
        var request = new DisplayConfigTargetDeviceName
        {
            Header = CreateHeader(DeviceInfoGetTargetName, adapterId, targetId, Marshal.SizeOf<DisplayConfigTargetDeviceName>())
        };

        var status = NativeMethods.DisplayConfigGetDeviceInfo(ref request);
        return status == ErrorSuccess ? request : default;
    }

    private static DisplayConfigAdapterName GetAdapterName(Luid adapterId)
    {
        var request = new DisplayConfigAdapterName
        {
            Header = CreateHeader(DeviceInfoGetAdapterName, adapterId, 0, Marshal.SizeOf<DisplayConfigAdapterName>())
        };

        var status = NativeMethods.DisplayConfigGetDeviceInfo(ref request);
        return status == ErrorSuccess ? request : default;
    }

    private static DisplayConfigDeviceInfoHeader CreateHeader(
        uint type,
        Luid adapterId,
        uint id,
        int size) =>
        new()
        {
            Type = type,
            Size = checked((uint)size),
            AdapterId = adapterId,
            Id = id
        };

    private static Classification Classify(
        uint outputTechnology,
        bool hasValidEdid,
        string? friendlyName,
        string? monitorDevicePath,
        string? adapterDevicePath)
    {
        if (GetBaseOutputTechnology(outputTechnology) == OutputIndirectVirtual)
        {
            return new Classification(DisplayDeviceKind.Virtual, "INDIRECT_VIRTUAL output");
        }

        if (ContainsVirtualMarker(friendlyName, monitorDevicePath, adapterDevicePath))
        {
            return new Classification(DisplayDeviceKind.Virtual, "virtual display name/device-path marker");
        }

        if (!string.IsNullOrWhiteSpace(adapterDevicePath) &&
            adapterDevicePath.Contains("ROOT#DISPLAY", StringComparison.OrdinalIgnoreCase))
        {
            return new Classification(DisplayDeviceKind.Virtual, "root-enumerated display adapter");
        }

        var baseTechnology = GetBaseOutputTechnology(outputTechnology);

        if (baseTechnology == OutputIndirectWired)
        {
            return new Classification(
                DisplayDeviceKind.Unknown,
                "INDIRECT_WIRED can represent a software display or a physical USB display");
        }

        if (IsInternalOutput(outputTechnology))
        {
            return new Classification(DisplayDeviceKind.Physical, "internal/embedded display connector");
        }

        if (IsKnownDirectPhysicalOutput(baseTechnology))
        {
            return new Classification(DisplayDeviceKind.Physical, "direct physical display connector");
        }

        if (hasValidEdid)
        {
            return new Classification(DisplayDeviceKind.Physical, "valid monitor EDID");
        }

        return new Classification(DisplayDeviceKind.Unknown, "insufficient evidence");
    }

    private static bool ContainsVirtualMarker(params string?[] values)
    {
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            foreach (var marker in VirtualMarkers)
            {
                if (value.Contains(marker, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsPhysicalDestinationCandidate(DisplayEndpoint display)
    {
        if (display.Kind != DisplayDeviceKind.Physical || display.IsInternal)
        {
            return false;
        }

        var baseTechnology = GetBaseOutputTechnology(display.OutputTechnologyValue);

        return baseTechnology is not OutputMiracast
            and not OutputIndirectWired
            and not OutputIndirectVirtual;
    }

    private static bool IsKnownDirectPhysicalOutput(uint baseTechnology) =>
        baseTechnology is
            OutputHd15 or
            OutputSVideo or
            OutputComposite or
            OutputComponent or
            OutputDvi or
            OutputHdmi or
            OutputLvds or
            OutputDJpn or
            OutputSdi or
            OutputDisplayPortExternal or
            OutputDisplayPortEmbedded or
            OutputUdiExternal or
            OutputUdiEmbedded or
            OutputSdtvDongle or
            OutputDisplayPortUsbTunnel;

    private static bool IsInternalOutput(uint outputTechnology)
    {
        if (outputTechnology == OutputOther)
        {
            return false;
        }

        var baseTechnology = GetBaseOutputTechnology(outputTechnology);

        return outputTechnology == OutputInternalFlag ||
               (outputTechnology & OutputInternalFlag) != 0 ||
               baseTechnology is OutputLvds or OutputDisplayPortEmbedded or OutputUdiEmbedded;
    }

    private static uint GetBaseOutputTechnology(uint outputTechnology)
    {
        if (outputTechnology == OutputOther)
        {
            return OutputOther;
        }

        return outputTechnology & ~OutputInternalFlag;
    }

    private static string FormatOutputTechnology(uint outputTechnology)
    {
        if (outputTechnology == OutputOther)
        {
            return "Other";
        }

        if (outputTechnology == OutputInternalFlag)
        {
            return "Internal";
        }

        var baseTechnology = GetBaseOutputTechnology(outputTechnology);
        var name = baseTechnology switch
        {
            OutputHd15 => "HD15",
            OutputSVideo => "S-Video",
            OutputComposite => "Composite",
            OutputComponent => "Component",
            OutputDvi => "DVI",
            OutputHdmi => "HDMI",
            OutputLvds => "LVDS",
            OutputDJpn => "D-JPN",
            OutputSdi => "SDI",
            OutputDisplayPortExternal => "DisplayPort",
            OutputDisplayPortEmbedded => "DisplayPort Embedded",
            OutputUdiExternal => "UDI",
            OutputUdiEmbedded => "UDI Embedded",
            OutputSdtvDongle => "SDTV Dongle",
            OutputMiracast => "Miracast",
            OutputIndirectWired => "Indirect Wired",
            OutputIndirectVirtual => "Indirect Virtual",
            OutputDisplayPortUsbTunnel => "DisplayPort USB Tunnel",
            _ => $"Unknown ({baseTechnology})"
        };

        return (outputTechnology & OutputInternalFlag) != 0
            ? $"{name} / Internal"
            : name;
    }

    private static string BuildStableId(
        Luid adapterId,
        uint targetId,
        uint connectorInstance,
        string? monitorDevicePath,
        string? adapterDevicePath)
    {
        if (!string.IsNullOrWhiteSpace(monitorDevicePath))
        {
            return $"{monitorDevicePath.Trim()}:{connectorInstance}".ToUpperInvariant();
        }

        if (!string.IsNullOrWhiteSpace(adapterDevicePath))
        {
            return $"{adapterDevicePath.Trim()}:{targetId}:{connectorInstance}".ToUpperInvariant();
        }

        var adapterKey = $"{unchecked((uint)adapterId.HighPart):X8}{adapterId.LowPart:X8}";
        return $"{adapterKey}:{targetId}:{connectorInstance}".ToUpperInvariant();
    }

    private static CandidateSelectionState GetSelectionState(int count) =>
        count switch
        {
            0 => CandidateSelectionState.Missing,
            1 => CandidateSelectionState.SingleCandidate,
            _ => CandidateSelectionState.MultipleCandidates
        };

    private static IReadOnlyList<string> BuildMessages(
        IReadOnlyList<DisplayEndpoint> virtualQhdCandidates,
        IReadOnlyList<DisplayEndpoint> physicalFhdCandidates)
    {
        var messages = new List<string>();

        messages.Add(virtualQhdCandidates.Count switch
        {
            0 => "No active 2560 × 1440 virtual display was identified.",
            1 => $"Virtual QHD source identified: {virtualQhdCandidates[0].FriendlyName}.",
            _ => $"{virtualQhdCandidates.Count} virtual QHD sources were identified; source selection is ambiguous."
        });

        messages.Add(physicalFhdCandidates.Count switch
        {
            0 => "No external physical 1920 × 1080 destination was identified.",
            1 => $"Physical FHD destination identified: {physicalFhdCandidates[0].FriendlyName}.",
            _ => $"{physicalFhdCandidates.Count} physical FHD destinations were identified; VQM will not choose one automatically yet."
        });

        return messages;
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private sealed record Classification(DisplayDeviceKind Kind, string Reason);

    private readonly record struct SourceModeSnapshot(int X, int Y, int Width, int Height)
    {
        public static SourceModeSnapshot Empty => new(0, 0, 0, 0);
    }

    private enum DisplayConfigModeInfoType : uint
    {
        Source = 1,
        Target = 2,
        DesktopImage = 3
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Luid : IEquatable<Luid>
    {
        public uint LowPart;
        public int HighPart;

        public bool Equals(Luid other) =>
            LowPart == other.LowPart && HighPart == other.HighPart;

        public override bool Equals(object? obj) =>
            obj is Luid other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(LowPart, HighPart);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigPathInfo
    {
        public DisplayConfigPathSourceInfo SourceInfo;
        public DisplayConfigPathTargetInfo TargetInfo;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigPathSourceInfo
    {
        public Luid AdapterId;
        public uint Id;
        public uint ModeInfoIdx;
        public uint StatusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigPathTargetInfo
    {
        public Luid AdapterId;
        public uint Id;
        public uint ModeInfoIdx;
        public uint OutputTechnology;
        public uint Rotation;
        public uint Scaling;
        public DisplayConfigRational RefreshRate;
        public uint ScanLineOrdering;
        public int TargetAvailable;
        public uint StatusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigRational
    {
        public uint Numerator;
        public uint Denominator;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigModeInfo
    {
        public DisplayConfigModeInfoType InfoType;
        public uint Id;
        public Luid AdapterId;
        public DisplayConfigModeInfoUnion ModeInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct DisplayConfigModeInfoUnion
    {
        [FieldOffset(0)]
        public DisplayConfigTargetMode TargetMode;

        [FieldOffset(0)]
        public DisplayConfigSourceMode SourceMode;

        [FieldOffset(0)]
        public DisplayConfigDesktopImageInfo DesktopImageInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigTargetMode
    {
        public DisplayConfigVideoSignalInfo TargetVideoSignalInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigVideoSignalInfo
    {
        public ulong PixelRate;
        public DisplayConfigRational HSyncFreq;
        public DisplayConfigRational VSyncFreq;
        public DisplayConfig2DRegion ActiveSize;
        public DisplayConfig2DRegion TotalSize;
        public uint VideoStandard;
        public uint ScanLineOrdering;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfig2DRegion
    {
        public uint Cx;
        public uint Cy;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigSourceMode
    {
        public uint Width;
        public uint Height;
        public uint PixelFormat;
        public PointL Position;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigDesktopImageInfo
    {
        public PointL PathSourceSize;
        public RectL DesktopImageRegion;
        public RectL DesktopImageClip;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PointL
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RectL
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigDeviceInfoHeader
    {
        public uint Type;
        public uint Size;
        public Luid AdapterId;
        public uint Id;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DisplayConfigSourceDeviceName
    {
        public DisplayConfigDeviceInfoHeader Header;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string? ViewGdiDeviceName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DisplayConfigTargetDeviceName
    {
        public DisplayConfigDeviceInfoHeader Header;
        public uint Flags;
        public uint OutputTechnology;
        public ushort EdidManufactureId;
        public ushort EdidProductCodeId;
        public uint ConnectorInstance;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string? MonitorFriendlyDeviceName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string? MonitorDevicePath;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DisplayConfigAdapterName
    {
        public DisplayConfigDeviceInfoHeader Header;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string? AdapterDevicePath;
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll", ExactSpelling = true)]
        public static extern int GetDisplayConfigBufferSizes(
            uint flags,
            out uint numPathArrayElements,
            out uint numModeInfoArrayElements);

        [DllImport("user32.dll", ExactSpelling = true)]
        public static extern int QueryDisplayConfig(
            uint flags,
            ref uint numPathArrayElements,
            [Out] DisplayConfigPathInfo[] pathInfoArray,
            ref uint numModeInfoArrayElements,
            [Out] DisplayConfigModeInfo[] modeInfoArray,
            IntPtr currentTopologyId);

        [DllImport("user32.dll", ExactSpelling = true)]
        public static extern int DisplayConfigGetDeviceInfo(
            ref DisplayConfigSourceDeviceName requestPacket);

        [DllImport("user32.dll", ExactSpelling = true)]
        public static extern int DisplayConfigGetDeviceInfo(
            ref DisplayConfigTargetDeviceName requestPacket);

        [DllImport("user32.dll", ExactSpelling = true)]
        public static extern int DisplayConfigGetDeviceInfo(
            ref DisplayConfigAdapterName requestPacket);
    }
}
