using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using System.Text;
using HL7Workbench.Core;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed class SchemaValidationViewTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static string Message => SyntheticMessageGenerator.Generate(new() { DemographicStyle = SyntheticDemographicStyle.RealisticSynthetic }, Token).Messages[0];
    private static void Pump(Window window) { window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static async Task Drain(DocumentManager documents)
    { var docs = documents.Documents.ToArray(); documents.Dispose(); await Task.WhenAll(docs.Select(d => d.Disposal)); }
    private static Hl7ValidationProfile Profile => new()
    {
        Id = "synthetic.desktop", Name = "Synthetic HIE", MessageType = "ADT", TriggerEvent = "A01", MessageStructure = "ADT_A01", BaseProfile = "hl7.v251.ADT.A01",
        TradingPartner = "Synthetic partner", Segments = [new() { Id = "PV1", Fields = [new() { Position = 2, Min = 1, DataType = "IS", AllowedValues = ["I"] }] }]
    };

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ExplicitApplyShowsInlineStatusAccurateUnderlinesHoverAndNavigationWithoutChangingText(bool dark)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var documents = new DocumentManager(); documents.ValidationProfiles.Import(Hl7ProfileImporter.ExportJson(Profile), false, "Synthetic.json", token: Token);
            var doc = MessageFileReader.Open(documents, "Synthetic", Hl7Parser.Parse(Message.Replace("PV1|1|I", "PV1|1|X")));
            var editor = MessageViewer.CreateEditor(doc.EditableText!); var view = new MessageInspectionView(doc.Inspection, editor, doc, true);
            var window = new Window { Content = view, Width = 800, Height = 500, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light }; window.Show(); Pump(window);
            try
            {
                Assert.Equal(SchemaValidationState.NotApplied, doc.ValidationState); Assert.Contains("not applied", view.ValidationProfileName.Text);
                var original = editor.Text; await doc.ApplySchemaAsync(Profile.Id); Pump(window);
                Assert.Equal(original, editor.Text); Assert.Contains("Synthetic HIE", view.ValidationProfileName.Text); Assert.Contains("1 errors", view.ValidationSummary.Text);
                Assert.NotNull(doc.AppliedSchemaId); Assert.NotEmpty(doc.SchemaValidation!.Diagnostics);
                var issue = Assert.Single(doc.SchemaValidation!.Diagnostics, d => d.RuleId == "Value.Allowed");
                var renderer = Assert.Single(editor.TextArea.TextView.BackgroundRenderers.OfType<ValidationInlineRenderer>());
                Assert.Contains(renderer.Decorations(), d => d.Diagnostic == issue && editor.Text.Substring((int)d.Display.Offset, (int)d.Display.Length) == "X");
                var tip = ValidationInlineRenderer.ToolTipAt(doc, issue.Range.Offset); Assert.NotNull(tip); Assert.Contains("PV1[1]-2", tip.Text); Assert.Contains("Value.Allowed", tip.Text); Assert.Contains(Profile.Id, tip.Text); Assert.True(tip.Text.Length <= 2049);
                // Navigate the full retained deterministic sequence, then wrap.
                for (var i = 0; i < doc.SchemaValidation.Diagnostics.Count; i++) view.NavigateValidationIssue(1);
                view.NavigateValidationIssue(1); Assert.Equal(doc.SchemaValidation.Diagnostics[0].Range.Offset, editor.SelectionStart);
                editor.TextArea.Focus(); window.KeyPress(Key.F8, RawInputModifiers.Shift, PhysicalKey.F8, null); window.KeyRelease(Key.F8, RawInputModifiers.Shift, PhysicalKey.F8, null);
                Assert.Equal(doc.SchemaValidation.Diagnostics[^1].Range.Offset, editor.SelectionStart);
                doc.RemoveSchema(); Assert.Null(doc.SchemaValidation); Assert.Empty(renderer.Decorations()); Assert.Contains("not applied", view.ValidationProfileName.Text); Assert.Equal(original, editor.Text);
            }
            finally { window.Close(); await Drain(documents); }
            return true;
        }, Token);
    }

    [Fact]
    public async Task EditorEditsImmediatelyClearStaleMarkersAndRevalidateThroughExistingHistory()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var documents = new DocumentManager(); var doc = MessageFileReader.Open(documents, "Synthetic", Hl7Parser.Parse(Message)); doc.IsEditMode = true;
            var editor = MessageViewer.CreateEditor(doc.EditableText!); var view = new MessageInspectionView(doc.Inspection, editor, doc, true); var window = new Window { Content = view }; window.Show(); Pump(window);
            try
            {
                await doc.ApplySchemaAsync("hl7.v251.ADT.A01"); Assert.NotNull(doc.SchemaValidation);
                var at = editor.Text.IndexOf("PV1|1|I", StringComparison.Ordinal) + 6;
                editor.Document.Replace(at, 1, ""); Assert.Null(doc.SchemaValidation); Assert.Equal(SchemaValidationState.Stale, doc.ValidationState);
                await doc.EditRefresh; await doc.ValidationRefresh; Pump(window);
                var issue = Assert.Single(doc.SchemaValidation!.Diagnostics, d => d.RuleId == "Value.Required" && d.Path == "PV1[1]-2"); Assert.True(issue.IsBoundary);
                var renderer = Assert.Single(editor.TextArea.TextView.BackgroundRenderers.OfType<ValidationInlineRenderer>()); Assert.Contains(renderer.Decorations(), d => d.Diagnostic == issue && d.Display.Length == 0);
                doc.Undo(); await doc.EditRefresh; await doc.ValidationRefresh; Pump(window); Assert.DoesNotContain(doc.SchemaValidation!.Diagnostics, d => d.RuleId == "Value.Required" && d.Path == "PV1[1]-2");
                Assert.False(doc.CanUndo); doc.Redo(); await doc.EditRefresh; await doc.ValidationRefresh; Assert.True(doc.SchemaValidation!.Errors > 0);
            }
            finally { window.Close(); await Drain(documents); }
            return true;
        }, Token);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ProfilePickerDefaultsDetectBuiltInsButNeverAutoApplyAndImportedMetadataIsVisible(bool selectedDefault)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var documents = new DocumentManager(); var entry = documents.ValidationProfiles.Import(Hl7ProfileImporter.ExportJson(Profile), false, "Synthetic-HIE.json", token: Token);
            documents.Preferences = new(SchemaMode: selectedDefault ? SchemaInterpretationMode.SelectedTarget : SchemaInterpretationMode.MessageDeclared, TargetSchema: entry.Profile.Id);
            var doc = MessageFileReader.Open(documents, "Synthetic", Hl7Parser.Parse(Message)); var dialog = new SchemaSelectionDialog(doc); dialog.Show(); await dialog.Ready; Pump(dialog);
            try
            {
                var selected = Assert.IsType<ValidationProfileEntry>(dialog.Profiles.SelectedItem); Assert.Equal(selectedDefault ? entry.Profile.Id : "hl7.v251.ADT.A01", selected.Profile.Id);
                Assert.Null(doc.AppliedSchemaId); Assert.Null(doc.SchemaValidation); Assert.True(dialog.ApplySchema.IsEnabled);
                dialog.Profiles.SelectedItem = dialog.Profiles.Items.OfType<ValidationProfileEntry>().Single(e => e.Profile.Id == entry.Profile.Id); Pump(dialog);
                Assert.Contains("Synthetic partner", dialog.Details.Text); Assert.Contains("Synthetic-HIE.json", dialog.Details.Text); Assert.Contains("Incomplete", dialog.Details.Text);
                Assert.True(dialog.ReimportProfile.IsEnabled); Click(dialog.RemoveProfile); Assert.Null(doc.ValidationLibrary.Find(entry.Profile.Id));
                Assert.Equal(SchemaValidationState.NotApplied, doc.ValidationState); Assert.Null(doc.SchemaValidation);
            }
            finally { dialog.Close(); await Drain(documents); }
            return true;
        }, Token);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ValidationPreferencePersistsOnlyInitialChoiceInBothThemes(bool dark)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "hl7-schema-preferences-" + Guid.NewGuid().ToString("N"));
            var preferences = new ApplicationPreferences(Path.Combine(root, "preferences.json")); var library = new ValidationProfileLibrary();
            var imported = library.Import(Hl7ProfileImporter.ExportJson(Profile), false, "Synthetic.json", token: Token);
            var window = new PreferencesWindow(preferences, library) { RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light }; window.Show(); Pump(window);
            try
            {
                window.Categories.SelectedIndex = 3; Pump(window); Assert.True(window.SchemaMode.IsEnabled); Assert.False(window.DefaultValidationProfile.IsEnabled);
                window.SchemaMode.SelectedIndex = 1; Assert.True(window.DefaultValidationProfile.IsEnabled);
                window.DefaultValidationProfile.SelectedItem = window.DefaultValidationProfile.Items.OfType<ValidationProfileEntry>().Single(e => e.Profile.Id == imported.Profile.Id);
                Assert.True(window.Apply()); var loaded = EditorPreferences.Load(Path.Combine(root, "preferences.json"));
                Assert.Equal(SchemaInterpretationMode.SelectedTarget, loaded.SchemaMode); Assert.Equal(imported.Profile.Id, loaded.TargetSchema);
            }
            finally { window.Close(); if (Directory.Exists(root)) Directory.Delete(root, true); }
            return true;
        }, Token);
    }

    [Fact]
    public async Task ApplySchemaActionUsesExistingModalPickerAndDoesNotAddAResultsTool()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var documents = new DocumentManager(); var doc = MessageFileReader.Open(documents, "Synthetic", Hl7Parser.Parse(Message));
            var view = new MessageInspectionView(doc.Inspection, MessageViewer.CreateEditor(doc.EditableText!), doc, true); var owner = new Window { Content = view }; owner.Show(); Pump(owner);
            try
            {
                using var commands = new SchemaValidationCommands(documents, () => owner, (_, direction) => view.NavigateValidationIssue(direction));
                commands.Actions["Apply/Change Schema"].Execute(null); Pump(owner);
                var picker = Assert.Single(owner.OwnedWindows.OfType<SchemaSelectionDialog>()); await picker.Ready; Click(picker.ApplySchema);
                await commands.Pending; Assert.Equal("hl7.v251.ADT.A01", doc.AppliedSchemaId); Assert.NotNull(doc.SchemaValidation); Assert.Empty(owner.OwnedWindows);
                Assert.Equal("Apply Schema", picker.Title);
            }
            finally { owner.Close(); await Drain(documents); }
            return true;
        }, Token);
    }

    [Fact]
    public async Task ReconstructedViewsHaveOneRendererEachAndRetainDocumentOwnedSelection()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var documents = new DocumentManager(); var doc = MessageFileReader.Open(documents, "Synthetic", Hl7Parser.Parse(Message)); await doc.ApplySchemaAsync("hl7.v251.ADT.A01");
            var result = doc.SchemaValidation; var window = new Window(); window.Show();
            try
            {
                for (var i = 0; i < 4; i++)
                {
                    var editor = MessageViewer.CreateEditor(doc.EditableText!); var view = new MessageInspectionView(doc.Inspection, editor, doc, true); window.Content = view; Pump(window);
                    Assert.Single(editor.TextArea.TextView.BackgroundRenderers.OfType<ValidationInlineRenderer>()); Assert.Same(result, doc.SchemaValidation);
                    Assert.Contains("Incomplete", view.ValidationSummary.Text); window.Content = null; Pump(window);
                }
                Assert.Same(result, doc.SchemaValidation);
            }
            finally { window.Close(); await Drain(documents); }
            return true;
        }, Token);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task LargeSourceDiagnosticsMapVisibleFragmentsWithoutPaintingOmissionMarkers(bool dark)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var documents = new DocumentManager(); var original = SyntheticMessageGenerator.Generate(new() { MessageType = SyntheticMessageType.OruR01, DemographicStyle = SyntheticDemographicStyle.RealisticSynthetic }, Token).Messages[0];
            var parsed = Hl7Parser.Parse(original); var field = parsed.Segments.First(s => s.Identifier == "OBX").GetField(5)!;
            var large = original.Remove(field.CharacterOffset, field.Raw.Length).Insert(field.CharacterOffset, new string('A', 128 * 1024));
            using var input = new MemoryStream(Encoding.UTF8.GetBytes(large)); var doc = await MessageFileReader.OpenFileAsync(documents, "Synthetic large", input, Token);
            await doc.ApplySchemaAsync("hl7.v251.ORU.R01"); var presentation = Assert.IsType<MessagePresentation>(doc.Presentation);
            var editor = MessageViewer.CreateEditor(""); var view = new MessageInspectionView(doc.Inspection, editor, doc, true); var window = new Window { Content = view, Width = 900, Height = 550,
                RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light }; window.Show(); Pump(window);
            try
            {
                await view.PresentationView!.Ready; Pump(window); var renderer = Assert.Single(editor.TextArea.TextView.BackgroundRenderers.OfType<ValidationInlineRenderer>());
                var marker = Assert.Single(presentation.Fragments, f => f.Marker is not null);
                Assert.All(renderer.Decorations(), d => Assert.False(d.Display.Offset < marker.Display.End && d.Display.End > marker.Display.Offset));
                Assert.Contains(renderer.Decorations(), d => d.Diagnostic.Path == "OBX[1]-5[1]");
                Assert.Null(presentation.SourceOffset(marker.Display.Offset)); Assert.Equal(SchemaValidationState.Incomplete, doc.ValidationState);
                Assert.Null(doc.MaterializedMessage); Assert.False(doc.IsDirty); Assert.NotEmpty(doc.SchemaValidation!.Diagnostics);
            }
            finally { window.Close(); await Drain(documents); }
            return true;
        }, Token);
    }
}
