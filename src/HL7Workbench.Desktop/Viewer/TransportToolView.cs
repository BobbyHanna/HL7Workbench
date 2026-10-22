using System.ComponentModel;
using System.Globalization;
using System.Net;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using HL7Workbench.Core;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Transport;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.Viewer;

/// <summary>Native themed controls over a disposable transport session; detached views retain no payload projection.</summary>
public sealed class TransportToolView : Grid
{
    private readonly TransportToolViewModel model;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private bool refreshing;
    private readonly StackPanel form = new() { Spacing = 5 };
    public TextBox NameField { get; } = Field(80);
    public TextBox Host { get; } = Field(253);
    public TextBox Port { get; } = Field(5);
    public TextBox ConnectTimeout { get; } = Field(3);
    public TextBox AckTimeout { get; } = Field(3);
    public TextBox Notes { get; } = Field(1024);
    public TextBox Position { get; } = Field(6);
    public ComboBox Profiles { get; } = new();
    public ComboBox Security { get; } = new() { ItemsSource = new[] { "Plain TCP (unencrypted)", "TLS (unavailable)" } };
    public ComboBox Encoding { get; } = new() { ItemsSource = new[] { "UTF-8 (strict)", "ASCII (strict)" } };
    public ComboBox AckMode { get; } = new() { ItemsSource = new[] { "No test ACK", "Positive test ACK", "Application error (AE)", "Application reject (AR)", "Commit error (CE)", "Commit reject (CR)" }, SelectedIndex = 1 };
    public CheckBox TestEndpoint { get; } = new() { Content = "Test endpoint (designation only; does not prove safety)" };
    public Button Connect { get; } = Button("Connect");
    public Button Disconnect { get; } = Button("Disconnect / Cancel");
    public Button Send { get; } = Button("Review and send one message…");
    public Button Start { get; } = Button("Start listener");
    public Button Stop { get; } = Button("Stop listener");
    public Button Save { get; } = Button("Save profile");
    public Button New { get; } = Button("New profile");
    public Button Delete { get; } = Button("Delete profile");
    public Button Open { get; } = Button("Open message");
    public Button OpenAck { get; } = Button("Open ACK 1");
    public Button OpenAck2 { get; } = Button("Open ACK 2");
    public Button Clear { get; } = Button("Clear history");
    public ListBox History { get; } = new() { MinHeight = 80 };
    public TextBlock Status { get; } = Text();
    public TextBlock Destination { get; } = Text();
    public TextBlock ActiveMessage { get; } = Text();
    public TextBlock Clients { get; } = Text();
    public Task ActionTask => model.ActionTask;
    public TransportToolView(TransportToolViewModel model)
    {
        this.model = model; Margin = new(8); MinWidth = 280; RowDefinitions = new("Auto,*,Auto,*,Auto"); RowSpacing = 5;
        var warning = Text(); warning.Text = model.IsListener ? "Local test listener. Automatic ACKs are test responses, not evidence of clinical processing. Stop/Start to change options." : "Interactive send: exactly one message. Confirming Send opens one fresh connection to the checked address. Each exchange disconnects. No retries.";
        Children.Add(warning);
        var scroll = new ScrollViewer { Content = form, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }; Grid.SetRow(scroll, 1); Children.Add(scroll);
        Profiles.ItemTemplate = new FuncDataTemplate<TransportEndpoint>((p, _) => new TextBlock { Text = p?.Name });
        if (!model.IsListener)
        {
            Add("Saved endpoint profiles", Profiles); form.Children.Add(Strip(New, Save, Delete)); Add("Friendly name", NameField);
        }
        Add(model.IsListener ? "Bind IP address (default loopback only)" : "Destination hostname / IP", Host); Add("Port", Port);
        if (!model.IsListener) { Add("Security", Security); Add("Connection timeout (seconds, 1–60)", ConnectTimeout); }
        Add(model.IsListener ? "Client idle / frame timeout (seconds, 1–120)" : "Response / ACK timeout (seconds, 1–120)", AckTimeout);
        Add("Message encoding", Encoding);
        if (model.IsListener) Add("Test acknowledgment policy", AckMode);
        else { form.Children.Add(TestEndpoint); Add("Notes (no secrets or patient data)", Notes); }
        var controls = new StackPanel { Spacing = 4 };
        if (model.IsListener) { controls.Children.Add(Strip(Start, Stop)); controls.Children.Add(Clients); }
        else { controls.Children.Add(Destination); controls.Children.Add(Strip(Connect, Disconnect)); controls.Children.Add(ActiveMessage); controls.Children.Add(new TextBlock { Text = "Message position (blank = active message)" }); controls.Children.Add(Position); controls.Children.Add(Send); }
        Grid.SetRow(controls, 2); Children.Add(controls); Grid.SetRow(History, 3); Children.Add(History);
        var bottom = new StackPanel { Spacing = 4, Children = { Strip(Open, OpenAck, OpenAck2, Clear), Status } }; Grid.SetRow(bottom, 4); Children.Add(bottom);
        History.ItemTemplate = new FuncDataTemplate<object>((r, _) => new TextBlock { Text = Row(r), TextWrapping = TextWrapping.Wrap });
        History.SelectionChanged += (_, _) => RefreshButtons();
        Profiles.SelectionChanged += (_, _) => { if (!refreshing && Profiles.SelectedItem is TransportEndpoint p) { model.SelectProfile(p); LoadDraft(); } };
        Connect.Click += (_, _) => { if (ReadDraft()) _ = model.ConnectAsync(); };
        Disconnect.Click += (_, _) => model.Cancel();
        Send.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(Position.Text)) _ = model.SendAsync();
            else if (int.TryParse(Position.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var position) && position > 0) _ = model.SendAsync(position);
            else Status.Text = "Choose one valid message position or leave blank for the active message.";
        };
        Save.Click += (_, _) => { if (ReadDraft()) model.SaveProfile(); };
        Delete.Click += (_, _) => model.SaveProfile(true);
        New.Click += (_, _) => { model.NewProfile(); LoadDraft(); };
        Start.Click += (_, _) =>
        {
            if (!ReadDraft()) return;
            if (!IPAddress.TryParse(Host.Text, out var address)) { Status.Text = "Choose a literal bind IP address."; return; }
            _ = model.StartAsync(new() { BindAddress = address, Port = model.Draft.Port, Encoding = model.Draft.Encoding, IdleTimeoutSeconds = model.Draft.AckTimeoutSeconds, AckMode = (TestAckMode)AckMode.SelectedIndex });
        };
        Stop.Click += (_, _) => _ = model.StopAsync();
        Open.Click += (_, _) => { if (History.SelectedItem is { } item) _ = model.OpenAsync(item); };
        OpenAck.Click += (_, _) => { if (History.SelectedItem is { } item) _ = model.OpenAsync(item, 0); };
        OpenAck2.Click += (_, _) => { if (History.SelectedItem is { } item) _ = model.OpenAsync(item, 1); };
        Clear.Click += (_, _) => model.ClearHistory();
        timer.Tick += (_, _) => model.Refresh();
        AttachedToVisualTree += (_, _) => { model.PropertyChanged += Changed; timer.Start(); Refresh(); };
        DetachedFromVisualTree += (_, _) => { model.PropertyChanged -= Changed; timer.Stop(); History.ItemsSource = null; Profiles.ItemsSource = null; };
        LoadDraft(); Refresh();
    }
    private void Changed(object? sender, PropertyChangedEventArgs e) => Refresh();
    private void LoadDraft()
    {
        var p = model.Draft; NameField.Text = p.Name; Host.Text = p.Host; Port.Text = p.Port.ToString(CultureInfo.InvariantCulture); Notes.Text = p.Notes;
        ConnectTimeout.Text = p.ConnectTimeoutSeconds.ToString(CultureInfo.InvariantCulture); AckTimeout.Text = p.AckTimeoutSeconds.ToString(CultureInfo.InvariantCulture);
        Security.SelectedIndex = (int)p.Security; Encoding.SelectedIndex = (int)p.Encoding; TestEndpoint.IsChecked = p.TestEndpoint;
    }
    private bool ReadDraft()
    {
        if (!int.TryParse(Port.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var port) ||
            !int.TryParse(ConnectTimeout.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var connect) ||
            !int.TryParse(AckTimeout.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var ack)) { Status.Text = "Port and timeouts must be whole numbers."; return false; }
        model.Draft = model.Draft with { Name = NameField.Text ?? "", Host = Host.Text ?? "", Port = port, Notes = Notes.Text ?? "", ConnectTimeoutSeconds = connect, AckTimeoutSeconds = ack,
            Security = (TransportSecurity)Security.SelectedIndex, Encoding = (TransportEncoding)Encoding.SelectedIndex, TestEndpoint = TestEndpoint.IsChecked == true };
        return true;
    }
    private void Refresh()
    {
        refreshing = true;
        try
        {
            var selected = History.SelectedItem; var rows = model.IsListener ? model.Listener.Receipts.Cast<object>().ToArray() : model.Session.History.Cast<object>().ToArray();
            if (History.ItemCount != rows.Length || rows.Length > 0 && !ReferenceEquals(History.Items[0], rows[0])) { History.ItemsSource = rows; History.SelectedItem = selected; }
            if (!ReferenceEquals(Profiles.ItemsSource, model.Profiles)) { Profiles.ItemsSource = model.Profiles; Profiles.SelectedItem = model.Profiles.FirstOrDefault(p => p.Id == model.Draft.Id); }
            Status.Text = model.Status; ActiveMessage.Text = model.ActiveMessage; Destination.Text = model.Session.Destination?.Display ?? "Disconnected · review the configured destination before connecting";
            Clients.Text = model.Clients; form.IsEnabled = model.CanConfigure; Connect.IsEnabled = !model.IsBusy && !model.Session.IsConnected; Send.IsEnabled = !model.IsBusy && model.Session.IsConnected;
            Start.IsEnabled = !model.IsBusy && !model.Listener.IsRunning; Stop.IsEnabled = !model.IsBusy && model.Listener.IsRunning;
            Disconnect.IsEnabled = model.IsBusy || model.Session.IsConnected; RefreshButtons();
        }
        finally { refreshing = false; }
    }
    private void RefreshButtons()
    {
        Open.IsEnabled = History.SelectedItem is TransportHistoryEntry or TransportReceipt { Payload.Length: > 0 };
        OpenAck.IsEnabled = History.SelectedItem is TransportHistoryEntry { Acknowledgments.Count: > 0 };
        OpenAck2.IsEnabled = History.SelectedItem is TransportHistoryEntry { Acknowledgments.Count: > 1 };
    }
    private void Add(string label, Control control) { form.Children.Add(new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap }); form.Children.Add(control); }
    private static TextBox Field(int maximum) => new() { MaxLength = maximum, HorizontalAlignment = HorizontalAlignment.Stretch };
    private static Button Button(string text) => new() { Content = text };
    private static TextBlock Text() => new() { TextWrapping = TextWrapping.Wrap };
    private static Control Strip(params Control[] controls) => new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
        Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, Children = { } }.With(controls) };
    private static string Row(object? value) => value is TransportReceipt r ? $"{r.Time:HH:mm:ss} · Received · {r.Remote} · {r.Status} · test ACK {string.Join(", ", r.AckCodes)} · complete payload {r.Payload.Length:N0} bytes · socket bytes read (connection total): {r.ConnectionSocketBytesRead:N0} · partial payload {r.PartialPayloadBytes:N0} bytes · {r.Duration.TotalSeconds:F2}s" : value?.ToString() ?? "";
}

internal static class TransportPanelChildren
{
    internal static StackPanel With(this StackPanel panel, IEnumerable<Control> controls) { foreach (var control in controls) panel.Children.Add(control); return panel; }
}
