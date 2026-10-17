using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public enum AutoPlayCodePuzzle
{
    Train,
    DelingSword
}

public sealed record AutoPlayCodeEntryRequest(AutoPlayCodePuzzle Puzzle, int ExpectedAnswer, bool BeginsAttempt, bool CompletesAttempt);

public sealed partial class EventEngine
{
    public Func<AutoPlayCodeEntryRequest, int>? AutoPlayCodeEntry { get; set; }
    public bool AutoPlayTrainRetryPending { get; set; }

    public bool HasDirectAutoPlayCodeScene => AutoPlayCodeEntry is not null && FieldId == 902;

    public bool TryApplyAutoPlayDirectCodeEntry()
    {
        return CurrentDialogue is { } dialogue && (TryApplyAutoPlayTrainDialogue(dialogue) || TryApplyAutoPlayDelingCode(dialogue));
    }

    private bool TryApplyAutoPlayTrainDialogue(DialogueState dialogue)
    {
        if (!HasDirectAutoPlayCodeScene || !dialogue.CanConfirm || dialogue.Channel != 0 || dialogue.FirstChoice != 2 || dialogue.LastChoice != 3)
        {
            return false;
        }
        if ((dialogue.ObjectId, dialogue.ScriptId, dialogue.Operation, dialogue.MessageId) is not ((3, 33, 447, 8) or (7, 56, 972, 32) or (7, 59, 1156, 31)))
        {
            return false;
        }
        ConfirmDialogueCore(2);
        return true;
    }

    private bool TryApplyAutoPlayTrainDigit(Fiber fiber, int objectId, int scriptId, int priority, Jsm.Opcode opcode)
    {
        if (AutoPlayCodeEntry is null || FieldId != 902 || fiber.ObjectId != 12 || fiber.Script.Id != 83 || objectId != 13 || opcode != Jsm.Opcode.REQSW || scriptId is < 88 or > 91)
        {
            return false;
        }
        // TODO(TD-017): direct automatic entry deliberately replaces only these original keypad requests.
        int expected = EventMemory.Read(1033, 1).RequireExact();
        int site = fiber.Position - 1;
        if (priority != 1 || expected != scriptId - 87 || site != 2018 + (scriptId - 88) * 8)
        {
            throw new NotSupportedException("Automatic train entry requires the original digit request and expected digit.");
        }
        int answer = AutoPlayCodeEntry(new(AutoPlayCodePuzzle.Train, expected, true, true));
        if (answer is < 1 or > 4)
        {
            throw new NotSupportedException("Automatic train entry requires a digit from one through four.");
        }
        WriteGenericEventMemory(1036, 1, NativeScalarValue.Exact(1));
        WriteGenericEventMemory(1037, 1, NativeScalarValue.Exact(answer == expected ? 1 : 0));
        return true;
    }

    private bool TryApplyAutoPlayTrainNavigation(Fiber fiber, Jsm.Opcode opcode, int mask, out int result)
    {
        result = 0;
        if (AutoPlayCodeEntry is not null && AutoPlayTrainRetryPending && FieldId == 75 && fiber.ObjectId == 0 && fiber.Script.Id == 1 && fiber.Position - 1 == 125 && opcode == Jsm.Opcode.KEYON && mask == 64)
        {
            result = 1;
            return true;
        }
        if (!HasDirectAutoPlayCodeScene || opcode != Jsm.Opcode.KEYSCAN2)
        {
            return false;
        }
        switch ((fiber.ObjectId, fiber.Script.Id, fiber.Position - 1, mask))
        {
            case (8, 62, 1295, 16384):
            case (9, 66, 1328, 4096):
                result = 1;
                return true;
            case (11, 74, 1649, 4096):
                // Let the authored loop enter the next code before the three-code return sequence.
                return true;
            case (10, 70, 1363, 4):
                return true;
            default:
                return false;
        }
    }
}
