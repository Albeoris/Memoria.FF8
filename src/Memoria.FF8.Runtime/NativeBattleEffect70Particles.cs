using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect70Profile;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect70Particles
{
    public static NativeBattleEffect70Frame InitializeSparks(NativeBattleEffect70Frame frame) => Initialize(frame, SparkOffset, 128, 15, 1, SparkCursorGlobal);
    public static NativeBattleEffect70Frame InitializeRings(NativeBattleEffect70Frame frame) => Initialize(frame, RingOffset, 64, 0, 2, RingCursorGlobal);
    public static (NativeBattleEffect70Frame Frame, uint Address) AllocateSpark(NativeBattleEffect70Frame frame, bool capturePresentation = false) => AllocateRow(frame, SparkOffset, 128, 15, 1, SparkCursorGlobal, capturePresentation);
    public static (NativeBattleEffect70Frame Frame, uint Address) AllocateRing(NativeBattleEffect70Frame frame, bool capturePresentation = false) => AllocateRow(frame, RingOffset, 64, 0, 2, RingCursorGlobal, capturePresentation);

    private static NativeBattleEffect70Frame Initialize(NativeBattleEffect70Frame frame, int bank, int count, int marker, int width, uint cursor)
    {
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Effect70 particles require current arena bytes.");
        for (int index = 0; index < count; index++)
        {
            arena = arena.Write(WorkingOffset(frame, bank + index * 16 + marker, width), new byte[width]);
        }
        return frame with { Memory = frame.Memory with { Arena = arena }, State = frame.State.WriteDword(cursor, checked(frame.State.ReadDword(ArenaGlobal) + (uint)bank)) };
    }

    private static (NativeBattleEffect70Frame Frame, uint Address) AllocateRow(NativeBattleEffect70Frame frame, int bank, int count, int marker, int width, uint cursor, bool capturePresentation)
    {
        uint start = checked(frame.State.ReadDword(ArenaGlobal) + (uint)bank);
        uint selected = frame.State.ReadDword(cursor);
        if (selected < start || selected >= start + count * 16 || ((selected - start) & 15) != 0)
        {
            throw new InvalidDataException("Effect70 particle cursor leaves its actual row bank.");
        }
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Effect70 particles require current arena bytes.");
        bool Empty(uint address) => arena.Read(checked((int)(address - ArenaAddress) + marker), width).All(value => value == 0);
        if (!Empty(selected))
        {
            selected = 0;
            for (int index = 0; index < count; index++)
            {
                uint candidate = checked(start + (uint)(index * 16));
                if (Empty(candidate))
                {
                    selected = candidate;
                    break;
                }
            }
        }
        if (selected == 0)
        {
            return (frame, 0);
        }
        if (capturePresentation)
        {
            NativeBattlePilotVisualLifetimes.Allocated(arena, start, checked((int)((selected - start) / 16)));
        }
        uint next = selected + 16;
        if (next == start + count * 16)
        {
            next = start;
        }
        return (frame with { State = frame.State.WriteDword(cursor, next) }, selected);
    }

    public static NativeBattleEffect70Frame AdvanceSparks(NativeBattleEffect70Frame frame)
    {
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Effect70 sparks require current arena bytes.");
        for (int index = 0; index < 128; index++)
        {
            int offset = WorkingOffset(frame, SparkOffset + index * 16, 16);
            byte life = arena.Read(offset + 15, 1)[0];
            if (life == 0)
            {
                continue;
            }
            arena = arena.Write(offset + 15, [unchecked((byte)(life - 1))]);
            for (int axis = 0; axis < 3; axis++)
            {
                int velocity = unchecked((sbyte)arena.Read(offset + 12 + axis, 1)[0]);
                arena = Word(arena, offset + 4 + axis * 2, unchecked((short)(I16(arena, offset + 4 + axis * 2) + (velocity << 2))));
            }
        }
        return frame with { Memory = frame.Memory with { Arena = arena } };
    }

    public static NativeBattleEffect70Frame AdvanceRings(NativeBattleEffect70Frame frame)
    {
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Effect70 rings require current arena bytes.");
        for (int index = 0; index < 64; index++)
        {
            int offset = WorkingOffset(frame, RingOffset + index * 16, 16);
            short life = I16(arena, offset);
            if (life == 0)
            {
                continue;
            }
            arena = Word(arena, offset, unchecked((short)(life - 1)));
            arena = Word(arena, offset + 4, unchecked((short)(I16(arena, offset + 4) + I16(arena, offset + 8))));
            arena = Word(arena, offset + 6, unchecked((short)(I16(arena, offset + 6) + I16(arena, offset + 10))));
        }
        return frame with { Memory = frame.Memory with { Arena = arena } };
    }

    public static NativeBattleEffect70Frame DrawSparks(NativeBattleEffect70Frame frame, NativeBattleEffect70Resources resources, NativeBattleEffect70Inputs inputs)
    {
        NativeBattlePackedMatrix32 view = inputs.ReadView(frame);
        NativeBattleCameraProjection projection = inputs.ReadProjection(frame);
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(Staging(frame), 180);
        NativeBattleEffectArena temporary = NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 180);
        foreach (int offset in new[] { 0x48, 0x50, 0x58, 0x60 })
        {
            temporary = Word(temporary, offset, 0);
        }
        frame = frame with { Staging = allocation.Staging };
        for (int index = 0; index < 128; index++)
        {
            NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Shockwave Pulsar puffs require their current working arena.");
            int offset = WorkingOffset(frame, SparkOffset + index * 16, 16);
            sbyte life = unchecked((sbyte)arena.Read(offset + 15, 1)[0]);
            if (life == 0)
            {
                continue;
            }
            uint sourceAddress = U32(arena, offset);
            NativeBattleModuleResource source = resources.Puff(sourceAddress);
            short scale = I16(arena, offset + 10);
            NativeBattleGeometryRegisterStep registers = Registers(frame.Memory);
            registers = LoadMatrix(registers, view);
            registers = NativeBattleGeometry.LoadVector(registers, 0, Vector(arena, offset + 4), scale);
            registers = NativeBattleGeometry.Transform(registers);
            temporary = temporary.Write(0x84, new byte[20]);
            temporary = Word(Word(Word(temporary, 0x84, scale), 0x8C, scale), 0x94, scale);
            for (int axis = 0; axis < 3; axis++)
            {
                temporary = Put(temporary, 0x98 + axis * 4, unchecked((uint)registers.Colors.Mac[axis]!.Value));
            }
            temporary = Put(temporary, 0, sourceAddress);
            int phases = BinaryPrimitives.ReadInt16LittleEndian(source.Bytes.AsSpan().Slice(8, 2));
            if (phases == 0)
            {
                throw new DivideByZeroException("Shockwave Pulsar puff frame count is zero.");
            }
            int phase = phases - ((life - 1) % phases);
            int selected = BinaryPrimitives.ReadInt16LittleEndian(source.Bytes.AsSpan().Slice(checked(8 + phase * 2), 2));
            uint count = BinaryPrimitives.ReadUInt32LittleEndian(source.Bytes.AsSpan().Slice(selected, 4));
            temporary = Put(Put(temporary, 0x2C, unchecked(sourceAddress + (uint)selected)), 0x30, count);
            if (unchecked((int)count) < 0)
            {
                temporary = temporary.Write(0x25, [(byte)(temporary.Read(0x25, 1)[0] | 1)]);
                temporary = Put(temporary, 0x30, count & 0x7FFFFFFF);
            }
            temporary = Put(temporary, 0x2C, unchecked(sourceAddress + (uint)selected + 4));
            NativeBattleShockwavePulsar260PuffStep drawn = NativeBattleShockwavePulsar260ParticlesDrawing.DrawPuffQuads(frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors }, temporary, source, projection, pilotIdentity: NativeBattleEffect70Presentation.Row(frame, inputs, SparkOffset, index, sourceAddress));
            frame = frame with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(Staging(frame), allocation.Address, drawn.Temporary180, 180) };
            frame = inputs.Execute(frame, new NativeBattleEffect70DeliverPrivate(drawn.Batch, CursorGlobal));
            temporary = NativeBattleTemporaryMemory.ReadView(Staging(frame), allocation.Address, 180);
        }
        frame = frame with { Staging = NativeBattleTemporaryMemory.PublishView(Staging(frame), allocation.Address, temporary, 180) };
        return frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(Staging(frame), 180) };
    }

    public static NativeBattleEffect70Frame DrawRings(NativeBattleEffect70Frame frame, NativeBattleEffect70Resources resources, NativeBattleEffect70Inputs inputs)
    {
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(Staging(frame), 120);
        frame = frame with { Staging = allocation.Staging };
        NativeBattleEffectArena temporary = NativeBattleTemporaryMemory.ReadView(Staging(frame), allocation.Address, 120);
        temporary = Put(Put(temporary, 0x38, unchecked((uint)-256)), 0x3C, 0x10);
        int angle = unchecked((int)frame.State.ReadDword(SideGlobal)) > 0 ? 0 : 2048;
        for (int index = 0; index < 64; index++)
        {
            NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Effect70 rings require current arena bytes.");
            int offset = WorkingOffset(frame, RingOffset + index * 16, 16);
            if (I16(arena, offset) == 0)
            {
                continue;
            }
            short sine = unchecked((short)NativeBattleCameraMath.Sine(-angle));
            short cosine = unchecked((short)NativeBattleCameraMath.Cosine(-angle));
            temporary = temporary.Write(0, new byte[20]);
            temporary = Word(Word(Word(Word(Word(temporary, 0, cosine), 4, unchecked((short)-sine)), 8, I16(arena, offset + 4)), 12, unchecked((short)(sine * I16(arena, offset + 6) >> 12))), 16, unchecked((short)(cosine * I16(arena, offset + 6) >> 12)));
            NativeBattleEffectArena target = NativeBattleEffect70Controller.ReadBytes(frame, U32(arena, offset + 12), 24);
            uint model = U32(target, 0);
            if (model < 0x1D972C0 || (model - 0x1D972C0) % 156 != 0 || (model - 0x1D972C0) / 156 >= 7)
            {
                throw new InvalidDataException("Effect70 ring target requires its actual physical model row.");
            }
            frame = frame with { Staging = NativeBattleTemporaryMemory.PublishView(Staging(frame), allocation.Address, temporary, 120) };
            frame = inputs.Execute(frame, new NativeBattleEffect70QueryBonePoint((int)((model - 0x1D972C0) / 156), I16(arena, offset + 2), allocation.Address + 0x14));
            temporary = NativeBattleTemporaryMemory.ReadView(Staging(frame), allocation.Address, 120);
            temporary = Put(temporary, 0x18, unchecked(U32(temporary, 0x18) + (uint)((I16(target, 0x14) + I16(target, 0x16)) / 4)));
            NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), Matrix(temporary, 0));
            temporary = WriteMatrix(temporary, 0, NativeBattlePackedMatrix32.FromMatrix(composed, null));
            NativeBattleGeometryRegisterStep registers = Registers(frame.Memory);
            registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, composed, true, true, Padding(temporary, 18)) };
            temporary = Put(temporary, 0x20, resources.ParticleMesh.Address);
            NativeBattleEffectArena context = NativeBattleEffectNodeContextMemory.Copy(temporary, 0x20, NativeBattleEffectArena.Clear().MarkUnknown(0, 88), 0, 88);
            NativeBattleShockwavePulsar260MeshStep drawn = NativeBattleShockwavePulsar260Mesh.Draw(frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors }, new(resources.ParticleMesh.Address, resources.ParticleMesh.Bytes), 0, context, inputs.ReadProjection(frame), inputs.Colors, pilotIdentity: NativeBattleEffect70Presentation.Row(frame, inputs, RingOffset, index, resources.ParticleMesh.Address));
            temporary = NativeBattleEffectNodeContextMemory.Copy(drawn.Context88, 0, temporary, 0x20, 88);
            frame = frame with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(Staging(frame), allocation.Address, temporary, 120) };
            frame = inputs.Execute(frame, new NativeBattleEffect70DeliverPrivate(drawn.Batch, CursorGlobal));
            temporary = NativeBattleTemporaryMemory.ReadView(Staging(frame), allocation.Address, 120);
        }
        frame = frame with { Staging = NativeBattleTemporaryMemory.PublishView(Staging(frame), allocation.Address, temporary, 120) };
        return frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(Staging(frame), 120) };
    }

    private static NativeBattleFileStaging Staging(NativeBattleEffect70Frame frame) => frame.Staging ?? throw new NotSupportedException("Effect70 particles require physical temporary memory.");
    private static NativeBattleGeometryRegisterStep Registers(NativeBattleEffectMemory memory) => new(memory.GeometryRegisters ?? throw new NotSupportedException("Effect70 particles require current geometry controls."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect70 particles require current geometry data."));
    private static NativeBattleGeometryRegisterStep LoadMatrix(NativeBattleGeometryRegisterStep registers, NativeBattlePackedMatrix32 matrix) => registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, matrix.ReadMatrix(), true, true, matrix.Known[18] && matrix.Known[19] ? BinaryPrimitives.ReadInt16LittleEndian(matrix.Bytes.AsSpan().Slice(18)) : null) };
    private static int WorkingOffset(NativeBattleEffect70Frame frame, int relative, int length)
    {
        long offset = (long)frame.State.ReadDword(ArenaGlobal) - ArenaAddress + relative;
        if (offset < 0 || length < 0 || offset > NativeBattleEffectArena.Length - length)
        {
            throw new InvalidDataException("Effect70 particle row leaves its actual working arena.");
        }
        return checked((int)offset);
    }
    private static uint U32(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static ushort U16(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(offset, 2));
    private static short I16(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    private static short I16(byte[] bytes, int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset));
    private static NativeBattleVector Vector(NativeBattleEffectArena arena, int offset) => new(I16(arena, offset), I16(arena, offset + 2), I16(arena, offset + 4));
    private static NativeBattleEffectArena Put(NativeBattleEffectArena arena, int offset, uint value) => arena.Write(offset, BitConverter.GetBytes(value));
    private static NativeBattleEffectArena Word(NativeBattleEffectArena arena, int offset, short value) => arena.Write(offset, BitConverter.GetBytes(value));
    private static short? Padding(NativeBattleEffectArena arena, int offset)
    {
        (byte[] bytes, bool[] known) = arena.ReadSnapshot(offset, 2);
        return known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
    }
    private static NativeBattleActionMatrix Matrix(NativeBattleEffectArena arena, int offset)
    {
        short[] rotation = Enumerable.Range(0, 9).Select(index => I16(arena, offset + index * 2)).ToArray();
        return new(rotation.ToImmutableArray(), unchecked((int)U32(arena, offset + 20)), unchecked((int)U32(arena, offset + 24)), unchecked((int)U32(arena, offset + 28)), 4096);
    }
    private static NativeBattleEffectArena WriteMatrix(NativeBattleEffectArena arena, int offset, NativeBattlePackedMatrix32 matrix)
    {
        NativeBattleEffectArena packed = NativeBattleEffectNodeContextMemory.Copy(arena, offset, NativeBattleEffectArena.Clear().MarkUnknown(0, 32), 0, 32);
        for (int index = 0; index < 32; index++)
        {
            if (matrix.Known[index])
            {
                packed = packed.Write(index, [matrix.Bytes[index]]);
            }
        }
        return NativeBattleEffectNodeContextMemory.Copy(packed, 0, arena, offset, 32);
    }
}
