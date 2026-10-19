using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleAngelWing84BodyResources(NativeBattleEffectOwnedModelResource Model, NativeBattleEffectHeapAllocation CasterOverride, ImmutableArray<ushort> OriginBones, uint DefaultColor44, uint Sound15720AC)
{
    public uint? Transition40 { get; init; }

    public void Validate()
    {
        if (Model.Address != 0x1572BB0 || Model.Bytes.IsDefault || Model.Bytes.Length != 0x3230 || CasterOverride.Data.IsDefault || CasterOverride.Data.Length != 0x2410 || OriginBones.IsDefault || OriginBones.Length != 14)
        {
            throw new InvalidDataException("Native Angel Wing requires its complete original body and caster sequence owners.");
        }
        Model.PoseSource.Validate();
        foreach (ushort bone in OriginBones)
        {
            if (bone >= Model.PoseSource.BoneCount)
            {
                throw new InvalidDataException("Native Angel Wing body-origin bone table leaves the original skeleton.");
            }
        }
    }
}
