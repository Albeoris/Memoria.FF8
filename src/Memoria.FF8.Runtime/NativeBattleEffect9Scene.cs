namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect9StageColor(short Depth, bool ClearRgb) : NativeBattleEffect9Operation;
public sealed record NativeBattleEffect9CaptureBody(uint Address) : NativeBattleEffect9Operation;
public sealed record NativeBattleEffect9SetupBody(uint Address) : NativeBattleEffect9Operation;
public sealed record NativeBattleEffect9RestoreBody(byte Actor, ushort SavedFlags) : NativeBattleEffect9Operation;
public sealed record NativeBattleEffect9DrawBody(uint Address) : NativeBattleEffect9Operation;

public static class NativeBattleEffect9Scene
{
    public static NativeBattleEffect9Step Advance(NativeBattleEffect9Frame frame, uint address, NativeBattleEffect9Inputs inputs)
    {
        NativeBattleModulePoolTask task = NativeBattleEffect9Controller.Read(frame, address);
        if (task.Callback is 0x8C32D0 or 0x8C33C0)
        {
            return AdvanceStage(frame, address, inputs);
        }
        if (task.Callback != 0x8C3490)
        {
            throw new InvalidDataException("Effect9 scene requires its original callback.");
        }
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                frame = inputs.Execute(frame, new NativeBattleEffect9CaptureBody(address));
                frame = NativeBattleEffect9Controller.NextPhase(frame, address);
                break;
            case 1:
                frame = inputs.Execute(frame, new NativeBattleEffect9SetupBody(address));
                task = NativeBattleEffect9Controller.Read(frame, address).WriteByte(0x26, (byte)(NativeBattleEffect9Controller.Read(frame, address).ReadByte(0x26) | 8)).WriteWord(0x6E, unchecked((ushort)-40)).WriteWord(0x78, 16);
                frame = NativeBattleEffect9Controller.Write(frame, address, task);
                NativeBattleEffect9Step node = NativeBattleEffect9Controller.Append(frame, 3, 0x8C3C30, 996, address, inputs.Packets);
                frame = node.Frame;
                uint child = unchecked((uint)node.Result);
                frame = NativeBattleEffect9Controller.Write(frame, child, NativeBattleEffect9Controller.Read(frame, child).WriteDword(0x74, 0x1817EE8).WriteDword(0x78, 128).WriteWord(0x80, 0).WriteWord(0x82, 0));
                frame = NativeBattleEffect9Controller.Append(frame, 5, 0x8C3D40, 92, address, inputs.Packets).Frame;
                frame = NativeBattleEffect9Controller.NextPhase(frame, address);
                break;
            case 2:
            case 4:
                frame = Move(frame, task);
                short remaining = unchecked((short)(task.ReadWord(0x78) - 1));
                frame = NativeBattleEffect9Controller.Write(frame, address, task.WriteWord(0x78, unchecked((ushort)remaining)));
                if (remaining <= 0)
                {
                    frame = NativeBattleEffect9Controller.NextPhase(frame, address);
                }
                break;
            case 3:
                frame = Move(frame, task);
                short shade = unchecked((short)(frame.State.ReadWord(0x274F046) - 16));
                frame = frame with { State = frame.State.WriteWord(0x274F046, unchecked((ushort)shade)) };
                if (shade <= 0)
                {
                    frame = frame with { State = frame.State.WriteWord(0x274F046, 0).WriteWord(NativeBattleEffect9State.ControlAddress + 0x48, 1) };
                    frame = inputs.Execute(frame, new NativeBattleEffect9RestoreBody(task.ReadByte(0x2D), unchecked((ushort)task.ReadDword(0x74))));
                    frame = NativeBattleEffect9Controller.Write(frame, address, NativeBattleEffect9Controller.Read(frame, address).WriteByte(0x26, (byte)(task.ReadByte(0x26) & ~8)).WriteWord(0x78, 10));
                    frame = NativeBattleEffect9Controller.NextPhase(frame, address);
                }
                break;
            case 5:
                if (NativeBattleEffect9Controller.RequestControl(frame.State, 4) is { } state)
                {
                    frame = frame with { State = state };
                    frame = NativeBattleEffect9Controller.Write(frame, address, task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                    frame = NativeBattleEffect9Controller.NextPhase(frame, address);
                }
                break;
            case 6:
                break;
            default:
                throw new InvalidDataException("Effect9 body signed phase exceeds its seven handlers.");
        }
        if ((NativeBattleEffect9Controller.Read(frame, address).ReadByte(0x26) & 8) != 0)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect9DrawBody(address));
        }
        return NativeBattleEffect9Controller.Finish(frame, address);
    }

    private static NativeBattleEffect9Frame Move(NativeBattleEffect9Frame frame, NativeBattleModulePoolTask task)
    {
        short step = unchecked((short)task.ReadWord(0x6E));
        return frame with { State = frame.State.WriteWord(0x274F044, unchecked((ushort)(frame.State.ReadWord(0x274F044) + step))).WriteWord(0x27571BA, unchecked((ushort)(frame.State.ReadWord(0x27571BA) + step))) };
    }

    private static NativeBattleEffect9Step AdvanceStage(NativeBattleEffect9Frame frame, uint address, NativeBattleEffect9Inputs inputs)
    {
        NativeBattleModulePoolTask task = NativeBattleEffect9Controller.Read(frame, address);
        bool opening = task.Callback == 0x8C32D0;
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                if (opening)
                {
                    frame = NativeBattleEffect9Controller.Write(frame, address, task.WriteWord(0x1C, 0));
                    frame = inputs.Execute(frame, new NativeBattleEffect9StageColor(0, true));
                    frame = NativeBattleEffect9Controller.NextPhase(frame, address);
                }
                else if (unchecked((short)task.ReadWord(0x24)) > 24)
                {
                    frame = NativeBattleEffect9Controller.Write(frame, address, task.WriteWord(0x1C, 2048));
                    frame = NativeBattleEffect9Controller.NextPhase(frame, address);
                }
                break;
            case 1:
                short depth = unchecked((short)(task.ReadWord(0x1C) + (opening ? 384 : -256)));
                bool complete = opening ? depth >= 2048 : depth <= 0;
                if (complete)
                {
                    depth = opening ? (short)2048 : (short)0;
                    task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)).WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
                }
                frame = NativeBattleEffect9Controller.Write(frame, address, task.WriteWord(0x1C, unchecked((ushort)depth)));
                frame = inputs.Execute(frame, new NativeBattleEffect9StageColor(depth, false));
                break;
            case 2:
                break;
            default:
                throw new InvalidDataException("Effect9 stage signed phase exceeds its three handlers.");
        }
        return NativeBattleEffect9Controller.Finish(frame, address);
    }

    public static NativeBattleEffect9State InitializeBody(NativeBattleEffect9State state, uint modelAddress, short minimumY, short maximumY, short positionY)
    {
        state = state.Write(0x274EFF0, new byte[92]).WriteDword(0x274F030, 0x274F638).WriteDword(0x274F034, 0x27584D0).WriteDword(0x274F038, modelAddress).WriteDword(0x274F03C, 0x274EFEC).WriteDword(0x274F040, 0x274EFEC);
        state = state.WriteDword(0x27584D4, 0x274F638).WriteWord(0x27584E4, 0).WriteWord(0x27584E6, 0).WriteWord(0x27584E8, 320).WriteWord(0x27584EA, 0).Write(0x27584EC, [128, 128, 128]).WriteDword(0x27584F0, uint.MaxValue).WriteWord(0x27584F4, 0);
        return state.WriteWord(0x274F04A, unchecked((ushort)(maximumY - positionY))).WriteWord(0x274F048, unchecked((ushort)((minimumY + maximumY) / 2))).WriteWord(0x274F046, 64);
    }
}
