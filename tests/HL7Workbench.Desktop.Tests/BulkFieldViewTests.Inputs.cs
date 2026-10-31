using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using HL7Workbench.Core;
using Avalonia.VisualTree;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed partial class BulkFieldViewTests
{
    private static Rect InWindow(Control control, Window window) => new(control.TranslatePoint(default, window)!.Value, control.Bounds.Size);
    private static void AssertFullyVisible(Control control, Window window)
    {
        Assert.True(control.IsVisible && control.IsAttachedToVisualTree());
        var rect = InWindow(control, window); Assert.True(rect.Width > 0 && rect.Height > 0);
        Assert.True(new Rect(window.ClientSize).Inflate(.5).Contains(rect), $"{control.GetType().Name} {rect} is outside the client viewport {window.ClientSize}");
        foreach (var clip in control.GetVisualAncestors().OfType<Control>().Where(c => c.ClipToBounds || c is ScrollViewer))
            Assert.True(InWindow(clip, window).Inflate(.5).Contains(rect), $"{control.GetType().Name} {rect} is clipped by {clip.GetType().Name} {InWindow(clip, window)}");
    }
    [Theory]
    [InlineData(false, 14, 980, 780)] [InlineData(true, 14, 980, 780)]
    [InlineData(false, 24, 980, 780)] [InlineData(true, 24, 980, 780)]
    [InlineData(false, 32, 580, 560)] [InlineData(true, 32, 580, 560)]
    [InlineData(false, 32, 1400, 950)] [InlineData(true, 32, 1400, 950)]
    public Task OperationValueInputsAreVisibleWithoutScrollingAndPrecedeAnalyze(bool assign, double font, double width, double height) => InShell(async (window, _, _, panes) =>
    {
        Assert.True(Get<ApplicationPreferences>(window, "preferences").SetContentFont("tool.field-inspector", font));
        var dialog = Open(panes, assign ? panes.AssignValue : panes.SearchReplace); dialog.Width = width; dialog.Height = height; Pump(dialog);
        if (!assign) AssertFullyVisible(dialog.Find, dialog);
        AssertFullyVisible(dialog.Replacement, dialog);
        Assert.Equal(assign ? "New value:" : "Replace with:", Assert.IsType<TextBlock>(AutomationProperties.GetLabeledBy(dialog.Replacement)).Text);
        AssertFullyVisible(AutomationProperties.GetLabeledBy(dialog.Replacement)!, dialog);
        if (!assign) Assert.Equal("Find value:", Assert.IsType<TextBlock>(AutomationProperties.GetLabeledBy(dialog.Find)).Text);
        if (!assign) AssertFullyVisible(AutomationProperties.GetLabeledBy(dialog.Find)!, dialog);
        Assert.True((assign ? dialog.Replacement : dialog.Find).IsFocused);
        var input = assign ? dialog.Replacement : dialog.Find;
        Assert.True(InWindow(input, dialog).Bottom <= InWindow(dialog.Analyze, dialog).Top);
        Assert.True(InWindow(dialog.Replacement, dialog).Bottom <= InWindow(dialog.Analyze, dialog).Top);
        dialog.Close(); await window.InspectorActionTask;
    });
    private static void Enter(BulkFieldWindow dialog, TextBox input, string value)
    {
        input.BringIntoView(); Pump(dialog); Assert.True(input.Focus()); input.SelectAll();
        if (value.Length == 0) dialog.KeyPress(Avalonia.Input.Key.Back, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.Backspace, null);
        else dialog.KeyTextInput(value);
        Assert.Equal(value, input.Text ?? "");
    }
    [Theory]
    [InlineData(false, "E")] [InlineData(true, "E")]
    [InlineData(false, "")] [InlineData(true, "")]
    public Task EnteredValuesIncludingEmptyReplacementArePreviewedAndOnlyPublishedAfterConfirmation(bool assign, string replacement) => InShell(async (window, manager, source, panes) =>
    {
        var original = source.Snapshot!.ReadBytes(new(0, source.Snapshot.Length));
        var dialog = Open(panes, assign ? panes.AssignValue : panes.SearchReplace); Pump(dialog);
        Assert.Empty(dialog.Replacement.Text!);
        if (!assign) { Assert.Equal("I", dialog.Find.Text); Enter(dialog, dialog.Find, "O"); }
        Enter(dialog, dialog.Replacement, replacement); Click(dialog.Analyze); await dialog.Completion;
        var preview = dialog.Result!; Assert.Equal(replacement, preview.Request.Replacement);
        if (!assign) Assert.Equal("O", preview.Request.Find);
        Assert.Equal(assign ? 3 : 1, preview.Summary.Replacements);
        Assert.All(preview.ChangePage(0).Where(c => c.Status == "Proposed"), c => Assert.Equal(replacement, c.Replacement));
        Assert.Single(manager.Documents); Assert.False(source.IsDirty); Assert.False(source.CanUndo); Assert.Equal(original, source.Snapshot.ReadBytes(new(0, source.Snapshot.Length)));
        Assert.False(dialog.Apply.IsEnabled); Click(dialog.Apply); await dialog.Completion; Assert.Single(manager.Documents);
        dialog.Confirm.IsChecked = true; Click(dialog.Apply); await dialog.Completion; await window.InspectorActionTask;
        var output = manager.ActiveDocument!; Assert.NotSame(source, output); Assert.Equal(3, output.MessageCount);
        for (var n = 1; n <= 3; n++)
        {
            await output.NavigateAsync(n, TestContext.Current.CancellationToken);
            Assert.Equal(assign || n == 2 ? replacement : "I", output.Message.Get("PV1-2").Raw);
        }
        Assert.Equal(original, source.Snapshot.ReadBytes(new(0, source.Snapshot.Length))); Assert.False(source.IsDirty); Assert.False(source.CanUndo);
    });
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task EditingEitherSearchInputDiscardsTheReviewedProposalAndRequiresReanalysis(bool changeFind) => InShell(async (window, manager, _, panes) =>
    {
        var dialog = Open(panes, panes.SearchReplace); Pump(dialog); Enter(dialog, dialog.Replacement, "E");
        Click(dialog.Analyze); await dialog.Completion; var previous = dialog.Result!; Assert.Equal(2, previous.Summary.Replacements);
        dialog.Confirm.IsChecked = true; Assert.True(dialog.Apply.IsEnabled);
        Enter(dialog, changeFind ? dialog.Find : dialog.Replacement, changeFind ? "O" : "");
        Assert.Null(dialog.Result); Assert.False(previous.IsCurrent); await previous.Disposal;
        Assert.False(dialog.Apply.IsEnabled); Assert.False(dialog.Confirm.IsChecked); Assert.Single(manager.Documents);
        Click(dialog.Analyze); await dialog.Completion;
        Assert.Equal(changeFind ? "O" : "I", dialog.Result!.Request.Find); Assert.Equal(changeFind ? "E" : "", dialog.Result.Request.Replacement);
        Assert.Equal(changeFind ? 1 : 2, dialog.Result.Summary.Replacements); Assert.False(dialog.Apply.IsEnabled);
        dialog.Close(); await window.InspectorActionTask; Assert.Single(manager.Documents);
    });
    [Fact]
    public Task SelectedComponentPrefillPreservesRawEncodedEscapesAndEmptyReplacementDefault() => InShell(async (window, _, source, panes) =>
    {
        const string raw = "Avery\\S\\Synthetic";
        source.IsEditMode = true; source.ReplaceWorkingText(Message(1).Replace("Avery", raw, StringComparison.Ordinal)); await source.EditRefresh;
        source.Inspection.SelectSegment(source.Inspection.Segments.Single(s => s.Identifier == "PID"));
        var field = source.Inspection.InspectField(5)!; var component = field.Children[0].Children[1]; panes.PrepareActions(component);
        var dialog = Open(panes, panes.SearchReplace); Pump(dialog);
        Assert.Equal("PID[1]-5[1].2", component.Path.ToString()); Assert.Equal(raw, dialog.Find.Text); Assert.Empty(dialog.Replacement.Text!);
        AssertFullyVisible(dialog.Find, dialog); AssertFullyVisible(dialog.Replacement, dialog);
        dialog.Close(); await window.InspectorActionTask; Assert.Equal(raw, source.Message.Get("PID-5.2").Raw);
    });
    [Theory]
    [InlineData(14)] [InlineData(32)]
    public Task UniqueAnalysisKeepsScopeVisibleAndHasNoMutationInputs(double font) => InShell(async (window, _, _, panes) =>
    {
        Assert.True(Get<ApplicationPreferences>(window, "preferences").SetContentFont("tool.field-inspector", font));
        var dialog = Open(panes, panes.UniqueValues); dialog.Width = 580; dialog.Height = 560; Pump(dialog);
        AssertFullyVisible(dialog.Scope, dialog); AssertFullyVisible(dialog.AllOccurrences, dialog);
        Assert.DoesNotContain(dialog.Find, dialog.GetVisualDescendants()); Assert.DoesNotContain(dialog.Replacement, dialog.GetVisualDescendants());
        Assert.False(dialog.Find.IsVisible); Assert.False(dialog.Replacement.IsVisible); dialog.Close(); await window.InspectorActionTask;
    });
}
