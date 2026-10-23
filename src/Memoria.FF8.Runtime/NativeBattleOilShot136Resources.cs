using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleOilShot136Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleOilShot136Resources(NativeBattleGfAnimationSource MainAct, NativeBattleGfAnimationSource SecondaryAct, NativeBattleGfAnimationSource ImpactAct, NativeBattleGfAnimationSource EmitterAct, NativeBattleGfAnimationSource LargeParticleAct, NativeBattleGfAnimationSource LargeImpactAct, NativeBattleModuleResource SpriteA, NativeBattleModuleResource SpriteB, NativeBattleStaticMeshResource InitialMesh, NativeBattleModuleResource InitialScaleMatrix32, NativeBattleEffectHeapAllocation Camera, uint Sound, ImmutableArray<byte> BoneAnchor, ImmutableArray<int> DeformationTable)
{
    public void Validate()
    {
        RequireAct(MainAct, MainActAddress, 0xD28);
        RequireAct(SecondaryAct, SecondaryActAddress, 0x1A6C);
        RequireAct(ImpactAct, ImpactActAddress, 0xC74);
        RequireAct(EmitterAct, EmitterActAddress, 0x290);
        RequireAct(LargeParticleAct, LargeParticleActAddress, 0x158);
        RequireAct(LargeImpactAct, LargeImpactActAddress, 0x888);
        RequireSprite(SpriteA, SpriteAAddress, 0x148, 9);
        RequireSprite(SpriteB, SpriteBAddress, 0x1AC, 16);
        if (InitialMesh.Address != MeshAddress || InitialMesh.Bytes.IsDefault || InitialMesh.Bytes.Length != MeshBytes || BinaryPrimitives.ReadInt32LittleEndian(InitialMesh.Bytes.AsSpan()) != 0x368 || BinaryPrimitives.ReadInt32LittleEndian(InitialMesh.Bytes.AsSpan().Slice(4)) != 108 || InitialScaleMatrix32.Address != MatrixAddress || InitialScaleMatrix32.Bytes.IsDefault || InitialScaleMatrix32.Bytes.Length != 32)
        {
            throw new InvalidDataException("Oil Shot requires its complete mesh and scale matrix cold seeds.");
        }
        for (int group = 0; group < 8; group++)
        {
            if (BinaryPrimitives.ReadInt32LittleEndian(InitialMesh.Bytes.AsSpan().Slice(0x368 + group * 4)) != (group == 7 ? 72 : 0))
            {
                throw new InvalidDataException("Oil Shot requires its original 72 GT4 mesh faces.");
            }
        }
        if (Camera.Identity != "FF8_EN:11F7EDC" || Camera.Data.IsDefault || Camera.Data.Length != CameraLength || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(6)) != CameraLength || Sound != 0x641E0)
        {
            throw new InvalidDataException("Oil Shot requires its original camera and sound.");
        }
        if (BoneAnchor.IsDefault || BoneAnchor.Length != 8 || DeformationTable.IsDefault || DeformationTable.Length != 2049)
        {
            throw new InvalidDataException("Oil Shot requires its full anchor and inclusive deformation table.");
        }
    }
    public NativeBattleGfAnimationSource Act(uint address) => address switch
    {
        MainActAddress => MainAct,
        SecondaryActAddress => SecondaryAct,
        ImpactActAddress => ImpactAct,
        EmitterActAddress => EmitterAct,
        LargeParticleActAddress => LargeParticleAct,
        LargeImpactActAddress => LargeImpactAct,
        _ => throw new NotSupportedException("Oil Shot selected an unsupported ACT source.")
    };
    public NativeBattleModuleResource Sprite(uint address) => address switch
    {
        SpriteAAddress => SpriteA,
        SpriteBAddress => SpriteB,
        _ => throw new NotSupportedException("Oil Shot selected an unsupported sprite source.")
    };
    private static void RequireAct(NativeBattleGfAnimationSource source, uint address, int length)
    {
        if (source.Address != address || source.Data.IsDefault || source.Data.Length != length)
        {
            throw new InvalidDataException("Oil Shot requires its complete original addressed ACT sources.");
        }
    }
    private static void RequireSprite(NativeBattleModuleResource sprite, uint address, int length, int count)
    {
        if (sprite.Address != address || sprite.Bytes.IsDefault || sprite.Bytes.Length != length || BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8)) != count)
        {
            throw new InvalidDataException("Oil Shot requires its complete original sprite source.");
        }
        for (int index = 0; index < count; index++)
        {
            _ = sprite.ProjectedFrame571C80(index);
        }
    }
}
