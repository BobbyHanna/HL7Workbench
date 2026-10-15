using System.Text;
using HL7Workbench.Core;

namespace HL7Workbench.Workspace;

/// <summary>Bounded, document-owned working text. History stores text deltas, never parsed models.</summary>
internal sealed class EditableMessageBuffer
{
    internal sealed record Change(int Offset, string Removed, string Inserted);
    private readonly List<Change> undo = [];
    private readonly List<Change> redo = [];
    private readonly byte[] originalBytes;
    private readonly Encoding encoding;
    private readonly byte[] bom;
    private int maximumVersionLength;
    public string OriginalText { get; }
    public string Text { get; private set; }
    public string SavedText { get; private set; }
    public bool IsDirty => Text != SavedText;
    public bool DiffersFromOriginal => Text != OriginalText;
    public bool CanUndo => undo.Count > 0;
    public bool CanRedo => redo.Count > 0;
    // Reserve both working/save-point strings at the largest retained revision size,
    // so Undo and Save cannot exceed a budget that was checked only on forward edits.
    public long RetainedCharacters => OriginalText.Length + 2L * maximumVersionLength +
        undo.Concat(redo).Sum(c => (long)c.Removed.Length + c.Inserted.Length);
    public int HistoryCount => undo.Count + redo.Count;
    public int HistoryCountAfterReplace => undo.Count + 1;
    public EditableMessageBuffer(Hl7Message message)
    {
        OriginalText = Text = SavedText = message.RawText;
        maximumVersionLength = Text.Length;
        originalBytes = Hl7Serializer.Serialize(message);
        encoding = message.Encoding; encoding.EncoderFallback = EncoderFallback.ExceptionFallback;
        bom = message.ByteOrderMark;
    }
    public Change Plan(string text)
    {
        var prefix = 0;
        while (prefix < Text.Length && prefix < text.Length && Text[prefix] == text[prefix]) prefix++;
        // The splice must not separate either version's UTF-16 surrogate pair.
        if (prefix > 0 && char.IsHighSurrogate(Text[prefix - 1])) prefix--;
        var suffix = 0;
        while (suffix < Text.Length - prefix && suffix < text.Length - prefix && Text[^(suffix + 1)] == text[^(suffix + 1)]) suffix++;
        if (suffix > 0 && char.IsLowSurrogate(Text[Text.Length - suffix])) suffix--;
        return new(prefix, Text.Substring(prefix, Text.Length - prefix - suffix), text.Substring(prefix, text.Length - prefix - suffix));
    }
    public long CostAfter(string text, Change change) => RetainedCharacters + 2L * (Math.Max(maximumVersionLength, text.Length) - maximumVersionLength) -
        redo.Sum(c => (long)c.Removed.Length + c.Inserted.Length) + change.Removed.Length + change.Inserted.Length;
    public void Replace(string text, Change change) { undo.Add(change); redo.Clear(); Text = text; maximumVersionLength = Math.Max(maximumVersionLength, text.Length); }
    public void Undo()
    {
        var change = undo[^1]; undo.RemoveAt(undo.Count - 1);
        Text = string.Concat(Text.AsSpan(0, change.Offset), change.Removed, Text.AsSpan(change.Offset + change.Inserted.Length)); redo.Add(change);
    }
    public void Redo()
    {
        var change = redo[^1]; redo.RemoveAt(redo.Count - 1);
        Text = string.Concat(Text.AsSpan(0, change.Offset), change.Inserted, Text.AsSpan(change.Offset + change.Removed.Length)); undo.Add(change);
    }
    public void MarkSaved() => SavedText = Text;
    public byte[] Serialize(string? workingText = null)
    {
        var text = workingText ?? Text;
        if (text == OriginalText) return originalBytes;
        // Diff against the immutable original, not the last saved output. Snapshot ranges
        // remain valid through every Save As and undo across a save point.
        var prefix = 0;
        while (prefix < OriginalText.Length && prefix < text.Length && OriginalText[prefix] == text[prefix]) prefix++;
        if (prefix > 0 && char.IsHighSurrogate(OriginalText[prefix - 1])) prefix--;
        var suffix = 0;
        while (suffix < OriginalText.Length - prefix && suffix < text.Length - prefix && OriginalText[^(suffix + 1)] == text[^(suffix + 1)]) suffix++;
        if (suffix > 0 && char.IsLowSurrogate(OriginalText[OriginalText.Length - suffix])) suffix--;
        var start = bom.Length + encoding.GetByteCount(OriginalText.AsSpan(0, prefix));
        var end = originalBytes.Length - encoding.GetByteCount(OriginalText.AsSpan(OriginalText.Length - suffix));
        var replacement = encoding.GetBytes(text.Substring(prefix, text.Length - prefix - suffix));
        var result = new byte[start + replacement.Length + originalBytes.Length - end];
        originalBytes.AsSpan(0, start).CopyTo(result);
        replacement.CopyTo(result, start);
        originalBytes.AsSpan(end).CopyTo(result.AsSpan(start + replacement.Length));
        return result;
    }
    public Hl7Message Parse(string? text = null)
    {
        // Working coordinates are text coordinates. Parsing the encoded draft could
        // mistake an intentionally typed leading U+FEFF for an encoding preamble.
        return Hl7Parser.Parse(text ?? Text, new() { Encoding = encoding,
            MaxMessageCharacters = MessagePresentation.MaximumEditorCharacters, MaxSegmentLength = MessageDocument.MaximumEditableLineCharacters });
    }
}
