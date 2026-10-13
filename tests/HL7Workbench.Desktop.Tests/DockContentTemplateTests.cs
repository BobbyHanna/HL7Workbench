using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Dock.Model.Mvvm.Controls;
using HL7Workbench.Core;
using HL7Workbench.Desktop.Docking;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed class DockContentTemplateTests
{
    [Fact]
    public void DockTemplateRejectsAndBuildsFallbackForNullUnsetAndUnexpectedContent()
    {
        var template = new DockContentTemplate();

        Assert.False(template.Match(null));
        Assert.False(template.Match(AvaloniaProperty.UnsetValue));
        Assert.False(template.Match(new object()));
        Assert.Equal(string.Empty, Assert.IsType<TextBlock>(template.Build(null)).Text);
        Assert.Equal(string.Empty, Assert.IsType<TextBlock>(template.Build(AvaloniaProperty.UnsetValue)).Text);
        Assert.Equal(string.Empty, Assert.IsType<TextBlock>(template.Build(new object())).Text);
        Assert.Equal(string.Empty, Assert.IsType<TextBlock>(template.Build(new Tool { Id = "tool.empty-context" })).Text);
    }

    [Fact]
    public void AvaloniaItemTemplatesHandleNullAndUnsetDataDuringDocumentAndDiagnosticUpdates()
    {
        var documents = new DocumentManager();
        documents.Open(new("synthetic.one", "Synthetic one", Hl7Parser.Parse("MSH|^~\\&|SYN|||||||SYN-1||2.5\r")));
        documents.Open(new("synthetic.two", "Synthetic two", Hl7Parser.Parse("PID|2")));

        using var browser = new MessageBrowserViewModel(documents);
        using var diagnostics = new DiagnosticsViewModel(documents);
        var template = new DockContentTemplate();

        var browserRoot = template.Build(new Tool { Id = "tool.browser", Context = browser });
        var browserList = Assert.Single(Assert.IsType<Grid>(browserRoot).Children.OfType<ListBox>());
        var documentTemplate = Assert.IsAssignableFrom<IDataTemplate>(browserList.ItemTemplate);

        Assert.False(documentTemplate.Match(null));
        Assert.False(documentTemplate.Match(AvaloniaProperty.UnsetValue));
        Assert.Equal("Synthetic two", Assert.IsType<TextBlock>(documentTemplate.Build(documents.ActiveDocument)).Text);
        Assert.Equal(string.Empty, Assert.IsType<TextBlock>(documentTemplate.Build(null)).Text);
        Assert.Equal(string.Empty, Assert.IsType<TextBlock>(documentTemplate.Build(AvaloniaProperty.UnsetValue)).Text);

        for (var i = 0; i < 50; i++)
        {
            documents.Activate(i % 2 == 0 ? "synthetic.one" : "synthetic.two");
            Assert.Equal(documents.ActiveDocument!.Title,
                Assert.IsType<TextBlock>(documentTemplate.Build(documents.ActiveDocument)).Text);
        }

        documents.Close("synthetic.one");
        documents.Close("synthetic.two");
        Assert.Null(documents.ActiveDocument);
        Assert.Equal(string.Empty, Assert.IsType<TextBlock>(documentTemplate.Build(null)).Text);

        var diagnosticsRoot = template.Build(new Tool { Id = "tool.diagnostics", Context = diagnostics });
        var diagnosticsList = Assert.Single(Assert.IsType<DockPanel>(diagnosticsRoot).Children.OfType<ListBox>());
        var diagnosticTemplate = Assert.IsAssignableFrom<IDataTemplate>(diagnosticsList.ItemTemplate);

        Assert.False(diagnosticTemplate.Match(null));
        Assert.False(diagnosticTemplate.Match(AvaloniaProperty.UnsetValue));
        Assert.Equal(string.Empty, Assert.IsType<TextBlock>(diagnosticTemplate.Build(null)).Text);
        Assert.Equal(string.Empty, Assert.IsType<TextBlock>(diagnosticTemplate.Build(AvaloniaProperty.UnsetValue)).Text);
        Assert.Equal("Error · Synthetic: diagnostic",
            Assert.IsType<TextBlock>(diagnosticTemplate.Build(new Hl7Diagnostic(
                "Synthetic", Hl7DiagnosticSeverity.Error, "diagnostic"))).Text);
    }
}
