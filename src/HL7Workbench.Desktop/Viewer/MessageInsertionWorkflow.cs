using System.Windows.Input;
using Avalonia.Controls;
using HL7Workbench.Core;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.Viewer;

/// <summary>Shared blank-insertion policy for document controls, menus and ribbon.</summary>
internal static class MessageInsertionWorkflow
{
    internal static readonly (MessageInsertionPosition Position, string Label)[] Choices =
    [
        (MessageInsertionPosition.BeforeCurrent, "Before Current"),
        (MessageInsertionPosition.AfterCurrent, "After Current"),
        (MessageInsertionPosition.Beginning, "Beginning"),
        (MessageInsertionPosition.End, "End")
    ];
    internal static string CommandName(MessageInsertionPosition position) => "Add Message: " + Choices.Single(c => c.Position == position).Label;
    internal static bool CanInsert(MessageDocument? document, MessageInsertionPosition position) =>
        document is { InsertionUnavailableReason: null } &&
        (document.ActiveMessageId is not null || position is MessageInsertionPosition.Beginning or MessageInsertionPosition.End);
    internal static bool TryInsert(MessageDocument? document, MessageInsertionPosition position, out MessageId id, out string status)
    {
        id = default;
        status = document?.InsertionUnavailableReason ?? "Select an open document and an available insertion position.";
        if (!CanInsert(document, position)) return false;
        try
        {
            id = document!.InsertMessage(position);
            // This is session state, not the persisted Start in Edit Mode preference.
            document.IsEditMode = true;
            status = $"Message {document.MessageNumber} inserted. Paste or type HL7; save explicitly when complete.";
            return true;
        }
        catch (Hl7LimitException) { status = "Insertion refused: the message or edit/history limit was reached. Existing work is retained."; }
        catch (InvalidOperationException error) { status = error.Message; }
        return false;
    }
    internal static MenuItem[] MenuItems(Func<MessageInsertionPosition, ICommand> command) =>
        Choices.Select(c => new MenuItem { Header = c.Label, Command = command(c.Position) }).ToArray();
}
