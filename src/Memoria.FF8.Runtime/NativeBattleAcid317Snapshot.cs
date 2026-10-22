using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleAcid317SnapshotStep(NativeBattleEffectMemory Memory, NativeCrtRandomState Random, NativeBattleEffectArena Temporary12);

public static class NativeBattleAcid317Snapshot
{
    public static NativeBattleAcid317SnapshotStep Capture(NativeBattleEffectMemory memory, NativeCrtRandomState random, NativeBattleEffectArena temporary12, Func<int, int, byte[]> readGeometry, uint geometryAddress, ImmutableArray<NativeBattlePackedMatrix32> bones, uint cacheAddress, int cacheLength)
    {
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Acid snapshot requires its current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Acid snapshot requires its current arithmetic registers."));
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Acid snapshot requires its actual effect arena.");
        uint cache = BinaryPrimitives.ReadUInt32LittleEndian(temporary12.Read(8, 4));
        int objects = BinaryPrimitives.ReadInt32LittleEndian(Read(0, 4));
        for (int index = 0; index < objects; index++)
        {
            int cursor = BinaryPrimitives.ReadInt32LittleEndian(Read(checked(4 + index * 4), 4));
            int groups = Count(cursor);
            cursor = checked(cursor + 2);
            temporary12 = temporary12.Write(4, BitConverter.GetBytes(checked(geometryAddress + (uint)cursor)));
            for (int group = 0; group < groups; group++)
            {
                int bone = BinaryPrimitives.ReadInt16LittleEndian(Read(cursor, 2));
                if (bone < 0 || bone >= bones.Length)
                {
                    throw new InvalidDataException("Acid snapshot bone leaves its actual matrix bank.");
                }
                NativeBattlePackedMatrix32 matrix = bones[bone];
                short? padding = matrix.Known[18] && matrix.Known[19] ? BinaryPrimitives.ReadInt16LittleEndian(matrix.Bytes.AsSpan().Slice(18, 2)) : null;
                registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, matrix.ReadMatrix(), true, true, padding) };
                cursor = checked(cursor + 2);
                temporary12 = temporary12.Write(4, BitConverter.GetBytes(checked(geometryAddress + (uint)cursor)));
                int vertices = Count(cursor);
                cursor = checked(cursor + 2);
                for (int vertex = 0; vertex < vertices; vertex++)
                {
                    if (cache < cacheAddress || cacheLength < 0 || (ulong)cache - cacheAddress >= (ulong)cacheLength)
                    {
                        throw new InvalidDataException("Acid snapshot leaves its bounded physical coefficient cache.");
                    }
                    byte[] xyz = Read(cursor, 6);
                    registers = NativeBattleGeometry.LoadVector(registers, 0, new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4))), 0);
                    registers = NativeBattleGeometry.Transform(registers);
                    int y = registers.Colors.Ir[1] ?? throw new NotSupportedException("Acid snapshot requires its transformed IR2.");
                    random = random.Draw(out ushort value);
                    int scaled = unchecked(((value & 31) + 16) * y) >> 10;
                    int destination = checked((int)(cache - NativeBattleEffectNodeMemory.ArenaAddress));
                    arena = arena.Write(destination, [unchecked((byte)-scaled)]);
                    cache = checked(cache + 1);
                    cursor = checked(cursor + 6);
                }
                temporary12 = temporary12.Write(4, BitConverter.GetBytes(checked(geometryAddress + (uint)cursor))).Write(8, BitConverter.GetBytes(cache));
            }
        }
        return new(memory with { Arena = arena, GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors }, random, temporary12);

        byte[] Read(int offset, int count)
        {
            byte[] bytes = readGeometry(offset, count);
            return bytes is not null && bytes.Length == count ? bytes : throw new InvalidDataException("Acid snapshot requires its exact current raw geometry bytes.");
        }
        int Count(int offset)
        {
            int count = BinaryPrimitives.ReadInt16LittleEndian(Read(offset, 2));
            return count >= 0 ? count : throw new InvalidDataException("Acid snapshot has an invalid native signed geometry count.");
        }
    }
}
