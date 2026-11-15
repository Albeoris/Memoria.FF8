namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect312Contacts
{
    public static int? Find(NativeBattleActionPacketsState packets, BattleActionHeaderReference reference, BattleModelOwner actor)
    {
        NativeBattleActionHeaderSlot header = packets.ReadHeader(reference);
        int count = header.ReadContactCount();
        for (int index = 0; index < count; index++)
        {
            if (packets.ReadContact(packets.ResolveContact(reference, index)).Primary.Recipient == actor)
            {
                return index;
            }
        }
        return null;
    }
}
