using static Memoria.FF8.Runtime.NativeBattleLaserEye71Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleLaserEye71Controller
{
    private static NativeBattleLaserEye71TaskStep AdvanceRoot(NativeBattleLaserEye71Frame frame, uint address, NativeBattleLaserEye71Resources resources, NativeBattleLaserEye71Inputs inputs)
    {
        frame = frame with { Memory = frame.Memory with { SharedEffectView2793E58 = inputs.ReadCamera(frame) }, State = frame.State.WriteDword(0x25FCDA0, 0x2793E58).WriteDword(0x25FA42C, 0x2793E58) };
        bool odd = (Read(frame, address).ReadByte(0x5C) & 1) != 0;
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, frame.State.ReadDword(odd ? 0x25FCD84u : 0x25FCD80u)).WriteDword(PacketEndGlobal, frame.State.ReadDword(odd ? 0x25FC17Cu : 0x25FC178u)) };
        frame = inputs.RefreshRoot(frame, address);
        frame = AdvanceRootPhase(frame, address, inputs);
        frame = Write(frame, address, Read(frame, address).WriteWord(0x5E, 0));
        frame = frame with { State = frame.State.WriteWord(0x25FCD9A, 0).WriteWord(0x25FCD8A, 0) };
        for (int index = 1; index < 6; index++)
        {
            int selected = index;
            NativeBattleLaserEye71Frame current = frame;
            NativeBattleEffectTaskStep step = current.Effect.Pools[selected].Advance((pool, slot) =>
            {
                current = current with { Effect = current.Effect with { Pools = current.Effect.Pools.SetItem(selected, pool) } };
                NativeBattleLaserEye71TaskStep advanced = Advance(current, checked(pool.BankAddress + (uint)(slot * pool.Stride)), resources, inputs);
                current = advanced.Frame;
                return new(current.Effect.Pools[selected], advanced.Result);
            });
            frame = current with { Effect = current.Effect with { Pools = current.Effect.Pools.SetItem(selected, step.Pool) } };
            NativeBattleModulePoolTask root = Read(frame, address);
            frame = Write(frame, address, root.WriteWord(0x5E, unchecked((ushort)(root.ReadWord(0x5E) + step.Survivors))));
        }
        NativeBattleModulePoolTask task = Read(frame, address);
        frame = Write(frame, address, task.WriteWord(0x5C, unchecked((ushort)(task.ReadWord(0x5C) + 1))));
        return Finish(frame, address);
    }

    private static NativeBattleLaserEye71Frame AdvanceRootPhase(NativeBattleLaserEye71Frame frame, uint address, NativeBattleLaserEye71Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
            case 1:
            case 7:
            case 8:
                return NextPhase(frame, address);
            case 2:
                if (task.ReadByte(0x28) == 0)
                {
                    uint first = frame.State.ReadDword(0x25FCDD0);
                    NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Laser Eye requires its actual effect arena.");
                    arena = arena.Write(ArenaOffset(first, EmitterBytes), new byte[EmitterBytes]);
                    arena = arena.Write(ArenaOffset(first + EmitterBytes, GeometryBytes), new byte[GeometryBytes]);
                    frame = frame with { Memory = frame.Memory with { Arena = arena }, State = frame.State.WriteDword(0x25FCD8C, first).WriteDword(0x25FCDC8, first + EmitterBytes).WriteDword(0x25FCDD0, first + EmitterBytes + GeometryBytes).WriteWord(0x25FAAE0, 0).WriteWord(0x25FCD98, 0).WriteWord(0x25FCD88, 0).WriteWord(0x25FA410, 0) };
                    (frame, _) = Append(frame, 5, StageInCallback, address, inputs);
                    frame = NextPhase(frame, address);
                }
                return frame;
            case 3:
                frame = Write(frame, address, task.WriteByte(0x63, 1));
                (frame, _) = Append(frame, 1, TargetCallback, address, inputs);
                return NextPhase(frame, address);
            case 4:
                if (task.ReadByte(0x63) == 0)
                {
                    if (unchecked((sbyte)task.ReadByte(0x2A)) < Signed(task, 0x58))
                    {
                        return Write(frame, address, task.WriteByte(0x2A, unchecked((byte)(task.ReadByte(0x2A) + 1))).WriteByte(0x2E, unchecked((byte)(task.ReadByte(0x2E) + 1))).WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) - 1))));
                    }
                    frame = Write(frame, address, task.WriteWord(0x60, 15));
                    return NextPhase(frame, address);
                }
                return frame;
            case 5:
                short remaining = unchecked((short)(task.ReadWord(0x60) - 1));
                frame = Write(frame, address, task.WriteWord(0x60, unchecked((ushort)remaining)));
                if (remaining <= 0)
                {
                    (frame, _) = Append(frame, 5, StageOutCallback, address, inputs);
                    frame = NextPhase(frame, address);
                }
                return frame;
            case 6:
                return task.ReadWord(0x5E) == 0 ? NextPhase(frame, address) : frame;
            case 9:
                frame = Write(frame, address, task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                return NextPhase(frame, address);
            case 10:
                return frame;
            default:
                throw new InvalidDataException("Laser Eye root phase leaves its original eleven-entry table.");
        }
    }
}
