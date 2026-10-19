using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleWindowedMesh701DD0Result(NativeBattleFileStaging Staging, NativeBattleGfGeometryBatch Batch);

/// <summary>701DD0's reached FT3/FT4/G3/G4/GT4 leaves and its physical context suffix.</summary>
public static class NativeBattleWindowedMesh701DD0
{
    public static NativeBattleWindowedMesh701DD0Result Emit(NativeBattleFileStaging staging, uint temporary, uint meshAddress, ImmutableArray<byte> mesh, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, NativeBattleEsunaMeshContext context, NativeBattleSummonColors colors, Func<int, NativeBattleVector> readSelectedVertex)
    {
        NativeBattleGfGeometryBatch batch = NativeBattleEffectMeshPackets.EmitBatch(mesh, matrix, projection, context.Flags, readSelectedVertex, EmitFace);
        int cursor = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(mesh.AsSpan()));
        for (int group = 0; group < 8; group++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.AsSpan().Slice(cursor, 4));
            cursor = checked(cursor + 4 + count * (group switch { 0 or 1 => 12, 2 or 4 => 20, 3 or 5 => 24, 6 => 28, _ => 36 }));
        }
        staging = Dword(staging, temporary + 52, checked(meshAddress + (uint)cursor));
        return new(staging, batch);

        NativeBattleGfGeometryBatch EmitFace(int group, ReadOnlySpan<byte> face, Func<int, NativeBattleModuleProjectedVertex> project, int offset)
        {
            NativeBattleModuleProjectedVertex?[] vertices = new NativeBattleModuleProjectedVertex?[4];
            NativeBattleGfGeometryBatch leaf = group switch
            {
                2 => EmitFt3_7023B0(face, Project, context, colors, offset),
                3 => NativeBattleRestorationMeshPackets.EmitFt4_88ECA0(face, Project, context, colors, offset),
                4 => NativeBattleRestorationMeshPackets.EmitG3_88F220(face, Project, context, colors, offset),
                5 => NativeBattleRestorationMeshPackets.EmitG4_88F470(face, Project, context, colors, offset),
                7 => NativeBattleRestorationMeshPackets.EmitGt4_88FC50(face, Project, context, colors, offset),
                _ => throw new NotSupportedException("Native701DD0 requires its reached FT3/FT4/G3/G4/GT4 polygon group.")
            };
            if (vertices.Take(3).All(vertex => vertex is not null))
            {
                NativeBattleModuleProjectedVertex[] first = vertices.Take(3).Select(vertex => vertex!.Value).ToArray();
                uint flags = first.Aggregate(0u, (value, vertex) => value | vertex.Flags);
                staging = Dword(staging, temporary + 68, flags);
                if ((flags & 0x60000) == 0)
                {
                    int area = NativeBattleModulePackets.Nclip(first[0].Screen, first[1].Screen, first[2].Screen);
                    staging = Dword(staging, temporary + 56, unchecked((uint)area));
                }
            }
            if (leaf.Attempts.Any(attempt => attempt.Primitive is not null))
            {
                NativeBattleModuleProjectedVertex[] all = vertices.Where(vertex => vertex is not null).Select(vertex => vertex!.Value).ToArray();
                uint average = unchecked((uint)all.Sum(vertex => (int)vertex.Depth) * (all.Length == 4 ? 0x100u : 0x155u)) >> 12;
                int depth = Math.Max(0, unchecked((int)average + context.DepthBias));
                staging = Dword(staging, temporary + 64, unchecked((uint)depth));
            }
            return leaf;

            NativeBattleModuleProjectedVertex Project(int corner)
            {
                NativeBattleModuleProjectedVertex value = project(corner);
                vertices[corner] = value;
                return value;
            }
        }
    }

    private static NativeBattleGfGeometryBatch EmitFt3_7023B0(ReadOnlySpan<byte> face, Func<int, NativeBattleModuleProjectedVertex> project, NativeBattleEsunaMeshContext context, NativeBattleSummonColors colors, int offset)
    {
        NativeBattleGfGeometryBatch batch = NativeBattleRestorationMeshPackets.EmitFt3_88E820(face, project, context, colors, offset);
        if (batch.AllocationBytes != 0)
        {
            return batch;
        }
        NativeBattleGfGeometryAttempt attempt = batch.Attempts.Single();
        byte[] bytes = attempt.Data.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), BinaryPrimitives.ReadUInt32LittleEndian(face));
        return new([attempt with { Data = bytes.ToImmutableArray() }], 0);
    }

    private static NativeBattleFileStaging Dword(NativeBattleFileStaging staging, uint address, uint value) => staging.Write(address, BitConverter.GetBytes(value));
}
