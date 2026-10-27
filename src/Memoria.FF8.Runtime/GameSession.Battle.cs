using System.Text.Json;

namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    private static void ValidateBattleFactsCheckpoint(GameState state)
    {
        ValidateAchievementCheckpoint(state);
        if (state.Battle?.NativeExamTimer is { } timer && !System.Text.Json.JsonElement.DeepEquals(System.Text.Json.JsonSerializer.SerializeToElement(timer), System.Text.Json.JsonSerializer.SerializeToElement(state.ExamTimer)))
        {
            throw new InvalidDataException("Active battle and game root disagree about the canonical exam timer.");
        }
        if (state.Battle?.NativeGlobalSoundProcess is { } sound && !System.Text.Json.JsonElement.DeepEquals(System.Text.Json.JsonSerializer.SerializeToElement(sound), System.Text.Json.JsonSerializer.SerializeToElement(state.GlobalSoundProcess)))
        {
            throw new InvalidDataException("Active battle and game root disagree about the canonical global sound process.");
        }
        state.NativeBattleMemory?.Validate();
        foreach (NativeBattleAbandonment abandoned in state.AbandonedNativeBattles ?? [])
        {
            if (abandoned is null)
            {
                throw new InvalidDataException("Abandoned battle history contains a missing receipt.");
            }
            abandoned.Validate();
            if (state.AppliedBattleResultIds is null || !state.AppliedBattleResultIds.Contains(abandoned.Encounter.OccurrenceId, StringComparer.Ordinal) || state.ExamTimer is null || abandoned.ExitClockCalls > state.ExamTimer.ClockCalls)
            {
                throw new InvalidDataException("Abandoned native battle has no applied outcome receipt.");
            }
            if (abandoned.Encounter.ScriptedOwner is not null && state.EventState is System.Text.Json.JsonElement vm)
            {
                if (vm.ValueKind == JsonValueKind.Object && vm.TryGetProperty("Scene", out JsonElement scene) && scene.ValueKind == JsonValueKind.Object && scene.TryGetProperty("NativeBattleCompletions", out JsonElement completions) && completions.ValueKind == JsonValueKind.Array && completions.EnumerateArray().Any(item => item.TryGetProperty("Abandonment", out JsonElement receipt) && receipt.ValueKind != JsonValueKind.Null && receipt.Deserialize<NativeBattleAbandonment>(JsonOptions)?.Encounter.OccurrenceId == abandoned.Encounter.OccurrenceId && receipt.Deserialize<NativeBattleAbandonment>(JsonOptions) != abandoned))
                {
                    throw new InvalidDataException("Abandoned scripted battle root and retained completion history disagree.");
                }
            }
        }
        if (state.EventState is JsonElement outcomeVm && outcomeVm.ValueKind == JsonValueKind.Object && outcomeVm.TryGetProperty("Scene", out JsonElement outcomeScene) && outcomeScene.ValueKind == JsonValueKind.Object && outcomeScene.TryGetProperty("NativeAbandonedBattleOutcome", out JsonElement outcomeJson) && outcomeJson.ValueKind != JsonValueKind.Null)
        {
            NativeBattleAbandonment abandoned = outcomeJson.Deserialize<NativeBattleAbandonment>(JsonOptions) ?? throw new InvalidDataException("Empty abandoned native outcome.");
            if (state.AbandonedNativeBattles?.Contains(abandoned) != true)
            {
                throw new InvalidDataException("Native abandoned outcome lacks its matching root battle receipt.");
            }
        }
        if (state.EventState is JsonElement worldOutcomeVm && worldOutcomeVm.ValueKind == JsonValueKind.Object && worldOutcomeVm.TryGetProperty("Scene", out JsonElement worldOutcomeScene) && worldOutcomeScene.ValueKind == JsonValueKind.Object && worldOutcomeScene.TryGetProperty("NativeWorldBattleOutcome", out JsonElement worldOutcomeJson) && worldOutcomeJson.ValueKind != JsonValueKind.Null)
        {
            NativeWorldBattleOutcome outcome = worldOutcomeJson.Deserialize<NativeWorldBattleOutcome>(JsonOptions) ?? throw new InvalidDataException("Empty world native outcome.");
            outcome.Validate();
            if (!state.AppliedBattleResultIds.Contains(outcome.Encounter.OccurrenceId, StringComparer.Ordinal) || state.ExamTimer is null || outcome.ExitClockCalls > state.ExamTimer.ClockCalls)
            {
                throw new InvalidDataException("World native outcome lost its applied encounter or actual process clock.");
            }
        }
        if (state.NativeBattleMemory is { } memory && state.Battle is { } battle)
        {
            NativeBattleRetainedMemory observed = NativeBattleRetainedMemory.Capture(battle, memory.ColdExecutableSha256);
            if (memory != observed && !System.Text.Json.JsonElement.DeepEquals(System.Text.Json.JsonSerializer.SerializeToElement(memory), System.Text.Json.JsonSerializer.SerializeToElement(observed)))
            {
                throw new InvalidDataException("Retained battle memory contradicts the active battle's physical owners.");
            }
        }
        if (state.AbandonedNativeBattles is { } abandonments && abandonments.Select(item => item.Encounter.OccurrenceId).Distinct(StringComparer.Ordinal).Count() != abandonments.Length || state.FieldLoadContext is { Origin: NativeFieldLoadOrigin.BattleDefeat } fresh && (state.AbandonedNativeBattles is not { Length: > 0 } history || history[^1].Branch != fresh.DefeatBranch))
        {
            throw new InvalidDataException("Fresh defeat entry lost its unique abandoned battle receipt.");
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
        BattleAchievementCursor? cursor = encounter is null ? null : State.PendingBattleEncounter?.OccurrenceId == encounter.OccurrenceId ? State.AchievementCursor : new(encounter.OccurrenceId, []);
        State = State with { PendingBattleEncounter = encounter, AchievementCursor = cursor, OriginalAchievementDrawCursor = encounter?.OccurrenceId == State.OriginalAchievementDrawCursor?.OccurrenceId ? State.OriginalAchievementDrawCursor : null };
    }

    public void AdvanceNativeBattleEntryRandom(NativeCrtRandomState before, int draws)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(draws);
        if (State.CrtRandom != before || State.PendingBattleEncounter is null || HasActiveBattle)
        {
            throw new InvalidOperationException("Battle entry random draws require the pending encounter and its unchanged CRT owner.");
        }
        NativeCrtRandomState after = before;
        for (int draw = 0; draw < draws; draw++)
        {
            after = after.Draw(out _);
        }
        State = State with { CrtRandom = after };
    }

    public void PublishNativeBattleEntryMusic(NativeBattleMusicEntryPreparation entry)
    {
        if (State.PendingBattleEncounter is not { } pending || entry.Receipt.Encounter != pending.Formation || HasActiveBattle)
        {
            throw new InvalidOperationException("Battle entry music requires its pending encounter before combat publication.");
        }
        entry.MusicProcess.Validate();
        entry.SoundProcess.Validate();
        entry.Receipt.Validate();
        NativeBattleRetainedMemory memory = State.NativeBattleMemory ?? throw new NotSupportedException("Battle entry music requires its retained process memory.");
        if (liveFieldMemory is { } fieldMemory)
        {
            liveFieldMemory = fieldMemory with { MusicProcess = entry.MusicProcess };
        }
        State = State with { NativeBattleMemory = memory with { MusicProcess = entry.MusicProcess }, GlobalSoundProcess = entry.SoundProcess };
    }

    public void CommitBattleResult(BattleEncounterOccurrence encounter, NativeCharacterVitals vitals, NativeEconomy economy, NativeCardInventory? cards, NativeBattleVariableState? battleVariables = null, NativeSavedBattleFlags? savedBattleFlags = null, byte? savedBattleFlagsBefore = null, NativeCrtRandomState? crtRandom = null, NativeCrtRandomState? crtRandomBefore = null, byte savedBattleFlagsSetMask = 0, byte savedBattleFlagsClearMask = 0, NativeScannedModels? scannedModelsBefore = null, NativeScannedModels? scannedModelsAfter = null, byte? limitSpecialFlagsBefore = null, byte? limitSpecialFlagsAfter = null, byte limitSpecialSetMask = 0, byte? shotFlagsBefore = null, byte? shotFlagsAfter = null, byte shotFlagsSetMask = 0, NativeGlobalRandomChange? globalRandomChange = null, NativeKnownMagic? knownMagicBefore = null, NativeKnownMagic? knownMagicAfter = null, IReadOnlyList<BattleGuardianForceCounterChange>? guardianForceCounters = null, IReadOnlyList<BattleEnemyDefeat>? achievementDefeats = null, BattleOutcome? achievementOutcome = null, byte? achievementFormationFlags = null, BattleRewardPolicy achievementRewardPolicy = BattleRewardPolicy.NativeCombat, ChocoboRewardLedger? chocoboRewardsBefore = null, ChocoboRewardLedger? chocoboRewardsAfter = null)
    {
        encounter.Validate();
        if ((chocoboRewardsBefore is null) != (chocoboRewardsAfter is null) || chocoboRewardsBefore is not null && !JsonElement.DeepEquals(JsonSerializer.SerializeToElement(State.ChocoboRewards), JsonSerializer.SerializeToElement(chocoboRewardsBefore)))
        {
            throw new InvalidDataException("Battle Chocobo reward receipt differs from its save-owned ledger.");
        }
        chocoboRewardsAfter?.Validate();
        if (chocoboRewardsBefore is not null && !chocoboRewardsBefore.AcquiredUniqueRewardIds.IsSubsetOf(chocoboRewardsAfter!.AcquiredUniqueRewardIds))
        {
            throw new InvalidDataException("Battle reward history cannot erase acquired rewards.");
        }
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
        GameState draws = StageOriginalAchievementDraws(State, acceptedBattleObservation ?? (State.Battle is { } savedBattle ? BattleLiveObservation.FromSnapshot(savedBattle) : null));
        GameState staged = achievementDefeats is null ? draws : StageBattleAchievementFacts(draws, achievementDefeats);
        GameState accepted = FinalizeBattleAchievementFacts(staged, encounter.OccurrenceId, achievementDefeats ?? [], achievementOutcome, achievementOutcome == BattleOutcome.Escaped || achievementOutcome == BattleOutcome.Victory && (achievementFormationFlags & 0x10) == 0);
        GameState candidate = accepted with { ChocoboRewards = chocoboRewardsAfter ?? State.ChocoboRewards, KnownMagic = knownMagicAfter ?? State.KnownMagic, GlobalRandom = globalRandom, ShotAvailability = shots, LimitProgress = limitProgress, ScannedModels = scannedModelsAfter ?? State.ScannedModels, CrtRandom = crtRandom ?? State.CrtRandom, CharacterVitals = vitals.DeepCopy(), SavedBattleFlags = savedBattleFlags ?? SynchronizeSavedBattleFlags(vitals), Economy = economy.DeepCopy(), CardInventory = cards?.DeepCopy(), BattleVariables = battleVariables ?? State.BattleVariables, AppliedBattleResultIds = [.. State.AppliedBattleResultIds, encounter.OccurrenceId], PendingBattleEncounter = null };
        ValidateNativeSavedBattleFlagsCheckpoint(candidate);
        ValidateLiveEventMemoryOwners(candidate, State.EventMemory);
        State = StageBattleAchievementObservation(candidate with { AchievementCursor = null, OriginalAchievementDrawCursor = null }, encounter.OccurrenceId, achievementOutcome, achievementFormationFlags, achievementRewardPolicy);
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

    private BattleSession? liveBattle;
    private BattleLiveObservation? acceptedBattleObservation;

    public bool HasActiveBattle => liveBattle is not null || State.Battle is not null;
    public BattleLiveObservation? LiveBattle => acceptedBattleObservation;

    private GameState CaptureLiveBattle(GameState source)
    {
        if (liveBattle is null)
        {
            return source;
        }
        BattleSnapshot snapshot = liveBattle.Capture();
        NativeBattleRetainedMemory retained = NativeBattleRetainedMemory.Capture(snapshot, source.NativeBattleMemory?.ColdExecutableSha256 ?? snapshot.Definition.NativeRetainedMemory?.ColdExecutableSha256);
        return source with { Battle = snapshot, NativeBattleMemory = retained, ExamTimer = snapshot.NativeExamTimer ?? source.ExamTimer, VibrationProcess = snapshot.NativeVibrationProcess ?? source.VibrationProcess, GlobalSoundProcess = snapshot.NativeGlobalSoundProcess ?? source.GlobalSoundProcess };
    }

    /// <summary>Exports the accepted battle once at lifecycle return and releases its live attachment.</summary>
    public BattleSnapshot CompleteLiveBattle()
    {
        if (liveBattle is null)
        {
            throw new InvalidOperationException("Battle completion requires its attached live owner.");
        }
        SetLiveBattle(liveBattle);
        State = CaptureLiveBattle(State);
        liveBattle = null;
        return State.Battle!;
    }

    public void SetBattle(BattleSnapshot? snapshot)
    {
        BattleSnapshot? owned = snapshot is null ? null : BattleSession.Restore(snapshot).Capture();
        liveFieldMemory = null;
        liveBattle = null;
        SetOwnedBattle(owned);
        acceptedBattleObservation = owned is null ? null : BattleLiveObservation.FromSnapshot(owned);
    }

    public void SetLiveBattle(BattleSession? active)
    {
        if (active is null)
        {
            if (liveBattle is not null)
            {
                State = CaptureLiveBattle(State);
            }
            liveBattle = null;
            SetOwnedBattle(null);
            acceptedBattleObservation = null;
            return;
        }
        BattleLiveObservation observation = active.ObserveLiveState();
        GameState candidate = AdvanceNativePlaytime(State with { Battle = null, ExamTimer = observation.Timer ?? State.ExamTimer, VibrationProcess = observation.Vibration ?? State.VibrationProcess, GlobalSoundProcess = observation.Sound ?? State.GlobalSoundProcess });
        if (candidate.AchievementCursor is null && State.PendingBattleEncounter is not null)
        {
            candidate = candidate with { AchievementCursor = LegacyAchievementCursor(State) };
        }
        candidate = StageOriginalAchievementDraws(candidate, observation);
        candidate = StageBattleAchievementFacts(candidate, observation.EnemyDefeats);
        State = candidate;
        liveFieldMemory = null;
        liveBattle = active;
        acceptedBattleObservation = observation;
    }

    private void SetOwnedBattle(BattleSnapshot? owned)
    {
        NativeBattleRetainedMemory? memory = owned is null ? State.NativeBattleMemory : NativeBattleRetainedMemory.Capture(owned, State.NativeBattleMemory?.ColdExecutableSha256 ?? owned.Definition.NativeRetainedMemory?.ColdExecutableSha256);
        GameState candidate = AdvanceNativePlaytime(State with { Battle = owned, NativeBattleMemory = memory, ExamTimer = owned?.NativeExamTimer ?? State.ExamTimer, VibrationProcess = owned?.NativeVibrationProcess ?? State.VibrationProcess, GlobalSoundProcess = owned?.NativeGlobalSoundProcess ?? State.GlobalSoundProcess });
        // Compare against the previous accepted owner when migrating a legacy live snapshot.
        if (candidate.AchievementCursor is null && State.PendingBattleEncounter is not null)
        {
            candidate = candidate with { AchievementCursor = LegacyAchievementCursor(State) };
        }
        candidate = StageOriginalAchievementDraws(candidate, owned is null ? null : BattleLiveObservation.FromSnapshot(owned));
        State = owned is null ? candidate : StageBattleAchievementFacts(candidate, owned.EnemyDefeats);
        if (owned is null && State.PendingBattleEncounter is null)
        {
            ApplyPendingGameplayPreferences();
        }
    }
}
