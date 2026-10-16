namespace Memoria.FF8.Runtime;

public static partial class BattleResultProjection
{
    private static NativeCharacterVital[] ApplyNativeGrowthChanges(BattleResult result, NativeCharacterVital[] records)
    {
        foreach (BattleNativeGrowthChange change in result.NativeGrowthChanges)
        {
            NativeCharacterVital current = records[change.Row];
            if (current.HitPointBonus != change.Before.HitPointBonus || !current.StatBonuses.AsSpan().SequenceEqual(change.Before.StatBonuses.AsSpan()))
            {
                throw new InvalidDataException("Native battle growth effect differs from its saved statistic bonus owners.");
            }
            records[change.Row] = current with { HitPointBonus = change.After.HitPointBonus, StatBonuses = change.After.StatBonuses.ToArray() };
        }
        return records;
    }

    private static NativeCardInventory? ApplyNativeCardChange(BattleResult result, NativeCardInventory? source)
    {
        if (result.NativeCardChange is not { } change)
        {
            return source?.DeepCopy();
        }
        change.Before.Validate();
        change.After.Validate();
        if (source is null || !source.Cards.AsSpan().SequenceEqual(change.Before.Cards) || !source.ObtainedRareCards.AsSpan().SequenceEqual(change.Before.ObtainedRareCards))
        {
            throw new InvalidDataException("Native direct card effect differs from its current saved inventory owner.");
        }
        return change.After.DeepCopy();
    }
}
