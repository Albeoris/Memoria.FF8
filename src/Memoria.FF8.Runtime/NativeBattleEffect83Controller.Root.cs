using static Memoria.FF8.Runtime.NativeBattleEffect83Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect83Controller
{
    public static NativeBattleEffect83Step AdvanceRoot(NativeBattleEffect83Frame frame, uint address, NativeBattleEffect83Resources resources, NativeBattleEffect83Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        bool odd = (task.ReadByte(0x5C) & 1) != 0;
        frame = frame with
        {
            Memory = frame.Memory with { SharedEffectView2793E58 = inputs.ReadView(frame) },
            State = frame.State.WriteDword(0x25CFA40, ViewAddress).WriteDword(0x25CA080, ViewAddress).WriteDword(0x25C9F74, frame.State.ReadDword(odd ? 0x25CF3ECu : 0x25CF3E8u)).WriteDword(0x25CA07C, frame.State.ReadDword(odd ? 0x25CED2Cu : 0x25CED28u))
        };
        frame = inputs.Execute(frame, new NativeBattleEffect83UpdateCaster8DC740(address));
        task = Read(frame, address);
        switch (Phase(frame, address))
        {
            case 0:
            case 1:
            case 5:
                frame = NextPhase(frame, address);
                break;
            case 2:
                if (task.ReadByte(0x28) == 0)
                {
                    uint emitters = frame.State.ReadDword(0x25CFA4C);
                    uint geometry = checked(emitters + 0x10E0);
                    frame = frame with { State = frame.State.WriteDword(0x25CFA2C, emitters).WriteDword(0x25CFA44, geometry).WriteDword(0x25CFA4C, checked(geometry + 0x10680)) };
                    frame = ClearBytes(frame, emitters, 0x10E0);
                    frame = ClearBytes(frame, geometry, 0x10680);
                    frame = frame with { State = frame.State.WriteWord(0x25CA850, 0).WriteWord(0x25CFA38, 0).WriteWord(0x25CF878, 0).WriteWord(0x25C9F78, 0) };
                    frame = NextPhase(frame, address);
                }
                break;
            case 3:
                frame = Write(frame, address, task.WriteByte(0x63, 1));
                (frame, _) = AppendHierarchy(frame, 1, 0x797A90, 0x58, address, inputs);
                frame = NextPhase(frame, address);
                break;
            case 4:
                if (task.ReadByte(0x63) == 0)
                {
                    if (unchecked((sbyte)task.ReadByte(0x2A)) < Signed(task, 0x58))
                    {
                        frame = Write(frame, address, task.WriteByte(0x2A, unchecked((byte)(task.ReadByte(0x2A) + 1))).WriteByte(0x2E, unchecked((byte)(task.ReadByte(0x2E) + 1))).WriteByte(0x29, 3));
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
                frame = inputs.Execute(frame, new NativeBattleEffect83TextureLoad508630(frame.State.ReadDword(InitializerGlobal), address + 0x62));
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
                throw new InvalidDataException("Effect83 root signed phase leaves its eleven actual handlers.");
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(0x5E, 0));
        frame = frame with { State = frame.State.WriteWord(0x25CFA3A, 0).WriteWord(0x25CFA28, 0) };
        foreach (int index in new[] { 1, 2, 7, 3, 4, 5, 6 })
        {
            (frame, int survivors) = Scan(frame, index, resources, inputs);
            task = Read(frame, address);
            frame = Write(frame, address, task.WriteWord(0x5E, unchecked((ushort)(task.ReadWord(0x5E) + survivors))));
        }
        task = Read(frame, address);
        frame = Write(frame, address, task.WriteWord(0x5C, unchecked((ushort)(task.ReadWord(0x5C) + 1))));
        return Finish(frame, address);
    }

    public static NativeBattleEffect83Step AdvanceMain(NativeBattleEffect83Frame frame, uint address, NativeBattleEffect83Resources resources, NativeBattleEffect83Inputs inputs)
    {
        frame = inputs.Execute(frame, new NativeBattleEffect83UpdateTarget8DC610(address));
        frame = inputs.Execute(frame, new NativeBattleEffect83UpdateBounds8DC870(address));
        NativeBattleModulePoolTask task = Read(frame, address);
        switch (Phase(frame, address))
        {
            case 0:
                frame = InitializeTarget797B40(frame, address, resources, inputs);
                (frame, _) = AppendHierarchy(frame, 2, 0x797BF0, 0x48, address, inputs);
                frame = NextPhase(frame, address);
                break;
            case 1:
                if (frame.State.ReadWord(TargetAddress + 0x48) == 1)
                {
                    NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
                    int header = checked(Parameters(frame).HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A)));
                    if ((uint)header >= packets.Headers.Length)
                    {
                        throw new InvalidDataException("Effect83 contact header leaves its actual physical rows.");
                    }
                    int first = packets.Headers[header].FirstContactIndex ?? throw new NotSupportedException("Effect83 requires its actual first contact pointer.");
                    int contact = checked(first + unchecked((sbyte)task.ReadByte(0x2B)));
                    if ((uint)contact >= NativeBattleActionPacketsState.ContactCapacity)
                    {
                        throw new InvalidDataException("Effect83 contact selector leaves its physical contact arena.");
                    }
                    frame = inputs.Execute(frame, new NativeBattleEffect83Contact506690(contact));
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
                throw new InvalidDataException("Effect83 main signed phase leaves its five actual handlers.");
        }
        return Finish(frame, address);
    }

    public static NativeBattleEffect83Frame InitializeTarget797B40(NativeBattleEffect83Frame frame, uint address, NativeBattleEffect83Resources resources, NativeBattleEffect83Inputs inputs)
    {
        if (resources.TargetRecordAddress != TargetAddress)
        {
            throw new InvalidDataException("Effect83 target pointer changed physical owner.");
        }
        frame = ClearBytes(frame, TargetAddress, 0x54);
        frame = inputs.Execute(frame, new NativeBattleEffect83Point502170(PhysicalModelAddress(Read(frame, address).ReadByte(0x2D)), 0xF1, TargetAddress + 8));
        frame = frame with { State = frame.State.WriteWord(TargetAddress + 0xA, 0).WriteWord(TargetAddress, 0).WriteWord(TargetAddress + 2, 0).WriteWord(TargetAddress + 4, 0) };
        short x = unchecked((short)(frame.State.ReadWord(TargetAddress + 8) - frame.State.ReadWord(TargetAddress)));
        short z = unchecked((short)frame.State.ReadWord(TargetAddress + 0xC));
        short angle = unchecked((short)((NativeWorldScriptedPathMath.NativeAngle(x, z) - 0x800) & 0xFFF));
        frame = frame with { State = frame.State.WriteWord(TargetAddress + 0x4A, unchecked((ushort)angle)) };
        NativeBattleEffectArena matrix = PackedArena(frame.Memory.CaptureEffectLight1D99AB0());
        short[] identity = [4096, 0, 0, 0, 4096, 0, 0, 0, 4096];
        for (int index = 0; index < identity.Length; index++)
        {
            matrix = matrix.Write(index * 2, BitConverter.GetBytes(identity[index]));
        }
        matrix = matrix.Write(20, new byte[12]);
        (byte[] bytes, bool[] known) = matrix.ReadSnapshot(0, 32);
        NativeBattlePackedMatrix32 packed = new([.. bytes], [.. known]);
        frame = frame with { Memory = frame.Memory.PublishEffectLight1D99AB0(packed) };
        NativeBattleStatefulMatrixRotationStep rotated = NativeBattleStatefulMatrixRotation.Rotate(frame.Memory, RequireStaging(frame), packed, 'Y', angle);
        return frame with { Memory = rotated.Memory.PublishEffectLight1D99AB0(rotated.Matrix), Staging = rotated.Staging };
    }
}
