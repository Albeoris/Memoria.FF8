using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private string? UnsupportedNativeDress(Fiber fiber)
    {
        if (fiber.Stack.Count < 2)
        {
            return "SETDRESS requires its original character and costume operands";
        }
        if (services.CharacterAppearance is not NativeCharacterAppearance appearance)
        {
            return "SETDRESS requires authentic New Game character costume provenance; no appearance defaults are inferred";
        }
        appearance.Validate();
        byte character = unchecked((byte)fiber.Stack.ElementAt(1));
        return appearance.Records.Any(record => record.CharacterId == character) ? null : "SETDRESS has no original saved-record identity for this character";
    }

    private int[] ExecuteNativeDress(Fiber fiber)
    {
        int[] operands = fiber.Stack.Take(2).Reverse().ToArray();
        NativeCharacterAppearance next = services.CharacterAppearance!.SetDress(unchecked((byte)operands[0]), unchecked((byte)operands[1]));
        services.SetCharacterAppearance(next);
        return Pop(fiber, 2);
    }
}
