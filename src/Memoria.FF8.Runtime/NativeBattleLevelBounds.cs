using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleLevelBoundsStep(NativeBattleEffectMemory Memory, NativeBattleEffectArena Temporary32, NativeBattleVector Minimum, NativeBattleVector Maximum);

public static class NativeBattleLevelBounds
{
    public static NativeBattleLevelBoundsStep Measure(NativeBattleEffectMemory memory, NativeBattleEffectArena temporary32, ImmutableArray<NativeBattleActionMatrix> bones, ImmutableArray<NativeBattleRawMesh> meshes)
    {
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Native level bounds require current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native level bounds require current arithmetic registers."));
        short[] minimum = [32767, 32767, 32767];
        short[] maximum = [-32767, -32767, -32767];
        foreach (NativeBattleRawMesh mesh in meshes)
        {
            ImmutableArray<NativeBattleRawVertexGroup> groups = mesh.PackedVertexGroups ?? throw new NotSupportedException("Native level bounds require every original packed body vertex group.");
            int visited = 0;
            foreach (NativeBattleRawVertexGroup group in groups)
            {
                if (group.Bone >= bones.Length || group.Count < 0 || group.Bytes.Length < group.Count * 6 + 2)
                {
                    throw new InvalidDataException("Native level bounds vertex group leaves its actual posed bone or packed source.");
                }
                registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, bones[group.Bone], true, true, null) };
                for (int vertex = 0; vertex < group.Count; vertex++)
                {
                    ReadOnlySpan<byte> packed = group.Bytes.AsSpan().Slice(vertex * 6, 8);
                    NativeBattleVector position = new(BinaryPrimitives.ReadInt16LittleEndian(packed), BinaryPrimitives.ReadInt16LittleEndian(packed[2..]), BinaryPrimitives.ReadInt16LittleEndian(packed[4..]));
                    registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(registers, 0, position, BinaryPrimitives.ReadInt16LittleEndian(packed[6..])));
                    for (int lane = 0; lane < 3; lane++)
                    {
                        short value = unchecked((short)registers.Colors.Mac[lane]!.Value);
                        temporary32 = temporary32.Write(12 + 2 * lane, BitConverter.GetBytes(value));
                        minimum[lane] = Math.Min(minimum[lane], value);
                        maximum[lane] = Math.Max(maximum[lane], value);
                    }
                }
                visited += group.Count;
            }
            if (visited != mesh.Vertices.Length)
            {
                throw new InvalidDataException("Native level bounds traversed a different body vertex owner.");
            }
        }
        for (int lane = 0; lane < 3; lane++)
        {
            temporary32 = temporary32.Write(20 + 2 * lane, BitConverter.GetBytes(minimum[lane])).Write(26 + 2 * lane, BitConverter.GetBytes(maximum[lane]));
        }
        return new(memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors }, temporary32, new(minimum[0], minimum[1], minimum[2]), new(maximum[0], maximum[1], maximum[2]));
    }
}
