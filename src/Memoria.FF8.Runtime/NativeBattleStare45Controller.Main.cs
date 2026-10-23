namespace Memoria.FF8.Runtime;

public static partial class NativeBattleStare45Controller
{
    private static (NativeBattleStare45Frame Frame, int Result) AdvanceRoot(NativeBattleStare45Frame frame, uint address, NativeBattleStare45Resources resources, NativeBattleStare45Inputs inputs)
    {
        NativeBattlePackedMatrix32 view = inputs.ReadView(frame);
        frame = frame with { Memory = frame.Memory with { SharedEffectView2793E58 = view } };
        frame = Dword(Dword(frame, 0x265574C, 0x2793E58), 0x26544B8, 0x2793E58);
        bool odd = (Read(frame, address).ReadWord(0x5C) & 1) != 0;
        frame = Dword(Dword(frame, 0x26543B4, frame.State.ReadDword(odd ? 0x26553C4u : 0x26553C0u)), 0x26544B4, frame.State.ReadDword(odd ? 0x2654FB4u : 0x2654FB0u));
        frame = inputs.Execute(frame, new NativeBattleStare45UpdateCaster8DC740(address));
        switch (Phase(frame, address))
        {
            case 0:
            case 1:
            case 5:
            case 7:
            case 8:
                frame = Next(frame, address);
                break;
            case 2:
                if (Read(frame, address).ReadByte(0x28) == 0)
                {
                    frame = Next(frame, address);
                }
                break;
            case 3:
                frame = Write(frame, address, Read(frame, address).WriteByte(0x63, 1));
                (frame, _) = Append(frame, 0x2655250, 0x836990, 88, address, inputs.ReadPackets(frame));
                frame = Next(frame, address);
                break;
            case 4:
                NativeBattleModulePoolTask root = Read(frame, address);
                if (root.ReadByte(0x63) == 0)
                {
                    if (unchecked((sbyte)root.ReadByte(0x2A)) < Signed(root, 0x58))
                    {
                        frame = Write(frame, address, root.WriteByte(0x2A, unchecked((byte)(root.ReadByte(0x2A) + 1))).WriteByte(0x2E, unchecked((byte)(root.ReadByte(0x2E) + 1))).WriteByte(0x29, unchecked((byte)(root.ReadByte(0x29) - 1))));
                    }
                    else
                    {
                        frame = Next(frame, address);
                    }
                }
                break;
            case 6:
                if (Read(frame, address).ReadWord(0x5E) == 0)
                {
                    frame = Next(frame, address);
                }
                break;
            case 9:
                frame = Done(frame, address);
                break;
            case 10:
                break;
            default:
                throw new NotSupportedException("Stare root phase exceeds its eleven-entry table.");
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(0x5E, 0));
        foreach (int pool in new[] { 1, 2, 4 })
        {
            (frame, int survivors) = AdvancePool(frame, pool, resources, inputs);
            frame = AddSurvivors(frame, address, survivors);
        }
        frame = BuildBasis836840(frame, inputs.ReadView(frame));
        foreach (int pool in new[] { 3, 5 })
        {
            (frame, int survivors) = AdvancePool(frame, pool, resources, inputs);
            frame = AddSurvivors(frame, address, survivors);
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(0x5C, unchecked((ushort)(Read(frame, address).ReadWord(0x5C) + 1))));
        return Finish(frame, address);
    }

