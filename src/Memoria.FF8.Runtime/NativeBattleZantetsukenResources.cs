using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleZantetsukenStageOrdering(uint Bucket, uint Shift);

public sealed record NativeBattleZantetsukenResources(NativeBattleStaticMeshResource Flare, ImmutableArray<uint> ClipIndices, ImmutableArray<NativeBattleZantetsukenStageOrdering> StageOrderPairs)
{
    public void Validate()
    {
        if (Flare is null || Flare.Address != 0xCCDE04 || Flare.Bytes.IsDefault || Flare.Bytes.Length != 2008)
        {
            throw new InvalidDataException("Zantetsuken requires its complete addressed flare mesh.");
        }
        uint[] indices = [0, 1, 2, 0, 0, 1, 2, 0, 0, 3, 2, 3, 3, 1, 2, 0, 0, 1, 4, 4, 0, 4, 2, 0, 3, 1, 4, 4, 0, 3, 2, 4, 5, 1, 2, 5, 0, 1, 5, 0, 0, 3, 5, 3, 5, 3, 2, 1, 5, 4, 2, 5, 0, 1, 5, 4];
        NativeBattleZantetsukenStageOrdering[] ordering = [new(4126, 6), new(4122, 12), new(17, 2), new(4382, 14)];
        if (ClipIndices.IsDefault || !ClipIndices.SequenceEqual(indices) || StageOrderPairs.IsDefault || !StageOrderPairs.SequenceEqual(ordering))
        {
            throw new InvalidDataException("Zantetsuken clipping and stage ordering require their original tables.");
        }
    }
}
