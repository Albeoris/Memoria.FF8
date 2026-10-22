using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleMeltEye158Resources(NativeBattleEffectHeapAllocation Camera, NativeBattleModuleResource SpriteA, NativeBattleModuleResource SpriteB, NativeBattleModuleResource SpriteC, NativeBattleStaticMeshResource MeshA, NativeBattleStaticMeshResource MeshB, ImmutableArray<NativeBattleModuleAnchor> Vectors, uint Sound)
{
    public void Validate()
    {
        if (Camera.Identity != "FF8_EN:DAFBF8" || Camera.Data.IsDefault || Camera.Data.Length != NativeBattleMeltEye158Profile.CameraLength || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(6)) != NativeBattleMeltEye158Profile.CameraLength || Vectors.IsDefault || Vectors.Length != 11 || Sound != 0x61AC0)
        {
            throw new InvalidDataException("Melt-Eye requires its complete original camera, eleven packed vectors and sound.");
        }
        ValidateSprite(SpriteA, NativeBattleMeltEye158Profile.SpriteAAddress, 0x224, 16);
        ValidateSprite(SpriteB, NativeBattleMeltEye158Profile.SpriteBAddress, 0x1AC, 16);
        ValidateSprite(SpriteC, NativeBattleMeltEye158Profile.SpriteCAddress, 0xDC, 8);
        if (MeshA.Address != NativeBattleMeltEye158Profile.MeshAAddress || MeshA.Bytes.IsDefault || MeshA.Bytes.Length != 0x3F0 || MeshB.Address != NativeBattleMeltEye158Profile.MeshBAddress || MeshB.Bytes.IsDefault || MeshB.Bytes.Length != 0x270)
        {
            throw new InvalidDataException("Melt-Eye requires its two complete original static meshes.");
        }
    }

    public NativeBattleModuleResource Sprite(uint address)
    {
        Validate();
        return address switch
        {
            NativeBattleMeltEye158Profile.SpriteAAddress => SpriteA,
            NativeBattleMeltEye158Profile.SpriteBAddress => SpriteB,
            NativeBattleMeltEye158Profile.SpriteCAddress => SpriteC,
            _ => throw new NotSupportedException("Melt-Eye selected an unsupported sprite source.")
        };
    }

    public NativeBattleStaticMeshResource Mesh(uint address)
    {
        Validate();
        return address switch
        {
            NativeBattleMeltEye158Profile.MeshAAddress => MeshA,
            NativeBattleMeltEye158Profile.MeshBAddress => MeshB,
            _ => throw new NotSupportedException("Melt-Eye selected an unsupported mesh source.")
        };
    }

    private static void ValidateSprite(NativeBattleModuleResource sprite, uint address, int length, int frames)
    {
        if (sprite.Address != address || sprite.Bytes.IsDefault || sprite.Bytes.Length != length || BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8)) != frames)
        {
            throw new InvalidDataException("Melt-Eye requires its original complete projected sprite.");
        }
        for (int index = 0; index < frames; index++)
        {
            _ = sprite.ProjectedFrame571C80(index);
        }
    }
}
