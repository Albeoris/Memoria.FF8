using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect66Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect66Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect66Lower
{
    public static NativeBattleEffect66Step AdvanceBirths624AE0(NativeBattleEffect66Frame frame, uint task, NativeBattleEffect66Resources resources, NativeBattleEffect66Inputs inputs)
    {
        if (Paused(frame, inputs))
        {
            return new(frame, 0);
        }
        ushort age = unchecked((ushort)(Read(frame, task).ReadWord(12) + 1));
        frame = Write(frame, task, Read(frame, task).WriteWord(12, age));
        if (unchecked((short)age) > 104)
        {
            return new(frame, 2);
        }
        if (unchecked((short)age) % 3 != 1)
        {
            return new(frame, 0);
        }
        frame = Random(frame, out int boneIndex);
        frame = Random(frame, out int offset);
        int bone = BinaryPrimitives.ReadInt32LittleEndian(resources.ReadStatic(0xDEA360 + (uint)((boneIndex % 29) * 4), 4));
        NativeBattleEffect66PointStep point = inputs.ReadBone502170(frame, (int)frame.State.ReadDword(CasterGlobal), bone, offset & 4095, NativeBattleEffectArena.Clear().MarkUnknown(0, 8));
        frame = point.Frame;
        (frame, uint ring) = Mandatory(frame, 0x624CB0);
        frame = WriteBytes(frame, ring + 0x10, point.Point8, 8);
        frame = Random(frame, out int angle);
        frame = Random(frame, out int speed);
        int scale = (speed % 5632 + 3072) / 3;
        frame = Write(frame, ring, Read(frame, ring).WriteWord(12, 0).WriteWord(0x18, (ushort)((angle & 511) + 128)).WriteWord(0x1C, (ushort)scale).WriteWord(0x1E, (ushort)scale));
        uint[] clouds = [0x624DF0, 0x625110, 0x625470];
        for (int index = 0; index < 3; index++)
        {
            (frame, uint cloud) = Mandatory(frame, clouds[index]);
            frame = WriteBytes(frame, cloud + 0x10, point.Point8, 8);
            frame = Write(frame, cloud, Read(frame, cloud).WriteWord(12, 0).WriteWord(14, unchecked((ushort)(age + (index + 1) * 100))));
        }
        ushort births = unchecked((ushort)(Read(frame, task).ReadWord(14) + 1));
        frame = Write(frame, task, Read(frame, task).WriteWord(14, births));
        if (unchecked((short)births) % 6 == 3)
        {
            frame = Random(frame, out int chance);
            if (chance % 100 < 60)
            {
                frame = inputs.Execute(frame, new NativeBattleEffect66Flash5712C0(0, 2, 6, 255));
            }
        }
        return new(frame, 0);
    }

    public static NativeBattleEffect66Step AdvanceStatic(NativeBattleEffect66Frame frame, uint callback, uint task, NativeBattleEffect66Resources resources, NativeBattleEffect66Inputs inputs)
    {
        NativeBattleModulePoolTask record = Read(frame, task);
        int age = Signed(record, 12);
        int scale = Signed(record, 0x1C);
        int yScale = callback == 0x6269D0 ? Signed(record, 0x20) : scale;
        short angle = callback is 0x627510 or 0x627730 ? (short)0 : Signed(record, 0x18);
        NativeBattleActionMatrix local = NativeBattlePoseMatrices.Scale56BEF0(NativeBattlePandemona291Restoration.Rotation701270(angle), scale, yScale, callback is 0x627510 or 0x627730 ? 0 : scale);
        NativeBattleVector point = Vector(TaskView(record, 0x10, 8));
        if (callback == 0x624CB0)
        {
            NativeBattleEffectArena raw = TaskView(record, 0x10, 8);
            (byte[] bytes, bool[] known) = raw.ReadSnapshot(6, 2);
            short? w = known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
            frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), point, w, Signed(record, 0x1C), unchecked((short)-(short)frame.State.ReadWord(CenterAddress + 6))) };
            frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(local, null), true, false);
        }
        else if (callback is 0x627510 or 0x627730)
        {
            if (callback == 0x627510)
            {
                local = NativeBattlePoseMatrices.Scale56BEF0(NativeBattlePandemona291Restoration.Rotation701270(0), scale, Signed(record, 0x20), 0);
            }
            frame = ProjectMatrix627630(frame, TaskView(record, 0x10, 8), 4096, 0, inputs);
            frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(local, null), true, false);
        }
        else
        {
            local = local with { X = point.X, Y = point.Y, Z = point.Z };
            NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local);
            frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(matrix, null));
        }
        uint source = callback switch { 0x624CB0 => 0xDE4A54, 0x6269D0 => 0xDE562C, 0x626C50 => 0xDE7F54, 0x626E20 => 0xDE98E4, 0x627510 or 0x627730 => 0xDE87FC, _ => throw new InvalidDataException("Effect66 static callback is unsupported.") };
        int? fade = null;
        if (callback == 0x624CB0 && age >= 2)
        {
            fade = (age - 2) << 9;
        }
        else if (callback == 0x626C50)
        {
            int rise = Signed(record, 0x16);
            int fall = Signed(record, 0x22);
            int start = Signed(record, 14) - fall;
            if (age < rise)
            {
                fade = 4096 - 4096 / rise * age;
            }
            else if (age >= start)
            {
                fade = (age - start) * (4096 / fall);
            }
        }
        else if (callback == 0x626E20)
        {
            if (age < 6)
            {
                fade = 4096 - age * 682;
            }
            else if (age >= 34)
            {
                fade = (age - 34) << 8;
            }
        }
        else if (callback == 0x627510 && age >= 22)
        {
            fade = (age - 22) << 10;
        }
        (frame, uint temporary) = Allocate(frame, 88);
        NativeBattleEffectArena context = Context(frame, temporary, 88).Write(0, BitConverter.GetBytes(source)).Write(8, new byte[4]).Write(0x1C, BitConverter.GetBytes(fade is null ? 0x33u : 0xF3u));
        if (fade is int depthCue)
        {
            context = context.Write(12, BitConverter.GetBytes(depthCue));
        }
        frame = PublishContext(frame, temporary, context, 88);
        frame = NativeBattleEffect66Drawing.DrawStatic(frame, temporary, source, callback is 0x627510 or 0x627730 ? (byte)14 : callback == 0x626E20 ? (byte)3 : (byte)2, CursorGlobal, resources, inputs, NativeBattlePilotVisualLifetimes.Identity(66, record) with { ResourceGeneration = source });
        frame = Release(frame, 88);
        if (Paused(frame, inputs))
        {
            return new(frame, 0);
        }
        record = Read(frame, task);
        int velocity = Signed(record, 0x1E);
        int end;
        switch (callback)
        {
            case 0x624CB0:
                record = record.WriteWord(0x1C, unchecked((ushort)(Signed(record, 0x1C) + velocity))).WriteWord(0x1E, unchecked((ushort)(velocity - velocity / 4)));
                end = 10;
                break;
            case 0x6269D0:
                record = record.WriteWord(0x18, unchecked((ushort)(record.ReadWord(0x18) + record.ReadWord(0x1A))));
                if (age < 110)
                {
                    record = record.WriteWord(0x1C, unchecked((ushort)(Signed(record, 0x1C) - velocity))).WriteWord(0x1E, unchecked((ushort)(velocity - velocity / 32)));
                }
                end = 120;
                break;
            case 0x626C50:
                record = record.WriteWord(0x18, unchecked((ushort)(record.ReadWord(0x18) + record.ReadWord(0x1A)))).WriteWord(0x1C, unchecked((ushort)(Signed(record, 0x1C) + velocity)));
                end = Signed(record, 14);
                break;
            case 0x626E20:
                record = record.WriteWord(0x1C, unchecked((ushort)(Signed(record, 0x1C) + velocity))).WriteWord(0x1E, unchecked((ushort)(velocity - velocity / 64)));
                end = 50;
                break;
            case 0x627510:
                record = record.WriteWord(0x1C, unchecked((ushort)(Signed(record, 0x1C) + velocity))).WriteWord(0x1E, unchecked((ushort)(velocity - velocity / 6)));
                end = 26;
                break;
            case 0x627730:
                if (age < 17)
                {
                    record = record.WriteWord(0x1C, unchecked((ushort)(Signed(record, 0x1C) + velocity))).WriteWord(0x1E, unchecked((ushort)(velocity - velocity / 16)));
                }
                end = 17;
                break;
            default:
                throw new InvalidDataException("Effect66 static clock is unsupported.");
        }
        ushort next = unchecked((ushort)(record.ReadWord(12) + 1));
        frame = Write(frame, task, record.WriteWord(12, next));
        return new(frame, unchecked((short)next) >= end ? 2 : 0);
    }

    public static NativeBattleEffect66Frame ProjectMatrix627630(NativeBattleEffect66Frame frame, NativeBattleEffectArena source8, short scale, int depth, NativeBattleEffect66Inputs inputs)
    {
        frame = frame with { State = frame.State.WriteWord(PartialMatrixAddress, unchecked((ushort)scale)).WriteWord(PartialMatrixAddress + 8, unchecked((ushort)scale)).WriteWord(PartialMatrixAddress + 16, unchecked((ushort)scale)) };
        frame = LoadMatrix(frame, inputs.ReadView(frame));
        (byte[] bytes, bool[] known) = source8.ReadSnapshot(6, 2);
        short? w = known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
        NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.LoadVector(Registers(frame), 0, Vector(source8), w);
        NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Effect66 projection requires its current depth controls.");
        registers = NativeBattleGeometry.ProjectSingle(registers, inputs.ReadProjection(frame), controls.Dqa, controls.Dqb);
        uint screen = registers.Geometry.Sxy[2] ?? throw new InvalidDataException("Effect66 projection requires its current SXY result.");
        int x = unchecked((short)screen) / 8;
        int y = unchecked((short)(screen >> 16)) / 8;
        short focal = inputs.ReadCameraPose(frame).FocalLength ?? throw new NotSupportedException("Effect66 projection requires its current focal WORD.");
        frame = Publish(frame, registers);
        frame = frame with { State = frame.State.WriteDword(PartialMatrixAddress + 20, unchecked((uint)(x - 160))).WriteDword(PartialMatrixAddress + 24, unchecked((uint)(y - 108))).WriteDword(PartialMatrixAddress + 28, unchecked((uint)(focal + depth))) };
        (byte[] matrix, bool[] matrixKnown) = frame.State.ReadView(PartialMatrixAddress, 32).ReadSnapshot(0, 32);
        return LoadMatrix(frame, new(matrix.ToImmutableArray(), matrixKnown.ToImmutableArray()));
    }
}
