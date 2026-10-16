using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;

namespace HL7Workbench.Desktop.Viewer;

/// <summary>Small semantic resource layer over Fluent, shared by docked/floating surfaces.
/// Custom profile storage/editing is deliberately outside this resource-resolution boundary.</summary>
public sealed class WorkbenchThemeResources : ResourceDictionary
{
    public const string Panel = "WorkbenchPanelBackground";
    public const string EditorBackground = "WorkbenchEditorBackground";
    public const string Surface = "WorkbenchSurfaceBackground";
    public const string Group = "WorkbenchGroupBackground";
    public const string Border = "WorkbenchBorder";
    public const string Accent = "WorkbenchAccent";
    public const string Validation = "WorkbenchValidationForeground";
    private static IBrush Brush(string value) => new ImmutableSolidColorBrush(Color.Parse(value));
    private static object Fluent(string key, string fallback) => Application.Current?.TryGetResource(key, ThemeVariant.Dark, out var resource) == true && resource is IBrush ? resource : Brush(fallback);
    public WorkbenchThemeResources()
    {
        ThemeDictionaries[ThemeVariant.Light] = new ResourceDictionary
        {
            [Hl7EditorTheme.PaletteResourceKey] = Hl7EditorPalette.Light,
            [EditorBackground] = Hl7EditorPalette.Light.EditorBackground,
            [Panel] = Brush("#F4F6FA"), [Surface] = Brush("#FFFFFF"), [Group] = Brush("#F1F5FA"),
            [Border] = Brush("#B9C5D4"), [Accent] = Brush("#005A9E"), [Validation] = Brush("#9E1B32")
        };
        ThemeDictionaries[ThemeVariant.Dark] = new ResourceDictionary
        {
            [Hl7EditorTheme.PaletteResourceKey] = Hl7EditorPalette.Dark,
            [EditorBackground] = Hl7EditorPalette.Dark.EditorBackground,
            [Panel] = Fluent("SystemControlPageBackgroundChromeLowBrush", "#202020"),
            [Surface] = Brushes.Transparent, [Group] = Brushes.Transparent,
            [Border] = Fluent("SystemControlForegroundBaseLowBrush", "#484848"),
            [Accent] = Fluent("SystemControlHighlightAccentBrush", "#60CDFF"), [Validation] = Brush("#FFB4AB")
        };
    }
}
