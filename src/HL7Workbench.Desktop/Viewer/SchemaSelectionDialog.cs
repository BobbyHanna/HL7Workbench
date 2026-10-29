using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using HL7Workbench.Core;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.Viewer;

/// <summary>Profile selection/library management only; findings stay inline in the existing editor.</summary>
internal sealed class SchemaSelectionDialog : Window
{
    internal ListBox Profiles { get; } = new() { MinHeight = 100 };
    internal Button ApplySchema { get; } = new() { Content = "Apply Schema", IsDefault = true };
    internal Button ImportProfile { get; } = new() { Content = "Import profile…" };
    internal Button ReimportProfile { get; } = new() { Content = "Reimport selected…" };
    internal Button RemoveProfile { get; } = new() { Content = "Remove imported profile" };
    internal TextBlock Details { get; } = new() { TextWrapping = TextWrapping.Wrap };
    internal TextBlock Status { get; } = new() { TextWrapping = TextWrapping.Wrap };
    private readonly MessageDocument? document;
    private readonly ValidationProfileLibrary library;
    private readonly MessageId? messageId;
    private readonly long revision;
    private readonly CancellationTokenSource lifetime = new();
    private bool importing;
    internal Task Ready { get; }
    internal Func<FilePickerOpenOptions, Task<IReadOnlyList<IStorageFile>>> PickProfile { get; set; }
    internal SchemaSelectionDialog(MessageDocument document) : this(document.ValidationLibrary, document) { }
    internal SchemaSelectionDialog(ValidationProfileLibrary library, MessageDocument? document = null)
    {
        this.library = library; this.document = document; messageId = document?.ActiveMessageId; revision = document?.ContentVersion ?? 0;
        PickProfile = options => StorageProvider.OpenFilePickerAsync(options);
        Title = document is null ? "Manage Schemas" : "Apply Schema"; Width = 720; Height = 610; MinWidth = 420; MinHeight = 420; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var auto = new Button { Content = "Auto-detect built-in from MSH-9 / MSH-12", IsVisible = document is not null };
        ApplySchema.IsVisible = document is not null;
        var cancel = new Button { Content = "Cancel", IsCancel = true }; cancel.Click += (_, _) => Close();
        var buttons = new WrapPanel { Orientation = Orientation.Horizontal, Children = { auto, ImportProfile, ReimportProfile, RemoveProfile, ApplySchema, cancel } };
        var root = new Grid { Margin = new Thickness(16), RowDefinitions = new("Auto,*,Auto,Auto,Auto"), RowSpacing = 10 };
        root.Children.Add(new TextBlock { Text = document is null ? "Manage local profiles. To validate a message, use HL7 Tools → Apply/Change Schema." :
            "Choose a local profile. Applying validates only this message and never changes its bytes.", TextWrapping = TextWrapping.Wrap });
        Grid.SetRow(Profiles, 1); root.Children.Add(Profiles);
        var detailScroll = new ScrollViewer { Content = Details, MaxHeight = 170, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Grid.SetRow(detailScroll, 2); root.Children.Add(detailScroll); Grid.SetRow(Status, 3); root.Children.Add(Status); Grid.SetRow(buttons, 4); root.Children.Add(buttons); Content = root;
        Avalonia.Automation.AutomationProperties.SetName(Profiles, "Available validation profiles");
        Profiles.SelectionChanged += (_, _) => RefreshDetails();
        auto.Click += async (_, _) => await DetectAsync();
        ImportProfile.Click += async (_, _) => await ImportAsync(false); ReimportProfile.Click += async (_, _) => await ImportAsync(true);
        RemoveProfile.Click += (_, _) =>
        {
            if (Profiles.SelectedItem is not ValidationProfileEntry { IsBuiltIn: false } entry) return;
            try { library.Remove(entry.Profile.Id, entry.Revision); RefreshProfiles(); Status.Text = "Profile removed. Applied selections depending on it are unavailable until explicitly resolved."; }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException) { Status.Text = "Profile could not be removed; reload the library or check storage access."; }
        };
        ApplySchema.Click += (_, _) =>
        {
            if (document is null) return;
            if (messageId != document.ActiveMessageId || revision != document.ContentVersion) { Status.Text = "The selected message changed. Close this dialog and apply a schema again."; return; }
            if (Profiles.SelectedItem is ValidationProfileEntry entry) Close(entry.Profile.Id);
        };
        Closed += (_, _) => { lifetime.Cancel(); lifetime.Dispose(); };
        RefreshProfiles(); Ready = InitializeSelectionAsync();
    }
    private void RefreshProfiles(string? id = null)
    {
        id ??= (Profiles.SelectedItem as ValidationProfileEntry)?.Profile.Id;
        Profiles.ItemsSource = library.Entries;
        Profiles.SelectedItem = library.Entries.FirstOrDefault(e => e.Profile.Id == id); RefreshDetails();
        if (library.InvalidEntries > 0) Status.Text = $"{library.InvalidEntries} corrupt/incompatible library entries are retained but unavailable.";
    }
    private void RefreshDetails()
    {
        var entry = Profiles.SelectedItem as ValidationProfileEntry;
        ApplySchema.IsEnabled = document is not null && !importing && entry is not null; ReimportProfile.IsEnabled = !importing && entry is { IsBuiltIn: false }; RemoveProfile.IsEnabled = !importing && entry is { IsBuiltIn: false };
        if (entry is null) { Details.Text = "Select a built-in or imported profile, or explicitly auto-detect a built-in."; return; }
        var p = entry.Profile;
        Details.Text = $"{p.Name} · profile {p.ProfileVersion}\n{p.Hl7Version} · {p.MessageType}^{p.TriggerEvent} · {p.MessageStructure}\nTrading partner: {p.TradingPartner ?? "none"} · Organization: {p.Organization ?? "none"}\nSource: {entry.SourceFile}\n{p.Source}\n";
        try
        {
            var resolved = library.Resolve(p.Id);
            Details.Text += "Inheritance: " + string.Join(" → ", resolved.Lineage) + "\n" + (resolved.Profile.CoverageWarnings.Count == 0 ? "No unsupported-rule metadata declared." : "Incomplete coverage: " + string.Join(" ", resolved.Profile.CoverageWarnings));
            if (resolved.Profile.UnsupportedSegments.Count > 0) Details.Text += "\nDeferred branches: " + string.Join(", ", resolved.Profile.UnsupportedSegments);
        }
        catch (ArgumentException) { Details.Text += "Unavailable: a parent, rule conflict or inheritance cycle prevents resolution."; ApplySchema.IsEnabled = false; }
        if (Details.Text.Length > 4096) Details.Text = Details.Text[..4096] + "…";
    }
    private async Task InitializeSelectionAsync()
    {
        if (document is null) return;
        var selected = document.AppliedSchemaId ?? (document.ValidationPreferences.SchemaMode == SchemaInterpretationMode.SelectedTarget ? document.ValidationPreferences.TargetSchema : null);
        if (selected is not null)
        {
            RefreshProfiles(selected); if (Profiles.SelectedItem is null) Status.Text = "The preferred/applied profile is unavailable. Select a profile explicitly.";
        }
        else await DetectAsync();
    }
    private async Task DetectAsync()
    {
        if (document is null) return;
        var token = lifetime.Token;
        try
        {
            var candidates = await document.DetectSchemasAsync(token);
            token.ThrowIfCancellationRequested();
            if (messageId != document.ActiveMessageId || revision != document.ContentVersion) { Status.Text = "The selected message changed. Reopen Apply Schema."; return; }
            if (candidates.Count == 1) { RefreshProfiles(candidates[0].Id); Status.Text = "Selected a matching built-in. Validation begins only when you choose Apply Schema."; }
            else { Profiles.SelectedItem = null; Status.Text = candidates.Count == 0 ? "No built-in matches MSH-9 and MSH-12. Import/select a compatible profile." : "Multiple built-ins match. Select a profile explicitly."; }
        }
        catch (OperationCanceledException) { }
        catch (Exception e) when (e is InvalidOperationException or IOException or Hl7LimitException or ArgumentException) { Status.Text = "Message declarations are unavailable for auto-detection. Select a compatible profile explicitly."; }
    }
    internal async Task ImportAsync(bool replace = false)
    {
        if (importing || replace && Profiles.SelectedItem is not ValidationProfileEntry { IsBuiltIn: false }) return;
        var token = lifetime.Token;
        var selected = Profiles.SelectedItem as ValidationProfileEntry;
        // Gate before opening the native picker, not after it returns: ribbon and
        // library buttons must never launch competing imports for this dialog.
        importing = true; ImportProfile.IsEnabled = false; RefreshDetails();
        IReadOnlyList<IStorageFile> files = [];
        try
        {
            files = await PickProfile(new() { Title = replace ? "Reimport selected validation profile" : "Import validation profile", AllowMultiple = false,
                FileTypeFilter = [new("HL7 validation profiles") { Patterns = ["*.json", "*.xml"] }] });
            token.ThrowIfCancellationRequested();
            if (files.Count != 1) return;
            await using var input = await files[0].OpenReadAsync(); using var output = new MemoryStream(); var buffer = new byte[16384];
            while (true) { var count = await input.ReadAsync(buffer, token); if (count == 0) break; if (output.Length + count > Hl7ValidationLimits.ProfileBytes) throw new ArgumentException("Profile exceeds 1 MiB input limit."); output.Write(buffer, 0, count); }
            var bytes = output.ToArray(); var xml = files[0].Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase);
            // Parsing is off-context; publication and library notifications remain
            // on the Workspace owning context, just like other local libraries.
            var parsed = await Task.Run(() => xml ? Hl7ProfileImporter.ImportXml(bytes, token) : Hl7ProfileImporter.ImportJson(bytes, token), token);
            token.ThrowIfCancellationRequested();
            if (replace && parsed.Id != selected!.Profile.Id) throw new ArgumentException("Reimport must retain the selected profile identifier.");
            var entry = library.Import(bytes, xml, files[0].Name, replace ? selected!.Revision : null, token);
            RefreshProfiles(entry.Profile.Id); Status.Text = "Profile imported locally. Review its coverage, then explicitly Apply Schema.";
        }
        catch (OperationCanceledException) { }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException) { Status.Text = e.Message.Length <= 512 ? e.Message : "Profile has unsupported or conflicting rules. Check the documented import subset."; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Status.Text = "Profile storage is unavailable; prior definitions are retained."; }
        finally { importing = false; ImportProfile.IsEnabled = true; RefreshDetails(); foreach (var file in files) file.Dispose(); }
    }
}
