using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private string? UnsupportedRemainingDialogueCode(Fiber fiber, JsmOperation operation)
    {
        int count = operation.Opcode switch
        {
            Jsm.Opcode.MESFORCUS => 1,
            Jsm.Opcode.MESSYNC when NativeRuntimeProfile is { IsVerified: true } => 1,
            Jsm.Opcode.MESW => 2,
            Jsm.Opcode.ASK => 6,
            Jsm.Opcode.AASK => 8,
            Jsm.Opcode.AMESW when NativeRuntimeProfile is { IsVerified: true } => 4,
            _ => 0
        };
        if (count == 0)
        {
            return null;
        }
        if (fiber.Stack.Count < count || fiber.Stack.Capture().Take(count).Any(value => !value.IsExact))
        {
            return $"{operation.Opcode} requires its complete original DWORD operands";
        }
        int channel = fiber.Stack.ElementAt(count - 1);
        if (operation.Opcode != Jsm.Opcode.MESFORCUS && (uint)channel >= 8)
        {
            return "Native dialogue requires a window channel0..7";
        }
        if (operation.Opcode is Jsm.Opcode.ASK or Jsm.Opcode.AASK && dialogues.TryGetValue(channel, out DialogueState? occupied) && occupied.FirstChoice is null && fibers.Any(owner => owner.NativeDialogueConsumer?.Channel == channel))
        {
            return "Native choice replacement of an active blocking message requires separate physical window and occupied-mask owners";
        }
        if ((operation.Opcode == Jsm.Opcode.ASK || operation.Opcode == Jsm.Opcode.AASK && NativeRuntimeProfile is { IsVerified: true }) && Scene.NativeDialogueFocus is null)
        {
            return "ASK requires the retained native focus marker to restore after its selection";
        }
        return null;
    }

    private void ValidateNativeChoiceFocus(Checkpoint saved)
    {
        if (NativeRuntimeProfile is not { IsVerified: true })
        {
            return;
        }
        HashSet<Jsm.Opcode> choices = [];
        foreach (DialogueState dialogue in saved.Dialogues.Where(item => item.FirstChoice is not null))
        {
            if ((uint)dialogue.Operation >= program!.Operations.Count)
            {
                throw new InvalidDataException("Saved choice focus lost its original opening operation.");
            }
            choices.Add(program.Operations[dialogue.Operation].Opcode);
        }
        foreach (FiberCheckpoint fiber in saved.Fibers.Where(item => !item.Complete && item.WaitReason == WaitReason.Dialogue))
        {
            int operation = fiber.NativeDialogueConsumer?.Operation ?? fiber.Position - 1;
            if ((uint)operation < program!.Operations.Count && program.Operations[operation].Opcode is Jsm.Opcode.ASK or Jsm.Opcode.AASK)
            {
                choices.Add(program.Operations[operation].Opcode);
            }
        }
        if (saved.Dialogues.Length == 0 && choices.Count == 0)
        {
            return;
        }
        if (saved.Scene.NativeDialogueFocus is null)
        {
            throw new InvalidDataException("Saved native dialogue requires its explicit focus BYTE.");
        }
        if (choices.Contains(Jsm.Opcode.ASK) && saved.Scene.NativeAskPreviousFocus is null || choices.Contains(Jsm.Opcode.AASK) && saved.Scene.NativeAaskPreviousFocus is null)
        {
            throw new InvalidDataException("Saved native choice or closing consumer lost its opcode-specific shared previous-focus global.");
        }
    }
    private bool NativeDialogueWaitReady(Fiber fiber)
    {
        if (fiber.NativeDialogueConsumer is not null)
        {
            return PollNativeBlockingDialogue(fiber);
        }
        bool open = dialogues.TryGetValue(fiber.DialogueChannel, out DialogueState? dialogue);
        if (Scene.NativeDialogueFocus is null)
        {
            return !open;
        }
        int operation = fiber.Position - 1;
        Jsm.Opcode opcode = fiber.Script.ContainsOperation(operation) ? program!.Operations[operation].Opcode : Jsm.Opcode.RET;
        if (opcode is not (Jsm.Opcode.ASK or Jsm.Opcode.AASK))
        {
            return !open;
        }
        if (open)
        {
            //Both native choice handlers select their window on every incomplete
            //poll, even if another actor changed focus after the preceding poll.
            Scene.NativeDialogueFocus = unchecked((byte)fiber.DialogueChannel);
            RefreshCurrentDialogue();
            return false;
        }
        Scene.NativeDialogueFocus = (opcode == Jsm.Opcode.ASK ? Scene.NativeAskPreviousFocus : Scene.NativeAaskPreviousFocus) ?? throw new NotSupportedException("Choice completion lost its native global previous-focus marker.");
        RefreshCurrentDialogue();
        return true;
    }
    private int[] ExecuteRemainingDialogueCode(Fiber fiber, Jsm.Opcode opcode)
    {
        if (NativeRuntimeProfile is { IsVerified: true } && opcode is Jsm.Opcode.MESW or Jsm.Opcode.ASK)
        {
            return ExecuteNativeBlockingDialogue(fiber, opcode);
        }
        if (opcode == Jsm.Opcode.MESFORCUS)
        {
            int[] focus = Pop(fiber, 1);
            //49FD50 accepts a char; markers outside0..7 deliberately focus no window.
            Scene.NativeDialogueFocus = unchecked((byte)focus[0]);
            RefreshCurrentDialogue();
            return focus;
        }
        int count = opcode == Jsm.Opcode.ASK ? 6 : 2;
        if (WaitForNativeDialogueChannel(fiber, count, opcode == Jsm.Opcode.ASK))
        {
            return [];
        }
        int[] args = Pop(fiber, count);
        DialogueState dialogue = Message(args[0], args[1], 0, 0);
        if (opcode == Jsm.Opcode.ASK)
        {
            dialogue = dialogue with { FirstChoice = args[2], LastChoice = args[3], InitialChoice = args[4], CancelChoice = args[5] };
        }
        ShowDialogue(fiber, dialogue, true);
        return args;
    }
}
