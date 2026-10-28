using System.ComponentModel;
using System.Globalization;
using HL7Workbench.Core;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.ViewModels;

/// <summary>Picker/UI state for one disposable local FHIR import. No persistence or Dock dependency.</summary>
public sealed class SyntheticPatientsViewModel : INotifyPropertyChanged, IDisposable
{
    private bool disposed, choosing;
    private long epoch;
    private string seed = "42", timestamp = "2030-01-01T00:00:00+00:00", error = "";
    private int mode;
    public SyntheticPatientsSession Session { get; }
    public bool CanWork => !disposed && !choosing && !Session.IsBusy;
    public bool IsBusy => choosing || Session.IsBusy;
    public string Status => error.Length > 0 ? error : choosing ? "Select local FHIR JSON; no data imported until selection completes." : Session.Status;
    internal int ClinicalTab { get; set; }
    internal double PatientProportion { get; set; } = .28;
    internal bool GenerationExpanded { get; set; }
    internal bool AdvancedExpanded { get; set; }
    internal bool PreviewExpanded { get; set; }
    internal bool ClinicalExpanded { get; set; } = true;
    internal bool ImportDetailsExpanded { get; set; }
    internal Guid? InspectedSession { get; set; }
    internal int? InspectedResource { get; set; }
    public string Seed { get => seed; set { if (seed == value || value.Length > 10) return; seed = value; OptionsChanged(); } }
    public string TransportTimestamp { get => timestamp; set { if (timestamp == value || value.Length > 40) return; timestamp = value; OptionsChanged(); } }
    public int Mode { get => mode; set { if (mode == value || value is < 0 or > 2) return; mode = value; OptionsChanged(); } }
    public FhirGenerationScope Scope { get => Session.Scope; set => Session.SetScope(value); }
    public Task ActionTask { get; private set; } = Task.CompletedTask;
    public Func<Task<IReadOnlyList<string>>> ChooseFilesAsync { get; set; } = () => Task.FromResult<IReadOnlyList<string>>([]);
    public Func<Task<string?>> ChooseFolderAsync { get; set; } = () => Task.FromResult<string?>(null);
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? Shown;
    public void NotifyShown() => Shown?.Invoke();
    public SyntheticPatientsViewModel(DocumentManager documents, FhirImportLimits? limits = null)
    { Session = new(documents, limits); Session.PropertyChanged += SessionChanged; }
    public string ConfigurationError => TrySettings(out _, out _) ? "" : "Use an unsigned seed and a complete transport timestamp with seconds and an offset in UTC years 1900–9998.";
    public bool CanPreview => CanWork && ConfigurationError.Length == 0 && Session.PreviewUnavailableReason.Length == 0;
    public bool CanGenerate => CanWork && ConfigurationError.Length == 0 && Session.GenerationUnavailableReason.Length == 0;
    private void OptionsChanged() { error = ConfigurationError; Session.InvalidatePreview(); Changed(); }
    private void SessionChanged(object? sender, PropertyChangedEventArgs e) => Changed();
    public Task ImportFilesAsync() => ImportAsync(false);
    public Task ImportFolderAsync() => ImportAsync(true);
    private Task ImportAsync(bool folder)
    {
        if (!CanWork) return Task.CompletedTask;
        return ActionTask = Core();
        async Task Core()
        {
            choosing = true; error = ""; var captured = epoch; Changed();
            try
            {
                if (folder)
                {
                    var path = await ChooseFolderAsync(); if (disposed || epoch != captured || path is null) return;
                    choosing = false; await Session.ImportFolderAsync(path);
                }
                else
                {
                    var paths = await ChooseFilesAsync(); if (disposed || epoch != captured || paths.Count == 0) return;
                    choosing = false; await Session.ImportFilesAsync(paths);
                }
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
            { if (!disposed) error = "Local import selection unavailable; source files and prior session retained."; }
            finally { choosing = false; if (!disposed) Changed(); }
        }
    }
    private bool TrySettings(out uint value, out DateTimeOffset timestampValue)
    {
        timestampValue = default;
        var time = FhirClinicalTime.Parse(timestamp);
        if (!uint.TryParse(seed, NumberStyles.None, CultureInfo.InvariantCulture, out value) || !time.IsInstant)
        { return false; }
        try { timestampValue = new SyntheticGenerationOptions { BaseTimestamp = time.Instant!.Value }.Normalize().BaseTimestamp; return true; }
        catch (ArgumentException) { return false; }
    }
    public Task PreviewAsync() => ExecuteGeneration(false);
    public Task GenerateAsync() => ExecuteGeneration(true);
    private Task ExecuteGeneration(bool publish)
    {
        if (!CanWork) return Task.CompletedTask;
        if (!TrySettings(out var value, out var at)) { error = ConfigurationError; Changed(); return Task.CompletedTask; }
        error = "";
        return ActionTask = publish ? Session.GenerateAsync(value, mode != 2, mode != 1, at) : Session.PreviewAsync(value, mode != 2, mode != 1, at);
    }
    public void Cancel() { epoch++; if (choosing) error = "Import selection canceled."; Session.Cancel(); Changed(); }
    public void Clear() { epoch++; error = ""; InspectedSession = null; InspectedResource = null; Session.Clear(); Changed(); }
    private void Changed() => PropertyChanged?.Invoke(this, new(null));
    public void Dispose()
    {
        if (disposed) return; disposed = true; epoch++; Session.Dispose(); Session.PropertyChanged -= SessionChanged; InspectedSession = null; InspectedResource = null;
        ChooseFilesAsync = () => Task.FromResult<IReadOnlyList<string>>([]); ChooseFolderAsync = () => Task.FromResult<string?>(null); PropertyChanged = null; Shown = null;
    }
}
