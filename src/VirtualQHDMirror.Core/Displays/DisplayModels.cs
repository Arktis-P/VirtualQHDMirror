namespace VirtualQHDMirror.Core.Displays;

public enum DisplayDeviceKind
{
    Unknown,
    Physical,
    Virtual
}

public enum CandidateSelectionState
{
    Missing,
    SingleCandidate,
    MultipleCandidates
}

public enum MirrorTopologyKind
{
    MissingVirtualSource,
    MissingPhysicalDestination,
    SinglePairReady,
    OneVirtualToMultiplePhysical,
    MultipleVirtualToOnePhysical,
    MultipleVirtualAndPhysical
}

public sealed record DisplayEndpoint(
    string StableId,
    string SourceName,
    string FriendlyName,
    string MonitorDevicePath,
    string AdapterDevicePath,
    uint SourceId,
    uint TargetId,
    int X,
    int Y,
    int Width,
    int Height,
    string OutputTechnology,
    uint OutputTechnologyValue,
    bool IsInternal,
    bool HasValidEdid,
    DisplayDeviceKind Kind,
    string ClassificationReason)
{
    public bool IsFhd => Width == 1920 && Height == 1080;
    public bool IsQhd => Width == 2560 && Height == 1440;
    public bool IsExternal => !IsInternal;
    public string Placement => IsInternal ? "Internal" : "External";
    public string Resolution => Width > 0 && Height > 0 ? $"{Width} × {Height}" : "Unknown";
}

public sealed record MirrorTopologyAssessment(
    MirrorTopologyKind Kind,
    int VirtualQhdCount,
    int PhysicalFhdCount,
    int IndependentPairCapacity,
    int AdditionalVirtualSourcesForFullPairing,
    bool CanAutoPairSingleSession,
    string Summary);

public sealed record DisplayDiscoveryResult(
    IReadOnlyList<DisplayEndpoint> Displays,
    IReadOnlyList<DisplayEndpoint> VirtualDisplays,
    IReadOnlyList<DisplayEndpoint> PhysicalDisplays,
    IReadOnlyList<DisplayEndpoint> VirtualQhdCandidates,
    IReadOnlyList<DisplayEndpoint> PhysicalFhdCandidates,
    IReadOnlyList<DisplayEndpoint> ExternalPhysicalFhdCandidates,
    IReadOnlyList<DisplayEndpoint> InternalPhysicalFhdCandidates,
    CandidateSelectionState VirtualSourceState,
    CandidateSelectionState PhysicalDestinationState,
    DisplayEndpoint? RecommendedVirtualSource,
    DisplayEndpoint? RecommendedPhysicalDestination,
    MirrorTopologyAssessment Topology,
    IReadOnlyList<string> Messages);
