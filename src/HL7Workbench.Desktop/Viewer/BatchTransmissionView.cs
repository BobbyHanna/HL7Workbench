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

public sealed class BatchTransmissionView : Grid
{
    private readonly BatchTransmissionViewModel model;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private bool refreshing;
    private readonly StackPanel form = new() { Spacing = 5 };
    private IReadOnlyList<BatchMessageResult> previous = [];
    public ComboBox Profiles { get; } = new();
    public ComboBox Selection { get; } = new() { ItemsSource = new[] { "All messages", "Contiguous range", "Explicit positions (source order)" }, SelectedIndex = 0 };
    public TextBox First { get; } = new() { Text = "1", MaxLength = 7 };
    public TextBox Last { get; } = new() { MaxLength = 7 };
    public TextBox Positions { get; } = new() { MaxLength = 8000 };
    public TextBox Interval { get; } = new() { Text = "1000", MaxLength = 5 };
    public ComboBox Policy { get; } = new() { ItemsSource = new[] { "Stop on unsuccessful outcome", "Continue after unsuccessful outcome (explicit review)" }, SelectedIndex = 0 };
    public Button Reload { get; } = B("Reload saved endpoints");
    public Button Prepare { get; } = B("Prepare");
    public Button Review { get; } = B("Review batch");
    public Button Start { get; } = B("Start");
    public Button Pause { get; } = B("Pause");
    public Button Resume { get; } = B("Resume");
    public Button Cancel { get; } = B("Cancel");
    public Button Export { get; } = B("Export report…");
    public Button OpenAck { get; } = B("Open ACK 1");
    public Button OpenMessage { get; } = B("Open captured message");
    public Button OpenAck2 { get; } = B("Open ACK 2");
    public ListBox Results { get; } = new() { MinHeight = 80 };
    public TextBlock Source { get; } = Text();
    public TextBlock Destination { get; } = Text();
    public TextBlock Progress { get; } = Text();
    public TextBlock Status { get; } = Text();
    public Task ActionTask => model.ActionTask;
    public BatchTransmissionView(BatchTransmissionViewModel model)
    {
        this.model = model; Margin = new(8); MinWidth = 280; RowDefinitions = new("Auto,*,Auto,Auto,*,Auto"); RowSpacing = 6;
        Children.Add(new TextBlock { Text = "Controlled sequential batch · one message per connection · no retries. Pacing does not guarantee receiver safety.", TextWrapping = TextWrapping.Wrap });
        Profiles.ItemTemplate = new FuncDataTemplate<TransportEndpoint>((p, _) => new TextBlock { Text = p?.Name });
        Add("Source (active document captured by Prepare)", Source); Add("Saved endpoint (configure in Connection Manager)", Profiles); Add("Captured destination", Destination); form.Children.Add(Reload);
        Add("Message selection", Selection); Add("First position / last position (range only)", First); form.Children.Add(Last);
        Add("Comma-separated positions (explicit selection only)", Positions); Add("Interval after each exchange (250–60000 ms)", Interval); Add("ACK policy", Policy);
        var mode = Text(); mode.Text = "Mode: sequential, one fresh connection per message. Only correlated AA establishes application acceptance."; form.Children.Add(mode);
        var scroll = new ScrollViewer { Content = form, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; SetRow(scroll, 1); Children.Add(scroll);
        var controls = new AdaptiveActionBar(Cancel, Prepare, Review, Start, Pause, Resume, Export, OpenMessage, OpenAck, OpenAck2); SetRow(controls, 2); Children.Add(controls);
        SetRow(Progress, 3); Children.Add(Progress); SetRow(Results, 4); Children.Add(Results); SetRow(Status, 5); Children.Add(Status);
        ResponsiveToolSurface.Attach(this, controls, (1, ToolLayoutMetrics.ListHeight), (4, ToolLayoutMetrics.ListHeight));
        Results.ItemTemplate = new FuncDataTemplate<BatchMessageResult>((r, _) => new TextBlock { Text = r?.ToString(), TextWrapping = TextWrapping.Wrap });
        Profiles.SelectionChanged += (_, _) => { if (!refreshing && Profiles.SelectedItem is TransportEndpoint p && model.CanConfigure) ApplyConfiguration(p); };
        Interval.TextChanged += (_, _) => ConfigurationChanged(); Policy.SelectionChanged += (_, _) => ConfigurationChanged();
        Selection.SelectionChanged += (_, _) => { if (!refreshing) model.InvalidateSelection(); };
        First.TextChanged += (_, _) => { if (!refreshing) model.InvalidateSelection(); };
        Last.TextChanged += (_, _) => { if (!refreshing) model.InvalidateSelection(); };
        Positions.TextChanged += (_, _) => { if (!refreshing) model.InvalidateSelection(); };
        Reload.Click += (_, _) => { model.ReloadProfiles(); Refresh(); };
        Prepare.Click += (_, _) =>
        {
            if (!ApplyConfiguration(Profiles.SelectedItem as TransportEndpoint ?? model.Endpoint)) return;
            if (Selection.SelectedIndex == 0) _ = model.PrepareAsync();
            else if (Selection.SelectedIndex == 1 && int.TryParse(First.Text, out var first) && int.TryParse(Last.Text, out var last)) _ = model.PrepareAsync(first, last);
            else if (Selection.SelectedIndex == 2)
            {
                var parts = (Positions.Text ?? "").Split(',', StringSplitOptions.TrimEntries);
                if (parts.Length > BatchTransmissionOptions.MaximumMessages || parts.Any(p => !int.TryParse(p, NumberStyles.None, CultureInfo.InvariantCulture, out _))) { Status.Text = "Choose 1–1000 unique valid message positions."; return; }
                _ = model.PrepareAsync(positions: parts.Select(p => int.Parse(p, CultureInfo.InvariantCulture)).ToArray());
            }
            else Status.Text = "Choose a valid contiguous message range.";
        };
        Review.Click += (_, _) => _ = model.ReviewAsync(); Start.Click += (_, _) => _ = model.StartAsync();
        Pause.Click += (_, _) => model.Pause(); Resume.Click += (_, _) => model.Resume(); Cancel.Click += (_, _) => model.Cancel();
        Export.Click += (_, _) => _ = model.ExportAsync();
        OpenAck.Click += (_, _) => { if (Results.SelectedItem is BatchMessageResult row) _ = model.OpenAckAsync(row, 0); };
        OpenMessage.Click += (_, _) => { if (Results.SelectedItem is BatchMessageResult row) _ = model.OpenMessageAsync(row); };
        OpenAck2.Click += (_, _) => { if (Results.SelectedItem is BatchMessageResult row) _ = model.OpenAckAsync(row, 1); };
        Results.SelectionChanged += (_, _) => Buttons(); timer.Tick += (_, _) => model.Refresh();
        AttachedToVisualTree += (_, _) => { model.PropertyChanged += Changed; timer.Start(); Refresh(); };
        DetachedFromVisualTree += (_, _) => { model.PropertyChanged -= Changed; timer.Stop(); Results.ItemsSource = null; Profiles.ItemsSource = null; previous = []; };
        Interval.Text = model.Options.IntervalMilliseconds.ToString(CultureInfo.InvariantCulture); Policy.SelectedIndex = (int)model.Options.Policy; Refresh();
    }
    private void ConfigurationChanged()
    {
        if (!refreshing && model.CanConfigure && !ApplyConfiguration(Profiles.SelectedItem as TransportEndpoint ?? model.Endpoint))
            model.InvalidateSelection();
    }
    private bool ApplyConfiguration(TransportEndpoint endpoint)
    {
        if (!int.TryParse(Interval.Text, out var interval) || interval is < 250 or > 60000 || Policy.SelectedIndex < 0)
        { Status.Text = "Interval must be 250–60000 ms; default 1000 ms."; return false; }
        try { model.Configure(endpoint, new() { IntervalMilliseconds = interval, Policy = (BatchAckPolicy)Policy.SelectedIndex }); return true; }
        catch (Exception e) when (e is IOException or ArgumentException or InvalidOperationException) { Status.Text = "Configuration unavailable; cancel and drain an active run first."; return false; }
    }
    private void Changed(object? sender, PropertyChangedEventArgs e) => Refresh();
    private void Refresh()
    {
        refreshing = true;
        try
        {
            var p = model.Session.Progress; Source.Text = model.Source; Status.Text = model.Status; Destination.Text = model.Session.Prepared?.Endpoint.Display ?? "No prepared destination";
            var completed = p.Results.Count(r => r.Attempted);
            var transportFailures = p.Results.Count(r => r.Classification == BatchMessageClassification.TransportFailure);
            Progress.Text = $"{p.State} · total {p.Total} · attempted {p.Attempted} · application accepted {p.Accepted} · rejected/error {p.RejectedError}" +
                $" · transport failures {transportFailures} · timed out {p.TimedOut} · uncertain {p.Uncertain} · canceled {p.Canceled} · not attempted/skipped {p.NotAttempted}" +
                $"\nCurrent message {p.CurrentPosition?.ToString() ?? "none"} · completed exchanges {completed} · unsuccessful completed exchanges {completed - p.Accepted} · elapsed {p.Elapsed.TotalSeconds:F1}s · average completed exchange {p.AverageExchange.TotalSeconds:F2}s. Uncertain overlaps timeout/cancellation/unknown outcomes.";
            if (!ReferenceEquals(Profiles.ItemsSource, model.Profiles)) { Profiles.ItemsSource = model.Profiles; Profiles.SelectedItem = model.Profiles.FirstOrDefault(e => e.Id == model.Endpoint.Id); }
            if (!p.Results.SequenceEqual(previous)) { var selected = (Results.SelectedItem as BatchMessageResult)?.Position; previous = p.Results; Results.ItemsSource = previous; Results.SelectedItem = previous.FirstOrDefault(r => r.Position == selected); }
            form.IsEnabled = model.CanConfigure; Prepare.IsEnabled = model.CanConfigure;
            Review.IsEnabled = Start.IsEnabled = !model.IsBusy && p.State == BatchTransmissionState.Prepared;
            Pause.IsEnabled = p.State == BatchTransmissionState.Running; Resume.IsEnabled = p.State is BatchTransmissionState.Paused or BatchTransmissionState.PausePending;
            Cancel.IsEnabled = model.IsBusy || model.Session.IsActive || p.State == BatchTransmissionState.Prepared;
            Export.IsEnabled = !model.IsBusy && model.Session.Prepared is not null && p.State is BatchTransmissionState.Completed or BatchTransmissionState.Stopped or BatchTransmissionState.Canceled or BatchTransmissionState.Faulted;
            Buttons();
        }
        finally { refreshing = false; }
    }
    private void Buttons()
    {
        OpenMessage.IsEnabled = !model.IsBusy && Results.SelectedItem is BatchMessageResult;
        OpenAck.IsEnabled = !model.IsBusy && Results.SelectedItem is BatchMessageResult { RetainedAckFrames: > 0 };
        OpenAck2.IsEnabled = !model.IsBusy && Results.SelectedItem is BatchMessageResult { RetainedAckFrames: > 1 };
    }
    private void Add(string label, Control control) => form.Children.Add(new ResponsiveFormField(label, control));
    private static Button B(string text) => new() { Content = text, Margin = new(2) };
    private static TextBlock Text() => new() { TextWrapping = TextWrapping.Wrap };
}
