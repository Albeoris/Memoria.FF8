namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    private static void ValidateBattleFactsCheckpoint(GameState state)
    {
        state.NativeBattleMemory?.Validate();
        if (state.NativeBattleMemory is { } memory && state.Battle is { } battle)
        {
            NativeBattleRetainedMemory observed = NativeBattleRetainedMemory.Capture(battle, memory.ColdExecutableSha256);
            if (!System.Text.Json.JsonElement.DeepEquals(System.Text.Json.JsonSerializer.SerializeToElement(memory), System.Text.Json.JsonSerializer.SerializeToElement(observed)))
            {
                throw new InvalidDataException("Retained battle memory contradicts the active battle's physical owners.");
            }
        }
        ValidateNativeSavedBattleFlagsCheckpoint(state);
        state.BattleVariables?.Validate();
        state.ScannedModels?.Validate();
        state.KnownMagic?.Validate();
        state.PendingBattleEncounter?.Validate();
        if (state.AppliedBattleResultIds is null || state.AppliedBattleResultIds.Any(id => string.IsNullOrWhiteSpace(id) || id.Length > 160) || state.AppliedBattleResultIds.Distinct(StringComparer.Ordinal).Count() != state.AppliedBattleResultIds.Length || state.PendingBattleEncounter is { } pending && (state.AppliedBattleResultIds.Contains(pending.OccurrenceId, StringComparer.Ordinal) || pending.SourceScene != state.Scene || pending.SourceFieldId != state.FieldId))
        {
            throw new InvalidDataException("Battle fact checkpoint has duplicate receipts or a contradictory current encounter owner.");
        }
    }

    public void SetPendingBattleEncounter(BattleEncounterOccurrence? encounter)
    {
        encounter?.Validate();
        if (encounter is not null && State.AppliedBattleResultIds.Contains(encounter.OccurrenceId, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("A completed encounter occurrence cannot become pending again.");
        }
        State = State with { PendingBattleEncounter = encounter };
    }

    public void CommitBattleResult(BattleEncounterOccurrence encounter, NativeCharacterVitals vitals, NativeEconomy economy, NativeCardInventory? cards, NativeBattleVariableState? battleVariables = null, NativeSavedBattleFlags? savedBattleFlags = null, byte? savedBattleFlagsBefore = null, NativeCrtRandomState? crtRandom = null, NativeCrtRandomState? crtRandomBefore = null, byte savedBattleFlagsSetMask = 0, byte savedBattleFlagsClearMask = 0, NativeScannedModels? scannedModelsBefore = null, NativeScannedModels? scannedModelsAfter = null, byte? limitSpecialFlagsBefore = null, byte? limitSpecialFlagsAfter = null, byte limitSpecialSetMask = 0, byte? shotFlagsBefore = null, byte? shotFlagsAfter = null, byte shotFlagsSetMask = 0, NativeGlobalRandomChange? globalRandomChange = null, NativeKnownMagic? knownMagicBefore = null, NativeKnownMagic? knownMagicAfter = null, IReadOnlyList<BattleGuardianForceCounterChange>? guardianForceCounters = null)
    {
        encounter.Validate();
        ValidateNativeScanReceipt(scannedModelsBefore, scannedModelsAfter);
        ValidateNativeKnownMagicReceipt(knownMagicBefore, knownMagicAfter);
        NativeLimitProgress? limitProgress = ProjectNativeLimitLearning(limitSpecialFlagsBefore, limitSpecialFlagsAfter, limitSpecialSetMask);
        NativeShotAvailabilityState? shots = ProjectNativeShotLearning(shotFlagsBefore, shotFlagsAfter, shotFlagsSetMask);
        vitals.Validate();
        ValidateNativeGuardianForceCounterReceipt(guardianForceCounters, vitals);
        economy.Validate();
        cards?.Validate();
        battleVariables?.Validate();
        savedBattleFlags?.Validate();
        if ((crtRandom is null) != (crtRandomBefore is null) || crtRandomBefore is not null && State.CrtRandom != crtRandomBefore)
        {
            throw new InvalidDataException("Battle CRT random receipt differs from its actual process owner.");
        }
        if ((savedBattleFlags is null) != (savedBattleFlagsBefore is null) || savedBattleFlags is null && (savedBattleFlagsSetMask != 0 || savedBattleFlagsClearMask != 0) || savedBattleFlags is not null && (savedBattleFlagsSetMask == 0 || State.SavedBattleFlags is not { } current || current.EffectiveKnownMask != byte.MaxValue || savedBattleFlags.EffectiveKnownMask != byte.MaxValue || current.Value != savedBattleFlagsBefore || current.InitOutSha256 != savedBattleFlags.InitOutSha256 || savedBattleFlags.Value != NativeSavedBattleFlags.ApplyBattleEffects(current.Value, savedBattleFlagsSetMask, savedBattleFlagsClearMask)))
        {
            throw new InvalidDataException("Battle saved flag receipt disagrees with its current canonical before value or native opcode54 transformation.");
        }
        if (State.PendingBattleEncounter != encounter || State.AppliedBattleResultIds.Contains(encounter.OccurrenceId, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("Battle result requires its unapplied current encounter occurrence.");
        }
        NativeGlobalRandomState? globalRandom = ProjectNativeGlobalRandom(globalRandomChange);
        GameState candidate = State with { KnownMagic = knownMagicAfter ?? State.KnownMagic, GlobalRandom = globalRandom, ShotAvailability = shots, LimitProgress = limitProgress, ScannedModels = scannedModelsAfter ?? State.ScannedModels, CrtRandom = crtRandom ?? State.CrtRandom, CharacterVitals = vitals.DeepCopy(), SavedBattleFlags = savedBattleFlags ?? SynchronizeSavedBattleFlags(vitals), Economy = economy.DeepCopy(), CardInventory = cards?.DeepCopy(), BattleVariables = battleVariables ?? State.BattleVariables, AppliedBattleResultIds = [.. State.AppliedBattleResultIds, encounter.OccurrenceId], PendingBattleEncounter = null };
        ValidateNativeSavedBattleFlagsCheckpoint(candidate);
        ValidateLiveEventMemoryOwners(candidate, State.EventMemory);
        State = candidate;
    }

    public void SetRandomEncounters(RandomEncounterSessionState state)
    {
        state.Validate();
        State = State with { RandomEncounters = state };
    }
    public void SetFullBattlePlaythrough(bool enabled)
    {
        State = State with { FullBattlePlaythrough = enabled ? true : null };
    }

    public void SetBattle(BattleSnapshot? snapshot)
    {
        BattleSnapshot? owned = snapshot is null ? null : BattleSession.Restore(snapshot).Capture();
        NativeBattleRetainedMemory? memory = owned is null ? State.NativeBattleMemory : NativeBattleRetainedMemory.Capture(owned, State.NativeBattleMemory?.ColdExecutableSha256 ?? owned.Definition.NativeRetainedMemory?.ColdExecutableSha256);
        State = State with { Battle = owned, NativeBattleMemory = memory };
    }
}
