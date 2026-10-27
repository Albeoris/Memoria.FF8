namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCuragaNodeCycleStep(NativeBattleCuragaFrame Frame, NativeCrtRandomState Random);

public static partial class NativeBattleCuragaNodeKinematics
{
    public static NativeBattleCuragaNodeCycleStep AdvanceList8804D0(NativeBattleCuragaFrame frame, uint contextTask, NativeCrtRandomState random, Func<int, NativeBattleGraphicsModel> readModel, bool birth882E20 = false)
    {
        using NativeBattleTaskOperation? operation = NativeBattleTaskOperation.BeginStandalone(frame.Memory, frame.Effect);
        using NativeBattleCuragaNodeMemory memory = new(frame, contextTask);
        using NativeBattleEffectNodeBank bank = CurrentBank(memory);
        NativeBattleEffectNodeLifecycle core = CreateLifecycle(memory, bank, readModel);
        if (birth882E20)
        {
            core.Birth();
        }
        core.AdvanceList(ref random);
        return NativeBattleTaskOperation.CompleteStandalone<NativeBattleCuragaNodeCycleStep>(operation, new(FlushCurrent(memory, bank), random));
    }
}
