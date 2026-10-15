using System.Text.Json;

namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    public NativeWeaponRecipes? WeaponRecipes => State.WeaponRecipes;
    public bool CanStoreWeaponRecipes => true;

    public void InitializeNewGameWeaponRecipes(NativeWeaponRecipes recipes)
    {
        if (State.WeaponRecipes is not null || State.Scene != SceneKind.Field || State.FieldId != 74 || State.EventState is not null || State.ReplayJournal is not { EntryCount: 0, IsComplete: true } || State.SavedWorld?.Origin.Kind != NativeSavedWorldOriginKind.NativeNewGame)
        {
            throw new NotSupportedException("Native weapon recipe initialization requires the untouched authentic New Game template load.");
        }
        recipes.Validate();
        State = State with { WeaponRecipes = recipes };
    }

    public void SetWeaponRecipes(NativeWeaponRecipes recipes)
    {
        ArgumentNullException.ThrowIfNull(recipes);
        recipes.Validate();
        if (State.Economy is NativeEconomy economy && economy.InitOutSha256 != recipes.InitOutSha256)
        {
            throw new InvalidDataException("Native weapon recipes and economy have different New Game provenance.");
        }
        State = State with { WeaponRecipes = recipes };
    }

    private static void ValidateWeaponRecipesCheckpoint(GameState state, JsonElement? currentVm)
    {
        state.WeaponRecipes?.Validate();
        if (state.WeaponRecipes is NativeWeaponRecipes recipes && state.Economy is NativeEconomy economy && recipes.InitOutSha256 != economy.InitOutSha256)
        {
            throw new InvalidDataException("Saved native weapon recipes and economy have different New Game provenance.");
        }
        if (currentVm is not JsonElement vm)
        {
            return;
        }
        if (!vm.TryGetProperty("WeaponRecipes", out JsonElement captured))
        {
            throw new InvalidDataException("Saved VM lacks its native weapon recipe owner; recreate authentic history.");
        }
        NativeWeaponRecipes? restored = captured.ValueKind == JsonValueKind.Null ? null : captured.Deserialize<NativeWeaponRecipes>(JsonOptions);
        restored?.Validate();
        if (state.Scene != SceneKind.WorldMap && !JsonElement.DeepEquals(captured, JsonSerializer.SerializeToElement(state.WeaponRecipes, JsonOptions)))
        {
            throw new InvalidDataException("Root and VM native weapon recipe owners disagree.");
        }
    }
}
