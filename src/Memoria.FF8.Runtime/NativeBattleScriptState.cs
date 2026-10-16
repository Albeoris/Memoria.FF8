namespace Memoria.FF8.Runtime;

/// <summary>Allocation-independent battle script owners from487DF0, retained without replaying callbacks.</summary>
[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record NativeBattleScriptState
{
    public NativeBattleAllocationControl? AllocationControl { get; init; }
    public bool DisableAutomaticTargetReplacement { get; init; }
    public bool ScriptedExitRequested { get; init; }
    public byte ResultFlags { get; init; }
    public byte? SavedBattleFlagsBefore { get; init; }
    public byte? SavedBattleFlagsAfter { get; init; }
    public byte SavedBattleFlagsSetMask { get; init; }
    public byte SavedBattleFlagsClearMask { get; init; }
    public byte? LimitSpecialFlagsBefore { get; init; }
    public byte? LimitSpecialFlagsAfter { get; init; }
    public byte LimitSpecialSetMask { get; init; }
    public byte? ShotFlagsBefore { get; init; }
    public byte? ShotFlagsAfter { get; init; }
    public byte ShotFlagsSetMask { get; init; }
    [System.Text.Json.Serialization.JsonRequired]
    public NativeBattleScriptTasks Tasks { get; init; } = NativeBattleScriptTasks.Clear();
    [System.Text.Json.Serialization.JsonIgnore]
    public ushort[] WaitCounters => Enumerable.Range(0, 16).Where(slot => Tasks.Allocated(slot) && Tasks.Slots[slot].ReadDword(0) == 0x48ACD0).Select(slot => Tasks.Slots[slot].ReadWord(8)).ToArray();
    public byte[] AcquiredGuardianForces { get; init; } = [];
    public byte[] Cards { get; init; } = [];
    public int[] UnlockedTutorials { get; init; } = [];

    public void Validate()
    {
        ValidateLimitSpecialEffect();
        if (Tasks is null)
        {
            throw new InvalidDataException("Native script callback bank is missing; migrate the standalone snapshot explicitly.");
        }
        Tasks.Validate();
        if ((ShotFlagsBefore is null) != (ShotFlagsAfter is null) || ShotFlagsBefore is byte shotBefore && (ShotFlagsSetMask == 0 || ShotFlagsAfter != (byte)(shotBefore | ShotFlagsSetMask)) || ShotFlagsBefore is null && ShotFlagsSetMask != 0)
        {
            throw new InvalidDataException("Shot learning requires its original BYTE1CFE770 before value and exact ammunition bit producers.");
        }
        if ((SavedBattleFlagsBefore is null) != (SavedBattleFlagsAfter is null) || SavedBattleFlagsBefore is byte before && (SavedBattleFlagsSetMask == 0 || SavedBattleFlagsAfter != NativeSavedBattleFlags.ApplyBattleEffects(before, SavedBattleFlagsSetMask, SavedBattleFlagsClearMask)) || SavedBattleFlagsBefore is null && (SavedBattleFlagsSetMask != 0 || SavedBattleFlagsClearMask != 0))
        {
            throw new InvalidDataException("Native saved battle flag effects require their exact before/after BYTE owners and native producer masks.");
        }
        if (AllocationControl?.SceneSlot >= 8 || AcquiredGuardianForces is null || AcquiredGuardianForces.Length > 255 || AcquiredGuardianForces.Any(id => id >= 16) || Cards is null || Cards.Length > 255 || Cards.Any(id => id >= NativeCardInventory.CardCount) || UnlockedTutorials is null || UnlockedTutorials.Any(id => id is < 0 or >= 128) || UnlockedTutorials.Distinct().Count() != UnlockedTutorials.Length)
        {
            throw new InvalidDataException("Native battle script state has invalid callback, acquisition or persistent flag owners.");
        }
    }

    private void ValidateLimitSpecialEffect()
    {
        if ((LimitSpecialFlagsBefore is null) != (LimitSpecialFlagsAfter is null) || (LimitSpecialSetMask & ~0x3F) != 0 || LimitSpecialFlagsBefore is byte before && (LimitSpecialSetMask == 0 || LimitSpecialFlagsAfter != (byte)(before | LimitSpecialSetMask)) || LimitSpecialFlagsBefore is null && LimitSpecialSetMask != 0)
        {
            throw new InvalidDataException("Slots learning requires its original BYTE1CFE771 before value and exact special magic bit producers.");
        }
    }

    public NativeBattleScriptState DeepCopy()
    {
        Validate();
        return this with { AcquiredGuardianForces = AcquiredGuardianForces.ToArray(), Cards = Cards.ToArray(), UnlockedTutorials = UnlockedTutorials.ToArray() };
    }
}
