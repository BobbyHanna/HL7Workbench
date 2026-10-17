using System.ComponentModel;

namespace HL7Workbench.Workspace;

public enum ComparisonSourceKind { OpenMessage, OriginalMessage, PastedText }

/// <summary>One session-only source slot. Switching kinds preserves its independent pasted draft and document choice.</summary>
public sealed class ComparisonInput : INotifyPropertyChanged
{
    public const int MaximumPastedCharacters = MessagePresentation.MaximumEditorCharacters;
    private ComparisonSourceKind kind = ComparisonSourceKind.PastedText;
    private MessageDocument? document;
    private int ordinal = 1;
    private string pastedText = "";
    public event PropertyChangedEventHandler? PropertyChanged;
    public long Revision { get; private set; }
    public ComparisonSourceKind Kind
    {
        get => kind;
        set { if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value)); if (kind == value) return; kind = value; Changed(); }
    }
    public MessageDocument? Document => document;
    public int Ordinal => ordinal;
    public string PastedText => pastedText;
    public ComparisonSource? Source => kind == ComparisonSourceKind.PastedText ?
        string.IsNullOrWhiteSpace(pastedText) ? null : ComparisonSource.FromPastedText(pastedText) :
        document is null ? null : new(document, ordinal, kind == ComparisonSourceKind.OriginalMessage);
    public string Label => kind == ComparisonSourceKind.PastedText ?
        pastedText.Length == 0 ? "Paste HL7 text…" : $"Pasted Text · temporary · {pastedText.Length:N0} characters" :
        document is null ? "Choose an open message…" : new ComparisonSource(document, ordinal, kind == ComparisonSourceKind.OriginalMessage).Label;
    public void SelectDocument(MessageDocument? value, int messageOrdinal = 1)
    {
        if (value is not null && (messageOrdinal < 1 || messageOrdinal > value.MessageCount)) throw new ArgumentOutOfRangeException(nameof(messageOrdinal));
        if (ReferenceEquals(document, value) && ordinal == messageOrdinal) return;
        document = value; ordinal = messageOrdinal; Changed();
    }
    /// <summary>Reject oversized edits atomically; never truncate or normalize clipboard text.</summary>
    public void SetPastedText(string text, bool activate = true)
    {
        ArgumentNullException.ThrowIfNull(text);
        ValidatePastedText(text);
        if (pastedText == text && (!activate || kind == ComparisonSourceKind.PastedText)) return;
        pastedText = text; if (activate) kind = ComparisonSourceKind.PastedText; Changed();
    }
    /// <summary>Selects an immutable source while retaining the inactive pasted draft.</summary>
    public void SelectSource(ComparisonSource? source)
    {
        if (source?.PastedText is { } text) { SetPastedText(text); return; }
        document = source?.Document; ordinal = source?.Ordinal ?? 1;
        kind = source?.Original == true ? ComparisonSourceKind.OriginalMessage : ComparisonSourceKind.OpenMessage;
        Changed();
    }
    public static void ValidatePastedText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length > MaximumPastedCharacters) throw new ComparisonInputException("Pasted Text is limited to 1 Mi UTF-16 characters per side. The previous draft was retained.");
        var start = 0;
        while (start < text.Length)
        {
            var length = text.AsSpan(start).IndexOfAny('\r', '\n'); if (length < 0) length = text.Length - start;
            if (length > MessageDocument.MaximumEditableLineCharacters) throw new ComparisonInputException("Pasted Text is limited to 16 Ki UTF-16 characters per line. The previous draft was retained.");
            start += length + 1;
        }
    }
    private void Changed() { Revision++; PropertyChanged?.Invoke(this, new(null)); }
    public void Clear()
    { pastedText = ""; document = null; ordinal = 1; kind = ComparisonSourceKind.PastedText; Changed(); }
}

/// <summary>Fixed user-facing input guidance; never includes pasted content.</summary>
public sealed class ComparisonInputException(string message) : InvalidOperationException(message);
