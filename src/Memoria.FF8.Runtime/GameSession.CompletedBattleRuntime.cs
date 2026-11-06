using System.Text.Json;

namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    /// <summary>Reconstructs retired runtime allocations from selected installed resources after a genuine scene outcome.</summary>
    public void ReconstructCompletedBattleRuntime(BattleEncounterOccurrence occurrence, NativeBattleRetainedMemory selectedColdMemory, NativeCharacterVitals selectedCharacters, NativeShotAvailabilityState? selectedShots)
    {
        ArgumentNullException.ThrowIfNull(occurrence);
        ArgumentNullException.ThrowIfNull(selectedColdMemory);
        ArgumentNullException.ThrowIfNull(selectedCharacters);
        occurrence.Validate();
        selectedColdMemory.Validate();
        selectedCharacters.Validate();
        selectedShots?.Validate();
        if (liveBattle is not null || State.Battle is not null || State.PendingBattleEncounter is not null || State.RandomEncounters?.Pending is not null || State.AchievementCursor is not null || State.OriginalAchievementDrawCursor is not null || !State.AppliedBattleResultIds.Contains(occurrence.OccurrenceId, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("Runtime reconstruction requires a committed outcome without any live combat or achievement transaction owner.");
        }
        bool abandoned = State.Scene == SceneKind.Field && State.FieldId == 75 && State.FieldLoadContext is { Origin: NativeFieldLoadOrigin.BattleDefeat } context && State.AbandonedNativeBattles is { Length: > 0 } history && history[^1] is { } last && last.Encounter == occurrence && last.Branch == context.DefeatBranch && last.ExitClockCalls == State.ExamTimer?.ClockCalls;
        if (abandoned)
        {
            State.AbandonedNativeBattles![^1].Validate();
        }
        bool world = false;
        if (State.Scene == SceneKind.WorldMap && State.FieldId == occurrence.SourceFieldId && State.World?.NativeFootRuntime is { Rule: NativeFootWorldRuntimeState.BattleReloadRule } foot && State.SavedWorld?.FootReturn is { WorldMode: 3 } saved && saved.SourceWorldFrame == occurrence.SourceFrame && foot.ReloadSourceWorldFrame == occurrence.SourceFrame && (State.EventState ?? State.WorldEventState) is { ValueKind: JsonValueKind.Object } events && events.TryGetProperty("Scene", out JsonElement scene) && scene.ValueKind == JsonValueKind.Object && scene.TryGetProperty("NativeWorldBattleOutcome", out JsonElement outcomeJson) && outcomeJson.ValueKind == JsonValueKind.Object)
        {
            NativeWorldBattleOutcome outcome = outcomeJson.Deserialize<NativeWorldBattleOutcome>(JsonOptions) ?? throw new InvalidDataException("Completed world outcome is missing.");
            outcome.Validate();
            world = outcome.Encounter == occurrence && outcome.ExitClockCalls <= State.ExamTimer?.ClockCalls;
            if (world)
            {
                ValidateWorldBattleFootReload(State, State.World, foot);
            }
        }
        if (!abandoned && !world)
        {
            throw new InvalidOperationException("Runtime reconstruction requires its actual completed world reload or fresh defeat allocation.");
        }
        if ((State.ShotAvailability is null) != (selectedShots is null) || selectedShots is { } shots && (shots.Flags770 != State.ShotAvailability!.Flags770 || shots.KernelSha256 != selectedCharacters.KernelSha256 || shots.InitOutSha256 != selectedCharacters.InitOutSha256))
        {
            throw new InvalidDataException("Selected ammo definitions changed their saved availability or character resource aliases.");
        }
        string image = selectedColdMemory.ColdExecutableSha256 ?? throw new InvalidDataException("Selected runtime reconstruction has no authenticated executable image.");
        if (!NativeFieldRuntimeProfile.IsSupportedImage(image) || selectedColdMemory.ModelCpuResources?.Cpu.EffectiveLayout != NativeBattlePartyCpuLayout.ForImage(image))
        {
            throw new InvalidDataException("Selected runtime reconstruction disagrees with its authenticated CPU layout.");
        }
        NativeBattleRetainedMemory previous = State.NativeBattleMemory ?? throw new InvalidDataException("Retired combat runtime is missing.");
        NativeBattleRetainedMemory memory = selectedColdMemory with
        {
            // Retiring scene allocations does not replace the selected process execution environment.
            EffectMemory = selectedColdMemory.EffectMemory! with { ExecutionStack = previous.EffectMemory?.ExecutionStack, HeroCrtStack = previous.EffectMemory?.HeroCrtStack },
            MusicProcess = previous.MusicProcess ?? throw new InvalidDataException("Completed combat lost its physical music owner."),
            VibrationProcess = State.VibrationProcess ?? throw new InvalidDataException("Completed combat lost its canonical vibration owner.")
        };
        memory.Validate();
        GameState candidate = State with { NativeBattleMemory = memory, CharacterVitals = selectedCharacters, ShotAvailability = selectedShots };
        Validate(candidate);
        State = candidate;
        liveFieldMemory = null;
        preparedFieldMemory = null;
        acceptedBattleObservation = null;
    }
}
