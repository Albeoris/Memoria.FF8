using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect213PosedVertexStep(NativeBattleEffectMemory Memory, NativeBattleEffectArena Temporary56, bool Found);

public static class NativeBattleEffect213PosedVertex
{
    public static NativeBattleEffect213PosedVertexStep Select6CE880(NativeBattleEffectMemory memory, NativeBattleEffectArena temporary56, ImmutableArray<NativeBattleRawMesh> geometry, ImmutableArray<NativeBattleActionMatrix> matrices)
    {
        uint mask = BinaryPrimitives.ReadUInt32LittleEndian(temporary56.Read(0x20, 4));
        int ordinal = BinaryPrimitives.ReadInt32LittleEndian(temporary56.Read(0x24, 4));
        bool found = NativeBattleStorm11Vertices.Find6CE880(geometry, mask, matrices, ref ordinal, out _, (matrix, local) =>
        {
            temporary56 = temporary56.Write(0x24, BitConverter.GetBytes(ordinal));
            for (int axis = 0; axis < 3; axis++)
            {
                int value = axis == 0 ? local.X : axis == 1 ? local.Y : local.Z;
                temporary56 = temporary56.Write(0x30 + axis * 2, BitConverter.GetBytes(unchecked((short)value)));
            }
            (byte[] packedW, bool[] knownW) = temporary56.ReadSnapshot(0x36, 2);
            short? w = knownW.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(packedW) : null;
            NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Effect213 vertex selection requires its current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect213 vertex selection requires its current arithmetic registers."));
            registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, matrix, true, true, null) };
            registers = NativeBattleGeometry.LoadVector(registers, 0, local, w);
            registers = NativeBattleGeometry.Transform(registers);
            memory = memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors };
            for (int axis = 0; axis < 3; axis++)
            {
                int value = registers.Colors.Ir[axis] ?? throw new NotSupportedException("Effect213 vertex selection lost its current IR output.");
                temporary56 = temporary56.Write(0x28 + axis * 2, BitConverter.GetBytes(unchecked((short)value)));
            }
        });
        temporary56 = temporary56.Write(0x24, BitConverter.GetBytes(ordinal));
        return new(memory, temporary56, found);
    }
}
