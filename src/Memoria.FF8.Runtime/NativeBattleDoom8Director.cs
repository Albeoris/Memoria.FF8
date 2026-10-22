namespace Memoria.FF8.Runtime;

public static class NativeBattleDoom8Director
{
    public static NativeBattleDoom8Step Advance(NativeBattleDoom8Frame frame, uint address, NativeBattleDoom8DirectorInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs.Execute);
        NativeBattleDoom8Controller.RequireDirector(frame, address);
        frame = ObserveModes(frame, address, inputs);
        NativeBattleModulePoolTask task = NativeBattleDoom8Controller.Read(frame, address);
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                frame = inputs.Execute(frame, new NativeBattleDoom8UploadTim(0x181CFAC));
                frame = inputs.Execute(frame, new NativeBattleDoom8UploadTim(0x18253CC));
                frame = NativeBattleDoom8Controller.Write(frame, address, NativeBattleDoom8Controller.Read(frame, address).WriteWord(0x44, 12));
                frame = Next(frame, address);
                break;
            case 1:
                frame = RequestAndAdvance(frame, address, 1);
                break;
            case 2:
                if (Reached(frame, 1))
                {
                    frame = inputs.Execute(frame, new NativeBattleDoom8Sound(new(inputs.SoundValue, 0, null) { Pan = 128, RawPan = 128, Volume = 127 }));
                    frame = Next(frame, address);
                }
                break;
            case 3:
                var released = NativeBattleDoom8Io.Release8C5550(frame.State.ReadIo());
                frame = frame with { State = frame.State.WithIo(released.State) };
                if (released.Released)
                {
                    frame = Next(frame, address);
                }
                break;
            case 4:
                frame = RequestAndAdvance(frame, address, 2);
                break;
            case 5:
                if (frame.State.ReadIoControlWord(0x166) == 0)
                {
                    frame = Next(frame, address);
                }
                break;
            case 6:
                frame = RequestAndAdvance(frame, address, 3);
                break;
            case 7:
                if (Reached(frame, 4))
                {
                    frame = Next(frame, address);
                }
                break;
            case 8:
                if (unchecked((short)frame.State.SharedWord(0x46)) > 45)
                {
                    frame = Next(frame, address);
                }
                break;
            case 9:
                frame = RequestAndAdvance(frame, address, 5);
                break;
            case 10:
                if (Reached(frame, 5))
                {
                    frame = NativeBattleDoom8Controller.Write(frame, address, NativeBattleDoom8Controller.Read(frame, address).WriteWord(0x44, 1));
                    frame = Next(frame, address);
                }
                break;
            case 11:
                task = NativeBattleDoom8Controller.Read(frame, address);
                ushort countdown = unchecked((ushort)(task.ReadWord(0x44) - 1));
                frame = NativeBattleDoom8Controller.Write(frame, address, task.WriteWord(0x44, countdown));
                if (unchecked((short)countdown) <= 0)
                {
                    frame = Next(frame, address);
                }
                break;
            case 12:
                if (TryRequest(frame.State, 6, out NativeBattleDoom8State requested))
                {
                    frame = frame with { State = requested.WithSharedWord(0x48, 1) };
                    frame = Next(frame, address);
                }
                break;
            case 13:
                task = NativeBattleDoom8Controller.Read(frame, address);
                frame = NativeBattleDoom8Controller.Write(frame, address, task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                frame = Next(frame, address);
                break;
            case 14:
                break;
            default:
                throw new InvalidDataException("Doom director signed phase exceeds its fifteen handlers.");
        }
        return NativeBattleDoom8Controller.Finish(frame, address);
    }

    private static NativeBattleDoom8Frame ObserveModes(NativeBattleDoom8Frame frame, uint address, NativeBattleDoom8DirectorInputs inputs)
    {
        NativeBattleDoom8State state = frame.State.WithSharedWord(0x46, unchecked((ushort)(frame.State.SharedWord(0x46) + 1)));
        if (state.SharedWord(0x40) != state.SharedWord(0x42))
        {
            ushort observed = state.SharedWord(0x42);
            frame = inputs.Execute(frame with { State = state.WithSharedWord(0x40, observed).WithSharedWord(0x46, 0) }, new NativeBattleDoom8ModeChanged(address, unchecked((short)observed)));
            state = frame.State;
        }
        if (state.SharedWord(0x42) != state.SharedWord(0x44))
        {
            state = state.WithSharedWord(0x42, state.SharedWord(0x44));
        }
        return frame with { State = state };
    }

    internal static bool Reached(NativeBattleDoom8Frame frame, short mode) => unchecked((short)frame.State.SharedWord(0x40)) >= mode;

    internal static bool TryRequest(NativeBattleDoom8State state, short mode, out NativeBattleDoom8State result)
    {
        ushort requested = state.SharedWord(0x44);
        bool success = requested == state.SharedWord(0x40) && unchecked((short)requested) == mode - 1;
        result = success ? state.WithSharedWord(0x44, unchecked((ushort)mode)) : state;
        return success;
    }

    private static NativeBattleDoom8Frame RequestAndAdvance(NativeBattleDoom8Frame frame, uint address, short mode)
    {
        if (TryRequest(frame.State, mode, out NativeBattleDoom8State requested))
        {
            return Next(frame with { State = requested }, address);
        }
        return frame;
    }

    private static NativeBattleDoom8Frame Next(NativeBattleDoom8Frame frame, uint address)
    {
        NativeBattleModulePoolTask task = NativeBattleDoom8Controller.Read(frame, address);
        return NativeBattleDoom8Controller.Write(frame, address, task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1))));
    }
}
