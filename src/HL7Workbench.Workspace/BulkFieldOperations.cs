using System.Diagnostics;
using System.Text;
using HL7Workbench.Core;

namespace HL7Workbench.Workspace;

/// <summary>Inspector workflow coordination over the existing capture, Core scanner/edit and save-plan services.</summary>
public sealed class BulkFieldOperations(BulkFieldOptions? options = null)
{
    private readonly BulkFieldOptions options = options ?? new();
    public async Task<BulkFieldResults> AnalyzeAsync(MessageDocument document, BulkFieldRequest request,
        IProgress<BulkFieldProgress>? progress = null, CancellationToken token = default)
    {
        options.Validate(); request.Validate();
        using var source = document.CaptureSearch();
        var filter = request.Scope == BulkFieldScope.NavigatorFilter ? document.NavigationMatchIds : null;
        if (request.Scope == BulkFieldScope.NavigatorFilter && filter is null)
            throw new InvalidOperationException("Apply a current navigator filter before selecting filtered scope.");
        var selected = request.Scope switch
        {
            BulkFieldScope.CurrentMessage => source.Entries.Where(e => e.Entry.Id == document.ActiveMessageId).Select(e => e.Entry.Id).ToHashSet(),
            BulkFieldScope.NavigatorFilter => filter!.ToHashSet(),
            _ => source.Entries.Select(e => e.Entry.Id).ToHashSet()
        };
        if (selected.Count == 0) throw new InvalidOperationException("The selected scope contains no messages.");
        if (selected.Count > options.MaximumMessages) throw new Hl7LimitException("BulkMessages");
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, source.Lifetime, source.Changes);
        MessageDocument.ScriptInputCapture? captured = null;
        try
        {
            // This is the existing validated byte capture only. No worker or script runs.
            // Whole capture retains every current draft and every out-of-scope message.
            progress?.Report(new("Capturing complete dataset", 0, source.Count));
            captured = await document.CaptureScriptAsync(options.MaximumSourceBytes, options.StorageRoot, null, source.Revision, linked.Token);
            var owned = captured;
            var result = await Task.Run(() => Scan(document, request, source.Revision, filter, selected, owned, progress, source.Lifetime, source.Changes, linked.Token));
            captured = null;
            return result;
        }
        finally { captured?.Dispose(); }
    }

    private BulkFieldResults Scan(MessageDocument document, BulkFieldRequest request, long revision, IReadOnlyList<MessageId>? filter,
        HashSet<MessageId> selected, MessageDocument.ScriptInputCapture captured, IProgress<BulkFieldProgress>? progress,
        CancellationToken lifetime, CancellationToken changesToken, CancellationToken token)
    {
        var encoding = captured.Index.Encoding;
        var values = new Dictionary<(Hl7ValueState State, string Raw), BulkUniqueValue>();
        var changes = new List<BulkFieldChange>(); var overlays = new Dictionary<int, byte[]>();
        int scanned = 0, containing = 0, nodes = 0, presentNodes = 0, absent = 0, skipped = 0, unchanged = 0, invalid = 0, replacements = 0, affected = 0;
        long retained = request.Replacement.Length + request.Find.Length, overlayCharacters = 0; var clock = Stopwatch.StartNew();
        for (var n = 0; n < captured.Index.Messages.Count; n++)
        {
            token.ThrowIfCancellationRequested(); var identity = captured.Mapping.Messages[n];
            if (!selected.Contains(identity.MessageId)) continue;
            scanned++; var indexed = captured.Index.Messages[n]; DecodedMessageSource? decoded = null;
            try
            {
                var prefix = captured.Snapshot.ReadBytes(new(indexed.PayloadRange.Offset, Math.Min(4, indexed.PayloadRange.Length)));
                var bom = Hl7SourceEncoding.Detect(prefix, encoding).BomLength;
                IHl7TextSource text;
                if (indexed.PayloadRange.Length <= 128 * 1024)
                {
                    var strict = (Encoding)encoding.Clone(); strict.DecoderFallback = DecoderFallback.ExceptionFallback;
                    text = new StringHl7TextSource(strict.GetString(captured.Snapshot.ReadBytes(indexed.PayloadRange).AsSpan(bom)));
                }
                else text = decoded = DecodedMessageSource.Create(captured.Snapshot, indexed.PayloadRange, encoding, bom, token);
                var structure = Hl7StructureScanner.Scan(text, encoding, cancellationToken: token);
                var targets = Select(structure, request, token).ToArray();
                if (targets.Any(t => t.Node is not null && !t.Unsafe)) containing++;
                var proposed = new List<(Hl7Path Path, CharacterRange Range, string Raw, int Row)>();
                foreach (var target in targets)
                {
                    token.ThrowIfCancellationRequested();
                    if (++nodes > options.MaximumNodes) throw new Hl7LimitException("BulkNodes");
                    if (target.Unsafe)
                    {
                        skipped++; if (request.Operation != BulkFieldOperation.UniqueValues) invalid++;
                        AddChange(target, "", "Skipped: ambiguous structure"); continue;
                    }
                    var state = target.Node?.State ?? Hl7ValueState.Missing;
                    if (target.Node is not null) presentNodes++;
                    if (target.Node?.Range.Length > options.MaximumValueCharacters) throw new Hl7LimitException("BulkValueCharacters");
                    var raw = target.Node is { } node ? text.Read(node.Range) : "";
                    if (target.Node is null) absent++;
                    if (request.Operation == BulkFieldOperation.UniqueValues)
                    {
                        if (!values.TryGetValue((state, raw), out var value))
                        {
                            if (values.Count == options.MaximumDistinctValues) throw new Hl7LimitException("BulkDistinctValues");
                            Reserve(raw.Length); values.Add((state, raw), value = new(raw, state));
                        }
                        value.Occurrences++; value.Messages[identity.MessageId] = value.Messages.GetValueOrDefault(identity.MessageId) + 1;
                    }
                    else if (target.Node is null) AddChange(target, "", "Absent: not created");
                    else if (request.Operation == BulkFieldOperation.SearchReplace && raw != request.Find)
                    { skipped++; AddChange(target, raw, "Skipped: no exact raw match"); }
                    else if (raw == request.Replacement) { unchanged++; AddChange(target, raw, "Unchanged"); }
                    else
                    {
                        var row = changes.Count; AddChange(target, raw, "Proposed");
                        proposed.Add((target.Path, target.Node.Range, raw, row)); replacements++;
                    }
                }
                if (proposed.Count > 0)
                {
                    affected++;
                    try
                    {
                        ValidateSpans(proposed.Select(p => p.Range));
                        if (text.Length > MessagePresentation.MaximumEditorCharacters || indexed.PayloadRange.Length > 4L * MessagePresentation.MaximumEditorCharacters + 4)
                            throw new InvalidOperationException("Message exceeds existing editing bounds.");
                        var model = Hl7Parser.Parse(captured.Snapshot.ReadBytes(indexed.PayloadRange, 4 * MessagePresentation.MaximumEditorCharacters + 4),
                            new() { Encoding = encoding, MaxMessageCharacters = MessagePresentation.MaximumEditorCharacters,
                                MaxSegmentLength = MessageDocument.MaximumEditableLineCharacters });
                        var buffer = new EditableMessageBuffer(model); var expected = model.RawText;
                        foreach (var proposal in proposed.OrderByDescending(p => p.Range.Offset))
                        {
                            token.ThrowIfCancellationRequested();
                            if (!expected.AsSpan((int)proposal.Range.Offset, (int)proposal.Range.Length).SequenceEqual(proposal.Raw))
                                throw new InvalidOperationException("Captured target changed.");
                            model.SetRaw(proposal.Path, request.Replacement);
                            expected = string.Concat(expected.AsSpan(0, (int)proposal.Range.Offset), request.Replacement, expected.AsSpan((int)proposal.Range.End));
                        }
                        if (model.RawText != expected) throw new InvalidOperationException("Neighboring structure would change.");
                        buffer.Replace(expected); overlayCharacters += buffer.RetainedCharacters;
                        if (overlayCharacters > options.MaximumOverlayCharacters) throw new Hl7LimitException("BulkOverlayCharacters");
                        // Core splices every controlled write into its original bytes.
                        // Re-encoding the text between two edits could change untouched
                        // byte aliases in a supported single-byte source encoding.
                        overlays.Add(n, Hl7Serializer.Serialize(model));
                    }
                    catch (Exception error) when (error is ArgumentException or InvalidOperationException or EncoderFallbackException or KeyNotFoundException or Hl7LimitException)
                    {
                        // Do not expose exception text, which can contain raw HL7. Any invalid
                        // proposed message blocks the complete publication, not just this row.
                        invalid += proposed.Count;
                        foreach (var proposal in proposed) changes[proposal.Row] = changes[proposal.Row] with { Status = "Invalid: encoding, structure or editing budget" };
                    }
                }
                void AddChange(Target target, string raw, string status)
                {
                    if (request.Operation == BulkFieldOperation.UniqueValues) return;
                    if (changes.Count == options.MaximumNodes) throw new Hl7LimitException("BulkPreviewRows");
                    Reserve(raw.Length);
                    changes.Add(new(identity.MessageId, identity.LogicalPosition, target.Path, target.Node?.Range, raw, request.Replacement, status));
                }
            }
            catch (DecoderFallbackException)
            {
                skipped++; if (request.Operation != BulkFieldOperation.UniqueValues) invalid++;
                if (request.Operation != BulkFieldOperation.UniqueValues)
                {
                    if (changes.Count == options.MaximumNodes) throw new Hl7LimitException("BulkPreviewRows");
                    changes.Add(new(identity.MessageId, identity.LogicalPosition, request.Path, null, "", request.Replacement, "Skipped: unreadable message"));
                }
            }
            finally { decoded?.Dispose(); }
            if (scanned == 1 || clock.ElapsedMilliseconds >= 100 || scanned == selected.Count)
            { progress?.Report(new("Analyzing", scanned, selected.Count)); clock.Restart(); }
        }
        token.ThrowIfCancellationRequested();
        if (!document.IsSearchCurrent(revision) || filter is not null && !ReferenceEquals(filter, document.NavigationMatchIds))
            throw new InvalidOperationException("Source or filter changed. Analyze again.");
        return new(document, revision, filter, request, captured, options, values.Values.ToArray(), changes.ToArray(), overlays,
            new(scanned, containing, presentNodes, absent, skipped, unchanged, invalid, replacements, affected, nodes), lifetime, changesToken);
        void Reserve(int characters)
        {
            retained += characters;
            if (retained > options.MaximumRetainedValueCharacters) throw new Hl7LimitException("BulkRetainedValues");
        }
    }

    private sealed record Target(Hl7Path Path, StructureNode? Node, bool Unsafe);
    private static IEnumerable<Target> Select(Hl7Structure structure, BulkFieldRequest request, CancellationToken token)
    {
        var path = request.Path; var occurrence = 0; var found = false;
        foreach (var segment in structure.Segments.Where(s => s.Identifier == path.Segment))
        {
            token.ThrowIfCancellationRequested(); occurrence++;
            if (!request.AllSegmentOccurrences && occurrence != path.Occurrence) continue;
            found = true;
            var node = segment.Fields.FirstOrDefault(f => f.Number == path.Field);
            if (path.Repetition is { } r) node = node?.Children.FirstOrDefault(c => c.Number == r);
            if (path.Component is { } c) node = node?.Children.FirstOrDefault(n => n.Number == c);
            if (path.Subcomponent is { } s) node = node?.Children.FirstOrDefault(n => n.Number == s);
            yield return new(new(path.Segment, path.Field, occurrence, path.Repetition, path.Component, path.Subcomponent), node, !structure.HasUnambiguousMessageStructure || !segment.CanEdit);
        }
        if (!found) yield return new(path, null, !structure.HasUnambiguousMessageStructure);
    }
    internal static void ValidateSpans(IEnumerable<CharacterRange> ranges)
    {
        CharacterRange? previous = null;
        foreach (var range in ranges.OrderBy(r => r.Offset))
        {
            if (range.Offset < 0 || range.Length < 0 || previous is { } p && (range.Offset < p.End || range.Offset == p.Offset))
                throw new InvalidOperationException("Overlapping or invalid structural targets.");
            previous = range;
        }
    }
}
