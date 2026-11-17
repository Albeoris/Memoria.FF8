namespace Memoria.FF8.Runtime;

public enum BattleSourceGroupKind
{
    Formation,
    EncounterCopy
}

public readonly record struct BattleSourceGroupId(int Slot)
{
    public BattleSourceGroupKind Kind { get; init; }

    internal static BattleSourceGroupId ForEncounterCopy(int generation) => new(generation) { Kind = BattleSourceGroupKind.EncounterCopy };
}

/// <summary>Immutable source placement for an enemy added from an area encounter table.</summary>
public sealed record BattleEnemyRosterSource(int Formation, int SceneSlot, string FormationHash)
{
    public BattleSourceGroupId? Group { get; init; }
    public int? OriginalEnemyOrdinal { get; init; }

    internal BattleSourceGroupId GroupForSlot(int addedSlot) => Group ?? new(addedSlot);

    public void Validate()
    {
        if (Formation < 0 || SceneSlot is < 0 or >= NativeBattleSlots.FormationSlotCount || FormationHash is null || FormationHash.Length != 64 || !FormationHash.All(Uri.IsHexDigit) ||
            Group is { Slot: < 0 or >= ExpandedBattleFormation.MaximumRewardSlots } or { Kind: not BattleSourceGroupKind.Formation } || OriginalEnemyOrdinal is < 0 or >= NativeBattleSlots.EnemyCount)
        {
            throw new InvalidDataException("Invalid added enemy placement provenance.");
        }
    }
}
