namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private string? UnsupportedPartyRemoval(Fiber fiber)
    {
        if (fiber.Stack.Count == 0)
        {
            return "SUBPARTY requires its original character operand";
        }
        if (!HasCanonicalPartySlots(Scene))
        {
            return "SUBPARTY requires the exact three native character slots";
        }
        int character = unchecked((byte)fiber.Stack.Peek());
        int slot = Scene.Party.IndexOf(character);
        if (slot >= 0 && character is >= 8 and < 255)
        {
            return "SUBPARTY guest-character removal requires its original character-to-combat-slot contract";
        }
        return null;
    }

    private int[] ExecutePartyRemoval(Fiber fiber)
    {
        int[] operands = Pop(fiber, 1);
        int character = unchecked((byte)operands[0]);
        RemoveCombatPartyCharacter((byte)character);
        int slot = Scene.Party.IndexOf(character);
        if (slot >= 0)
        {
            // 51E270 writes FF only to the matched field-party byte.
            Scene.Party[slot] = 255;
        }
        // Native bindings, actor activity and Members remain unchanged until their own executed commands.
        // TODO(TD-009): exact junction/stat rebuilding remains deferred; authenticated combat slots and zero-HP/status repair are shared with DYING
        return operands;
    }
}
