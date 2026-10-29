using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using HL7Workbench.Core;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.Viewer;

public sealed partial class MessageInspectionView
{
    public TextBlock ValidationProfileName { get; } = new() { TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 1 };
    public TextBlock ValidationSummary { get; } = new() { TextWrapping = TextWrapping.Wrap };
    private int validationIssue = -1;
    private Hl7SchemaValidationResult? displayedValidation;
    private void InitializeValidation()
    {
        if (document is null) return;
        var panel = new ValidationStatusLayout(ValidationProfileName, ValidationSummary) { Margin = new Thickness(4, 0, 4, 4) };
        SetRow(panel, 3); RawPane.Children.Add(panel);
        AutomationProperties.SetName(panel, "Schema validation status");
        AutomationProperties.SetName(ValidationProfileName, "Applied schema");
        AutomationProperties.SetName(ValidationSummary, "Inline schema validation status");
        AddHandler(KeyDownEvent, ValidationKeyDown, RoutingStrategies.Tunnel);
        if (PresentationView is null) _ = new ValidationInlineRenderer(Editor, document);
        UpdateValidation();
    }
    private void ValidationKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.F8 || (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) != 0 || document?.SchemaValidation?.Diagnostics.Count is not > 0) return;
        e.Handled = true; NavigateValidationIssue((e.KeyModifiers & KeyModifiers.Shift) != 0 ? -1 : 1);
    }
    private void UpdateValidation()
    {
        if (document is null) return;
        if (!ReferenceEquals(displayedValidation, document.SchemaValidation)) { displayedValidation = document.SchemaValidation; validationIssue = -1; CloseRawToolTip(); }
        var result = document.SchemaValidation;
        ValidationProfileName.Text = document.AppliedSchemaId is null ? "Schema: not applied" : document.AppliedSchemaName;
        ValidationSummary.Text = $"{document.ValidationState} \u00b7 {result?.Errors ?? 0} errors / {result?.Warnings ?? 0} warnings";
        var details = $"{ValidationProfileName.Text}. {document.ValidationStatus} F8: next issue; Shift+F8: previous issue. Schema actions: HL7 Tools ribbon." + (result?.Diagnostics.Count > ValidationInlineRenderer.MaximumDecorations ?
            $" Only the first {ValidationInlineRenderer.MaximumDecorations} prioritized decorations are drawn; F8 navigates all retained findings." : "");
        ToolTip.SetTip(ValidationProfileName, details); AutomationProperties.SetHelpText(ValidationProfileName, details);
        ToolTip.SetTip(ValidationSummary, details); AutomationProperties.SetHelpText(ValidationSummary, details);
        Editor.TextArea.TextView.Redraw();
    }
    internal void NavigateValidationIssue(int direction)
    {
        if (document?.SchemaValidation is not { Diagnostics.Count: > 0 } result) return;
        validationIssue = validationIssue < 0 ? (direction < 0 ? result.Diagnostics.Count - 1 : 0) :
            (validationIssue + (direction < 0 ? -1 : 1) + result.Diagnostics.Count) % result.Diagnostics.Count;
        var issue = result.Diagnostics[validationIssue];
        // Keep counts and coverage visible while navigating. Long issue explanations
        // belong in the hover/accessibility text, never in the measured toolbar.
        var details = $"Issue {validationIssue + 1}/{result.Diagnostics.Count}. " + ValidationInlineRenderer.Format(issue);
        ToolTip.SetTip(ValidationSummary, details); AutomationProperties.SetHelpText(ValidationSummary, details);
        if (PresentationView is not null) { PresentationView.NavigateValidationRange(issue.Range); return; }
        synchronizing = true;
        try
        {
            var start = Math.Clamp(issue.Range.Offset, 0, Editor.Document.TextLength); var end = Math.Clamp(issue.Range.End, start, Editor.Document.TextLength);
            Editor.Select((int)start, (int)(end - start)); var location = Editor.Document.GetLocation((int)start); Editor.ScrollTo(location.Line, location.Column); Editor.TextArea.Focus();
        }
        finally { synchronizing = false; }
    }
}

/// <summary>Reflows status in DIPs without measuring the full profile name as a toolbar width.</summary>
internal sealed class ValidationStatusLayout : Grid
{
    private readonly TextBlock summary;
    private bool? wide;
    internal ValidationStatusLayout(TextBlock profile, TextBlock summary)
    { this.summary = summary; ColumnSpacing = 8; Children.Add(profile); Children.Add(summary); }
    protected override Size MeasureOverride(Size availableSize)
    {
        var next = availableSize.Width >= ToolLayoutMetrics.FullWidth;
        if (wide != next)
        {
            wide = next; ColumnDefinitions = new(next ? "*,Auto" : "*"); RowDefinitions = new(next ? "Auto" : "Auto,Auto");
            SetColumn(summary, next ? 1 : 0); SetRow(summary, next ? 0 : 1);
        }
        return base.MeasureOverride(availableSize);
    }
}
