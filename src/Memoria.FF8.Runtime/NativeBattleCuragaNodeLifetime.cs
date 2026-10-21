namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCuragaNodeLifetimeStep(NativeBattleCuragaFrame Frame, bool Complete);

public static partial class NativeBattleCuragaNodeKinematics
{
    public static NativeBattleCuragaNodeLifetimeStep CompleteGeometry881650(NativeBattleCuragaFrame frame, uint contextTask, int node)
    {
        NativeBattleCuragaNodeMemory memory = new(frame, contextTask);
        NativeBattleEffectNodeBank bank = CurrentBank(memory.Profile.RequireExecutable(frame.Memory));
        NativeBattleEffectNodeLifecycle core = CreateLifecycle(memory, bank);
        bool complete = core.CompleteGeometry(node, core.ReadSource(unchecked((sbyte)memory.Byte(node + 0x1D6))));
        return new(FlushCurrent(memory, bank), complete);
    }

    public static NativeBattleCuragaFrame AppendChildren882D90(NativeBattleCuragaFrame frame, uint contextTask, int node)
    {
        NativeBattleCuragaNodeMemory memory = new(frame, contextTask);
        NativeBattleEffectNodeBank bank = CurrentBank(memory.Profile.RequireExecutable(frame.Memory));
        NativeBattleEffectNodeLifecycle core = CreateLifecycle(memory, bank);
        core.AppendChildren(node, core.ReadSource(unchecked((sbyte)memory.Byte(node + 0x1D6))));
        return FlushCurrent(memory, bank);
    }
}
