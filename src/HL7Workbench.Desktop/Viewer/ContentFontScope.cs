using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.Viewer;

/// <summary>View-local resources over shared feature preferences. No transforms or document recreation.</summary>
internal sealed class ContentFontScope
{
    private readonly Control content;
    private readonly ApplicationPreferences preferences;
    private readonly string key;
    private double appliedSize = double.NaN;
    internal double Size => (preferences.Current.ContentFonts ?? new()).For(key);
    internal ContentFontScope(Control content, ApplicationPreferences preferences, string key)
    {
        this.content = content; this.preferences = preferences; this.key = key;
        content.AttachedToVisualTree += (_, _) => { preferences.PropertyChanged += Changed; Refresh(); };
        content.DetachedFromVisualTree += (_, _) => preferences.PropertyChanged -= Changed;
        content.AddHandler(InputElement.KeyDownEvent, KeyDown, RoutingStrategies.Tunnel);
        Refresh();
    }
    private void Changed(object? sender, PropertyChangedEventArgs e) => Refresh();
    private void Refresh()
    {
        if (appliedSize == Size) return;
        appliedSize = Size;
        var scale = Size / ContentFontPreferences.Default;
        // Retain authored typography at the default; headings/code keep their relative sizes.
        DefineResources(content.Resources, scale);
        content.SetValue(TextElement.FontSizeProperty, Size);
        ToolTip.SetTip(content, $"Content size: {Size:0.#} DIP · Ctrl+Plus/Minus zooms this tool; Ctrl+0 uses the global default.");
    }
    internal static void DefineResources(IResourceDictionary resources, double scale)
    {
        foreach (var original in new[] { 10d, 11, 12, 13, 14, 16, 18, 19, 21, 22, 23, 24 })
            resources["ContentFont." + original.ToString(System.Globalization.CultureInfo.InvariantCulture)] = original * scale;
    }
    private void KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers is not (KeyModifiers.Control or (KeyModifiers.Control | KeyModifiers.Shift))) return;
        double? size;
        if (e.Key is Key.OemPlus or Key.Add) size = Size + 1;
        else if (e.Key is Key.OemMinus or Key.Subtract) size = Size - 1;
        else if (e.KeyModifiers == KeyModifiers.Control && e.Key is Key.D0 or Key.NumPad0) size = null;
        else return;
        e.Handled = true;
        if (!preferences.SetContentFont(key, size)) ToolTip.SetTip(content, preferences.Status);
    }
}
