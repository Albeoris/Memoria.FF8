using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBlizzagaResources(NativeBattleEffectHeapAllocation Animation, NativeBattleModuleResource Sprite10, NativeBattleModuleResource Sprite14, NativeBattleBlizzagaSeparateMeshResource PrimaryMesh, NativeBattleBlizzagaSeparateMeshResource PartnerMesh, NativeBattleBlizzagaVertexBank MorphSourceVertices, NativeBattleBlizzagaVertexBank ReferenceVertices, NativeBattleStaticMeshResource Mesh1317088, NativeBattleStaticMeshResource Mesh1318490, uint Sound);

/// <summary>Geometry whose declared vertices are owned by a separate native bank.</summary>
public sealed record NativeBattleBlizzagaSeparateMeshResource(uint Address, int DeclaredVertexCount, ImmutableArray<byte> Bytes)
{
    public void Validate(int group, int stride)
    {
        if ((group != 6 || stride != 28) && (group != 2 || stride != 20) || DeclaredVertexCount != 177 || Bytes.IsDefault || Bytes.Length != 8 + 8 * 4 + 320 * stride || BinaryPrimitives.ReadUInt32LittleEndian(Bytes.AsSpan()) != 8 || BinaryPrimitives.ReadUInt32LittleEndian(Bytes.AsSpan().Slice(4)) != DeclaredVertexCount)
        {
            throw new InvalidDataException("Native Blizzaga separate-bank geometry lacks its complete original header and extent.");
        }
        int cursor = 8;
        for (int index = 0; index < 8; index++)
        {
            uint count = BinaryPrimitives.ReadUInt32LittleEndian(Bytes.AsSpan().Slice(cursor));
            if (count != (index == group ? 320u : 0u))
            {
                throw new InvalidDataException("Native Blizzaga separate-bank geometry has an unsupported primitive directory.");
            }
            cursor += 4 + checked((int)count * stride);
        }
    }
}

public sealed record NativeBattleBlizzagaVertexBank(uint Address, ImmutableArray<byte> Bytes)
{
    public void Validate()
    {
        if (Bytes.IsDefault || Bytes.Length != 0x590 || BinaryPrimitives.ReadUInt32LittleEndian(Bytes.AsSpan()) != 0x590 || BinaryPrimitives.ReadUInt32LittleEndian(Bytes.AsSpan().Slice(4)) != 177)
        {
            throw new InvalidDataException("Native Blizzaga vertex bank requires its original header and all177 complete vertices.");
        }
    }
}
