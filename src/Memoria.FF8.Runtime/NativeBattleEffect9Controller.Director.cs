namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect9Controller
{
    internal static NativeBattleEffect9Step AdvanceTarget(NativeBattleEffect9Frame frame, uint address, NativeBattleEffect9Inputs inputs)
    {
        frame = inputs.Execute(frame, new NativeBattleEffect9UpdateTarget(address));
        NativeBattleModulePoolTask task = Read(frame, address);
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                frame = inputs.Execute(frame, new NativeBattleEffect9InitializeControl(address));
                frame = Append(frame, 2, 0x8C0800, 72, address, inputs.Packets).Frame;
                frame = NextPhase(frame, address);
                break;
            case 1:
                if (frame.State.ReadWord(NativeBattleEffect9State.ControlAddress + 0x48) == 1)
                {
                    frame = inputs.Execute(frame, new NativeBattleEffect9Contact(address));
                    frame = NextPhase(frame, address);
                }
                break;
            case 2:
                if (task.ReadByte(0x28) == 0 || frame.State.ReadWord(NativeBattleEffect9State.ControlAddress + 0x4C) == 1)
                {
                    frame = NextPhase(frame, address);
                }
                break;
            case 3:
                frame = Write(frame, address, task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                uint root = task.ReadDword(0x10);
                frame = Write(frame, root, Read(frame, root).WriteByte(0x63, 0));
                frame = NextPhase(frame, address);
                break;
            case 4:
                break;
            default:
                throw new InvalidDataException("Effect9 target signed phase exceeds its five handlers.");
        }
        return Finish(frame, address);
    }

    public static NativeBattleEffect9State InitializeControl(NativeBattleEffect9State state, uint packedPosition28, uint packedPosition2C)
    {
        state = state.Write(NativeBattleEffect9State.ControlAddress, new byte[NativeBattleEffect9State.ControlLength])
            .WriteDword(NativeBattleEffect9State.ControlAddress + 8, packedPosition28)
            .WriteDword(NativeBattleEffect9State.ControlAddress + 12, packedPosition2C);
        int x = unchecked((short)packedPosition28);
        int z = unchecked((short)packedPosition2C);
        int angle = NativePrecision53.RoundInteger(Math.Atan2(x, z) * (double)BitConverter.Int32BitsToSingle(0x4422F983));
        return state.WriteWord(NativeBattleEffect9State.ControlAddress + 0x4A, unchecked((ushort)((angle - 2048) & 4095)));
    }

    internal static NativeBattleEffect9Step AdvanceControl(NativeBattleEffect9Frame frame, uint address, NativeBattleEffect9Inputs inputs)
    {
        const uint control = NativeBattleEffect9State.ControlAddress;
        frame = frame with { State = frame.State.WriteWord(control + 0x46, unchecked((ushort)(frame.State.ReadWord(control + 0x46) + 1))) };
        ushort actual = frame.State.ReadWord(control + 0x40);
        ushort pending = frame.State.ReadWord(control + 0x42);
        if (actual != pending)
        {
            frame = frame with { State = frame.State.WriteWord(control + 0x40, pending).WriteWord(control + 0x46, 0) };
            frame = EnterControl(frame, address, inputs.Packets, unchecked((short)pending));
        }
        ushort requested = frame.State.ReadWord(control + 0x44);
        if (frame.State.ReadWord(control + 0x42) != requested)
        {
            frame = frame with { State = frame.State.WriteWord(control + 0x42, requested) };
        }
        NativeBattleModulePoolTask task = Read(frame, address);
        short phase = unchecked((short)frame.State.ReadWord(control + 0x40));
        short ticks = unchecked((short)frame.State.ReadWord(control + 0x46));
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
            case 2:
            case 5:
            case 8:
                int next = task.ReadByte(0x29) switch { 0 => 1, 2 => 2, 5 => 3, _ => 5 };
                if (RequestControl(frame.State, next) is { } requestedState)
                {
                    frame = frame with { State = requestedState };
                    frame = NextPhase(frame, address);
                }
                break;
            case 1:
                if (ticks >= 5)
                {
                    frame = NextPhase(frame, address);
                }
                break;
            case 3:
                if (phase >= 2)
                {
                    NativeBattleModuleSound sound = new(inputs.DirectorSound, 0, null) { Pan = 0, RawPan = 0, Volume = 128 };
                    frame = inputs.Execute(frame, new NativeBattleEffect9Sound(sound));
                    frame = NextPhase(frame, address);
                }
                break;
            case 4:
                if (ticks >= 32)
                {
                    frame = NextPhase(frame, address);
                }
                break;
            case 6:
            case 7:
                if (phase >= (task.ReadByte(0x29) == 6 ? 3 : 4))
                {
                    frame = NextPhase(frame, address);
                }
                break;
            case 9:
                frame = Write(frame, address, task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                frame = NextPhase(frame, address);
                break;
            case 10:
                break;
            default:
                throw new InvalidDataException("Effect9 control signed phase exceeds its eleven handlers.");
        }
        return Finish(frame, address);
    }

    public static NativeBattleEffect9State? RequestControl(NativeBattleEffect9State state, int next)
    {
        const uint control = NativeBattleEffect9State.ControlAddress;
        short request = unchecked((short)next);
        ushort requested = state.ReadWord(control + 0x44);
        if (requested != state.ReadWord(control + 0x40) || unchecked((short)requested) != request - 1)
        {
            return null;
        }
        return state.WriteWord(control + 0x44, unchecked((ushort)request));
    }

    private static NativeBattleEffect9Frame EnterControl(NativeBattleEffect9Frame frame, uint address, NativeBattleActionPacketsState packets, short phase)
    {
        switch (phase)
        {
            case 1:
                return Append(frame, 4, 0x8C32D0, 124, address, packets).Frame;
            case 2:
                NativeBattleEffect9Step node = Append(frame, 3, 0x8C0BC0, 996, address, packets);
                frame = node.Frame;
                uint task = unchecked((uint)node.Result);
                frame = Write(frame, task, Read(frame, task).WriteWord(0x80, 0).WriteDword(0x74, 0x18155F0).WriteDword(0x78, 840).WriteWord(0x82, 0));
                NativeBattleEffect9Step overlay = Append(frame, 6, 0x8DDC30, 64, address, packets);
                frame = overlay.Frame;
                task = unchecked((uint)overlay.Result);
                return Write(frame, task, Read(frame, task).WriteDword(0x34, 0x1634F00).WriteWord(0x38, 17));
            case 3:
                return Append(frame, 4, 0x8C3490, 124, address, packets).Frame;
            case 4:
                return Append(frame, 4, 0x8C33C0, 124, address, packets).Frame;
            default:
                return frame;
        }
    }
}
