using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class MenuRecoveryTargetTests
{
    [Theory]
    [InlineData("item-target")]
    [InlineData("magic-target")]
    public void CursorPrefersHighestEnabledScorePreservesManualChoiceAndReevaluatesOnReopen(string screen)
    {
        Dictionary<string, int> scores = new() { ["character-0"] = 6, ["character-1"] = 15, ["character-2"] = 1000, ["character-3"] = 15 };
        MenuData data = new();
        data.Items.Add(new MenuItem { Id = "recovery", RecoveryTargetScores = scores });
        data.Magic.Add(new MenuMagic { Id = "recovery", CharacterId = "character-0", RecoveryTargetScores = scores });
        data.Characters.Add(new MenuCharacter { Id = "character-0", InParty = true });
        MenuController menu = new(() => data);
        menu.Register(new MenuScreenDefinition
        {
            Id = screen,
            Title = "Recovery targets",
            Help = "Select a target.",
            Entries = _ => Enumerable.Range(0, 4).Select(row => new MenuEntry($"character-{row}", $"Target {row}", "", Action: new MenuAction("target", TargetId: $"character-{row}"), Enabled: row != 2)).ToArray()
        });
        menu.Open(screen);

        Assert.Equal("character-1", menu.SelectedEntry?.Id);
        menu.SelectEntry(screen, "character-0");
        menu.InvalidateLiveData();
        Assert.Equal("character-0", menu.SelectedEntry?.Id);
        scores["character-3"] = 20;
        menu.Open(screen);
        Assert.Equal("character-3", menu.SelectedEntry?.Id);
        scores.Clear();
        menu.Open(screen);
        Assert.Equal("character-0", menu.SelectedEntry?.Id);
    }

    [Theory]
    [InlineData("Phoenix Down", 2)]
    [InlineData("Antidote", 1)]
    public void InstalledItemProjectionSelectsAnActualCurableCondition(string name, int expectedRow)
    {
        using GameAssets assets = InstalledAssets();
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        int itemId = Enumerable.Range(1, 255).Single(id => catalog.ItemName(id) == name);
        NativeCharacterVitals vitals = Targets(assets);
        NativeEconomy economy = assets.ReadNewGameEconomy().AddItem(itemId, 1, out _);
        GameState state = new() { Scene = SceneKind.Field, CharacterVitals = vitals, Economy = economy };
        string before = JsonSerializer.Serialize(state);
        MenuController menu = new(() => LiveMenuData.Build(state, catalog: catalog));
        menu.Open("item-use");
        menu.Execute(new MenuAction("select-item", $"item-{itemId}"));
        menu.Push("item-target");

        Assert.Equal($"character-{expectedRow}", menu.SelectedEntry?.Id);
        Assert.True(catalog.FieldItem(itemId)!.Use(vitals, economy, itemId, expectedRow).Changed);
        Assert.Equal(before, JsonSerializer.Serialize(state));
    }

    [Fact]
    public void InstalledEsunaProjectionRanksActualRemovableStatusesWithoutMutatingTheSave()
    {
        using GameAssets assets = InstalledAssets();
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        int esuna = Enumerable.Range(1, 56).Single(id => catalog.MagicName(id) == "Esuna");
        NativeCharacterVitals vitals = Targets(assets).ChangeMagicStock(0, esuna, 10, MagicOperationReason.ScriptReward, out _);
        GameState state = new() { Scene = SceneKind.Field, CharacterVitals = vitals };
        string before = JsonSerializer.Serialize(state);
        MenuController menu = new(() => LiveMenuData.Build(state, catalog: catalog));
        menu.Open("magic-use");
        menu.Execute(new MenuAction("select-magic", $"magic-0-{esuna}"));
        menu.Push("magic-target");

        Assert.Equal("character-3", menu.SelectedEntry?.Id);
        Assert.Equal(0, menu.Magic!.RecoveryTargetScores["character-2"]);
        vitals.CastFieldMagic(0, esuna, 3, catalog.ReadStatRules(vitals, 0), out bool used);
        Assert.True(used);
        Assert.Equal(before, JsonSerializer.Serialize(state));
    }

    [Fact]
    public void InstalledItemScoringHonorsNativeZombieBehaviorAndDoesNotTreatHealingAsStatusRecovery()
    {
        using GameAssets assets = InstalledAssets();
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        NativeCharacterVitals vitals = Targets(assets);
        NativeCharacterVital[] records = vitals.Records.ToArray();
        records[1] = records[1] with { Status = 0x42 };
        vitals = vitals with { Records = records };
        GameState state = new() { Scene = SceneKind.Field, CharacterVitals = vitals };
        int antidote = Enumerable.Range(1, 255).Single(id => catalog.ItemName(id) == "Antidote");
        int potion = Enumerable.Range(1, 255).Single(id => catalog.ItemName(id) == "Potion");

        NativeEconomy economy = assets.ReadNewGameEconomy().AddItem(antidote, 1, out _);
        bool changed = catalog.FieldItem(antidote)!.Use(vitals, economy, antidote, 1).Changed;
        Assert.Equal(changed, catalog.ItemRecoveryTargetScores(state, antidote)["character-1"] > 0);
        Assert.All(catalog.ItemRecoveryTargetScores(state, potion).Values, score => Assert.Equal(0, score));
    }

    [Theory]
    [InlineData("Life")]
    [InlineData("Full-life")]
    public void InstalledRevivalMagicSelectsKoBeforeLivingStatusTargets(string name)
    {
        using GameAssets assets = InstalledAssets();
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        int spell = Enumerable.Range(1, 56).Single(id => catalog.MagicName(id) == name);
        NativeCharacterVitals vitals = Targets(assets).ChangeMagicStock(0, spell, 10, MagicOperationReason.ScriptReward, out _);
        GameState state = new() { Scene = SceneKind.Field, CharacterVitals = vitals };
        string before = JsonSerializer.Serialize(state);
        MenuController menu = new(() => LiveMenuData.Build(state, catalog: catalog));
        menu.Open("magic-use");
        menu.Execute(new MenuAction("select-magic", $"magic-0-{spell}"));
        menu.Push("magic-target");

        Assert.Equal("character-2", menu.SelectedEntry?.Id);
        Assert.Equal(0, menu.Magic!.RecoveryTargetScores["character-1"]);
        NativeCharacterVitals revived = vitals.CastFieldMagic(0, spell, 2, catalog.ReadStatRules(vitals, 0), out bool used);
        Assert.True(used);
        Assert.True(revived.Records[2].HitPoints.Minimum > 0);
        Assert.Equal(0, revived.Records[2].Status & 1);
        Assert.Equal(before, JsonSerializer.Serialize(state));
    }

    [Theory]
    [InlineData((byte)1)]
    [InlineData((byte)4)]
    public void InstalledRevivalItemsDoNotScoreWhenSavedCommandRestrictionsForbidTheirUse(byte type)
    {
        using GameAssets assets = InstalledAssets();
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        int itemId = Enumerable.Range(1, 255).First(id => catalog.FieldItem(id) is { } item && item.Type == type && (item.SupportsCharacterUse || item.SupportsGuardianForceUse));
        NativeCharacterVitals vitals = Targets(assets).AcquireGuardianForce(0);
        NativeGuardianForceVital[] forces = vitals.GuardianForces.ToArray();
        forces[0] = forces[0] with { HitPoints = 0 };
        vitals = vitals with { GuardianForces = forces };
        NativeInputConfiguration input = assets.ReadNewGameInputConfiguration();
        GameState allowed = new() { Scene = SceneKind.Field, CharacterVitals = vitals, InputConfiguration = input with { Options = input.Options & ~0x40000000u } };
        GameState blocked = allowed with { InputConfiguration = input with { Options = input.Options | 0x40000000u } };
        NativeEconomy economy = assets.ReadNewGameEconomy().AddItem(itemId, 1, out _);

        Assert.Contains(catalog.ItemRecoveryTargetScores(allowed, itemId).Values, score => score > 0);
        Assert.All(catalog.ItemRecoveryTargetScores(blocked, itemId).Values, score => Assert.Equal(0, score));
        Assert.Throws<InvalidOperationException>(() => catalog.FieldItem(itemId)!.Use(vitals, economy, itemId, type is 4 or 5 ? 16 : 2, context: new NativeFieldItemContext(false, blocked.InputConfiguration!.CommandRestrictions)));
    }

    private static NativeCharacterVitals Targets(GameAssets assets)
    {
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals().WithMemberFlags(1, true).WithMemberFlags(2, true).WithMemberFlags(3, true);
        NativeCharacterVital[] records = vitals.Records.ToArray();
        records[0] = records[0] with { Status = 0 };
        records[1] = records[1] with { Status = 2 };
        records[2] = records[2] with { Status = 1, HitPoints = new(0, 0) };
        records[3] = records[3] with { Status = 4 };
        return vitals with { Records = records, CombatParty = [0, 1, 2] };
    }

    private static GameAssets InstalledAssets()
    {
        Assert.SkipUnless(Directory.Exists(TestResources.GameDirectory), "Installed native menu resources are required.");
        return new GameAssets(TestResources.GameDirectory);
    }
}
