using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private sealed record NativeDialogueConsumer(int Version, int Operation, int OperandCount, int Channel, bool ConfirmationReceived = false, bool Closing = false, int? PendingSelection = null);

    private static int NativeBlockingDialogueOperandCount(Jsm.Opcode opcode) => opcode switch
    {
        Jsm.Opcode.MESSYNC => 1,
        Jsm.Opcode.MESW => 2,
        Jsm.Opcode.AMESW => 4,
        Jsm.Opcode.ASK => 6,
        Jsm.Opcode.AASK => 8,
        _ => 0
    };

    private int[] ExecuteNativeBlockingDialogue(Fiber fiber, Jsm.Opcode opcode)
    {
        int count = NativeBlockingDialogueOperandCount(opcode);
        bool choice = opcode is Jsm.Opcode.ASK or Jsm.Opcode.AASK;
        if (WaitForNativeDialogueChannel(fiber, count, choice))
        {
            return [];
        }
        int[] args = fiber.Stack.Take(count).Reverse().ToArray();
        int x = opcode == Jsm.Opcode.AMESW ? args[2] : opcode == Jsm.Opcode.AASK ? args[6] : 0;
        int y = opcode == Jsm.Opcode.AMESW ? args[3] : opcode == Jsm.Opcode.AASK ? args[7] : 0;
        DialogueState dialogue = Message(args[0], args[1], x, y);
        if (choice)
        {
            dialogue = dialogue with { FirstChoice = args[2], LastChoice = args[3], InitialChoice = args[4], CancelChoice = args[5] };
        }
        ShowDialogue(fiber, dialogue, false);
        //528E40/529020/529520/5296C0 retain388 and the issuing PC until
        //their completion branch pops the original operands and returns3.
        fiber.NativeDialogueConsumer = new(1, fiber.Position - 1, count, args[0]);
        fiber.DialogueChannel = args[0];
        fiber.Position--;
        fiber.WaitReason = WaitReason.Dialogue;
        if (opcode == Jsm.Opcode.AASK)
        {
            DialogueState opened = dialogues[args[0]];
            if (!TryApplyAutoPlayTrainDialogue(opened))
            {
                ApplyAutoPlayDelingCode(fiber, opened);
            }
        }
        return args;
    }

    private int[] ExecuteNativeMessageSync(Fiber fiber)
    {
        int channel = fiber.Stack.Peek();
        if (!dialogues.TryGetValue(channel, out DialogueState? dialogue))
        {
            return Pop(fiber, 1);
        }
        dialogues[channel] = dialogue with { CanConfirm = true };
        RefreshCurrentDialogue();
        //529900 peeks until both persistent/occupied masks clear; only its
        //return2 completion consumes the channel and continues the same budget.
        fiber.NativeDialogueConsumer = new(1, fiber.Position - 1, 1, channel);
        fiber.DialogueChannel = channel;
        fiber.Position--;
        fiber.WaitReason = WaitReason.Dialogue;
        return [channel];
    }
    private bool TryConfirmNativeBlockingDialogue(DialogueState dialogue, int? selection)
    {
        Fiber? owner = fibers.FirstOrDefault(fiber => fiber.NativeDialogueConsumer is { } consumer && consumer.Channel == dialogue.Channel && (consumer.Operation == dialogue.Operation && fiber.ObjectId == dialogue.ObjectId || consumer.OperandCount == 1));
        if (owner?.NativeDialogueConsumer is not { } issued)
        {
            return false;
        }
        owner.NativeDialogueConsumer = issued with { ConfirmationReceived = true, PendingSelection = selection };
        dialogues[dialogue.Channel] = dialogue with { CanConfirm = false };
        RefreshCurrentDialogue();
        return true;
    }

    private void StepNativeAutomaticDialogueClosuresBeforeVm()
    {
        //529FF0 visits automatic211 channels before scheduling any actor VM.
        //Persistent MESSYNC closes in529900; selected choices close in their VM poll.
        if (ButtonInput?.Pressed is ushort pressed && (pressed & 0xC0) != 0)
        {
            foreach (DialogueState automatic in dialogues.Values.Where(item => item.CanConfirm && item.FirstChoice is null && !item.IsNamePrompt).ToArray())
            {
                RecordDialogueOutcome(automatic, null, null);
                if (!TryConfirmNativeBlockingDialogue(automatic, null))
                {
                    CloseDialogue(automatic.Channel);
                }
            }
        }
        foreach (Fiber fiber in fibers)
        {
            if (fiber.NativeDialogueConsumer is not { } consumer || program!.Operations[consumer.Operation].Opcode is not (Jsm.Opcode.MESW or Jsm.Opcode.AMESW) || !dialogues.TryGetValue(consumer.Channel, out DialogueState? dialogue))
            {
                continue;
            }
            NativeDialogueWindowState window = dialogue.NativeWindow ?? throw new NotSupportedException("Native automatic message cleanup lost its physical window.");
            if (consumer.ConfirmationReceived && !consumer.Closing)
            {
                window.Delta = -512;
                fiber.NativeDialogueConsumer = consumer with { Closing = true };
            }
            if (window.Progress == 0 && window.Delta < 0)
            {
                CloseDialogue(consumer.Channel);
            }
        }
    }
    private bool PollNativeBlockingDialogue(Fiber fiber)
    {
        NativeDialogueConsumer consumer = fiber.NativeDialogueConsumer!;
        bool open = dialogues.TryGetValue(consumer.Channel, out DialogueState? dialogue);
        Jsm.Opcode opcode = program!.Operations[consumer.Operation].Opcode;
        bool choice = opcode is Jsm.Opcode.ASK or Jsm.Opcode.AASK;
        if (choice)
        {
            if (!consumer.ConfirmationReceived)
            {
                Scene.NativeDialogueFocus = unchecked((byte)consumer.Channel);
                RefreshCurrentDialogue();
                return false;
            }
            NativeDialogueWindowState window = dialogue?.NativeWindow ?? throw new NotSupportedException("Native selected choice lost its retained closing window.");
            if (!consumer.Closing)
            {
                //529520/5296C0 write actor320 and call4A0660 on this poll,
                //then return1 even if the retained progress was already zero.
                fiber.Results[0] = consumer.PendingSelection ?? throw new NotSupportedException("Native selected choice lost its pending relative result.");
                window.Delta = -512;
                fiber.NativeDialogueConsumer = consumer with { Closing = true, PendingSelection = null };
                return false;
            }
            if (window.Progress != 0)
            {
                return false;
            }
            CloseDialogue(consumer.Channel);
            Scene.NativeDialogueFocus = (opcode == Jsm.Opcode.ASK ? Scene.NativeAskPreviousFocus : Scene.NativeAaskPreviousFocus) ?? throw new NotSupportedException("Native choice consumer lost its shared previous-focus marker.");
            RefreshCurrentDialogue();
        }
        else if (open)
        {
            NativeDialogueWindowState window = dialogue!.NativeWindow ?? throw new NotSupportedException("Native blocking message lost its retained window progress.");
            if (consumer.ConfirmationReceived && !consumer.Closing)
            {
                window.Delta = -512;
                fiber.NativeDialogueConsumer = consumer with { Closing = true };
            }
            if (window.Progress != 0 || window.Delta >= 0)
            {
                return false;
            }
            //529FF0 clears automatic211 only after49FBF0 reports zero;
            //MESSYNC529900 performs the corresponding persistent-mask clear.
            CloseDialogue(consumer.Channel);
        }
        Pop(fiber, consumer.OperandCount);
        fiber.Position++;
        fiber.NativeDialogueConsumer = null;
        return true;
    }
    private void ValidateNativeBlockingDialogueConsumers(Checkpoint saved)
    {
        foreach (FiberCheckpoint fiber in saved.Fibers)
        {
            if (fiber.NativeDialogueConsumer is not { } consumer)
            {
                if (NativeRuntimeProfile is { IsVerified: true } && !fiber.Complete && fiber.WaitReason == WaitReason.Dialogue && ((uint)fiber.Position < program!.Operations.Count && NativeBlockingDialogueOperandCount(program.Operations[fiber.Position].Opcode) != 0 || (uint)(fiber.Position - 1) < program.Operations.Count && NativeBlockingDialogueOperandCount(program.Operations[fiber.Position - 1].Opcode) != 0))
                {
                    throw new InvalidDataException("Saved native blocking dialogue requires its held PC and original physical operands; explicitly migrate this waiter.");
                }
                continue;
            }
            if (NativeRuntimeProfile is not { IsVerified: true } || consumer.Version != 1 || fiber.Complete || fiber.WaitReason != WaitReason.Dialogue || consumer.Operation != fiber.Position || (uint)consumer.Operation >= program!.Operations.Count || consumer.OperandCount != NativeBlockingDialogueOperandCount(program.Operations[consumer.Operation].Opcode) || consumer.OperandCount == 0 || consumer.Channel != fiber.DialogueChannel || (uint)consumer.Channel >= 8 || fiber.Stack.Length < consumer.OperandCount || fiber.Stack.Take(consumer.OperandCount).Any(value => !value.IsExact) || fiber.Stack[consumer.OperandCount - 1].RequireExact() != consumer.Channel || !program.Objects.Any(item => item.Index == fiber.ObjectId && item.Scripts.Any(script => script.Id == fiber.ScriptId && script.ContainsOperation(consumer.Operation))))
            {
                throw new InvalidDataException("Saved native blocking dialogue lost its exact issuing operation, channel or held physical operands.");
            }
            Jsm.Opcode opcode = program.Operations[consumer.Operation].Opcode;
            bool choice = opcode is Jsm.Opcode.ASK or Jsm.Opcode.AASK;
            if (consumer.Closing && !consumer.ConfirmationReceived || !choice && consumer.PendingSelection is not null || choice && consumer.PendingSelection.HasValue != (consumer.ConfirmationReceived && !consumer.Closing))
            {
                throw new InvalidDataException("Saved native dialogue confirmation or close phase is inconsistent.");
            }
            if (choice)
            {
                DialogueState? windowOwner = saved.Dialogues.SingleOrDefault(dialogue => dialogue.Channel == consumer.Channel);
                if (windowOwner is null || windowOwner.ObjectId != fiber.ObjectId || windowOwner.ScriptId != fiber.ScriptId || windowOwner.Operation != consumer.Operation || windowOwner.FirstChoice != fiber.Stack[consumer.OperandCount - 3].RequireExact() || windowOwner.LastChoice != fiber.Stack[consumer.OperandCount - 4].RequireExact() || consumer.ConfirmationReceived && windowOwner.CanConfirm || consumer.Closing && windowOwner.NativeWindow?.Delta != -512)
                {
                    throw new InvalidDataException("Saved native choice consumer lost its actual opening, selection or physical closing window.");
                }
            }
            if (consumer.PendingSelection is int selected)
            {
                int first = fiber.Stack[consumer.OperandCount - 3].RequireExact();
                int last = fiber.Stack[consumer.OperandCount - 4].RequireExact();
                if (selected < 0 || (long)selected > (long)last - first)
                {
                    throw new InvalidDataException("Saved pending choice result lies outside its held original choice range.");
                }
            }
        }
    }
}
