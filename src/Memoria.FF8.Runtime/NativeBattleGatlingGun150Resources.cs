using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleGatlingGun150Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGatlingGun150Resources(NativeBattleStaticMeshResource MeshA, NativeBattleStaticMeshResource MeshB, NativeBattleStaticMeshResource MeshC, NativeBattleModuleResource SpriteA, NativeBattleModuleResource SpriteB, NativeBattleModuleResource SpriteC, NativeBattleEffectHeapAllocation Camera, uint Sound)
{
    public void Validate()
    {
        MeshResource(MeshA, MeshAAddress, 0x300, 25, [0, 0, 12, 12, 0, 0, 0, 0]);
        MeshResource(MeshB, MeshBAddress, 0x2C8, 24, [0, 0, 0, 8, 0, 0, 0, 8]);
        MeshResource(MeshC, MeshCAddress, 0x2C8, 24, [0, 0, 0, 8, 0, 0, 0, 8]);
        RequireSprite(SpriteA, SpriteAAddress, 0x1FC, 16);
        RequireSprite(SpriteB, SpriteBAddress, 0x1AC, 16);
        RequireSprite(SpriteC, SpriteCAddress, 0x194, 15);
        if (Camera is null || Camera.Identity != "FF8_EN:DC1DD4" || Camera.Data.IsDefault || Camera.Data.Length != CameraLength || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(6)) != CameraLength || Sound != 0x61AD6)
        {
            throw new InvalidDataException("Gatling Gun requires its complete original camera and sound descriptor.");
        }
    }

    public NativeBattleStaticMeshResource Mesh(uint address) => address switch
    {
        MeshAAddress => MeshA,
        MeshBAddress => MeshB,
        MeshCAddress => MeshC,
        _ => throw new NotSupportedException("Gatling Gun selected an unsupported addressed mesh.")
    };

    public NativeBattleModuleResource Sprite(uint address) => address switch
    {
        SpriteAAddress => SpriteA,
        SpriteBAddress => SpriteB,
        SpriteCAddress => SpriteC,
        _ => throw new NotSupportedException("Gatling Gun selected an unsupported addressed sprite.")
    };

    public NativeBattleStaticMeshResource ReadMesh(uint address) => Mesh(address);
    public NativeBattleModuleResource ReadSprite(uint address) => Sprite(address);

    private static void RequireSprite(NativeBattleModuleResource sprite, uint address, int length, int count)
    {
        if (sprite is null || sprite.Address != address || sprite.Bytes.IsDefault || sprite.Bytes.Length != length || BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8)) != count)
        {
            throw new InvalidDataException("Gatling Gun requires its complete original projected sprite source.");
        }
        for (int phase = 0; phase < count; phase++)
        {
            _ = sprite.ProjectedFrame571C80(phase);
        }
    }

    private static void MeshResource(NativeBattleStaticMeshResource mesh, uint address, int length, int vertices, int[] groups)
    {
        if (mesh is null || mesh.Address != address || mesh.Bytes.IsDefault || mesh.Bytes.Length != length || Int(0) != 8 + vertices * 8 || Int(4) != vertices)
        {
            throw new InvalidDataException("Gatling Gun requires its complete original addressed mesh.");
        }
        int cursor = Int(0);
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        int[] corners = [3, 4, 3, 4, 3, 4, 3, 4];
        for (int group = 0; group < 8; group++)
        {
            if (Int(cursor) != groups[group])
            {
                throw new InvalidDataException("Gatling Gun mesh primitive groups disagree with their original source.");
            }
            cursor += 4;
            for (int face = 0; face < groups[group]; face++)
            {
                for (int corner = 0; corner < corners[group]; corner++)
                {
                    int index = BinaryPrimitives.ReadUInt16LittleEndian(mesh.Bytes.AsSpan().Slice(cursor + 4 + corner * 2));
                    if ((index & 1) != 0 || index / 2 >= vertices)
                    {
                        throw new InvalidDataException("Gatling Gun mesh face leaves its packed original vertex bank.");
                    }
                }
                cursor += strides[group];
            }
        }
        if (cursor != length)
        {
            throw new InvalidDataException("Gatling Gun mesh directory leaves its bounded original resource.");
        }
        int Int(int offset) => BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(offset, 4));
    }
}
