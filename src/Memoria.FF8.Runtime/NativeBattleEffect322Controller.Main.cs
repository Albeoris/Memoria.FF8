using static Memoria.FF8.Runtime.NativeBattleEffect322Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect322Controller
{
    public static NativeBattleEffect322Step AdvanceMain65AE00(NativeBattleEffect322Frame frame, uint address, NativeBattleEffect322Resources resources, NativeBattleEffect322Inputs inputs)
    {
        int age = Signed(Read(frame, address), 12);
        uint caster = frame.State.ReadDword(CasterGlobal);
        if (age == 13)
        {
            NativeBattleEffect322PointStep point = inputs.Point502170(frame, caster, 24, 2048, TaskView(Read(frame, address), 0x64, 8));
            frame = Write(point.Frame, address, CopyTask(CopyTask(Read(point.Frame, address), 0x64, point.Output8, 8), 0x6C, point.Output8, 8));
        }
        if (age == 14)
        {
            frame = InitializeControls(frame, address, inputs);
        }
        if (age is >= 14 and <= 56)
        {
            (int segment, int parameter) = PathParameter(age);
            NativeBattleEffect322PointStep sampled = inputs.SampleTrajectory(frame, TaskView(Read(frame, address), 0x1C, 64), segment, parameter, UnknownPoint());
            frame = sampled.Frame;
            NativeBattleEffectArena previous = TaskView(Read(frame, address), 0x64, 8);
            NativeBattleVector old = Vector(previous);
            NativeBattleVector current = Vector(sampled.Output8);
            if (old.X != current.X || old.Z != current.Z)
            {
                frame = Write(frame, address, CopyTask(Read(frame, address), 0x6C, previous, 8));
            }
            frame = Write(frame, address, CopyTask(Read(frame, address), 0x64, sampled.Output8, 8).WriteWord(14, unchecked((ushort)(Read(frame, address).ReadWord(14) + 1024))));
            frame = inputs.Execute(frame, new NativeBattleEffect322MeshMask(caster, 0xFFFFFFFD, 0));
            NativeBattleVector history = Vector(TaskView(Read(frame, address), 0x6C, 8));
            NativeBattleActionMatrix basis = NativeBattleFunguarLaser277.Basis50CBA0(history.X - current.X, 0, current.Z - history.Z).Matrix;
            NativeBattleActionMatrix rotation = NativeBattlePandemona291Restoration.Rotation701220(Signed(Read(frame, address), 14));
            NativeBattleActionMatrix world = NativeBattleGfMatrices.Multiply56C090(basis, rotation) with { X = current.X, Y = current.Y, Z = current.Z };
            frame = DrawMainMesh(frame, world, resources, inputs);
            frame = BirthMesh(frame, age, world);
            if (current.Y > -512)
            {
                frame = BirthPuff(frame, current.X, current.Z, current.Y);
                frame = BirthPuff(frame, (history.X + current.X) >> 1, (history.Z + current.Z) >> 1, current.Y);
            }
        }
        if (age == 15)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect322Sound501330(0xFE7000, 0, 128));
        }
        if (age == 36)
        {
            frame = BirthProgramme(frame, address, resources, inputs);
            frame = BirthBurst(frame, address);
            frame = inputs.Execute(frame, new NativeBattleEffect322Contact506690(FirstContact(frame, inputs)));
        }
        if (age == 30)
        {
            NativeBattleEffect322PointStep point = inputs.Point502170(frame, caster, 24, 2048, UnknownPoint());
            frame = Write(point.Frame, address, CopyTask(CopyTask(Read(point.Frame, address), 0x44, point.Output8, 8), 0x4C, point.Output8, 8));
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1))));
        if (age + 1 >= 57)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect322MeshMask(caster, uint.MaxValue, 2));
            return new(frame, 2);
        }
        return new(frame, 0);
    }

    public static (int Segment, int Parameter) PathParameter(int age)
    {
        if (age is < 14 or > 56)
        {
            throw new ArgumentOutOfRangeException(nameof(age));
        }
        if (age < 34)
        {
            int value = ((age - 14) << 12) / 20 + 102;
            return (0, unchecked(value * value) >> 12);
        }
        if (age < 38)
        {
            int value = (age - 34) * 2048 + 512;
            return ((value >> 12) + 1, value & 4095);
        }
        return (3, ((age - 38) << 12) / 20 + 102);
    }

    private static NativeBattleEffect322Frame InitializeControls(NativeBattleEffect322Frame frame, uint address, NativeBattleEffect322Inputs inputs)
    {
        NativeBattleEffect322PointStep point = inputs.Point502170(frame, frame.State.ReadDword(CasterGlobal), 24, 2048, UnknownPoint());
        frame = point.Frame;
        NativeBattleEffectArena target = TaskView(Read(frame, address), 0x14, 8);
        NativeBattleVector from = Vector(point.Output8);
        NativeBattleVector to = Vector(target);
        NativeBattleActionMatrix basis = NativeBattleFunguarLaser277.Basis50CBA0(from.X - to.X, 0, to.Z - from.Z).Matrix with { X = to.X, Y = to.Y, Z = to.Z };
        frame = Write(frame, address, CopyTask(CopyTask(Read(frame, address), 0x1C, point.Output8, 8), 0x24, point.Output8, 8));
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(basis, null));
        (frame, NativeBattleEffectArena first) = TransformControl(frame, -1000, 300);
        frame = Write(frame, address, CopyTask(Read(frame, address), 0x2C, first, 6));
        frame = Write(frame, address, CopyTask(Read(frame, address), 0x34, target, 8));
        (frame, NativeBattleEffectArena second) = TransformControl(frame, 1000, -600);
        frame = Write(frame, address, CopyTask(Read(frame, address), 0x3C, second, 6));
        return Write(frame, address, CopyTask(CopyTask(Read(frame, address), 0x44, point.Output8, 8), 0x4C, point.Output8, 8));
    }

    private static (NativeBattleEffect322Frame Frame, NativeBattleEffectArena Point) TransformControl(NativeBattleEffect322Frame frame, short z, int yOffset)
    {
        NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(Registers(frame), 0, new(0, 0, z), null));
        frame = Publish(frame, registers);
        NativeBattleEffectArena point = UnknownPoint();
        for (int axis = 0; axis < 3; axis++)
        {
            int value = registers.Colors.Ir[axis] ?? throw new NotSupportedException("Effect322 requires its transformed control point IR lanes.");
            point = point.Write(axis * 2, BitConverter.GetBytes(unchecked((short)(value + (axis == 1 ? yOffset : 0)))));
        }
        return (frame, point);
    }

    private static NativeBattleEffect322Frame DrawMainMesh(NativeBattleEffect322Frame frame, NativeBattleActionMatrix world, NativeBattleEffect322Resources resources, NativeBattleEffect322Inputs inputs)
    {
        (frame, uint temporary) = Allocate(frame, 88);
        NativeBattleActionMatrix viewWorld = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), world);
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(viewWorld, null));
        NativeBattleEffectArena context = Context(frame, temporary, 88).Write(0, BitConverter.GetBytes(0xFE44C8u)).Write(0x1C, BitConverter.GetBytes(0u));
        frame = PublishContext(frame, temporary, context, 88);
        frame = NativeBattleEffect322Lower.DrawMesh572200(frame, temporary, resources, inputs);
        return Release(frame, 88);
    }

    public static int MeshStrength(int age)
    {
        int relative = age - 14;
        return relative < 16 ? relative << 8 : relative <= 36 ? 4096 : (44 - relative) << 9;
    }

    private static NativeBattleEffect322Frame BirthMesh(NativeBattleEffect322Frame frame, int age, NativeBattleActionMatrix world)
    {
        (frame, uint task) = Append(frame, MeshDescriptor, 0x65BB20);
        if (task == 0)
        {
            return frame;
        }
        NativeBattleEffectArena packed = PackedArena(NativeBattlePackedMatrix32.FromMatrix(world, null));
        frame = WriteBytes(frame, task + 0x10, packed, 18);
        frame = WriteBytes(frame, task + 0x24, CopyBytes(packed, 20, NativeBattleEffectArena.Clear(), 0, 12), 12);
        return Write(frame, task, Read(frame, task).WriteWord(12, 0).WriteWord(14, unchecked((ushort)MeshStrength(age))));
    }

    private static NativeBattleEffect322Frame BirthPuff(NativeBattleEffect322Frame frame, int x, int z, int y)
    {
        (frame, uint task) = Append(frame, PuffDescriptor, 0x65B600);
        if (task != 0)
        {
            NativeBattleModulePoolTask value = Read(frame, task).WriteWord(12, unchecked((ushort)x)).WriteWord(14, 0).WriteWord(16, unchecked((ushort)z)).WriteWord(18, 0).WriteWord(20, unchecked((ushort)(6144 + y * 8)));
            frame = Write(frame, task, value);
        }
        return frame;
    }

    private static NativeBattleEffect322Frame BirthProgramme(NativeBattleEffect322Frame frame, uint main, NativeBattleEffect322Resources resources, NativeBattleEffect322Inputs inputs)
    {
        (frame, uint task) = Append(frame, ProgrammeDescriptor, 0x65B7E0);
        if (task == 0)
        {
            return frame;
        }
        uint target = Read(frame, main).ReadDword(0x10);
        frame = Write(frame, task, Read(frame, task).WriteDword(12, target));
        NativeBattleEffect322PointStep point = inputs.Anchor571400(frame, target, TaskView(Read(frame, task), 0x10, 8));
        frame = Write(point.Frame, task, CopyTask(Read(point.Frame, task), 0x10, point.Output8, 8));
        return NativeBattleEffect322Programme.Initialize(frame, task + WorkspaceOffset, ProgrammeAddress, WorkspaceLength, resources);
    }

    public static NativeBattleEffect322Frame BirthBurst(NativeBattleEffect322Frame frame, uint main)
    {
        NativeBattleEffectArena point = NativeBattleEffectNodeContextMemory.Copy(TaskView(Read(frame, main), 0x64, 8), 0, NativeBattleEffectArena.CreateUnknown(8), 0, 8).Write(6, BitConverter.GetBytes((short)0));
        NativeBattleVector current = Vector(point);
        NativeBattleVector previous = Vector(TaskView(Read(frame, main), 0x6C, 8));
        NativeBattleActionMatrix basis = NativeBattleFunguarLaser277.Basis50CBA0(previous.X - current.X, 0, current.Z - previous.Z).Matrix;
        int yaw = 0;
        for (int index = 0; index < 32; index++)
        {
            (frame, uint task) = Append(frame, BurstDescriptor, 0x65B680);
            if (task == 0)
            {
                continue;
            }
            frame = frame with { Random = frame.Random.Draw(out ushort radiusRandom) };
            int radius = (radiusRandom & 63) + 64;
            frame = frame with { Random = frame.Random.Draw(out ushort depthRandom) };
            NativeBattleVector local = new(unchecked((short)(NativeBattleCameraMath.Sine(yaw) * radius / 4096)), unchecked((short)(NativeBattleCameraMath.Cosine(yaw) * radius / 4096)), (short)((depthRandom & 63) + 64));
            NativeBattleVector velocity = NativeBattleGfMatrices.RotateShort56C4F0(basis, local);
            NativeBattleModulePoolTask value = CopyTask(Read(frame, task), 12, point, 8).WriteWord(18, 0);
            value = CopyTask(value, 0x14, PutPoint(TaskView(value, 0x14, 8), 0, velocity), 6);
            value = CopyTask(value, 0x24, TaskView(value, 0x14, 8), 8);
            value = value.WriteWord(0x1C, unchecked((ushort)(-velocity.X / 12))).WriteWord(0x1E, unchecked((ushort)(-velocity.Y / 12))).WriteWord(0x20, unchecked((ushort)(-velocity.Z / 12)));
            frame = Write(frame, task, value);
            yaw += 128;
        }
        return frame;
    }

    private static NativeBattleEffectArena UnknownPoint() => NativeBattleEffectArena.Clear().MarkUnknown(0, 8);
}
