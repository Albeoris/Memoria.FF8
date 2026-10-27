namespace Memoria.FF8.Runtime;

public static partial class NativeBattleCuragaNodeKinematics
{
    public static NativeBattleCuragaFrame FollowAnimatedAnchor8822A0(NativeBattleCuragaFrame frame, uint contextTask, int node)
    {
        using NativeBattleCuragaNodeMemory memory = new(frame, contextTask);
        NativeBattleEffectNodeBank bank = CurrentBank(memory);
        NativeBattleEffectNodeLifecycle core = CreateLifecycle(memory, bank);
        NativeBattleEffectAnimatedAnchor.Advance(memory, node, core.ReadSource(unchecked((sbyte)memory.Byte(node + 0x6A))), bank.Read);
        return FlushCurrent(memory, bank);
    }
}
