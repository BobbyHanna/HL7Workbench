using HL7Workbench.Core;

namespace HL7Workbench.Workspace;

public enum ActivityKind
{
    ApplicationReady, ApplicationError, OpeningFile, FileIndexed, FileOpened, FileOpenFailed,
    FileOpenCanceled, Diagnostic, DiagnosticLimit, SourceViewerOpened, SourceReadFailed,
    ExportStarted, ExportCompleted, ExportFailed, ExportCanceled, LayoutFailed
}
public enum ActivityFailure { None, Unavailable, AccessDenied, ResourceLimit, Encoding, Unsupported, Unexpected }
public enum ActivityCategory { Application, File, Boundary, Structure, Inspection, Export }
public enum ActivityDiagnostic
{
    Unknown, MissingMsh, MshNotFirst, InvalidDelimiters, MultipleMessages, MalformedSegment,
    AmbiguousEscape, UnterminatedEscape, InvalidHexEscape, UnsupportedEscape,
    UnsupportedEditEncoding, EmptyMessage, MissingHeaderValue, InvalidEncoding,
    UnclassifiedSource, InteriorBom, IncompleteMllp, AmbiguousFraming, DiagnosticsSuppressed,
    InspectionFailed, InspectionIncomplete
}

/// <summary>Safe immutable metadata only. Descriptions are generated here, never imported from source or exceptions.</summary>
public sealed record ActivityEntry
{
    public long Sequence { get; internal init; }
    public DateTimeOffset Timestamp { get; internal init; }
    public ActivityKind Kind { get; internal init; }
    public Hl7DiagnosticSeverity Severity { get; internal init; }
    public int? DocumentNumber { get; internal init; }
    public int? MessageNumber { get; internal init; }
    public long? Count { get; internal init; }
    public ActivityFailure Failure { get; internal init; }
    public ActivityCategory Category { get; internal init; }
    public ActivityDiagnostic Diagnostic { get; internal init; }
    public long? CharacterOffset { get; internal init; }
    public long? ByteOffset { get; internal init; }
    public int? HeaderField { get; internal init; }
    internal ActivityEntry() { }
    public string Description => Kind switch
    {
        ActivityKind.ApplicationReady => "Application ready",
        ActivityKind.ApplicationError => "Application operation unavailable",
        ActivityKind.OpeningFile => "Opening file",
        ActivityKind.FileIndexed => $"Indexed {Count:N0} HL7 message(s)",
        ActivityKind.FileOpened => "File opened read-only",
        ActivityKind.FileOpenFailed => "File open failed",
        ActivityKind.FileOpenCanceled => "File open canceled",
        ActivityKind.SourceViewerOpened => "Full Source Viewer opened",
        ActivityKind.SourceReadFailed => "Source inspection read unavailable",
        ActivityKind.ExportStarted => "Original-source export started",
        ActivityKind.ExportCompleted => $"Original-source export completed — {Count:N0} bytes",
        ActivityKind.ExportFailed => "Original-source export failed; no partial output published",
        ActivityKind.ExportCanceled => "Original-source export canceled",
        ActivityKind.LayoutFailed => "Workspace layout operation unavailable",
        ActivityKind.DiagnosticLimit => "Additional diagnostic activity omitted; document diagnostics remain available",
        _ => Diagnostic switch
        {
            ActivityDiagnostic.UnclassifiedSource => "Unclassified source regions retained for byte inspection",
            ActivityDiagnostic.InvalidEncoding => "Input cannot be safely decoded; original bytes retained",
            ActivityDiagnostic.InspectionIncomplete => "Structural inspection budget exceeded; source remains accessible",
            ActivityDiagnostic.InspectionFailed => "Structural inspection unavailable; original bytes retained",
            ActivityDiagnostic.MissingHeaderValue => "Commonly required header value is missing, empty or null",
            ActivityDiagnostic.MissingMsh => "MSH missing; standard delimiters assumed",
            ActivityDiagnostic.MshNotFirst => "MSH is not the first segment",
            ActivityDiagnostic.InvalidDelimiters => "Invalid delimiter declaration retained",
            ActivityDiagnostic.MultipleMessages => "Multiple headers in a single parsed message",
            ActivityDiagnostic.MalformedSegment => "Malformed segment retained",
            ActivityDiagnostic.AmbiguousEscape => "Ambiguous escape structure retained",
            ActivityDiagnostic.UnterminatedEscape => "Unterminated escape retained",
            ActivityDiagnostic.InvalidHexEscape => "Invalid hexadecimal escape retained",
            ActivityDiagnostic.UnsupportedEscape => "Unsupported escape retained verbatim",
            ActivityDiagnostic.UnsupportedEditEncoding => "Encoding does not support lossless editing",
            ActivityDiagnostic.EmptyMessage => "Message has no segments",
            ActivityDiagnostic.InteriorBom => "Interior BOM retained under inherited encoding",
            ActivityDiagnostic.IncompleteMllp => "Incomplete MLLP envelope retained",
            ActivityDiagnostic.AmbiguousFraming => "Ambiguous wide-encoding MLLP boundary retained",
            ActivityDiagnostic.DiagnosticsSuppressed => "Additional parser or index diagnostics suppressed",
            _ => "Structural diagnostic recorded; imported explanatory text omitted"
        }
    };
    public string Context => string.Join(" · ", new[]
    {
        DocumentNumber is { } d ? $"Document {d}" : null,
        MessageNumber is { } m ? $"message {m}" : null,
        Kind == ActivityKind.Diagnostic ? $"{Category}/{Diagnostic}" : null,
        HeaderField is { } f ? $"MSH-{f}" : null,
        CharacterOffset is { } c ? $"message UTF-16 {c}" : null,
        ByteOffset is { } b ? $"snapshot byte {b}" : null,
        Failure != ActivityFailure.None ? Failure.ToString() : null
    }.OfType<string>());
    public override string ToString() => $"{Timestamp.ToLocalTime():HH:mm:ss} {Severity.ToString().ToUpperInvariant()} — {Description}" +
        (Context.Length == 0 ? "" : $" · {Context}");
}

