namespace Memoria.FF8.Runtime;

public static class NativeBattleDuel216Contacts
{
    public static NativeBattleContactPacket Read506690(NativeBattleActionPacketsState packets, BattleActionHeaderReference header)
    {
        return packets.ReadContact(packets.ReadHeader(header).FirstContact);
    }
}
