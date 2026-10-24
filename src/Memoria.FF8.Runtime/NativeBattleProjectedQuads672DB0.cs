using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleProjectedQuads672DB0Step(NativeBattleEffectMemory Memory, NativeBattleEffectArena Context180, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleProjectedQuads672DB0
{
    public static NativeBattleProjectedQuads672DB0Step DrawLoaded(NativeBattleEffectMemory memory, NativeBattleEffectArena context, NativeBattleModuleResource source, int firstQuad, int count, NativeBattleCameraProjection projection)
    {
        if (count <= 0)
        {
            return new(memory, context, new([], 0));
        }
        if (firstQuad < 0 || firstQuad + 20L * count > source.Bytes.Length)
        {
            throw new InvalidDataException("Effect298 smoke quads leave their selected source frame.");
        }
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Quad projection requires its current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Quad projection requires its current color registers."));
        NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Effect298 smoke requires its actual projection controls.");
        var attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int index = 0; index < count; index++)
        {
            int at = firstQuad + index * 20;
            ReadOnlySpan<byte> quad = source.Bytes.AsSpan().Slice(at, 20);
            int width = quad[0];
            int height = quad[2];
            foreach (int offset in new[] { 0x72, 0x70, 0x6E, 0x6A, 0x68, 0x66 })
            {
                context = context.Write(offset, new byte[2]);
            }
            context = context.Write(0x74, BitConverter.GetBytes((short)4096)).Write(0x64, quad.Slice(16, 2)).Write(0x6C, quad.Slice(18, 2));
            foreach (int offset in new[] { 0x54, 0x44 })
            {
                context = context.Write(offset, BitConverter.GetBytes(unchecked((short)(-width * 8))));
            }
            foreach (int offset in new[] { 0x5C, 0x4C })
            {
                context = context.Write(offset, BitConverter.GetBytes(unchecked((short)(width * 8))));
            }
            foreach (int offset in new[] { 0x4E, 0x46 })
            {
                context = context.Write(offset, BitConverter.GetBytes(unchecked((short)(-height * 8))));
            }
            foreach (int offset in new[] { 0x5E, 0x56 })
            {
                context = context.Write(offset, BitConverter.GetBytes(unchecked((short)(height * 8))));
            }
            context = context.Write(0x78, BitConverter.GetBytes(unchecked((BinaryPrimitives.ReadInt16LittleEndian(quad.Slice(8)) << 4) + width * 8))).Write(0x7C, BitConverter.GetBytes(unchecked((BinaryPrimitives.ReadInt16LittleEndian(quad.Slice(10)) << 4) + height * 8))).Write(0x80, new byte[4]);
            NativeBattlePackedMatrix32 parent = Matrix(context, 0x84);
            registers = LoadMatrix(registers, parent, true, false);
            for (int column = 0; column < 3; column++)
            {
                registers = NativeBattleGeometry.LoadIrColumn(registers, unchecked((ushort)Word(context, 0x64 + column * 2)), unchecked((ushort)Word(context, 0x6A + column * 2)), unchecked((ushort)Word(context, 0x70 + column * 2)));
                registers = NativeBattleGeometry.Transform(registers, false, true);
                for (int axis = 0; axis < 3; axis++)
                {
                    context = context.Write(0x64 + axis * 6 + column * 2, BitConverter.GetBytes(unchecked((short)registers.Colors.Ir[axis]!.Value)));
                }
            }
            registers = LoadMatrix(registers, parent, false, true);
            registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(registers, 0, new(unchecked((short)Int(context, 0x78)), unchecked((short)Int(context, 0x7C)), unchecked((short)Int(context, 0x80))), unchecked((short)(Int(context, 0x80) >> 16))));
            for (int axis = 0; axis < 3; axis++)
            {
                context = context.Write(0x78 + axis * 4, BitConverter.GetBytes(registers.Colors.Mac[axis]!.Value));
            }
            registers = LoadMatrix(registers, Matrix(context, 0x64), true, true);
            foreach ((int slot, int offset) in new[] { (0, 0x44), (1, 0x4C), (2, 0x54) })
            {
                registers = LoadVertex(registers, context, slot, offset);
            }
            byte[] data = new byte[40];
            var mask = new NativeBattleGfPacketByteState[40];
            void Store(int offset, ReadOnlySpan<byte> bytes)
            {
                bytes.CopyTo(data.AsSpan(offset));
                Array.Fill(mask, NativeBattleGfPacketByteState.Known, offset, bytes.Length);
            }
            Store(0, BitConverter.GetBytes(0x09000000u));
            Store(4, quad.Slice(0, 4));
            Store(12, quad.Slice(4, 4));
            Store(28, quad.Slice(4, 1));
            Store(21, quad.Slice(5, 1));
            registers = NativeBattleGeometry.ProjectTriple(registers, projection, controls.Dqa, controls.Dqb);
            int right = Math.Min(width + quad[4], 255);
            Store(36, [(byte)right]);
            Store(20, [(byte)right]);
            uint flags = registers.Colors.Flags ?? throw new NotSupportedException("Effect298 smoke requires its actual first projection flags.");
            context = context.Write(0x40, BitConverter.GetBytes(flags));
            NativeBattleGeometryPrimitive? primitive = null;
            if ((flags & 0x60000) == 0)
            {
                Store(22, BitConverter.GetBytes(unchecked((ushort)(BinaryPrimitives.ReadUInt16LittleEndian(quad.Slice(14)) & 0x1FF))));
                for (int vertex = 0; vertex < 3; vertex++)
                {
                    Store(8 + vertex * 8, BitConverter.GetBytes(registers.Geometry.Sxy[vertex]!.Value));
                }
                registers = NativeBattleGeometry.ProjectSingle(LoadVertex(registers, context, 0, 0x5C), projection, controls.Dqa, controls.Dqb);
                int bottom = Math.Min(height + quad[5], 255);
                Store(37, [(byte)bottom]);
                Store(29, [(byte)bottom]);
                Store(32, BitConverter.GetBytes(registers.Geometry.Sxy[2]!.Value));
                ImmutableArray<uint> words = Enumerable.Range(0, 10).Select(word => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(word * 4))).ToImmutableArray();
                ImmutableArray<int> depths = registers.Geometry.Sz.Select(value => unchecked((int)(value ?? throw new NotSupportedException("Effect298 smoke requires its current four-depth insertion payload.")))).ToImmutableArray();
                primitive = new(19, words, depths);
            }
            attempts.Add(new(allocated, data.ToImmutableArray(), mask.ToImmutableArray(), primitive));
            if (primitive is not null)
            {
                allocated += 40;
            }
        }
        memory = memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors };
        return new(memory, context, new(attempts.ToImmutable(), allocated));
    }

    private static NativeBattlePackedMatrix32 Matrix(NativeBattleEffectArena context, int offset)
    {
        (byte[] bytes, bool[] known) = context.ReadSnapshot(offset, 32);
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }

    private static NativeBattleGeometryRegisterStep LoadMatrix(NativeBattleGeometryRegisterStep registers, NativeBattlePackedMatrix32 matrix, bool rotation, bool translation)
    {
        NativeBattleGeometryRegisters geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, matrix.ReadMatrix(), rotation, translation, null);
        if (rotation)
        {
            geometry = geometry with { Rotation20 = Enumerable.Range(0, 20).Select(index => matrix.Known[index] ? (byte?)matrix.Bytes[index] : null).ToImmutableArray() };
        }
        return registers with { Geometry = geometry };
    }

    private static NativeBattleGeometryRegisterStep LoadVertex(NativeBattleGeometryRegisterStep registers, NativeBattleEffectArena context, int slot, int offset)
    {
        (byte[] w, bool[] known) = context.ReadSnapshot(offset + 6, 2);
        return NativeBattleGeometry.LoadVector(registers, slot, new(Word(context, offset), Word(context, offset + 2), Word(context, offset + 4)), known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(w) : null);
    }
    private static short Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    private static int Int(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt32LittleEndian(arena.Read(offset, 4));
}
