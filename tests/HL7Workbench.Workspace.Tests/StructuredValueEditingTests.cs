using System.Text;
using HL7Workbench.Core;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed class StructuredValueEditingTests
{
    private const string Header = "MSH|^~\\&|SYN|TEST|DEST|TEST|20261031000000||ADT^A01|SYN-1|T|2.5.1\r";
    private const string Body = "PID|1||SYN-A^^^TEST&LOCAL^MR~SYN-B^^^TEST&BRANCH^ALT||Example^Avery&Initial~Other^Second||TAIL||\r\nNTE|1||KEEP\\Zraw^data\\\nPID|2||SECOND||Unchanged^Synthetic\r";
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private sealed class Files : IAsyncDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "hl7-structured-synthetic-" + Guid.NewGuid().ToString("N"));
        internal DocumentManager Manager { get; } = new();
        private readonly List<MessageDocument> owned = [];
        internal Files() => Directory.CreateDirectory(Root);
        internal MessageDocument Memory(string text, Encoding? encoding = null)
        {
            var model = encoding is null ? Hl7Parser.Parse(text) : Hl7Parser.Parse(encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray(), new() { Encoding = encoding });
            var doc = new MessageDocument(Guid.NewGuid().ToString(), "Synthetic", model); Manager.Open(doc); doc.IsEditMode = true; owned.Add(doc); return doc;
        }
        internal async Task<MessageDocument> Open(string text, Encoding encoding)
        {
            using var input = new MemoryStream(encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray());
            var doc = await MessageFileReader.OpenFileAsync(Manager, "Synthetic.hl7", input, Token); doc.IsEditMode = true; owned.Add(doc); return doc;
        }
        internal StructuredValueEditSession Edit(MessageDocument doc, string path)
        {
            var node = Select(doc.Inspection, path); return new(Manager, new(doc, doc.Inspection, node));
        }
        public async ValueTask DisposeAsync()
        {
            Manager.Dispose(); await Task.WhenAll(owned.Select(d => d.Disposal));
            try { Directory.Delete(Root, true); } catch (DirectoryNotFoundException) { }
        }
    }
    private static InspectionNode Select(MessageInspection inspection, string text)
    {
        var path = Hl7Path.Parse(text);
        inspection.SelectSegment(inspection.Segments.Single(s => s.Identifier == path.Segment && s.Occurrence == path.Occurrence));
        var target = inspection.InspectField(path.Field)!;
        foreach (var index in new[] { path.Repetition, path.Component, path.Subcomponent })
        { if (index is null) break; target = target.Children[index.Value - 1]; }
        inspection.SelectNode(target); return target;
    }
    [Theory]
    [InlineData("PID[1]-5", "New^Rowan&Q~Third^Name")]
    [InlineData("PID[1]-5[2]", "New^Rowan&Q")]
    [InlineData("PID[1]-5[1].2", "Rowan&Q")]
    [InlineData("PID[1]-5[1].2.1", "Rowan")]
    [InlineData("PID[1]-4", "SYN-EMPTY")]
    [InlineData("PID[1]-3[1].2", "SYN-EMPTY-COMPONENT")]
    [InlineData("PID[2]-5[1].2", "EditedSecond")]
    public async Task ExistingLevelsSpliceOnlyTheirRangeAndUseOneExactUndoRedoStep(string path, string value)
    {
        await using var files = new Files(); var doc = files.Memory(Header + Body);
        var original = doc.EditableText!; var node = Select(doc.Inspection, path); var range = node.Range64!.Value;
        var expected = string.Concat(original.AsSpan(0, (int)range.Offset), value, original.AsSpan((int)range.End));
        using var edit = new StructuredValueEditSession(files.Manager, new(doc, doc.Inspection, node));
        Assert.True(edit.CanApply); Assert.True(edit.TryApply(value), edit.Status); Assert.Equal(expected, doc.EditableText);
        Assert.True(doc.IsDirty); await edit.RestoreSelectionAsync(); Assert.Equal(edit.Path, doc.Inspection.SelectedNode!.Path); Assert.Equal(value, doc.Inspection.SelectedNode.Raw);
        Assert.Equal("SECOND", doc.Message.Get("PID[2]-3").Raw); Assert.Equal("KEEP\\Zraw^data\\", doc.Message.Get("NTE-3").Raw);
        doc.Undo(); await doc.EditRefresh; Assert.Equal(original, doc.EditableText); Assert.False(doc.CanUndo); Assert.False(doc.IsDirty);
        doc.Redo(); await doc.EditRefresh; Assert.Equal(expected, doc.EditableText); Assert.False(doc.CanRedo);
    }
    [Theory]
    [InlineData("PID-5", "A|B")]
    [InlineData("PID-5[1]", "A~B")]
    [InlineData("PID-5.2", "A^B")]
    [InlineData("PID-5.2.1", "A&B")]
    [InlineData("PID-5.2", "unclosed\\Zescape")]
    [InlineData("PID-5.2", "A\rB")]
    [InlineData("PID-5.2", "A\nB")]
    [InlineData("PID-5.2", "A\tB")]
    [InlineData("PID-5.2", "A\0B")]
    [InlineData("PID-5.2", "A\u007fB")]
    [InlineData("PID-5.2", "A\u0085B")]
    [InlineData("PID-5.2", "A\u2028B")]
    public async Task UnsafeValuesNeverChangeTextDirtyStateOrExistingRedo(string path, string value)
    {
        await using var files = new Files(); var doc = files.Memory(Header + Body);
        var original = doc.EditableText!; doc.ReplaceWorkingText(original + "NTE|2||SYN\r"); await doc.EditRefresh;
        doc.Undo(); await doc.EditRefresh; Assert.True(doc.CanRedo);
        using var edit = files.Edit(doc, path);
        Assert.False(edit.TryApply(value)); Assert.Equal(original, doc.EditableText); Assert.False(doc.IsDirty); Assert.False(doc.CanUndo); Assert.True(doc.CanRedo);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ActualDelimitersAndClosedEscapesRemainEncodedWithoutTouchingOtherValues(bool custom)
    {
        await using var files = new Files(); var text = Header + Body;
        if (custom) text = text.Replace('|', '*').Replace('^', '$').Replace('~', '%').Replace('\\', '!').Replace('&', '@');
        var doc = files.Memory(text); using var edit = files.Edit(doc, "PID-5.2.1");
        var value = custom ? "Rowan!S!Synthetic" : "Rowan\\S\\Synthetic";
        Assert.True(edit.TryApply(value), edit.Status); await edit.RestoreSelectionAsync();
        Assert.Equal(value, doc.Message.Get("PID-5.2.1").Raw); Assert.Equal(custom ? '!' : '\\', doc.Message.Delimiters.Escape);
        Assert.Contains(custom ? "!Zraw$data!" : "\\Zraw^data\\", doc.EditableText);
    }
    [Theory]
    [InlineData("mode")][InlineData("absent")][InlineData("msh1")][InlineData("msh2")][InlineData("ambiguous")]
    public async Task NoneditableTargetsStayInspectableWithoutCreatingStructure(string kind)
    {
        await using var files = new Files(); var doc = files.Memory(kind == "ambiguous" ? "MSH|^~^&|SYN\rPID|1||SYN\r" : Header + Body);
        var path = kind switch { "msh1" => "MSH-1", "msh2" => "MSH-2", "absent" => "PID-999", _ => "PID-1" };
        var node = Select(doc.Inspection, path); if (kind == "mode") doc.IsEditMode = false;
        Assert.NotNull(StructuredValueEditSession.UnavailableReason(doc, node)); var before = doc.EditableText;
        Assert.Throws<InvalidOperationException>(() => new StructuredValueEditSession(files.Manager, new(doc, doc.Inspection, node)));
        Assert.Equal(before, doc.EditableText); Assert.False(doc.CanUndo);
    }
    [Theory]
    [InlineData("selection")][InlineData("raw")][InlineData("document")][InlineData("mode")][InlineData("close")][InlineData("message")]
    public async Task InvalidatedSessionsNeverReviveOrRetargetAnIdenticalPath(string change)
    {
        await using var files = new Files(); var doc = await files.Open(Header + Body + Header + Body, new UTF8Encoding(false, true));
        using var edit = files.Edit(doc, "PID-5.2.1"); var before = doc.EditableText;
        switch (change)
        {
            case "selection": Select(doc.Inspection, "PID-5.1"); Select(doc.Inspection, "PID-5.2.1"); break;
            case "raw": doc.ReplaceWorkingText(doc.EditableText!.Replace("Avery", "RAW", StringComparison.Ordinal)); break;
            case "document": var other = files.Memory(Header + Body); files.Manager.Activate(doc.Id); break;
            case "mode": doc.IsEditMode = false; doc.IsEditMode = true; break;
            case "close": Assert.True(files.Manager.Close(doc.Id)); break;
            case "message": await doc.NavigateAsync(2, Token); await doc.NavigateAsync(1, Token); break;
        }
        Assert.False(edit.CanApply); Assert.False(edit.TryApply("WRONG"));
        if (change != "close") Assert.DoesNotContain("WRONG", doc.EditableText!);
        if (change is not ("close" or "raw")) { Assert.Equal(before, doc.EditableText); Assert.False(doc.CanUndo); }
    }
    [Fact]
    public async Task CancelUnchangedAndDisposedSessionsCreateNoEditAndReleaseTheirDraft()
    {
        await using var files = new Files(); var doc = files.Memory(Header + Body); var original = doc.EditableText;
        var edit = files.Edit(doc, "PID-5.2.1"); Assert.False(edit.TryApply(edit.OriginalValue)); edit.Dispose();
        Assert.Empty(edit.OriginalValue); Assert.False(edit.TryApply("WRONG")); Assert.Equal(original, doc.EditableText); Assert.False(doc.CanUndo); Assert.False(doc.IsDirty);
    }
    [Fact]
    public async Task EncodingAndExistingLineLimitsAreCheckedBeforeHistoryPublication()
    {
        await using var files = new Files(); var doc = files.Memory(Header + Body, Encoding.Latin1);
        using var edit = files.Edit(doc, "PID-5.2.1"); var original = doc.EditableText;
        Assert.False(edit.TryApply("\u03a9")); Assert.Contains("encoding", edit.Status);
        Assert.False(edit.TryApply(new string('X', 16385))); Assert.False(edit.TryApply(new string('X', 16380)));
        Assert.Equal(original, doc.EditableText); Assert.False(doc.CanUndo);
        Assert.True(edit.TryApply("Ren\u00e9"), edit.Status); await edit.RestoreSelectionAsync(); Assert.Equal("Ren\u00e9", doc.Message.Get("PID-5.2.1").Raw);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task SaveAsPreservesBomEncodingTerminatorsAndOtherMessagesByteForByte(bool utf16)
    {
        await using var files = new Files(); Encoding encoding = utf16 ? new UnicodeEncoding(false, true, true) : new UTF8Encoding(true, true);
        var text = Header + Body + Header + "PID|9||OTHER\r\n"; var doc = await files.Open(text, encoding);
        var snapshot = doc.Snapshot!.ReadBytes(new(0, doc.Snapshot.Length)); var node = Select(doc.Inspection, "PID-5.2.1"); var range = node.Range64!.Value;
        using var edit = new StructuredValueEditSession(files.Manager, new(doc, doc.Inspection, node));
        Assert.True(edit.TryApply("Rowan")); await edit.RestoreSelectionAsync();
        var destination = Path.Combine(files.Root, "synthetic-edited.hl7"); await doc.SaveAsync(destination, Token);
        var expected = string.Concat(text.AsSpan(0, (int)range.Offset), "Rowan", text.AsSpan((int)range.End));
        Assert.Equal(encoding.GetPreamble().Concat(encoding.GetBytes(expected)), File.ReadAllBytes(destination));
        Assert.Equal(snapshot, doc.Snapshot.ReadBytes(new(0, doc.Snapshot.Length))); Assert.False(doc.IsDirty);
        doc.Undo(); await doc.EditRefresh; Assert.True(doc.IsDirty); Assert.Equal(encoding.GetPreamble().Concat(encoding.GetBytes(expected)), File.ReadAllBytes(destination));
    }
    [Fact]
    public async Task LargeSourceBackedValueIsRejectedWithoutMaterializingOrRetainingAnotherSource()
    {
        await using var files = new Files(); var doc = await files.Open(Header + "OBX|1|TX|||" + new string('X', 200000) + "\r", new UTF8Encoding(false, true));
        var source = doc.Source; var node = Select(doc.Inspection, "OBX-5");
        Assert.NotNull(StructuredValueEditSession.UnavailableReason(doc, node));
        Assert.Throws<InvalidOperationException>(() => new StructuredValueEditSession(files.Manager, new(doc, doc.Inspection, node)));
        Assert.Same(source, doc.Source); Assert.Null(doc.WorkingText); Assert.False(doc.CanUndo);
    }
    [Fact]
    public async Task UnrelatedOpaqueMalformedRegionIsPreservedWithoutBeingRebuilt()
    {
        await using var files = new Files(); const string opaque = "BAD opaque | not parsed fields\r\n";
        var doc = files.Memory(Header + Body + opaque); using var edit = files.Edit(doc, "PID-5.2.1");
        Assert.True(edit.TryApply("Rowan"), edit.Status); await edit.RestoreSelectionAsync();
        Assert.EndsWith(opaque, doc.EditableText); Assert.Contains(doc.Message.Diagnostics, d => d.Code == "MalformedSegment");
        doc.Undo(); await doc.EditRefresh; Assert.Equal(Header + Body + opaque, doc.EditableText);
    }
}
