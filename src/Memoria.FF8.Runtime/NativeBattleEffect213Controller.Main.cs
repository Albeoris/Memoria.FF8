using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect213Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect213Controller
{
    public static NativeBattleEffect213Step AdvanceMain69EF60(NativeBattleEffect213Frame frame, uint address, NativeBattleEffect213Resources resources, NativeBattleEffect213Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int age = Signed(task, 12);
        uint caster = frame.State.ReadDword(CasterGlobal);
        NativeBattleEffect213PointStep anchor = inputs.Point502170(frame, caster, 29, 1024, TaskView(task, 0x34, 8));
        frame = Write(anchor.Frame, address, CopyTask(Read(anchor.Frame, address), 0x34, anchor.Output8, 8));
        if (age == 8)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect213PositionalSound5013A0(SoundAddress, 0, anchor.Output8));
        }
        int dx = 0;
        int dz = 0;
        int distance = unchecked((int)Read(frame, address).ReadDword(0x30));
        NativeBattleActionMatrix streamWorld = NativeBattleCuragaMatrices.Identity8DD770();
        if ((uint)(age - 25) < 50)
        {
            (frame, streamWorld, dx, dz, distance) = DrawStream(frame, address, resources, inputs);
        }
        NativeBattleVector mouth = Vector(TaskView(Read(frame, address), 0x34, 8));
        short yaw = BinaryPrimitives.ReadInt16LittleEndian(inputs.ReadModelArena9C(frame, caster).Read(14, 2));
        if ((uint)age < 24)
        {
            if (age == 0)
            {
                frame = NativeBattleEffect213Programme.Initialize(frame, address + 0x64, 0x117EA88, 0x4A0, resources);
            }
            NativeBattleActionMatrix world = NativeBattleGfMatrices.Euler56CD50(new(-128, unchecked((short)(yaw + 2048)), 0)) with { X = mouth.X, Y = mouth.Y, Z = mouth.Z };
            NativeBattleActionMatrix view = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), world);
            NativeBattleEffect213Step programme = NativeBattleEffect213Programme.Advance(frame, address + 0x64, 0x4A0, NativeBattlePackedMatrix32.FromMatrix(view, null), 0x118102C, frame.State.ReadDword(ArenaGlobal) + MorphOffset, resources, inputs, inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(213, Read(frame, address)) : null);
            frame = programme.Frame;
            Roll(ref frame);
            int angle = Roll(ref frame);
            frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(world, null));
            frame = BirthMouth(frame, world, angle);
        }
        if (age >= 29)
        {
            task = Read(frame, address);
            if (age == 29)
            {
                frame = Write(frame, address, CopyTask(task, 0x3C, TaskView(task, 0x34, 8), 8));
                frame = NativeBattleEffect213Programme.Initialize(frame, address + 0x64, 0x117DD7C, 0x140, resources);
            }
            task = Read(frame, address);
            NativeBattleVector saved = Vector(TaskView(task, 0x3C, 8));
            NativeBattleActionMatrix basis = Packed(TaskView(task, 0x44, 32)).ReadMatrix() with { X = saved.X, Y = saved.Y, Z = saved.Z };
            NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), basis);
            frame = NativeBattleEffect213Programme.Advance(frame, address + 0x64, 0x140, NativeBattlePackedMatrix32.FromMatrix(composed, null), 0x1181054, frame.State.ReadDword(ArenaGlobal) + MorphOffset, resources, inputs, inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(213, Read(frame, address)) : null).Frame;
        }
        if ((uint)(age - 35) < 30)
        {
            frame = BirthAtVertices(frame, address, dx, dz, distance, inputs);
        }
        if ((uint)(age - 30) < 40)
        {
            frame = BirthFromStream(frame, streamWorld, age, dx, dz, distance);
        }
        if ((uint)(age - 20) < 5)
        {
            frame = BirthRadial(frame, yaw, mouth, 256);
        }
        if ((uint)(age - 25) < 40)
        {
            frame = BirthRadial(frame, yaw, mouth, 1024);
        }
        int ringAge = age - 25;
        if ((uint)ringAge < 17 && (ringAge & 3) == 0)
        {
            (frame, uint ring) = Append(frame, RibbonDescriptor, 0x6A08D0);
            if (ring != 0)
            {
                frame = ClearDwords701200(frame, ring + 12, 2);
                frame = Write(frame, ring, Read(frame, ring).WriteDword(16, address));
            }
        }
        if (age == 1)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect213Sound501330(SoundAddress, 0, 128));
        }
        task = Read(frame, address);
        task = task.WriteWord(0x28, unchecked((ushort)(Signed(task, 0x28) - 100))).WriteWord(0x2A, unchecked((ushort)(Signed(task, 0x2A) + 100)));
        if (age >= 75)
        {
            return new(Write(frame, address, task), 2);
        }
        return new(Write(frame, address, task.WriteWord(12, unchecked((ushort)(age + 1)))), 0);
    }

    private static (NativeBattleEffect213Frame Frame, NativeBattleActionMatrix World, int Dx, int Dz, int Distance) DrawStream(NativeBattleEffect213Frame frame, uint address, NativeBattleEffect213Resources resources, NativeBattleEffect213Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int phase = Signed(task, 12) - 25;
        (frame, uint temporary) = Allocate(frame, 92);
        frame = NativeBattleEffect213Stream.Wobble(frame, Signed(task, 0x28), Signed(task, 0x2A), resources, inputs);
        (frame, NativeBattleVector target) = InterpolateTarget(frame, address, phase, inputs);
        task = Read(frame, address);
        NativeBattleVector mouth = Vector(TaskView(task, 0x34, 8));
        int dx = target.X - mouth.X;
        int dy = target.Y - mouth.Y;
        int dz = target.Z - mouth.Z;
        int square = unchecked(dx * dx + dy * dy + dz * dz);
        int distance = unchecked((int)Math.Sqrt(square));
        NativeBattleActionMatrix basis = NativeBattlePandemona291Restoration.Basis50CBA0(-dx, -dy, dz, default);
        task = CopyTask(task, 0x44, PackedArena(NativeBattlePackedMatrix32.FromMatrix(basis, null)), 18).WriteDword(0x30, unchecked((uint)distance));
        frame = Write(frame, address, task);
        short scale = unchecked((short)distance);
        int fade = 0;
        if (phase < 8)
        {
            int sine = NativeBattleCameraMath.Sine(phase << 7);
            scale = unchecked((short)((scale * sine) >> 12));
            fade = 4096 - sine;
        }
        else if (phase > 42)
        {
            fade = NativeBattleCameraMath.Sine(unchecked(((phase << 7) - 0x1481) & 0x1FFFFF80));
        }
        NativeBattleActionMatrix diagonal = new([4096, 0, 0, 0, 4096, 0, 0, 0, scale], 0, 0, 0, 4096);
        NativeBattleActionMatrix world = NativeBattleGfMatrices.Multiply56C090(basis, diagonal) with { X = mouth.X, Y = mouth.Y, Z = mouth.Z };
        NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), world);
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(composed, null));
        NativeBattleEffectArena context = Context(frame, temporary, 92).Write(0, BitConverter.GetBytes(0x117ADD8u)).Write(4, BitConverter.GetBytes(0)).Write(8, BitConverter.GetBytes(fade));
        context = Word(context, 12, (phase & 7) << 4);
        foreach ((int offset, int value) in new (int, int)[] { (16, 0), (18, 0), (20, 256), (22, 256), (24, 0), (26, 0), (28, 128), (30, 256) })
        {
            context = Word(context, offset, value);
        }
        frame = PublishContext(frame, temporary, context, 92);
        frame = NativeBattleEffect213Stream.Draw(frame, temporary, resources, inputs, inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(213, Read(frame, address), 1) with { ResourceGeneration = 0x117ADD8 } : null);
        frame = Release(frame, 92);
        return (frame, world, dx, dz, distance);
    }

    public static (NativeBattleEffect213Frame Frame, NativeBattleVector Point) InterpolateTarget(NativeBattleEffect213Frame frame, uint address, int phase, NativeBattleEffect213Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int current = Signed(task, 0x20);
        int next = Signed(task, 0x22);
        NativeBattleEffectArena retained = NativeBattleEffectArena.Clear().MarkUnknown(0, 8);
        NativeBattleEffect213PointStep a = inputs.Point502170(frame, task.ReadDword(16 + current * 4), 0xF1, 0, retained);
        NativeBattleEffect213PointStep b = inputs.Point502170(a.Frame, task.ReadDword(16 + next * 4), 0xF1, 0, retained);
        frame = b.Frame;
        NativeBattleVector from = Vector(a.Output8);
        NativeBattleVector to = Vector(b.Output8);
        int fraction = Signed(task, 0x24);
        NativeBattleVector point = new(unchecked((short)(from.X + ((to.X - from.X) * fraction >> 12))), unchecked((short)(from.Y + ((to.Y - from.Y) * fraction >> 12))), unchecked((short)(from.Z + ((to.Z - from.Z) * fraction >> 12))));
        if (phase >= 5)
        {
            fraction = unchecked((short)(fraction + Signed(task, 0x26)));
            if (fraction >= 4096)
            {
                NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect213 requires its actual contact header.");
                int first = inputs.ReadPackets(frame).Headers[parameters.HeaderIndex7C].FirstContactIndex ?? throw new NotSupportedException("Effect213 requires its actual contact pointer.");
                frame = inputs.Execute(frame, new NativeBattleEffect213Contact506690(first + task.ReadByte(0x1C + current)));
                if (current != next)
                {
                    NativeBattleEffect213CountStep count = inputs.CountVertices69EE80(frame, task.ReadDword(16 + next * 4));
                    frame = count.Frame;
                    task = Read(frame, address).WriteWord(0x2C, unchecked((ushort)count.Count));
                }
                fraction -= 4096;
                task = task.WriteWord(0x20, unchecked((ushort)next)).WriteWord(0x22, unchecked((ushort)Math.Min(next + 1, Signed(task, 14) - 1)));
            }
            frame = Write(frame, address, task.WriteWord(0x24, unchecked((ushort)fraction)));
        }
        return (frame, point);
    }

    public static int Roll(ref NativeBattleEffect213Frame frame)
    {
        NativeCrtRandomState random = frame.Random.Draw(out ushort value);
        frame = frame with { Random = random };
        return value;
    }

    public static NativeBattlePackedMatrix32 Packed(NativeBattleEffectArena matrix)
    {
        (byte[] bytes, bool[] known) = matrix.ReadSnapshot(0, 32);
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }

    public static NativeBattleEffectArena Word(NativeBattleEffectArena arena, int offset, int value) => arena.Write(offset, BitConverter.GetBytes(unchecked((short)value)));
    public static NativeBattleVector Transform(ref NativeBattleEffect213Frame frame, NativeBattleVector vector, short? w = null)
    {
        NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(Registers(frame), 0, vector, w));
        frame = Publish(frame, registers);
        return new(unchecked((short)registers.Colors.Ir[0]!.Value), unchecked((short)registers.Colors.Ir[1]!.Value), unchecked((short)registers.Colors.Ir[2]!.Value));
    }
}
