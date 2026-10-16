namespace Memoria.FF8.Runtime;

public sealed partial record BattleResult
{
    private void ValidateNativeSpecialEffects()
    {
        if (NativeGrowthChanges is null || NativeGrowthChanges.Any(change => change is null) || Party is null || NativeGrowthChanges.Select(change => change.Row).Distinct().Count() != NativeGrowthChanges.Length || NativeGrowthChanges.Any(change => !Party.Any(member => member is not null && member.Row == change.Row)) || (NativeCrtRandomBefore is null) != (NativeCrtRandomAfter is null))
        {
            throw new InvalidDataException("Native battle special effects lack valid party growth or CRT before/after owners.");
        }
        foreach (BattleNativeGrowthChange change in NativeGrowthChanges)
        {
            if (change.Before is null || change.After is null)
            {
                throw new InvalidDataException("Native character growth change lacks its before/after bonus owners.");
            }
            change.Before.Validate();
            change.After.Validate();
        }
        if (NativeCardChange is { } cards)
        {
            if (cards.Before is null || cards.After is null)
            {
                throw new InvalidDataException("Native card change lacks its before/after inventory owners.");
            }
            cards.Before.Validate();
            cards.After.Validate();
        }
    }
}
