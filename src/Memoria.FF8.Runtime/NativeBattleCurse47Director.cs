namespace Memoria.FF8.Runtime;

public static class NativeBattleCurse47Director
{
    public const uint Callback = 0x82B170;

    public static NativeBattleCurse47Step Advance(NativeBattleCurse47Frame frame, uint address, NativeBattleCurse47DirectorInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs.Execute);
        Require(frame, address);
        frame = ObserveModes(frame, address, inputs);
        NativeBattleModulePoolTask task = Read(frame, address);
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                frame = RequestAndAdvance(frame, address, 1);
                break;
            case 1:
                if (Reached(frame.State, 1))
                {
                    frame = inputs.Execute(frame, new NativeBattleCurse47Sound(inputs.SoundValue, 128));
                    frame = Next(frame, address);
                }
                break;
            case 2:
                var released = NativeBattleCurse47Io.ReleaseWait(frame.State);
                frame = frame with { State = released.State };
                if (released.Released)
                {
                    frame = Next(frame, address);
                }
                break;
            case 3:
                if (frame.State.ReadIoControlWord(0x166) == 0)
                {
                    frame = Pause(frame, 0, inputs);
                    frame = Next(frame, address);
                }
                else if (unchecked((short)frame.State.SharedWord(0x46)) >= 53)
                {
                    frame = Pause(frame, 1, inputs);
                }
                break;
            case 4:
                if (unchecked((short)frame.State.SharedWord(0x46)) >= 54)
                {
                    frame = Next(frame, address);
                }
                break;
            case 5:
                frame = RequestAndAdvance(frame, address, 2);
                break;
            case 6:
                if (Reached(frame.State, 2))
                {
                    frame = Next(frame, address);
                }
                break;
            case 7:
                if (unchecked((short)frame.State.SharedWord(0x46)) > 50)
                {
                    frame = frame with { State = frame.State.WithSharedWord(0x48, 1) };
                    frame = Next(frame, address);
                }
                break;
            case 8:
                frame = RequestAndAdvance(frame, address, 3);
                break;
            case 9:
                if (Reached(frame.State, 3))
                {
                    frame = Next(frame, address);
                }
                break;
            case 10:
                frame = Put(frame, address, Read(frame, address).WriteByte(0x26, (byte)(Read(frame, address).ReadByte(0x26) | 1)));
                frame = Next(frame, address);
                break;
            case 11:
                break;
            default:
                throw new InvalidDataException("Curse director signed phase exceeds its twelve handlers.");
        }
        task = Read(frame, address);
        frame = Put(frame, address, task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1))));
        task = Read(frame, address);
        bool complete = (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
        if (complete)
        {
            frame = frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Complete8DC530(address).Pools } };
        }
        return new(frame, complete ? 2 : 0);
    }

    public static bool Reached(NativeBattleCurse47State state, short mode) => unchecked((short)state.SharedWord(0x40)) >= mode;

    public static bool TryRequest(NativeBattleCurse47State state, short mode, out NativeBattleCurse47State result)
    {
        ushort requested = state.SharedWord(0x44);
        bool success = requested == state.SharedWord(0x40) && unchecked((short)requested) == mode - 1;
        result = success ? state.WithSharedWord(0x44, unchecked((ushort)mode)) : state;
        return success;
    }

    private static NativeBattleCurse47Frame ObserveModes(NativeBattleCurse47Frame frame, uint address, NativeBattleCurse47DirectorInputs inputs)
    {
        NativeBattleCurse47State state = frame.State.WithSharedWord(0x46, unchecked((ushort)(frame.State.SharedWord(0x46) + 1)));
        if (state.SharedWord(0x40) != state.SharedWord(0x42))
        {
            ushort observed = state.SharedWord(0x42);
            frame = inputs.Execute(frame with { State = state.WithSharedWord(0x40, observed).WithSharedWord(0x46, 0) }, new NativeBattleCurse47ObservedMode(address, unchecked((short)observed)));
            state = frame.State;
        }
        if (state.SharedWord(0x42) != state.SharedWord(0x44))
        {
            state = state.WithSharedWord(0x42, state.SharedWord(0x44));
        }
        return frame with { State = state };
    }

    private static NativeBattleCurse47Frame Pause(NativeBattleCurse47Frame frame, int value, NativeBattleCurse47DirectorInputs inputs)
    {
        uint previous = frame.State.ReadDword(0x264EAE4);
        if (value == 0 ? previous == 1 : previous == 0)
        {
            frame = frame with { State = frame.State.WriteDword(0x264EAE4, (uint)value).WriteDword(0x264D82C, (uint)value) };
            frame = inputs.Execute(frame, new NativeBattleCurse47Pause(value));
        }
        return frame;
    }

    private static NativeBattleCurse47Frame RequestAndAdvance(NativeBattleCurse47Frame frame, uint address, short mode)
    {
        return TryRequest(frame.State, mode, out NativeBattleCurse47State requested) ? Next(frame with { State = requested }, address) : frame;
    }

    private static NativeBattleModulePoolTask Read(NativeBattleCurse47Frame frame, uint address) => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
    private static NativeBattleCurse47Frame Put(NativeBattleCurse47Frame frame, uint address, NativeBattleModulePoolTask task) => frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(address, task with { Next = Read(frame, address).Next }).Pools } };
    private static NativeBattleCurse47Frame Next(NativeBattleCurse47Frame frame, uint address) => Put(frame, address, Read(frame, address).WriteByte(0x29, unchecked((byte)(Read(frame, address).ReadByte(0x29) + 1))));

    private static void Require(NativeBattleCurse47Frame frame, uint address)
    {
        frame.State.Validate();
        NativeBattleCureFamilyPool expected = NativeBattleCurse47State.Pools[2];
        NativeBattleEffectTaskPool pool = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == expected.Descriptor);
        if (frame.Effect.Binding.Effect != 47 || frame.Effect.Binding.Entry != 0x82A7F0 || Read(frame, address).Callback != Callback || pool.BankAddress != expected.Bank || pool.Stride != expected.Stride || pool.Slots.Length != expected.Capacity || address < pool.BankAddress || address >= pool.BankAddress + pool.Stride * pool.Slots.Length || (address - pool.BankAddress) % pool.Stride != 0)
        {
            throw new InvalidDataException("Curse director requires its actual selected module and seventy-two-byte task bank.");
        }
    }
}
