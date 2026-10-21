using System.ComponentModel;
using System.Globalization;
using HL7Workbench.Core;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.ViewModels;

/// <summary>Picker/UI preferences for one disposable synthetic import. No persistence or Dock dependency.</summary>
public sealed class SyntheticPatientsViewModel : INotifyPropertyChanged, IDisposable
{
    private bool disposed, choosing, confirmed;
    private long epoch;
    private string seed = "42", timestamp = "2030-01-01T00:00:00+00:00", error = "";
    private int mode;
    public SyntheticPatientsSession Session { get; }
    public bool CanWork => !disposed && !choosing && !Session.IsBusy;
    public bool IsBusy => choosing || Session.IsBusy;
    public string Status => error.Length > 0 ? error : choosing ? "Select local synthetic FHIR JSON; no data imported until selection completes." : Session.Status;
    public bool Confirmed { get => confirmed; set { confirmed = value; Changed(); } }
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
    private void OptionsChanged() { error = ""; Session.InvalidatePreview(); Changed(); }
    private void SessionChanged(object? sender, PropertyChangedEventArgs e) => Changed();
    public Task ImportFilesAsync() => ImportAsync(false);
    public Task ImportFolderAsync() => ImportAsync(true);
    private Task ImportAsync(bool folder)
    {
        if (!CanWork || !confirmed) return Task.CompletedTask;
        return ActionTask = Core();
        async Task Core()
        {
            choosing = true; error = ""; var captured = epoch; Changed();
            try
            {
                if (folder)
                {
                    var path = await ChooseFolderAsync(); if (disposed || epoch != captured || path is null) return;
                    choosing = false; await Session.ImportFolderAsync(path, true);
                }
                else
                {
                    var paths = await ChooseFilesAsync(); if (disposed || epoch != captured || paths.Count == 0) return;
                    choosing = false; await Session.ImportFilesAsync(paths, true);
                }
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
            { if (!disposed) error = "Local import selection unavailable; source files and prior session retained."; }
            finally { choosing = false; confirmed = false; if (!disposed) Changed(); }
        }
    }
    public Task PreviewAsync()
    {
        if (!CanWork) return Task.CompletedTask; error = "";
        var time = FhirClinicalTime.Parse(timestamp);
        if (!uint.TryParse(seed, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || !time.IsInstant)
        { error = "Use an unsigned seed and complete transport timestamp with seconds and an offset."; Changed(); return Task.CompletedTask; }
        return ActionTask = Session.PreviewAsync(value, mode != 2, mode != 1, time.Instant!.Value);
    }
    public Task GenerateAsync() => CanWork ? ActionTask = Session.GenerateAsync() : Task.CompletedTask;
    public void Clear() { epoch++; error = ""; confirmed = false; Session.Clear(); Changed(); }
    private void Changed() => PropertyChanged?.Invoke(this, new(null));
    public void Dispose()
    {
        if (disposed) return; disposed = true; epoch++; confirmed = false; Session.Dispose(); Session.PropertyChanged -= SessionChanged;
        ChooseFilesAsync = () => Task.FromResult<IReadOnlyList<string>>([]); ChooseFolderAsync = () => Task.FromResult<string?>(null); PropertyChanged = null; Shown = null;
    }
}
