using System.Text.Json;

namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    public NativeShotAvailabilityState? ShotAvailability => State.ShotAvailability;
    public NativeFieldLocation? NativeFieldLocation => State.NativeFieldLocation;
    public bool CanStoreNativePartyMenuOwners => true;

    public void InitializeNewGameShotAvailability(NativeShotAvailabilityState shots)
    {
        if (State.ShotAvailability is not null || State.Scene != SceneKind.Field || State.FieldId != 74 || State.EventState is not null || State.SavedWorld?.Origin.Kind != NativeSavedWorldOriginKind.NativeNewGame)
        {
            throw new NotSupportedException("Shot availability initialization requires the untouched authentic New Game template load.");
        }
        shots.Validate();
        State = State with { ShotAvailability = shots };
    }

    public void SetNativePartyMenuOwners(NativeCharacterVitals characterVitals, NativeEconomy economy, NativeShotAvailabilityState shots, NativeSalaryPresentation salaryPresentation, NativeExamTimerState? timer = null, NativeFieldLocation? fieldLocation = null, NativeVibrationProcessState? vibrationProcess = null, NativeWorldProcess? worldProcess = null, NativeFieldFadeState? fieldFade = null, NativeShopHistory? shopHistory = null, NativeMenuRandomState? menuRandom = null, NativeWeaponRecipes? weaponRecipes = null)
    {
        characterVitals.Validate();
        economy.Validate();
        shots.Validate();
        salaryPresentation.Validate();
        timer?.Validate();
        fieldLocation?.Validate();
        vibrationProcess?.Validate();
        worldProcess?.Validate();
        fieldFade?.Validate();
        shopHistory?.Validate();
        menuRandom?.Validate();
        weaponRecipes?.Validate();
        if (weaponRecipes is not null && weaponRecipes.InitOutSha256 != economy.InitOutSha256)
        {
            throw new InvalidDataException("Native weapon recipes and economy have different New Game resource provenance.");
        }
        if (shopHistory is not null && shopHistory.InitOutSha256 != economy.InitOutSha256)
        {
            throw new InvalidDataException("Native shop history and economy have different New Game resource provenance.");
        }
        ValidateJunctionPartyBackupAliases(State.EventMemory);
        ValidateEconomyAliases(State.EventMemory);
        if (characterVitals.InitOutSha256 != economy.InitOutSha256 || shots.InitOutSha256 != economy.InitOutSha256 || shots.KernelSha256 != characterVitals.KernelSha256)
        {
            throw new InvalidDataException("Party menu owners disagree with their actual New Game resources.");
        }
        NativeCharacterVitals nextVitals = characterVitals.DeepCopy();
        NativeEconomy nextEconomy = economy.DeepCopy();
        GameState candidate = State with { CharacterVitals = nextVitals, Economy = nextEconomy, ShotAvailability = shots, SalaryPresentation = salaryPresentation, ExamTimer = timer ?? State.ExamTimer, NativeFieldLocation = fieldLocation ?? State.NativeFieldLocation, VibrationProcess = vibrationProcess ?? State.VibrationProcess, WorldProcess = worldProcess ?? State.WorldProcess, FieldFade = fieldFade ?? State.FieldFade, ShopHistory = shopHistory ?? State.ShopHistory, MenuRandom = menuRandom ?? State.MenuRandom, WeaponRecipes = weaponRecipes ?? State.WeaponRecipes };
        ValidateLiveEventMemoryOwners(candidate, State.EventMemory);
        State = candidate;
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
            throw new InvalidDataException("Root and VM native shot availability disagree; migrate this save explicitly.");
        }
    }
}
