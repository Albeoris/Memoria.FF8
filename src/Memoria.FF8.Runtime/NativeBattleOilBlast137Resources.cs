using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleOilBlast137Resources(NativeBattleGfAnimationSource OpeningAct, NativeBattleGfAnimationSource MainAct, NativeBattleGfAnimationSource ImpactAct, NativeBattleModuleResource Sprite, NativeBattleStaticMeshResource StaticMesh, NativeBattleStaticMeshResource CustomMesh, NativeBattleEffectHeapAllocation Camera, uint Sound, NativeBattleModuleResource InitialMatrix32)
{
    public void Validate()
    {
        if (OpeningAct.Address != NativeBattleOilBlast137Profile.OpeningActAddress || OpeningAct.Data.IsDefault || OpeningAct.Data.Length != 0x6C8 || MainAct.Address != NativeBattleOilBlast137Profile.MainActAddress || MainAct.Data.IsDefault || MainAct.Data.Length != 0x18E8 || ImpactAct.Address != NativeBattleOilBlast137Profile.ImpactActAddress || ImpactAct.Data.IsDefault || ImpactAct.Data.Length != 0x3348)
        {
            throw new InvalidDataException("Oil Blast requires its three complete original addressed ACT sources.");
        }
        if (Camera.Identity != "FF8_EN:11F0108" || Camera.Data.IsDefault || Camera.Data.Length != NativeBattleOilBlast137Profile.CameraLength || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(6)) != NativeBattleOilBlast137Profile.CameraLength || Sound != 0x641B2 || InitialMatrix32.Address != NativeBattleOilBlast137Profile.MatrixAddress || InitialMatrix32.Bytes.IsDefault || InitialMatrix32.Bytes.Length != 32)
        {
            throw new InvalidDataException("Oil Blast requires its original camera, sound and mutable matrix seed.");
        }
        if (StaticMesh.Address != NativeBattleOilBlast137Profile.StaticMeshAddress || StaticMesh.Bytes.IsDefault || StaticMesh.Bytes.Length != 0x150 || CustomMesh.Address != NativeBattleOilBlast137Profile.CustomMeshAddress || CustomMesh.Bytes.IsDefault || CustomMesh.Bytes.Length != 0x19C8 || Sprite.Address != NativeBattleOilBlast137Profile.SpriteAddress || Sprite.Bytes.IsDefault || Sprite.Bytes.Length != 0x158 || BinaryPrimitives.ReadUInt16LittleEndian(Sprite.Bytes.AsSpan().Slice(8)) != 12)
        {
            throw new InvalidDataException("Oil Blast requires its original complete mesh and sprite sources.");
        }
        for (int index = 0; index < 12; index++)
        {
            _ = Sprite.ProjectedFrame571C80(index);
        }
    }

    public NativeBattleGfAnimationSource Act(uint address)
    {
        Validate();
        return address switch
        {
            NativeBattleOilBlast137Profile.OpeningActAddress => OpeningAct,
            NativeBattleOilBlast137Profile.MainActAddress => MainAct,
            NativeBattleOilBlast137Profile.ImpactActAddress => ImpactAct,
            _ => throw new NotSupportedException("Oil Blast selected an unsupported ACT source.")
        };
    }
}
