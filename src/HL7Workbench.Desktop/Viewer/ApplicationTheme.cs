using Avalonia;
using Avalonia.Styling;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.Viewer;

/// <summary>Uses Avalonia's application-wide theme inheritance, including native platform change notifications.</summary>
public static class ApplicationTheme
{
    public static void InitializeResources(Application application)
    {
        application.Resources.MergedDictionaries.Add(new WorkbenchThemeResources());
        application.Styles.Add(new Avalonia.Markup.Xaml.Styling.StyleInclude(new Uri("avares://HL7Workbench.Desktop/"))
            { Source = new Uri("avares://HL7Workbench.Desktop/Viewer/WorkbenchStyles.axaml") });
    }
    public static void Apply(ApplicationThemePreference preference)
    {
        if (Application.Current is { } application) application.RequestedThemeVariant = preference switch
        {
            ApplicationThemePreference.Light => ThemeVariant.Light,
            ApplicationThemePreference.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
    }
}
