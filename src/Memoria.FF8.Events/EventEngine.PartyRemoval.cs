namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private string? UnsupportedPartyRemoval(Fiber fiber)
    {
        if (fiber.Stack.Count == 0 || !fiber.Stack.PeekValue().IsExact)
        {
            return "SUBPARTY requires its original character operand";
        }
        if (!HasCanonicalPartySlots(Scene))
        {
            return "SUBPARTY requires the exact three native character slots";
        }
        return null;
    }

    private int[] ExecutePartyRemoval(Fiber fiber)
    {
        int character = unchecked((byte)fiber.Stack.Peek());
        CommitSavedCharacters(fiber, PrepareRemovedCombatCharacter((byte)character), Albeoris.Games.FF8.Jsm.Jsm.Opcode.SUBPARTY);
        int slot = Scene.Party.IndexOf(character);
        if (slot >= 0)
        {
            // 51E270 writes FF only to the matched field-party byte.
            Scene.Party[slot] = 255;
        }
        // Native bindings, actor activity and membership remain unchanged until their own writers.
        return Pop(fiber, 1);
    }
}
