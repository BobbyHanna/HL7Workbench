using System.Diagnostics;
using System.Globalization;
using System.Text;
using HL7Workbench.Core;
using HL7Workbench.Querying;

namespace HL7Workbench.Workspace;

/// <summary>Read-only Workspace SQL workflow over captured logical order and owned bytes. Contains no Dock or SQLite types.</summary>
public sealed class SqlQuery(DocumentManager documents, SqlQueryOptions? options = null)
{
    private readonly SqlQueryOptions options = options ?? new();
    /// <summary>Runs a fresh complete identity-selection query under extraction's own message/output bounds.</summary>
    public async Task<MessageDocument?> OpenMatchingMessagesAsync(string identitySql, IReadOnlyList<MessageDocument>? selectedDocuments = null,
        IReadOnlyList<QueryParameter>? parameters = null, IProgress<SqlQueryProgress>? progress = null, CancellationToken token = default)
    {
        using var result = await RunAsync(identitySql, selectedDocuments, parameters, progress, token);
        return await result.OpenMatchingMessagesAsync(documents, token);
    }
    /// <summary>Captures the selected open documents (or active document), then indexes and executes off the caller thread.
    /// Capture starts on the document-owning context. Successful results own independent snapshots until disposed.</summary>
    public async Task<SqlQueryResults> RunAsync(string sql, IReadOnlyList<MessageDocument>? selectedDocuments = null,
        IReadOnlyList<QueryParameter>? parameters = null, IProgress<SqlQueryProgress>? progress = null, CancellationToken token = default)
    {
        options.Validate(); token.ThrowIfCancellationRequested();
        var chosen = selectedDocuments?.ToArray() ?? (documents.ActiveDocument is { } active ? [active] : []);
        if (chosen.Length is < 1 || chosen.Length > options.MaximumDocuments || chosen.Distinct().Count() != chosen.Length || chosen.Any(d => !documents.Documents.Contains(d)))
            throw new QueryException(QueryError.SourceUnavailable);
        // Capture in document order, independent of caller checkbox order or later tab activation.
        chosen = documents.Documents.Where(chosen.Contains).ToArray();
        if (chosen.Sum(d => (long)d.MessageCount) > 100_000) throw new QueryException(QueryError.ImportLimit);
        var captures = new List<MessageDocument.SearchCapture>();
        try
        {
            foreach (var document in chosen) captures.Add(document.CaptureSearch());
            if (captures.Sum(c => c.Entries.Sum(e => (long)(e.Text?.Length ?? 0))) > MessageDocument.MaximumRetainedEditCharacters)
                throw new QueryException(QueryError.ImportLimit);
            var memory = chosen.Select(d => d.SqlMemoryOriginalBytes).ToArray();
            var guards = chosen.Select(d => d.ExtractionDestinations()).ToArray();
            var args = parameters?.ToArray() ?? [];
            using var total = CancellationTokenSource.CreateLinkedTokenSource(token); total.CancelAfter(options.TotalTimeout);
            return await Task.Run(async () =>
            {
                var watch = Stopwatch.StartNew(); SqlCapturedSources? sources = null;
                try
                {
                    using (var captureToken = CancellationTokenSource.CreateLinkedTokenSource([total.Token, .. captures.SelectMany(c => new[] { c.Changes, c.Lifetime })]))
                        sources = await SqlCapturedSources.CreateAsync(captures.ToArray(), memory, guards, options, progress, captureToken.Token);
                    // Captured bytes/drafts are now independent. Close/edit can no longer retarget this run.
                    foreach (var capture in captures) capture.Dispose();
                    await using var worker = await QueryWorkerSession.StartAsync(options.WorkerPath, options.Budgets, options.Worker, total.Token, sources.ReadAsync,
                        sources.Documents.Count == 1 ? sources.Documents[0].Id : null);
                    var plan = await worker.PlanAsync(sql, args, total.Token);
                    var imported = await PopulateAsync(worker, sources, plan, progress, total.Token);
                    sources.ResetReader();
                    progress?.Report(new("Executing", 0, 1));
                    var result = await worker.ExecuteAsync(sql, args, total.Token);
                    var metrics = new SqlQueryMetrics(sources.HeaderCharacters, sources.BodyDecodedCharacters, sources.ValueRequests, sources.StructuredMessages, imported);
                    var output = await SqlQueryResults.CreateAsync(result, sources, options, metrics, watch.Elapsed, total.Token);
                    sources = null; progress?.Report(new("Completed", result.Rows.Count, result.Rows.Count)); return output;
                }
                catch (Hl7LimitException) { throw new QueryException(QueryError.ImportLimit); }
                catch (DecoderFallbackException) { throw new QueryException(QueryError.SourceUnavailable); }
                finally { sources?.Dispose(); }
            }, total.Token);
        }
        finally { foreach (var capture in captures) capture.Dispose(); }
    }
    private static QueryImportRow Row(QueryTable table, params object?[] values) => new(table, values.Select(value => value switch
    {
        null => QueryCell.Null, string s => QueryCell.From(s), int i => QueryCell.From((long)i), long n => QueryCell.From(n),
        _ => throw new QueryException(QueryError.InvalidRequest)
    }).ToArray());

