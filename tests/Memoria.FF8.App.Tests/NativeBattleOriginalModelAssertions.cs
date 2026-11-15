using Memoria.FF8.Runtime;
using static Memoria.FF8.Tests.NativeBattleTypedFixture;

namespace Memoria.FF8.App.Tests;

internal static class NativeBattleOriginalModelAssertions
{
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
