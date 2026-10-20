namespace Memoria.FF8.Runtime;

public static class NativeBattleDuel216Contacts
{
    public static NativeBattleContactPacket Read506690(NativeBattleActionPacketsState packets, int headerIndex)
    {
        if ((uint)headerIndex >= packets.Headers.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(headerIndex));
        }
        int first = packets.Headers[headerIndex].FirstContactIndex ?? throw new NotSupportedException("Native Different Beat contact requires its actual first physical contact pointer.");
        return packets.ReadContact(first);
    }
}
