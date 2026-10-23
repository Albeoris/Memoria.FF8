using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect289WarpVertices
{
    public static NativeBattleEffectMemory Execute6790E0(NativeBattleEffectMemory memory, NativeBattleStaticMeshResource mesh, Func<int, short> readLookup, short angle, int amplitudeA, int amplitudeB, int referenceW, int scale, uint scratchAddress)
    {
        if (mesh.Bytes.IsDefault || mesh.Bytes.Length < 8 || BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4)) != 164)
        {
            throw new InvalidDataException("Effect289 warp requires its current 164 addressed packed source rows.");
        }
        int scratch = NativeBattleWaterExecutionFields.ArenaOffset(scratchAddress, 164 * 8);
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Effect289 warp requires its current COMMON owner.");
        short sine = unchecked((short)NativeBattleCameraMath.Sine(angle));
        short cosine = unchecked((short)NativeBattleCameraMath.Cosine(angle));
        short Scale(short value) => unchecked((short)(unchecked(value * scale) >> 12));
        short a = scale == 4096 ? cosine : Scale(cosine);
        short b = scale == 4096 ? sine : Scale(sine);
        short c = scale == 4096 ? unchecked((short)-sine) : Scale(unchecked((short)-sine));
        NativeBattleActionMatrix rotation = new([a, b, 0, c, a, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Effect289 warp requires its current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect289 warp requires its current arithmetic registers."));
        registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, rotation, true, false, 0) };
        for (int index = 0; index < 164; index++)
        {
            ReadOnlySpan<byte> row = mesh.Bytes.AsSpan().Slice(8 + index * 8, 8);
            short w = BinaryPrimitives.ReadInt16LittleEndian(row[6..]);
            registers = NativeBattleGeometry.LoadVector(registers, 0, new(BinaryPrimitives.ReadInt16LittleEndian(row), BinaryPrimitives.ReadInt16LittleEndian(row[2..]), BinaryPrimitives.ReadInt16LittleEndian(row[4..])), w);
            registers = NativeBattleGeometry.Transform(registers, false);
            int fade = unchecked(w - referenceW);
            if (fade < 0)
            {
                fade = unchecked(-fade);
            }
            if (fade > 4096)
            {
                fade = 4096;
            }
            short fullWave = readLookup(w);
            short halfWave = readLookup(w >> 1);
            int destination = scratch + index * 8;
            arena = arena.Write(destination + 6, BitConverter.GetBytes(unchecked((ushort)fade)));
            for (int axis = 0; axis < 3; axis++)
            {
                arena = arena.Write(destination + axis * 2, BitConverter.GetBytes(unchecked((short)registers.Colors.Ir[axis]!.Value)));
            }
            int displacement = unchecked(unchecked(halfWave * amplitudeA) + unchecked(fullWave * amplitudeB)) >> 12;
            short y = unchecked((short)(BinaryPrimitives.ReadInt16LittleEndian(arena.Read(destination + 2, 2)) - displacement));
            arena = arena.Write(destination + 2, BitConverter.GetBytes(y));
        }
        return memory with { Arena = arena, GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors };
    }
}
