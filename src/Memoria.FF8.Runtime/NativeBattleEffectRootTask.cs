namespace Memoria.FF8.Runtime;

public static class NativeBattleEffectRootTask
{
    public static NativeBattleModulePoolTask Initialize(NativeBattleModulePoolTask task, NativeBattleEffectParameters parameters, NativeBattleActionPacketsState packets)
    {
        task = task.WriteDword(0x0C, 0x1D99A78);
        NativeBattleEffectTaskActors actors = NativeBattleCureController.SelectActors(parameters, packets, task.ReadDword(0x0C), unchecked((sbyte)task.ReadByte(0x2A)), unchecked((sbyte)task.ReadByte(0x2B)));
        NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
        byte targets = header.ReadByte(0x10);
        byte groups = header.ReadByte(0x11);
        byte maximum = unchecked((byte)(targets - 1));
        if (maximum < groups)
        {
            maximum = groups;
        }
        return task.WriteByte(0x2C, actors.Actor).WriteByte(0x29, 0).WriteByte(0x2D, actors.Target).WriteWord(0x5A, targets).WriteWord(0x58, groups).WriteByte(0x2F, maximum);
    }
}
