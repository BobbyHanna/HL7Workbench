using System.Diagnostics;
using System.Buffers.Binary;
using System.Security.Cryptography;
using HL7Workbench.Core;

namespace HL7Workbench.Workspace;

public sealed record SyntheticGenerationProgress(string Phase, long Completed, long Total);
public sealed record SyntheticGenerationResult(MessageDocument Document, IReadOnlyList<MessageId> MessageIds,
    SyntheticGenerationOptions Options, bool IsNewDocument, TimeSpan GenerationTime, TimeSpan PublicationTime)
{
    public SyntheticScenarioOptions? ScenarioOptions { get; init; }
    public SyntheticGenerationReport? Report { get; init; }
}

/// <summary>Generates off-context, validates a whole prospective document and publishes one batch on the caller's owning context.</summary>
public sealed class SyntheticGeneration(DocumentManager documents, Func<uint>? scenarioSeedProvider = null)
{
    // Keep the constructor parameter's original name for named callers; the same
    // provider now serves both standalone and scenario operations.
    private readonly Func<uint> nextSeed = scenarioSeedProvider ?? CreateSeed;
    private static uint CreateSeed()
    {
        Span<byte> bytes = stackalloc byte[sizeof(uint)]; RandomNumberGenerator.Fill(bytes);
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes);
    }
    /// <summary>Generates the selected standalone style/version. Automatic selection is opt-in for programmatic callers;
    /// when requested, the shared provider is invoked once after preflight, exactly as for scenarios.</summary>
    public async Task<SyntheticGenerationResult> GenerateAsync(SyntheticGenerationOptions options, MessageDocument? target = null,
        MessageInsertionPosition position = MessageInsertionPosition.End, IProgress<SyntheticGenerationProgress>? progress = null, CancellationToken token = default,
        bool automaticSeed = false)
    {
        ArgumentNullException.ThrowIfNull(options); var normalized = options.Normalize();
        // As with scenarios, do not freeze seed-dependent automatic facilities
        // against the preflight seed. Explicit overrides remain on the draft.
        return await GenerateInternalAsync(automaticSeed ? options : normalized, null, target, position, progress, token, automaticSeed);
    }

    /// <summary>Generates a coherent whole scenario batch using the existing captured-target transaction.
    /// When automaticSeed is true, replaces Options.Seed exactly once after preflight with a cryptographically random value
    /// (or the injected provider's value). Result options and report retain that actual seed for replay.</summary>
    public Task<SyntheticGenerationResult> GenerateScenariosAsync(SyntheticScenarioOptions options, MessageDocument? target = null,
        MessageInsertionPosition position = MessageInsertionPosition.End, IProgress<SyntheticGenerationProgress>? progress = null, CancellationToken token = default,
        bool automaticSeed = false)
    {
        ArgumentNullException.ThrowIfNull(options); var normalized = options.Normalize();
        // Options on the original result contract describe its transport seed/date
        // and total count. ScenarioOptions is authoritative for scenario content.
        var transport = new SyntheticGenerationOptions { Count = normalized.EstimateMessageCount(), Seed = normalized.Seed, BaseTimestamp = normalized.BaseTimestamp, Prefix = normalized.Prefix };
        // Keep the unresolved automatic transport defaults: normalizing a seed-42
        // preview would otherwise freeze its facility codes before choosing a seed.
        return GenerateInternalAsync(transport, automaticSeed ? options : normalized, target, position, progress, token, automaticSeed);
    }

    private async Task<SyntheticGenerationResult> GenerateInternalAsync(SyntheticGenerationOptions options, SyntheticScenarioOptions? scenario,
        MessageDocument? target, MessageInsertionPosition position, IProgress<SyntheticGenerationProgress>? progress, CancellationToken token, bool automaticSeed = false)
    {
        token.ThrowIfCancellationRequested();
        if (!Enum.IsDefined(position)) throw new ArgumentOutOfRangeException(nameof(position));
        var revision = target?.ContentVersion;
        void CheckTarget()
        {
            if (target is null) return;
            if (!documents.Documents.Contains(target) || target.ContentVersion != revision)
                throw new InvalidOperationException("The target document or selection changed during generation. Review it and generate again.");
            if (target.InsertionUnavailableReason is { } reason) throw new InvalidOperationException(reason);
            if (options.Count > MessageFileIndex.MaxIndexedMessages - target.MessageCount) throw new Hl7LimitException("IndexedMessages");
            if (target.ActiveMessageId is null && position is MessageInsertionPosition.BeforeCurrent or MessageInsertionPosition.AfterCurrent)
                throw new InvalidOperationException("Select a current message, or insert at Beginning/End.");
        }
        CheckTarget();
        if (automaticSeed)
        {
            token.ThrowIfCancellationRequested();
            var resolvedSeed = nextSeed();
            token.ThrowIfCancellationRequested();
            if (scenario is not null) scenario = (scenario with { Seed = resolvedSeed }).Normalize();
            options = (options with { Seed = resolvedSeed }).Normalize();
        }
        var watch = Stopwatch.StartNew();
        var notification = new InlineProgress<int>(count =>
            {
                // Bound dispatcher notifications without changing generation or cancellation checks.
                if (count == 1 || count == options.Count || count % 25 == 0) progress?.Report(new("Generating", count, options.Count));
            });
        SyntheticMessageBatch? batch = null; SyntheticScenarioBatch? scenarioBatch = null;
        await Task.Run(() =>
        {
            if (scenario is null) batch = SyntheticMessageGenerator.Generate(options, token, notification);
            else scenarioBatch = SyntheticScenarioGenerator.Generate(scenario, token, notification);
        }, token);
        var generationTime = watch.Elapsed; watch.Restart(); token.ThrowIfCancellationRequested(); CheckTarget();
        MessageDocument? owned = null;
        try
        {
            if (target is null)
            {
                var snapshot = await Task.Run(async () =>
                {
                    using var empty = new MemoryStream(); return await SourceSnapshot.CaptureAsync(empty, token);
                }, token);
                try { owned = new(Guid.NewGuid().ToString("N"), scenario?.SuggestedFileName ?? options.SuggestedFileName, snapshot, MessageFileIndex.Build(snapshot, token)); }
                catch { snapshot.Dispose(); throw; }
            }
            var destination = target ?? owned!;
            var publicationProgress = new InlineProgress<DocumentSaveProgress>(p => progress?.Report(new(p.Phase, p.CompletedBytes, p.TotalBytes)));
            var ids = scenarioBatch is not null
                ? await destination.InsertGeneratedMessagesAsync(scenarioBatch, position, publicationProgress, token)
                : await destination.InsertGeneratedMessagesAsync(batch!, position, publicationProgress, token);
            var summary = scenarioBatch?.Summary;
            var report = new SyntheticGenerationReport(scenario?.Label ?? "Standalone " + options.TypeLabel,
                scenario is null ? options.GeneratorVersion : SyntheticScenarioGenerator.Version, options.Seed, options.BaseTimestamp,
                summary?.Patients ?? options.Count, summary?.Encounters ?? options.Count,
                summary?.Orders ?? (options.MessageType is SyntheticMessageType.OrmO01 or SyntheticMessageType.OruR01 ? options.Count : 0),
                summary?.Results ?? (options.MessageType == SyntheticMessageType.OruR01 ? options.Count : 0),
                summary?.Observations ?? (options.MessageType == SyntheticMessageType.OruR01 ? options.Count * 3 : 0),
                ids.Count, summary?.MessageTypes ?? new System.Collections.ObjectModel.ReadOnlyDictionary<string, int>(new Dictionary<string, int> { [options.TypeLabel] = options.Count }),
                generationTime, watch.Elapsed, target is null)
            {
                DemographicStyle = scenario?.DemographicStyle ?? options.DemographicStyle,
                SeedWasAutomatic = automaticSeed,
                Warnings = scenario?.Overrides.ObservationValue is not null || scenario?.Overrides.OrderCode is not null
                    ? Array.AsReadOnly(new[] { "Custom numeric values or local order codes retain the chosen synthetic panel; review interface expectations." }) : []
            };
            if (owned is not null)
            {
                await owned.EditRefresh; token.ThrowIfCancellationRequested();
                owned.SetSyntheticGenerationReport(report); owned.MarkGeneratedUnsaved(); documents.Open(owned); owned = null;
            }
            else destination.SetSyntheticGenerationReport(report);
            destination.IsEditMode = true;
            return new(destination, ids, options, target is null, generationTime, report.PublicationTime) { ScenarioOptions = scenario, Report = report };
        }
        finally { if (owned is not null) { owned.Dispose(); await owned.Disposal; } }
    }
    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T> { public void Report(T value) => report(value); }
}
