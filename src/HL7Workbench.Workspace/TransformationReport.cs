using System.ComponentModel;
using System.Globalization;
using System.Text;
using HL7Workbench.Scripting;

namespace HL7Workbench.Workspace;

/// <summary>Document-owned, bounded projection of sealed summary and structural-finding CSVs.
/// Contains no source values, script names, filenames or mapping keys; journal ownership stays unchanged.</summary>
public sealed class TransformationReport : INotifyPropertyChanged, IDisposable
{
    private readonly WeakReference<MessageDocument> source;
    private readonly WeakReference<MessageDocument> output;
    private readonly ScriptInputMapping mapping;
    private readonly int? targetOrdinal;
    private bool closed, disposed;
    public DeidentificationSummary Summary { get; }
    public IReadOnlyList<DeidentificationFinding> Findings { get; }
    public bool FindingDetailsAvailable { get; }
    public Guid ExecutionId { get; } = Guid.NewGuid();
    public long CapturedOutputRevision { get; }
    public bool OutputIsCurrent => output.TryGetTarget(out var document) && document.IsSearchCurrent(CapturedOutputRevision);
    public string OutputState => OutputIsCurrent ? "Output matches the producing execution." : "Stale output: output was edited; later changes are not reassessed.";
    /// <summary>Resolves workflow ordinals against capture-time positions, never current document order.</summary>
    public int SourceMessageNumber(int processedOrdinal) => mapping.Resolve(Scope == ScriptScope.CurrentMessage ? targetOrdinal ?? processedOrdinal : processedOrdinal)?.LogicalPosition ?? processedOrdinal;
    public ScriptScope Scope { get; }
    public long CapturedRevision => mapping.Revision;
    public bool SourceChangedDuringRun { get; }
    public bool SourceIsCurrent => !closed && source.TryGetTarget(out var document) && mapping.IsCurrent(document);
    public string PresetName => Summary.Preset switch
    { DeidentificationPreset.PatientDeidentifier => "Patient Deidentifier", DeidentificationPreset.IdentifierPseudonymizer => "Identifier Pseudonymizer", _ => "Timestamp Shifter" };
    public string SourceState => closed ? "Source closed; report describes the captured revision." : !SourceIsCurrent ?
        "Stale source: source has changed since capture; newer source work is retained." : "Source matches captured revision.";
    public const string Warning = "General limitation: synthetic substitution does not guarantee complete de-identification or safe disclosure. " +
        "Free text, unknown segments, attachments, container bytes, nested metadata and retained clinical values require review. No HIPAA certification.";
    public event PropertyChangedEventHandler? PropertyChanged;
    private TransformationReport(DeidentificationSummary summary, IReadOnlyList<DeidentificationFinding> findings, bool available,
        ScriptScope scope, ScriptInputMapping mapping, int? targetOrdinal, MessageDocument source, MessageDocument output, bool changedDuringRun)
    {
        Summary = summary; Scope = scope; this.mapping = mapping; this.source = new(source); SourceChangedDuringRun = changedDuringRun;
        this.targetOrdinal = targetOrdinal;
        Findings = findings; FindingDetailsAvailable = available; this.output = new(output); CapturedOutputRevision = output.SearchRevision;
        source.PropertyChanged += Changed; source.Closed += Closed;
        output.PropertyChanged += Changed;
    }
    private void Changed(object? sender, PropertyChangedEventArgs e) => PropertyChanged?.Invoke(this, new(null));
    private void Closed(object? sender, EventArgs e) { closed = true; Unsubscribe(); PropertyChanged?.Invoke(this, new(null)); }
    private void Unsubscribe()
    { if (source.TryGetTarget(out var document)) { document.PropertyChanged -= Changed; document.Closed -= Closed; } }
    public void Dispose() { if (disposed) return; disposed = true; Unsubscribe(); if (output.TryGetTarget(out var document)) document.PropertyChanged -= Changed; PropertyChanged = null; }

