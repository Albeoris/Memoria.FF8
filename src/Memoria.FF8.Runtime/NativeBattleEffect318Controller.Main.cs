using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect318Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect318Controller
{
    public static NativeBattleEffect318Step AdvanceMain65E790(NativeBattleEffect318Frame frame, uint address, NativeBattleEffect318Resources resources, NativeBattleEffect318Inputs inputs)
    {
        int age = Signed(Read(frame, address), 12);
        if (age == 2)
        {
            frame = InitializeMatrices(frame, address, resources, inputs);
        }
        NativeBattleVector caster = Vector(TaskView(Read(frame, address), 0x5C, 8));
        NativeBattleVector target = Vector(TaskView(Read(frame, address), 0x64, 8));
        if (age is >= 2 and < 24)
        {
            NativeBattleVector offset = Vector(TaskView(Read(frame, address), 0x6C, 8));
            frame = StoreTranslation(frame, address + 0xA4, caster.X - offset.X, caster.Y - offset.Y, caster.Z - offset.Z);
            frame = AdvanceProgramme(frame, address + FirstWorkspaceOffset, FirstWorkspaceLength, address + 0xA4, 0, resources, inputs);
        }
        int time = age - 4;
        if (time is >= 0 and < 16)
        {
            if (time == 0)
            {
                frame = inputs.Execute(frame, new NativeBattleEffect318MeshMask(frame.State.ReadDword(CasterGlobal), 0xFFFFFFF7, 0));
            }
            (frame, NativeBattleVector point) = BlendPath(frame, target, caster, time * 256, inputs.Colors);
            NativeBattleVector offset = Vector(TaskView(Read(frame, address), 0x6C, 8));
            frame = StoreTranslation(frame, address + 0x84, point.X + offset.X, point.Y + offset.Y, point.Z + offset.Z);
            frame = AdvanceProgramme(frame, address + SecondWorkspaceOffset, SecondWorkspaceLength, address + 0x84, -128, resources, inputs);
            frame = StoreTranslation(frame, address + 0x84, point.X, point.Y, point.Z);
            frame = LoadMatrix(frame, Compose(frame, address + 0x84, inputs));
            (frame, uint temporary) = Allocate(frame, 88);
            NativeBattleEffectArena context = Context(frame, temporary, 88).Write(0, BitConverter.GetBytes(0xFF7CB0u)).Write(0x1C, BitConverter.GetBytes(0u));
            frame = PublishContext(frame, temporary, context, 88);
            frame = NativeBattleEffect318Lower.DrawMesh572200(frame, temporary, resources, inputs);
            frame = Release(frame, 88);
        }
        if (time is >= 0 and <= 16)
        {
            frame = BirthRibbonRecords(frame, address, time, resources, inputs);
        }
        if (age is >= 19 and < 51)
        {
            frame = StoreTranslation(frame, address + 0xC4, target.X, target.Y, target.Z);
            frame = AdvanceProgramme(frame, address + ThirdWorkspaceOffset, ThirdWorkspaceLength, address + 0xC4, -128, resources, inputs);
            if (age < 23)
            {
                frame = BirthMeshes(frame, address, inputs);
            }
        }
        frame = NativeBattleEffect318Records.Advance65F340(frame, address, resources, inputs);
        return FinishMain(frame, address, age, inputs);
    }

    public static NativeBattleEffect318Frame InitializeMatrices(NativeBattleEffect318Frame frame, uint address, NativeBattleEffect318Resources resources, NativeBattleEffect318Inputs inputs)
    {
        NativeBattleEffect318PointStep bone = inputs.Point502170(frame, frame.State.ReadDword(CasterGlobal), 29, 2048, TaskView(Read(frame, address), 0x5C, 8));
        frame = Write(bone.Frame, address, CopyTask(Read(bone.Frame, address), 0x5C, bone.Output8, 8));
        NativeBattleVector caster = Vector(bone.Output8);
        NativeBattleVector target = Vector(TaskView(Read(frame, address), 0x64, 8));
        int x = target.X - caster.X;
        int y = caster.Y - target.Y;
        int z = caster.Z - target.Z;
        NativeBattleActionMatrix basis = NativeBattleFunguarLaser277.Basis50CBA0(x, y, z).Matrix;
        frame = StoreRotation(frame, address + 0x84, basis);
        for (int index = 0; index < 3; index++)
        {
            NativeBattleEffectArena local = NativeBattleEffectArena.Clear().Write(0, resources.ReadStatic(0xFFE688u + (uint)(index * 8), 8));
            NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(basis, Vector(local));
            uint destination = address + 0x6Cu + (uint)(index * 8);
            frame = WriteBytes(frame, destination, PutPoint(ReadBytes(frame, destination, 8), 0, rotated), 6);
        }
        frame = StoreRotation(frame, address + 0xA4, NativeBattleFunguarLaser277.Basis50CBA0(x, y, z).Matrix);
        return StoreRotation(frame, address + 0xC4, NativeBattleFunguarLaser277.Basis50CBA0(-x, y, -z).Matrix);
    }

    public static int Quartic(int fraction)
    {
        int square = unchecked(fraction * fraction) >> 12;
        return unchecked(square * square) >> 12;
    }

    private static (NativeBattleEffect318Frame Frame, NativeBattleVector Point) BlendPath(NativeBattleEffect318Frame frame, NativeBattleVector target, NativeBattleVector caster, int fraction, NativeBattleSummonColors colors)
    {
        int factor = Quartic(fraction);
        NativeBattleWeightedPointsStep result = NativeBattleWeightedPoints56CB90.Blend(frame.Memory, target, caster, unchecked((short)factor), unchecked((short)(4096 - factor)), colors);
        return (frame with { Memory = result.Memory }, result.Position);
    }

    public static NativeBattleEffect318Frame BirthRibbonRecords(NativeBattleEffect318Frame frame, uint main, int time, NativeBattleEffect318Resources resources, NativeBattleEffect318Inputs inputs)
    {
        frame = LoadMatrix(frame, Packed(frame, main + 0x84), true, false);
        NativeBattleVector target = Vector(TaskView(Read(frame, main), 0x64, 8));
        NativeBattleVector caster = Vector(TaskView(Read(frame, main), 0x5C, 8));
        int fraction = time * 256;
        for (int inner = 0; inner < 4; inner++)
        {
            (frame, NativeBattleVector point) = BlendPath(frame, target, caster, fraction, inputs.Colors);
            fraction -= 64;
            NativeBattleVector first = Vector(TaskView(Read(frame, main), 0x74, 8));
            frame = BirthRibbonPair(frame, main, AddWrapped(point, first), resources, inputs);
            if (time >= 4)
            {
                NativeBattleVector second = Vector(TaskView(Read(frame, main), 0x7C, 8));
                frame = BirthRibbonPair(frame, main, AddWrapped(point, second), resources, inputs);
            }
        }
        return frame;
    }

    private static NativeBattleEffect318Frame BirthRibbonPair(NativeBattleEffect318Frame frame, uint main, NativeBattleVector point, NativeBattleEffect318Resources resources, NativeBattleEffect318Inputs inputs)
    {
        frame = frame with { Random = frame.Random.Draw(out ushort angle) };
        NativeBattleEffectArena direction = NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes((short)1024)).Write(4, BitConverter.GetBytes(angle));
        NativeBattleEffectArena packedPoint = PointView(point);
        frame = NativeBattleEffect318Records.Birth65F710(frame, main, packedPoint, direction, resources, inputs);
        direction = direction.Write(4, BitConverter.GetBytes(unchecked((ushort)(angle + 2048))));
        return NativeBattleEffect318Records.Birth65F710(frame, main, packedPoint, direction, resources, inputs);
    }

    public static NativeBattleEffect318Frame BirthMeshes(NativeBattleEffect318Frame frame, uint main, NativeBattleEffect318Inputs inputs)
    {
        frame = frame with { Random = frame.Random.Draw(out ushort yaw) };
        NativeBattleEffectArena direction = NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(2, BitConverter.GetBytes(yaw));
        for (int index = 0; index < 8; index++)
        {
            (frame, uint mesh) = Append(frame, MeshDescriptor, 0x65F100);
            if (mesh != 0)
            {
                frame = Write(frame, mesh, CopyTask(Read(frame, mesh), 0x10, TaskView(Read(frame, main), 0x64, 8), 8).WriteWord(12, 0));
                frame = frame with { Random = frame.Random.Draw(out ushort pitch) };
                direction = direction.Write(0, BitConverter.GetBytes(unchecked((short)((pitch * 1500 >> 15) - 750))));
                NativeBattleEffectArena local = NativeBattleEffect318Lower.Direction65F0A0(direction, NativeBattleEffectArena.Clear().MarkUnknown(0, 8));
                frame = frame with { Random = frame.Random.Draw(out ushort magnitude) };
                NativeBattleVector vector = Vector(local);
                NativeBattleGeometryRegisterStep registers = Registers(frame);
                registers = NativeBattleGeometry.LoadIrColumn(registers with { Colors = registers.Colors with { Ir0 = (uint)((magnitude & 255) + 128) } }, unchecked((ushort)vector.X), unchecked((ushort)vector.Y), unchecked((ushort)vector.Z));
                registers = NativeBattleRaldo19BallRendering.Weighted(registers, false, inputs.Colors);
                frame = Publish(frame, registers);
                NativeBattleVector velocity = new(unchecked((short)(registers.Colors.Ir[0] ?? throw new NotSupportedException("Effect318 requires its scaled velocity IR lanes."))), unchecked((short)(registers.Colors.Ir[1] ?? throw new NotSupportedException("Effect318 requires its scaled velocity IR lanes."))), unchecked((short)(registers.Colors.Ir[2] ?? throw new NotSupportedException("Effect318 requires its scaled velocity IR lanes."))));
                NativeBattleModulePoolTask task = CopyTask(Read(frame, mesh), 0x18, PutPoint(NativeBattleEffectNodeContextMemory.Copy(TaskView(Read(frame, mesh), 0x18, 8), 0, NativeBattleEffectArena.CreateUnknown(8), 0, 8), 0, velocity), 6);
                task = task.WriteWord(0x20, unchecked((ushort)(-velocity.X / 12))).WriteWord(0x22, unchecked((ushort)(-velocity.Y / 12))).WriteWord(0x24, unchecked((ushort)(-velocity.Z / 12)));
                frame = Write(frame, mesh, CopyTask(task, 0x28, TaskView(task, 0x18, 8), 8));
            }
            yaw = unchecked((ushort)(yaw + 512));
            direction = direction.Write(2, BitConverter.GetBytes(yaw));
        }
        return frame;
    }

    public static NativeBattleEffect318Step FinishMain(NativeBattleEffect318Frame frame, uint address, int age, NativeBattleEffect318Inputs inputs)
    {
        if (age == 8)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect318Sound501330(SoundAddress, 0, 128));
        }
        if (age == 41)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect318Queue5341D0(360, frame.State.ReadDword(ArenaGlobal) + ThirdPacketOffset, 0));
        }
        frame = inputs.Execute(frame, new NativeBattleEffect318Advance534210());
        if (age == 24)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect318Contact506690(FirstContact(frame, inputs)));
        }
        if (age == 39)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect318MeshMask(frame.State.ReadDword(CasterGlobal), uint.MaxValue, 8));
            foreach ((int bone, int delay) in new[] { (22, 0), (29, 8), (33, 16), (34, 16), (35, 16) })
            {
                frame = NativeBattleEffect318Lower.Birth65F250(frame, bone, delay, 1024, 512, inputs);
            }
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1))));
        if (age + 1 >= 51)
        {
            NativeBattleEffect318PollStep poll = inputs.IoSelect5342C0(frame, 0);
            frame = poll.Frame;
            if (poll.Complete)
            {
                return new(frame, 2);
            }
            frame = Write(frame, address, Read(frame, address).WriteWord(12, 51));
        }
        return new(frame, 0);
    }

    private static NativeBattleVector AddWrapped(NativeBattleVector first, NativeBattleVector second) => new(unchecked((short)(first.X + second.X)), unchecked((short)(first.Y + second.Y)), unchecked((short)(first.Z + second.Z)));
    private static NativeBattleEffect318Frame StoreRotation(NativeBattleEffect318Frame frame, uint address, NativeBattleActionMatrix matrix) => WriteBytes(frame, address, PackedArena(NativeBattlePackedMatrix32.FromMatrix(matrix, null)), 18);
    private static NativeBattleEffect318Frame StoreTranslation(NativeBattleEffect318Frame frame, uint address, int x, int y, int z) => WriteBytes(frame, address + 20, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(x)).Write(4, BitConverter.GetBytes(y)).Write(8, BitConverter.GetBytes(z)), 12);

    private static NativeBattlePackedMatrix32 Packed(NativeBattleEffect318Frame frame, uint address)
    {
        (byte[] bytes, bool[] known) = ReadBytes(frame, address, 32).ReadSnapshot(0, 32);
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }

    private static NativeBattlePackedMatrix32 Compose(NativeBattleEffect318Frame frame, uint world, NativeBattleEffect318Inputs inputs) => NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), Packed(frame, world).ReadMatrix()), null);

    private static NativeBattleEffect318Frame AdvanceProgramme(NativeBattleEffect318Frame frame, uint workspace, int length, uint world, int depth, NativeBattleEffect318Resources resources, NativeBattleEffect318Inputs inputs)
    {
        NativeBattleEffectArena caller = PackedArena(Compose(frame, world, inputs)).Write(32, BitConverter.GetBytes(depth)).Write(36, BitConverter.GetBytes(frame.State.ReadDword(ArenaGlobal) + MorphOffset));
        return NativeBattleEffect318Programme.Advance(frame, workspace, length, caller, resources, inputs).Frame;
    }
}
