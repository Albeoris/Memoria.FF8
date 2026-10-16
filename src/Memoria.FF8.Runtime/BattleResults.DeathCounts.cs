namespace Memoria.FF8.Runtime;

public sealed record BattleNativeDeathCountChange(byte Row, ushort Before, ushort After);

public sealed partial record BattleResult
{
    public BattleNativeDeathCountChange[] NativeDeathCountChanges { get; init; } = [];

    private void ValidateNativeDeathCounts()
    {
        if (NativeDeathCountChanges is null || Party is null || NativeDeathCountChanges.Any(change => change is null || change.Before == change.After || !Party.Any(member => member is not null && member.Row == change.Row)) || NativeDeathCountChanges.Select(change => change.Row).Distinct().Count() != NativeDeathCountChanges.Length)
        {
            throw new InvalidDataException("Native death counters require unique changed party before/after WORD owners.");
        }
    }
}

public static partial class BattleResultProjection
{
    private static NativeCharacterVital[] ApplyNativeDeathCounts(BattleResult result, NativeCharacterVital[] records)
    {
        foreach (BattleNativeDeathCountChange change in result.NativeDeathCountChanges)
        {
            NativeCharacterVital current = records[change.Row];
            if (current.DeathCount != change.Before)
            {
                throw new InvalidDataException("Native battle death counter differs from its actual saved before owner.");
            }
            records[change.Row] = current with { DeathCount = change.After };
        }
        return records;
    }
}
