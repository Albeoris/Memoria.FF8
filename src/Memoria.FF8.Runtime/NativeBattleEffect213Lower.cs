using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect213Controller;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect213Lower
{
    public const uint CursorGlobal = 0x2510194;
    public const uint ArenaGlobal = 0x2510178;

    public static NativeBattleEffect213Step Advance(NativeBattleEffect213Frame frame, uint address, NativeBattleEffect213Resources resources, NativeBattleEffect213Inputs inputs)
    {
        uint callback = Read(frame, address).Callback;
        return callback switch
        {
            0x6A0760 => AdvanceDebris(frame, address, resources, inputs),
            0x6A08D0 => AdvanceRing(frame, address, resources, inputs),
            0x6A0A50 or 0x6A0AF0 or 0x6A0BE0 or 0x6A0C80 => AdvanceSprite(frame, address, resources, inputs),
            _ => throw new NotSupportedException("The effect requires its selected lower callback.")
        };
    }

    public static NativeBattleEffect213Frame PrepareSprite(NativeBattleEffect213Frame frame, NativeBattleEffectArena point, int scale, NativeBattleEffect213Inputs inputs)
    {
        (byte[] bytes, bool[] known) = point.ReadSnapshot(6, 2);
        short? w = known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
        return frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), Vector(point), w, unchecked((short)scale), unchecked((short)-(scale >> 2))) };
    }

    private static NativeBattleEffect213Step AdvanceSprite(NativeBattleEffect213Frame frame, uint address, NativeBattleEffect213Resources resources, NativeBattleEffect213Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        (frame, uint temporary) = Allocate(frame, 180);
        NativeBattleEffectArena point = TaskView(task, 0x10, 8);
        uint source;
        int lifetime;
        if (task.Callback == 0x6A0AF0)
        {
            int factor = (Signed(task, 12) << 12) / 12;
            NativeBattleGeometryRegisterStep registers = Registers(frame);
            NativeBattleEffectArena mouth = ReadBytes(frame, frame.State.ReadDword(ArenaGlobal) + 0x1648, 8);
            registers = Weight(registers, Vector(mouth), factor, false, inputs.Colors);
            registers = Weight(registers, Vector(point), 4096 - factor, true, inputs.Colors);
            frame = Publish(frame, registers);
            point = Store(NativeBattleEffectArena.Clear().MarkUnknown(0, 8), Ir(registers));
            source = 0x117DC38;
            lifetime = 12;
        }
        else
        {
            (source, lifetime) = task.Callback switch
            {
                0x6A0A50 => (0x117D8B8u, 13),
                0x6A0BE0 => (0x117DAF4u, 12),
                0x6A0C80 => (0x117DA18u, 8),
                _ => throw new NotSupportedException("The effect requires its selected sprite source.")
            };
        }
        frame = PrepareSprite(frame, point, Signed(task, 0x16), inputs);
        NativeBattleEffectArena context = PutWord(PutWord(Put(Context(frame, temporary, 180), 0, source), 4, Signed(task, 12)), 0x24, 0);
        frame = PublishContext(frame, temporary, context, 180);
        NativeBattleModuleSpriteDraw draw = NativeBattleModuleSpriteDrawing.Draw571C80(frame.Memory, context, resources.Sprite, inputs.ReadProjection(frame), inputs.ReadCameraRoll(frame), 17, 2);
        frame = PublishContext(frame with { Memory = draw.Memory }, temporary, draw.Temporary180, 180);
        frame = inputs.Execute(frame, new NativeBattleEffect213DrawBatch(draw.Batch, CursorGlobal));
        frame = Release(frame, 180);
        task = Read(frame, address);
        if (task.Callback != 0x6A0AF0)
        {
            for (int axis = 0; axis < 3; axis++)
            {
                task = AddWord(task, 0x10 + axis * 2, Signed(task, 0x18 + axis * 2));
            }
        }
        short age = unchecked((short)(Signed(task, 12) + 1));
        return new(Write(frame, address, task.WriteWord(12, unchecked((ushort)age))), age >= lifetime ? 2 : 0);
    }

    private static NativeBattleEffect213Step AdvanceDebris(NativeBattleEffect213Frame frame, uint address, NativeBattleEffect213Resources resources, NativeBattleEffect213Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        (frame, uint temporary) = Allocate(frame, 88);
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CD50(new(Signed(task, 0x20), Signed(task, 0x22), Signed(task, 0x24)));
        int age = Signed(task, 12);
        if (age >= 22)
        {
            int scale = 4096 + ((22 - age) << 12) / 12;
            local = NativeBattlePoseMatrices.Scale56BEF0(local, scale, scale, scale);
        }
        local = local with { X = Signed(task, 0x10), Y = Signed(task, 0x12), Z = Signed(task, 0x14) };
        NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local);
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(composed, null));
        NativeBattleEffectArena context = Put(Put(Context(frame, temporary, 88), 0, 0x117D6C8), 0x1C, 0);
        frame = DrawStatic(frame, temporary, context, 0x117D6C8, resources, inputs);
        frame = Release(frame, 88);
        task = Read(frame, address);
        task = NativeBattleStorm11Motion.MoveDebris6EB790(task, out bool ended);
        return new(Write(frame, address, task), ended ? 2 : 0);
    }

    private static NativeBattleEffect213Step AdvanceRing(NativeBattleEffect213Frame frame, uint address, NativeBattleEffect213Resources resources, NativeBattleEffect213Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleModulePoolTask target = Read(frame, task.ReadDword(0x10));
        (frame, uint temporary) = Allocate(frame, 88);
        int growth = Signed(task, 14);
        short angle = unchecked((short)(Signed(target, 0x2A) + growth));
        int radius = NativeBattleCameraMath.Sine(growth >> 1) >> 3;
        NativeBattleVector point = new(unchecked((short)((NativeBattleCameraMath.Cosine(angle) * radius) >> 12)), unchecked((short)((-NativeBattleCameraMath.Sine(angle) * radius) >> 12)), unchecked((short)((unchecked((int)target.ReadDword(0x30)) * growth) >> 12)));
        NativeBattlePackedMatrix32 basis = Packed(TaskView(target, 0x44, 32));
        NativeBattleActionMatrix world = basis.ReadMatrix() with { X = Signed(target, 0x34), Y = Signed(target, 0x36), Z = Signed(target, 0x38) };
        frame = LoadMatrix(frame, Packed(Put(Put(Put(PackedArena(basis), 20, unchecked((uint)world.X)), 24, unchecked((uint)world.Y)), 28, unchecked((uint)world.Z))));
        NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.LoadVector(Registers(frame), 0, point, null);
        registers = NativeBattleGeometry.Transform(registers);
        frame = Publish(frame, registers);
        int[] mac = registers.Colors.Mac.Select(value => unchecked((int)(value ?? throw new NotSupportedException("The ring requires its current transformed MAC values.")))).ToArray();
        NativeBattleActionMatrix scaled = NativeBattleGfMatrices.Multiply56C090(basis.ReadMatrix(), new([unchecked((short)(growth + 1024)), 0, 0, 0, unchecked((short)(growth + 1024)), 0, 0, 0, 4096], 0, 0, 0, 4096)) with { X = mac[0], Y = mac[1], Z = mac[2] };
        NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), scaled);
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(composed, null));
        NativeBattleEffectArena context = Put(Put(Context(frame, temporary, 88), 0, 0x117D360), 0x1C, 0x30);
        frame = DrawStatic(frame, temporary, context, 0x117D360, resources, inputs);
        frame = Release(frame, 88);
        task = Read(frame, address);
        short next = unchecked((short)(Signed(task, 14) + 128));
        return new(Write(frame, address, task.WriteWord(14, unchecked((ushort)next))), next >= 4096 ? 2 : 0);
    }

    public static NativeBattleEffect213Frame DrawStatic(NativeBattleEffect213Frame frame, uint temporary, NativeBattleEffectArena context, uint source, NativeBattleEffect213Resources resources, NativeBattleEffect213Inputs inputs)
    {
        frame = PublishContext(frame, temporary, context, 88);
        NativeBattleStaticMeshContext88Step draw = NativeBattleStaticMeshContext88.DrawLoaded(frame.Memory, resources.ReadMesh(source), inputs.ReadProjection(frame), context, inputs.Colors, 17, 2);
        frame = PublishContext(frame with { Memory = draw.Memory }, temporary, draw.Context, 88);
        return inputs.Execute(frame, new NativeBattleEffect213DrawBatch(draw.Batch, CursorGlobal));
    }

    public static NativeBattleGeometryRegisterStep Weight(NativeBattleGeometryRegisterStep registers, NativeBattleVector value, int factor, bool add, NativeBattleSummonColors colors)
    {
        registers = NativeBattleGeometry.LoadIrColumn(registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)factor) } }, unchecked((ushort)value.X), unchecked((ushort)value.Y), unchecked((ushort)value.Z));
        return NativeBattleRaldo19BallRendering.Weighted(registers, add, colors);
    }

    public static NativeBattleVector Ir(NativeBattleGeometryRegisterStep registers) => new(unchecked((short)registers.Colors.Ir[0]!.Value), unchecked((short)registers.Colors.Ir[1]!.Value), unchecked((short)registers.Colors.Ir[2]!.Value));
    public static ushort Word(NativeBattleEffectArena value, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(value.Read(offset, 2));
    public static short SignedWord(NativeBattleEffectArena value, int offset) => unchecked((short)Word(value, offset));
    public static uint Dword(NativeBattleEffectArena value, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(value.Read(offset, 4));
    public static NativeBattleVector Vector(NativeBattleEffectArena value) => new(SignedWord(value, 0), SignedWord(value, 2), SignedWord(value, 4));
    public static NativeBattleEffectArena Put(NativeBattleEffectArena value, int offset, uint number) => value.Write(offset, BitConverter.GetBytes(number));
    public static NativeBattleEffectArena PutWord(NativeBattleEffectArena value, int offset, int number) => value.Write(offset, BitConverter.GetBytes(unchecked((short)number)));
    public static NativeBattleEffectArena Store(NativeBattleEffectArena value, NativeBattleVector point) => PutWord(PutWord(PutWord(value, 0, point.X), 2, point.Y), 4, point.Z);
    private static NativeBattleModulePoolTask AddWord(NativeBattleModulePoolTask task, int offset, int value) => task.WriteWord(offset, unchecked((ushort)(Signed(task, offset) + value)));
    public static NativeBattlePackedMatrix32 Packed(NativeBattleEffectArena value)
    {
        (byte[] data, bool[] known) = value.ReadSnapshot(0, 32);
        return new(data.ToImmutableArray(), known.ToImmutableArray());
    }
}
