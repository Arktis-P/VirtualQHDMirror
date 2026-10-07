using System.Windows;
using System.Windows.Controls;
using VirtualQHDMirror.Core.Displays;
using VirtualQHDMirror.DriverManager;

namespace VirtualQHDMirror.App;

public partial class MainWindow : Window
{
    private readonly DisplayDiscoveryService _displayDiscovery = new();
    private readonly DriverEnvironmentProbe _driverProbe = new();
    private readonly DriverConfigurationPlanner _driverConfigurationPlanner = new();
    private readonly DriverPlanElevatedRunner _driverPlanRunner = new();
    private readonly LegacyMirrorProcessController _mirrorController = new();
    private readonly NativeMirrorSessionProcessController _nativeMirrorController = new();

    private DriverConfigurationPlan? _currentDriverPlan;
    private bool _driverPlanApplyInProgress;
    private bool _synchronizingEngineSelection;

    public MainWindow()
    {
        InitializeComponent();

        MirrorEngineComboBox.ItemsSource =
            new[]
            {
                new MirrorEngineChoice(
                    MirrorEngineKind.StableV0141,
                    "Stable v0.14.1 Portal"),
                new MirrorEngineChoice(
                    MirrorEngineKind.NativeDotNetPreview,
                    ".NET GDI + Portal (preview)")
            };

        MirrorEngineComboBox.SelectedIndex = 0;

        _mirrorController.StateChanged += MirrorController_StateChanged;
        _nativeMirrorController.StateChanged += MirrorController_StateChanged;
        Loaded += (_, _) =>
        {
            DetectMirrorSessions();
            RefreshDisplays();
        };
        Closed += (_, _) =>
        {
            _mirrorController.StateChanged -= MirrorController_StateChanged;
            _nativeMirrorController.StateChanged -= MirrorController_StateChanged;
            _mirrorController.Dispose();
            _nativeMirrorController.Dispose();
        };
    }

    private void RefreshDisplays_Click(object sender, RoutedEventArgs e)
    {
        DetectMirrorSessions();
        RefreshDisplays();
    }

    private void MirrorEngine_Changed(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_synchronizingEngineSelection)
        {
            return;
        }

        UpdateMirrorControls();
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
            DetectMirrorSessions();

