using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEnergyBomber217Resources(NativeBattleGfAnimationSource MainAct, NativeBattleGfAnimationSource ImpactAct, NativeBattleStaticMeshResource TrailMesh, NativeBattleModuleResource Sprite, NativeBattleEffectHeapAllocation Camera, uint Sound)
{
    public void Validate()
    {
        if (MainAct.Address != NativeBattleEnergyBomber217Profile.MainActAddress || MainAct.Data.IsDefault || MainAct.Data.Length != 0x4900 || ImpactAct.Address != NativeBattleEnergyBomber217Profile.ImpactActAddress || ImpactAct.Data.IsDefault || ImpactAct.Data.Length != 0x1BC0 || Camera.Identity != "FF8_EN:11355D4" || Camera.Data.IsDefault || Camera.Data.Length != NativeBattleEnergyBomber217Profile.CameraLength || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(6)) != NativeBattleEnergyBomber217Profile.CameraLength || Sound != 0x642C8)
        {
            throw new InvalidDataException("Energy Bomber requires its complete original addressed ACTs, camera and sound.");
        }
        if (TrailMesh.Address != NativeBattleEnergyBomber217Profile.TrailMeshAddress || TrailMesh.Bytes.IsDefault || TrailMesh.Bytes.Length != 0x270 || Sprite.Address != NativeBattleEnergyBomber217Profile.SpriteAddress || Sprite.Bytes.IsDefault || Sprite.Bytes.Length != 0x16C || BinaryPrimitives.ReadUInt16LittleEndian(Sprite.Bytes.AsSpan().Slice(8)) != 12)
        {
            throw new InvalidDataException("Energy Bomber requires its original trail mesh and projected sprite.");
        }
        for (int frame = 0; frame < 12; frame++)
        {
            _ = Sprite.ProjectedFrame571C80(frame);
        }
    }

    public NativeBattleGfAnimationSource Act(uint address)
    {
        Validate();
        if (address == MainAct.Address)
        {
            return MainAct;
        }
        if (address == ImpactAct.Address)
        {
            return ImpactAct;
        }
        throw new NotSupportedException("Energy Bomber selected an unsupported ACT source.");
    }
}
