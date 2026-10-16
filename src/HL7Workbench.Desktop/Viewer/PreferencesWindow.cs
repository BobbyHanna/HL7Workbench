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
    public ComboBox SchemaMode { get; } = new() { ItemsSource = new[] { "Use message-declared schema", "Use selected target schema" }, IsEnabled = false, MinWidth = 290 };
    public TextBox TargetSchema { get; } = new() { IsEnabled = false, PlaceholderText = "No schema engine installed" };
    public Button ApplyButton { get; } = new() { Content = "Apply" };
    public Button OkButton { get; } = new() { Content = "OK", IsDefault = true };
    public Button CancelButton { get; } = new() { Content = "Cancel", IsCancel = true };
    public ListBox Categories { get; } = new() { ItemsSource = new[] { "General", "HL7" }, SelectedIndex = 0, Width = 140 };
    public TextBlock Feedback { get; } = new() { TextWrapping = TextWrapping.Wrap };
    private readonly ApplicationPreferences preferences;
    public PreferencesWindow(ApplicationPreferences preferences)
    {
        this.preferences = preferences;
        Title = "Preferences"; Width = 700; Height = 420; MinWidth = 580; MinHeight = 350;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        EditByDefault.IsChecked = preferences.Current.StartInEditMode;
        RibbonVisibility.SelectedIndex = preferences.Current.RibbonExpanded ? 1 : 0;
        SchemaMode.SelectedIndex = (int)preferences.Current.SchemaMode; TargetSchema.Text = preferences.Current.TargetSchema;
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
            Explanation("Declared HL7 version is read from each message's MSH-12. A selected target schema would be an independent inspection choice, including for batches with mixed declarations."),
            SchemaMode, new TextBlock { Text = "Selected target schema (reserved)" }, TargetSchema,
            Explanation("Schema interpretation and target-schema validation are not implemented. These controls are reserved and disabled. Current inspection is structural; no conformance or version-conflict results are produced. Preferences never rewrite MSH-12 or source bytes.")
        } };
        var panel = new ContentControl { Content = general, Margin = new Thickness(20, 0, 0, 0) };
        Categories.SelectionChanged += (_, _) => panel.Content = Categories.SelectedIndex == 1 ? hl7 : general;
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
        // Leave reserved schema metadata and unknown future fields intact.
        var result = preferences.Apply(preferences.Current with { StartInEditMode = EditByDefault.IsChecked == true, RibbonExpanded = RibbonVisibility.SelectedIndex == 1 });
        Feedback.Text = preferences.Status; return result;
    }
    private static TextBlock Heading(string text) => new() { Text = text, FontSize = 18, FontWeight = FontWeight.SemiBold };
    private static TextBlock Explanation(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap };
}
