using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleShockwavePulsar260Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleShockwavePulsar260PuffStep(NativeBattleEffectMemory Memory, NativeBattleEffectArena Temporary180, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleShockwavePulsar260ParticlesDrawing
{
    public static NativeBattleShockwavePulsar260Frame DrawPuffs(NativeBattleShockwavePulsar260Frame frame, NativeBattleShockwavePulsar260Resources resources, NativeBattlePackedMatrix32 view, NativeBattleCameraProjection projection, Func<NativeBattleShockwavePulsar260Frame, NativeBattleGfGeometryBatch, NativeBattleShockwavePulsar260Frame> deliverPrivate)
    {
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(Staging(frame), 180);
        NativeBattleEffectArena temporary = NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 180);
        foreach (int offset in new[] { 0x48, 0x50, 0x58, 0x60 })
        {
            temporary = Word(temporary, offset, 0);
        }
        frame = frame with { Staging = allocation.Staging };
        for (int index = 0; index < PuffsCount; index++)
        {
            NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Shockwave Pulsar puffs require their current working arena.");
            int offset = WorkingOffset(frame, PuffsOffset + index * 16, 16);
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
            NativeBattleShockwavePulsar260PuffStep drawn = DrawPuffQuads(frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors }, temporary, source, projection);
            frame = frame with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(Staging(frame), allocation.Address, drawn.Temporary180, 180) };
            frame = deliverPrivate(frame, drawn.Batch);
            temporary = NativeBattleTemporaryMemory.ReadView(Staging(frame), allocation.Address, 180);
        }
        frame = frame with { Staging = NativeBattleTemporaryMemory.PublishView(Staging(frame), allocation.Address, temporary, 180) };
        return frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(Staging(frame), 180) };
    }

    public static NativeBattleShockwavePulsar260Frame DrawSecondaryParticles(NativeBattleShockwavePulsar260Frame frame, NativeBattleShockwavePulsar260Resources resources, NativeBattlePackedMatrix32 view, NativeBattleCameraProjection projection, NativeBattleSummonColors constants, Func<uint, NativeBattleEffectArena> readTargetRecord, Func<uint, NativeBattleVector> readModelPosition, Func<NativeBattleShockwavePulsar260Frame, NativeBattleGfGeometryBatch, NativeBattleShockwavePulsar260Frame> deliverPrivate)
    {
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(Staging(frame), 120);
        NativeBattleEffectArena temporary = NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 120);
        temporary = Put(Put(temporary, 0x38, unchecked((uint)-1024)), 0x3C, 0x30);
        frame = frame with { Staging = allocation.Staging };
        for (int index = 0; index < SecondaryParticlesCount; index++)
        {
            NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Shockwave Pulsar secondary particles require their current working arena.");
            int offset = WorkingOffset(frame, SecondaryParticlesOffset + index * 16, 16);
            if (I16(arena, offset) == 0)
            {
                continue;
            }
            NativeBattleEffectArena target = readTargetRecord(U32(arena, offset + 12));
            NativeBattleVector position = readModelPosition(U32(target, 0));
            int middle = (I16(target, 0x20) + I16(target, 0x22)) / 2;
            short xScale = I16(arena, offset + 4);
            short zScale = I16(arena, offset + 6);
            short sine = unchecked((short)NativeBattleCameraMath.Sine(-2048));
            short cosine = unchecked((short)NativeBattleCameraMath.Cosine(-2048));
            temporary = temporary.Write(0, new byte[20]);
            temporary = Word(Word(Word(Word(Word(temporary, 0, cosine), 4, unchecked((short)-sine)), 8, xScale), 12, unchecked((short)(sine * zScale >> 12))), 16, unchecked((short)(cosine * zScale >> 12)));
            temporary = Put(Put(Put(temporary, 0x14, unchecked((uint)(int)position.X)), 0x18, unchecked((uint)(position.Y + I16(arena, offset + 2) + middle))), 0x1C, unchecked((uint)(int)position.Z));
            NativeBattleActionMatrix local = Matrix(temporary, 0);
            NativeBattleActionMatrix current = NativeBattleGfMatrices.Compose56C2F0(view.ReadMatrix(), local);
            temporary = WriteMatrix(temporary, 0, NativeBattlePackedMatrix32.FromMatrix(current, null));
            NativeBattleGeometryRegisterStep registers = Registers(frame.Memory);
            registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, current, true, true, Padding(temporary, 18)) };
            temporary = Put(temporary, 0x20, resources.ParticleMesh.Address);
            NativeBattleEffectArena context = NativeBattleEffectNodeContextMemory.Copy(temporary, 0x20, NativeBattleEffectArena.Clear().MarkUnknown(0, 88), 0, 88);
            NativeBattleShockwavePulsar260MeshStep drawn = NativeBattleShockwavePulsar260Mesh.Draw(frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors }, new(resources.ParticleMesh.Address, resources.ParticleMesh.Bytes), 0, context, projection, constants);
            temporary = NativeBattleEffectNodeContextMemory.Copy(drawn.Context88, 0, temporary, 0x20, 88);
            frame = frame with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(Staging(frame), allocation.Address, temporary, 120) };
            frame = deliverPrivate(frame, drawn.Batch);
            temporary = NativeBattleTemporaryMemory.ReadView(Staging(frame), allocation.Address, 120);
        }
        frame = frame with { Staging = NativeBattleTemporaryMemory.PublishView(Staging(frame), allocation.Address, temporary, 120) };
        return frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(Staging(frame), 120) };
    }

    public static NativeBattleShockwavePulsar260PuffStep DrawPuffQuads(NativeBattleEffectMemory memory, NativeBattleEffectArena temporary, NativeBattleModuleResource source, NativeBattleCameraProjection projection)
    {
        int count = unchecked((int)U32(temporary, 0x30));
        uint address = U32(temporary, 0x2C);
        if (count <= 0)
        {
            return new(memory, temporary, new([], 0));
        }
        NativeBattleGeometryRegisterStep registers = Registers(memory);
        NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Shockwave Pulsar puffs require their current projection controls.");
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int index = 0; index < count; index++)
        {
            int offset = checked((int)(address - source.Address) + index * 20);
            byte[] quad = source.Bytes.AsSpan().Slice(offset, 20).ToArray();
            short width = (short)(quad[0] << 3);
            short height = (short)(quad[2] << 3);
            NativeBattleActionMatrix local = new([I16(quad, 16), 0, 0, 0, I16(quad, 18), 0, 0, 0, 4096], 0, 0, 0, 4096);
            for (int item = 0; item < 9; item++)
            {
                temporary = Word(temporary, 0x64 + item * 2, local.Rotation[item]);
            }
            temporary = Put(Put(Put(temporary, 0x78, unchecked((uint)(width + 16 * I16(quad, 8)))), 0x7C, unchecked((uint)(height + 16 * I16(quad, 10)))), 0x80, 0);
            NativeBattleActionMatrix main = Matrix(temporary, 0x84);
            registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, main, true, false, Padding(temporary, 0x96)) };
            for (int column = 0; column < 3; column++)
            {
                registers = NativeBattleGeometry.LoadIrColumn(registers, U16(temporary, 0x64 + column * 2), U16(temporary, 0x6A + column * 2), U16(temporary, 0x70 + column * 2));
                registers = NativeBattleGeometry.Transform(registers, false, true);
                for (int row = 0; row < 3; row++)
                {
                    temporary = Word(temporary, 0x64 + row * 6 + column * 2, unchecked((short)registers.Colors.Ir[row]!.Value));
                }
            }
            registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, main, false, true, null) };
            registers = NativeBattleGeometry.LoadVector(registers, 0, new(unchecked((short)U32(temporary, 0x78)), unchecked((short)U32(temporary, 0x7C)), 0), 0);
            registers = NativeBattleGeometry.Transform(registers);
            for (int axis = 0; axis < 3; axis++)
            {
                temporary = Put(temporary, 0x78 + axis * 4, unchecked((uint)registers.Colors.Mac[axis]!.Value));
            }
            NativeBattleActionMatrix matrix = Matrix(temporary, 0x64);
            registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, matrix, true, true, Padding(temporary, 0x76)) };
            NativeBattleVector[] corners = [new(unchecked((short)-width), unchecked((short)-height), 0), new(width, unchecked((short)-height), 0), new(unchecked((short)-width), height, 0), new(width, height, 0)];
            for (int corner = 0; corner < 4; corner++)
            {
                temporary = Word(Word(temporary, 0x44 + corner * 8, corners[corner].X), 0x46 + corner * 8, corners[corner].Y);
            }
            for (int corner = 0; corner < 3; corner++)
            {
                registers = NativeBattleGeometry.LoadVector(registers, corner, corners[corner], Padding(temporary, 0x4A + corner * 8));
            }
            registers = NativeBattleGeometry.ProjectTriple(registers, projection, controls.Dqa, controls.Dqb);
            uint flags = registers.Colors.Flags!.Value;
            temporary = Put(temporary, 0x40, flags);
            uint[] screens = [registers.Geometry.Sxy[0]!.Value, registers.Geometry.Sxy[1]!.Value, registers.Geometry.Sxy[2]!.Value, 0];
            int[] depths = [unchecked((ushort)registers.Geometry.Sz[1]!.Value), unchecked((ushort)registers.Geometry.Sz[2]!.Value), unchecked((ushort)registers.Geometry.Sz[3]!.Value), 0];
            byte[] packet = new byte[40];
            NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[40];
            Store32(0, 0x09000000);
            Store32(4, (uint)quad[3] << 24 | (uint)quad[1] * 0x010101);
            Store32(12, BinaryPrimitives.ReadUInt32LittleEndian(quad.AsSpan(4)));
            Store8(28, quad[4]);
            byte right = unchecked((byte)Math.Min(quad[4] + quad[0], 255));
            Store8(20, right);
            Store8(21, quad[5]);
            Store8(36, right);
            if ((flags & 0x60000) != 0)
            {
                attempts.Add(Attempt(null));
                continue;
            }
            Store16(22, (ushort)(I16(quad, 14) & 0x1FF));
            for (int corner = 0; corner < 3; corner++)
            {
                Store32(8 + corner * 8, screens[corner]);
            }
            registers = NativeBattleGeometry.LoadVector(registers, 0, corners[3], Padding(temporary, 0x62));
            registers = NativeBattleGeometry.ProjectSingle(registers, projection, controls.Dqa, controls.Dqb);
            screens[3] = registers.Geometry.Sxy[2]!.Value;
            depths[3] = unchecked((ushort)registers.Geometry.Sz[3]!.Value);
            byte bottom = unchecked((byte)Math.Min(quad[5] + quad[2], 255));
            Store8(29, bottom);
            Store8(37, bottom);
            Store32(32, screens[3]);
            uint opcode = quad[3] & 0xFCu;
            ImmutableArray<int> payload = opcode is < 0x20 or > 0x3C ? [0, 0, 0, 0] : (opcode & 8) != 0 ? depths.ToImmutableArray() : [depths[1], depths[2], depths[3], 0];
            NativeBattleGeometryPrimitive primitive = new(19, Enumerable.Range(0, 10).Select(word => BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(word * 4))).ToImmutableArray(), payload);
            if ((temporary.Read(0x25, 1)[0] & 1) != 0)
            {
                attempts.Add(Attempt(null));
                byte[] mode = new byte[8];
                BinaryPrimitives.WriteUInt32LittleEndian(mode, 0x01000000);
                BinaryPrimitives.WriteUInt32LittleEndian(mode.AsSpan(4), 0xE1000220);
                attempts.Add(new(allocated + 40, mode.ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 8).ToImmutableArray(), new(19, [0x01000000, 0xE1000220], [0, 0, 0, 0])));
                attempts.Add(new(allocated, packet.ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Untouched, 40).ToImmutableArray(), primitive));
                allocated = checked(allocated + 48);
            }
            else
            {
                attempts.Add(Attempt(primitive));
                allocated = checked(allocated + 40);
            }
            NativeBattleGfGeometryAttempt Attempt(NativeBattleGeometryPrimitive? linked) => new(allocated, packet.ToImmutableArray(), mask.ToImmutableArray(), linked);
            void Store8(int at, byte value)
            {
                packet[at] = value;
                mask[at] = NativeBattleGfPacketByteState.Known;
            }
            void Store16(int at, ushort value)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(at), value);
                Array.Fill(mask, NativeBattleGfPacketByteState.Known, at, 2);
            }
            void Store32(int at, uint value)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(at), value);
                Array.Fill(mask, NativeBattleGfPacketByteState.Known, at, 4);
            }
        }
        return new(memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors }, temporary, new(attempts.ToImmutable(), allocated));
    }

    private static NativeBattleFileStaging Staging(NativeBattleShockwavePulsar260Frame frame) => frame.Staging ?? throw new NotSupportedException("Shockwave Pulsar particles require physical temporary memory.");
    private static NativeBattleGeometryRegisterStep Registers(NativeBattleEffectMemory memory) => new(memory.GeometryRegisters ?? throw new NotSupportedException("Shockwave Pulsar particles require current geometry controls."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Shockwave Pulsar particles require current geometry data."));
    private static NativeBattleGeometryRegisterStep LoadMatrix(NativeBattleGeometryRegisterStep registers, NativeBattlePackedMatrix32 matrix) => registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, matrix.ReadMatrix(), true, true, matrix.Known[18] && matrix.Known[19] ? BinaryPrimitives.ReadInt16LittleEndian(matrix.Bytes.AsSpan().Slice(18)) : null) };
    private static int WorkingOffset(NativeBattleShockwavePulsar260Frame frame, int relative, int length)
    {
        long offset = (long)frame.State.ReadDword(WorkingGlobal) - ArenaAddress + relative;
        if (offset < 0 || length < 0 || offset > NativeBattleEffectArena.Length - length)
        {
            throw new InvalidDataException("Shockwave Pulsar particle row leaves its actual working arena.");
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
        NativeBattleEffectArena packed = NativeBattleEffectArena.Clear().MarkUnknown(0, 32);
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
