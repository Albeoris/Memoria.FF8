using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRinoa89Resources(NativeBattleEffectHeapAllocation StaticImage, ImmutableArray<NativeBattleEffectHeapAllocation> InitialTextures)
{
    public const uint StaticAddress = 0x14913FC;
    public const uint StaticEnd = 0x14D0740;

    public NativeBattleEffectOwnedModelResource? Body { get; init; }
    public ImmutableArray<NativeBattleEffectFileAsset> Files { get; init; }
    public ImmutableDictionary<uint, uint> Sounds { get; init; } = ImmutableDictionary<uint, uint>.Empty;

    public NativeBattleGfAnimationSource Program(uint address)
    {
        if (address is not (0x14A09A0 or 0x14A3EB0 or 0x14A6370 or 0x14A6600 or 0x14A9FF0))
        {
            throw new NotSupportedException("Native Wishing Star programme has no authenticated static owner.");
        }
        int offset = checked((int)(address - StaticAddress));
        return new(address, StaticImage.Data.Slice(offset, StaticImage.Data.Length - offset));
    }

    public void Validate()
    {
        if (StaticImage.Data.IsDefault || StaticImage.Data.Length != StaticEnd - StaticAddress || InitialTextures.IsDefault || InitialTextures.Length != 2 || InitialTextures[0].Data.IsDefault || InitialTextures[0].Data.Length != 33824 || InitialTextures[1].Data.IsDefault || InitialTextures[1].Data.Length != 101408)
        {
            throw new InvalidDataException("Native Rinoa89 requires its complete original static image and two original texture owners.");
        }
    }
}
