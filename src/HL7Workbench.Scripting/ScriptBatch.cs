using HL7Workbench.Core;

namespace HL7Workbench.Scripting;

/// <summary>Advanced facilities for one unified run. Top-level variables persist across Main calls;
/// a new run receives a fresh process/context. This object is not a script type.</summary>
public sealed class ScriptBatch
{
    private readonly CancellationToken cancellation;
    private readonly List<ScriptCsvReport> reports = [];
    private readonly List<ScriptReportChunk> pending = [];
    private readonly Queue<(int Number, byte[] Bytes)> deferred = new();
    private ScriptMessage? current;
    private int currentNumber, processed, reportRows, pendingBytes;
    private long reportBytes, deferredBytes, retainedBytes;
    private int retainedCount;
    private bool initializing = true, completing, finished;
    private string? outputFileName;
    private bool ignoredOutputFileName;
    internal ScriptBatch(CancellationToken cancellation) => this.cancellation = cancellation;

    private Action? process, complete;
    internal ScriptMessage CurrentMessage
    { get { Check(); return current ?? throw Failure("LifecyclePhase"); } }
    /// <summary>One-based captured job ordinal during Main; zero in initialization/Complete.
    /// Current-message scope uses 1 regardless of the selected document position.</summary>
    public int MessageNumber => currentNumber;

    /// <summary>Suggests only an output filename, including from Complete. The last valid suggestion wins;
    /// invalid values are ignored without failing execution. Does not save or choose a directory.</summary>
    public void SetOutputFileName(string? fileName)
    {
        Check();
        if (ScriptOutputFileName.TryNormalize(fileName, out var valid)) outputFileName = valid;
        else ignoredOutputFileName = true;
    }
    internal ScriptOutputNaming? OutputNaming => outputFileName is null && !ignoredOutputFileName ? null :
        new(outputFileName, ignoredOutputFileName);