    private static async Task<int> PopulateAsync(QueryWorkerSession worker, SqlCapturedSources sources, QueryPlan plan,
        IProgress<SqlQueryProgress>? progress, CancellationToken token)
    {
        var batch = new List<QueryImportRow>(); var bytes = 0; var imported = 0; var examined = 0;
        var total = sources.Documents.Sum(d => d.Messages.Length);
        async Task Flush()
        {
            if (batch.Count == 0) return;
            await worker.ImportAsync(batch, token); imported += batch.Count; batch.Clear(); bytes = 0;
        }
        async Task Add(QueryImportRow row)
        {
            // Small metadata batches stay below JSON's encoded frame cap, including escaping.
            var size = row.Values.Sum(v => v.Text?.Length ?? 16);
            if (batch.Count == 100 || bytes + size > 128 * 1024) await Flush();
            batch.Add(row); bytes += size;
        }
        var fields = plan.Tables.Any(t => t is QueryTable.Fields or QueryTable.Components or QueryTable.Subcomponents);
        var segments = fields || plan.Tables.Contains(QueryTable.Segments);
        var messages = plan.Tables.Any(t => t != QueryTable.Documents);
        foreach (var document in sources.Documents)
        {
            if (messages || plan.Tables.Contains(QueryTable.Documents)) await Add(Row(QueryTable.Documents, document.Id, document.Number, document.Revision));
            if (!messages) continue;
            foreach (var message in document.Messages)
            {
                token.ThrowIfCancellationRequested();
                (IHl7TextSource Text, Hl7Structure Structure)? opened = null; var status = "HeaderOnly"; long? diagnostic = message.IndexDiagnostics ? 1 : null;
                string? Header(string path)
                {
                    if (opened is not { } data) return null;
                    var match = Hl7FieldSelection.Parse(path).Select(data.Structure, false, token: token).Matches[0];
                    var value = match.Read(data.Text, data.Structure, document.Encoding);
                    if (value.Status != Hl7ExtractionStatus.Complete) { diagnostic = 1; return null; }
                    return value.State is Hl7ValueState.Missing or Hl7ValueState.ExplicitNull ? null : value.Text;
                }
                try
                {
                    opened = sources.Open(document, message, segments, token);
                    if (opened.Value.Structure.Diagnostics.Count != 0) diagnostic = 1;
                    if (segments) { status = "Complete"; diagnostic ??= 0; }
                }
                catch (DecoderFallbackException) { status = "Unreadable"; diagnostic = 1; if (segments) throw new QueryException(QueryError.SourceUnavailable); }
                catch (Hl7LimitException) { status = "HeaderLimit"; diagnostic = 1; if (segments) throw; }
                var metadata = new object?[] { document.Id, message.Id, message.Number, Header("MSH-9.1"), Header("MSH-9.2"), Header("MSH-10"),
                    Header("MSH-12"), Header("MSH-3"), Header("MSH-4"), Header("MSH-5"), Header("MSH-6"), Header("MSH-7"), null, status };
                metadata[12] = diagnostic;
                await Add(Row(QueryTable.Messages, metadata));
                if (segments && opened is { } data)
                {
                    var occurrences = new Dictionary<string, int>(StringComparer.Ordinal); var ordinal = 0;
                    foreach (var segment in data.Structure.Segments)
                    {
                        token.ThrowIfCancellationRequested(); ordinal++;
                        var occurrence = occurrences.GetValueOrDefault(segment.Identifier) + 1; occurrences[segment.Identifier] = occurrence;
                        await Add(Row(QueryTable.Segments, document.Id, message.Id, ordinal, segment.Identifier, occurrence, ordinal, segment.CanEdit ? "Complete" : "Unstructured"));
                        if (!fields) continue;
                        foreach (var field in segment.Fields)
                        foreach (var repetition in field.Children.Count == 0 ? new[] { field } : field.Children)
                        {
                            var rep = field.Children.Count == 0 ? 1 : repetition.Number;
                            object?[] coordinates = [document.Id, message.Id, ordinal, segment.Identifier, occurrence, field.Number, rep];
                            object?[] Values(StructureNode node) => [null, null, node.State.ToString(), segment.CanEdit ? "NotMaterialized" : "Unstructured", node.Range.Length];
                            if (plan.Tables.Contains(QueryTable.Fields)) await Add(Row(QueryTable.Fields, [.. coordinates, .. Values(repetition)]));
                            if (plan.Tables.Contains(QueryTable.Components) || plan.Tables.Contains(QueryTable.Subcomponents))
                            foreach (var component in repetition.Children)
                            {
                                if (plan.Tables.Contains(QueryTable.Components)) await Add(Row(QueryTable.Components, [.. coordinates, component.Number, .. Values(component)]));
                                if (plan.Tables.Contains(QueryTable.Subcomponents))
                                    foreach (var sub in component.Children) await Add(Row(QueryTable.Subcomponents, [.. coordinates, component.Number, sub.Number, .. Values(sub)]));
                            }
                        }
                    }
                }
                examined++; if (examined == 1 || examined % 25 == 0 || examined == total) progress?.Report(new("Indexing", examined, total));
            }
        }
        await Flush(); return imported;
    }
}
