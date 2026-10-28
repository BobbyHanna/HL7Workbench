using System.ComponentModel;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Transport;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.Viewer;

public sealed class LoadTestingView : Grid
{
    private readonly LoadTestingViewModel model;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly StackPanel form = new() { Spacing = 5 };
    private bool refreshing;
    private int renderedResults = -1;
    public CheckBox Synthetic { get; } = new() { Content = "I confirm this source contains synthetic test data only." };
    public ComboBox Profiles { get; } = new();
    public ComboBox Selection { get; } = new() { ItemsSource = new[] { "All messages", "Contiguous range", "Explicit positions (source order)" }, SelectedIndex = 0 };
    public TextBox First { get; } = Input("1");
    public TextBox Last { get; } = Input("");
    public TextBox Positions { get; } = new() { MaxLength = 8000 };
    public TextBox Concurrency { get; } = Input("1");
    public TextBox Rate { get; } = Input("1");
    public TextBox Duration { get; } = Input("60");
    public TextBox Attempts { get; } = Input("60");
    public TextBox Failures { get; } = Input("3");
    public TextBox Rejections { get; } = Input("20");
    public TextBox Timeouts { get; } = Input("20");
    public TextBox Samples { get; } = Input("5");
    public CheckBox Replay { get; } = new() { Content = "Replay the captured set (identical control IDs and clinical content)" };
    public Button Reload { get; } = Button("Reload saved endpoints");
    public Button Prepare { get; } = Button("Prepare");
    public Button Review { get; } = Button("Review");
    public Button Start { get; } = Button("Start");
    public Button Stop { get; } = Button("Stop / Cancel");
    public Button ExportCsv { get; } = Button("Export CSV…");
    public Button ExportSummary { get; } = Button("Export summary…");
    public TextBlock Source { get; } = Text();
    public TextBlock Destination { get; } = Text();
    public TextBlock Progress { get; } = Text();
    public TextBlock Latency { get; } = Text();
    public TextBlock Status { get; } = Text();
    public ListBox Results { get; } = new() { MinHeight = 60, ItemsPanel = new FuncTemplate<Panel?>(() => new VirtualizingStackPanel()) };
    public Task ActionTask => model.ActionTask;
    public ResponsiveToolSurface LayoutSurface { get; }
    public LoadTestingView(LoadTestingViewModel model)
    {
        this.model = model; refreshing = true; Margin = new(8); RowDefinitions = new("Auto,*,Auto,*,Auto"); RowSpacing = ToolLayoutMetrics.Gap;
        Synthetic.Content = new TextBlock { Text = (string)Synthetic.Content!, TextWrapping = TextWrapping.Wrap };
        Replay.Content = new TextBlock { Text = (string)Replay.Content!, TextWrapping = TextWrapping.Wrap };
        form.Children.Add(new TextBlock { Text = "Controlled load testing · authorized test environments only. Concurrency can reorder registration/results. Limits do not guarantee receiver safety. No retries.", TextWrapping = TextWrapping.Wrap });
        form.Children.Add(Synthetic); Add("Source captured by Prepare", Source); Add("Saved endpoint (configure in Connection Manager)", Profiles); form.Children.Add(Reload); Add("Exact prepared destination", Destination);
        Profiles.ItemTemplate = new FuncDataTemplate<TransportEndpoint>((p, _) => new TextBlock { Text = p?.Name });
        Add("Source selection (maximum 1,000 messages / 32 MiB)", Selection); Add("First / last position (range)", First); form.Children.Add(Last); Add("Comma-separated positions (explicit)", Positions);
        Add("Concurrency (1–8)", Concurrency); Add("Global target messages/second (0.1–20)", Rate); Add("Duration seconds (1–300)", Duration); Add("Attempt limit (1–5000)", Attempts); form.Children.Add(Replay);
        Add("Stop: consecutive transport failures (1–3)", Failures); Add("Stop: application/commit rejection percent (1–20)", Rejections); Add("Stop: timeout percent (1–20)", Timeouts); Add("Minimum completed outcomes for rate thresholds (1–5)", Samples);
        var scroll = new ScrollViewer { Content = form, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; SetRow(scroll, 1); Children.Add(scroll);
        // Keep Stop first in a fixed top toolbar, even when a short/narrow dock
        // cannot show the full form or live statistics without scrolling.
        var actions = new AdaptiveActionBar(Stop, Prepare, Review, Start, ExportCsv, ExportSummary); Children.Add(actions);
        var metrics = new StackPanel { Spacing = 5, Children = { Progress, Latency } }; var metricScroll = new ScrollViewer { Content = metrics, MaxHeight = 210 }; SetRow(metricScroll, 2); Children.Add(metricScroll);
        SetRow(Results, 3); Children.Add(Results); SetRow(Status, 4); Children.Add(Status);
        Results.ItemTemplate = new FuncDataTemplate<LoadExchangeResult>((r, _) => new TextBlock { Text = r?.ToString(), TextWrapping = TextWrapping.Wrap });
        Synthetic.IsCheckedChanged += (_, _) => { if (!refreshing) model.SyntheticConfirmed = Synthetic.IsChecked == true; };
        Profiles.SelectionChanged += (_, _) => { if (!refreshing && model.CanConfigure) Configure(); };
        foreach (var input in new[] { Concurrency, Rate, Duration, Attempts, Failures, Rejections, Timeouts, Samples }) input.TextChanged += (_, _) => { if (!refreshing && model.CanConfigure && !Configure()) model.InvalidateSelection(); };
        Replay.IsCheckedChanged += (_, _) => { if (!refreshing && model.CanConfigure) Configure(); };
        Selection.SelectionChanged += (_, _) => { if (!refreshing) model.InvalidateSelection(); };
        foreach (var input in new[] { First, Last, Positions }) input.TextChanged += (_, _) => { if (!refreshing) model.InvalidateSelection(); };
        Reload.Click += (_, _) => model.ReloadProfiles();
        Prepare.Click += (_, _) =>
        {
            if (!Configure()) return;
            renderedResults = -1; Results.ItemsSource = null;
            if (Selection.SelectedIndex == 0) _ = model.PrepareAsync();
            else if (Selection.SelectedIndex == 1 && int.TryParse(First.Text, out var first) && int.TryParse(Last.Text, out var last)) _ = model.PrepareAsync(first, last);
            else if (Selection.SelectedIndex == 2)
            {
                var parts = (Positions.Text ?? "").Split(',', StringSplitOptions.TrimEntries);
                if (parts.Length > BatchTransmissionOptions.MaximumMessages || parts.Any(p => !int.TryParse(p, NumberStyles.None, CultureInfo.InvariantCulture, out _))) { Status.Text = "Select up to 1,000 unique valid positions."; return; }
                _ = model.PrepareAsync(positions: parts.Select(p => int.Parse(p, CultureInfo.InvariantCulture)).ToArray());
            }
            else Status.Text = "Enter a valid source range.";
        };
        Review.Click += (_, _) => _ = model.ReviewAsync(); Start.Click += (_, _) => _ = model.StartAsync(); Stop.Click += (_, _) => model.Stop();
        ExportCsv.Click += (_, _) => _ = model.ExportAsync(false); ExportSummary.Click += (_, _) => _ = model.ExportAsync(true);
        timer.Tick += (_, _) => model.Refresh();
        AttachedToVisualTree += (_, _) => { model.PropertyChanged += Changed; timer.Start(); Refresh(); };
        DetachedFromVisualTree += (_, _) => { model.PropertyChanged -= Changed; timer.Stop(); Results.ItemsSource = null; Profiles.ItemsSource = null; renderedResults = -1; };
        Concurrency.Text = model.Options.Concurrency.ToString(CultureInfo.InvariantCulture); Rate.Text = model.Options.MessagesPerSecond.ToString(CultureInfo.InvariantCulture);
        Duration.Text = model.Options.DurationSeconds.ToString(CultureInfo.InvariantCulture); Attempts.Text = model.Options.AttemptLimit.ToString(CultureInfo.InvariantCulture);
        Failures.Text = model.Options.ConsecutiveFailureLimit.ToString(CultureInfo.InvariantCulture); Rejections.Text = model.Options.RejectionPercent.ToString(CultureInfo.InvariantCulture);
        Timeouts.Text = model.Options.TimeoutPercent.ToString(CultureInfo.InvariantCulture); Samples.Text = model.Options.MinimumOutcomeSamples.ToString(CultureInfo.InvariantCulture); Replay.IsChecked = model.Options.Replay;
        LayoutSurface = ResponsiveToolSurface.Attach(this, actions, (1, ToolLayoutMetrics.ListHeight), (3, ToolLayoutMetrics.ListHeight));
        refreshing = false; Refresh();
    }
    private bool Configure()
    {
        if (!int.TryParse(Concurrency.Text, out var concurrency) || !double.TryParse(Rate.Text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var rate) || !int.TryParse(Duration.Text, out var duration) ||
            !int.TryParse(Attempts.Text, out var attempts) || !int.TryParse(Failures.Text, out var failures) || !int.TryParse(Rejections.Text, out var rejections) || !int.TryParse(Timeouts.Text, out var timeouts) || !int.TryParse(Samples.Text, out var samples))
        { Status.Text = "Enter numeric limits within the labeled ranges."; return false; }
        try { model.Configure(Profiles.SelectedItem as TransportEndpoint ?? model.Endpoint, new() { Concurrency = concurrency, MessagesPerSecond = rate, DurationSeconds = duration, AttemptLimit = attempts, Replay = Replay.IsChecked == true, ConsecutiveFailureLimit = failures, RejectionPercent = rejections, TimeoutPercent = timeouts, MinimumOutcomeSamples = samples }); return true; }
        catch (Exception error) when (error is ArgumentException or IOException or InvalidOperationException) { Status.Text = "Limits are invalid or configuration is locked. Stop and drain first; no authorization reused."; return false; }
    }
    private void Changed(object? sender, PropertyChangedEventArgs e) => Refresh();
    private void Refresh()
    {
        refreshing = true;
        try
        {
            var p = model.Session.Progress; Source.Text = model.Source; Destination.Text = model.Session.Prepared?.Endpoint.Display ?? "Loopback default; no prepared destination"; Status.Text = model.Status;
            Synthetic.IsChecked = model.SyntheticConfirmed;
            if (!ReferenceEquals(Profiles.ItemsSource, model.Profiles)) { Profiles.ItemsSource = model.Profiles; Profiles.SelectedItem = model.Profiles.FirstOrDefault(e => e.Id == model.Endpoint.Id); }
            Progress.Text = $"{p.State} · stop {p.StopReason} · active {p.Active}/{model.Options.Concurrency} · elapsed {p.Elapsed.TotalSeconds:F1}s · remaining {p.Remaining.TotalSeconds:F1}s\n" +
                $"Target {p.TargetRate:F2}/s · achieved attempts {p.AttemptRate:F2}/s · completed {p.CompletionRate:F2}/s · application accepted {p.AcceptedRate:F2}/s\n" +
                $"Attempts {p.Attempts} · completed {p.Completed} · connections {p.Connected} · complete writes {p.CompleteWrites} · ACK frames {p.AckFrames} · commit accepted {p.CommitAccepted} · commit rejected/error {p.CommitRejectedError}\n" +
                $"Application accepted {p.ApplicationAccepted} · rejected/error {p.ApplicationRejectedError} · missing/mismatched {p.MissingOrMismatched} · timeouts {p.Timeouts} · transport failures {p.TransportFailures} · canceled {p.Canceled} · uncertain {p.Uncertain} (overlapping).\n" +
                $"First {p.Results.Count} completed result rows retained; {p.OmittedResults} omitted. All aggregates/samples remain exact. Individual rows display after Stop/completion.";
            Latency.Text = string.Join('\n', new[] { LoadTestingSession.FormatLatency("Connect", p.ConnectionLatency), LoadTestingSession.FormatLatency("Write", p.WriteLatency), LoadTestingSession.FormatLatency("ACK round-trip", p.AckLatency), LoadTestingSession.FormatLatency("End-to-end", p.ExchangeLatency) });
            form.IsEnabled = model.CanConfigure; Prepare.IsEnabled = model.CanConfigure && model.SyntheticConfirmed;
            Review.IsEnabled = Start.IsEnabled = !model.IsBusy && p.State == LoadTestingState.Prepared;
            Stop.IsEnabled = model.IsBusy || model.Session.IsActive || p.State == LoadTestingState.Prepared;
            ExportCsv.IsEnabled = ExportSummary.IsEnabled = !model.IsBusy && !model.Session.IsActive && model.Session.Prepared is not null && p.State is LoadTestingState.Completed or LoadTestingState.Stopped or LoadTestingState.Canceled or LoadTestingState.Faulted;
            if (!model.Session.IsActive && renderedResults != p.Results.Count) { Results.ItemsSource = p.Results; renderedResults = p.Results.Count; }
        }
        finally { refreshing = false; }
    }
    private void Add(string label, Control control) => form.Children.Add(new ResponsiveFormField(label, control));
    private static TextBox Input(string value) => new() { Text = value, MaxLength = 8 };
    private static Button Button(string text) => new() { Content = text, Margin = new(2) };
    private static TextBlock Text() => new() { TextWrapping = TextWrapping.Wrap };
}
