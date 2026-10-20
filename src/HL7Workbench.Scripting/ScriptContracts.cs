namespace HL7Workbench.Scripting;

public enum ScriptScope { CurrentMessage, WholeDocument }
public enum ScriptPhase { Configuration, Compilation, Execution, Protocol, Worker, Input, Output }

/// <summary>Safe diagnostic metadata: no exception messages, source values, filenames or script excerpts.</summary>
public sealed record ScriptDiagnostic(ScriptPhase Phase, string Code, int? Line = null, int? Column = null,
    int? MessageNumber = null, string? Path = null)
{
    public string Description => Code switch
    {
        "UnsupportedApi" => "This worker requires unified Main scripts using API version 2.",
        "MissingMain" => "Define one synchronous, parameterless void Main() method.",
        "EntryPointSignature" => "Main and optional Complete must be instance, synchronous, nongeneric, parameterless void methods.",
        "AmbiguousEntryPoint" => "Define only one Main and at most one Complete method; entry-point overloads are unavailable.",
        "LifecyclePhase" => "Message and Hold/Defer require Main; Emit requires Complete. This operation is unavailable in the current phase.",
        "BatchLimit" => "Batch exceeds the 100,000-message limit.",
        "DeferredLimit" => "Deferred output exceeds 128 messages or 16 MiB.",
        "ReportSchema" => "Report schema, name, phase or row column count is invalid.",
        "ReportLimit" => "Report exceeds its row, cell, operation or total byte limit.",
        "ScriptLimit" => "Script exceeds the 65,536-character limit.",
        "DirectiveNotSupported" => "External reference, source loading and line-mapping directives are unavailable.",
        "CompilationFailed" => "C# compilation failed. Check the indicated script location and compiler code.",
        "Canceled" => "Script run canceled; no output published.",
        "Timeout" => "Worker operation exceeded its time limit; no output published.",
        "WorkerUnavailable" => "Unable to start the scripting worker.",
        "WorkerStopped" => "Worker stopped unexpectedly; no output published.",
        "ProtocolError" => "Worker communication failed; no output published.",
        "InputLimit" => "Targeted message exceeds the complete-message scripting limits.",
        "TransformationLimit" => "De-identification exceeds its bounded identity, value or mutation budget; no output was published.",
        "InputStorageLimit" => "Captured source exceeds the configured input-storage limit.",
        "InvalidEncoding" => "Message cannot be strictly decoded or encoded.",
        "InputUnavailable" => "Complete captured input is unavailable.",
        "InputChanged" => "Source content or membership changed before input capture. Run again; no output was published and newer edits are retained.",
        "OutputLimit" => "Output exceeds the configured size limit.",
        "BoundaryChange" => "Output would change a message, framing or batch boundary.",
        "ContainerExtraction" => "Filtering or extraction requires plain messages or complete, unambiguous MLLP envelopes. Batch wrappers and unrelated regions cannot be silently discarded.",
        "InvalidPath" => "HL7 path is invalid.",
        "MissingValue" => "Requested value is missing or explicitly null.",
        "DecodeDiagnostic" => "Value has unresolved escape diagnostics; use Read for its original spelling.",
        "EditRejected" => "Core rejected the edit. Check path, delimiter, encoding and editability rules.",
        _ when Code.StartsWith("CS", StringComparison.Ordinal) => "C# compiler diagnostic; check the indicated script location.",
        _ => "Script operation failed. Imported exception text is omitted."
    };
    public override string ToString() => $"{Phase}: {Code} — {Description}" +
        (MessageNumber is { } number ? $" · message {number}" : "") +
        (Line is { } line ? $" · line {line}, column {Column ?? 1}" : "") + (Path is { } path ? $" · {path}" : "");
}

public sealed class ScriptRunException : Exception
{
    public IReadOnlyList<ScriptDiagnostic> Diagnostics { get; }
    public ScriptRunException(params ScriptDiagnostic[] diagnostics) : base(string.Join(Environment.NewLine, diagnostics.Select(d => d.ToString()))) =>
        Diagnostics = diagnostics;
}

public sealed record ScriptDefinition(string Source, int ApiVersion = 2);
public sealed record ScriptWorkerOptions
{
    public TimeSpan StartupTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan CompilationTimeout { get; init; } = TimeSpan.FromSeconds(60);
    public TimeSpan MessageTimeout { get; init; } = TimeSpan.FromSeconds(10);
    public long MemoryLimitBytes { get; init; } = 768L * 1024 * 1024;
    internal void Validate()
    {
        if (StartupTimeout <= TimeSpan.Zero || CompilationTimeout <= TimeSpan.Zero || MessageTimeout <= TimeSpan.Zero ||
            MemoryLimitBytes < 128L * 1024 * 1024 || MemoryLimitBytes > 2L * 1024 * 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(ScriptWorkerOptions));
    }
}

public static class ScriptLimits
{
    public const int ScriptCharacters = 65_536;
    public const int MessageCharacters = 1_048_576;
    public const int LineCharacters = 16_384;
    public const int MessageBytes = 8 * 1024 * 1024;
    public const int Diagnostics = 100;
    public const int BatchMessages = 100_000;
    public const int DeferredMessages = 128;
    public const int DeferredBytes = 16 * 1024 * 1024;
    public const int Reports = 8;
    public const int ReportColumns = 64;
    public const int ReportCellCharacters = 16_384;
    public const int ReportRows = 100_000;
    public const int ReportBytes = 16 * 1024 * 1024;
    public const int ReportOperationBytes = 256 * 1024;
}

public sealed record ScriptMessageResult(bool Excluded, byte[]? Bytes);
public sealed record ScriptProgress(string Phase, int Processed, int Total, long OutputBytes);