    private static NativeBattleStare45Frame AddSurvivors(NativeBattleStare45Frame frame, uint address, int survivors)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        return Write(frame, address, task.WriteWord(0x5E, unchecked((ushort)(task.ReadWord(0x5E) + survivors))));
    }

    public static NativeBattleStare45Frame BuildBasis836840(NativeBattleStare45Frame frame, NativeBattlePackedMatrix32 camera)
    {
        NativeBattleActionMatrix matrix = camera.ReadMatrix();
        var up = NativeBattleActionGeometryRules.NormalizeVector56BC50(matrix.Rotation[1], matrix.Rotation[4], matrix.Rotation[7]);
        var right = NativeBattleActionGeometryRules.NormalizeVector56BC50(up.Y, unchecked(-up.X), 0);
        var cross = NativeBattleRayBomb10Geometry.Cross56BBF0(right, up);
        var forward = NativeBattleActionGeometryRules.NormalizeVector56BC50(cross.X, cross.Y, cross.Z);
        NativeBattleEffectArena basis = frame.State.ReadView(0x2655758, 32);
        foreach ((int offset, int value) in new[] { (0, right.X), (2, up.X), (4, forward.X), (6, right.Y), (8, up.Y), (10, forward.Y), (12, right.Z), (14, up.Z), (16, forward.Z) })
        {
            basis = basis.Write(offset, BitConverter.GetBytes(unchecked((short)value)));
        }
        basis = NativeBattleEffectNodeContextMemory.Copy(PackedArena(camera), 20, basis, 20, 12);
        return frame with { State = frame.State.WriteView(0x2655758, basis, 32) };
    }

    private static (NativeBattleStare45Frame Frame, int Result) AdvanceTarget(NativeBattleStare45Frame frame, uint address, NativeBattleStare45Resources resources, NativeBattleStare45Inputs inputs)
    {
        frame = inputs.Execute(frame, new NativeBattleStare45UpdateTargetBounds8DC610_8DC870(address));
        switch (Phase(frame, address))
        {
            case 0:
                frame = InitializeEffect836A40(frame, address, inputs);
                (frame, _) = Append(frame, 0x2654490, 0x836AC0, 72, address, inputs.ReadPackets(frame));
                frame = Next(frame, address);
                break;
            case 1:
                if (frame.State.ReadWord(0x2655410) == 1)
                {
                    NativeBattleModulePoolTask task = Read(frame, address);
                    NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
                    NativeBattleActionHeaderSlot header = Header(frame, task, packets);
                    int contact = checked((header.FirstContactIndex ?? throw new NotSupportedException("Stare requires its physical contact pointer.")) + unchecked((sbyte)task.ReadByte(0x2B)));
                    _ = packets.ReadContact(contact);
                    frame = inputs.Execute(frame, new NativeBattleStare45Contact506690(contact));
                    frame = Next(frame, address);
                }
                break;
            case 2:
                if (Read(frame, address).ReadByte(0x28) == 0 || frame.State.ReadWord(0x2655414) == 1)
                {
                    frame = Next(frame, address);
                }
                break;
            case 3:
                NativeBattleModulePoolTask target = Read(frame, address);
                frame = Write(frame, address, target.WriteByte(0x26, (byte)(target.ReadByte(0x26) | 1)));
                uint root = target.ReadDword(0x10);
                frame = Write(frame, root, Read(frame, root).WriteByte(0x63, 0));
                frame = Next(frame, address);
                break;
            case 4:
                break;
            default:
                throw new NotSupportedException("Stare target phase exceeds its five-entry table.");
        }
        return Finish(frame, address);
    }

    private static NativeBattleStare45Frame InitializeEffect836A40(NativeBattleStare45Frame frame, uint address, NativeBattleStare45Inputs inputs)
    {
        NativeBattleModelTransform transform = inputs.ReadModel(frame, Read(frame, address).ReadByte(0x2D)).Transform ?? throw new NotSupportedException("Stare requires its current recipient packed model position.");
        NativeBattleEffectArena view = NativeBattleEffectArena.Clear().Write(0, new byte[84]);
        view = NativeBattleEffectNodeContextMemory.Copy(PointView(transform.Position28, transform.Vertical34), 0, view, 8, 8);
        int angle = NativePrecision53.RoundInteger(Math.Atan2(transform.Position28.X, transform.Position28.Z) * (double)BitConverter.Int32BitsToSingle(0x4422F983));
        view = view.Write(0x4A, BitConverter.GetBytes(unchecked((ushort)((angle - 0x800) & 0xFFF))));
        return frame with { State = frame.State.WriteView(0x26553C8, view, 84) };
    }

    private static (NativeBattleStare45Frame Frame, int Result) AdvanceDirector(NativeBattleStare45Frame frame, uint address, NativeBattleStare45Resources resources, NativeBattleStare45Inputs inputs)
    {
        frame = Word(frame, 0x265540E, frame.State.ReadWord(0x265540E) + 1);
        ushort observed = frame.State.ReadWord(0x265540A);
        if (frame.State.ReadWord(0x2655408) != observed)
        {
            frame = Word(Word(frame, 0x2655408, observed), 0x265540E, 0);
            if (unchecked((short)observed) == 1)
            {
                frame = SpawnProgrammes836C80(frame, address, inputs);
            }
        }
        ushort requested = frame.State.ReadWord(0x265540C);
        if (frame.State.ReadWord(0x265540A) != requested)
        {
            frame = Word(frame, 0x265540A, requested);
        }
        switch (Phase(frame, address))
        {
            case 0:
                if (frame.State.ReadWord(0x265540C) == frame.State.ReadWord(0x2655408) && Word(frame, 0x265540C) == 0)
                {
                    frame = Next(Word(frame, 0x265540C, 1), address);
                }
                break;
            case 1:
                if (Word(frame, 0x2655408) >= 1)
                {
                    frame = inputs.Execute(frame, new NativeBattleStare45Sound501330(resources.Sound, 0, 128));
                    frame = Next(frame, address);
                }
                break;
            case 2:
                if (Word(frame, 0x265540E) >= 32)
                {
                    frame = Next(Word(frame, 0x2655410, 1), address);
                }
                break;
            case 3:
                frame = Done(frame, address);
                break;
            case 4:
                break;
            default:
                throw new NotSupportedException("Stare director phase exceeds its five-entry table.");
        }
        return Finish(frame, address);
    }

    private static NativeBattleStare45Frame SpawnProgrammes836C80(NativeBattleStare45Frame frame, uint address, NativeBattleStare45Inputs inputs)
    {
        (frame, uint first) = SpawnProgramme836D10(frame, address, 0x836D60, 0x17C4DA0, 452, 0, inputs);
        frame = Write(frame, first, Read(frame, first).WriteWord(0x8A, 0));
        (frame, uint second) = SpawnProgramme836D10(frame, address, 0x836D60, 0x17C4DA0, 452, 0, inputs);
        frame = Write(frame, second, Read(frame, second).WriteWord(0x8A, 1));
        (frame, _) = SpawnProgramme836D10(frame, address, 0x839590, 0x17C6A14, 572, 1, inputs);
        (frame, _) = Append(frame, 0x2654598, 0x839690, 48, address, inputs.ReadPackets(frame));
        return frame;
    }

    private static (NativeBattleStare45Frame Frame, uint Address) SpawnProgramme836D10(NativeBattleStare45Frame frame, uint parent, uint callback, uint source, short bytes, ushort selector, NativeBattleStare45Inputs inputs)
    {
        (frame, uint address) = Append(frame, 0x2654FA0, callback, 720, parent, inputs.ReadPackets(frame));
        NativeBattleModulePoolTask task = Read(frame, address).WriteDword(0x74, source).WriteDword(0x78, unchecked((uint)(int)bytes)).WriteWord(0x80, selector).WriteWord(0x82, 0);
        return (Write(frame, address, task), address);
    }

    private static (NativeBattleStare45Frame Frame, int Result) AdvanceCasterProgramme(NativeBattleStare45Frame frame, uint address, NativeBattleStare45Resources resources, NativeBattleStare45Inputs inputs)
    {
        switch (Phase(frame, address))
        {
            case 0:
                frame = InitializeCasterProgramme836DC0(frame, address, resources, inputs);
                break;
            case 1:
                (frame, int remaining) = AdvanceCasterProgramme8394C0(frame, address, resources, inputs);
                if (remaining == 0)
                {
                    frame = Done(frame, address);
                }
                break;
            case 2:
                break;
            default:
                throw new NotSupportedException("Stare caster programme phase exceeds its three-entry table.");
        }
        return Finish(frame, address);
    }

    private static (NativeBattleStare45Frame Frame, int Result) AdvanceTargetProgramme(NativeBattleStare45Frame frame, uint address, NativeBattleStare45Resources resources, NativeBattleStare45Inputs inputs)
    {
        switch (Phase(frame, address))
        {
            case 0:
                NativeBattleModulePoolTask task = Read(frame, address);
                if (Signed(task, 0x24) > 14)
                {
                    int length = checked((int)task.ReadDword(0x78));
                    NativeBattleGfAnimationWorkspace workspace = ProgrammeWorkspace(task, false).Initialize7016B0(resources.ReadProgramme(task.ReadDword(0x74)), length);
                    frame = WriteBytes(frame, address + 0x94, NativeBattleEffectArena.Clear().Write(0, workspace.Data.AsSpan().Slice(0, length)), length);
                    NativeBattleStare45PointStep bone = inputs.ReadBone502170(frame, task.ReadByte(0x2D), 0xF1, 0);
                    frame = WriteBytes(bone.Frame, address + 0x1C, bone.Point8, 6);
                    frame = Write(frame, address, Read(frame, address).WriteDword(0x50, 4096).WriteDword(0x54, 4096).WriteDword(0x58, 4096).WriteWord(0x88, 0xFF80));
                    (frame, _) = DrawProgramme836EB0(frame, address, resources, inputs);
                    frame = Next(frame, address);
                }
                break;
            case 1:
                (frame, int remaining) = DrawProgramme836EB0(frame, address, resources, inputs);
                if (remaining == 0)
                {
                    frame = Done(frame, address);
                }
                break;
            case 2:
                break;
            default:
                throw new NotSupportedException("Stare recipient programme phase exceeds its three-entry table.");
        }
        return Finish(frame, address);
    }

    private static (NativeBattleStare45Frame Frame, int Result) AdvanceStage(NativeBattleStare45Frame frame, uint address, NativeBattleStare45Inputs inputs)
    {
        switch (Phase(frame, address))
        {
            case 0:
                frame = Write(frame, address, Read(frame, address).WriteWord(0x1C, 0));
                for (int group = 0; group < 4; group++)
                {
                    frame = inputs.Execute(frame, new NativeBattleStare45StageColorDepth(group, 0));
                    frame = inputs.Execute(frame, new NativeBattleStare45StageRgb(group, 0, 0, 0));
                }
                frame = Next(frame, address);
                break;
            case 1:
                short up = unchecked((short)(Read(frame, address).ReadWord(0x1C) + 512));
                if (up >= 1536)
                {
                    up = 1536;
                    frame = Next(frame, address);
                }
                frame = Write(frame, address, Read(frame, address).WriteWord(0x1C, unchecked((ushort)up)));
                frame = PublishStageDepth(frame, up, inputs);
                break;
            case 2:
                if (Signed(Read(frame, address), 0x24) > 40)
                {
                    frame = Next(frame, address);
                }
                break;
            case 3:
                short down = unchecked((short)(Read(frame, address).ReadWord(0x1C) - 256));
                if (down <= 0)
                {
                    down = 0;
                    frame = Done(frame, address);
                }
                frame = Write(frame, address, Read(frame, address).WriteWord(0x1C, unchecked((ushort)down)));
                frame = PublishStageDepth(frame, down, inputs);
                break;
            case 4:
                break;
            default:
                throw new NotSupportedException("Stare stage phase exceeds its five-entry table.");
        }
        return Finish(frame, address);
    }

    private static NativeBattleStare45Frame PublishStageDepth(NativeBattleStare45Frame frame, short depth, NativeBattleStare45Inputs inputs)
    {
        for (int group = 0; group < 4; group++)
        {
            frame = inputs.Execute(frame, new NativeBattleStare45StageColorDepth(group, depth));
        }
        return frame;
    }

    private static NativeBattleActionHeaderSlot Header(NativeBattleStare45Frame frame, NativeBattleModulePoolTask task, NativeBattleActionPacketsState packets)
    {
        if (task.ReadDword(0x0C) != 0x1D99A78)
        {
            throw new NotSupportedException("Stare requires its captured parameter pointer.");
        }
        int index = checked(Parameters(frame).HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A)));
        if ((uint)index >= packets.Headers.Length)
        {
            throw new NotSupportedException("Stare signed header offset leaves its action arena.");
        }
        return packets.Headers[index];
    }
}
