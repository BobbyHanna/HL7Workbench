using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace HL7Workbench.Desktop.Viewer;

internal sealed class AboutWindow : Window
{
    internal AboutWindow()
    {
        Title = "About " + ProductBranding.Name;
        Width = 480; SizeToContent = SizeToContent.Height; CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var close = new Button { Content = "Close", IsCancel = true, HorizontalAlignment = HorizontalAlignment.Right };
        close.Click += (_, _) => Close();
        Content = new StackPanel
        {
            Margin = new Thickness(24), Spacing = 14,
            Children =
            {
                new TextBlock { Text = ProductBranding.Name, FontSize = 24, FontWeight = FontWeight.SemiBold },
                new TextBlock { Text = ProductBranding.Description, TextWrapping = TextWrapping.Wrap },
                new TextBlock { Text = "HL7 v2 viewing and editing, search and extraction, scripting, de-identification, synthetic generation, FHIR R4 import and conversion, and TCP/MLLP testing.", TextWrapping = TextWrapping.Wrap },
                close
            }
        };
        Opened += (_, _) => close.Focus();
    }
}
