using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleAdditional181Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleAdditional181Resources(NativeBattleStaticMeshResource MeshA, NativeBattleStaticMeshResource MeshB, NativeBattleModuleResource SpriteA, NativeBattleModuleResource SpriteB, NativeBattleModuleResource SpriteC, NativeBattleEffectHeapAllocation Camera, uint Sound)
{
    public void Validate()
    {
        MeshResource(MeshA, MeshAAddress, 0x1D68, 234, [0, 0, 0, 180, 0, 0, 0, 36]);
        MeshResource(MeshB, MeshBAddress, 0xDA8, 144, [0, 0, 0, 96, 0, 0, 0, 0]);
        RequireSprite(SpriteA, SpriteAAddress, 0x1AC, 16);
        RequireSprite(SpriteB, SpriteBAddress, 0x160, 13);
        RequireSprite(SpriteC, SpriteCAddress, 0xC4, 7);
        if (Camera is null || Camera.Identity != "FF8_EN:D3D3F0" || Camera.Data.IsDefault || Camera.Data.Length != CameraLength || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(6)) != CameraLength || Sound != 0x4949F)
        {
            throw new InvalidDataException("Additional181 requires its complete original camera and sound descriptor.");
        }
    }

    public NativeBattleStaticMeshResource Mesh(uint address) => address switch
    {
        MeshAAddress => MeshA,
        MeshBAddress => MeshB,
        _ => throw new NotSupportedException("Additional181 selected an unsupported addressed mesh.")
    };

    public NativeBattleModuleResource Sprite(uint address) => address switch
    {
        SpriteAAddress => SpriteA,
        SpriteBAddress => SpriteB,
        SpriteCAddress => SpriteC,
        _ => throw new NotSupportedException("Additional181 selected an unsupported addressed sprite.")
    };

    public NativeBattleStaticMeshResource ReadMesh(uint address) => Mesh(address);
    public NativeBattleModuleResource ReadSprite(uint address) => Sprite(address);

    private static void RequireSprite(NativeBattleModuleResource sprite, uint address, int length, int count)
    {
        if (sprite is null || sprite.Address != address || sprite.Bytes.IsDefault || sprite.Bytes.Length != length || BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8)) != count)
        {
            throw new InvalidDataException("Additional181 requires its complete original projected sprite source.");
        }
        for (int phase = 0; phase < count; phase++)
        {
            NativeBattleModuleSpriteFrame frame = sprite.ProjectedFrame571C80(phase);
            for (int quad = 0; quad < frame.Quads.Length / 20; quad++)
            {
                ushort flags = BinaryPrimitives.ReadUInt16LittleEndian(frame.Quads.AsSpan().Slice(quad * 20 + 14));
                if ((flags >> 12) >= 4)
                {
                    throw new InvalidDataException("Additional181 sprite material selector leaves its original four palette bytes.");
                }
            }
        }
    }

    private static void MeshResource(NativeBattleStaticMeshResource mesh, uint address, int length, int vertices, int[] groups)
    {
        if (mesh is null || mesh.Address != address || mesh.Bytes.IsDefault || mesh.Bytes.Length != length || Int(0) != 8 + vertices * 8 || Int(4) != vertices)
        {
            throw new InvalidDataException("Additional181 requires its complete original addressed mesh.");
        }
        int cursor = Int(0);
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        int[] corners = [3, 4, 3, 4, 3, 4, 3, 4];
        for (int group = 0; group < 8; group++)
        {
            if (Int(cursor) != groups[group])
            {
                throw new InvalidDataException("Additional181 mesh primitive groups disagree with their original source.");
            }
            cursor += 4;
            for (int face = 0; face < groups[group]; face++)
            {
                for (int corner = 0; corner < corners[group]; corner++)
                {
                    int index = BinaryPrimitives.ReadUInt16LittleEndian(mesh.Bytes.AsSpan().Slice(cursor + 4 + corner * 2));
                    if ((index & 1) != 0 || index / 2 >= vertices)
                    {
                        throw new InvalidDataException("Additional181 mesh face leaves its packed original vertex bank.");
                    }
                }
                cursor += strides[group];
            }
        }
        if (cursor != length)
        {
            throw new InvalidDataException("Additional181 mesh directory leaves its bounded original resource.");
        }
        int Int(int offset) => BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(offset, 4));
    }
}
