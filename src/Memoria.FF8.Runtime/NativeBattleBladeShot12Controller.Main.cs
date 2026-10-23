using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleBladeShot12Controller
{
    public static (NativeBattleBladeShot12Frame Frame, int Result) AdvanceMain(NativeBattleBladeShot12Frame frame, uint address, NativeBattleBladeShot12Resources resources, NativeBattleBladeShot12Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int age = Signed(task, 12);
        bool firstBank = task.ReadDword(16) != 0;
        frame = Dword(frame, 0x2556100, firstBank ? 0x2546100u : 0x254E100u);
        frame = Write(frame, address, task.WriteDword(16, firstBank ? 0u : 1u));
        if (age == 5)
        {
            frame = inputs.Execute(frame, new NativeBattleBladeShot12Sound5013A0(resources.OpeningSound, 0, TaskView(Read(frame, address), 0x1C, 8)));
        }
        if ((ushort)age < 32)
        {
            frame = DrawOutbound(frame, address, resources, inputs);
        }
        else
        {
            frame = Write(frame, address, Read(frame, address).WriteWord(0x58, 0));
        }
        if ((uint)(age - 32) < 30)
        {
            frame = DrawReturn(frame, address, age, resources, inputs);
        }
        uint caster = frame.State.ReadDword(0x2545EF0);
        frame = inputs.Execute(frame, new NativeBattleBladeShot12ActorMeshMask124(caster, age < 62 ? 0x80u : 0u, age >= 62 ? 0x80u : 0u));
        if ((uint)(age - 32) < 4)
        {
            if (age == 32)
            {
                NativeBattleBladeShot12PointStep point = inputs.ReadBone502170(frame, frame.State.ReadDword(0x25460F8), 0xF1, 0);
                frame = WriteBytes(point.Frame, address + 0x4C, point.Point8, 6);
                NativeBattleActionHeaderSlot header = inputs.Packets.Headers[Parameters(frame).HeaderIndex7C];
                int first = header.FirstContactIndex ?? throw new NotSupportedException("Blade Shot requires its captured first physical contact pointer.");
                frame = inputs.Execute(frame, new NativeBattleBladeShot12Contact506690(first));
            }
            frame = DrawFlash(frame, 0x1362C80, age - 32, TaskView(Read(frame, address), 0x4C, 8), inputs);
        }
        if ((uint)(age - 32) < 5)
        {
            if (age == 32)
            {
                frame = WriteBytes(frame, address + 0x4C, TaskView(Read(frame, address), 0x2C, 8), 8);
            }
            frame = DrawFlash(frame, 0x13628FC, age - 32, TaskView(Read(frame, address), 0x4C, 8), inputs);
        }
        frame = DrawTransient6EA3E0(frame, address, age - 32, resources, inputs);
        frame = DrawTransient6EA3E0(frame, address, age - 34, resources, inputs);
        if ((uint)(age - 32) < 20)
        {
            int factor = unchecked((age - 32) << 12) / 20 >> 2;
            frame = DrawMorphPair(frame, address, 0x1362E3C, 0x136340C, 0x136359C, factor, 0xF0, 0x71, resources, inputs);
            frame = DrawMorphPair(frame, address, 0x136372C, 0x1363CFC, 0x1363E8C, factor, 0xF0, 0x11C, resources, inputs);
        }
        frame = SpawnRegular(frame, address, age, inputs);
        frame = SpawnBursts(frame, address, age);
        frame = SpawnTrails(frame, address, age);
        if ((uint)(age - 60) < 5)
        {
            task = Read(frame, address);
            NativeBattleEffectArena point = PointView(new(Signed(task, 0x2C), unchecked((short)(Signed(task, 0x2E) + 100)), Signed(task, 0x30)));
            frame = DrawFlash(frame, 0x1362C80, age - 60, point, inputs);
            if (age == 60)
            {
                frame = inputs.Execute(frame, new NativeBattleBladeShot12Sound5013A0(resources.EndingSound, 0, point));
            }
        }
        (frame, int survivorsA) = AdvanceChildren(frame, 1, resources, inputs);
        (frame, int survivorsB) = AdvanceChildren(frame, 2, resources, inputs);
        if (age >= 65 && survivorsA == 0 && survivorsB == 0)
        {
            return (frame, 2);
        }
        return (Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1)))), 0);
    }

    private static NativeBattleBladeShot12Frame DrawOutbound(NativeBattleBladeShot12Frame frame, uint address, NativeBattleBladeShot12Resources resources, NativeBattleBladeShot12Inputs inputs)
    {
        (frame, uint temporary) = Allocate(frame, 88);
        NativeBattleModulePoolTask task = Read(frame, address);
        int time = (Signed(task, 0x22) >> 1) - 1024;
        int cube = unchecked(time * time * time);
        int shape = time < 0 ? -NativeBattleRayBomb10Geometry.Sqrt56BEC0(unchecked(-cube)) : NativeBattleRayBomb10Geometry.Sqrt56BEC0(cube);
        int scale = (shape >> 4) + 2048;
        frame = NativeBattleBladeShot12Geometry.BlendPoint6E9B00(frame, address + 0x14);
        task = Read(frame, address);
        task = task.WriteWord(0x54, unchecked((ushort)(task.ReadWord(0x54) + ((unchecked((ushort)(scale - task.ReadWord(0x32))) * 4) + 660)))).WriteWord(0x32, unchecked((ushort)scale));
        task = CopyTask(task, 0x34, TaskView(task, 0x2C, 8), 8);
        int halfSine = NativeBattleCameraMath.Sine(scale >> 1);
        int amplitude = NativeBattleCameraMath.Sine(scale) >> 2;
        short x = unchecked((short)(Signed(task, 0x14) + (unchecked(Signed(task, 0x5C) * amplitude) >> 12)));
        short y = unchecked((short)(Signed(task, 0x16) + (unchecked(-(halfSine * halfSine)) >> 13)));
        short z = unchecked((short)(Signed(task, 0x18) + (unchecked(Signed(task, 0x5E) * amplitude) >> 12)));
        task = task.WriteWord(0x2C, unchecked((ushort)x)).WriteWord(0x2E, unchecked((ushort)y)).WriteWord(0x30, unchecked((ushort)z));
        frame = Write(frame, address, task);
        frame = DrawBlade(frame, address, temporary, resources, inputs);
        return Release(frame, 88);
    }

    private static NativeBattleBladeShot12Frame DrawReturn(NativeBattleBladeShot12Frame frame, uint address, int age, NativeBattleBladeShot12Resources resources, NativeBattleBladeShot12Inputs inputs)
    {
        (frame, uint temporary) = Allocate(frame, 88);
        NativeBattleModulePoolTask task = Read(frame, address);
        if (age == 32 && TaskView(task, 0x60, 1).Read(0, 1)[0] == 0)
        {
            short correctionX = unchecked((short)((Signed(task, 0x24) - Signed(task, 0x1C)) / 32 - Signed(task, 0x34) + Signed(task, 0x2C)));
            short correctionZ = unchecked((short)((Signed(task, 0x28) - Signed(task, 0x20)) / 32 - Signed(task, 0x38) + Signed(task, 0x30)));
            uint source0 = task.ReadDword(0x1C);
            uint source1 = task.ReadDword(0x20);
            task = CopyTask(task, 0x60, NativeBattleEffectArena.Clear().Write(0, new byte[] { 1 }), 1).WriteWord(0x44, unchecked((ushort)correctionX)).WriteWord(0x48, unchecked((ushort)correctionZ)).WriteDword(0x1C, task.ReadDword(0x24)).WriteDword(0x20, task.ReadDword(0x28)).WriteDword(0x24, source0).WriteDword(0x28, source1);
            frame = Write(frame, address, task);
            frame = InitializeInterpolation6E8C60(frame, address + 0x14, 30);
            task = Read(frame, address);
            short driftX = unchecked((short)(Signed(task, 0x44) + (Signed(task, 0x1C) - Signed(task, 0x24)) / 30));
            short driftZ = unchecked((short)(Signed(task, 0x48) + (Signed(task, 0x20) - Signed(task, 0x28)) / 30));
            frame = Write(frame, address, task.WriteWord(0x44, unchecked((ushort)(driftX >> 1))).WriteWord(0x48, unchecked((ushort)(driftZ >> 1))).WriteWord(0x5C, unchecked((ushort)-Signed(task, 0x5C))).WriteWord(0x5E, unchecked((ushort)-Signed(task, 0x5E))));
        }
        NativeBattleBladeShot12PointStep point = inputs.ReadBone502170(frame, frame.State.ReadDword(0x2545EF0), 7, 0);
        frame = WriteBytes(point.Frame, address + 0x24, point.Point8, 6);
        task = Read(frame, address);
        frame = Write(frame, address, task.WriteWord(0x26, unchecked((ushort)(Signed(task, 0x26) - 600))));
        frame = NativeBattleBladeShot12Geometry.BlendPoint6E9B00(frame, address + 0x14);
        task = Read(frame, address);
        task = task.WriteWord(0x54, unchecked((ushort)(task.ReadWord(0x54) + 528))).WriteWord(0x3C, unchecked((ushort)(task.ReadWord(0x3C) + task.ReadWord(0x44)))).WriteWord(0x40, unchecked((ushort)(task.ReadWord(0x40) + task.ReadWord(0x48))));
        int factor = 4096 - Signed(task, 0x22);
        int dx = unchecked(Signed(task, 0x3C) * factor) >> 12;
        int dz = unchecked(Signed(task, 0x40) * factor) >> 12;
        int dy = unchecked(-NativeBattleCameraMath.Sine(Signed(task, 0x22) >> 1)) >> 2;
        task = CopyTask(task, 0x34, TaskView(task, 0x2C, 8), 8);
        frame = Write(frame, address, task.WriteWord(0x2C, unchecked((ushort)(Signed(task, 0x14) + dx))).WriteWord(0x2E, unchecked((ushort)(Signed(task, 0x16) + dy))).WriteWord(0x30, unchecked((ushort)(Signed(task, 0x18) + dz))));
        frame = DrawBlade(frame, address, temporary, resources, inputs);
        return Release(frame, 88);
    }

    private static NativeBattleBladeShot12Frame DrawBlade(NativeBattleBladeShot12Frame frame, uint address, uint temporary, NativeBattleBladeShot12Resources resources, NativeBattleBladeShot12Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattlePackedMatrix32 matrix = NativeBattleBladeShot12Geometry.Rotation6E9AB0(TaskView(task, 0x54, 6), UnknownMatrix());
        frame = PlaceMatrix(frame, matrix, TaskView(task, 0x2C, 8), inputs);
        frame = PublishContext(frame, temporary, Context(frame, temporary, 88).Write(0, BitConverter.GetBytes(0x13646ECu)).Write(0x1C, new byte[4]), 88);
        return inputs.Execute(frame, new NativeBattleBladeShot12DrawStatic88(temporary, 0x13646EC));
    }

    private static NativeBattleBladeShot12Frame DrawMorphPair(NativeBattleBladeShot12Frame frame, uint address, uint source, uint firstShape, uint secondShape, int phase, uint flags, int spread, NativeBattleBladeShot12Resources resources, NativeBattleBladeShot12Inputs inputs)
    {
        (frame, uint temporary) = Allocate(frame, 88);
        int sine = NativeBattleCameraMath.Sine(phase);
        NativeBattleEffectArena context = Context(frame, temporary, 88).Write(0, BitConverter.GetBytes(source)).Write(8, new byte[3]).Write(12, BitConverter.GetBytes(sine)).Write(0x1C, BitConverter.GetBytes(flags));
        frame = PublishContext(frame, temporary, context, 88);
        frame = NativeBattleBladeShot12Geometry.Morph6E9970(frame, temporary, firstShape, secondShape, sine, resources);
        foreach (int direction in new[] { spread, -spread })
        {
            NativeBattleModulePoolTask task = Read(frame, address);
            NativeBattleActionMatrix rotation = new(NativeBattleDuelSharedPackets.Rotation6D9510(unchecked((short)(Signed(task, 0x56) + direction))), 0, 0, 0, 4096);
            frame = PlaceMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(rotation, null), TaskView(task, 0x4C, 8), inputs);
            frame = inputs.Execute(frame, new NativeBattleBladeShot12DrawStatic88(temporary, source));
        }
        return Release(frame, 88);
    }

    private static NativeBattleBladeShot12Frame DrawFlash(NativeBattleBladeShot12Frame frame, uint source, int phase, NativeBattleEffectArena point, NativeBattleBladeShot12Inputs inputs)
    {
        (frame, uint temporary) = Allocate(frame, 180);
        byte[] xyz = point.Read(0, 6);
        (byte[] w, bool[] known) = point.ReadSnapshot(6, 2);
        frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4))), known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(w) : null, 4096, -1024) };
        frame = PublishContext(frame, temporary, Context(frame, temporary, 180).Write(0, BitConverter.GetBytes(source)).Write(4, BitConverter.GetBytes(unchecked((ushort)phase))).Write(0x24, new byte[2]), 180);
        frame = inputs.Execute(frame, new NativeBattleBladeShot12DrawSprite180(temporary, source));
        return Release(frame, 180);
    }

    private static NativeBattleBladeShot12Frame PlaceMatrix(NativeBattleBladeShot12Frame frame, NativeBattlePackedMatrix32 packed, NativeBattleEffectArena point, NativeBattleBladeShot12Inputs inputs)
    {
        byte[] bytes = packed.Bytes.ToArray();
        bool[] known = packed.Known.ToArray();
        for (int axis = 0; axis < 3; axis++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20 + axis * 4), BinaryPrimitives.ReadInt16LittleEndian(point.Read(axis * 2, 2)));
            Array.Fill(known, true, 20 + axis * 4, 4);
        }
        packed = new(bytes.ToImmutableArray(), known.ToImmutableArray());
        NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), packed.ReadMatrix());
        ushort? padding = packed.Known[18] && packed.Known[19] ? BinaryPrimitives.ReadUInt16LittleEndian(packed.Bytes.AsSpan().Slice(18, 2)) : null;
        return LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(composed, padding));
    }

    private static NativeBattleBladeShot12Frame SpawnRegular(NativeBattleBladeShot12Frame frame, uint address, int age, NativeBattleBladeShot12Inputs inputs)
    {
        if ((uint)(age - 32) >= 10)
        {
            return frame;
        }
        for (int side = 2; side >= 1; side--)
        {
            (frame, uint child) = Append(frame, 0x2556130, 0x6E9B50);
            if (child == 0)
            {
                continue;
            }
            frame = NativeBattleBladeShot12Geometry.FillDwords6DA380(frame, child + 12, 0, 10);
            NativeBattleModulePoolTask root = Read(frame, address);
            NativeBattleModulePoolTask task = Read(frame, child);
            frame = DrawRandom(frame, out ushort draw);
            int angle = ((draw - 16384) >> 6) + ((side & 1) != 0 ? 1024 : -1024) + Signed(root, 0x56);
            task = task.WriteWord(0x14, unchecked((ushort)(Signed(root, 0x4C) + (NativeBattleCameraMath.Sine(angle) >> 3)))).WriteWord(0x18, unchecked((ushort)(Signed(root, 0x50) + (NativeBattleCameraMath.Cosine(angle) >> 3))));
            frame = DrawRandom(frame, out draw);
            NativeBattleModelTransform transform = inputs.ReadModel(frame, frame.State.ReadDword(0x25460F8)).Transform ?? throw new NotSupportedException("Blade Shot regular particles require the current recipient WORD36.");
            task = task.WriteWord(0x16, unchecked((ushort)(transform.Angle36 - (draw >> 8))));
            frame = DrawRandom(frame, out draw);
            angle = ((draw - 16384) >> 6) + ((side & 1) != 0 ? 400 : -400) + Signed(root, 0x56);
            short vx = unchecked((short)(NativeBattleCameraMath.Sine(angle) >> 4));
            short vz = unchecked((short)(NativeBattleCameraMath.Cosine(angle) >> 4));
            task = task.WriteWord(0x1C, unchecked((ushort)vx)).WriteWord(0x20, unchecked((ushort)vz)).WriteWord(0x24, unchecked((ushort)(-vx >> 5))).WriteWord(0x28, unchecked((ushort)(-vz >> 5)));
            frame = DrawRandom(frame, out draw);
            short vy = unchecked((short)(-draw >> 10));
            task = task.WriteWord(0x1E, unchecked((ushort)vy)).WriteWord(0x26, unchecked((ushort)(-vy >> 5))).WriteDword(0x10, 0x1362AEC).WriteWord(0x1A, 15);
            frame = DrawRandom(frame, out draw);
            frame = Write(frame, child, task.WriteWord(0x22, (ushort)((draw >> 4) + 3072)));
        }
        return frame;
    }

    private static NativeBattleBladeShot12Frame SpawnBursts(NativeBattleBladeShot12Frame frame, uint address, int age)
    {
        int phase = age - 32;
        if ((uint)phase >= 6 || (phase & 1) != 0)
        {
            return frame;
        }
        for (int kind = 0; kind < 2; kind++)
        {
            frame = DrawRandom(frame, out ushort start);
            ushort y = unchecked((ushort)((start >> 9) + (kind == 0 ? 480 : 768)));
            ushort z = unchecked((ushort)(phase * 136 + (kind == 0 ? 68 : 0)));
            for (int index = 0; index < 5; index++)
            {
                (frame, uint child) = Append(frame, 0x2556130, kind == 0 ? 0x6E9B50u : 0x6E9C10u);
                if (child != 0)
                {
                    frame = NativeBattleBladeShot12Geometry.FillDwords6DA380(frame, child + 12, 0, 10);
                    NativeBattleEffectArena angles = NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(2, BitConverter.GetBytes(y)).Write(4, BitConverter.GetBytes(z));
                    NativeBattleEffectArena direction = NativeBattleBladeShot12Geometry.SphericalDirection6E99F0(angles, NativeBattleEffectArena.Clear().MarkUnknown(0, 8));
                    NativeBattleModulePoolTask root = Read(frame, address);
                    NativeBattleModulePoolTask task = Read(frame, child);
                    direction = NativeBattleBladeShot12Geometry.RotateXZ6E9A50(direction, TaskView(task, 0x14, 8), Signed(root, 0x56));
                    task = CopyTask(task, 0x14, direction, 8);
                    task = CopyTask(task, 0x1C, TaskView(task, 0x14, 8), 8);
                    short x = unchecked((short)(Signed(root, 0x4C) + (Signed(task, 0x14) >> 3)));
                    short py = unchecked((short)(Signed(root, 0x4E) + (Signed(task, 0x16) >> 3)));
                    short pz = unchecked((short)(Signed(root, 0x50) + (Signed(task, 0x18) >> 3)));
                    int shift = kind == 0 ? 5 : 4;
                    short vx = unchecked((short)(Signed(task, 0x1C) >> shift));
                    short vy = unchecked((short)(Signed(task, 0x1E) >> shift));
                    short vz = unchecked((short)(Signed(task, 0x20) >> shift));
                    task = task.WriteWord(0x14, unchecked((ushort)x)).WriteWord(0x16, unchecked((ushort)py)).WriteWord(0x18, unchecked((ushort)pz)).WriteWord(0x1C, unchecked((ushort)vx)).WriteWord(0x1E, unchecked((ushort)vy)).WriteWord(0x20, unchecked((ushort)vz)).WriteWord(0x24, unchecked((ushort)(-vx >> 4))).WriteWord(0x26, unchecked((ushort)(-vy >> 4))).WriteWord(0x28, unchecked((ushort)(-vz >> 4))).WriteWord(0x1A, (ushort)(kind == 0 ? 13 : 8)).WriteDword(0x10, kind == 0 ? 0x136298Cu : 0x13627E4u);
                    if (kind == 0)
                    {
                        task = task.WriteWord(0x22, 0x800);
                    }
                    else
                    {
                        task = CopyTask(task, 0x2C, TaskView(task, 0x1C, 8), 8);
                    }
                    frame = Write(frame, child, task);
                }
                frame = DrawRandom(frame, out ushort increment);
                z = unchecked((ushort)(z + (increment >> 9) + 787));
            }
        }
        return frame;
    }

    private static NativeBattleBladeShot12Frame SpawnTrails(NativeBattleBladeShot12Frame frame, uint address, int age)
    {
        if (age >= 62)
        {
            return frame;
        }
        for (int side = 2; side >= 1; side--)
        {
            (frame, uint child) = Append(frame, 0x2556120, 0x6E9D60);
            if (child == 0)
            {
                continue;
            }
            frame = NativeBattleBladeShot12Geometry.FillDwords6DA380(frame, child + 12, 0, 5);
            NativeBattleModulePoolTask root = Read(frame, address);
            NativeBattleModulePoolTask task = CopyTask(Read(frame, child), 0x10, TaskView(root, 0x2C, 8), 8);
            task = CopyTask(task, 0x18, TaskView(root, 0x54, 8), 8);
            if ((side & 1) == 0)
            {
                task = task.WriteWord(0x18, unchecked((ushort)(task.ReadWord(0x18) - 330)));
            }
            frame = Write(frame, child, task.WriteWord(0x16, unchecked((ushort)(age < 8 ? age << 9 : 4096))));
        }
        return frame;
    }
    private static NativeBattlePackedMatrix32 UnknownMatrix() => new(new byte[32].ToImmutableArray(), Enumerable.Repeat(false, 32).ToImmutableArray());
}
