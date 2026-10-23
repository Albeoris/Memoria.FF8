using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleAcid170Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleAcid170Resources(NativeBattleStaticMeshResource MeshA, NativeBattleStaticMeshResource MeshB, NativeBattleModuleResource SpriteA, NativeBattleModuleResource SpriteB, ImmutableArray<byte> EndpointVertices, ImmutableArray<int> DripCounts, NativeBattleEffectHeapAllocation Camera, uint Sound)
{
    public void Validate()
    {
        MeshResource(MeshA, MeshAAddress, 0xA20, 73, [0, 0, 12, 36, 0, 0, 0, 24]);
        MeshResource(MeshB, MeshBAddress, 0x6E8, 72, [0, 0, 0, 0, 0, 48, 0, 0]);
        RequireSprite(SpriteA, SpriteAAddress, 0xA8, 6);
        RequireSprite(SpriteB, SpriteBAddress, 0x1AC, 16);
        if (EndpointVertices.IsDefault || EndpointVertices.Length != SelectedVerticesLength || DripCounts.IsDefault || DripCounts.Length != DripCountLength)
        {
            throw new InvalidDataException("Acid170 requires its complete original endpoint vertices and drip count table.");
        }
        if (Camera is null || Camera.Identity != "FF8_EN:D68D0C" || Camera.Data.IsDefault || Camera.Data.Length != CameraLength || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(6)) != CameraLength || Sound != 0x61AFB)
        {
            throw new InvalidDataException("Acid170 requires its complete original camera and sound descriptor.");
        }
    }

    public NativeBattleStaticMeshResource Mesh(uint address) => address switch
    {
        MeshAAddress => MeshA,
        MeshBAddress => MeshB,
        _ => throw new NotSupportedException("Acid170 selected an unsupported addressed mesh.")
    };

    public NativeBattleModuleResource Sprite(uint address) => address switch
    {
        SpriteAAddress => SpriteA,
        SpriteBAddress => SpriteB,
        _ => throw new NotSupportedException("Acid170 selected an unsupported addressed sprite.")
    };

    public NativeBattleStaticMeshResource ReadMesh(uint address) => Mesh(address);
    public NativeBattleModuleResource ReadSprite(uint address) => Sprite(address);

    private static void RequireSprite(NativeBattleModuleResource sprite, uint address, int length, int count)
    {
        if (sprite is null || sprite.Address != address || sprite.Bytes.IsDefault || sprite.Bytes.Length != length || BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8)) != count)
        {
            throw new InvalidDataException("Acid170 requires its complete original projected sprite source.");
        }
        for (int phase = 0; phase < count; phase++)
        {
            NativeBattleModuleSpriteFrame frame = sprite.ProjectedFrame571C80(phase);
            for (int quad = 0; quad < frame.Quads.Length / 20; quad++)
            {
                ushort flags = BinaryPrimitives.ReadUInt16LittleEndian(frame.Quads.AsSpan().Slice(quad * 20 + 14));
                if ((flags >> 12) >= 4)
                {
                    throw new InvalidDataException("Acid170 sprite material selector leaves its original four palette bytes.");
                }
            }
        }
    }

    private static void MeshResource(NativeBattleStaticMeshResource mesh, uint address, int length, int vertices, int[] groups)
    {
        if (mesh is null || mesh.Address != address || mesh.Bytes.IsDefault || mesh.Bytes.Length != length || Int(0) != 8 + vertices * 8 || Int(4) != vertices)
        {
            throw new InvalidDataException("Acid170 requires its complete original addressed mesh.");
        }
        int cursor = Int(0);
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        int[] corners = [3, 4, 3, 4, 3, 4, 3, 4];
        for (int group = 0; group < 8; group++)
        {
            if (Int(cursor) != groups[group])
            {
                throw new InvalidDataException("Acid170 mesh primitive groups disagree with their original source.");
            }
            cursor += 4;
            for (int face = 0; face < groups[group]; face++)
            {
                for (int corner = 0; corner < corners[group]; corner++)
                {
                    int index = BinaryPrimitives.ReadUInt16LittleEndian(mesh.Bytes.AsSpan().Slice(cursor + 4 + corner * 2));
                    if ((index & 1) != 0 || index / 2 >= vertices)
                    {
                        throw new InvalidDataException("Acid170 mesh face leaves its packed original vertex bank.");
                    }
                }
                cursor += strides[group];
            }
        }
        if (cursor != length)
        {
            throw new InvalidDataException("Acid170 mesh directory leaves its bounded original resource.");
        }
        int Int(int offset) => BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(offset, 4));
    }
}
