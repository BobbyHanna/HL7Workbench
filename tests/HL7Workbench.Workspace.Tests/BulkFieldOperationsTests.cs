using System.Text;
using HL7Workbench.Core;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed class BulkFieldOperationsTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    internal static string Message(int n, string name = "Avery", string visit = "I") =>
        $"MSH|^~\\&|SYN|TEST|DEST|TEST|20261031000000||ADT^A01|SYN-{n}|T|2.5.1\rPID|1||SYN-A~SYN-B||Example^{name}&Q||\r\nPID|2||SECOND||Other^Rowan&X\nPV1|1|{visit}\r";
    private sealed class Files : IAsyncDisposable
    {
        internal readonly string Root = Path.Combine(Path.GetTempPath(), "hl7-bulk-synthetic-" + Guid.NewGuid().ToString("N"));
        internal readonly DocumentManager Manager = new();
        private readonly List<MessageDocument> owned = [];
        internal Files() { Directory.CreateDirectory(Root); ((System.Collections.Specialized.INotifyCollectionChanged)Manager.Documents).CollectionChanged += (_, _) => { foreach (var doc in Manager.Documents) if (!owned.Contains(doc)) owned.Add(doc); }; }
        internal BulkFieldOperations Service(BulkFieldOptions? options = null) => new((options ?? new()) with { StorageRoot = Path.Combine(Root, "snapshots") });
        internal async Task<MessageDocument> Open(string text, Encoding? encoding = null)
        {
            encoding ??= new UTF8Encoding(false, true);
            using var stream = new MemoryStream(encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray());
            return await MessageFileReader.OpenFileAsync(Manager, "Synthetic.hl7", stream, Token);
        }
        internal MessageDocument Memory(string text, Encoding encoding)
        {
            var model = Hl7Parser.Parse(encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray(), new() { Encoding = encoding });
            var doc = new MessageDocument(Guid.NewGuid().ToString("N"), "Synthetic", model); Manager.Open(doc); return doc;
        }
        public async ValueTask DisposeAsync()
        {
            Manager.Dispose(); await Task.WhenAll(owned.Select(d => d.Disposal));
            try { Directory.Delete(Root, true); } catch (DirectoryNotFoundException) { }
        }
    }
    private static BulkFieldRequest Request(string path = "PID-5.2", BulkFieldOperation operation = BulkFieldOperation.UniqueValues,
        BulkFieldScope scope = BulkFieldScope.ActiveFile, string replacement = "Rowan", string find = "Avery", bool all = false) =>
        new(Hl7Path.Parse(path), operation, scope, find, replacement, all);

    [Theory]
    [InlineData("PID-3", "SYN-A~SYN-B")]
    [InlineData("PID-3[2]", "SYN-B")]
    [InlineData("PID-5.2", "Avery&Q")]
    [InlineData("PID-5.2.1", "Avery")]
    [InlineData("PID[2]-5.2.1", "Rowan")]
    public async Task ExactSelectedLevelAndOccurrenceAreNotExpandedOrPromoted(string path, string expected)
    {
        await using var files = new Files(); var doc = await files.Open(Message(1));
        using var result = await files.Service().AnalyzeAsync(doc, Request(path), token: Token);
        var value = Assert.Single(result.UniquePage(0)); Assert.Equal(expected, value.Raw);
        Assert.Equal(1, value.NodeCount); Assert.Equal(1, value.MessageCount); Assert.False(doc.IsEditMode); Assert.False(doc.IsDirty);
    }
    [Fact]
    public async Task UniqueStatesCountsAndAllOccurrencesUseFullValuesAndStableMessageIds()
    {
        await using var files = new Files();
        var doc = await files.Open(Message(1) + Message(2).Replace("Example^Avery&Q", "Example^", StringComparison.Ordinal) + Message(3).Replace("PID|1||SYN-A~SYN-B||Example^Avery&Q||", "PID|1", StringComparison.Ordinal) + Message(4).Replace("Avery&Q", "\"\"", StringComparison.Ordinal));
        using var result = await files.Service().AnalyzeAsync(doc, Request(all: true), token: Token);
        var rows = result.UniquePage(0);
        Assert.Equal(8, result.Summary.SelectedSlots); Assert.Equal(7, result.Summary.Nodes); Assert.Equal(4, result.Summary.MessagesContainingPath);
        Assert.Equal(1, rows.Single(v => v.State == Hl7ValueState.Missing).Occurrences); Assert.Equal(0, rows.Single(v => v.State == Hl7ValueState.Missing).NodeCount);
        Assert.Equal(1, rows.Single(v => v.State == Hl7ValueState.Empty).NodeCount);
        Assert.Equal("\"\"", rows.Single(v => v.State == Hl7ValueState.ExplicitNull).Raw);
        var repeated = rows.Single(v => v.Raw == "Rowan&X"); Assert.Equal(4, repeated.NodeCount); Assert.Equal(4, repeated.MessageCount);
        result.FilterNavigator(repeated); Assert.Equal(4, doc.NavigationMatchIds!.Count); await result.NavigateAsync(repeated, Token);
        Assert.Equal(1, doc.MessageNumber);
        Assert.False(doc.IsDirty); Assert.False(doc.CanUndo);
    }
    [Fact]
    public async Task SameValueInTwoSegmentsCountsNodesSeparatelyFromMessages()
    {
        await using var files = new Files(); var doc = await files.Open(Message(1).Replace("Rowan&X", "Avery&Q", StringComparison.Ordinal));
        using var result = await files.Service().AnalyzeAsync(doc, Request(all: true), token: Token);
        var row = Assert.Single(result.UniquePage(0)); Assert.Equal(2, row.NodeCount); Assert.Equal(1, row.MessageCount);
    }
    [Fact]
    public async Task IdenticalAbbreviationsDoNotGroupDistinctCompleteValues()
    {
        await using var files = new Files(); var prefix = new string('Z', 300);
        var doc = await files.Open(Message(1, prefix + "A") + Message(2, prefix + "B"));
        using var result = await files.Service().AnalyzeAsync(doc, Request(), token: Token);
        var rows = result.UniquePage(0); Assert.Equal(2, rows.Count); Assert.Equal(rows[0].Preview, rows[1].Preview); Assert.NotEqual(rows[0].Raw, rows[1].Raw);
    }
    [Fact]
    public async Task PaginationAndFrequencySortingUseAllRowsAndCompleteFilterMembership()
    {
        await using var files = new Files(); var text = string.Concat(Enumerable.Range(1, 230).Select(n => Message(n, "VALUE" + (n % 115).ToString("D3"))));
        var doc = await files.Open(text); using var result = await files.Service().AnalyzeAsync(doc, Request(), token: Token);
        Assert.Equal(115, result.RowCount); Assert.Equal(2, result.PageCount); Assert.Equal(100, result.UniquePage(0).Count); Assert.Equal(15, result.UniquePage(1).Count);
        var all = result.UniquePage(0).Concat(result.UniquePage(1)).ToArray(); Assert.Equal(115, all.Select(v => v.Raw).Distinct().Count());
        var last = result.UniquePage(0, BulkFieldSort.Frequency, true)[0]; Assert.Equal(2, last.NodeCount);
        result.FilterNavigator(last); Assert.Equal(2, doc.NavigationMatchIds!.Count);
    }
    [Theory]
    [InlineData("PID-3", "SYN-A~SYN-B", "NEW~SYN-C")]
    [InlineData("PID-3[2]", "SYN-B", "NEW^ID")]
    [InlineData("PID-5.2", "Avery&Q", "Rowan&Y")]
    [InlineData("PID-5.2.1", "Avery", "Rowan")]
    [InlineData("PID[2]-5.2.1", "Rowan", "Second")]
    public async Task SearchReplaceUsesExactSpansAndIndependentCompleteOutput(string path, string find, string replacement)
    {
        await using var files = new Files(); var original = Message(1) + Message(2, "Other"); var doc = await files.Open(original);
        var before = doc.Snapshot!.ReadBytes(new(0, doc.Snapshot.Length), 1024 * 1024);
        using var preview = await files.Service().AnalyzeAsync(doc, Request(path, BulkFieldOperation.SearchReplace, replacement: replacement, find: find), token: Token);
        Assert.True(preview.CanApply); var changed = preview.ChangePage(0).Where(r => r.Status == "Proposed").ToArray(); Assert.NotEmpty(changed);
        await Assert.ThrowsAsync<InvalidOperationException>(() => preview.ApplyAsync(files.Manager, false, token: Token));
        var output = await preview.ApplyAsync(files.Manager, true, token: Token);
        var expected = original.Replace(find, replacement, StringComparison.Ordinal);
        Assert.Equal(Encoding.UTF8.GetBytes(expected), output.Snapshot!.ReadBytes(new(0, output.Snapshot.Length), 1024 * 1024));
        Assert.Equal(before, doc.Snapshot.ReadBytes(new(0, doc.Snapshot.Length), 1024 * 1024)); Assert.Equal(Message(1), doc.EditableText);
        Assert.False(doc.IsDirty); Assert.False(doc.CanUndo); Assert.False(doc.IsEditMode); Assert.True(output.IsDirty); Assert.False(output.CanUndo);
        await Assert.ThrowsAsync<InvalidOperationException>(() => preview.ApplyAsync(files.Manager, true, token: Token));
        var saved = Path.Combine(files.Root, "explicit-result.hl7"); await output.SaveAsync(saved, Token); Assert.Equal(Encoding.UTF8.GetBytes(expected), await File.ReadAllBytesAsync(saved, Token));
    }
    [Theory]
    [InlineData(BulkFieldScope.CurrentMessage, 1)]
    [InlineData(BulkFieldScope.ActiveFile, 3)]
    [InlineData(BulkFieldScope.NavigatorFilter, 2)]
    public async Task ScopeControlsMutationButNeverDropsOutsideMessages(BulkFieldScope scope, int count)
    {
        await using var files = new Files(); var doc = await files.Open(Message(1) + Message(2, visit: "O") + Message(3));
        var search = await new MessageSearch().SearchAsync(doc, new(MessageSearchMode.Field, "I", true, "PV1-2", MessageSearchComparison.Equals), token: Token); doc.ApplySearchFilter(search);
        using var preview = await files.Service().AnalyzeAsync(doc, Request("PV1-2", BulkFieldOperation.Assign, scope, "E"), token: Token);
        Assert.Equal(count, preview.Summary.Replacements); var output = await preview.ApplyAsync(files.Manager, true, token: Token); Assert.Equal(3, output.MessageCount);
        var bytes = output.Snapshot!.ReadBytes(new(0, output.Snapshot.Length), 1024 * 1024); var expected = scope == BulkFieldScope.CurrentMessage ? Message(1, visit: "E") + Message(2, visit: "O") + Message(3) :
            scope == BulkFieldScope.NavigatorFilter ? Message(1, visit: "E") + Message(2, visit: "O") + Message(3, visit: "E") : Message(1, visit: "E") + Message(2, visit: "E") + Message(3, visit: "E");
        Assert.Equal(Encoding.UTF8.GetBytes(expected), bytes);
    }
    [Theory]
    [InlineData("PID-5.2", "A^B")][InlineData("PID-3[2]", "A~B")][InlineData("PID-5.2.1", "A&B")]
    [InlineData("PID-5.2", "unclosed\\X")]
    public async Task UnsafeRawReplacementBlocksAllPublication(string path, string replacement)
    {
        await using var files = new Files(); var doc = await files.Open(Message(1) + Message(2));
        using var preview = await files.Service().AnalyzeAsync(doc, Request(path, BulkFieldOperation.Assign, replacement: replacement), token: Token);
        Assert.True(preview.Summary.Invalid > 0); Assert.False(preview.CanApply);
        await Assert.ThrowsAsync<InvalidOperationException>(() => preview.ApplyAsync(files.Manager, true, token: Token)); Assert.Single(files.Manager.Documents); Assert.False(doc.IsDirty);
    }
    [Theory]
    [InlineData("MSH-1", "X")][InlineData("MSH-2", "X")][InlineData("PID-5.2", "A\rB")][InlineData("PID-5.2", "A\tB")][InlineData("PID-5.2", "A\u2028B")]
    public async Task GlobalUnsafeRequestsAreRejectedBeforeCapture(string path, string replacement)
    {
        await using var files = new Files(); var doc = await files.Open(Message(1));
        if (path.StartsWith("MSH", StringComparison.Ordinal))
            await Assert.ThrowsAsync<InvalidOperationException>(() => files.Service().AnalyzeAsync(doc, Request(path, BulkFieldOperation.Assign, replacement: replacement), token: Token));
        else await Assert.ThrowsAsync<ArgumentException>(() => files.Service().AnalyzeAsync(doc, Request(path, BulkFieldOperation.Assign, replacement: replacement), token: Token));
        Assert.False(doc.IsDirty); Assert.False(doc.CanUndo); Assert.False(Directory.Exists(Path.Combine(files.Root, "snapshots")));
    }
    [Fact]
    public async Task EmptyAbsentUnchangedAndNonmatchingTargetsRemainExplicit()
    {
        await using var files = new Files(); var doc = await files.Open(Message(1) + Message(2, visit: "") + Message(3).Replace("PV1|1|I", "PV1|1", StringComparison.Ordinal));
        using var preview = await files.Service().AnalyzeAsync(doc, Request("PV1-2", BulkFieldOperation.Assign, replacement: "I"), token: Token);
        Assert.Equal(1, preview.Summary.Absent); Assert.Equal(1, preview.Summary.Unchanged); Assert.Equal(1, preview.Summary.Replacements);
        Assert.Contains(preview.ChangePage(0), c => c.Status == "Absent: not created"); Assert.Contains(preview.ChangePage(0), c => c.Status == "Unchanged");
        var output = await preview.ApplyAsync(files.Manager, true, token: Token); Assert.Equal(3, output.MessageCount);
        using var noop = await files.Service().AnalyzeAsync(doc, Request("PV1-2", BulkFieldOperation.SearchReplace, replacement: "I", find: "I"), token: Token);
        Assert.Equal(0, noop.Summary.Replacements); Assert.False(noop.CanApply);
    }
    [Fact]
    public async Task RawEqualityIsCaseSensitiveWholeValueAndDoesNotMatchOtherFields()
    {
        await using var files = new Files(); var doc = await files.Open(Message(1, "avery") + Message(2, "AverySuffix") + Message(3));
        using var preview = await files.Service().AnalyzeAsync(doc, Request("PID-5.2.1", BulkFieldOperation.SearchReplace), token: Token);
        Assert.Equal(1, preview.Summary.Replacements); Assert.Equal(2, preview.Summary.Skipped);
    }
    [Fact]
    public async Task AllOccurrencesAndCustomDelimitersPreserveRawClosedEscapes()
    {
        await using var files = new Files(); var text = Message(1).Replace('|', '*').Replace('^', '$').Replace('~', '%').Replace('\\', '!').Replace('&', '@');
        var doc = await files.Open(text); using var preview = await files.Service().AnalyzeAsync(doc, Request("PID-5.2.1", BulkFieldOperation.Assign, replacement: "SYN!S!VALUE", all: true), token: Token);
        Assert.Equal(2, preview.Summary.Replacements); var output = await preview.ApplyAsync(files.Manager, true, token: Token);
        Assert.Equal(Encoding.UTF8.GetBytes(text.Replace("Avery", "SYN!S!VALUE", StringComparison.Ordinal).Replace("Rowan", "SYN!S!VALUE", StringComparison.Ordinal)), output.Snapshot!.ReadBytes(new(0, output.Snapshot.Length)));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task BomEncodingBatchWrappersAndUntouchedBytesSurvive(bool utf16)
    {
        await using var files = new Files(); Encoding encoding = utf16 ? new UnicodeEncoding(false, true, true) : new UTF8Encoding(true, true);
        var text = "FHS|^~\\&|SYN\rBHS|^~\\&|SYN\r" + Message(1) + Message(2, "Other") + "BTS|2\rFTS|1\r";
        var doc = await files.Open(text, encoding); using var preview = await files.Service().AnalyzeAsync(doc, Request("PID-5.2.1", BulkFieldOperation.SearchReplace), token: Token);
        var output = await preview.ApplyAsync(files.Manager, true, token: Token);
        Assert.Equal(encoding.GetPreamble().Concat(encoding.GetBytes(text.Replace("Avery", "Rowan", StringComparison.Ordinal))), output.Snapshot!.ReadBytes(new(0, output.Snapshot.Length)));
        Assert.Equal(encoding.GetPreamble().Concat(encoding.GetBytes(text)), doc.Snapshot!.ReadBytes(new(0, doc.Snapshot.Length)));
    }
    [Fact]
    public async Task OriginalSingleByteEncodingIsStrictAndNeverTranscoded()
    {
        await using var files = new Files(); var doc = files.Memory(Message(1), Encoding.Latin1);
        using var invalid = await files.Service().AnalyzeAsync(doc, Request("PID-5.2.1", BulkFieldOperation.Assign, replacement: "Ω"), token: Token);
        Assert.False(invalid.CanApply); Assert.Equal(1, invalid.Summary.Invalid);
        using var valid = await files.Service().AnalyzeAsync(doc, Request("PID-5.2.1", BulkFieldOperation.Assign, replacement: "René"), token: Token);
        var output = await valid.ApplyAsync(files.Manager, true, token: Token); Assert.Equal(28591, output.Index!.Encoding.CodePage);
        Assert.Equal(Encoding.Latin1.GetBytes(Message(1, "René")), output.Snapshot!.ReadBytes(new(0, output.Snapshot.Length)));
    }
    [Theory]
    [InlineData("edit")][InlineData("filter")][InlineData("close")]
    public async Task StalePreviewNeverRetargetsAndReleasesCapture(string change)
    {
        await using var files = new Files(); var doc = await files.Open(Message(1));
        var matches = await new MessageSearch().SearchAsync(doc, new(MessageSearchMode.Field, "I", true, "PV1-2", MessageSearchComparison.Equals), token: Token); doc.ApplySearchFilter(matches);
        var preview = await files.Service().AnalyzeAsync(doc, Request("PV1-2", BulkFieldOperation.Assign, BulkFieldScope.NavigatorFilter, "O"), token: Token);
        if (change == "edit") { doc.IsEditMode = true; doc.ReplaceWorkingText(Message(1, "Raw")); }
        else if (change == "filter") doc.ClearSearchFilter();
        else files.Manager.Close(doc.Id, true);
        Assert.False(preview.IsCurrent); await Assert.ThrowsAsync<InvalidOperationException>(() => preview.ApplyAsync(files.Manager, true, token: Token));
        preview.Dispose(); await preview.Disposal; Assert.Empty(Directory.GetDirectories(Path.Combine(files.Root, "snapshots")));
    }
    [Fact]
    public async Task SwitchingActiveDocumentsKeepsPreviewTiedToCapturedSource()
    {
        await using var files = new Files(); var source = await files.Open(Message(1));
        using var preview = await files.Service().AnalyzeAsync(source, Request("PV1-2", BulkFieldOperation.Assign, replacement: "E"), token: Token);
        var other = await files.Open(Message(2, visit: "O")); Assert.True(preview.IsCurrent);
        var output = await preview.ApplyAsync(files.Manager, true, token: Token); Assert.Equal(Message(1, visit: "E"), output.EditableText); Assert.Equal(Message(2, visit: "O"), other.EditableText);
    }
    [Theory]
    [InlineData("distinct")][InlineData("value")][InlineData("retained")][InlineData("nodes")][InlineData("messages")][InlineData("source")]
    public async Task BudgetFailuresDoNotRetainPartialAnalysisOrLeases(string budget)
    {
        await using var files = new Files(); var doc = await files.Open(Message(1) + Message(2, "Other"));
        var options = budget switch { "distinct" => new BulkFieldOptions { MaximumDistinctValues = 1 }, "value" => new() { MaximumValueCharacters = 2 },
            "retained" => new() { MaximumRetainedValueCharacters = 1 }, "nodes" => new() { MaximumNodes = 1 }, "messages" => new() { MaximumMessages = 1 }, _ => new() { MaximumSourceBytes = 1 } };
        if (budget == "source") await Assert.ThrowsAsync<HL7Workbench.Scripting.ScriptRunException>(() => files.Service(options).AnalyzeAsync(doc, Request(), token: Token));
        else await Assert.ThrowsAsync<Hl7LimitException>(() => files.Service(options).AnalyzeAsync(doc, Request(), token: Token));
        Assert.False(doc.IsDirty); Assert.False(doc.IsCapturingScript);
        if (Directory.Exists(Path.Combine(files.Root, "snapshots"))) Assert.Empty(Directory.GetDirectories(Path.Combine(files.Root, "snapshots")));
    }
    private sealed class ProgressReporter(Action<BulkFieldProgress> action) : IProgress<BulkFieldProgress> { public void Report(BulkFieldProgress value) => action(value); }
    [Fact]
    public async Task CancellationDuringScanAndBeforePublicationLeavesNoOutputOrTemporaryCapture()
    {
        await using var files = new Files(); var doc = await files.Open(Message(1) + Message(2)); using var cancellation = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => files.Service().AnalyzeAsync(doc, Request(), new ProgressReporter(p => { if (p.Phase == "Analyzing") cancellation.Cancel(); }), cancellation.Token));
        Assert.Empty(Directory.GetDirectories(Path.Combine(files.Root, "snapshots")));
        using var preview = await files.Service().AnalyzeAsync(doc, Request("PV1-2", BulkFieldOperation.Assign, replacement: "E"), token: Token);
        using var beforePublish = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => preview.ApplyAsync(files.Manager, true, new ProgressReporter(_ => beforePublish.Cancel()), beforePublish.Token));
        Assert.Single(files.Manager.Documents); Assert.True(preview.CanApply); Assert.False(doc.IsDirty);
        var output = await preview.ApplyAsync(files.Manager, true, token: Token); Assert.Equal(2, output.MessageCount);
    }
    [Fact]
    public async Task DisposalCancelsAndDrainsActivePublicationBeforeReleasingSnapshot()
    {
        await using var files = new Files(); var doc = await files.Open(Message(1));
        var preview = await files.Service().AnalyzeAsync(doc, Request("PV1-2", BulkFieldOperation.Assign, replacement: "E"), token: Token);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var apply = preview.ApplyAsync(files.Manager, true, new ProgressReporter(_ => { entered.TrySetResult(); release.Task.GetAwaiter().GetResult(); }), Token);
        await entered.Task; preview.Dispose(); Assert.False(preview.Disposal.IsCompleted); release.SetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => apply); await preview.Disposal;
        Assert.Empty(Directory.GetDirectories(Path.Combine(files.Root, "snapshots"))); Assert.Single(files.Manager.Documents);
    }
    [Fact]
    public async Task OutputLimitFailurePublishesNothingAndRetainsRetryablePreview()
    {
        await using var files = new Files(); var doc = await files.Open(Message(1));
        using var preview = await files.Service(new() { MaximumOutputBytes = 1 }).AnalyzeAsync(doc, Request("PV1-2", BulkFieldOperation.Assign, replacement: "O"), token: Token);
        await Assert.ThrowsAsync<Hl7LimitException>(() => preview.ApplyAsync(files.Manager, true, token: Token)); Assert.Single(files.Manager.Documents); Assert.True(preview.CanApply);
        Assert.Single(Directory.GetDirectories(Path.Combine(files.Root, "snapshots")));
    }
    [Fact]
    public async Task LargeSourceAnalysisIsStreamedAndOversizedMutationRemainsBounded()
    {
        await using var files = new Files(); var text = Message(1) + "OBX|1|TX|||" + new string('X', MessagePresentation.MaximumEditorCharacters + 1) + "\r";
        var doc = await files.Open(text); Assert.NotNull(doc.Source); var originalSource = doc.Source;
        using var values = await files.Service().AnalyzeAsync(doc, Request("PV1-2"), token: Token); Assert.Equal("I", Assert.Single(values.UniquePage(0)).Raw);
        using var preview = await files.Service().AnalyzeAsync(doc, Request("PV1-2", BulkFieldOperation.Assign, replacement: "O"), token: Token);
        Assert.False(preview.CanApply); Assert.True(preview.Summary.Invalid > 0); Assert.Same(originalSource, doc.Source); Assert.Null(doc.MaterializedMessage); Assert.Null(doc.WorkingText);
    }
    [Fact]
    public void OverlappingSpansAreRejectedBeforeAnyEdit()
    {
        Assert.Throws<InvalidOperationException>(() => BulkFieldOperations.ValidateSpans([new(2, 5), new(4, 1)]));
        Assert.Throws<InvalidOperationException>(() => BulkFieldOperations.ValidateSpans([new(2, 0), new(2, 0)]));
        BulkFieldOperations.ValidateSpans([new(2, 2), new(4, 0), new(5, 2)]);
    }
    [Fact]
    public async Task CompleteFilteredScopeAndPreviewPaginationIncludeMatchesBeyondVisiblePages()
    {
        await using var files = new Files(); var doc = await files.Open(string.Concat(Enumerable.Range(1, 250).Select(n => Message(n))));
        var matches = await new MessageSearch().SearchAsync(doc, new(MessageSearchMode.Field, "I", true, "PV1-2", MessageSearchComparison.Equals), token: Token);
        Assert.Equal(2, matches.PageCount); doc.ApplySearchFilter(matches);
        using var preview = await files.Service().AnalyzeAsync(doc, Request("PV1-2", BulkFieldOperation.Assign, BulkFieldScope.NavigatorFilter, "O"), token: Token);
        Assert.Equal(250, preview.Summary.Replacements); Assert.Equal(3, preview.PageCount); Assert.Equal(50, preview.ChangePage(2).Count);
        Assert.Equal(250, preview.ChangePage(2).Last().MessageNumber);
        var output = await preview.ApplyAsync(files.Manager, true, token: Token); await output.NavigateAsync(250, Token); Assert.Contains("PV1|1|O", output.EditableText);
    }
    [Fact]
    public async Task CurrentDraftsAndInsertedMessagesAreCapturedWithoutChangingOriginalHistory()
    {
        await using var files = new Files(); var doc = await files.Open(Message(1) + Message(2)); doc.IsEditMode = true;
        doc.ReplaceWorkingText(Message(1, "Draft")); await doc.EditRefresh;
        doc.InsertMessage(MessageInsertionPosition.End, Message(3, "Inserted")); await doc.EditRefresh;
        var originalRevision = doc.SearchRevision; var originalUndo = doc.CanUndo;
        using var preview = await files.Service().AnalyzeAsync(doc, Request("PV1-2", BulkFieldOperation.Assign, replacement: "O"), token: Token);
        var output = await preview.ApplyAsync(files.Manager, true, token: Token); Assert.Equal(3, output.MessageCount);
        await output.NavigateAsync(1, Token); Assert.Contains("Example^Draft", output.EditableText);
        await output.NavigateAsync(3, Token); Assert.Contains("Example^Inserted", output.EditableText);
        Assert.Equal(originalRevision, doc.SearchRevision); Assert.Equal(originalUndo, doc.CanUndo); Assert.True(doc.IsDirty);
        doc.Undo(); await doc.EditRefresh; Assert.Equal(2, doc.MessageCount); Assert.True(doc.CanUndo);
        doc.Undo(); await doc.EditRefresh; Assert.Equal(Message(1), doc.EditableText); Assert.False(doc.CanUndo);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task MllpOrOpaqueRegionsRemainExact(bool opaque)
    {
        await using var files = new Files();
        var text = opaque ? Message(1) + "BAD opaque | not parsed fields\r\n" + Message(2, "Other") : "\v" + Message(1) + "\x1c\r\v" + Message(2, "Other") + "\x1c\r";
        var doc = await files.Open(text); using var preview = await files.Service().AnalyzeAsync(doc, Request("PID-5.2.1", BulkFieldOperation.SearchReplace), token: Token);
        var output = await preview.ApplyAsync(files.Manager, true, token: Token);
        Assert.Equal(Encoding.UTF8.GetBytes(text.Replace("Avery", "Rowan", StringComparison.Ordinal)), output.Snapshot!.ReadBytes(new(0, output.Snapshot.Length)));
    }
    [Fact]
    public async Task AmbiguousTargetsAreSkippedAndBlockMutationInsteadOfBecomingAbsent()
    {
        await using var files = new Files(); var doc = await files.Open(Message(1).Replace("^~\\&", "^~^&", StringComparison.Ordinal));
        using var unique = await files.Service().AnalyzeAsync(doc, Request(), token: Token); Assert.Equal(0, unique.RowCount); Assert.Equal(1, unique.Summary.Skipped); Assert.Equal(0, unique.Summary.Absent);
        using var preview = await files.Service().AnalyzeAsync(doc, Request(operation: BulkFieldOperation.Assign), token: Token);
        Assert.Equal(1, preview.Summary.Invalid); Assert.False(preview.CanApply); Assert.Contains("ambiguous", Assert.Single(preview.ChangePage(0)).Status);
    }
    [Fact]
    public async Task UnsupportedEditEncodingRemainsAnalyzableButCannotBeMutated()
    {
        await using var files = new Files(); var doc = await files.Open(Message(1), new UTF32Encoding(false, true, true));
        using var unique = await files.Service().AnalyzeAsync(doc, Request(), token: Token); Assert.Equal("Avery&Q", Assert.Single(unique.UniquePage(0)).Raw);
        using var preview = await files.Service().AnalyzeAsync(doc, Request(operation: BulkFieldOperation.Assign), token: Token); Assert.False(preview.CanApply); Assert.True(preview.Summary.Invalid > 0);
    }
    [Fact]
    public async Task BigEndianUtf16AndExplicitNullRemainExact()
    {
        await using var files = new Files(); var encoding = new UnicodeEncoding(true, true, true); var doc = await files.Open(Message(1), encoding);
        using var preview = await files.Service().AnalyzeAsync(doc, Request("PV1-2", BulkFieldOperation.Assign, replacement: "\"\""), token: Token);
        var output = await preview.ApplyAsync(files.Manager, true, token: Token);
        Assert.Equal(encoding.GetPreamble().Concat(encoding.GetBytes(Message(1, visit: "\"\""))), output.Snapshot!.ReadBytes(new(0, output.Snapshot.Length)));
        using var analysis = await files.Service().AnalyzeAsync(output, Request("PV1-2"), token: Token); Assert.Equal(Hl7ValueState.ExplicitNull, Assert.Single(analysis.UniquePage(0)).State);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task EditOrCloseDuringPublicationCancelsAndCannotOpenPartialOutput(bool close)
    {
        await using var files = new Files(); var doc = await files.Open(Message(1)); doc.IsEditMode = true;
        using var preview = await files.Service().AnalyzeAsync(doc, Request("PV1-2", BulkFieldOperation.Assign, replacement: "O"), token: Token);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var applying = preview.ApplyAsync(files.Manager, true, new ProgressReporter(_ => { entered.TrySetResult(); release.Task.GetAwaiter().GetResult(); }), Token);
        await entered.Task;
        if (close) files.Manager.Close(doc.Id, true); else doc.ReplaceWorkingText(Message(1, "Draft"));
        release.SetResult(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => applying);
        Assert.Equal(close ? 0 : 1, files.Manager.Documents.Count);
    }
    [Fact]
    public async Task PreCanceledAnalysisAlwaysReleasesBorrowedReadLease()
    {
        await using var files = new Files(); var doc = await files.Open(Message(1)); using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => files.Service().AnalyzeAsync(doc, Request(), token: cancellation.Token));
        files.Manager.Close(doc.Id, true); await doc.Disposal.WaitAsync(Token); Assert.False(doc.IsCapturingScript);
    }
    [Fact]
    public async Task ChangedSourceDuringAnalysisCancelsWithoutRetainedCapture()
    {
        await using var files = new Files(); var doc = await files.Open(Message(1) + Message(2)); doc.IsEditMode = true;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var analysis = files.Service().AnalyzeAsync(doc, Request(), new ProgressReporter(p => { if (p.Phase == "Analyzing") { entered.TrySetResult(); release.Task.GetAwaiter().GetResult(); } }), Token);
        await entered.Task; doc.ReplaceWorkingText(Message(1, "Draft")); release.SetResult(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => analysis);
        Assert.Empty(Directory.GetDirectories(Path.Combine(files.Root, "snapshots")));
    }
    [Fact]
    public async Task OverlayBudgetFailureIsVisibleAndBlocksAllProposals()
    {
        await using var files = new Files(); var doc = await files.Open(Message(1) + Message(2));
        using var preview = await files.Service(new() { MaximumOverlayCharacters = 1 }).AnalyzeAsync(doc, Request("PV1-2", BulkFieldOperation.Assign, replacement: "O"), token: Token);
        Assert.Equal(2, preview.Summary.Invalid); Assert.False(preview.CanApply); Assert.All(preview.ChangePage(0), row => Assert.StartsWith("Invalid", row.Status));
    }
    [Fact]
    public async Task LargeOutOfScopeMessageIsCopiedWithoutBeingMaterializedForMutation()
    {
        await using var files = new Files(); var large = Message(1) + "OBX|1|TX|||" + new string('X', MessagePresentation.MaximumEditorCharacters + 1) + "\r";
        var text = large + Message(2); var doc = await files.Open(text); Assert.NotNull(doc.Source); await doc.NavigateAsync(2, Token);
        using var preview = await files.Service().AnalyzeAsync(doc, Request("PV1-2", BulkFieldOperation.Assign, BulkFieldScope.CurrentMessage, "O"), token: Token);
        Assert.Equal(1, preview.Summary.ScannedMessages); Assert.True(preview.CanApply);
        var output = await preview.ApplyAsync(files.Manager, true, token: Token);
        Assert.Equal(Encoding.UTF8.GetBytes(large + Message(2, visit: "O")), output.Snapshot!.ReadBytes(new(0, output.Snapshot.Length), 3 * 1024 * 1024));
        Assert.Equal(Encoding.UTF8.GetBytes(text), doc.Snapshot!.ReadBytes(new(0, doc.Snapshot.Length), 3 * 1024 * 1024)); Assert.False(doc.IsDirty);
    }
    [Theory]
    [InlineData("registration")] [InlineData("activation")] [InlineData("presentation")]
    public async Task FailedPublicationWithdrawsOwnedDocumentDrainsCleanupAndRetainsRetryablePreview(string phase)
    {
        await using var files = new Files(); var text = string.Concat(Enumerable.Range(1, 30).Select(n => Message(n, "Avery").Replace("Avery&Q", "Avery", StringComparison.Ordinal)));
        var source = await files.Open(text); var before = source.Snapshot!.ReadBytes(new(0, source.Snapshot.Length));
        using var preview = await files.Service().AnalyzeAsync(source, Request("PID-5.2", BulkFieldOperation.SearchReplace, replacement: "Morgan"), token: Token);
        MessageDocument? attempted = null;
        void FailRegistration(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (phase != "registration" || e.Action != System.Collections.Specialized.NotifyCollectionChangedAction.Add) return;
            attempted = Assert.Single(e.NewItems!.Cast<MessageDocument>());
            throw new InvalidOperationException("Synthetic registration observer failure.");
        }
        void FailActivation(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (phase != "activation" || e.PropertyName != nameof(DocumentManager.ActiveDocument) || ReferenceEquals(files.Manager.ActiveDocument, source)) return;
            attempted = files.Manager.ActiveDocument;
            throw new InvalidOperationException("Synthetic activation observer failure.");
        }
        var notifications = (System.Collections.Specialized.INotifyCollectionChanged)files.Manager.Documents;
        notifications.CollectionChanged += FailRegistration; files.Manager.PropertyChanged += FailActivation;
        try
        {
            await Assert.ThrowsAsync<BulkFieldPublicationException>(() => preview.ApplyAsync(files.Manager, true, token: Token, presentResult: result =>
            {
                Assert.Equal(30, result.MessageCount); Assert.Equal("Morgan", result.ActiveInspection!.SelectedNode!.Raw);
                if (phase == "presentation") { attempted = result; throw new InvalidOperationException("Synthetic presentation failure."); }
            }));
        }
        finally { notifications.CollectionChanged -= FailRegistration; files.Manager.PropertyChanged -= FailActivation; }
        Assert.NotNull(attempted); Assert.True(attempted.Disposal.IsCompletedSuccessfully);
        Assert.Single(files.Manager.Documents); Assert.Same(source, files.Manager.ActiveDocument); Assert.True(preview.CanApply);
        Assert.Equal(before, source.Snapshot.ReadBytes(new(0, source.Snapshot.Length))); Assert.False(source.IsDirty); Assert.False(source.CanUndo);
        var output = await preview.ApplyAsync(files.Manager, true, token: Token);
        Assert.Equal(2, files.Manager.Documents.Count); Assert.Same(output, files.Manager.ActiveDocument); Assert.Equal(30, output.MessageCount);
        Assert.Equal(Encoding.UTF8.GetBytes(text.Replace("Example^Avery", "Example^Morgan", StringComparison.Ordinal)), output.Snapshot!.ReadBytes(new(0, output.Snapshot.Length)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => preview.ApplyAsync(files.Manager, true, token: Token));
        Assert.Equal(2, files.Manager.Documents.Count);
    }
    private sealed class AliasedSingleByteEncoding() : Encoding(28591)
    {
        public override bool IsSingleByte => true;
        public override Decoder GetDecoder() => new AliasDecoder();
        private sealed class AliasDecoder : Decoder
        {
            public override int GetCharCount(byte[] bytes, int index, int count) => count;
            public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
            { for (var i = 0; i < byteCount; i++) chars[charIndex + i] = bytes[byteIndex + i] == 0x80 ? 'A' : (char)bytes[byteIndex + i]; return byteCount; }
            public override void Convert(ReadOnlySpan<byte> bytes, Span<char> chars, bool flush, out int bytesUsed, out int charsUsed, out bool completed)
            {
                bytesUsed = charsUsed = Math.Min(bytes.Length, chars.Length); completed = bytesUsed == bytes.Length;
                for (var i = 0; i < bytesUsed; i++) chars[i] = bytes[i] == 0x80 ? 'A' : (char)bytes[i];
            }
        }
        public override int GetByteCount(char[] chars, int index, int count)
        { for (var i = index; i < index + count; i++) if (chars[i] > 255) throw new EncoderFallbackException(); return count; }
        public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex)
        { _ = GetByteCount(chars, charIndex, charCount); for (var i = 0; i < charCount; i++) bytes[byteIndex + i] = (byte)chars[charIndex + i]; return charCount; }
        public override int GetCharCount(byte[] bytes, int index, int count) => count;
        public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
        { for (var i = 0; i < byteCount; i++) chars[charIndex + i] = bytes[byteIndex + i] == 0x80 ? 'A' : (char)bytes[byteIndex + i]; return byteCount; }
        public override int GetMaxByteCount(int charCount) => charCount;
        public override int GetMaxCharCount(int byteCount) => byteCount;
    }
    [Fact]
    public async Task MultipleControlledEditsPreserveUntouchedByteAliasesBetweenThem()
    {
        await using var files = new Files(); var encoding = new AliasedSingleByteEncoding();
        var text = Message(1).Replace("PID|2", "NTE|1||PLACEHOLDER\rPID|2", StringComparison.Ordinal);
        var original = Encoding.Latin1.GetBytes(text); original[text.IndexOf("PLACEHOLDER", StringComparison.Ordinal)] = 0x80;
        var doc = new MessageDocument("alias-source", "Synthetic", Hl7Parser.Parse(original, new() { Encoding = encoding })); files.Manager.Open(doc);
        using var preview = await files.Service().AnalyzeAsync(doc, Request("PID-5.2.1", BulkFieldOperation.Assign, replacement: "Edited", all: true), token: Token);
        Assert.Equal(2, preview.Summary.Replacements); var output = await preview.ApplyAsync(files.Manager, true, token: Token);
        var expectedText = text.Replace("Avery", "Edited", StringComparison.Ordinal).Replace("Rowan", "Edited", StringComparison.Ordinal);
        var expected = Encoding.Latin1.GetBytes(expectedText); expected[expectedText.IndexOf("PLACEHOLDER", StringComparison.Ordinal)] = 0x80;
        Assert.Equal(expected, output.Snapshot!.ReadBytes(new(0, output.Snapshot.Length)));
        Assert.Equal(original, Hl7Serializer.Serialize(doc.Message)); Assert.False(doc.IsDirty); Assert.False(doc.CanUndo);
    }
}
