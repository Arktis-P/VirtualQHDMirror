using System.Windows;
using System.Windows.Controls;
using VirtualQHDMirror.Core.Displays;
using VirtualQHDMirror.DriverManager;

namespace VirtualQHDMirror.App;

public partial class MainWindow : Window
{
    private readonly DisplayDiscoveryService _displayDiscovery = new();
    private readonly DriverEnvironmentProbe _driverProbe = new();
    private readonly LegacyMirrorProcessController _mirrorController = new();

    public MainWindow()
    {
        InitializeComponent();

        _mirrorController.StateChanged += MirrorController_StateChanged;
        Loaded += (_, _) =>
        {
            _mirrorController.DetectExistingSession();
            RefreshDisplays();
        };
        Closed += (_, _) =>
        {
            _mirrorController.StateChanged -= MirrorController_StateChanged;
            _mirrorController.Dispose();
        };
    }

    private void RefreshDisplays_Click(object sender, RoutedEventArgs e)
    {
        RefreshDisplays();
    }

    private void PairSelection_Changed(object sender, SelectionChangedEventArgs e)
    {
        UpdateMirrorControls();
    }

    private void StartMirror_Click(object sender, RoutedEventArgs e)
    {
        if (VirtualSourceComboBox.SelectedItem is not DisplayChoice source ||
            PhysicalDestinationComboBox.SelectedItem is not DisplayChoice destination)
        {
            MessageBox.Show(
                this,
                "Select one virtual QHD source and one physical FHD destination.",
                "VirtualQHDMirror",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        try
        {
            _mirrorController.Start(
                source.Endpoint.SourceName,
                destination.Endpoint.SourceName);

            MirrorStateText.Text =
                $"Starting mirror: {source.Endpoint.FriendlyName} → {destination.Endpoint.FriendlyName}";
            UpdateMirrorControls();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                exception.Message,
                "Could not start mirror",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            MirrorStateText.Text = "Mirror failed to start.";
            UpdateMirrorControls();
        }
    }

    private void StopMirror_Click(object sender, RoutedEventArgs e)
    {
        if (_mirrorController.RequestStop())
        {
            MirrorStateText.Text = "Stopping mirror...";
            return;
        }

        MessageBox.Show(
            this,
            "The mirror window is not ready to receive a clean stop request yet. Wait a moment and try again, or use Ctrl+Alt+F12 while the v0.14.1 mirror is running.",
            "VirtualQHDMirror",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void RefreshDisplays()
    {
        var previousVirtualId =
            (VirtualSourceComboBox.SelectedItem as DisplayChoice)?.Endpoint.StableId;

        var previousPhysicalId =
            (PhysicalDestinationComboBox.SelectedItem as DisplayChoice)?.Endpoint.StableId;

        try
        {
            var result = _displayDiscovery.DiscoverActiveDisplays();
            var driverReport = _driverProbe.Probe(
                result.VirtualDisplays,
                result.PhysicalFhdCandidates.Count);

            DriverSummaryText.Text = driverReport.Summary;
            CapacitySummaryText.Text = driverReport.Requirement.Summary;

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

            var virtualChoices = result.VirtualQhdCandidates
                .Select(display => new DisplayChoice(
                    display,
                    $"{display.FriendlyName} — {display.Resolution} — {display.SourceName}"))
                .ToArray();

            var physicalChoices = result.PhysicalFhdCandidates
                .Select(display => new DisplayChoice(
                    display,
                    $"{display.FriendlyName} — {display.Resolution} — {display.Placement} — {display.SourceName}"))
                .ToArray();

            VirtualSourceComboBox.ItemsSource = virtualChoices;
            PhysicalDestinationComboBox.ItemsSource = physicalChoices;

            RestoreSelection(
                VirtualSourceComboBox,
                virtualChoices,
                previousVirtualId);

            RestoreSelection(
                PhysicalDestinationComboBox,
                physicalChoices,
                previousPhysicalId);

            VirtualSummaryText.Text = result.VirtualSourceState switch
            {
                CandidateSelectionState.SingleCandidate =>
                    $"1 QHD virtual source: {result.VirtualQhdCandidates[0].FriendlyName}",
                CandidateSelectionState.MultipleCandidates =>
                    $"{result.VirtualQhdCandidates.Count} QHD virtual sources found — choose a source below",
                _ => $"No QHD virtual source found ({result.VirtualDisplays.Count} virtual display(s) identified)"
            };

            PhysicalSummaryText.Text = result.PhysicalDestinationState switch
            {
                CandidateSelectionState.SingleCandidate =>
                    $"1 physical FHD destination: {result.PhysicalFhdCandidates[0].FriendlyName} ({result.PhysicalFhdCandidates[0].Placement})",
                CandidateSelectionState.MultipleCandidates =>
                    $"{result.PhysicalFhdCandidates.Count} physical FHD destinations found — choose a destination below",
                _ => $"No physical FHD destination found ({result.PhysicalDisplays.Count} physical display(s) identified)"
            };

            StatusText.Text = string.Join(Environment.NewLine, result.Messages);
            UpdateMirrorControls();
        }
        catch (Exception exception)
        {
            DisplayList.ItemsSource = null;
            VirtualSourceComboBox.ItemsSource = null;
            PhysicalDestinationComboBox.ItemsSource = null;
            VirtualSummaryText.Text = "Display discovery failed.";
            PhysicalSummaryText.Text = "Display discovery failed.";
            DriverSummaryText.Text = "Driver probe unavailable because display discovery failed.";
            CapacitySummaryText.Text = "Virtual-monitor capacity could not be assessed.";
            StatusText.Text = $"{exception.GetType().Name}: {exception.Message}";
            UpdateMirrorControls();
        }
    }

    private static void RestoreSelection(
        ComboBox comboBox,
        IReadOnlyList<DisplayChoice> choices,
        string? previousStableId)
    {
        var previous = choices.FirstOrDefault(choice =>
            string.Equals(
                choice.Endpoint.StableId,
                previousStableId,
                StringComparison.OrdinalIgnoreCase));

        if (previous is not null)
        {
            comboBox.SelectedItem = previous;
            return;
        }

        comboBox.SelectedIndex = choices.Count == 1 ? 0 : -1;
    }

    private void UpdateMirrorControls()
    {
        var sessionState = _mirrorController.SessionState;
        var running = sessionState == LegacyMirrorSessionState.Attached;
        var multipleSessions = sessionState == LegacyMirrorSessionState.Multiple;
        var hasPair =
            VirtualSourceComboBox.SelectedItem is DisplayChoice &&
            PhysicalDestinationComboBox.SelectedItem is DisplayChoice;

        StartMirrorButton.IsEnabled =
            !running &&
            !multipleSessions &&
            hasPair;

        StopMirrorButton.IsEnabled = running;
        VirtualSourceComboBox.IsEnabled = !running && !multipleSessions;
        PhysicalDestinationComboBox.IsEnabled = !running && !multipleSessions;
        RefreshButton.IsEnabled = !running;

        if (running)
        {
            var processText =
                _mirrorController.ProcessId is int processId
                    ? $"PID {processId}"
                    : "process attached";

            MirrorStateText.Text =
                _mirrorController.AttachedFromDiscovery
                    ? $"Attached to an existing v0.14.1 mirror session ({processText}). Closing this v0.20 window will leave the mirror running; reopening v0.20 can attach again."
                    : $"Mirror is running through the v0.14.1 stable engine ({processText}). Closing this v0.20 window will leave the mirror running.";
        }
        else if (multipleSessions)
        {
            MirrorStateText.Text =
                $"{_mirrorController.DetectedSessionCount} v0.14.1 mirror sessions were detected. VQM will not guess which one to control; Start/Stop are disabled for this compatibility bridge.";
        }
        else if (hasPair)
        {
            MirrorStateText.Text =
                "Ready. Start Mirror will launch the selected pair using the stable v0.14.1 Portal engine.";
        }
        else
        {
            MirrorStateText.Text =
                "Select one virtual QHD source and one physical FHD destination.";
        }
    }

    private void MirrorController_StateChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.CheckAccess())
        {
            UpdateMirrorControls();
            return;
        }

        Dispatcher.BeginInvoke(new Action(UpdateMirrorControls));
    }

    private sealed record DisplayChoice(
        DisplayEndpoint Endpoint,
        string Label);
}
