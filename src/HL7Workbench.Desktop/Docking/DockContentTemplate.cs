using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Dock.Model.Core;
using Dock.Model.Controls;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.Docking;

public sealed class DockContentTemplate : IDataTemplate
{
    public bool Match(object? data) => data is IDocument or ITool;
    public Control Build(object? data)
    {
        if (data is not IDockable dockable) return EmptyContent();

        var context = dockable.Context;
        if (context is MessageDocument document)
            return Viewer.MessageViewer.Create(document);
        if (context is MessageBrowserViewModel browser)
        {
            var list = new ListBox { ItemsSource = browser.Documents, DataContext = browser,
                ItemTemplate = new FuncDataTemplate(
                    data => data is MessageDocument,
                    (data, _) => CreateDocumentItem(data as MessageDocument)) };
            list.Bind(ListBox.SelectedItemProperty, new Binding(nameof(browser.SelectedDocument)) { Mode = BindingMode.TwoWay });
            return new Grid { Margin = new(8), Children = { list } };
        }
        if (context is DiagnosticsViewModel diagnostics)
        {
            var title = new TextBlock { FontWeight = Avalonia.Media.FontWeight.SemiBold, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
            title.Bind(TextBlock.TextProperty, new Binding(nameof(diagnostics.ActiveTitle)));
            var list = new ListBox { ItemTemplate = new FuncDataTemplate(
                data => data is HL7Workbench.Core.Hl7Diagnostic,
                (data, _) => CreateDiagnosticItem(data as HL7Workbench.Core.Hl7Diagnostic)) };
            list.Bind(ItemsControl.ItemsSourceProperty, new Binding(nameof(diagnostics.Diagnostics)));
            var panel = new DockPanel { Margin = new(8), DataContext = diagnostics, LastChildFill = true };
            DockPanel.SetDock(title, Avalonia.Controls.Dock.Top);
            panel.Children.Add(title);
            panel.Children.Add(list);
            return panel;
        }
        return EmptyContent();
    }

    private static Control CreateDocumentItem(MessageDocument? document) =>
        new TextBlock { Text = document?.Title ?? string.Empty };

    private static Control CreateDiagnosticItem(HL7Workbench.Core.Hl7Diagnostic? diagnostic) =>
        new TextBlock
        {
            Text = diagnostic is null ? string.Empty : $"{diagnostic.Severity} · {diagnostic.Code}: {diagnostic.Description}",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        };

    private static Control EmptyContent() => new TextBlock { Text = string.Empty };
}
