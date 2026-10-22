namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCurse47StageDepthWrite(NativeBattleStageFadeWrite Write) : NativeBattleCurse47Operation;

public static class NativeBattleCurse47ParticleStage
{
    public const uint Callback = 0x831870;

    public static NativeBattleCurse47Step Advance(NativeBattleCurse47Frame frame, uint address, Func<NativeBattleCurse47Frame, NativeBattleCurse47Operation, NativeBattleCurse47Frame> execute)
    {
        ArgumentNullException.ThrowIfNull(execute);
        frame.State.Validate();
        NativeBattleModulePoolTask task = NativeBattleCurse47Controller.Read(frame, address);
        NativeBattleCureFamilyPool expected = NativeBattleCurse47State.Pools[5];
        NativeBattleEffectTaskPool pool = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == expected.Descriptor);
        if (frame.Effect.Binding.Effect != 47 || frame.Effect.Binding.Entry != NativeBattleCurse47Controller.Entry || task.Callback != Callback || pool.BankAddress != expected.Bank || pool.Stride != 176 || pool.Slots.Length != 50 || address < pool.BankAddress || address >= pool.BankAddress + pool.Stride * pool.Slots.Length || (address - pool.BankAddress) % 176 != 0)
        {
            throw new InvalidDataException("Curse stage depth requires its original176-byte task and module callback.");
        }
        NativeBattleStageFadeWrite? write = null;
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                task = task.WriteWord(0x1C, 0).WriteByte(0x29, 1);
                write = new(0, true);
                break;
            case 1:
                short rising = unchecked((short)(task.ReadWord(0x1C) + 512));
                if (rising >= 1536)
                {
                    rising = 1536;
                    task = task.WriteByte(0x29, 2);
                }
                task = task.WriteWord(0x1C, unchecked((ushort)rising));
                write = new(rising, false);
                break;
            case 2:
                if (NativeBattleCurse47Director.Reached(frame.State, 3))
                {
                    task = task.WriteByte(0x29, 3);
                }
                break;
            case 3:
                short falling = unchecked((short)(task.ReadWord(0x1C) - 256));
                if (falling <= 0)
                {
                    falling = 0;
                    task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)).WriteByte(0x29, 4);
                }
                task = task.WriteWord(0x1C, unchecked((ushort)falling));
                write = new(falling, false);
                break;
            case 4:
                break;
            default:
                throw new InvalidDataException("Curse stage depth signed phase leaves its original five handlers.");
        }
        frame = NativeBattleCurse47Controller.Write(frame, address, task);
        if (write is not null)
        {
            frame = execute(frame, new NativeBattleCurse47StageDepthWrite(write));
        }
        return NativeBattleCurse47Controller.Finish(frame, address);
    }
}
