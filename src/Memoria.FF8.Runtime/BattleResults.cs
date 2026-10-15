using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record ScriptedBattleOwner([property: JsonRequired] int FieldId, [property: JsonRequired] int ObjectId, [property: JsonRequired] int ScriptId, [property: JsonRequired] int Operation, [property: JsonRequired] int Flags, [property: JsonRequired] string ProgramHash)
{
    public void Validate()
    {
        if (FieldId < 0 || ObjectId < 0 || ScriptId < 0 || Operation < 0 || Flags is < 0 or > ushort.MaxValue || !BattleEncounterOccurrence.IsHash(ProgramHash))
        {
            throw new InvalidDataException("Scripted encounter fact lacks its original BATTLE owner.");
        }
    }
}

/// <summary>An observed encounter occurrence; it contains no exploration route, RNG or battle input history.</summary>
public sealed record BattleEncounterOccurrence([property: JsonRequired] string OccurrenceId, [property: JsonRequired] int Formation, [property: JsonRequired] string FormationHash, [property: JsonRequired] SceneKind SourceScene, [property: JsonRequired] int SourceFieldId, [property: JsonRequired] long SourceFrame, [property: JsonRequired] string SourceHash, [property: JsonRequired] ScriptedBattleOwner? ScriptedOwner)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(OccurrenceId) || OccurrenceId.Length > 160 || Formation is < 0 or >= 1024 || SourceScene is not (SceneKind.Field or SceneKind.WorldMap) || SourceFieldId < 0 || SourceFrame < 0 || !IsHash(FormationHash) || !IsHash(SourceHash) || ScriptedOwner is not null && (SourceScene != SceneKind.Field || ScriptedOwner.FieldId != SourceFieldId))
        {
            throw new InvalidDataException("Encounter occurrence lacks its identity, source scene or original formation resource.");
        }
        ScriptedOwner?.Validate();
    }

    internal static bool IsHash(string value)
    {
        return value is { Length: 64 } && value.All(Uri.IsHexDigit);
    }
}

public sealed record BattleMagicChange([property: JsonRequired] byte SpellId, [property: JsonRequired] byte CountBefore, [property: JsonRequired] int Change);
public sealed record BattleItemConsumption([property: JsonRequired] byte ItemId, [property: JsonRequired] byte CountBefore, [property: JsonRequired] byte Consumed);
public sealed record BattlePartyResult([property: JsonRequired] byte Row, [property: JsonRequired] byte CharacterId, [property: JsonRequired] ushort HitPointsBefore, [property: JsonRequired] ushort HitPointsAfter, [property: JsonRequired] ushort StatusBefore, [property: JsonRequired] ushort StatusAfter, [property: JsonRequired] BattleMagicChange[] MagicChanges);

/// <summary>Observed persistent effects of a completed live encounter. Reward amounts are recomputed from current installed rules.</summary>
public sealed record BattleResult([property: JsonRequired] BattleEncounterOccurrence Encounter, [property: JsonRequired] BattleOutcome Outcome, [property: JsonRequired] BattlePartyResult[] Party, [property: JsonRequired] BattleItemConsumption[] ItemConsumptions, [property: JsonRequired] int[] DefeatedEnemySlots, [property: JsonRequired] int[] RewardEligibleGuardianForceIds, [property: JsonRequired] bool DebugVictory, [property: JsonRequired] BattleRewardPolicy RewardPolicy, [property: JsonRequired] BattleEnemyDefeat[] EnemyDefeats)
{
    public BattleStock[] ScriptedItems { get; init; } = [];
    public uint[]? PersistentAiVariablesBefore { get; init; }
    public uint[]? PersistentAiVariablesAfter { get; init; }

