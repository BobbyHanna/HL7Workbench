using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.Viewer;

/// <summary>Every retained byte is accessible, including undecodable and unclassified content.</summary>
public sealed class SourceRegionInspector : StackPanel
{
    private readonly SourceSnapshot snapshot;
    private readonly Encoding encoding;
    private readonly SourceRegion region;
    private readonly TextBlock status = new();
    private readonly TextBox hex = new() { IsReadOnly = true, AcceptsReturn = true, Height = 90 };
    private readonly TextBox decoded = new() { IsReadOnly = true, AcceptsReturn = true, Height = 60 };
    private readonly NumericUpDown offset = new() { Minimum = 0, Value = 0, Width = 150, FormatString = "0" };
    private long position;
    private long generation;
    private readonly MessageDocument? document;
    public SourceRegionInspector(SourceSnapshot snapshot, Encoding encoding, SourceRegion region, MessageDocument? document = null)
    {
        this.document = document;
        this.snapshot = snapshot; this.encoding = encoding; this.region = region;
        Children.Add(new TextBlock { Text = $"{region.Reason}; source byte {region.Range.Offset}; {region.Range.Length} bytes" });
        var controls = new WrapPanel { Orientation = Orientation.Horizontal };
        void Button(string text, string tip, Func<long> target)
        { var button = new Button { Content = text, Margin = new(2) }; ToolTip.SetTip(button, tip); button.Click += async (_, _) => await LoadAsync(target()); controls.Children.Add(button); }
        Button("First bytes", "First byte page within this source region", () => 0); Button("Previous bytes", "Previous byte page within this source region", () => Math.Max(0, position - 512));
        Button("Next bytes", "Next byte page within this source region", () => Math.Min(region.Range.Length, position + 512)); Button("Last bytes", "Last byte page within this source region", () => Math.Max(0, region.Range.Length - 512));
        offset.Maximum = region.Range.Length; controls.Children.Add(offset);
        ToolTip.SetTip(offset, "Byte offset relative to the start of this source region");
        Button("Go", "Jump to byte offset within this source region", () => offset.Value is { } value && decimal.Truncate(value) == value ? checked((long)value) : -1);
        Children.Add(controls); Children.Add(status); Children.Add(hex); Children.Add(decoded);
    }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) { base.OnAttachedToVisualTree(e); _ = LoadAsync(0); }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) { generation++; base.OnDetachedFromVisualTree(e); }
    private async Task LoadAsync(long target)
    {
        if (target < 0 || target > region.Range.Length) { status.Text = "Enter a whole byte offset inside this region."; return; }
        var version = ++generation;
        try
        {
            var bytes = await Task.Run(() => snapshot.ReadBytes(new(region.Range.Offset + target, Math.Min(512, region.Range.Length - target))));
            if (version != generation) return;
            position = target; offset.Value = target;
            status.Text = $"Bytes {target}–{target + bytes.Length} / {region.Range.Length}";
            hex.Text = string.Join(" ", bytes.Select(b => b.ToString("X2")));
            try { decoded.Text = encoding.GetString(bytes); }
            catch (DecoderFallbackException) { decoded.Text = "No safely decoded text for this byte page."; }
        }
        catch (Exception error) when (error is IOException or ObjectDisposedException)
        { if (version == generation) { status.Text = "Source read unavailable."; if (error is IOException) document?.RecordActivity(ActivityKind.SourceReadFailed); } }
    }
}
