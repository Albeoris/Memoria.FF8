namespace Memoria.FF8.Runtime;

/// <summary>The normalized Curaga/Esuna/Full-life node kernels over one current byte view and explicit spell-owned operands.</summary>
internal sealed partial class NativeBattleEffectNodeLifecycle
{
    private readonly NativeBattleEffectNodeMemory memory;
    private readonly NativeBattleEffectNodeBank bank;
    private readonly NativeBattleEffectNodeInputs inputs;

    internal NativeBattleEffectNodeLifecycle(NativeBattleEffectNodeMemory memory, NativeBattleEffectNodeBank bank, NativeBattleEffectNodeInputs inputs)
    {
        this.memory = memory;
        this.bank = bank;
        this.inputs = inputs;
    }

    internal static (bool Complete, ushort EmitterCount, ushort GeometryCount) AdvanceContext(Func<NativeBattleModulePoolTask> readTask, Action<NativeBattleModulePoolTask> writeTask, Action birthUpdateAndDraw)
    {
        NativeBattleModulePoolTask task = readTask();
        bool complete = false;
        switch (unchecked((short)task.ReadWord(0x34 + 0x20)))
        {
            case 0:
                writeTask(task.WriteWord(0x34 + 0x20, unchecked((ushort)(task.ReadWord(0x34 + 0x20) + 1))));
                break;
            case 1:
                birthUpdateAndDraw();
                task = readTask();
                short counter = unchecked((short)(task.ReadWord(0x34 + 0x18) + 1));
                task = task.WriteWord(0x34 + 0x18, unchecked((ushort)counter));
                if (counter >= unchecked((short)task.ReadWord(0x34 + 0x1A)) && task.ReadWord(0x34 + 0x14) == 0 && task.ReadWord(0x34 + 0x16) == 0)
                {
                    task = task.WriteWord(0x34 + 0x20, unchecked((ushort)(task.ReadWord(0x34 + 0x20) + 1)));
                }
                writeTask(task);
                break;
            case 2:
                complete = true;
                break;
        }
        task = readTask();
        return (complete, task.ReadWord(0x34 + 0x14), task.ReadWord(0x34 + 0x16));
    }
}
