using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleSaliva138Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleSaliva138Resources(NativeBattleGfAnimationSource MainAct, NativeBattleGfAnimationSource SecondaryAct, NativeBattleGfAnimationSource ImpactAct, NativeBattleModuleResource SpriteA, NativeBattleModuleResource SpriteB, NativeBattleStaticMeshResource InitialMesh, NativeBattleModuleResource InitialScaleMatrix32, NativeBattleEffectHeapAllocation Camera, uint Sound, ImmutableArray<short> AgeDrift, ImmutableArray<byte> BoneAnchor, ImmutableArray<int> DeformationTable, ImmutableArray<byte> DistanceTable)
{
    public void Validate()
    {
        RequireAct(MainAct, MainActAddress, 0x1198);
        RequireAct(SecondaryAct, SecondaryActAddress, 0xC00);
        RequireAct(ImpactAct, ImpactActAddress, 0xE58);
        RequireSprite(SpriteA, SpriteAAddress, 0x124, 10);
        RequireSprite(SpriteB, SpriteBAddress, 0x160, 13);
        if (InitialMesh.Address != MeshAddress || InitialMesh.Bytes.IsDefault || InitialMesh.Bytes.Length != MeshBytes || BinaryPrimitives.ReadInt32LittleEndian(InitialMesh.Bytes.AsSpan()) != 0x368 || BinaryPrimitives.ReadInt32LittleEndian(InitialMesh.Bytes.AsSpan().Slice(4)) != 108 || InitialScaleMatrix32.Address != MatrixAddress || InitialScaleMatrix32.Bytes.IsDefault || InitialScaleMatrix32.Bytes.Length != 32)
        {
            throw new InvalidDataException("Saliva requires its complete mesh and scale matrix cold seeds.");
        }
        if (Camera.Identity != "FF8_EN:11E3024" || Camera.Data.IsDefault || Camera.Data.Length != CameraLength || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(6)) != CameraLength || Sound != 0x641B5)
        {
            throw new InvalidDataException("Saliva requires its original camera and sound.");
        }
        if (AgeDrift.IsDefault || AgeDrift.Length != 18 || BoneAnchor.IsDefault || BoneAnchor.Length != 8 || DeformationTable.IsDefault || DeformationTable.Length != 2049 || DistanceTable.IsDefault || DistanceTable.Length != 0x4000)
        {
            throw new InvalidDataException("Saliva requires its full drift, anchor, inclusive deformation and distance tables.");
        }
    }
    public NativeBattleGfAnimationSource Act(uint address) => address switch
    {
        MainActAddress => MainAct,
        SecondaryActAddress => SecondaryAct,
        ImpactActAddress => ImpactAct,
        _ => throw new NotSupportedException("Saliva selected an unsupported ACT source.")
    };
    public NativeBattleModuleResource Sprite(uint address) => address switch
    {
        SpriteAAddress => SpriteA,
        SpriteBAddress => SpriteB,
        _ => throw new NotSupportedException("Saliva selected an unsupported sprite source.")
    };
    private static void RequireAct(NativeBattleGfAnimationSource source, uint address, int length)
    {
        if (source.Address != address || source.Data.IsDefault || source.Data.Length != length)
        {
            throw new InvalidDataException("Saliva requires its complete original addressed ACT sources.");
        }
    }
    private static void RequireSprite(NativeBattleModuleResource sprite, uint address, int length, int count)
    {
        if (sprite.Address != address || sprite.Bytes.IsDefault || sprite.Bytes.Length != length || BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8)) != count)
        {
            throw new InvalidDataException("Saliva requires its complete original sprite source.");
        }
        for (int index = 0; index < count; index++)
        {
            _ = sprite.ProjectedFrame571C80(index);
        }
    }
}
