using static Memoria.FF8.Runtime.NativeBattleEffect79Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect79Controller
{
    public static NativeBattleEffect79Step AdvanceRoot(NativeBattleEffect79Frame frame, uint address, NativeBattleEffect79Resources resources, NativeBattleEffect79Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        bool odd = (task.ReadByte(0x5C) & 1) != 0;
        frame = frame with
        {
            Memory = frame.Memory with { SharedEffectView2793E58 = inputs.ReadView(frame) },
            State = frame.State.WriteDword(0x25EB3AC, ViewAddress).WriteDword(0x25E5024, ViewAddress).WriteDword(0x25DF33C, frame.State.ReadDword(odd ? 0x25EA344u : 0x25EA340u)).WriteDword(0x25E5020, frame.State.ReadDword(odd ? 0x25E973Cu : 0x25E9738u))
        };
        frame = inputs.Execute(frame, new NativeBattleEffect79UpdateCaster8DC740(address));
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
                    uint emitters = frame.State.ReadDword(0x25EB3B8);
                    uint geometry = checked(emitters + 0x870);
                    frame = frame with { State = frame.State.WriteDword(0x25EAF34, emitters).WriteDword(0x25EB3B0, geometry).WriteDword(0x25EB3B8, checked(geometry + 0x9D80)) };
                    frame = ClearBytes(frame, emitters, 0x870);
                    frame = ClearBytes(frame, geometry, 0x9D80);
                    frame = frame with { State = frame.State.WriteWord(0x25E8950, 0).WriteWord(0x25EAF40, 0).WriteWord(0x25EAD80, 0).WriteWord(0x25DF342, 0) };
                    frame = NextPhase(frame, address);
                }
                break;
            case 3:
                frame = Write(frame, address, task.WriteByte(0x63, 1));
                (frame, _) = AppendHierarchy(frame, 1, 0x7B4FF0, 0x58, address, inputs);
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
                    frame = frame with { State = frame.State.WriteByte(0x25DF340, 0) };
                    frame = inputs.Execute(frame, new NativeBattleEffect79QueueFile48D0A0(401, frame.State.ReadDword(InitializerGlobal), 0x7BE1E0));
                    frame = NextPhase(frame, address);
                }
                break;
            case 7:
                if (frame.State.ReadByte(0x25DF340) == 1)
                {
                    frame = frame with { State = frame.State.WriteByte(0x25DF340, 0) };
                    frame = inputs.Execute(frame, new NativeBattleEffect79StageMusic501A20(frame.State.ReadDword(InitializerGlobal), 0x25DF340));
                    frame = NextPhase(frame, address);
                }
                break;
            case 8:
                if (frame.State.ReadByte(0x25DF340) != 0)
                {
                    frame = inputs.Execute(frame, new NativeBattleEffect79MusicGain501B10(0, 127));
                    frame = NextPhase(frame, address);
                }
                break;
            case 9:
                frame = inputs.Execute(frame, new NativeBattleEffect79TextureLoad508630(frame.State.ReadDword(InitializerGlobal), address + 0x62));
                frame = NextPhase(frame, address);
                break;
            case 10:
                if (task.ReadByte(0x62) != 0)
                {
                    frame = NextPhase(frame, address);
                }
                break;
            case 11:
                frame = NextPhase(MarkComplete(frame, address), address);
                break;
            case 12:
                break;
            default:
                throw new InvalidDataException("Effect79 root signed phase leaves its thirteen actual handlers.");
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(0x5E, 0));
        frame = frame with { State = frame.State.WriteWord(0x25EB390, 0).WriteWord(0x25EAF30, 0) };
        foreach (int index in new[] { 1, 2, 9, 8, 3, 5, 6, 4, 7 })
        {
            (frame, int survivors) = Scan(frame, index, resources, inputs);
            task = Read(frame, address);
            frame = Write(frame, address, task.WriteWord(0x5E, unchecked((ushort)(task.ReadWord(0x5E) + survivors))));
        }
        task = Read(frame, address);
        frame = Write(frame, address, task.WriteWord(0x5C, unchecked((ushort)(task.ReadWord(0x5C) + 1))));
        return Finish(frame, address);
    }

    public static NativeBattleEffect79Step AdvanceMain(NativeBattleEffect79Frame frame, uint address, NativeBattleEffect79Resources resources, NativeBattleEffect79Inputs inputs)
    {
        frame = inputs.Execute(frame, new NativeBattleEffect79UpdateTarget8DC610(address));
        frame = inputs.Execute(frame, new NativeBattleEffect79UpdateBounds8DC870(address));
        NativeBattleModulePoolTask task = Read(frame, address);
        switch (Phase(frame, address))
        {
            case 0:
                frame = InitializeTarget7B5520(frame, address, resources, inputs);
                frame = InitializeIo7B50C0(frame);
                (frame, _) = AppendHierarchy(frame, 9, 0x7B51C0, 0x30, address, inputs);
                (frame, _) = AppendHierarchy(frame, 2, 0x7B55B0, 0x48, address, inputs);
                frame = NextPhase(frame, address);
                break;
            case 1:
                if (frame.State.ReadWord(TargetAddress + 0x48) == 1)
                {
                    NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
                    int header = checked(Parameters(frame).HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A)));
                    if ((uint)header >= packets.Headers.Length)
                    {
                        throw new InvalidDataException("Effect79 contact header leaves its actual physical rows.");
                    }
                    int first = packets.Headers[header].FirstContactIndex ?? throw new NotSupportedException("Effect79 requires its actual first contact pointer.");
                    int contact = checked(first + unchecked((sbyte)task.ReadByte(0x2B)));
                    if ((uint)contact >= NativeBattleActionPacketsState.ContactCapacity)
                    {
                        throw new InvalidDataException("Effect79 contact selector leaves its physical contact arena.");
                    }
                    frame = inputs.Execute(frame, new NativeBattleEffect79Contact506690(contact));
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
                throw new InvalidDataException("Effect79 main signed phase leaves its five actual handlers.");
        }
        return Finish(frame, address);
    }

    public static NativeBattleEffect79Frame InitializeTarget7B5520(NativeBattleEffect79Frame frame, uint address, NativeBattleEffect79Resources resources, NativeBattleEffect79Inputs inputs)
    {
        if (resources.TargetRecordAddress != TargetAddress)
        {
            throw new InvalidDataException("Effect79 target pointer changed physical owner.");
        }
        frame = ClearBytes(frame, TargetAddress, 0x54);
        NativeBattleGraphicsModel model = inputs.ReadModel(frame, Read(frame, address).ReadByte(0x2D));
        NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Effect79 requires its current target transform.");
        frame = frame with { State = frame.State.WriteWord(TargetAddress + 8, unchecked((ushort)transform.Position28.X)).WriteWord(TargetAddress + 10, unchecked((ushort)transform.Position28.Y)).WriteWord(TargetAddress + 12, unchecked((ushort)transform.Position28.Z)).WriteWord(TargetAddress + 14, unchecked((ushort)transform.Vertical34)) };
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
