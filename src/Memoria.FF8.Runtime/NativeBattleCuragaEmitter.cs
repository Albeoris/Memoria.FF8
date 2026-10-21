namespace Memoria.FF8.Runtime;

/// <summary>Typed Curaga entry points for the shared original emitter initialization and parent-follow kernels.</summary>
public static partial class NativeBattleCuragaNodeKinematics
{
    public static NativeBattleCuragaFrame InitializeEmitter882990(NativeBattleCuragaFrame frame, uint contextTask, int node, Func<int, NativeBattleGraphicsModel> readModel)
    {
        NativeBattleCuragaNodeMemory memory = new(frame, contextTask);
        NativeBattleEffectNodeBank bank = CurrentBank(NativeBattleCuragaFamilyProfile.ForEffect(frame.Effect.Binding.Effect).RequireExecutable(frame.Memory));
        NativeBattleEffectNodeLifecycle core = CreateLifecycle(memory, bank, readModel);
        core.InitializeEmitter(node, core.ReadSource(unchecked((sbyte)memory.Byte(node + 0x6A))));
        return FlushCurrent(memory, bank);
    }

    public static NativeBattleCuragaFrame FollowParent882150(NativeBattleCuragaFrame frame, uint contextTask, int node)
    {
        NativeBattleCuragaNodeMemory memory = new(frame, contextTask);
        NativeBattleEffectNodeBank bank = CurrentBank(NativeBattleCuragaFamilyProfile.ForEffect(frame.Effect.Binding.Effect).RequireExecutable(frame.Memory));
        NativeBattleEffectNodeLifecycle core = CreateLifecycle(memory, bank);
        core.FollowParent(node, core.ReadSource(unchecked((sbyte)memory.Byte(node + 0x6A))));
        return FlushCurrent(memory, bank);
    }
}
