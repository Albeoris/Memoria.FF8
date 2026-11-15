namespace Memoria.FF8.Runtime;

public static class NativeBattleEffectRootTask
{
    internal static int LastContactPosition(int count) => count == 0 ? byte.MaxValue : count - 1;

    public static NativeBattleModulePoolTask Initialize(NativeBattleModulePoolTask task, NativeBattleEffectParameters parameters, NativeBattleActionPacketsState packets)
    {
        task = task.WriteDword(0x0C, 0x1D99A78);
        NativeBattleEffectTaskActors actors = NativeBattleCureController.SelectActors(parameters, packets, task.ReadDword(0x0C), unchecked((sbyte)task.ReadByte(0x2A)), task.ReadTraversalPosition(0x2B));
        NativeBattleActionHeaderSlot header = packets.ReadHeader(parameters.Header);
        int targets = header.ReadContactCount();
        byte groups = header.ReadRepeatModeMinusOne();
        int maximum = LastContactPosition(targets);
        if (maximum < groups)
        {
            maximum = groups;
        }
        return actors.WriteTargetTo(actors.WriteSourceTo(task).WriteByte(0x29, 0)).WriteContactCount(0x5A, targets).WriteWord(0x58, groups).WriteTraversalPosition(0x2F, maximum);
    }
}
