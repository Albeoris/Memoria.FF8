using System.Collections.Immutable;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AbilityShopHostTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CalledShopTradesAndCommitsOnExit(bool world)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        var (session, engine) = CreateMenu(assets, world, 95);
        Assert.False(engine.ApplyAbilityShop(new(PlayerActionKind.OpenCallShop, 1), world));
        Assert.False(engine.ApplyAbilityShop(new(PlayerActionKind.OpenJunkShop), world));
        uint gil = session.Economy!.Gil;
        Assert.True(engine.ApplyAbilityShop(new(PlayerActionKind.OpenCallShop, 0), world));
        Assert.Equal(0, engine.CalledShop!.Catalog);
        Assert.Throws<InvalidOperationException>(() => engine.Capture());
        Assert.Throws<InvalidOperationException>(() => engine.CaptureWorldEventState());
        Assert.False(engine.Apply(new(PlayerActionKind.Move, Horizontal: 1)).Accepted);
        Assert.False(engine.ApplyAbilityShop(new(PlayerActionKind.OpenCallShop, 0), world));
        Assert.True(engine.ApplyAbilityShop(new(PlayerActionKind.BuyShopItem, 191, Quantity: 2), world));
        Assert.True(engine.ApplyAbilityShop(new(PlayerActionKind.SellShopItem, 191, Quantity: 1), world));
        Assert.Equal(gil, session.Economy.Gil);
        Assert.Equal(gil - 1800, engine.CalledShop.Gil);
        Assert.True(engine.ApplyAbilityShop(new(PlayerActionKind.CloseAbilityShop), world));
        Assert.False(engine.HasAbilityShop);
        Assert.Equal(gil - 1800, session.Economy.Gil);
        Assert.Contains(new NativeItemStock(191, 1), session.Economy.Items);
        Assert.True(engine.ApplyAbilityShop(new(PlayerActionKind.OpenCallShop, 0), world));
        Assert.Equal(1, engine.CalledShop!.Counts[191]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WorkshopCommitsTheFeeAndMaterialsForTheEquippedUpgrade(bool world)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        var (session, engine) = CreateMenu(assets, world, 96);
        NativeWeaponRecipe recipe = assets.ReadNativeWeaponShopDefinitions().Recipes[1];
        NativeEconomy inventory = session.Economy!.DeepCopy() with { Gil = 100000 };
        Array.Fill(inventory.Items, new NativeItemStock(0, 0));
        int slot = 0;
        foreach (NativeItemStock material in recipe.Materials.Where(material => material.ItemId != 0))
        {
            inventory.Items[slot++] = material;
        }
        session.SetEconomy(inventory);
        Assert.True(engine.ApplyAbilityShop(new(PlayerActionKind.OpenJunkShop), world));
        Assert.Null(engine.CalledWorkshop!.RawArgument);
        Assert.True(engine.ApplyAbilityShop(new(PlayerActionKind.SelectWeaponShopCharacter, 6), world));
        Assert.Empty(engine.CalledWorkshop.AvailableRecipes);
        Assert.True(engine.ApplyAbilityShop(new(PlayerActionKind.SelectWeaponShopCharacter, 0), world));
        uint price = engine.CalledWorkshop.Price(1);
        Assert.True(engine.ApplyAbilityShop(new(PlayerActionKind.RemodelWeapon, 1), world));
        Assert.Equal(1, session.CharacterVitals!.Records[0].Weapon);
        Assert.Equal(inventory.Gil, session.Economy.Gil);
        Assert.True(engine.ApplyAbilityShop(new(PlayerActionKind.CloseAbilityShop), world));
        Assert.Equal(inventory.Gil - price, session.Economy.Gil);
        Assert.All(recipe.Materials.Where(material => material.ItemId != 0), material => Assert.DoesNotContain(session.Economy.Items, item => item.ItemId == material.ItemId && item.Count > 0));
    }

    private static (GameSession Session, EventEngine Engine) CreateMenu(GameAssets assets, bool world, int ability)
    {
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        vitals.GuardianForces[0] = vitals.GuardianForces[0] with { Flags = 1, LearnedBank = new(0, 1UL << (ability - 64)) };
        vitals.Records[6] = vitals.Records[6] with { MemberFlags = (ushort)(vitals.Records[6].MemberFlags | 1) };
        NativeShopHistory history = assets.ReadNewGameShopHistory();
        history = history with { Bytes = new byte[400].ToImmutableArray().SetItem(16, 1) };
        GameSession session = new(new GameState
        {
            Scene = world ? SceneKind.WorldMap : SceneKind.Field,
            FieldId = 114,
            WorldArrival = world ? new WorldTransition(0, 1, 0) : null,
            CharacterVitals = vitals,
            Economy = assets.ReadNewGameEconomy(),
            ShopHistory = history,
            MenuRandom = NativeMenuRandomState.FromColdProcess(),
            InputConfiguration = assets.ReadNewGameInputConfiguration(),
            WeaponRecipes = assets.ReadNewGameWeaponRecipes(),
            ShotAvailability = assets.ReadNewGameShotAvailability(),
            SalaryPresentation = NativeSalaryPresentation.FromColdProcess(),
            WorldControls = NativeWorldControlState.CreateNewGame()
        });
        EventEngine engine = new(session) { ResolveNativeShopDefinitions = assets.ReadNativeShopDefinitions, ResolveNativeWeaponShopDefinitions = assets.ReadNativeWeaponShopDefinitions };
        if (!world)
        {
            FieldAssets field = assets.LoadField(114);
            engine.LoadField(field.Id, field.Jsm, field.Messages);
            Assert.True(engine.CanOpenFieldMenu);
        }
        return (session, engine);
    }
}
