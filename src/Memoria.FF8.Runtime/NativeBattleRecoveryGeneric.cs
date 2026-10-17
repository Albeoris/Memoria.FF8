namespace Memoria.FF8.Runtime;

public static class NativeBattleRecoveryGeneric
{
    /// <summary>8124B0 and its recovery profiles: signed delay, initialize-before-first-advance, deferred completion and parent lifetime.</summary>
    public static NativeBattleRecoveryLeafStep Advance(NativeBattleRecoveryFrame frame, uint taskAddress, NativeBattleRecoveryGenericInputs? inputs = null)
    {
        NativeBattleRecoveryProfile profile = NativeBattleRecoveryProfile.ForEffect(frame.Effect.Binding.Effect);
        if (frame.Effect.Binding.Entry != profile.Entry)
        {
            throw new InvalidDataException("Native recovery generic task belongs to another entry.");
        }
        NativeBattleModulePoolTask task = Read();
        if (task.Callback != profile.GenericCallback)
        {
            throw new InvalidDataException("Native recovery generic task requires its own original callback.");
        }
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                if (unchecked((short)task.ReadWord(0x24)) >= unchecked((short)task.ReadWord(0x298)))
                {
                    NativeBattleRecoveryGenericInputs original = RequireInputs();
                    frame = original.InitializeContext(frame, taskAddress);
                    frame = original.AdvanceContext(frame, taskAddress).Frame;
                    task = Read();
                    Write(task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1))));
                }
                break;
            case 1:
                NativeBattleRecoveryContextStep step = RequireInputs().AdvanceContext(frame, taskAddress);
                frame = step.Frame;
                if (step.Complete)
                {
                    task = Read();
                    Write(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)).WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1))));
                }
                break;
            case 2:
                break;
            default:
                throw new InvalidDataException("Native8124B0 and its recovery profiles signed phase leaves its three original callbacks.");
        }
        task = Read();
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        Write(task);
        bool complete = (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
        if (complete)
        {
            NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
            frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Complete8DC530(taskAddress).Pools } };
        }
        return new(frame, complete ? 2 : 0);

        NativeBattleRecoveryGenericInputs RequireInputs() => inputs ?? throw new NotSupportedException($"Native0x{profile.GenericCallback:X} requires its original mutable context initializer and node engine; a different healing effect is not a substitute.");
        NativeBattleModulePoolTask Read() => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(taskAddress);
        void Write(NativeBattleModulePoolTask updated)
        {
            NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
            frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Write(taskAddress, updated).Pools } };
        }
    }
}
