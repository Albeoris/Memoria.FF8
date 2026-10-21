namespace Memoria.FF8.Runtime;

public static partial class NativeBattleCuragaNodeKinematics
{
    public static NativeBattleCuragaFrame AdvanceMotion8805A0(NativeBattleCuragaFrame frame, uint contextTask, int node)
    {
        NativeBattleCuragaNodeMemory memory = new(frame, contextTask);
        NativeBattleEffectNodeBank bank = CurrentBank(NativeBattleCuragaFamilyProfile.ForGenericNodes(frame.Effect.Binding.Effect).RequireExecutable(frame.Memory));
        NativeBattleEffectNodeLifecycle core = CreateLifecycle(memory, bank);
        core.AdvanceMotion(node, core.ReadSource(unchecked((sbyte)memory.Byte(node + 0x1D6))));
        return FlushCurrent(memory, bank);
    }

    public static int Damp880C00(int ratio, int value)
    {
        return NativeBattleEffectNodeLifecycle.Damp880C00(ratio, value);
    }
}