    /// <summary>Defines an ordered schema during initialization. ASCII report labels are independent of final export paths.</summary>
    public ScriptCsvReport Report(string name, params string[] columns)
    {
        Check();
        if (!initializing || reports.Count >= ScriptLimits.Reports || !ValidReportName(name) ||
            reports.Any(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)) || columns.Length is < 1 or > ScriptLimits.ReportColumns)
            throw Failure("ReportSchema");
        var report = new ScriptCsvReport(this, name, columns.Length);
        Append(name, columns, header: true);
        reports.Add(report);
        return report;
    }

    /// <summary>Copies the current transformed message for emission after Complete. Only call during Main.
    /// Does not exclude its normal output; call message.Exclude() separately for delayed-only output.</summary>
    public void Defer(ScriptMessage message) => Queue(Hold(message));

    /// <summary>Retains a bounded copy of the current transformed message for an explicit Emit decision during Complete.
    /// Exclude normal output separately if needed. Retained copies and emitted copies each share the 128/16 MiB job limits.</summary>
    public ScriptDeferredMessage Hold(ScriptMessage message)
    {
        Check();
        if (!ReferenceEquals(current, message)) throw Failure("LifecyclePhase");
        var bytes = message.Serialize();
        if (retainedCount >= ScriptLimits.DeferredMessages || bytes.Length > ScriptLimits.MessageBytes ||
            retainedBytes + bytes.Length > ScriptLimits.DeferredBytes) throw Failure("DeferredLimit");
        retainedCount++; retainedBytes += bytes.Length;
        return new(this, currentNumber, bytes);
    }
    /// <summary>Emits a Hold copy during Complete, in call order, after normal/deferred output. No arbitrary source-free messages.</summary>
    public void Emit(ScriptDeferredMessage copy)
    {
        Check();
        if (!completing || !ReferenceEquals(copy.Owner, this)) throw Failure("LifecyclePhase");
        Queue(copy);
    }
    private void Queue(ScriptDeferredMessage copy)
    {
        if (deferred.Count >= ScriptLimits.DeferredMessages || deferredBytes + copy.Bytes.Length > ScriptLimits.DeferredBytes)
            throw Failure("DeferredLimit");
        deferred.Enqueue((copy.Number, copy.Bytes)); deferredBytes += copy.Bytes.Length;
    }

    /// <summary>Report names are 1–64 ASCII letters/digits, underscore or hyphen, independent of export destinations.</summary>
    public static bool ValidReportName(string name) => name.Length is > 0 and <= 64 &&
        name.All(c => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' or '-');
    internal void Append(string name, string?[] values, bool header = false)
    {
        Check();
        if (!header && reportRows >= ScriptLimits.ReportRows) throw Failure("ReportLimit");
        if (values.Any(v => v?.Length > ScriptLimits.ReportCellCharacters)) throw Failure("ReportLimit");
        // Share the existing spelling/formula policy with read-only field extraction.
        var bytes = CsvRecordEncoder.Encode(values);
        if (reportBytes + bytes.Length > ScriptLimits.ReportBytes || pendingBytes + bytes.Length > ScriptLimits.ReportOperationBytes)
            throw Failure("ReportLimit");
        pending.Add(new(name, bytes, header)); pendingBytes += bytes.Length; reportBytes += bytes.Length;
        if (!header) reportRows++;
    }
    internal void Initialized(Action main, Action? onComplete = null)
    {
        if (!initializing || IsAsync(main) || onComplete is not null && IsAsync(onComplete)) throw Failure("LifecyclePhase");
        process = main; complete = onComplete; initializing = false;
    }
    internal void Invoke(ScriptMessage message, int number)
    {
        Check();
        if (initializing || completing) throw Failure("LifecyclePhase");
        if (++processed > ScriptLimits.BatchMessages) throw Failure("BatchLimit");
        current = message; currentNumber = number;
        try
        {
            process!();
        }
        finally { current = null; currentNumber = 0; }
    }
    internal void Finish()
    {
        Check(); completing = true;
        complete?.Invoke(); finished = true;
    }
    internal ScriptReportChunk[] DrainReports()
    { var result = pending.ToArray(); pending.Clear(); pendingBytes = 0; return result; }
    internal (int Number, byte[] Bytes)? TakeDeferred() => deferred.TryDequeue(out var item) ? item : null;
    internal int DeferredCount => deferred.Count;
    private void Check() { cancellation.ThrowIfCancellationRequested(); if (finished) throw Failure("LifecyclePhase"); }
    private static bool IsAsync(Delegate callback) => callback.GetInvocationList().Any(d =>
        d.Method.IsDefined(typeof(System.Runtime.CompilerServices.AsyncStateMachineAttribute), inherit: false));
    private static ScriptRunException Failure(string code) => new(new ScriptDiagnostic(ScriptPhase.Execution, code));
}

/// <summary>Opaque immutable processed-message copy, valid only for emission by its owning batch during Complete.</summary>
public sealed class ScriptDeferredMessage
{
    internal ScriptBatch Owner { get; }
    internal int Number { get; }
    internal byte[] Bytes { get; }
    internal ScriptDeferredMessage(ScriptBatch owner, int number, byte[] bytes) { Owner = owner; Number = number; Bytes = bytes; }
}

/// <summary>Ordered, string-valued CSV schema. Row values must match column count; null and empty both export as an empty quoted cell.</summary>
public sealed class ScriptCsvReport
{
    private readonly ScriptBatch batch;
    private readonly int columns;
    internal string Name { get; }
    internal ScriptCsvReport(ScriptBatch batch, string name, int columns) { this.batch = batch; Name = name; this.columns = columns; }
    public void Row(params string?[] values)
    {
        if (values.Length != columns) throw new ScriptRunException(new ScriptDiagnostic(ScriptPhase.Execution, "ReportSchema"));
        batch.Append(Name, values);
    }
}

/// <summary>Bounded report transfer data. Consumers must stage it and publish only after the complete job succeeds.</summary>
public sealed record ScriptReportChunk(string Name, byte[] Bytes, bool Header = false);
