namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCuragaNodeLifetimeStep(NativeBattleCuragaFrame Frame, bool Complete);

public static partial class NativeBattleCuragaNodeKinematics
{
    public static NativeBattleCuragaNodeLifetimeStep CompleteGeometry881650(NativeBattleCuragaFrame frame, uint contextTask, int node)
    {
        using NativeBattleTaskOperation? operation = NativeBattleTaskOperation.BeginStandalone(frame.Memory, frame.Effect);
        using NativeBattleCuragaNodeMemory memory = new(frame, contextTask);
        using NativeBattleEffectNodeBank bank = CurrentBank(memory);
        NativeBattleEffectNodeLifecycle core = CreateLifecycle(memory, bank);
        bool complete = core.CompleteGeometry(node, core.ReadSource(unchecked((sbyte)memory.Byte(node + 0x1D6))));
        return NativeBattleTaskOperation.CompleteStandalone<NativeBattleCuragaNodeLifetimeStep>(operation, new(FlushCurrent(memory, bank), complete));
    }

    public static NativeBattleCuragaFrame AppendChildren882D90(NativeBattleCuragaFrame frame, uint contextTask, int node)
    {
        using NativeBattleTaskOperation? operation = NativeBattleTaskOperation.BeginStandalone(frame.Memory, frame.Effect);
        using NativeBattleCuragaNodeMemory memory = new(frame, contextTask);
        using NativeBattleEffectNodeBank bank = CurrentBank(memory);
        NativeBattleEffectNodeLifecycle core = CreateLifecycle(memory, bank);
        core.AppendChildren(node, core.ReadSource(unchecked((sbyte)memory.Byte(node + 0x1D6))));
        return NativeBattleTaskOperation.CompleteStandalone<NativeBattleCuragaFrame>(operation, FlushCurrent(memory, bank));
    }
}
