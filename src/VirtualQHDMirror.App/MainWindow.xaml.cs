using System.Windows;
using VirtualQHDMirror.Core.Displays;

namespace VirtualQHDMirror.App;

public partial class MainWindow : Window
{
    private readonly DisplayDiscoveryService _displayDiscovery = new();

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshDisplays();
    }

    private void RefreshDisplays_Click(object sender, RoutedEventArgs e)
    {
        RefreshDisplays();
    }

    private void RefreshDisplays()
    {
        try
        {
            var result = _displayDiscovery.DiscoverActiveDisplays();

            DisplayList.ItemsSource = result.Displays.Select(display => new
            {
                Name = display.FriendlyName,
                Kind = display.Kind.ToString(),
                Resolution = display.Resolution,
                Placement = display.Placement,
                Connector = display.OutputTechnology,
                Source = display.SourceName,
                Reason = display.ClassificationReason
            }).ToArray();

            VirtualSummaryText.Text = result.VirtualSourceState switch
            {
                CandidateSelectionState.SingleCandidate =>
                    $"1 QHD virtual source: {result.RecommendedVirtualSource!.FriendlyName}",
                CandidateSelectionState.MultipleCandidates =>
                    $"{result.VirtualQhdCandidates.Count} QHD virtual sources found — selection required",
                _ => $"No QHD virtual source found ({result.VirtualDisplays.Count} virtual display(s) identified)"
            };

            PhysicalSummaryText.Text = result.PhysicalDestinationState switch
            {
                CandidateSelectionState.SingleCandidate when result.Topology.CanAutoPairSingleSession =>
                    $"1 physical FHD destination: {result.RecommendedPhysicalDestination!.FriendlyName} ({result.RecommendedPhysicalDestination.Placement})",
                CandidateSelectionState.SingleCandidate =>
                    $"1 physical FHD destination found; source pairing still requires a choice",
                CandidateSelectionState.MultipleCandidates =>
                    $"{result.PhysicalFhdCandidates.Count} physical FHD destinations found — pairing policy required",
                _ => $"No physical FHD destination found ({result.PhysicalDisplays.Count} physical display(s) identified)"
            };

            StatusText.Text = string.Join(Environment.NewLine, result.Messages);
        }
        catch (Exception exception)
        {
            DisplayList.ItemsSource = null;
            VirtualSummaryText.Text = "Display discovery failed.";
            PhysicalSummaryText.Text = "Display discovery failed.";
            StatusText.Text = $"{exception.GetType().Name}: {exception.Message}";
        }
    }
}
