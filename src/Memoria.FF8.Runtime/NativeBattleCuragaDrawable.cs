namespace Memoria.FF8.Runtime;

public static partial class NativeBattleCuragaNodeKinematics
{
    public static NativeBattleCuragaFrame AdvanceDrawable880DB0(NativeBattleCuragaFrame frame, uint contextTask, int node)
    {
        using NativeBattleCuragaNodeMemory memory = new(frame, contextTask);
        NativeBattleEffectNodeBank bank = CurrentBank(memory);
        NativeBattleEffectNodeLifecycle core = CreateLifecycle(memory, bank);
        core.AdvanceDrawable(node, core.ReadSource(unchecked((sbyte)memory.Byte(node + 0x1D6))));
        return FlushCurrent(memory, bank);
    }
}
