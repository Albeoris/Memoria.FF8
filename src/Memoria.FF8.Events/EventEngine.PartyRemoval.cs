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
        int slot = Scene.Party.IndexOf(character);
        if (slot >= 0)
        {
            // 51E270 writes FF only to the matched field-party byte. A tail removal preserves our canonical prefix.
            Scene.Party.RemoveAt(slot);
        }
        // Native bindings, actor activity and Members remain unchanged until their own executed commands.
        // TODO(TD-009): model 52CC50 combat-slot removal, optional 4ACE50 junction reset, 495EF0 stat rebuild and surviving-member HP/status repair when native combat is implemented
        return operands;
    }
}
