namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private void ApplyAutoPlayDelingCode(Fiber fiber, DialogueState dialogue)
    {
        if (dialogue.ObjectId == fiber.ObjectId && dialogue.ScriptId == fiber.Script.Id && dialogue.Operation == fiber.Position - 1)
        {
            TryApplyAutoPlayDelingCode(dialogue);
        }
    }

    private bool TryApplyAutoPlayDelingCode(DialogueState dialogue)
    {
        if (AutoPlayCodeEntry is null || FieldId != 761 || dialogue.ObjectId != 15 || dialogue.ScriptId != 145 || !dialogue.CanConfirm)
        {
            return false;
        }
        int divisor;
        int last;
        switch ((dialogue.Operation, dialogue.MessageId, dialogue.Channel))
        {
            case (1990, 38, 0):
                divisor = 0;
                last = 4;
                break;
            case (2015, 52, 6):
                divisor = 1;
                last = 10;
                break;
            case (2034, 51, 6):
                divisor = 10;
                last = 10;
                break;
            case (2053, 50, 6):
                divisor = 100;
                last = 2;
                break;
            default:
                return false;
        }
        // TODO(TD-017): keep this explicit automatic-entry policy bounded to the original guard's requests.
        int first = divisor == 0 ? 0 : 1;
        if (dialogue.FirstChoice != first || dialogue.LastChoice != last || CurrentDialogue is not { } current || current.Channel != dialogue.Channel || current.ObjectId != dialogue.ObjectId || current.ScriptId != dialogue.ScriptId || current.Operation != dialogue.Operation)
        {
            throw new NotSupportedException("Automatic sword-number entry requires the original active dialogue range and owner.");
        }
        int selection = first;
        if (divisor != 0)
        {
            int expected = EventMemory.Read(728, 1).RequireExact();
            if (expected is < 0 or > 199)
            {
                throw new NotSupportedException("The sword number is outside the original guard's decimal entry range.");
            }
            int answer = AutoPlayCodeEntry(new(AutoPlayCodePuzzle.DelingSword, expected, divisor == 1, divisor == 100));
            if (answer is < 0 or > 199)
            {
                throw new NotSupportedException("Automatic sword-number entry returned an answer outside the original dialogue range.");
            }
            selection += answer / divisor % 10;
        }
        ConfirmDialogueCore(selection);
        return true;
    }
}
