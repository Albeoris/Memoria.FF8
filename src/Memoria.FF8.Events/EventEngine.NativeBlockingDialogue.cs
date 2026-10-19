using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private sealed record NativeDialogueConsumer(int Version, int Operation, int OperandCount, int Channel);

    private static int NativeBlockingDialogueOperandCount(Jsm.Opcode opcode) => opcode switch
    {
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

    private bool PollNativeBlockingDialogue(Fiber fiber)
    {
        NativeDialogueConsumer consumer = fiber.NativeDialogueConsumer!;
        bool open = dialogues.ContainsKey(consumer.Channel);
        Jsm.Opcode opcode = program!.Operations[consumer.Operation].Opcode;
        if (opcode is Jsm.Opcode.ASK or Jsm.Opcode.AASK)
        {
            Scene.NativeDialogueFocus = open ? unchecked((byte)consumer.Channel) : (opcode == Jsm.Opcode.ASK ? Scene.NativeAskPreviousFocus : Scene.NativeAaskPreviousFocus) ?? throw new NotSupportedException("Native choice consumer lost its shared previous-focus marker.");
            RefreshCurrentDialogue();
        }
        if (open)
        {
            return false;
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
                if (NativeRuntimeProfile is { IsVerified: true } && !fiber.Complete && fiber.WaitReason == WaitReason.Dialogue && (uint)(fiber.Position - 1) < program!.Operations.Count && NativeBlockingDialogueOperandCount(program.Operations[fiber.Position - 1].Opcode) != 0)
                {
                    throw new InvalidDataException("Saved native blocking dialogue requires its held PC and original physical operands; explicitly migrate this waiter.");
                }
                continue;
            }
            if (NativeRuntimeProfile is not { IsVerified: true } || consumer.Version != 1 || fiber.Complete || fiber.WaitReason != WaitReason.Dialogue || consumer.Operation != fiber.Position || (uint)consumer.Operation >= program!.Operations.Count || consumer.OperandCount != NativeBlockingDialogueOperandCount(program.Operations[consumer.Operation].Opcode) || consumer.OperandCount == 0 || consumer.Channel != fiber.DialogueChannel || (uint)consumer.Channel >= 8 || fiber.Stack.Length < consumer.OperandCount || fiber.Stack.Take(consumer.OperandCount).Any(value => !value.IsExact) || fiber.Stack[consumer.OperandCount - 1].RequireExact() != consumer.Channel || !program.Objects.Any(item => item.Index == fiber.ObjectId && item.Scripts.Any(script => script.Id == fiber.ScriptId && script.ContainsOperation(consumer.Operation))))
            {
                throw new InvalidDataException("Saved native blocking dialogue lost its exact issuing operation, channel or held physical operands.");
            }
        }
    }
}