/// <summary>Application-session history. Thread-safe, bounded, and deliberately has no free-text logging API.</summary>
public sealed class ActivityLog
{
    public const int Capacity = 2000;
    private readonly object gate = new();
    private readonly Queue<ActivityEntry> entries = new();
    private long sequence;
    private int documentNumber;
    public event EventHandler? Changed;
    public IReadOnlyList<ActivityEntry> Snapshot() { lock (gate) return entries.ToArray(); }
    internal int NextDocumentNumber() => Interlocked.Increment(ref documentNumber);
    public int BeginFileOpen()
    {
        var number = NextDocumentNumber();
        Record(ActivityKind.OpeningFile, number); return number;
    }
    public void Clear() { lock (gate) entries.Clear(); Changed?.Invoke(this, EventArgs.Empty); }
    public void Record(ActivityKind kind, int? document = null, int? message = null, long? count = null,
        ActivityFailure failure = ActivityFailure.None)
    {
        if (!Enum.IsDefined(kind) || kind == ActivityKind.Diagnostic || !Enum.IsDefined(failure)) throw new ArgumentOutOfRangeException(nameof(kind));
        Add(new() { Kind = kind, DocumentNumber = document, MessageNumber = message, Count = count, Failure = failure,
            Category = kind switch
            {
                ActivityKind.OpeningFile or ActivityKind.FileIndexed or ActivityKind.FileOpened or ActivityKind.FileOpenFailed or ActivityKind.FileOpenCanceled => ActivityCategory.File,
                ActivityKind.ExportStarted or ActivityKind.ExportCompleted or ActivityKind.ExportFailed or ActivityKind.ExportCanceled => ActivityCategory.Export,
                ActivityKind.SourceViewerOpened or ActivityKind.SourceReadFailed or ActivityKind.DiagnosticLimit => ActivityCategory.Inspection,
                _ => ActivityCategory.Application
            },
            Severity = kind is ActivityKind.ApplicationError or ActivityKind.FileOpenFailed or ActivityKind.SourceReadFailed or ActivityKind.ExportFailed or ActivityKind.LayoutFailed
                ? Hl7DiagnosticSeverity.Error : kind == ActivityKind.DiagnosticLimit ? Hl7DiagnosticSeverity.Warning : Hl7DiagnosticSeverity.Information });
    }
    /// <summary>Projects a diagnostic through a fixed vocabulary; imported description, code and path strings are never retained.</summary>
    public void RecordDiagnostic(int document, int? message, Hl7Diagnostic diagnostic, ActivityCategory category,
        long? characterOffset = null, long? byteOffset = null)
    {
        // Code and path are user-extensible Core strings. Only known enum codes and
        // three fixed header paths cross this boundary; descriptions never do.
        var code = Enum.TryParse<ActivityDiagnostic>(diagnostic.Code, out var value) && Enum.IsDefined(value) ? value : ActivityDiagnostic.Unknown;
        Add(new() { Kind = ActivityKind.Diagnostic, DocumentNumber = document, MessageNumber = message,
            Severity = Enum.IsDefined(diagnostic.Severity) ? diagnostic.Severity : Hl7DiagnosticSeverity.Warning,
            Diagnostic = code, Category = Enum.IsDefined(category) ? category : ActivityCategory.Structure, CharacterOffset = characterOffset ?? diagnostic.CharacterOffset, ByteOffset = byteOffset,
            HeaderField = diagnostic.Path switch { "MSH-9" => 9, "MSH-10" => 10, "MSH-12" => 12, _ => null } });
    }
    private void Add(ActivityEntry entry)
    {
        lock (gate)
        {
            entries.Enqueue(entry with { Sequence = ++sequence, Timestamp = DateTimeOffset.UtcNow });
            if (entries.Count > Capacity) entries.Dequeue();
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }
    public static ActivityFailure Classify(Exception error) => error switch
    {
        Hl7LimitException => ActivityFailure.ResourceLimit, System.Text.DecoderFallbackException => ActivityFailure.Encoding,
        UnauthorizedAccessException => ActivityFailure.AccessDenied, IOException => ActivityFailure.Unavailable,
        NotSupportedException => ActivityFailure.Unsupported, _ => ActivityFailure.Unexpected
    };
}

/// <summary>Counts actual retained diagnostics, including boundaries of inactive indexed messages.</summary>
public readonly record struct ActivityHealth(int Warnings, int Errors)
{
    public static ActivityHealth For(MessageDocument document)
    {
        var warnings = 0; var errors = 0;
        foreach (var item in DocumentActivity.Diagnostics(document))
        { if (item.Diagnostic.Severity == Hl7DiagnosticSeverity.Warning) warnings++; else if (item.Diagnostic.Severity == Hl7DiagnosticSeverity.Error) errors++; }
        return new(warnings, errors);
    }
    public static string OpenStatus(int opened, int failed, ActivityHealth health) =>
        $"Opened {opened} file(s) read-only" + (failed > 0 ? $"; {failed} failed" : "") +
        (health.Errors > 0 ? $"; {health.Errors} error(s)" : "") + (health.Warnings > 0 ? $"; {health.Warnings} warning(s)" : "") + ".";
}

/// <summary>Document-owned deduplication; log entries never retain documents or source objects.</summary>
internal sealed class DocumentActivity(ActivityLog log, int number)
{
    private const int MaxIdentities = 8192;
    private readonly HashSet<(int?, ActivityCategory, ActivityDiagnostic, Hl7DiagnosticSeverity, long?, long?, int?)> seen = [];
    private bool fileObserved;
    private bool saturated;
    internal readonly record struct Item(Hl7Diagnostic Diagnostic, int? Message, ActivityCategory Category, long? Offset = null, long? ByteOffset = null);
    internal static IEnumerable<Item> Diagnostics(MessageDocument document, bool includeFile = true)
    {
        if (document.Index is { } index)
        {
            if (includeFile)
            {
                foreach (var d in index.Diagnostics) yield return new(d, null, ActivityCategory.File, ByteOffset: d.Code == "UnclassifiedSource" ? index.Regions.FirstOrDefault(r => r.MessageNumber is null && r.Reason != "File encoding BOM")?.Range.Offset : null);
                foreach (var message in index.Messages)
                    foreach (var d in message.Diagnostics) yield return new(d, message.Number, ActivityCategory.Boundary);
            }
            var boundary = document.MessageNumber > 0 ? index.Messages[document.MessageNumber - 1].Diagnostics : [];
            var structural = document.ActiveDiagnostics.Skip(boundary.Count).ToArray();
            for (var i = 0; i < structural.Length; i++)
                yield return new(structural[i], document.MessageNumber, ActivityCategory.Structure,
                    document.Structure is { } structure && i < structure.Diagnostics.Count ? structure.Diagnostics[i].CharacterOffset : null);
        }
        else if (document.MaterializedMessage is { } materialized)
            foreach (var d in materialized.Diagnostics.Concat(Hl7Validator.Validate(materialized))) yield return new(d, 1, ActivityCategory.Structure);
    }
    public void Observe(MessageDocument document)
    {
        if (document.IsLoading || saturated) return;
        foreach (var item in Diagnostics(document, !fileObserved))
        {
            var d = item.Diagnostic;
            var code = Enum.TryParse<ActivityDiagnostic>(d.Code, out var parsed) && Enum.IsDefined(parsed) ? parsed : ActivityDiagnostic.Unknown;
            int? field = d.Path switch { "MSH-9" => 9, "MSH-10" => 10, "MSH-12" => 12, _ => null };
            var key = (item.Message, item.Category, code, d.Severity, item.Offset ?? d.CharacterOffset, item.ByteOffset, field);
            if (seen.Contains(key)) continue;
            if (seen.Count == MaxIdentities) { saturated = true; log.Record(ActivityKind.DiagnosticLimit, number); break; }
            seen.Add(key); log.RecordDiagnostic(number, item.Message, d, item.Category, item.Offset, item.ByteOffset);
        }
        fileObserved = true;
    }
}
