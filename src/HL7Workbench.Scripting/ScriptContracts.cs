namespace HL7Workbench.Scripting;

/// <summary>Entry-point discriminator. Only Message is implemented in Stage 5A.</summary>
public enum ScriptMode { Message, Batch, Generate }
public enum ScriptScope { CurrentMessage, WholeDocument }
public enum ScriptPhase { Configuration, Compilation, Execution, Protocol, Worker, Input, Output }

/// <summary>Safe diagnostic metadata: no exception messages, source values, filenames or script excerpts.</summary>
public sealed record ScriptDiagnostic(ScriptPhase Phase, string Code, int? Line = null, int? Column = null,
    int? MessageNumber = null, string? Path = null)
{
    public string Description => Code switch
    {
        "UnsupportedMode" => "Only Message scripts are available in this milestone.",
        "ScriptLimit" => "Script exceeds the 65,536-character limit.",
        "DirectiveNotSupported" => "External reference, source loading and line-mapping directives are unavailable.",
        "CompilationFailed" => "C# compilation failed. Check the indicated script location and compiler code.",
        "Canceled" => "Script run canceled; no output published.",
        "Timeout" => "Worker operation exceeded its time limit; no output published.",
        "WorkerUnavailable" => "Unable to start the scripting worker.",
        "WorkerStopped" => "Worker stopped unexpectedly; no output published.",
        "ProtocolError" => "Worker communication failed; no output published.",
        "InputLimit" => "Targeted message exceeds the complete-message scripting limits.",
        "InputStorageLimit" => "Captured source exceeds the configured input-storage limit.",
        "InvalidEncoding" => "Message cannot be strictly decoded or encoded.",
        "InputUnavailable" => "Complete captured input is unavailable.",
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

public sealed record ScriptDefinition(string Source, ScriptMode Mode = ScriptMode.Message, int ApiVersion = 1);
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
}

public sealed record ScriptMessageResult(bool Excluded, byte[]? Bytes);
public sealed record ScriptProgress(string Phase, int Processed, int Total, long OutputBytes);
