using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect80Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect80Controller
{
    public static NativeBattleEffect80Step AdvanceRoot(NativeBattleEffect80Frame frame, uint address, NativeBattleEffect80Resources resources, NativeBattleEffect80Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        bool odd = (task.ReadByte(0x5C) & 1) != 0;
        frame = frame with
        {
            Memory = frame.Memory with { SharedEffectView2793E58 = inputs.ReadView(frame) },
            State = frame.State.WriteDword(NodeViewPointerGlobal, ViewAddress).WriteDword(ViewPointerGlobal, ViewAddress).WriteDword(CursorGlobal, frame.State.ReadDword(odd ? 0x25DE424u : 0x25DE420u)).WriteDword(SecondaryCursorGlobal, frame.State.ReadDword(odd ? 0x25DD81Cu : 0x25DD818u))
        };
        frame = inputs.Execute(frame, new NativeBattleEffect80UpdateCaster8DC740(address));
        task = Read(frame, address);
        switch (Phase(frame, address))
        {
            case 0:
            case 5:
                frame = NextPhase(frame, address);
                break;
            case 1:
                frame = inputs.Execute(frame, new NativeBattleEffect80CopyActorPoint(PhysicalModelAddress(task.ReadByte(0x2C)), PhysicalModelAddress(task.ReadByte(0x2D)), -1768, true));
                frame = NextPhase(frame, address);
                break;
            case 2:
                if (task.ReadByte(0x28) == 0)
                {
                    uint emitters = frame.State.ReadDword(NextArenaGlobal);
                    uint geometry = checked(emitters + 0x870);
                    frame = frame with { State = frame.State.WriteDword(EmitterArenaGlobal, emitters).WriteDword(GeometryArenaGlobal, geometry).WriteDword(NextArenaGlobal, checked(geometry + 0x10680)) };
                    frame = ClearBytes(frame, emitters, 0x870);
                    frame = ClearBytes(frame, geometry, 0x10680);
                    frame = frame with { State = frame.State.WriteWord(EmitterHintGlobal, 0).WriteWord(GeometryHintGlobal, 0).WriteWord(ModelHintGlobal, 0).WriteWord(0x25D4940, 0) };
                    frame = NextPhase(frame, address);
                }
                break;
            case 3:
                frame = Write(frame, address, task.WriteByte(0x63, 1));
                (frame, _) = AppendHierarchy(frame, 1, DriverCallback, 0x58, address, inputs);
                frame = NextPhase(frame, address);
                break;
            case 4:
                if (task.ReadByte(0x63) == 0)
                {
                    if (unchecked((sbyte)task.ReadByte(0x2A)) < Signed(task, 0x58))
                    {
                        frame = Write(frame, address, task.WriteByte(0x2A, unchecked((byte)(task.ReadByte(0x2A) + 1))).WriteByte(0x2E, unchecked((byte)(task.ReadByte(0x2E) + 1))).WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) - 1))));
                    }
                    else
                    {
                        frame = NextPhase(frame, address);
                    }
                }
                break;
            case 6:
                if (task.ReadWord(0x5E) == 0)
                {
                    frame = NextPhase(frame, address);
                }
                break;
            case 7:
                frame = inputs.Execute(frame, new NativeBattleEffect80TextureLoad508630(frame.State.ReadDword(InitializerGlobal), address + 0x62));
                frame = NextPhase(frame, address);
                break;
            case 8:
                if (task.ReadByte(0x62) != 0)
                {
                    frame = NextPhase(frame, address);
                }
                break;
            case 9:
                frame = NextPhase(MarkComplete(frame, address), address);
                break;
            case 10:
                break;
            default:
                throw new InvalidDataException("Effect80 root signed phase leaves its eleven actual handlers.");
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(0x5E, 0));
        frame = frame with { State = frame.State.WriteWord(NodePassCounterGlobal, 0).WriteWord(GeometryPassCounterGlobal, 0) };
        foreach (int index in ScanOrder)
        {
            (frame, int survivors) = Scan(frame, index, resources, inputs);
            task = Read(frame, address);
            frame = Write(frame, address, task.WriteWord(0x5E, unchecked((ushort)(task.ReadWord(0x5E) + survivors))));
        }
        task = Read(frame, address);
        frame = Write(frame, address, task.WriteWord(0x5C, unchecked((ushort)(task.ReadWord(0x5C) + 1))));
        return Finish(frame, address);
    }

    public static NativeBattleEffect80Step AdvanceDriver(NativeBattleEffect80Frame frame, uint address, NativeBattleEffect80Resources resources, NativeBattleEffect80Inputs inputs)
    {
        frame = inputs.Execute(frame, new NativeBattleEffect80UpdateTarget8DC610(address));
        frame = inputs.Execute(frame, new NativeBattleEffect80UpdateBounds8DC870(address));
        NativeBattleModulePoolTask task = Read(frame, address);
        switch (Phase(frame, address))
        {
            case 0:
                frame = InitializeTarget7ABA50(frame, address, resources, inputs);
                (frame, _) = AppendHierarchy(frame, 2, ControlCallback, 0x48, address, inputs);
                frame = NextPhase(frame, address);
                break;
            case 1:
                if (frame.State.ReadWord(TargetAddress + 0x48) == 1)
                {
                    NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
                    int header = checked(Parameters(frame).HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A)));
                    if ((uint)header >= packets.Headers.Length)
                    {
                        throw new InvalidDataException("Effect80 contact header leaves its actual physical rows.");
                    }
                    int first = packets.Headers[header].FirstContactIndex ?? throw new NotSupportedException("Effect80 requires its actual first contact pointer.");
                    int contact = checked(first + unchecked((sbyte)task.ReadByte(0x2B)));
                    _ = packets.ReadContact(contact);
                    frame = inputs.Execute(frame, new NativeBattleEffect80Contact506690(contact));
                    frame = NextPhase(frame, address);
                }
                break;
            case 2:
                if (task.ReadByte(0x28) == 0 || frame.State.ReadWord(TargetAddress + 0x4C) == 1)
                {
                    frame = NextPhase(frame, address);
                }
                break;
            case 3:
                frame = MarkComplete(frame, address);
                uint root = task.ReadDword(0x10);
                frame = Write(frame, root, Read(frame, root).WriteByte(0x63, 0));
                frame = NextPhase(frame, address);
                break;
            case 4:
                break;
            default:
                throw new InvalidDataException("Effect80 driver signed phase leaves its five actual handlers.");
        }
        return Finish(frame, address);
    }

    public static NativeBattleEffect80Frame InitializeTarget7ABA50(NativeBattleEffect80Frame frame, uint address, NativeBattleEffect80Resources resources, NativeBattleEffect80Inputs inputs)
    {
        if (resources.TargetRecordAddress != TargetAddress)
        {
            throw new InvalidDataException("Effect80 target pointer changed its physical owner.");
        }
        NativeBattleModulePoolTask task = Read(frame, address);
        int target = task.ReadByte(0x2D);
        frame = ClearBytes(frame, TargetAddress, TargetLength);
        frame = WriteBytes(frame, TargetAddress + 8, inputs.ReadPosition8(frame, target), 8);
        frame = WriteBytes(frame, TargetAddress, inputs.ReadPosition8(frame, task.ReadByte(0x2C)), 8);
        frame = frame with { State = frame.State.WriteDword(TargetAddress + 0x1C, PhysicalModelAddress(target)) };
        short x = unchecked((short)(frame.State.ReadWord(TargetAddress + 8) - frame.State.ReadWord(TargetAddress)));
        short z = unchecked((short)(frame.State.ReadWord(TargetAddress + 0xC) - frame.State.ReadWord(TargetAddress + 4)));
        ushort angle = unchecked((ushort)((NativeWorldScriptedPathMath.NativeAngle(x, z) - 0x800) & 0xFFF));
        return frame with { State = frame.State.WriteWord(TargetAddress + 0x4A, angle) };
    }
}
