using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private bool TryApplyAutoPlaySecondWagonDigit(Fiber fiber, int objectId, int scriptId, int priority, Jsm.Opcode opcode)
    {
        if (AutoPlayCodeEntry is null || FieldId != 903 || fiber.ObjectId != 10 || fiber.Script.Id != 67 || objectId != 11 || opcode != Jsm.Opcode.REQSW || scriptId is < 72 or > 77)
        {
            return false;
        }
        // TODO(TD-017): direct automatic entry replaces only the original keypad child request.
        int expected = EventMemory.Read(1033, 1).RequireExact();
        if (priority != 1 || expected != scriptId - 71 || fiber.Position - 1 != 1790 + (scriptId - 72) * 8)
        {
            throw new NotSupportedException("Automatic second-wagon entry requires the original digit request and expected digit.");
        }
        int answer = AutoPlayCodeEntry(new(AutoPlayCodePuzzle.Train, expected, true, true));
        if (answer is < 1 or > 6)
        {
            throw new NotSupportedException("Automatic second-wagon entry requires a digit from one through six.");
        }
        WriteGenericEventMemory(1036, 1, NativeScalarValue.Exact(1));
        WriteGenericEventMemory(1037, 1, NativeScalarValue.Exact(answer == expected ? 1 : 0));
        return true;
    }

    private bool TryApplyAutoPlaySecondWagonNavigation(Fiber fiber, Jsm.Opcode opcode, int mask, out int result)
    {
        result = 0;
        if (AutoPlayCodeEntry is null || FieldId != 903 || opcode != Jsm.Opcode.KEYSCAN2)
        {
            return false;
        }
        switch ((fiber.ObjectId, fiber.Script.Id, fiber.Position - 1, mask))
        {
            case (6, 46, 1064, 16384):
                result = HasAutoPlaySecondWagonPatrolWindow() ? 1 : 0;
                return true;
            case (7, 50, 1097, 4096):
            case (9, 58, 1421, 4096):
                result = 1;
                return true;
            default:
                return false;
        }
    }

    private bool HasAutoPlaySecondWagonPatrolWindow()
    {
        // TODO(TD-017): reserve the authored long wait for a complete visit; the final code ends native patrol capture eligibility.
        if (program is null || program.Operations[470].Opcode != Jsm.Opcode.PSHN_L || program.Operations[471].Opcode != Jsm.Opcode.WAIT)
        {
            throw new NotSupportedException("Automatic second-wagon navigation requires the original patrol wait.");
        }
        if (program.Operations[470].Parameter != 360 || program.Operations[590].Opcode != Jsm.Opcode.PSHN_L || program.Operations[590].Parameter != 200 || program.Operations[591].Opcode != Jsm.Opcode.WAIT)
        {
            throw new NotSupportedException("Automatic second-wagon navigation has an unsupported patrol interval.");
        }
        int completed = EventMemory.Read(1038, 1).RequireExact();
        if (completed >= 5)
        {
            return false;
        }
        int reserve = program.Operations[470].Parameter;
        if (completed == 4)
        {
            reserve -= 100;
        }
        int pollingDelay = 1;
        return fibers.Any(fiber => !fiber.Complete && fiber.ObjectId == 3 && fiber.Script.Id == 28 && fiber.Position == 472 && fiber.WaitReason == WaitReason.Frames && fiber.WaitUntil - Frame >= reserve - pollingDelay)
            && fibers.Any(fiber => !fiber.Complete && fiber.ObjectId == 4 && fiber.Script.Id == 34 && fiber.Position == 589 && fiber.WaitReason == WaitReason.Frames && fiber.WaitUntil - Frame >= reserve - pollingDelay);
    }
}
