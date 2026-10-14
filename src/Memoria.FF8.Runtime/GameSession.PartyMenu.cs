using System.Text.Json;

namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    public NativeShotAvailabilityState? ShotAvailability => State.ShotAvailability;
    public NativeFieldLocation? NativeFieldLocation => State.NativeFieldLocation;
    public FieldTransition? NativeIncomingFieldEntryWords => State.FieldArrival;
    public bool CanStoreNativePartyMenuOwners => true;

    public void InitializeNewGameShotAvailability(NativeShotAvailabilityState shots)
    {
        if (State.ShotAvailability is not null || State.Scene != SceneKind.Field || State.FieldId != 74 || State.EventState is not null || State.ReplayJournal is not { EntryCount: 0, IsComplete: true } || State.SavedWorld?.Origin.Kind != NativeSavedWorldOriginKind.NativeNewGame)
        {
            throw new NotSupportedException("Shot availability initialization requires the untouched authentic New Game template load.");
        }
        shots.Validate();
        State = State with { ShotAvailability = shots };
    }

    public void SetNativePartyMenuOwners(NativeCharacterVitals characterVitals, NativeEconomy economy, NativeShotAvailabilityState shots, NativeSalaryPresentation salaryPresentation, NativeExamTimerState? timer = null, NativeFieldLocation? fieldLocation = null, NativeVibrationProcessState? vibrationProcess = null)
    {
        characterVitals.Validate();
        economy.Validate();
        shots.Validate();
        salaryPresentation.Validate();
        timer?.Validate();
        fieldLocation?.Validate();
        vibrationProcess?.Validate();
        ValidateJunctionPartyBackupAliases(State.Variables, State.NativeVariableValues);
        ValidateEconomyAliases(State.Variables, State.NativeVariableValues);
        if (characterVitals.InitOutSha256 != economy.InitOutSha256 || shots.InitOutSha256 != economy.InitOutSha256 || shots.KernelSha256 != characterVitals.KernelSha256)
        {
            throw new InvalidDataException("Party menu owners disagree with their actual New Game resources.");
        }
        NativeCharacterVitals nextVitals = characterVitals.DeepCopy();
        NativeEconomy nextEconomy = economy.DeepCopy();
        if (!IsReplayCaptureActive)
        {
            MarkReplayUnavailable("Party menu owners changed outside a recorded engine transaction.");
        }
        State = State with { CharacterVitals = nextVitals, Economy = nextEconomy, ShotAvailability = shots, SalaryPresentation = salaryPresentation, ExamTimer = timer ?? State.ExamTimer, NativeFieldLocation = fieldLocation ?? State.NativeFieldLocation, VibrationProcess = vibrationProcess ?? State.VibrationProcess };
    }

    private static void ValidatePartyMenuOwnersCheckpoint(GameState state, JsonElement? vm)
    {
        state.ShotAvailability?.Validate();
        state.NativeFieldLocation?.Validate();
        if (vm is JsonElement locationVm && (!locationVm.TryGetProperty("NativeFieldLocation", out JsonElement location) || !JsonElement.DeepEquals(location, JsonSerializer.SerializeToElement(state.NativeFieldLocation, JsonOptions))))
        {
            throw new InvalidDataException("Root and VM actual470250 saved field location disagree.");
        }
        if (vm is JsonElement snapshot && (!snapshot.TryGetProperty("ShotAvailability", out JsonElement shots) || !JsonElement.DeepEquals(shots, JsonSerializer.SerializeToElement(state.ShotAvailability, JsonOptions))))
        {
            throw new InvalidDataException("Root and VM native shot availability disagree; recreate authentic history.");
        }
    }
}
