using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using HL7Workbench.Core;
using HL7Workbench.Desktop.Docking;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed class SchemaRibbonTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private const string ProfileId = "hl7.v251.ADT.A01";
    private static string Message => SyntheticMessageGenerator.Generate(new(), Token).Messages[0];
    private static void Pump(Window window) { window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static async Task Drain(DocumentManager documents)
    { var docs = documents.Documents.ToArray(); documents.Dispose(); await Task.WhenAll(docs.Select(d => d.Disposal)); }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    public async Task Hl7RibbonGroupsSchemaCommandsAndTargetsDockedOrFloatingActiveMessage(bool dark, bool floating)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            Application.Current!.Styles.Add(new Dock.Avalonia.Themes.Fluent.DockFluentTheme());
            Application.Current.DataTemplates.Add(new DockContentTemplate { DockedInspection = true });
            var root = Path.Combine(Path.GetTempPath(), "hl7-schema-ribbon-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            var window = new MainWindow(root) { Width = 1500, Height = 1000, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light }; window.Show();
            var factory = Assert.IsType<WorkspaceDockFactory>(window.FindControl<DockControl>("WorkspaceDock")!.Layout!.Factory);
            factory.ShowTool(WorkspaceDockFactory.ComparisonId);
            var documents = Assert.IsType<ComparisonViewModel>(factory.GetDockable<ITool>(WorkspaceDockFactory.ComparisonId)!.Context).Documents;
            factory.CloseDockable(factory.GetDockable<ITool>(WorkspaceDockFactory.ComparisonId)!);
            try
            {
                var ribbon = Assert.IsType<UtilityRibbon>(window.FindControl<ContentControl>("RibbonHost")!.Content);
                ribbon.SelectTab(2); Pump(window);
                var surface = (Control)(ribbon.IsExpanded ? ribbon.PinnedHost.Content! : ribbon.TemporaryPopup.Child!);
                var caption = Assert.Single(surface.GetVisualDescendants().OfType<TextBlock>(), c => c.Text == "Schema Validation");
                var group = Assert.IsType<StackPanel>(caption.Parent);
                Button Command(string name) => Assert.Single(group.GetVisualDescendants().OfType<Button>(), b => AutomationProperties.GetName(b) == name);
                var apply = Command("Apply/Change Schema"); var remove = Command("Remove Schema"); var next = Command("Next Issue");
                Assert.Equal(6, group.GetVisualDescendants().OfType<Button>().Count());
                Assert.False(apply.Command!.CanExecute(null)); Assert.False(remove.Command!.CanExecute(null)); Assert.False(next.Command!.CanExecute(null));
                Assert.True(Command("Import Schema").Command!.CanExecute(null)); Assert.True(Command("Manage Schemas").Command!.CanExecute(null));
                Assert.Contains("F8", ToolTip.GetTip(next)!.ToString());
                // Both pinned and popup hosts share these exact command instances.
                var schemaCommand = apply.Command;
                Click(ribbon.Pin); ribbon.SelectTab(2); Pump(window);
                surface = (Control)(ribbon.IsExpanded ? ribbon.PinnedHost.Content! : ribbon.TemporaryPopup.Child!);
                Assert.Same(schemaCommand, surface.GetVisualDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Apply/Change Schema").Command);
                apply = surface.GetVisualDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Apply/Change Schema");
                remove = surface.GetVisualDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Remove Schema");
                next = surface.GetVisualDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Next Issue");
                var doc = MessageFileReader.Open(documents, "Synthetic", Hl7Parser.Parse(Message)); factory.OpenDocument(doc.Id); Pump(window);
                if (floating) { factory.FloatDockable(factory.GetDockable<IDocument>(doc.Id)!); Pump(window); }
                Assert.True(apply.IsEffectivelyEnabled); apply.Focus(NavigationMethod.Tab); Assert.True(apply.IsKeyboardFocusWithin);
                Assert.True(apply.Command!.CanExecute(null)); apply.Command.Execute(null); Pump(window);
                var picker = Assert.Single(window.OwnedWindows.OfType<SchemaSelectionDialog>()); await picker.Ready; Pump(picker);
                picker.ApplySchema.Focus(NavigationMethod.Tab); Assert.True(picker.ApplySchema.IsKeyboardFocusWithin);
                Click(picker.ApplySchema);
                // Dialog completion starts application asynchronously. Await the whole
                // shell operation rather than a validation task from before Apply.
                await window.SchemaCommandTask; Pump(window);
                Assert.Equal(ProfileId, doc.AppliedSchemaId); Assert.True(remove.Command!.CanExecute(null)); Assert.True(next.Command!.CanExecute(null));
                Assert.True(remove.IsEffectivelyEnabled); Assert.True(next.IsEffectivelyEnabled);
                next.Command.Execute(null); Pump(window);
                var hosts = factory.Root.Windows?.Select(w => w.Host).OfType<Window>() ?? [];
                var view = window.GetVisualDescendants().Concat(hosts.SelectMany(w => w.GetVisualDescendants())).OfType<FileDocumentView>().Single(v => v.Document == doc && v.IsEffectivelyVisible).ActiveView!;
                Assert.True(view.Editor.TextArea.IsKeyboardFocusWithin);
                Assert.Equal(doc.SchemaValidation!.Diagnostics[0].Range.Offset, view.Editor.SelectionStart);
                Assert.DoesNotContain(view.GetVisualDescendants().OfType<Button>(), b => b.Content is string text && text.Contains("Schema", StringComparison.OrdinalIgnoreCase));
                var other = MessageFileReader.Open(documents, "Unrelated synthetic", Hl7Parser.Parse(Message)); factory.OpenDocument(other.Id); Pump(window);
                Assert.False(remove.Command!.CanExecute(null)); Assert.False(next.Command!.CanExecute(null)); Assert.Null(other.AppliedSchemaId);
                Assert.False(remove.IsEffectivelyEnabled); Assert.False(next.IsEffectivelyEnabled);
                documents.Activate(doc.Id); factory.OpenDocument(doc.Id); Pump(window); remove.Command.Execute(null);
                Assert.Null(doc.AppliedSchemaId); Assert.Null(doc.SchemaValidation); Assert.False(remove.Command!.CanExecute(null));
            }
            finally
            {
                factory.CloseFloatingWindows(); var docs = documents.Documents.ToArray(); foreach (var doc in docs) documents.Close(doc.Id, true);
                window.Close(); await Task.WhenAll(docs.Select(d => d.Disposal)); Directory.Delete(root, true);
            }
            return true;
        }, Token);
    }

    [Theory]
    [InlineData("document")] [InlineData("message")] [InlineData("edit")] [InlineData("close")] [InlineData("cancel")]
    public async Task SelectorNeverAppliesToAChangedTargetOrAfterCancellation(string change)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var documents = new DocumentManager(); var doc = MessageFileReader.Open(documents, "Synthetic", Hl7Parser.Parse(Message));
            var owner = new Window(); owner.Show();
            using var commands = new SchemaValidationCommands(documents, () => owner, (_, _) => { });
            try
            {
                var notifications = 0; commands.Actions["Remove Schema"].CanExecuteChanged += (_, _) => notifications++;
                commands.Actions["Apply/Change Schema"].Execute(null); var picker = commands.Dialog!; await picker.Ready;
                Assert.False(commands.Actions["Apply/Change Schema"].CanExecute(null));
                commands.Actions["Manage Schemas"].Execute(null); Assert.Same(picker, commands.Dialog); Assert.Single(owner.OwnedWindows);
                switch (change)
                {
                    case "document": MessageFileReader.Open(documents, "Other synthetic", Hl7Parser.Parse(Message)); break;
                    case "message": doc.InsertMessage(MessageInsertionPosition.End); break;
                    case "edit": doc.IsEditMode = true; doc.ReplaceWorkingText(doc.EditableText! + "\rZPI|SYNTHETIC"); break;
                    case "close": documents.Close(doc.Id, true); break;
                }
                // The shell also checks ownership if a result was already queued by the dialog.
                if (change == "cancel") picker.Close(); else picker.Close(ProfileId);
                await commands.Pending; Assert.Null(doc.AppliedSchemaId); Assert.Null(documents.ActiveDocument?.AppliedSchemaId); Assert.True(notifications > 0);
                if (change != "close")
                {
                    documents.Activate(doc.Id); await doc.EditRefresh;
                    await doc.ApplySchemaAsync(ProfileId); Assert.True(commands.Actions["Remove Schema"].CanExecute(null));
                    commands.Actions["Remove Schema"].Execute(null); Assert.False(commands.Actions["Remove Schema"].CanExecute(null));
                }
            }
            finally { owner.Close(); await Drain(documents); }
            return true;
        }, Token);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ImportAndManageReuseLibraryDialogAndCancellationCannotPublish(bool closeDuringPicker)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var documents = new DocumentManager(); var owner = new Window(); owner.Show();
            using var commands = new SchemaValidationCommands(documents, () => owner, (_, _) => { });
            var profile = new Hl7ValidationProfile { Id = "synthetic.ribbon", Name = "Synthetic ribbon profile", BaseProfile = ProfileId, MessageType = "ADT", TriggerEvent = "A01", MessageStructure = "ADT_A01" };
            var profilePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Synthetic-" + Guid.NewGuid().ToString("N") + ".json");
            await File.WriteAllBytesAsync(profilePath, Hl7ProfileImporter.ExportJson(profile), Token);
            var file = await owner.StorageProvider.TryGetFileFromPathAsync(profilePath); Assert.NotNull(file);
            var picked = new TaskCompletionSource<IReadOnlyList<IStorageFile>>(); var pickCount = 0;
            commands.PickProfile = options => { Assert.Contains("Import", options.Title); pickCount++; return picked.Task; };
            try
            {
                commands.Actions["Import Schema"].Execute(null); var picker = commands.Dialog!; Pump(picker);
                Assert.Equal("Manage Schemas", picker.Title); Assert.False(picker.ApplySchema.IsVisible); Assert.False(picker.ImportProfile.IsEnabled);
                commands.Actions["Manage Schemas"].Execute(null); commands.Actions["Import Schema"].Execute(null);
                await picker.ImportAsync(); Assert.Equal(1, pickCount); Assert.Single(owner.OwnedWindows);
                if (closeDuringPicker) picker.Close();
                picked.SetResult([file]);
                if (closeDuringPicker) { await commands.Pending; Assert.Null(documents.ValidationProfiles.Find(profile.Id)); }
                else
                {
                    // Import completes before the modal selection task, which stays open for review.
                    while (!picker.ImportProfile.IsEnabled) await Task.Yield();
                    var entry = Assert.IsType<ValidationProfileEntry>(picker.Profiles.SelectedItem); Assert.Equal(profile.Id, entry.Profile.Id);
                    Assert.Contains("Synthetic ribbon profile", picker.Details.Text); Assert.True(picker.ReimportProfile.IsEnabled);
                    await File.WriteAllBytesAsync(profilePath, Hl7ProfileImporter.ExportJson(profile with { Name = "Synthetic revised profile" }), Token);
                    picker.PickProfile = _ => Task.FromResult<IReadOnlyList<IStorageFile>>([file]);
                    await picker.ImportAsync(true); var revised = documents.ValidationProfiles.Find(profile.Id)!;
                    Assert.NotEqual(entry.Revision, revised.Revision); Assert.Equal("Synthetic revised profile", revised.Profile.Name);
                    Click(picker.RemoveProfile); Assert.Null(documents.ValidationProfiles.Find(profile.Id)); picker.Close(); await commands.Pending;
                    commands.Actions["Manage Schemas"].Execute(null); Assert.NotSame(picker, commands.Dialog); commands.Dialog!.Close(); await commands.Pending;
                }
                Assert.Empty(documents.Documents);
            }
            finally { commands.Dialog?.Close(); file.Dispose(); File.Delete(profilePath); owner.Close(); await Drain(documents); }
            return true;
        }, Token);
    }

    [Theory]
    [InlineData(280, 600, false, 1)] [InlineData(380, 800, true, 1.5)]
    [InlineData(800, 700, false, 2)] [InlineData(1600, 1000, true, 1)]
    [InlineData(280, 600, true, 2)] [InlineData(1600, 1000, false, 1.5)]
    public async Task LongProfileStatusAndEditorGroupsReflowWithoutOverlapAcrossThemesScalesAndHosts(double width, double height, bool dark, double scale)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var documents = new DocumentManager();
            var profile = new Hl7ValidationProfile { Id = "synthetic.long-name", Name = "Synthetic " + new string('W', 150), BaseProfile = ProfileId, MessageType = "ADT", TriggerEvent = "A01", MessageStructure = "ADT_A01" };
            documents.ValidationProfiles.Import(Hl7ProfileImporter.ExportJson(profile), false, "Synthetic.json", token: Token);
            var doc = MessageFileReader.Open(documents, "Synthetic", Hl7Parser.Parse(Message)); await doc.ApplySchemaAsync(profile.Id);
            var file = new FileDocumentView(doc, true); var dock = new Border { Child = file };
            var transform = new LayoutTransformControl { Child = dock, LayoutTransform = new ScaleTransform(scale, scale) };
            var window = new Window { Content = transform, Width = width * scale, Height = height * scale, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light }; window.Show(); Pump(window);
            try
            {
                for (var host = 0; host < 2; host++)
                {
                    var view = file.ActiveView!; Pump(window);
                    Assert.Equal(profile.Name, view.ValidationProfileName.Text); Assert.Equal(TextTrimming.CharacterEllipsis, view.ValidationProfileName.TextTrimming);
                    Assert.Contains(profile.Name, ToolTip.GetTip(view.ValidationProfileName)!.ToString());
                    Assert.Contains("Incomplete", view.ValidationSummary.Text); Assert.Contains("errors", view.ValidationSummary.Text); Assert.Contains("warnings", view.ValidationSummary.Text);
                    var rows = view.RawPane.Children.Where(c => c.IsVisible && c.Bounds.Height > 0).OrderBy(Grid.GetRow).ToArray();
                    for (var i = 1; i < rows.Length; i++) Assert.True(rows[i - 1].Bounds.Bottom <= rows[i].Bounds.Top + .5, "Editor groups overlap");
                    Assert.True(view.Editor.Bounds.Height > 80, "Raw content retains a usable viewport");
                    foreach (var bar in view.GetVisualDescendants().OfType<AdaptiveActionBar>())
                    {
                        var boxes = bar.Children.Where(c => c.IsVisible).Select(c => c.Bounds).ToArray();
                        foreach (var box in boxes) { Assert.InRange(box.Right, 0, bar.Bounds.Width + .5); Assert.InRange(box.Bottom, 0, bar.Bounds.Height + .5); }
                        for (var i = 0; i < boxes.Length; i++) for (var j = i + 1; j < boxes.Length; j++)
                        { var intersection = boxes[i].Intersect(boxes[j]); Assert.True(intersection.Width <= .1 || intersection.Height <= .1); }
                    }
                    foreach (var text in new[] { view.ValidationProfileName, view.ValidationSummary })
                    { Assert.True(text.IsEffectivelyVisible); Assert.InRange(text.Bounds.Right, 0, ((Control)text.Parent!).Bounds.Width + .5); }
                    foreach (var button in view.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible && b.IsEffectivelyEnabled && b.Focusable))
                    { Assert.DoesNotContain(button.Content?.ToString() ?? "", new[] { "Apply Schema…", "Remove schema", "Previous issue", "Next issue" }); button.Focus(NavigationMethod.Tab); Assert.True(button.IsKeyboardFocusWithin); }
                    var summary = view.ValidationSummary.Text; view.NavigateValidationIssue(1); Assert.Equal(summary, view.ValidationSummary.Text); Assert.True(view.Editor.TextArea.IsKeyboardFocusWithin);
                    // Reparent the same file content as Dock does when floating/redocking.
                    if (host == 0) { dock.Child = null; window.Content = null; transform.Child = file; window.Content = transform; Pump(window); }
                }
                Assert.False(doc.IsDirty);
            }
            finally { window.Close(); await Drain(documents); }
            return true;
        }, Token);
    }

}
