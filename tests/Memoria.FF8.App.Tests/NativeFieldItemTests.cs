using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class NativeFieldItemTests
{
    [Fact]
    public void InstalledPotionUsesOriginalPowerAndDoesNotConsumeWhenIneffective()
    {
        using GameAssets assets = Assets();
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        NativeFieldItem potion = catalog.FieldItem(1)!;
        Assert.True(potion.SupportsCharacterUse, JsonSerializer.Serialize(potion));
        Assert.Equal(0, potion.Type);
        Assert.Equal(4, potion.Power);
        Assert.Equal(0, potion.StatusMask);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals().WithSavedHitPoints(0, 1);
        NativeEconomy economy = assets.ReadNewGameEconomy().AddItem(1, 1, out _);
        int quantity = economy.Items.Single(stock => stock.ItemId == 1).Count;
        var healed = potion.Use(vitals, economy, 1, 0);
        Assert.True(healed.Changed);
        Assert.Equal(201, healed.Vitals.Records[0].HitPoints.Minimum);
        Assert.Equal(quantity - 1, healed.Economy.Items.Where(stock => stock.ItemId == 1).Sum(stock => stock.Count));
        Assert.Equal(1, vitals.Records[0].HitPoints.Minimum);
        Assert.Equal(quantity, economy.Items.Single(stock => stock.ItemId == 1).Count);
        int maximum = vitals.Definitions.CharacterMaximum(vitals.Records[0]);
        var full = potion.Use(vitals.WithSavedHitPoints(0, maximum), economy, 1, 0);
        Assert.False(full.Changed);
        Assert.Same(economy, full.Economy);
    }

    [Fact]
    public void InstalledRevivalAndStatusItemsKeepNativeEligibilityAndMasks()
    {
        using GameAssets assets = Assets();
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals().WithSavedHitPoints(0, 0);
        vitals.Records[0] = vitals.Records[0] with { Status = 1 };
        NativeEconomy economy = assets.ReadNewGameEconomy().AddItem(7, 1, out _).AddItem(11, 1, out _);
        NativeFieldItem phoenix = catalog.FieldItem(7)!;
        Assert.True(phoenix.SupportsCharacterUse, JsonSerializer.Serialize(phoenix));
        int maximum = vitals.Definitions.CharacterMaximum(vitals.Records[0]);
        var revived = phoenix.Use(vitals, economy, 7, 0);
        Assert.Equal(maximum / 8, revived.Vitals.Records[0].HitPoints.Minimum);
        Assert.Equal(0, revived.Vitals.Records[0].Status & 1);
        Assert.False(phoenix.Use(revived.Vitals, economy, 7, 0).Changed);
        NativeFieldItem antidote = catalog.FieldItem(11)!;
        NativeCharacterVitals poisoned = revived.Vitals.DeepCopy();
        poisoned.Records[0] = poisoned.Records[0] with { Status = 0xC2 };
        var cured = antidote.Use(poisoned, economy, 11, 0);
        Assert.True(cured.Changed);
        Assert.Equal(0, cured.Vitals.Records[0].Status & 2);
        Assert.Equal(0xC0, cured.Vitals.Records[0].Status & 0xC0);
    }

    [Fact]
    public void ZombieKoUncertainHpAndUnsupportedTypesCannotSpendPotion()
    {
        using GameAssets assets = Assets();
        NativeFieldItem potion = LiveMenuCatalog.ReadInstalled(assets).FieldItem(1)!;
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals().WithSavedHitPoints(0, 1);
        NativeEconomy economy = assets.ReadNewGameEconomy().AddItem(1, 1, out _);
        vitals.Records[0] = vitals.Records[0] with { Status = 0x40 };
        Assert.False(potion.Use(vitals, economy, 1, 0).Changed);
        vitals.Records[0] = vitals.Records[0] with { HitPoints = new(0, 0), Status = 1 };
        Assert.False(potion.Use(vitals, economy, 1, 0).Changed);
        vitals.Records[0] = vitals.Records[0] with { HitPoints = new(1, 2), Status = 0 };
        Assert.Throws<NotSupportedException>(() => potion.Use(vitals, economy, 1, 0));
        Assert.Throws<NotSupportedException>(() => (potion with { Type = 6 }).Use(vitals, economy, 1, 0));
    }

    [Fact]
    public void OriginalGuardianRecoveryAndRevivalConsumeOnlyEffectiveItems()
    {
        using GameAssets assets = Assets();
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        int healingId = Enumerable.Range(1, 197).First(id => catalog.FieldItem(id)?.Type == 3);
        int revivalId = Enumerable.Range(1, 197).First(id => catalog.FieldItem(id)?.Type == 4);
        NativeFieldItem heal = catalog.FieldItem(healingId)!;
        NativeFieldItem revive = catalog.FieldItem(revivalId)!;
        Assert.True(heal.SupportsGuardianForceUse, JsonSerializer.Serialize(heal));
        Assert.True(revive.SupportsGuardianForceUse, JsonSerializer.Serialize(revive));
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals().AcquireGuardianForce(0);
        vitals.GuardianForces[0] = vitals.GuardianForces[0] with { HitPoints = 1 };
        NativeEconomy economy = assets.ReadNewGameEconomy().AddItem(healingId, 1, out _).AddItem(revivalId, 1, out _);
        var healed = heal.Use(vitals, economy, healingId, 16);
        Assert.True(healed.Changed);
        Assert.Equal(Math.Min(vitals.Definitions.GuardianForceMaximum(0, vitals.GuardianForces[0]), 1 + heal.Power * 50), healed.Vitals.GuardianForces[0].HitPoints);
        vitals.GuardianForces[0] = vitals.GuardianForces[0] with { HitPoints = 0 };
        Assert.False(heal.Use(vitals, economy, healingId, 16).Changed);
        var revived = revive.Use(vitals, economy, revivalId, 16);
        Assert.True(revived.Changed);
        Assert.Equal(vitals.Definitions.GuardianForceMaximum(0, vitals.GuardianForces[0]) / 8, revived.Vitals.GuardianForces[0].HitPoints);
        Assert.Throws<NotSupportedException>(() => heal.Use(vitals, economy, healingId, 0));
    }

    [Fact]
    public void NativeAllTargetItemHealsEligibleMembersWithOnlyOneDebit()
    {
        using GameAssets assets = Assets();
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals().WithMemberFlags(1, true).WithSavedHitPoints(0, 1).WithSavedHitPoints(1, 1);
        NativeEconomy economy = assets.ReadNewGameEconomy().AddItem(6, 1, out _);
        var healed = catalog.FieldItem(6)!.Use(vitals, economy, 6, 0);
        Assert.True(healed.Vitals.Records[0].HitPoints.Minimum > 1);
        Assert.True(healed.Vitals.Records[1].HitPoints.Minimum > 1);
        Assert.DoesNotContain(healed.Economy.Items, stock => stock.ItemId == 6);
        NativeCharacterVitals dream = vitals with { DreamEnabled = true, CombatParty = [0, 255, 255] };
        var dreamHeal = catalog.FieldItem(6)!.Use(dream, economy, 6, 0);
        Assert.Equal(1, dreamHeal.Vitals.Records[1].HitPoints.Minimum);
        Assert.Throws<InvalidOperationException>(() => catalog.FieldItem(1)!.Use(dream, economy.AddItem(1, 1, out _), 1, 1));
    }

    [Fact]
    public void NativeTeachingAndCompatibilityUseExistingSavedOwners()
    {
        using GameAssets assets = Assets();
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals().AcquireGuardianForce(0).AcquireGuardianForce(1);
        NativeEconomy economy = assets.ReadNewGameEconomy().AddItem(42, 2, out _);
        NativeFieldItem scroll = catalog.FieldItem(42)!;
        Assert.Equal(17, scroll.Type);
        vitals.GuardianForces[0] = vitals.GuardianForces[0] with { Learning = scroll.Power };
        var taught = scroll.Use(vitals, economy, 42, 16);
        Assert.True(taught.Changed);
        Assert.True(taught.Vitals.GuardianForces[0].LearnedBank.Has(scroll.Power));
        HashSet<int> available = taught.Vitals.GuardianForceAbilities(0).Where(ability => !ability.Learned).Select(ability => ability.Ability).ToHashSet();
        int next = taught.Vitals.ResolveGuardianForceTemplates()[0].OrderBy(ability => ability.LearningPriority).First(ability => available.Contains(ability.Ability)).Ability;
        Assert.Equal(next, taught.Vitals.GuardianForces[0].Learning);
        Assert.DoesNotContain(taught.Vitals.GuardianForceAbilities(0), ability => ability.Ability == taught.Vitals.GuardianForces[0].Learning && ability.Learned);
        Assert.Equal(vitals.GuardianForces[0].AbilityProgress, taught.Vitals.GuardianForces[0].AbilityProgress);
        Assert.False(scroll.Use(taught.Vitals, taught.Economy, 42, 16).Changed);
        int compatibilityId = Enumerable.Range(1, 197).First(id => catalog.FieldItem(id) is { Type: 16, Power: 0 });
        NativeFieldItem compatibilityItem = catalog.FieldItem(compatibilityId)!;
        ushort[] values = Enumerable.Repeat((ushort)2000, 16).ToArray();
        vitals.Records[0] = vitals.Records[0] with { GuardianForceCompatibility = values };
        var compatible = compatibilityItem.Use(vitals, economy.AddItem(compatibilityId, 1, out _), compatibilityId, 0);
        Assert.True(compatible.Changed);
        Assert.Equal(2000 - compatibilityItem.StatusMask, compatible.Vitals.Records[0].GuardianForceCompatibility![0]);
        Assert.Equal(2000 + compatibilityItem.StatusMask / 2, compatible.Vitals.Records[0].GuardianForceCompatibility![1]);
        Assert.Equal(2000, vitals.Records[0].GuardianForceCompatibility![0]);
    }

    [Fact]
    public void LiveMenuPassesSelectedItemAndPhysicalTargetWithoutEditingProjection()
    {
        using GameAssets assets = Assets();
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        GameState state = new() { Scene = SceneKind.Field, CharacterVitals = assets.ReadNewGameCharacterVitals(), Economy = assets.ReadNewGameEconomy().AddItem(1, 1, out _) };
        List<MenuAction> requests = [];
        MenuController controller = new(() => LiveMenuData.Build(state, catalog: catalog), requests.Add);
        controller.Open("item-use", false);
        controller.Navigate(MenuInput.Confirm);
        controller.Navigate(MenuInput.Confirm);
        MenuAction action = Assert.Single(requests);
        Assert.Equal("item-use", action.Kind);
        Assert.Equal("item-1", action.SubjectId);
        Assert.Equal("character-0", action.TargetId);
        Assert.Equal(state.Economy.Items.Single(stock => stock.ItemId == 1).Count, controller.Data.Items.Single(item => item.Id == "item-1").Quantity);
    }

    [Fact]
    public void StatItemsUseUnjunctionedNativeCapsAndPreserveEquipmentAndHp()
    {
        using GameAssets assets = Assets();
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals().WithSavedHitPoints(0, 100);
        NativeEconomy economy = assets.ReadNewGameEconomy().AddItem(169, 1, out _).AddItem(170, 1, out _);
        var hp = catalog.FieldItem(169)!.Use(vitals, economy, 169, 0, assets.ReadFieldItemStatRules(vitals, 0));
        Assert.True(hp.Changed);
        Assert.Equal(vitals.Records[0].HitPointBonus + 10, hp.Vitals.Records[0].HitPointBonus);
        Assert.Equal(100, hp.Vitals.Records[0].HitPoints.Minimum);
        Assert.Equal(vitals.Records[0].Weapon, hp.Vitals.Records[0].Weapon);
        Assert.Equal(vitals.Records[0].Junction, hp.Vitals.Records[0].Junction);
        var strength = catalog.FieldItem(170)!.Use(vitals, economy, 170, 0, assets.ReadFieldItemStatRules(vitals, 0));
        Assert.Equal(vitals.Records[0].StatBonuses[0] + 1, strength.Vitals.Records[0].StatBonuses[0]);
        NativeCharacterVitals capped = vitals.DeepCopy();
        capped.Records[0].StatBonuses[0] = 255;
        var exhausted = catalog.FieldItem(170)!.Use(capped, economy, 170, 0, assets.ReadFieldItemStatRules(capped, 0));
        Assert.False(exhausted.Changed);
        Assert.Same(economy, exhausted.Economy);
        NativeCharacterVitals restored = JsonSerializer.Deserialize<NativeCharacterVitals>(JsonSerializer.Serialize(strength.Vitals))!;
        restored.Validate();
        Assert.Equal(strength.Vitals.Records[0].StatBonuses[0], restored.Records[0].StatBonuses[0]);
    }

    [Fact]
    public void BlueMagicUsesQuistisAndPublishesLearnedBitWithOneDebit()
    {
        using GameAssets assets = Assets();
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        NativeFieldItem web = catalog.FieldItem(127)!;
        Assert.True(web.SupportsBlueMagicUse);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals().WithMemberFlags(3, true);
        NativeEconomy economy = assets.ReadNewGameEconomy().AddItem(127, 2, out _);
        NativeLimitProgress progress = assets.ReadNewGameLimitProgress();
        Assert.False(LiveMenuData.Build(new GameState { Scene = SceneKind.Field, CharacterVitals = vitals, Economy = economy }, catalog: catalog).Items.Single(item => item.Id == "item-127").NativeFieldUseSupported);
        var learned = web.LearnBlueMagic(vitals, economy, progress, 127, 3);
        Assert.True(learned.Changed);
        Assert.Equal((ushort)(progress.BlueMagicFlags | 2), learned.Progress.BlueMagicFlags);
        Assert.Equal(1, learned.Economy.Items.Single(stock => stock.ItemId == 127).Count);
        Assert.False(web.LearnBlueMagic(vitals, learned.Economy, learned.Progress, 127, 3).Changed);
        Assert.Throws<InvalidOperationException>(() => web.LearnBlueMagic(vitals, economy, progress, 127, 0));
        Assert.Throws<InvalidOperationException>(() => web.LearnBlueMagic(vitals with { DreamEnabled = true }, economy, progress, 127, 3));
        Assert.Equal(progress.BlueMagicFlags, JsonSerializer.Deserialize<NativeLimitProgress>(JsonSerializer.Serialize(progress))!.BlueMagicFlags);
    }

    [Fact]
    public void AmnesiaClearsAbilityLearningAndUnsupportedJunctionsThenScrollRestoresRule()
    {
        using GameAssets assets = Assets();
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals().AcquireGuardianForce(0);
        NativeEconomy economy = assets.ReadNewGameEconomy().AddItem(42, 2, out _).AddItem(41, 1, out _);
        var taught = catalog.FieldItem(42)!.Use(vitals, economy, 42, 16);
        NativeCharacterVitals junctioned = taught.Vitals.DeepCopy();
        byte[] magic = new byte[20];
        magic[0] = 1;
        junctioned.Records[0] = junctioned.Records[0] with { Junction = junctioned.Records[0].Junction with { GuardianForces = 1, MagicJunctions = magic } };
        junctioned.Records[0].MagicInventory[0] = new NativeMagicStock(1, 100);
        var forgotten = catalog.FieldItem(41)!.Use(junctioned, taught.Economy, 41, 16, forgottenAbility: 1);
        Assert.True(forgotten.Changed);
        Assert.False(forgotten.Vitals.GuardianForces[0].LearnedBank.Has(1));
        Assert.Equal(0, forgotten.Vitals.Records[0].Junction.MagicJunctions[0]);
        Assert.DoesNotContain(forgotten.Vitals.GuardianForceAbilities(0), entry => entry.Ability == 1);
        var restored = catalog.FieldItem(42)!.Use(forgotten.Vitals, forgotten.Economy, 42, 16);
        Assert.True(restored.Vitals.GuardianForces[0].LearnedBank.Has(1));
        Assert.Contains(restored.Vitals.GuardianForceAbilities(0), entry => entry.Ability == 1 && entry.Learned);
        Assert.True(junctioned.GuardianForces[0].LearnedBank.Has(1));
    }

    [Fact]
    public void RestItemsRespectSaveContextAndNativeCharacterGuardianTargetFlags()
    {
        using GameAssets assets = Assets();
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals().WithSavedHitPoints(0, 0).AcquireGuardianForce(0);
        vitals.Records[0] = vitals.Records[0] with { Status = 0x81 };
        vitals.GuardianForces[0] = vitals.GuardianForces[0] with { HitPoints = 0 };
        NativeEconomy economy = assets.ReadNewGameEconomy().AddItem(33, 1, out _).AddItem(34, 1, out _).AddItem(35, 1, out _);
        Assert.Throws<InvalidOperationException>(() => catalog.FieldItem(33)!.Use(vitals, economy, 33, 0, context: new(false, 0)));
        var tent = catalog.FieldItem(33)!.Use(vitals, economy, 33, 0, context: new(true, 0));
        Assert.True(tent.Changed);
        Assert.Equal(vitals.Definitions.CharacterMaximum(vitals.Records[0]), tent.Vitals.Records[0].HitPoints.Minimum);
        Assert.Equal(0x80, tent.Vitals.Records[0].Status);
        Assert.Equal(0, tent.Vitals.GuardianForces[0].HitPoints);
        var pet = catalog.FieldItem(34)!.Use(vitals, economy, 34, 0, context: new(true, 0));
        Assert.True(pet.Vitals.GuardianForces[0].HitPoints > 0);
        Assert.Equal(0, pet.Vitals.Records[0].HitPoints.Minimum);
        var cottage = catalog.FieldItem(35)!.Use(vitals, economy, 35, 0, context: new(true, 0));
        Assert.True(cottage.Vitals.Records[0].HitPoints.Minimum > 0);
        Assert.True(cottage.Vitals.GuardianForces[0].HitPoints > 0);
        Assert.False(catalog.FieldItem(35)!.Use(vitals, economy, 35, 0, context: new(true, 0x40)).Changed);
        Assert.Throws<InvalidOperationException>(() => catalog.FieldItem(7)!.Use(vitals, economy.AddItem(7, 1, out _), 7, 0, context: new(true, 0x40)));
    }

    [Fact]
    public void ExhaustingSelectedItemReturnsToInventoryWithoutSelectingAnotherItem()
    {
        MenuData data = new();
        data.Items.Add(new MenuItem { Id = "item-1", Name = "Potion", Quantity = 1, NativeFieldUseSupported = true });
        data.Items.Add(new MenuItem { Id = "item-2", Name = "Potion+", Quantity = 1, NativeFieldUseSupported = true });
        data.Characters.Add(new MenuCharacter { Id = "character-0", Name = "Squall", NativeFieldItemTargetAllowed = true });
        data.SupportedActions.Add("item-use");
        MenuController controller = new(() => data, action => data.Items.RemoveAll(item => item.Id == action.SubjectId));
        controller.Open("item-use", false);
        controller.Navigate(MenuInput.Confirm);
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal("item-use", controller.ScreenId);
        Assert.Equal("item-2", Assert.Single(data.Items).Id);
    }

    private static GameAssets Assets()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed native initialization resources are required.");
        return new GameAssets(directory);
    }
}
