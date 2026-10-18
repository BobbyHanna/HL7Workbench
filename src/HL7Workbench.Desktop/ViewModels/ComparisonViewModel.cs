using System.ComponentModel;
using System.Collections.Specialized;
using HL7Workbench.Core;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.ViewModels;

public enum ComparisonPresentation { Differences, SideBySide }

/// <summary>Session-only comparison state, independent of Dock and the lifetime of its reconstructed view.</summary>
public sealed class ComparisonViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly DocumentManager documents;
    private CancellationTokenSource? request;
    private long generation;
    private bool disposed;
    private bool changingInputs;
    // Retain coordinates only across recomputation, never a previous result's source values.
    private sealed record SelectionKey(Hl7DifferenceKind Kind, string? BeforePath, string? AfterPath);
    private SelectionKey? selectionAnchor;
    private static SelectionKey? KeyFor(Hl7Difference? difference) => difference is null ? null : new(difference.Kind, difference.Before?.Path, difference.After?.Path);
    // Initialize once per session; visual reattachment and Dock reveal reuse this state.
    private ComparisonPresentation presentation = ComparisonPresentation.SideBySide;
    private readonly HashSet<MessageDocument> observed = [];
    public event PropertyChangedEventHandler? PropertyChanged;
    public DocumentManager Documents => documents;
    public ComparisonInput InputA { get; } = new();
    public ComparisonInput InputB { get; } = new();
    public ComparisonPresentation Presentation
    {
        get => presentation;
        set { if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value)); if (presentation == value) return; presentation = value; PropertyChanged?.Invoke(this, new(nameof(Presentation))); }
    }
    public ComparisonSource? Before { get; private set; }
    public ComparisonSource? After { get; private set; }
    public Hl7ComparisonOptions Options { get; private set; } = new();
    public MessageComparison? Comparison { get; private set; }
    public IReadOnlyList<Hl7Difference> Differences => Comparison?.Result.Differences ?? [];
    public Hl7Difference? Selected { get; private set; }
    public bool IsBusy { get; private set; }
    public string Status { get; private set; } = "Paste one HL7 message into A and B, or choose open messages using the source controls.";
    public string Count => $"{Differences.Count:N0} differences" + (Options == new Hl7ComparisonOptions() ? "" : " · ignore options enabled");
    public Task Pending { get; private set; } = Task.CompletedTask;
    public ComparisonViewModel(DocumentManager documents)
    {
        this.documents = documents; InputA.PropertyChanged += InputChanged; InputB.PropertyChanged += InputChanged;
        ((INotifyCollectionChanged)documents.Documents).CollectionChanged += DocumentsChanged;
    }
    private void DocumentsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var invalidated = false; var changed = false;
        changingInputs = true;
        try
        {
            foreach (var input in new[] { InputA, InputB })
                if (input.Document is { } document && !documents.Documents.Contains(document))
                { invalidated |= input.Kind != ComparisonSourceKind.PastedText; changed = true; input.SelectDocument(null); }
        }
        finally { changingInputs = false; }
        if (invalidated) { UpdateSources(false); Status = "A source document closed. Choose another open source."; Notify(); }
        else if (changed) Notify();
    }
    public void SetSources(ComparisonSource? before, ComparisonSource? after)
    {
        if (disposed || Before == before && After == after) return;
        changingInputs = true;
        try { InputA.SelectSource(before); InputB.SelectSource(after); }
        finally { changingInputs = false; }
        UpdateSources(false);
    }
    private void InputChanged(object? sender, PropertyChangedEventArgs e)
    { if (!changingInputs && !disposed) UpdateSources(true); }
    private void UpdateSources(bool debounce)
    {
        Invalidate("Sources changed. Comparison will refresh when both sides are ready.");
        foreach (var d in observed) { d.PropertyChanged -= SourceChanged; d.Closed -= SourceClosed; }
        observed.Clear(); Before = InputA.Source; After = InputB.Source;
        foreach (var d in new[] { Before?.Document, After?.Document }.OfType<MessageDocument>().Distinct())
        { observed.Add(d); d.PropertyChanged += SourceChanged; d.Closed += SourceClosed; }
        Notify();
        if (debounce && Before is not null && After is not null)
        { request = new(); Pending = DebounceAsync(generation, request.Token); }
    }
    private async Task DebounceAsync(long version, CancellationToken token)
    {
        try { await Task.Delay(250, token); if (!disposed && version == generation) await CompareAsync(); }
        catch (OperationCanceledException) { }
    }
    public bool SetPastedText(bool before, string text)
    {
        if (disposed) return false;
        try { (before ? InputA : InputB).SetPastedText(text); return true; }
        catch (ComparisonInputException e) { Status = e.Message; Notify(); return false; }
    }
    public void ReportStatus(string status) { if (!disposed) { Status = status; Notify(); } }
    public void UseActive(bool before)
    {
        if (documents.ActiveDocument is not { MessageNumber: > 0 } d) return;
        var source = new ComparisonSource(d, d.MessageNumber);
        SetSources(before ? source : Before, before ? After : source);
    }
    public void CompareWithOriginal(MessageDocument? document = null)
    {
        var d = document ?? documents.ActiveDocument;
        if (d is null || d.MessageNumber < 1) return;
        if (!d.HasOriginalMessage) { ReportStatus("An inserted message has no Original Message during this open session, including after Save."); return; }
        SetSources(new(d, d.MessageNumber, true), new(d, d.MessageNumber)); Start();
    }
    public void SetOptions(Hl7ComparisonOptions options)
    { if (Options == options) return; Options = options; Invalidate("Ignore options changed."); Notify(); if (Before is not null && After is not null) Start(); }
    public void Start() => Pending = CompareAsync();
    public async Task CompareAsync()
    {
        if (disposed) return;
        Invalidate("Comparing…");
        if (Before is null || After is null) { Status = "Paste one HL7 message into each side, or choose both open-message sources."; Notify(); return; }
        var version = generation; var before = Before; var after = After;
        request = new(); var token = request.Token;
        IsBusy = true; Notify();
        try
        {
            var result = await MessageComparison.CreateAsync(before, after, Options, token);
            if (disposed || version != generation || !result.IsCurrent) { result.Dispose(); return; }
            Comparison = result;
            Selected = Differences.FirstOrDefault(d => selectionAnchor is not null && d.Kind == selectionAnchor.Kind && d.Before?.Path == selectionAnchor.BeforePath && d.After?.Path == selectionAnchor.AfterPath) ?? Differences.FirstOrDefault();
            selectionAnchor = KeyFor(Selected);
            Status = Differences.Count == 0 ? "No differences found." : "Select a difference to review it. F7 / Shift+F7 navigates.";
            if (result.BeforeStructure.Diagnostics.Count > 0 || result.AfterStructure.Diagnostics.Count > 0)
                Status += " Source has parser diagnostics; recovered structural coordinates may be provisional. Source text is unchanged.";
        }
        catch (OperationCanceledException) { if (version == generation) Status = "Comparison canceled."; }
        catch (ComparisonInputException error) { if (version == generation) Status = error.Message; }
        catch (Exception error) when (error is Hl7LimitException or IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        { if (version == generation) Status = error is Hl7LimitException ? "Comparison exceeds a structural or 20,000-difference limit. No partial result is shown." : "Comparison unavailable. Check that both sources are still open and safely decodable, then retry."; }
        finally { if (!disposed && version == generation) { IsBusy = false; Notify(); } }
    }
    public void Cancel() => Invalidate("Comparison canceled.");
    public void Select(Hl7Difference? difference)
    { if (difference is not null && !Differences.Contains(difference)) return; Selected = difference; selectionAnchor = KeyFor(difference); PropertyChanged?.Invoke(this, new(nameof(Selected))); }
    public void Move(int direction)
    {
        if (Differences.Count == 0) return;
        var index = Selected is null ? -1 : Differences.ToList().IndexOf(Selected);
        Select(Differences[(index + direction + Differences.Count) % Differences.Count]);
    }
    private void SourceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MessageDocument.SearchRevision)) Invalidate("Source changed; comparison is stale. Compare again.");
        else if (e.PropertyName == nameof(MessageDocument.DisplayTitle)) Notify();
    }
    private void SourceClosed(object? sender, EventArgs e)
    {
        SetSources(ReferenceEquals(Before?.Document, sender) ? null : Before, ReferenceEquals(After?.Document, sender) ? null : After);
        Status = "A source document closed. Choose another open source."; Notify();
    }
    private void Invalidate(string status)
    {
        selectionAnchor = KeyFor(Selected) ?? selectionAnchor;
        generation++; request?.Cancel(); request?.Dispose(); request = null;
        Comparison?.Dispose(); Comparison = null; Selected = null; IsBusy = false; Status = status; Notify();
    }
    private void Notify() => PropertyChanged?.Invoke(this, new(null));
    public void Dispose()
    {
        if (disposed) return; disposed = true; Invalidate("Comparison closed.");
        InputA.PropertyChanged -= InputChanged; InputB.PropertyChanged -= InputChanged;
        ((INotifyCollectionChanged)documents.Documents).CollectionChanged -= DocumentsChanged;
        InputA.Clear(); InputB.Clear(); selectionAnchor = null;
        foreach (var d in observed) { d.PropertyChanged -= SourceChanged; d.Closed -= SourceClosed; }
        observed.Clear(); Before = null; After = null;
    }
}
