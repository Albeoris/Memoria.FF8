namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCuragaSpawnStep(NativeBattleCuragaFrame Frame, NativeCrtRandomState Random, int Node);

public static partial class NativeBattleCuragaNodeKinematics
{
    public static NativeBattleCuragaSpawnStep Spawn8818A0(NativeBattleCuragaFrame frame, uint contextTask, int emitter, int instanceCount, NativeCrtRandomState random)
    {
        NativeBattleCuragaNodeMemory memory = new(frame, contextTask);
        NativeBattleEffectNodeBank bank = CurrentBank(memory);
        NativeBattleEffectNodeLifecycle core = CreateLifecycle(memory, bank);
        int node = core.Spawn(emitter, instanceCount, core.ReadSource(unchecked((sbyte)memory.Byte(emitter + 0x6A))), ref random);
        return new(FlushCurrent(memory, bank), random, node);
    }
}
