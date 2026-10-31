using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.Viewer;

/// <summary>Modal preference draft. Apply persists before notifying the shell; Cancel only abandons unapplied edits.</summary>
public sealed class PreferencesWindow : Window
{
    public CheckBox EditByDefault { get; } = new() { Content = "Open documents in Edit Mode by default" };
    public ComboBox RibbonVisibility { get; } = new() { ItemsSource = new[] { "Collapsed", "Expanded" }, Width = 170 };
    public ComboBox ThemeSelection { get; } = new() { ItemsSource = new[] { "Follow System", "Light", "Dark" }, Width = 170 };
    public NumericUpDown ContentSize { get; } = new() { Minimum = (decimal)ContentFontPreferences.Minimum, Maximum = (decimal)ContentFontPreferences.Maximum, Increment = 1, Width = 150 };
    public Button ResetContentSize { get; } = new() { Content = "Reset Content Size" };
    public ComboBox SchemaMode { get; } = new() { ItemsSource = new[] { "Auto-detect built-in from MSH-9 / MSH-12", "Select a default validation profile" }, MinWidth = 290 };
    // Retain the old preference draft API for callers with an unavailable ID.
    public TextBox TargetSchema { get; } = new() { IsVisible = false };
    public ComboBox DefaultValidationProfile { get; } = new() { MinWidth = 290, MaxDropDownHeight = 280 };
    public Button ApplyButton { get; } = new() { Content = "Apply" };
    public Button OkButton { get; } = new() { Content = "OK", IsDefault = true };
    public Button CancelButton { get; } = new() { Content = "Cancel", IsCancel = true };
    public ListBox Categories { get; } = new() { ItemsSource = new[] { "General", "HL7", "Appearance", "Validation" }, SelectedIndex = 0, Width = 140 };
    public TextBlock Feedback { get; } = new() { TextWrapping = TextWrapping.Wrap };
    private readonly ApplicationPreferences preferences;
    public PreferencesWindow(ApplicationPreferences preferences, ValidationProfileLibrary? library = null)
    {
        this.preferences = preferences;
        Title = "Preferences"; Width = 700; Height = 420; MinWidth = 580; MinHeight = 350;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        EditByDefault.IsChecked = preferences.Current.StartInEditMode;
        RibbonVisibility.SelectedIndex = preferences.Current.RibbonExpanded ? 1 : 0;
        ThemeSelection.SelectedIndex = (int)preferences.Current.Theme;
        ContentSize.Value = (decimal)(preferences.Current.ContentFonts?.DefaultSize ?? ContentFontPreferences.Default);
        Avalonia.Automation.AutomationProperties.SetName(ContentSize, "Default Content Font Size in DIP");
        ResetContentSize.Click += (_, _) => ContentSize.Value = (decimal)ContentFontPreferences.Default;
        Avalonia.Automation.AutomationProperties.SetName(ThemeSelection, "Application theme");
        ToolTip.SetTip(ThemeSelection, "Apply Follow System, Light or Dark without restarting");
        SchemaMode.SelectedIndex = (int)preferences.Current.SchemaMode; TargetSchema.Text = preferences.Current.TargetSchema;
        library ??= new(); DefaultValidationProfile.ItemsSource = library.Entries;
        DefaultValidationProfile.SelectedItem = library.Entries.FirstOrDefault(e => e.Profile.Id == preferences.Current.TargetSchema);
        DefaultValidationProfile.IsEnabled = SchemaMode.SelectedIndex == 1;
        SchemaMode.SelectionChanged += (_, _) => DefaultValidationProfile.IsEnabled = SchemaMode.SelectedIndex == 1;
        Avalonia.Automation.AutomationProperties.SetName(SchemaMode, "Default validation selection mode");
        Avalonia.Automation.AutomationProperties.SetName(DefaultValidationProfile, "Default validation profile");
        var general = new StackPanel { Spacing = 14, Children =
        {
            Heading("General"), EditByDefault,
            Explanation("Applies only to newly opened documents. Existing documents keep their Edit Mode and working edits."),
            new TextBlock { Text = "Ribbon visibility" }, RibbonVisibility,
            Explanation("Collapsed keeps tabs visible and opens commands temporarily. Pinning the ribbon also saves this preference.")
        } };
        var hl7 = new StackPanel { Spacing = 12, Children =
        {
            Heading("HL7 interpretation"),
            Explanation("Declared HL7 version is read from MSH-12. Structural inspection stays tolerant and lossless. Use HL7 Tools → Schema Validation → Apply/Change Schema explicitly; preferences never rewrite declarations or source bytes.")
        } };
        var panel = new ContentControl { Content = general, Margin = new Thickness(20, 0, 0, 0) };
        var appearance = new StackPanel { Spacing = 14, Children = { Heading("Appearance"), new TextBlock { Text = "Application theme" }, ThemeSelection,
            Explanation("Follow System tracks the operating system's light/dark preference where supported. Apply changes all windows immediately; no restart is required."),
            new TextBlock { Text = "Default Content Font Size (DIP; 12–32 ≈ 9–24 pt)" }, ContentSize, ResetContentSize,
            Explanation("Apply updates open content tools immediately. Typography scales from the existing 14 DIP default; code and headings retain their relative sizes. Ctrl+Plus/Minus changes only the focused tool; Ctrl+0 removes its saved override. Ribbon/menu sizing is unchanged.") } };
        var validation = new StackPanel { Spacing = 12, Children = { Heading("Validation"), SchemaMode, DefaultValidationProfile,
            Explanation("This preference selects the initial choice in Apply Schema. It never validates newly opened messages automatically. Auto-detect considers only matching built-ins; trading-partner profiles require explicit selection. Use HL7 Tools → Schema Validation → Manage Schemas for the existing import/reimport/remove workflow."),
            Explanation("Applied selections and diagnostics are session-only per message. Unsupported checks display Incomplete; no conformance certification is implied.") } };
        Categories.SelectionChanged += (_, _) => panel.Content = Categories.SelectedIndex switch { 1 => hl7, 2 => appearance, 3 => validation, _ => general };
        var scroll = new ScrollViewer { Content = panel, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
        var body = new Grid { ColumnDefinitions = new("Auto,*") }; body.Children.Add(Categories); Grid.SetColumn(scroll, 1); body.Children.Add(scroll);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8,
            Children = { OkButton, CancelButton, ApplyButton } };
        var root = new Grid { Margin = new Thickness(16), RowDefinitions = new("*,Auto,Auto"), RowSpacing = 12 };
        root.Children.Add(body); Grid.SetRow(Feedback, 1); root.Children.Add(Feedback); Grid.SetRow(buttons, 2); root.Children.Add(buttons); Content = root;
        ApplyButton.Click += (_, _) => Apply(); OkButton.Click += (_, _) => { if (Apply()) Close(); }; CancelButton.Click += (_, _) => Close();
        Feedback.Text = preferences.Status;
    }
    public bool Apply()
    {
        // Unknown future fields and an unavailable profile ID remain intact.
        var result = preferences.Apply(preferences.Current with { StartInEditMode = EditByDefault.IsChecked == true, RibbonExpanded = RibbonVisibility.SelectedIndex == 1,
            Theme = (ApplicationThemePreference)Math.Max(0, ThemeSelection.SelectedIndex), SchemaMode = (SchemaInterpretationMode)Math.Max(0, SchemaMode.SelectedIndex),
            TargetSchema = (DefaultValidationProfile.SelectedItem as ValidationProfileEntry)?.Profile.Id ?? TargetSchema.Text,
            ContentFonts = (preferences.Current.ContentFonts ?? new()) with { DefaultSize = (double)(ContentSize.Value ?? (decimal)ContentFontPreferences.Default) } });
        if (result) ApplicationTheme.Apply(preferences.Current.Theme);
        Feedback.Text = preferences.Status; return result;
    }
    private static TextBlock Heading(string text) => new() { Text = text, FontSize = 18, FontWeight = FontWeight.SemiBold };
    private static TextBlock Explanation(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap };
}
