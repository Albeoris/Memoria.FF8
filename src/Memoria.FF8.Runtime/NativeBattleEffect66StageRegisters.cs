using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect66StageRegisters
{
    public static NativeBattleEffectMemory Publish5099D0(NativeBattleEffectMemory memory, NativeBattleRawMesh mesh, ImmutableArray<NativeBattleActionMatrix> matrices, ImmutableArray<byte> projectedScratch, uint color, NativeBattleEffectArena context44, NativeBattleSummonColors constants)
    {
        ImmutableArray<NativeBattleRawVertexGroup> groups = mesh.PackedVertexGroups ?? throw new NotSupportedException("Effect66 stage drawing requires its original bone group order, including empty groups.");
        NativeBattleGeometryRegisters geometry = memory.GeometryRegisters ?? throw new NotSupportedException("Effect66 stage drawing requires its current geometry registers.");
        foreach (NativeBattleRawVertexGroup group in groups)
        {
            if (group.Bone >= matrices.Length)
            {
                throw new InvalidDataException("Effect66 stage drawing selects a missing current bone.");
            }
            geometry = NativeBattleGeometry.LoadMatrix(geometry, matrices[group.Bone], true, true, null);
        }
        memory = memory with { GeometryRegisters = geometry };
        short cue = BinaryPrimitives.ReadInt16LittleEndian(context44.Read(0x24, 2));
        if (cue == 0)
        {
            return memory;
        }
        if (projectedScratch.IsDefault || projectedScratch.Length < checked(mesh.Vertices.Length * 8))
        {
            throw new InvalidDataException("Effect66 stage drawing requires its actual projected vertex bytes.");
        }
        NativeBattleProjectedVertex[] vertices = Enumerable.Range(0, mesh.Vertices.Length).Select(index => new NativeBattleProjectedVertex(BinaryPrimitives.ReadUInt32LittleEndian(projectedScratch.AsSpan().Slice(index * 8)), BinaryPrimitives.ReadUInt32LittleEndian(projectedScratch.AsSpan().Slice(index * 8 + 4)))).ToArray();
        NativeBattleScriptedColorRegisters registers = memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect66 stage drawing requires its current color registers.");
        NativeBattleModelRendererRegisters far = memory.PrimitiveProcess?.ModelRendererRegisters ?? throw new NotSupportedException("Effect66 stage drawing requires its actual far-color controls.");
        foreach (NativeBattleRawPolygon polygon in mesh.Polygons)
        {
            int baseLength = polygon.Quad ? 20 : 16;
            if (polygon.Bytes.Length != baseLength + 4 || NativeBattleGeometryPackets.EmitRaw50FDF0(polygon.Bytes.AsSpan(), vertices, polygon.Quad, color, 17, 4, cue, constants) is null)
            {
                continue;
            }
            uint material = BinaryPrimitives.ReadUInt32LittleEndian(polygon.Bytes.AsSpan().Slice(baseLength));
            (byte[] cueBytes, bool[] cueKnown) = context44.ReadSnapshot(0x24, 4);
            uint ir0 = BinaryPrimitives.ReadUInt32LittleEndian(cueBytes) | (polygon.Quad ? 0x08000000u : 0);
            registers = NativeBattleScriptedDpcs.Transform45F270(registers, material, ir0, far, constants).Registers;
            if (!cueKnown.All(value => value))
            {
                registers = registers with { Ir0 = null };
            }
        }
        return memory with { ScriptedColorRegisters = registers };
    }
}
