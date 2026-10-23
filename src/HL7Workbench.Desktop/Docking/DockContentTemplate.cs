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
    public bool DockedInspection { get; set; }
    public bool Match(object? data) => data is IDocument or ITool;
    public Control Build(object? data)
    {
        if (data is not IDockable dockable) return EmptyContent();
        // Optional tools replace Context on close/reopen. Dock may cache this template
        // for the unchanged dockable, so a one-time Context dispatch can stay blank.
        if (dockable is ITool && (dockable.Id == WorkspaceDockFactory.FullRawId ||
            dockable.Factory is WorkspaceDockFactory factory && factory.HasReplaceableContent(dockable.Id!)))
            return new DockToolContentHost(dockable, context => BuildContext(context, dockable));
        return BuildContext(dockable.Context, dockable);
    }
    private Control BuildContext(object? context, IDockable dockable)
    {
        if (context is MessageDocument document)
            return Viewer.MessageViewer.Create(document, DockedInspection && dockable.Factory is WorkspaceDockFactory { HasInspectionTools: true });
        if (context is FullRawInspectorViewModel raw) return new Viewer.FullRawInspectorHost(raw);
        if (context is ActivityLogViewModel activity) return new Viewer.ActivityLogView(activity);
        if (context is ScriptEditorViewModel script) return new Viewer.ScriptEditorView(script);
        if (context is ScriptApiReferenceViewModel reference) return new Viewer.ScriptApiReferenceView(reference);
        if (context is FieldExtractionViewModel extraction) return new Viewer.FieldExtractionView(extraction);
        if (context is SyntheticPatientsViewModel patients) return new Viewer.SyntheticPatientsView(patients);
        if (context is GenerationReportViewModel report) return new Viewer.GenerationReportView(report);
        if (context is DeidentificationReportViewModel deidentification) return new Viewer.TransformationReportView(deidentification);
        if (context is TransportToolViewModel transport) return new Viewer.TransportToolView(transport);
        if (context is BatchTransmissionViewModel batch) return new Viewer.BatchTransmissionView(batch);
        if (context is LoadTestingViewModel load) return new Viewer.LoadTestingView(load);
        if (context is SearchViewModel search) return new Viewer.SearchResultsView(search);
        if (context is ComparisonViewModel comparison) return new Viewer.ComparisonView(comparison);
        if (context is InspectionToolViewModel inspection) return new Viewer.InspectionToolHost(inspection);
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

    private static Control CreateDocumentItem(MessageDocument? document)
    {
        if (document is null) return new TextBlock { Text = string.Empty };
        var label = new TextBlock { DataContext = document };
        label.Bind(TextBlock.TextProperty, new Binding(nameof(MessageDocument.DisplayTitle)));
        return label;
    }

    private static Control CreateDiagnosticItem(HL7Workbench.Core.Hl7Diagnostic? diagnostic) =>
        new TextBlock
        {
            Text = diagnostic is null ? string.Empty : $"{diagnostic.Severity} · {diagnostic.Code}: {diagnostic.Description}",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        };

    private static Control EmptyContent() => new TextBlock { Text = string.Empty };
}