            if (HasAnyMirrorSession())
            {
                MessageBox.Show(
                    this,
                    "A mirror session is already running or multiple sessions were detected. Stop the existing session before starting another engine.",
                    "VirtualQHDMirror",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var engine =
                (MirrorEngineComboBox.SelectedItem as MirrorEngineChoice)?.Kind ??
                MirrorEngineKind.StableV0141;

            if (engine == MirrorEngineKind.NativeDotNetPreview)
            {
                _nativeMirrorController.Start(
                    source.Endpoint.SourceName,
                    destination.Endpoint.SourceName);
            }
            else
            {
                _mirrorController.Start(
                    source.Endpoint.SourceName,
                    destination.Endpoint.SourceName);
            }

            MirrorStateText.Text =
                $"Starting {GetEngineLabel(engine)}: {source.Endpoint.FriendlyName} → {destination.Endpoint.FriendlyName}";

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
        var stopped =
            _nativeMirrorController.IsRunning
                ? _nativeMirrorController.RequestStop()
                : _mirrorController.IsRunning &&
                  _mirrorController.RequestStop();

        if (stopped)
        {
            MirrorStateText.Text = "Stopping mirror...";
            return;
        }

        MessageBox.Show(
            this,
            "The active mirror session could not receive a clean stop request. You can also use Ctrl+Alt+F12 while the mirror session is running.",
            "VirtualQHDMirror",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private async void ApplyDriverPlan_Click(
        object sender,
        RoutedEventArgs e)
    {
        DetectMirrorSessions();
        RefreshDisplays();

        var plan = _currentDriverPlan;

        if (plan is null ||
            plan.State is DriverConfigurationPlanState.NotApplicable or
                DriverConfigurationPlanState.NoChangeRequired or
                DriverConfigurationPlanState.Blocked)
        {
            return;
        }

        if (HasAnyMirrorSession())
        {
            MessageBox.Show(
                this,
                "Stop the active mirror session before changing or restarting the virtual display driver.",
                "VirtualQHDMirror",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var operationText = plan.RequiresConfigurationWrite
            ? $"increase the virtual monitor count from {plan.ConfiguredMonitorCount} to {plan.DesiredMonitorCount} and restart the IDD device"
            : "restart the detected IDD device without changing the configuration";

        var confirmed = MessageBox.Show(
            this,
            $"VirtualQHDMirror will {operationText}.\n\nWindows will request administrator approval and the virtual display topology may briefly disappear and reconnect. Continue?",
            "Apply virtual display driver plan",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmed != MessageBoxResult.Yes)
        {
            return;
        }

        _driverPlanApplyInProgress = true;
        UpdateMirrorControls();

        var result = await _driverPlanRunner.ApplyAsync(
            plan);

        _driverPlanApplyInProgress = false;

        if (!result.Success)
        {
            MessageBox.Show(
                this,
                result.Message,
                result.Cancelled
                    ? "Driver change cancelled"
                    : "Driver change failed",
                MessageBoxButton.OK,
                result.Cancelled
                    ? MessageBoxImage.Information
                    : MessageBoxImage.Error);

            UpdateMirrorControls();
            return;
        }

        DriverPlanSummaryText.Text =
            "Driver change completed. Waiting for Windows display topology to settle...";

        await Task.Delay(1500);

        DetectMirrorSessions();
        RefreshDisplays();

        MessageBox.Show(
            this,
            result.Message,
            "Driver change completed",
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

            var driverPlan =
                _driverConfigurationPlanner.BuildFullCoveragePlan(driverReport);

            _currentDriverPlan = driverPlan;

            DriverSummaryText.Text = driverReport.Summary;
            CapacitySummaryText.Text = driverReport.Requirement.Summary;
            DriverPlanSummaryText.Text =
                $"[{driverPlan.State}] {driverPlan.Summary}";

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
            _currentDriverPlan = null;
            DriverPlanSummaryText.Text = "Driver configuration plan is unavailable.";
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
        var legacyState =
            _mirrorController.SessionState;

        var nativeState =
            _nativeMirrorController.SessionState;

        var legacyRunning =
            legacyState ==
            LegacyMirrorSessionState.Attached;

        var nativeRunning =
            nativeState ==
            NativeMirrorSessionState.Attached;

        var bothEnginesRunning =
            legacyRunning &&
            nativeRunning;

        var ambiguousSessions =
            bothEnginesRunning ||
            legacyState ==
                LegacyMirrorSessionState.Multiple ||
            nativeState ==
                NativeMirrorSessionState.Multiple;

        var running =
            legacyRunning ||
            nativeRunning;

        SynchronizeEngineSelection(
            legacyRunning,
            nativeRunning);

        var hasPair =
            VirtualSourceComboBox.SelectedItem is DisplayChoice &&
            PhysicalDestinationComboBox.SelectedItem is DisplayChoice;

        var hasEngine =
            MirrorEngineComboBox.SelectedItem is MirrorEngineChoice;

        StartMirrorButton.IsEnabled =
            !running &&
            !ambiguousSessions &&
            !_driverPlanApplyInProgress &&
            hasPair &&
            hasEngine;

        StopMirrorButton.IsEnabled =
            running &&
            !ambiguousSessions &&
            !_driverPlanApplyInProgress;

        VirtualSourceComboBox.IsEnabled =
            !running &&
            !ambiguousSessions &&
            !_driverPlanApplyInProgress;

        PhysicalDestinationComboBox.IsEnabled =
            !running &&
            !ambiguousSessions &&
            !_driverPlanApplyInProgress;

        MirrorEngineComboBox.IsEnabled =
            !running &&
            !ambiguousSessions &&
            !_driverPlanApplyInProgress;

        RefreshButton.IsEnabled =
            !running &&
            !_driverPlanApplyInProgress;

        var driverPlanCanApply =
            _currentDriverPlan is
            {
                CanProceed: true,
                RequiresElevation: true
            } plan &&
            plan.State !=
                DriverConfigurationPlanState.NoChangeRequired;

        ApplyDriverPlanButton.IsEnabled =
            !running &&
            !ambiguousSessions &&
            !_driverPlanApplyInProgress &&
            driverPlanCanApply;

        ApplyDriverPlanButton.Content =
            _driverPlanApplyInProgress
                ? "Applying..."
                : _currentDriverPlan?.State switch
                {
                    DriverConfigurationPlanState.RestartRecommended =>
                        "Restart virtual display",

                    DriverConfigurationPlanState.ReadyToIncrease =>
                        "Apply & restart driver",

                    _ =>
                        "Apply driver plan"
                };

        if (_driverPlanApplyInProgress)
        {
            MirrorStateText.Text =
                "Applying virtual display driver changes with administrator approval. Do not start a mirror session until topology refresh completes.";
            return;
        }

        if (bothEnginesRunning)
        {
            MirrorStateText.Text =
                "Both the stable v0.14.1 engine and the .NET preview engine are running. VQM will not choose one automatically; stop one with Ctrl+Alt+F12 before continuing.";
            return;
        }

        if (nativeRunning)
        {
            var processText =
                _nativeMirrorController.ProcessId is int processId
                    ? $"PID {processId}"
                    : "process attached";

            MirrorStateText.Text =
                _nativeMirrorController.AttachedFromDiscovery
                    ? $"Attached to an existing .NET GDI + Portal preview session ({processText}). Closing this v0.20 window will leave the mirror running; reopening v0.20 can attach again."
                    : $".NET GDI + Portal preview is running ({processText}). Closing this v0.20 window will leave the mirror running.";

            return;
        }

        if (legacyRunning)
        {
            var processText =
                _mirrorController.ProcessId is int processId
                    ? $"PID {processId}"
                    : "process attached";

            MirrorStateText.Text =
                _mirrorController.AttachedFromDiscovery
                    ? $"Attached to an existing v0.14.1 mirror session ({processText}). Closing this v0.20 window will leave the mirror running; reopening v0.20 can attach again."
                    : $"Mirror is running through the v0.14.1 stable engine ({processText}). Closing this v0.20 window will leave the mirror running.";

            return;
        }

        if (ambiguousSessions)
        {
            MirrorStateText.Text =
                $"Ambiguous mirror sessions detected (stable: {_mirrorController.DetectedSessionCount}, .NET: {_nativeMirrorController.DetectedSessionCount}). Start/Stop are disabled until only one session remains.";
            return;
        }

        if (hasPair)
        {
            var selectedEngine =
                (MirrorEngineComboBox.SelectedItem as MirrorEngineChoice)?.Kind ??
                MirrorEngineKind.StableV0141;

            MirrorStateText.Text =
                selectedEngine ==
                MirrorEngineKind.NativeDotNetPreview
                    ? "Ready. Start Mirror will launch the .NET GDI + Portal preview engine. Ctrl+Alt+F12 exits; Ctrl+Alt+F9 cycles render mode."
                    : "Ready. Start Mirror will launch the stable v0.14.1 Portal engine.";

            return;
        }

        MirrorStateText.Text =
            "Select one virtual QHD source and one physical FHD destination.";
    }

    private void SynchronizeEngineSelection(
        bool legacyRunning,
        bool nativeRunning)
    {
        if (legacyRunning == nativeRunning)
        {
            // Both false: preserve the user's selected engine.
            // Both true: ambiguous state; do not pretend one engine owns it.
            return;
        }

        var expectedKind =
            nativeRunning
                ? MirrorEngineKind.NativeDotNetPreview
                : MirrorEngineKind.StableV0141;

        if (MirrorEngineComboBox.SelectedItem is MirrorEngineChoice selected &&
            selected.Kind == expectedKind)
        {
            return;
        }

        var expectedChoice =
            MirrorEngineComboBox.Items
                .OfType<MirrorEngineChoice>()
                .FirstOrDefault(
                    choice =>
                        choice.Kind == expectedKind);

        if (expectedChoice is null)
        {
            return;
        }

        _synchronizingEngineSelection = true;

        try
        {
            MirrorEngineComboBox.SelectedItem =
                expectedChoice;
        }
        finally
        {
            _synchronizingEngineSelection = false;
        }
    }

    private void DetectMirrorSessions()
    {
        _mirrorController.DetectExistingSession();
        _nativeMirrorController.DetectExistingSession();
    }

    private bool HasAnyMirrorSession() =>
        _mirrorController.SessionState !=
            LegacyMirrorSessionState.None ||
        _nativeMirrorController.SessionState !=
            NativeMirrorSessionState.None;

    private static string GetEngineLabel(
        MirrorEngineKind engine) =>
        engine switch
        {
            MirrorEngineKind.NativeDotNetPreview =>
                ".NET GDI + Portal preview",

            _ =>
                "stable v0.14.1 Portal engine"
        };

    private void MirrorController_StateChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.CheckAccess())
        {
            UpdateMirrorControls();
            return;
        }

        Dispatcher.BeginInvoke(new Action(UpdateMirrorControls));
    }

    private enum MirrorEngineKind
    {
        StableV0141,
        NativeDotNetPreview
    }

    private sealed record MirrorEngineChoice(
        MirrorEngineKind Kind,
        string Label);

    private sealed record DisplayChoice(
        DisplayEndpoint Endpoint,
        string Label);
}
