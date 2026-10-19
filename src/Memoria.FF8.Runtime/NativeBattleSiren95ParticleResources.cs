using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleSiren95ParticleResources(ImmutableArray<byte> BoneSelectors, NativeBattleEffectHeapAllocation ModelSprites, NativeBattleEffectHeapAllocation AlternateModelSprites, NativeBattleEffectHeapAllocation ScreenCloudSprites, NativeBattleEffectHeapAllocation ScreenSpraySprites)
{
    public void Validate()
    {
        if (BoneSelectors.IsDefault || BoneSelectors.Length != 32 || ModelSprites.Data.IsDefault || ModelSprites.Data.Length != 0x12C || AlternateModelSprites.Data.IsDefault || AlternateModelSprites.Data.Length != 0x144 || ScreenCloudSprites.Data.IsDefault || ScreenCloudSprites.Data.Length != 0x140 || ScreenSpraySprites.Data.IsDefault || ScreenSpraySprites.Data.Length != 0x130)
        {
            throw new InvalidDataException("Native Siren particles require their complete selector table and original sprite directories.");
        }
    }
}
