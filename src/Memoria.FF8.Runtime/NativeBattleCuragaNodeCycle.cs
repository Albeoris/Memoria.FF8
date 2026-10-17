namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCuragaNodeCycleStep(NativeBattleCuragaFrame Frame, NativeCrtRandomState Random);

public static partial class NativeBattleCuragaNodeKinematics
{
    public static NativeBattleCuragaNodeCycleStep AdvanceList8804D0(NativeBattleCuragaFrame frame, uint contextTask, NativeCrtRandomState random, Func<int, NativeBattleGraphicsModel> readModel, bool birth882E20 = false)
    {
        NativeBattleCuragaNodeMemory memory = new(frame, contextTask);
        NativeBattleEffectNodeBank bank = CurrentBank(frame.Memory.CuragaExecutable);
        NativeBattleEffectNodeLifecycle core = CreateLifecycle(memory, bank, readModel);
        if (birth882E20)
        {
            core.Birth();
        }
        core.AdvanceList(ref random);
        return new(FlushCurrent(memory, bank), random);
    }
}
