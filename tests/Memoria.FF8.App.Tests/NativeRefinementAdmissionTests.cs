using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeRefinementAdmissionTests
{
    public static IEnumerable<object[]> Abilities => Enumerable.Range(97, 19).Select(ability => new object[] { ability });

    [Theory]
    [MemberData(nameof(Abilities))]
    public void AllPossibleUsesMenuAdmissionWithoutRequiringAFieldMovementActor(int abilityId)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeRefinementDefinitions definitions = assets.ReadNativeRefinementDefinitions();
        NativeRefinementAbility ability = definitions.Abilities[abilityId - 92];
        NativeRefinementRecipe recipe = definitions.Recipe(abilityId, ability.First);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        vitals.GuardianForces[0] = vitals.GuardianForces[0] with { Flags = 1, LearnedBank = new(0, 1UL << (abilityId - 64)) };
        NativeEconomy economy = assets.ReadNewGameEconomy();
        Array.Fill(economy.Items, new NativeItemStock(0, 0));
        NativeCardInventory cards = NativeCardInventory.CreateNewGame();
        if (ability.Kind == 2)
        {
            vitals = vitals.ChangeMagicStock(0, recipe.Ingredient, 100, MagicOperationReason.ScriptReward, out _);
        }
        else if (ability.Kind == 4)
        {
            for (int copy = 0; copy < recipe.IngredientCount; copy++)
            {
                cards = cards.Acquire(recipe.Ingredient, out _);
            }
        }
        else
        {
            economy.Items[0] = new(recipe.Ingredient, 100);
        }
        GameSession session = new(new GameState { Scene = SceneKind.Field, FieldId = 114, CharacterVitals = vitals, Economy = economy, CardInventory = cards, ShotAvailability = assets.ReadNewGameShotAvailability(), SalaryPresentation = NativeSalaryPresentation.FromColdProcess(), WorldControls = NativeWorldControlState.CreateNewGame() });
        EventEngine engine = new(session) { ResolveNativeRefinementDefinitions = () => definitions };
        FieldAssets field = assets.LoadField(114);
        // Resource-backed menu component; no field scripts, movement or gameplay are executed.
        engine.LoadField(field.Id, field.Jsm, field.Messages);
        Assert.Null(engine.PlayerActor);
        Assert.False(engine.CanAcceptMovementInput);
        Assert.True(engine.CanOpenFieldMenu);
        LiveMenuCatalog names = LiveMenuCatalog.ReadInstalled(assets);
        MenuController menu = new(() =>
        {
            MenuData data = LiveMenuData.Build(session.State, catalog: names);
            NativeRefinementMenuProjection.Augment(data, session.State, definitions, names);
            return data;
        }, action =>
        {
            PlayerActionResult result = engine.Apply(NativeRefinementMenuProjection.PlayerAction(action));
            Assert.True(result.Accepted, result.Detail);
            Assert.Equal(0, result.FramesAdvanced);
        });
        menu.Open("ability-refine");
        menu.Execute(new MenuAction("select-recipe", $"native-refine-{abilityId}-{ability.First}"));
        int maximum = definitions.MaximumBatches(vitals, economy, cards, abilityId, ability.First, 0);
        MenuEntry all = menu.Entries.Single(entry => entry.Id == maximum.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.True(all.Enabled);
        Assert.Equal(maximum.ToString(System.Globalization.CultureInfo.InvariantCulture), all.Action!.Value);
        string before = JsonSerializer.Serialize(session.State);
        typeof(SceneEventState).GetProperty(nameof(SceneEventState.MenuEnabled))!.SetValue(engine.Scene, false);
        Assert.False(engine.CanOpenFieldMenu);
        PlayerActionResult blocked = engine.Apply(NativeRefinementMenuProjection.PlayerAction(all.Action));
        Assert.False(blocked.Accepted);
        Assert.NotNull(blocked.Detail);
        Assert.Equal(before, JsonSerializer.Serialize(session.State));
        typeof(SceneEventState).GetProperty(nameof(SceneEventState.MenuEnabled))!.SetValue(engine.Scene, true);
        menu.Execute(all.Action);
        Assert.Equal(0, definitions.MaximumBatches(session.CharacterVitals!, session.Economy!, session.CardInventory, abilityId, ability.First, 0));
        Assert.Equal(0, engine.Frame);
    }
}
