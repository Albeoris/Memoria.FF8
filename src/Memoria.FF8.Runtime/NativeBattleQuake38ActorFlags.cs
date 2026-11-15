namespace Memoria.FF8.Runtime;

public static class NativeBattleQuake38ActorFlags
{
    public static ushort Restore8DBE80(bool party, ushort flags, uint gateFlags, uint restoreFlags, bool targetsParty)
    {
        if ((gateFlags & 4) != 0)
        {
            return flags;
        }
        uint mask = party == targetsParty ? 0x1020u : 4u;
        return unchecked((ushort)(flags ^ ((flags ^ restoreFlags) & mask)));
    }

    public static ushort Restore8DC180(bool party, ushort flags, uint gateFlags, uint restoreFlags, bool targetsParty)
    {
        if ((gateFlags & 4) != 0 || party == targetsParty)
        {
            return flags;
        }
        return unchecked((ushort)(flags ^ ((flags ^ restoreFlags) & 0x800)));
    }
}
