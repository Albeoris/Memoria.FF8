using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWildCannonBlow156Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleWildCannonBlow156Resources(NativeBattleStaticMeshResource MeshA, NativeBattleStaticMeshResource MeshB, NativeBattleStaticMeshResource MeshC, NativeBattleStaticMeshResource MeshD, NativeBattleModuleResource SpriteA, NativeBattleModuleResource SpriteB, NativeBattleModuleResource SpriteC, NativeBattleModuleResource SpriteD, NativeBattleModuleResource SpriteE, NativeBattleEffectHeapAllocation Camera, uint SoundA, uint SoundB)
{
    public void Validate()
    {
        RequireMesh(MeshA, MeshAAddress, 0x688, 60, [0, 0, 0, 0, 0, 48, 0, 0]);
        RequireMesh(MeshB, MeshBAddress, 0x510, 49, [0, 0, 0, 36, 0, 0, 0, 0]);
        RequireMesh(MeshC, MeshCAddress, 0xC30, 73, [0, 0, 0, 0, 0, 0, 12, 60]);
        RequireMesh(MeshD, MeshDAddress, 0x928, 72, [0, 0, 0, 0, 0, 0, 0, 48]);
        RequireSprite(SpriteA, SpriteAAddress, 0x1AC, 16);
        RequireSprite(SpriteB, SpriteBAddress, 0x74, 4);
        RequireSprite(SpriteC, SpriteCAddress, 0x1AC, 16);
        RequireSprite(SpriteD, SpriteDAddress, 0x1AC, 16);
        RequireSprite(SpriteE, SpriteEAddress, 0x1AC, 16);
        if (Camera.Identity != "FF8_EN:DB4CD4" || Camera.Data.IsDefault || Camera.Data.Length != CameraLength || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(6)) != CameraLength || SoundA != 0x61AD9 || SoundB != 0x61ADB)
        {
            throw new InvalidDataException("Wild Cannon Blow requires its complete original camera and sounds.");
        }
    }

    public NativeBattleStaticMeshResource Mesh(uint address) => address switch
    {
        MeshAAddress => MeshA,
        MeshBAddress => MeshB,
        MeshCAddress => MeshC,
        MeshDAddress => MeshD,
        _ => throw new NotSupportedException("Wild Cannon Blow selected an unsupported mesh source.")
    };

    public NativeBattleModuleResource Sprite(uint address) => address switch
    {
        SpriteAAddress => SpriteA,
        SpriteBAddress => SpriteB,
        SpriteCAddress => SpriteC,
        SpriteDAddress => SpriteD,
        SpriteEAddress => SpriteE,
        _ => throw new NotSupportedException("Wild Cannon Blow selected an unsupported sprite source.")
    };

    private static void RequireMesh(NativeBattleStaticMeshResource mesh, uint address, int length, int vertices, int[] counts)
    {
        if (mesh.Address != address || mesh.Bytes.IsDefault || mesh.Bytes.Length != length || BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan()) != 8 + vertices * 8 || BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4)) != vertices)
        {
            throw new InvalidDataException("Wild Cannon Blow requires its complete selected original mesh.");
        }
        int cursor = 8 + vertices * 8;
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < counts.Length; group++)
        {
            if (BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(cursor)) != counts[group])
            {
                throw new InvalidDataException("Wild Cannon Blow requires its original selected primitive groups.");
            }
            cursor = checked(cursor + 4 + counts[group] * strides[group]);
        }
        if (cursor != length)
        {
            throw new InvalidDataException("Wild Cannon Blow mesh groups leave their source owner.");
        }
    }

    private static void RequireSprite(NativeBattleModuleResource sprite, uint address, int length, int count)
    {
        if (sprite.Address != address || sprite.Bytes.IsDefault || sprite.Bytes.Length != length || BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8)) != count)
        {
            throw new InvalidDataException("Wild Cannon Blow requires its complete original sprite source.");
        }
        for (int index = 0; index < count; index++)
        {
            _ = sprite.ProjectedFrame571C80(index);
        }
    }
}
