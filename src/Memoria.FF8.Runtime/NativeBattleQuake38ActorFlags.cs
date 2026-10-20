using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleQuake38ActorFlags
{
    public static ushort Restore8DBE80(int actor, ushort flags, ImmutableArray<uint> saved, bool targetsParty)
    {
        Validate(actor, saved);
        if ((saved[actor] & 4) != 0)
        {
            return flags;
        }
        bool party = actor < 3;
        // Enemy rows gate saved3..6 but restore bits from saved0..3.
        int originalIndex = party ? actor : actor - 3;
        uint mask = party == targetsParty ? 0x1020u : 4u;
        return unchecked((ushort)(flags ^ ((flags ^ saved[originalIndex]) & mask)));
    }

    public static ushort Restore8DC180(int actor, ushort flags, ImmutableArray<uint> saved, bool targetsParty)
    {
        Validate(actor, saved);
        if ((saved[actor] & 4) != 0 || (actor < 3) == targetsParty)
        {
            return flags;
        }
        int originalIndex = actor < 3 ? actor : actor - 3;
        return unchecked((ushort)(flags ^ ((flags ^ saved[originalIndex]) & 0x800)));
    }

    private static void Validate(int actor, ImmutableArray<uint> saved)
    {
        if ((uint)actor >= 7 || saved.IsDefault || saved.Length != 7)
        {
            throw new InvalidDataException("Native Quake flag restoration requires its seven original actor flag snapshots.");
        }
    }
}
