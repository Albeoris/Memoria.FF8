using Memoria.FF8.Runtime;
using static Memoria.FF8.Tests.NativeBattleTypedFixture;

namespace Memoria.FF8.App.Tests;

internal static class NativeBattleOriginalModelAssertions
{
    internal static byte ReadOriginalModelOperand(NativeBattleModelAliases aliases, BattleModelOwner owner)
    {
        return aliases.TryEncodeExact(owner, out byte operand)
            ? operand
            : throw new InvalidDataException("The tested model has no original operand in this packet bank.");
    }

    internal static BattleModelOwner RequireOriginalModelOwner(NativeBattleGraphicsState graphics, int slot)
    {
        return graphics.NativeBindings.FindOriginalOwner(NativeBattleModelSlot.Decode(checked((byte)slot)), graphics.ActorModels)
            ?? throw new InvalidDataException("The tested original model cell has no bound owner.");
    }

    internal static bool MatchesOriginalModelSlot(NativeBattleGraphicsState graphics, BattleModelOwner? owner, int slot)
    {
        return owner is not null && graphics.NativeBindings.FindOriginalOwner(NativeBattleModelSlot.Decode(checked((byte)slot)), graphics.ActorModels) == owner;
    }

    internal static NativeBattleEffectsState ExpectedOriginalDamageText(NativeBattleContactHalf contact, int amount)
    {
        return OriginalEffects().PrependDamageText(contact.Recipient, contact.ContactFlags, amount, new BattleModelRoster([contact.Recipient]));
    }

    internal static int FindOriginalModelSlot(NativeBattleGraphicsState graphics, string? actorId)
    {
        for (int slot = 0; slot < NativeBattleGraphicsModelAddress.NativeRowCount; slot++)
        {
            if (string.Equals(ReadOriginalModelActorId(graphics, slot), actorId, StringComparison.Ordinal))
            {
                return slot;
            }
        }
        return -1;
    }
}
