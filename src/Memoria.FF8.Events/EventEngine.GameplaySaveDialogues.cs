using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    public bool HasSupportedGameplayDialogueBoundary => dialogues.Count != 0 && dialogues.Values.All(dialogue => !dialogue.IsNamePrompt) && LastFault is null;

    private GameplaySaveFieldDialogues? CaptureGameplayDialogues()
    {
        if (dialogues.Count == 0 && fibers.All(fiber => fiber.DialogueChannel == -1 && fiber.NativeDialogueConsumer is null))
        {
            return null;
        }
        GameplaySaveFieldDialogues saved = new(1, dialogues.Values.Select(dialogue => new GameplaySaveDialogue(dialogue.Channel, dialogue.MessageId, dialogue.Text, dialogue.X, dialogue.Y, dialogue.FirstChoice, dialogue.LastChoice, dialogue.InitialChoice, dialogue.CancelChoice, dialogue.IsNamePrompt, dialogue.ObjectId, dialogue.ScriptId, dialogue.Operation, dialogue.CanConfirm, dialogue.NativeWindow is { } window ? new(window.Progress, window.Delta) : null)).ToArray(), Scene.NativeDialogueFocus, Scene.NativeAskPreviousFocus, Scene.NativeAaskPreviousFocus);
        saved.Validate();
        ValidateGameplayDialogues(saved, fibers);
        return saved;
    }

    private static GameplaySaveDialogueWait? CaptureGameplayDialogueWait(Fiber fiber)
    {
        if (fiber.WaitReason != WaitReason.Dialogue)
        {
            if (fiber.NativeDialogueConsumer is not null || fiber.DialogueChannel != -1)
            {
                throw new NotSupportedException("Gameplay dialogue ownership requires its waiting script consumer.");
            }
            return null;
        }
        NativeDialogueConsumer? consumer = fiber.NativeDialogueConsumer;
        return new(fiber.DialogueChannel, consumer is null ? null : new(consumer.Operation, consumer.OperandCount, consumer.ConfirmationReceived, consumer.Closing, consumer.PendingSelection));
    }

    private static void RestoreGameplayDialogueWait(Fiber fiber, GameplaySaveDialogueWait? saved)
    {
        if (saved is null)
        {
            return;
        }
        fiber.DialogueChannel = saved.Channel;
        if (saved.Consumer is { } consumer)
        {
            fiber.NativeDialogueConsumer = new(1, consumer.Operation, consumer.OperandCount, saved.Channel, consumer.ConfirmationReceived, consumer.Closing, consumer.PendingSelection);
        }
    }

    private void ValidateGameplayDialogues(GameplaySaveFieldScripts saved, IReadOnlyCollection<Fiber> restored)
    {
        ValidateGameplayDialogues(saved.Dialogues, restored);
    }

    private void ValidateGameplayDialogues(GameplaySaveFieldDialogues? saved, IReadOnlyCollection<Fiber> restored)
    {
        if (saved is null)
        {
            if (restored.Any(fiber => fiber.WaitReason == WaitReason.Dialogue || fiber.NativeDialogueConsumer is not null || fiber.DialogueChannel != -1))
            {
                throw new InvalidDataException("Gameplay dialogue waits require their issued dialogue owner.");
            }
            return;
        }
        saved.Validate();
        DialogueState[] windows = saved.Windows.Select(RestoreGameplayDialogue).ToArray();
        bool native = NativeRuntimeProfile is { IsVerified: true };
        ValidateNativeDialogueWindows(windows, native);
        foreach (DialogueState dialogue in windows)
        {
            if ((uint)dialogue.ObjectId >= program!.Objects.Count || !program.Objects[dialogue.ObjectId].Scripts.Any(script => script.Id == dialogue.ScriptId && script.ContainsOperation(dialogue.Operation)) || !dialogue.IsNamePrompt && dialogue.MessageId >= messages.Count)
            {
                throw new InvalidDataException("Gameplay dialogue opening is absent from the installed field program or message table.");
            }
            if (native && (saved.Focus is null || dialogue.FirstChoice is not null && (program.Operations[dialogue.Operation].Opcode == Jsm.Opcode.ASK ? saved.AskPreviousFocus is null : saved.AaskPreviousFocus is null)))
            {
                throw new InvalidDataException("Gameplay native dialogue lost its current or previous focus owner.");
            }
        }
        foreach (Fiber fiber in restored)
        {
            if (fiber.NativeDialogueConsumer is not { } consumer)
            {
                if (fiber.WaitReason == WaitReason.Dialogue && (fiber.DialogueChannel is < 0 or > 7 || native && (fiber.Script.ContainsOperation(fiber.Position) && NativeBlockingDialogueOperandCount(program!.Operations[fiber.Position].Opcode) != 0 || fiber.Script.ContainsOperation(fiber.Position - 1) && NativeBlockingDialogueOperandCount(program!.Operations[fiber.Position - 1].Opcode) != 0)))
                {
                    throw new InvalidDataException("Gameplay native dialogue requires its retained issuing consumer and operands.");
                }
                continue;
            }
            if (!native || consumer.Version != 1 || fiber.Complete || fiber.WaitReason != WaitReason.Dialogue || consumer.Operation != fiber.Position || !fiber.Script.ContainsOperation(consumer.Operation) || consumer.OperandCount == 0 || consumer.OperandCount != NativeBlockingDialogueOperandCount(program!.Operations[consumer.Operation].Opcode) || consumer.Channel != fiber.DialogueChannel || consumer.Channel is < 0 or > 7 || fiber.Stack.Count < consumer.OperandCount || fiber.Stack.Capture().Take(consumer.OperandCount).Any(value => !value.IsExact) || fiber.Stack.ElementAt(consumer.OperandCount - 1) != consumer.Channel)
            {
                throw new InvalidDataException("Gameplay dialogue consumer lost its held instruction, channel or original operands.");
            }
            bool choice = program!.Operations[consumer.Operation].Opcode is Jsm.Opcode.ASK or Jsm.Opcode.AASK;
            if (consumer.Closing && !consumer.ConfirmationReceived || !choice && consumer.PendingSelection is not null || choice && consumer.PendingSelection.HasValue != (consumer.ConfirmationReceived && !consumer.Closing))
            {
                throw new InvalidDataException("Gameplay dialogue confirmation and closing phases disagree.");
            }
            if (choice)
            {
                DialogueState? window = windows.SingleOrDefault(dialogue => dialogue.Channel == consumer.Channel);
                int first = fiber.Stack.ElementAt(consumer.OperandCount - 3);
                int last = fiber.Stack.ElementAt(consumer.OperandCount - 4);
                if (window is null || window.ObjectId != fiber.ObjectId || window.ScriptId != fiber.Script.Id || window.Operation != consumer.Operation || window.FirstChoice != first || window.LastChoice != last || consumer.ConfirmationReceived && window.CanConfirm || consumer.Closing && window.NativeWindow?.Delta != -512 || consumer.PendingSelection is int selected && (selected < 0 || (long)selected > (long)last - first))
                {
                    throw new InvalidDataException("Gameplay choice consumer lost its opening, pending selection or closing window.");
                }
                if ((program.Operations[consumer.Operation].Opcode == Jsm.Opcode.ASK ? saved.AskPreviousFocus : saved.AaskPreviousFocus) is null)
                {
                    throw new InvalidDataException("Gameplay choice consumer lost its previous focus owner.");
                }
            }
        }
    }

    private static DialogueState RestoreGameplayDialogue(GameplaySaveDialogue dialogue)
    {
        return new(dialogue.Channel, dialogue.MessageId, dialogue.Text, dialogue.X, dialogue.Y, dialogue.FirstChoice, dialogue.LastChoice, dialogue.InitialChoice, dialogue.CancelChoice, dialogue.IsNamePrompt, dialogue.ObjectId, dialogue.ScriptId, dialogue.Operation, dialogue.CanConfirm, dialogue.Window is { } window ? new(1, window.Progress, window.Delta) : null);
    }

    private void RestoreGameplayDialogues(GameplaySaveFieldDialogues? saved)
    {
        dialogues.Clear();
        dialogueOccurrences.Clear();
        if (saved is not null)
        {
            Scene.NativeDialogueFocus = saved.Focus;
            Scene.NativeAskPreviousFocus = saved.AskPreviousFocus;
            Scene.NativeAaskPreviousFocus = saved.AaskPreviousFocus;
            foreach (GameplaySaveDialogue window in saved.Windows)
            {
                dialogues.Add(window.Channel, RestoreGameplayDialogue(window));
                dialogueOccurrences.Add(window.Channel, ++dialogueOccurrenceSequence);
            }
        }
        IEnumerable<DialogueState> visible = dialogues.Values.Where(dialogue => dialogue.NativeWindow is not { Progress: 0, Delta: < 0 });
        CurrentDialogue = Scene.NativeDialogueFocus is byte focus ? visible.FirstOrDefault(dialogue => dialogue.Channel == focus) : visible.LastOrDefault(dialogue => dialogue.CanConfirm) ?? visible.LastOrDefault();
    }
}
