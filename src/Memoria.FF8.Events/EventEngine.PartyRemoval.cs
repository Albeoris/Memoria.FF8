namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private string? UnsupportedPartyRemoval(Fiber fiber)
    {
        if (fiber.Stack.Count == 0)
        {
            return "SUBPARTY requires its original character operand";
        }
        if (Scene.Party.Count > 3 || Scene.Party.Any(character => character is < 0 or >= 255) || Scene.Party.Distinct().Count() != Scene.Party.Count)
        {
            return "SUBPARTY requires a known canonical character prefix followed by absent FF slots";
        }
        int character = unchecked((byte)fiber.Stack.Peek());
        int slot = Scene.Party.IndexOf(character);
        if (slot >= 0 && slot != Scene.Party.Count - 1)
        {
            return "SUBPARTY would leave an internal native FF slot; the canonical party representation cannot preserve that sparse slot without a raw-party contract";
        }
        if (slot >= 0 && character >= 8)
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
            // 51E270 writes FF only to the matched field-party byte. A tail removal preserves our canonical prefix.
            Scene.Party.RemoveAt(slot);
        }
        // Native bindings, actor activity and Members remain unchanged until their own executed commands.
        // TODO(TD-009): exact junction/stat rebuilding remains deferred; authenticated combat slots and zero-HP/status repair are shared with DYING
        return operands;
    }
}
