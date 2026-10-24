using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>69EEE0/6CE880's masked group traversal, including its reverse within-group index and adjacent source bytes.</summary>
public static class NativeBattleStorm11Vertices
{
    public static int Count69EEE0(ImmutableArray<NativeBattleRawMesh> meshes, uint mask)
    {
        int count = 0;
        for (int mesh = 0; mesh < meshes.Length; mesh++)
        {
            if ((mask & (1u << (mesh & 31))) == 0)
            {
                continue;
            }
            foreach (NativeBattleRawVertexGroup group in Groups(meshes[mesh]))
            {
                count = unchecked(count + group.Count);
            }
        }
        return count;
    }

    public static bool Find6CE880(ImmutableArray<NativeBattleRawMesh> meshes, uint mask, ImmutableArray<NativeBattleActionMatrix> matrices, ref int index, out NativeBattleVector position, Action<NativeBattleActionMatrix, NativeBattleVector>? observeSelected = null)
    {
        for (int mesh = 0; mesh < meshes.Length; mesh++)
        {
            if ((mask & (1u << (mesh & 31))) == 0)
            {
                continue;
            }
            foreach (NativeBattleRawVertexGroup group in Groups(meshes[mesh]))
            {
                index = unchecked(index - group.Count);
                if (index > 0)
                {
                    continue;
                }
                index = unchecked(-index);
                if (group.Bone >= matrices.Length || group.Bytes.IsDefault || index < 0 || index > group.Count || group.Bytes.Length != group.Count * 6 + 6)
                {
                    throw new InvalidDataException("Native6CE880 lost its addressed bone or complete packed group with adjacent source bytes.");
                }
                ReadOnlySpan<byte> bytes = group.Bytes.AsSpan().Slice(index * 6, 6);
                NativeBattleVector local = new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes[2..]), BinaryPrimitives.ReadInt16LittleEndian(bytes[4..]));
                observeSelected?.Invoke(matrices[group.Bone], local);
                position = NativeBattleGfMatrices.Transform460840(matrices[group.Bone], local);
                return true;
            }
        }
        position = default;
        return false;
    }

    private static ImmutableArray<NativeBattleRawVertexGroup> Groups(NativeBattleRawMesh mesh)
    {
        ImmutableArray<NativeBattleRawVertexGroup> groups = mesh.PackedVertexGroups ?? throw new NotSupportedException("Native6CE880 requires its actual packed group bytes and original adjacent source data.");
        if (groups.IsDefault || groups.Any(group => group.Count < 0 || group.Bytes.IsDefault || group.Bytes.Length != group.Count * 6 + 6))
        {
            throw new InvalidDataException("Native69EEE0 requires its bounded original signed vertex groups.");
        }
        return groups;
    }
}
