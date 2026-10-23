using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleCannonBlow151Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCannonBlow151Resources(NativeBattleStaticMeshResource MeshA, NativeBattleStaticMeshResource MeshB, NativeBattleStaticMeshResource MeshC, NativeBattleStaticMeshResource MeshD, NativeBattleStaticMeshResource ScrollingMesh, NativeBattleModuleResource SpriteA, NativeBattleModuleResource SpriteB, NativeBattleModuleResource SpriteC, NativeBattleModuleResource SpriteD, NativeBattleModuleResource SpriteE, NativeBattleEffectHeapAllocation Camera, uint Sound)
{
    public void Validate()
    {
        MeshResource(MeshA, MeshAAddress, 0x688, 60, [0, 0, 0, 0, 0, 48, 0, 0]);
        MeshResource(MeshB, MeshBAddress, 0x510, 49, [0, 0, 0, 36, 0, 0, 0, 0]);
        MeshResource(MeshC, MeshCAddress, 0xC30, 73, [0, 0, 0, 0, 0, 0, 12, 60]);
        MeshResource(MeshD, MeshDAddress, 0x928, 72, [0, 0, 0, 0, 0, 0, 0, 48]);
        MeshResource(ScrollingMesh, ScrollingMeshAddress, 0x810, 61, [0, 0, 12, 36, 0, 0, 0, 12]);
        RequireSprite(SpriteA, SpriteAAddress, 0x1AC, 16);
        RequireSprite(SpriteB, SpriteBAddress, 0x1AC, 16);
        RequireSprite(SpriteC, SpriteCAddress, 0x1AC, 16);
        RequireSprite(SpriteD, SpriteDAddress, 0x74, 4);
        RequireSprite(SpriteE, SpriteEAddress, 0x1AC, 16);
        if (Camera is null || Camera.Identity != "FF8_EN:DBEC8C" || Camera.Data.IsDefault || Camera.Data.Length != CameraLength || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(6)) != CameraLength || Sound != 0x61AD9)
        {
            throw new InvalidDataException("Cannon Blow requires its complete original camera and sound descriptor.");
        }
    }

    public NativeBattleStaticMeshResource Mesh(uint address) => address switch
    {
        MeshAAddress => MeshA,
        MeshBAddress => MeshB,
        MeshCAddress => MeshC,
        MeshDAddress => MeshD,
        ScrollingMeshAddress => ScrollingMesh,
        _ => throw new NotSupportedException("Cannon Blow selected an unsupported addressed mesh.")
    };

    public NativeBattleModuleResource Sprite(uint address) => address switch
    {
        SpriteAAddress => SpriteA,
        SpriteBAddress => SpriteB,
        SpriteCAddress => SpriteC,
        SpriteDAddress => SpriteD,
        SpriteEAddress => SpriteE,
        _ => throw new NotSupportedException("Cannon Blow selected an unsupported addressed sprite.")
    };

    public NativeBattleStaticMeshResource ReadMesh(uint address) => Mesh(address);
    public NativeBattleModuleResource ReadSprite(uint address) => Sprite(address);

    private static void RequireSprite(NativeBattleModuleResource sprite, uint address, int length, int count)
    {
        if (sprite is null || sprite.Address != address || sprite.Bytes.IsDefault || sprite.Bytes.Length != length || BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8)) != count)
        {
            throw new InvalidDataException("Cannon Blow requires its complete original projected sprite source.");
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
            throw new InvalidDataException("Cannon Blow requires its complete original addressed mesh.");
        }
        int cursor = Int(0);
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        int[] corners = [3, 4, 3, 4, 3, 4, 3, 4];
        for (int group = 0; group < 8; group++)
        {
            if (Int(cursor) != groups[group])
            {
                throw new InvalidDataException("Cannon Blow mesh primitive groups disagree with their original source.");
            }
            cursor += 4;
            for (int face = 0; face < groups[group]; face++)
            {
                for (int corner = 0; corner < corners[group]; corner++)
                {
                    int index = BinaryPrimitives.ReadUInt16LittleEndian(mesh.Bytes.AsSpan().Slice(cursor + 4 + corner * 2));
                    if ((index & 1) != 0 || index / 2 >= vertices)
                    {
                        throw new InvalidDataException("Cannon Blow mesh face leaves its packed original vertex bank.");
                    }
                }
                cursor += strides[group];
            }
        }
        if (cursor != length)
        {
            throw new InvalidDataException("Cannon Blow mesh directory leaves its bounded original resource.");
        }
        int Int(int offset) => BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(offset, 4));
    }
}
