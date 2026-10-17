namespace Memoria.FF8.Runtime;

public static class NativeBattleRestorationGeneric
{
    /// <summary>88D4C0/883660: signed delay, initialize-before-first-advance, deferred completion and parent lifetime.</summary>
    public static NativeBattleRestorationLeafStep Advance(NativeBattleRestorationFrame frame, uint taskAddress, NativeBattleRestorationGenericInputs? inputs = null)
    {
        NativeBattleRestorationProfile profile = NativeBattleRestorationProfile.ForEffect(frame.Effect.Binding.Effect);
        if (frame.Effect.Binding.Entry != profile.Entry)
        {
            throw new InvalidDataException("Native restoration generic task belongs to another entry.");
        }
        NativeBattleModulePoolTask task = Read();
        if (task.Callback != profile.GenericCallback)
        {
            throw new InvalidDataException("Native restoration generic task requires its own original callback.");
        }
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                if (unchecked((short)task.ReadWord(0x24)) >= unchecked((short)task.ReadWord(0x298)))
                {
                    NativeBattleRestorationGenericInputs original = RequireInputs();
                    frame = original.InitializeContext(frame, taskAddress);
                    frame = original.AdvanceContext(frame, taskAddress).Frame;
                    task = Read();
                    Write(task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1))));
                }
                break;
            case 1:
                NativeBattleRestorationContextStep step = RequireInputs().AdvanceContext(frame, taskAddress);
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
                throw new InvalidDataException("Native88D4C0/883660 signed phase leaves its three original callbacks.");
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

        NativeBattleRestorationGenericInputs RequireInputs() => inputs ?? throw new NotSupportedException($"Native0x{profile.GenericCallback:X} requires its original mutable context initializer and node engine; a different healing effect is not a substitute.");
        NativeBattleModulePoolTask Read() => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(taskAddress);
        void Write(NativeBattleModulePoolTask updated)
        {
            NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
            frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Write(taskAddress, updated).Pools } };
        }
    }
}
