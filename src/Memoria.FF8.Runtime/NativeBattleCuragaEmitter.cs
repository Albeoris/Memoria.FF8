namespace Memoria.FF8.Runtime;

/// <summary>Typed Curaga entry points for the shared original emitter initialization and parent-follow kernels.</summary>
public static partial class NativeBattleCuragaNodeKinematics
{
    public static NativeBattleCuragaFrame InitializeEmitter882990(NativeBattleCuragaFrame frame, uint contextTask, int node, Func<int, NativeBattleGraphicsModel> readModel)
    {
        using NativeBattleTaskOperation? operation = NativeBattleTaskOperation.BeginStandalone(frame.Memory, frame.Effect);
        using NativeBattleCuragaNodeMemory memory = new(frame, contextTask);
        using NativeBattleEffectNodeBank bank = CurrentBank(memory);
        NativeBattleEffectNodeLifecycle core = CreateLifecycle(memory, bank, readModel);
        core.InitializeEmitter(node, core.ReadSource(unchecked((sbyte)memory.Byte(node + 0x6A))));
        return NativeBattleTaskOperation.CompleteStandalone<NativeBattleCuragaFrame>(operation, FlushCurrent(memory, bank));
    }

    public static NativeBattleCuragaFrame FollowParent882150(NativeBattleCuragaFrame frame, uint contextTask, int node)
    {
        using NativeBattleTaskOperation? operation = NativeBattleTaskOperation.BeginStandalone(frame.Memory, frame.Effect);
        using NativeBattleCuragaNodeMemory memory = new(frame, contextTask);
        using NativeBattleEffectNodeBank bank = CurrentBank(memory);
        NativeBattleEffectNodeLifecycle core = CreateLifecycle(memory, bank);
        core.FollowParent(node, core.ReadSource(unchecked((sbyte)memory.Byte(node + 0x6A))));
        return NativeBattleTaskOperation.CompleteStandalone<NativeBattleCuragaFrame>(operation, FlushCurrent(memory, bank));
    }
}
