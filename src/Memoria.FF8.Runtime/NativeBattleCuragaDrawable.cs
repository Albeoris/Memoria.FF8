namespace Memoria.FF8.Runtime;

public static partial class NativeBattleCuragaNodeKinematics
{
    public static NativeBattleCuragaFrame AdvanceDrawable880DB0(NativeBattleCuragaFrame frame, uint contextTask, int node)
    {
        using NativeBattleTaskOperation? operation = NativeBattleTaskOperation.BeginStandalone(frame.Memory, frame.Effect);
        using NativeBattleCuragaNodeMemory memory = new(frame, contextTask);
        using NativeBattleEffectNodeBank bank = CurrentBank(memory);
        NativeBattleEffectNodeLifecycle core = CreateLifecycle(memory, bank);
        core.AdvanceDrawable(node, core.ReadSource(unchecked((sbyte)memory.Byte(node + 0x1D6))));
        return NativeBattleTaskOperation.CompleteStandalone<NativeBattleCuragaFrame>(operation, FlushCurrent(memory, bank));
    }
}