    public void Validate()
    {
        if (Encounter is null)
        {
            throw new InvalidDataException("Battle outcome requires its observed encounter occurrence.");
        }
        Encounter.Validate();
        if ((PersistentAiVariablesBefore is null) != (PersistentAiVariablesAfter is null) || PersistentAiVariablesBefore is { Length: not 8 } || PersistentAiVariablesAfter is { Length: not 8 } || PersistentAiVariablesBefore is not null && RewardPolicy != BattleRewardPolicy.NativeCombat)
        {
            throw new InvalidDataException("Native persistent battle variable effects require their complete before/after owners.");
        }
        if (ScriptedItems is null || ScriptedItems.Any(item => item is null || item.Id is < 1 or > 255 || item.Count is < 1 or > 65535) || ScriptedItems.Select(item => item.Id).Distinct().Count() != ScriptedItems.Length || RewardPolicy != BattleRewardPolicy.NativeCombat && ScriptedItems.Length != 0)
        {
            throw new InvalidDataException("Battle script item rewards require their observed native combat owner.");
        }
        if (Outcome is not (BattleOutcome.Victory or BattleOutcome.Defeat) || Party is null || Party.Length is < 1 or > 3 || Party.Any(member => member is null || member.Row >= 8 || member.CharacterId > 10 || member.MagicChanges is null || member.MagicChanges.Length > 64 || member.MagicChanges.Any(change => change is null || change.SpellId is 0 or >= 64 || change.CountBefore > 100 || change.Change == 0 || change.CountBefore + change.Change is < 0 or > 100) || member.MagicChanges.Select(change => change.SpellId).Distinct().Count() != member.MagicChanges.Length || (member.StatusBefore & ~0x9B) != 0 || (member.StatusAfter & ~0x9B) != 0) || Party.Select(member => member.Row).Distinct().Count() != Party.Length || ItemConsumptions is null || ItemConsumptions.Any(item => item is null || item.ItemId == 0 || item.CountBefore is 0 or > 100 || item.Consumed == 0 || item.Consumed > item.CountBefore) || ItemConsumptions.Select(item => item.ItemId).Distinct().Count() != ItemConsumptions.Length || DefeatedEnemySlots is null || DefeatedEnemySlots.Any(slot => slot is < 0 or >= 8) || DefeatedEnemySlots.Distinct().Count() != DefeatedEnemySlots.Length || RewardEligibleGuardianForceIds is null || RewardEligibleGuardianForceIds.Any(id => id is < 0 or >= 16) || RewardEligibleGuardianForceIds.Distinct().Count() != RewardEligibleGuardianForceIds.Length || Outcome != BattleOutcome.Victory && RewardEligibleGuardianForceIds.Length != 0 || DebugVictory && (Outcome != BattleOutcome.Victory || RewardEligibleGuardianForceIds.Length != 0))
        {
            throw new InvalidDataException("Invalid factual battle outcome or persistent effect operands.");
        }
        if (Party.Any(member => ((member.StatusAfter & 1) != 0) != (member.HitPointsAfter == 0)) || Outcome == BattleOutcome.Victory && Party.All(member => member.HitPointsAfter == 0) || Outcome == BattleOutcome.Defeat && Party.Any(member => member.HitPointsAfter > 0))
        {
            throw new InvalidDataException("Battle outcome contradicts its observed party HP or KO effects.");
        }
        if (!Enum.IsDefined(RewardPolicy) || EnemyDefeats is null || EnemyDefeats.Any(defeat => defeat is null) || EnemyDefeats.Select(defeat => defeat.Slot).Distinct().Count() != EnemyDefeats.Length || DebugVictory != (RewardPolicy == BattleRewardPolicy.DebugNoRewards) || RewardPolicy != BattleRewardPolicy.NativeCombat && EnemyDefeats.Length != 0 || RewardPolicy == BattleRewardPolicy.ResearchAutomaticApOnly && (Outcome != BattleOutcome.Victory || Encounter.ScriptedOwner is null) || RewardPolicy == BattleRewardPolicy.NativeCombat && !EnemyDefeats.Select(defeat => defeat.Slot).Order().SequenceEqual(DefeatedEnemySlots.Order()))
        {
            throw new InvalidDataException("Battle reward policy contradicts its observed native defeats or declared research/debug backend.");
        }
        foreach (BattleEnemyDefeat defeat in EnemyDefeats)
        {
            defeat.Validate();
        }
    }

    public BattleResult DeepCopy()
    {
        return this with { Party = Party.Select(member => member with { MagicChanges = member.MagicChanges.ToArray() }).ToArray(), ItemConsumptions = ItemConsumptions.ToArray(), DefeatedEnemySlots = DefeatedEnemySlots.ToArray(), RewardEligibleGuardianForceIds = RewardEligibleGuardianForceIds.ToArray(), EnemyDefeats = EnemyDefeats.ToArray(), ScriptedItems = ScriptedItems.ToArray(), PersistentAiVariablesBefore = PersistentAiVariablesBefore?.ToArray(), PersistentAiVariablesAfter = PersistentAiVariablesAfter?.ToArray() };
    }
}
