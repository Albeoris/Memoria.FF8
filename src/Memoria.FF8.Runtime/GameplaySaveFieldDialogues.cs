using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record GameplaySaveDialogueConsumer([property: JsonRequired] int Operation, [property: JsonRequired] int OperandCount, [property: JsonRequired] bool ConfirmationReceived, [property: JsonRequired] bool Closing, [property: JsonRequired] int? PendingSelection);
public sealed record GameplaySaveDialogueWait([property: JsonRequired] int Channel, [property: JsonRequired] GameplaySaveDialogueConsumer? Consumer);
public sealed record GameplaySaveDialogueWindow([property: JsonRequired] int Progress, [property: JsonRequired] int Delta);
public sealed record GameplaySaveDialogue([property: JsonRequired] int Channel, [property: JsonRequired] int MessageId, [property: JsonRequired] string Text, [property: JsonRequired] int X, [property: JsonRequired] int Y, [property: JsonRequired] int? FirstChoice, [property: JsonRequired] int? LastChoice, [property: JsonRequired] int? InitialChoice, [property: JsonRequired] int? CancelChoice, [property: JsonRequired] bool IsNamePrompt, [property: JsonRequired] int ObjectId, [property: JsonRequired] int ScriptId, [property: JsonRequired] int Operation, [property: JsonRequired] bool CanConfirm, [property: JsonRequired] GameplaySaveDialogueWindow? Window);

/// <summary>Issued field dialogue and focus ownership, independent of presentation caches and callback histories.</summary>
public sealed record GameplaySaveFieldDialogues([property: JsonRequired] int Version, [property: JsonRequired] GameplaySaveDialogue[] Windows, [property: JsonRequired] byte? Focus, [property: JsonRequired] byte? AskPreviousFocus, [property: JsonRequired] byte? AaskPreviousFocus)
{
    public void Validate()
    {
        if (Version != 1 || Windows is null || Windows.Length > 8 || Windows.Any(window => window is null) || Windows.Select(window => window.Channel).Distinct().Count() != Windows.Length)
        {
            throw new InvalidDataException("Gameplay dialogue owner has invalid or duplicate windows.");
        }
        foreach (GameplaySaveDialogue window in Windows)
        {
            if (window.Channel is < 0 or > 7 || window.Text is null || window.ObjectId < 0 || window.ScriptId < 0 || window.Operation < 0 || window.MessageId < (window.IsNamePrompt ? -1 : 0) || window.FirstChoice.HasValue != window.LastChoice.HasValue || window.FirstChoice is int first && (window.LastChoice < first || window.InitialChoice is null || window.CancelChoice is null) || window.Window is { } phase && (phase.Progress is < 0 or > 4096 || phase.Delta is not (-512 or 0 or 512)))
            {
                throw new InvalidDataException("Gameplay dialogue has an invalid opening, choice or window phase.");
            }
        }
    }
}
