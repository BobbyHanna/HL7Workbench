using System.ComponentModel;
using System.Globalization;
using System.Text;
using HL7Workbench.Scripting;

namespace HL7Workbench.Workspace;

/// <summary>Document-owned, bounded projection of the existing sealed count-only report.
/// Contains no source fields, script names, filenames or mapping keys; journal ownership stays unchanged.</summary>
public sealed class TransformationReport : INotifyPropertyChanged, IDisposable
{
    private readonly WeakReference<MessageDocument> source;
    private readonly ScriptInputMapping mapping;
    private bool closed, disposed;
    public DeidentificationSummary Summary { get; }
    public ScriptScope Scope { get; }
    public long CapturedRevision => mapping.Revision;
    public bool SourceChangedDuringRun { get; }
    public bool SourceIsCurrent => !closed && source.TryGetTarget(out var document) && mapping.IsCurrent(document);
    public string PresetName => Summary.Preset switch
    { DeidentificationPreset.PatientDeidentifier => "Patient Deidentifier", DeidentificationPreset.IdentifierPseudonymizer => "Identifier Pseudonymizer", _ => "Timestamp Shifter" };
    public string SourceState => closed ? "Source closed; report describes the captured revision." : !SourceIsCurrent ?
        "Stale source: source has changed since capture; newer source work is retained." : "Source matches captured revision.";
    public const string Warning = "Review required. Synthetic substitution does not guarantee complete de-identification or safe disclosure. " +
        "Free text, unknown segments, attachments, container bytes, nested metadata and retained clinical values require review. No HIPAA certification.";
    public event PropertyChangedEventHandler? PropertyChanged;
    private TransformationReport(DeidentificationSummary summary, ScriptScope scope, ScriptInputMapping mapping, MessageDocument source, bool changedDuringRun)
    {
        Summary = summary; Scope = scope; this.mapping = mapping; this.source = new(source); SourceChangedDuringRun = changedDuringRun;
        source.PropertyChanged += Changed; source.Closed += Closed;
    }
    private void Changed(object? sender, PropertyChangedEventArgs e) => PropertyChanged?.Invoke(this, new(null));
    private void Closed(object? sender, EventArgs e) { closed = true; Unsubscribe(); PropertyChanged?.Invoke(this, new(null)); }
    private void Unsubscribe()
    { if (source.TryGetTarget(out var document)) { document.PropertyChanged -= Changed; document.Closed -= Closed; } }
    public void Dispose() { if (disposed) return; disposed = true; Unsubscribe(); PropertyChanged = null; }

    internal static TransformationReport? Read(ScriptReports reports, ScriptScope scope, ScriptInputMapping mapping, MessageDocument source, bool stale, int processed)
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
            cells[0] != "Review required" || cells[12] != DeidentificationSummary.ScopeNotice ||
            !Enum.TryParse<DeidentificationPreset>(cells[13], out var preset) || !Enum.IsDefined(preset) ||
            !int.TryParse(cells[1], NumberStyles.None, CultureInfo.InvariantCulture, out var version) || version != (preset == DeidentificationPreset.PatientDeidentifier ? 2 : 1)) return null;
        var numbers = new long[cells.Length];
        for (var i = 2; i < cells.Length; i++)
            if (i is not (12 or 13) && (!long.TryParse(cells[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i]) || numbers[i] > 2_000_000_000)) return null;
        if (numbers[2] != processed || Enumerable.Range(0, 9).Sum(i => numbers[14 + i * 2]) != numbers[5] ||
            Enumerable.Range(0, 9).Sum(i => numbers[15 + i * 2]) != numbers[6]) return null;
        var summary = new DeidentificationSummary(numbers[2], numbers[3], numbers[4], numbers[5], numbers[6], numbers[7], numbers[8], numbers[9], numbers[10], numbers[11])
        {
            Preset = preset, Version = version,
            Categories = Array.AsReadOnly(Enum.GetValues<DeidentificationCategory>().Select((c, i) => new DeidentificationCategoryCount(c, numbers[14 + i * 2], numbers[15 + i * 2])).ToArray())
        };
        return new(summary, scope, mapping, source, stale);
    }
}
