using System.ComponentModel;
using HL7Workbench.Core;

namespace HL7Workbench.Workspace;

/// <summary>Session-only import/browse/selection lifetime; immutable captures and generation checks prevent late publication after clear/close.</summary>
public sealed class SyntheticPatientsSession(DocumentManager documents, FhirImportLimits? limits = null) : INotifyPropertyChanged, IDisposable
{
    public const int PageSize = 100;
    private readonly CancellationTokenSource lifetime = new();
    private CancellationTokenSource? operation;
    private bool disposed; private long revision;
    private FhirGenerationPlan? plan; private long planRevision;
    private readonly HashSet<int> selectedEncounters = [];
    private readonly HashSet<FhirEventSelection> selectedEvents = [];
    private FhirResource[] matchingPatients = [];
    public FhirImportSession? Data { get; private set; }
    public int? PatientKey { get; private set; }
    public IReadOnlyList<FhirResource> PatientPage { get; private set; } = [];
    public IReadOnlyList<FhirResource> EncounterPage { get; private set; } = [];
    public IReadOnlyList<FhirTimelineEvent> TimelinePage { get; private set; } = [];
    public int PatientPageIndex { get; private set; }
    public int EncounterPageIndex { get; private set; }
    public int TimelinePageIndex { get; private set; }
    public int PatientPageCount => Pages(matchingPatients.Length);
    public int MatchingPatients => matchingPatients.Length;
    public int EncounterCount => Data is { } data && PatientKey is { } patient ? data.Encounters(patient).Count : 0;
    public int EncounterPageCount => Pages(EncounterCount);
    public int TimelinePageCount => Pages(Data is { } data && PatientKey is { } patient ? data.Timeline(patient).Count : 0);
    public IReadOnlySet<int> SelectedEncounters => new HashSet<int>(selectedEncounters);
    public IReadOnlySet<FhirEventSelection> SelectedEvents => new HashSet<FhirEventSelection>(selectedEvents);
    public FhirGenerationReport? Preview => plan?.Preview;
    public FhirGenerationScope Scope { get; private set; } = FhirGenerationScope.SelectedPatient;
    public string PreviewUnavailableReason => disposed ? "This synthetic session is closed." : IsBusy ? "Wait for the current operation or Cancel." :
        Data is null ? "Import synthetic FHIR Bundles before previewing." :
        Scope != FhirGenerationScope.EntireImport && PatientKey is null ? "Select a patient. Selected Patient includes their complete supported history." :
        Scope == FhirGenerationScope.SelectedEvents && selectedEncounters.Count + selectedEvents.Count == 0 ? "Selected Events: select at least one encounter or timeline event." : "";
    public string GenerationUnavailableReason => PreviewUnavailableReason.Length > 0 ? PreviewUnavailableReason :
        Preview is null ? "Preview generation to calculate eligibility and output size." :
        Preview.Messages == 0 ? "No eligible messages. Review ineligible events and skipped/unsupported reasons." :
        Preview.Limits.Count > 0 ? "Output limit exceeded: " + string.Join(", ", Preview.Limits) + ". Narrow the scope; no partial output will be published." : "";
    public bool IsBusy { get; private set; }
    public bool LastCanceled { get; private set; }
    public string Status { get; private set; } = "Confirm synthetic/test data, then import local FHIR R4 JSON.";
    public string PatientFilter { get; private set; } = "";
    public Task RunningTask { get; private set; } = Task.CompletedTask;
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<FhirGenerationResult>? Generated;
    private static int Pages(int count) => Math.Max(1, (count + PageSize - 1) / PageSize);
    public Task ImportFilesAsync(IEnumerable<string> paths, bool confirmed, CancellationToken token = default) =>
        ImportAsync(ct => FhirPatientImport.FilesAsync(paths, confirmed, limits, token: ct), token);
    public Task ImportFolderAsync(string path, bool confirmed, CancellationToken token = default) =>
        ImportAsync(ct => FhirPatientImport.FolderAsync(path, confirmed, limits, token: ct), token);
    private Task ImportAsync(Func<CancellationToken, Task<FhirImportSession>> import, CancellationToken token)
    {
        if (disposed || IsBusy) return Task.CompletedTask;
        return RunningTask = OperateAsync("Import", async ct =>
        {
            var captured = revision; var data = await import(ct); ct.ThrowIfCancellationRequested();
            if (disposed || captured != revision) return;
            Data = data; PatientKey = null; PatientFilter = ""; matchingPatients = data.Patients.OrderBy(p => p.Person?.DisplayName, StringComparer.OrdinalIgnoreCase).ThenBy(p => p.Id, StringComparer.Ordinal).ToArray();
            revision++; plan = null; selectedEncounters.Clear(); selectedEvents.Clear(); PatientPageIndex = EncounterPageIndex = TimelinePageIndex = 0;
            RefreshPages(); Status = $"Imported {data.Summary.Files:N0} files, {data.Summary.Resources:N0} resources, {data.Patients.Count:N0} patients. Review reference/date/unsupported counts. Synthetic status is user-confirmed.";
        }, token);
    }
    public Task FilterPatientsAsync(string literal, CancellationToken token = default)
    {
        if (disposed || IsBusy || Data is not { } data) return Task.CompletedTask;
        if (literal.Length > 256) { Status = "Patient search is limited to 256 characters."; Changed(); return Task.CompletedTask; }
        return RunningTask = OperateAsync("Patient filter", async ct =>
        {
            var matches = await Task.Run(() => data.Patients.Where(p =>
            { ct.ThrowIfCancellationRequested(); return p.Id.Contains(literal, StringComparison.OrdinalIgnoreCase) || p.Person!.Identifier.Contains(literal, StringComparison.OrdinalIgnoreCase) || p.Person.DisplayName.Contains(literal, StringComparison.OrdinalIgnoreCase); })
                .OrderBy(p => p.Person?.DisplayName, StringComparer.OrdinalIgnoreCase).ThenBy(p => p.Id, StringComparer.Ordinal).ToArray(), ct);
            ct.ThrowIfCancellationRequested(); if (disposed) return; matchingPatients = matches; PatientFilter = literal; PatientPageIndex = 0;
            if (PatientKey is { } selected && !matches.Any(p => p.Key == selected))
            { PatientKey = null; selectedEncounters.Clear(); selectedEvents.Clear(); revision++; plan = null; }
            RefreshPages(); Status = $"{matches.Length:N0} matching synthetic patients. Search stays in this session.";
        }, token);
    }
    public void SelectPatient(int? key)
    {
        if (disposed || IsBusy || key == PatientKey || key is not null && (Data is null || Data.Resource(key.Value).Kind != FhirResourceKind.Patient)) return;
        PatientKey = key; EncounterPageIndex = TimelinePageIndex = 0; selectedEncounters.Clear(); selectedEvents.Clear(); InvalidatePreview(); RefreshPages(); Changed();
    }
    public void SelectEncounters(IEnumerable<int> keys)
    {
        if (disposed || IsBusy || Data is not { } data || PatientKey is not { } patient) return;
        var selected = keys.Take(257).Distinct().ToArray();
        if (selected.Length > 256 || selected.Any(k => k < 1 || k > data.Resources.Count || data.Resource(k).Kind != FhirResourceKind.Encounter || data.PatientOf(k) != patient))
        { Status = "Select at most 256 encounters belonging to this patient. Prior selection retained; preview again."; InvalidatePreview(); return; }
        selectedEncounters.Clear(); selectedEncounters.UnionWith(selected); InvalidatePreview();
    }
    public void SelectEvents(IEnumerable<FhirEventSelection> keys)
    {
        if (disposed || IsBusy || Data is not { } data || PatientKey is not { } patient) return;
        var selected = keys.Take(257).Distinct().ToArray();
        if (selected.Length > 256 || selected.Any(k => k.ResourceKey < 1 || k.ResourceKey > data.Resources.Count || data.PatientOf(k.ResourceKey) != patient || !data.Resource(k.ResourceKey).Times.Any(t => t.Kind == k.Kind)))
        { Status = "Select at most 256 timeline events belonging to this patient. Prior selection retained; preview again."; InvalidatePreview(); return; }
        selectedEvents.Clear(); selectedEvents.UnionWith(selected); InvalidatePreview();
    }
    public void InvalidatePreview()
    { if (disposed) return; revision++; plan = null; Changed(); }
    public void SetScope(FhirGenerationScope scope)
    {
        if (disposed || IsBusy || !Enum.IsDefined(scope) || scope == Scope) return;
        // Manual selection belongs to the selected patient, independently of the
        // current generation scope. Switching away and back must not discard it.
        Scope = scope; InvalidatePreview();
    }
    public void ClearSelection()
    { if (disposed || IsBusy) return; selectedEncounters.Clear(); selectedEvents.Clear(); InvalidatePreview(); }
    public void SetPatientPage(int page) { if (!disposed && !IsBusy && page >= 0 && page < PatientPageCount) { PatientPageIndex = page; RefreshPages(); Changed(); } }
    public void SetEncounterPage(int page) { if (!disposed && !IsBusy && page >= 0 && page < EncounterPageCount) { EncounterPageIndex = page; RefreshPages(); Changed(); } }
    public void SetTimelinePage(int page) { if (!disposed && !IsBusy && page >= 0 && page < TimelinePageCount) { TimelinePageIndex = page; RefreshPages(); Changed(); } }
    private void RefreshPages()
    {
        PatientPage = Array.AsReadOnly(matchingPatients.Skip(PatientPageIndex * PageSize).Take(PageSize).ToArray());
        EncounterPage = Data is { } data && PatientKey is { } patient ? Array.AsReadOnly(data.Encounters(patient).Skip(EncounterPageIndex * PageSize).Take(PageSize).ToArray()) : [];
        TimelinePage = Data is { } source && PatientKey is { } patientKey ? Array.AsReadOnly(source.Timeline(patientKey).Skip(TimelinePageIndex * PageSize).Take(PageSize).ToArray()) : [];
    }
    public Task PreviewAsync(uint seed, bool adt, bool oru, DateTimeOffset transportTimestamp, CancellationToken token = default)
    {
        if (PreviewUnavailableReason.Length > 0 || Data is not { } data) return Task.CompletedTask;
        var request = new FhirGenerationRequest { Scope = Scope, PatientKey = PatientKey ?? 0, Encounters = selectedEncounters.ToArray(), Events = selectedEvents.ToArray(), Adt = adt, Oru = oru, Seed = seed, TransportTimestamp = transportTimestamp };
        var version = revision;
        return RunningTask = OperateAsync("Generation preview", async ct =>
        {
            var pending = await Task.Run(() => FhirGenerationPlan.Create(data, request, ct), ct); ct.ThrowIfCancellationRequested();
            if (disposed || version != revision) return; plan = pending; planRevision = version;
            Status = pending.Preview.Limits.Count > 0 ? "Output limit exceeded: " + string.Join(", ", pending.Preview.Limits) + ". Narrow the scope; no partial output published." :
                pending.Preview.Messages == 0 ? "No eligible messages. Review ineligible events and skipped/unsupported reasons." :
                $"Preview: {pending.Preview.Messages:N0} supported messages for {pending.Preview.PatientsIncluded:N0} patients. Review warnings before Generate.";
        }, token);
    }
    public Task GenerateAsync(CancellationToken token = default)
    {
        if (disposed || IsBusy || plan is not { } captured || planRevision != revision || !captured.Preview.CanGenerate) return Task.CompletedTask;
        var version = revision; var sessionId = Data?.Id;
        return RunningTask = OperateAsync("Generation", async ct =>
        {
            var output = await new FhirGenerationPublisher(documents).PublishAsync(captured, token: ct, captureIsCurrent: () => !disposed && revision == version && Data?.Id == sessionId);
            Status = $"Generated {output.MessageIds.Count:N0} messages into a new unsaved document. Review its Generation Report; source files unchanged."; Generated?.Invoke(output);
        }, token);
    }
    private async Task OperateAsync(string name, Func<CancellationToken, Task> action, CancellationToken token)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token); operation = cancellation;
        IsBusy = true; LastCanceled = false; Status = name + " running…"; Changed();
        try { await action(cancellation.Token); }
        catch (OperationCanceledException) { LastCanceled = true; if (!disposed) Status = name + " canceled; no partial session/output published. Prior completed session retained unless cleared."; }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or Hl7LimitException or FormatException)
        { if (!disposed) Status = name + " failed. Review synthetic confirmation, selection, local access, supported R4 data and limits; prior session/source files retained; no partial output published."; }
        finally { operation = null; IsBusy = false; if (!disposed) Changed(); }
    }
    public void Cancel() => operation?.Cancel();
    public void Clear()
    {
        if (disposed) return; revision++; operation?.Cancel(); Data = null; plan = null; PatientKey = null; Scope = FhirGenerationScope.SelectedPatient; PatientFilter = ""; matchingPatients = [];
        selectedEncounters.Clear(); selectedEvents.Clear(); PatientPageIndex = EncounterPageIndex = TimelinePageIndex = 0; RefreshPages();
        Status = "Imported session cleared. Previously generated HL7 documents remain independent."; Changed();
    }
    private void Changed() => PropertyChanged?.Invoke(this, new(null));
    public void Dispose()
    {
        if (disposed) return; Clear(); disposed = true; lifetime.Cancel(); operation?.Cancel(); lifetime.Dispose(); PropertyChanged = null; Generated = null;
    }
}