    internal static TransformationReport? Read(ScriptReports reports, ScriptScope scope, ScriptInputMapping mapping, int? targetOrdinal, MessageDocument source, MessageDocument output, bool stale, int processed)
    {
        var info = reports.Items.FirstOrDefault(r => r.Name == "deidentification-review");
        if (info?.Rows != 1 || reports.ReadSmallReport(info.Name, 8192) is not { } bytes) return null;
        string text; try { text = new UTF8Encoding(false, true).GetString(bytes); } catch (DecoderFallbackException) { return null; }
        var header = string.Join(',', DeidentificationSummary.Columns.Select(c => "\"" + c + "\"")) + "\r\n";
        if (!text.StartsWith(header, StringComparison.Ordinal) || !text.EndsWith("\r\n", StringComparison.Ordinal)) return null;
        var row = text[header.Length..^2];
        if (row.Length < 2 || row[0] != '"' || row[^1] != '"') return null;
        var cells = row[1..^1].Split("\",\"", StringSplitOptions.None);
        // Only this fixed schema is displayed: never render arbitrary CSV cells
        // from a user script as a privacy report, even under the reserved name.
        if (cells.Length != DeidentificationSummary.Columns.Length || cells.Any(c => c.Contains('"') || c.Contains('\r') || c.Contains('\n')) ||
            cells[12] != DeidentificationSummary.ScopeNotice ||
            !Enum.TryParse<DeidentificationPreset>(cells[13], out var preset) || !Enum.IsDefined(preset) ||
            !int.TryParse(cells[1], NumberStyles.None, CultureInfo.InvariantCulture, out var version) || version != (preset == DeidentificationPreset.PatientDeidentifier ? 2 : 1)) return null;
        var numbers = new long[cells.Length];
        for (var i = 2; i < cells.Length; i++)
            if (i is not (12 or 13) && (!long.TryParse(cells[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i]) || numbers[i] > 2_000_000_000)) return null;
        if (numbers[2] != processed || Enumerable.Range(0, 9).Sum(i => numbers[14 + i * 2]) != numbers[5] ||
            Enumerable.Range(0, 9).Sum(i => numbers[15 + i * 2]) != numbers[6]) return null;
        var summary = new DeidentificationSummary(numbers[2], numbers[3], numbers[4], numbers[5], numbers[6], numbers[7], numbers[8], numbers[9], numbers[10], numbers[11])
        {
            Preset = preset, Version = version, RetainedValuesRequiringReview = numbers[^2], OmittedFindings = numbers[^1],
            Categories = Array.AsReadOnly(Enum.GetValues<DeidentificationCategory>().Select((c, i) => new DeidentificationCategoryCount(c, numbers[14 + i * 2], numbers[15 + i * 2])).ToArray())
        };
        if (cells[0] != summary.Status || summary.RetainedValuesRequiringReview > summary.RetainedValues) return null;
        var findings = ReadFindings(reports, processed);
        return new(summary, findings ?? [], findings is not null, scope, mapping, targetOrdinal, source, output, stale);
    }

    private static IReadOnlyList<DeidentificationFinding>? ReadFindings(ScriptReports reports, int processed)
    {
        var info = reports.Items.FirstOrDefault(r => r.Name == "deidentification-findings");
        if (info is null || info.Rows > DeidentificationFinding.MaximumFindings || reports.ReadSmallReport(info.Name, 256 * 1024) is not { } bytes) return null;
        string text; try { text = new UTF8Encoding(false, true).GetString(bytes); } catch (DecoderFallbackException) { return null; }
        var lines = text.Split("\r\n", StringSplitOptions.None);
        if (lines.Length != info.Rows + 2 || lines[^1] != "" || lines[0] != string.Join(',', DeidentificationFinding.Columns.Select(c => "\"" + c + "\""))) return null;
        var findings = new List<DeidentificationFinding>();
        foreach (var line in lines.Skip(1).SkipLast(1))
        {
            if (line.Length < 2 || line[0] != '"' || line[^1] != '"') return null;
            var cells = line[1..^1].Split("\",\"", StringSplitOptions.None);
            bool Number(int i, int minimum, int maximum, out int value) => int.TryParse(cells[i], NumberStyles.None, CultureInfo.InvariantCulture, out value) && value >= minimum && value <= maximum;
            if (cells.Length != 6 || !Number(0, 1, processed, out var message) || !Number(1, 0, 65536, out var position) ||
                !Enum.TryParse<DeidentificationSegment>(cells[2], out var segment) || !Enum.IsDefined(segment) || cells[2] != segment.ToString() ||
                !Number(3, 0, 65536, out var occurrence) || !Number(4, 0, 65536, out var field) ||
                !Enum.TryParse<DeidentificationReason>(cells[5], out var reason) || !Enum.IsDefined(reason) || cells[5] != reason.ToString()) return null;
            findings.Add(new(message, position, segment, occurrence, field, reason));
        }
        return findings.AsReadOnly();
    }
}
