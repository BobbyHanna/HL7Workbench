using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace HL7Workbench.Desktop.Viewer;

internal sealed class AboutWindow : Window
{
    internal AboutWindow()
    {
        var identity = BuildIdentity.Current;
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
                new TextBlock { Text = "Version " + identity.Version },
                new TextBlock { Text = "Build " + identity.Build, Name = "BuildIdentifier" },
                new TextBlock { Text = ProductBranding.Description, TextWrapping = TextWrapping.Wrap },
                new TextBlock { Text = "HL7 v2 viewing and editing, search and extraction, scripting, de-identification, synthetic generation, FHIR R4 import and conversion, and TCP/MLLP testing.", TextWrapping = TextWrapping.Wrap },
                new Expander { Header = "Data handling and privacy", Content = new ScrollViewer { MaxHeight = 260, Content = new TextBlock
                {
                    Text = "Use data you are authorized to access. Imported FHIR records are not verified as synthetic. FHIR Patient Workspace processes local files and keeps imported records in the current session; closing or clearing it releases that session.\n\n" +
                        "There are no automatic uploads or runtime telemetry. HL7 source snapshots and query journals can use temporary local storage. Explicit saves and exports persist data; saved scripts and SQL definitions can also contain sensitive text.\n\n" +
                        "TCP/MLLP tools transmit only through their explicit workflows. Trusted C# scripts run with your account permissions and can access files or networks. Mapping and de-identification have documented limits; review output before use.",
                    TextWrapping = TextWrapping.Wrap
                } } },
                close
            }
        };
        Opened += (_, _) => close.Focus();
    }
}
