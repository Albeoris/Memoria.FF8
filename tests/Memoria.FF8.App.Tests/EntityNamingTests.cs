using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class EntityNamingTests
{
    [Fact]
    public void Solomon_ring_acquisition_opens_required_default_name_only_for_the_new_gf()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using GameWorld world = new(session, LaunchOptions.DefaultGameDirectory);
        world.EnsureField();
        using GameAssets assets = new(LaunchOptions.DefaultGameDirectory);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        int itemId = Enumerable.Range(1, 197).Single(id => catalog.FieldItem(id)?.SupportsInventoryUse == true);
        NativeFieldItem ring = catalog.FieldItem(itemId)!;
        NativeEconomy economy = session.State.Economy!.AddItem(itemId, 1, out _);
        foreach (NativeItemStock ingredient in ring.SolomonRingDefinition!.Ingredients)
        {
            economy = economy.AddItem(ingredient.ItemId, ingredient.Count, out _);
        }
        // Declared component recipe inventory; no gameplay continuation or source save is changed.
        var next = ring.Use(session.State.CharacterVitals!, economy, itemId, 0);
        Assert.True(next.Changed);
        session.SetCharacterVitals(next.Vitals);
        session.SetEconomy(next.Economy);
        world.Events.Scene.Names[GameNames.GuardianForceSubject(11)] = "Old custom name";
        world.QueueItemGuardianForceNames(itemId, 0);
        Assert.Equal(GameNames.GuardianForceSubject(11), world.PendingEntityName!.Subject);
        Assert.Equal("Doomtrain", world.PendingEntityName.DefaultName);
        Assert.Equal($"item:{itemId}", world.PendingEntityName.Source);
        Assert.Equal("Doomtrain", world.Events.Scene.Names[GameNames.GuardianForceSubject(11)]);
        Assert.Single(session.State.PendingEntityNames!);
        world.QueueItemGuardianForceNames(itemId, 1 << 11);
        Assert.Single(session.State.PendingEntityNames!);
    }

    [Theory]
    [InlineData("item:0", 2)]
    [InlineData("item:256", 2)]
    [InlineData("item:001", 2)]
    [InlineData("item:67", 0)]
    [InlineData("battle:missing-result", 16)]
    [InlineData("battle:", 16)]
    [InlineData("debug:67", 2)]
    public void Saved_required_names_reject_unowned_or_malformed_sources(string source, int subject)
    {
        using GameAssets assets = new(LaunchOptions.DefaultGameDirectory);
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        GameState before = session.State with { CharacterVitals = assets.ReadNewGameCharacterVitals().AcquireGuardianForce(0) };
        Assert.Throws<InvalidDataException>(() => new GameSession(before with { PendingEntityNames = [new PendingEntityName(subject, "Valid", source)] }));
    }

    [Theory]
    [InlineData(11, 16, 16)]
    [InlineData(12, 0, 2)]
    [InlineData(13, 0, GameNames.BokoSubject)]
    public void Installed_naming_items_consume_before_editor_and_preserve_saved_pending_draft(byte type, int target, int subject)
    {
        using GameAssets assets = new(LaunchOptions.DefaultGameDirectory);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        int itemId = Enumerable.Range(1, 197).Single(id => catalog.FieldItem(id)?.Type == type);
        NativeFieldItem item = catalog.FieldItem(itemId)!;
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals().AcquireGuardianForce(0);
        NativeEconomy economy = assets.ReadNewGameEconomy().AddItem(itemId, 2, out _);
        int quantity = economy.Items.Where(stock => stock.ItemId == itemId).Sum(stock => stock.Count);
        (NativeEconomy consumed, int actualSubject) = item.BeginNaming(vitals, economy, itemId, target);
        Assert.Equal(subject, actualSubject);
        Assert.Equal(quantity - 1, consumed.Items.Where(stock => stock.ItemId == itemId).Sum(stock => stock.Count));
        Assert.Equal(quantity, economy.Items.Where(stock => stock.ItemId == itemId).Sum(stock => stock.Count));
        GameSession cold = GameSession.CreateColdProcess();
        cold.StartNewGame();
        GameSession session = new(cold.State with { CharacterVitals = vitals, Economy = economy });
        session.BeginItemEntityName(consumed, new PendingEntityName(subject, "Existing {Page}", $"item:{itemId}"));
        session.SetEntityNameDraft("👩🏽‍🚀 e\u0301 {Page}");
        GameState saved = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!;
        GameSession restored = new(saved);
        Assert.Equal(session.State.PendingEntityNames, restored.State.PendingEntityNames);
        Assert.Equal(quantity - 1, restored.State.Economy!.Items.Where(stock => stock.ItemId == itemId).Sum(stock => stock.Count));
        Assert.Equal("👩🏽‍🚀 e\u0301 {Page}", restored.State.PendingEntityNames![0].Draft);
        restored.CompleteEntityName(restored.State.PendingEntityNames[0]);
        Assert.Null(restored.State.PendingEntityNames);
        Assert.Equal(quantity - 1, restored.State.Economy.Items.Where(stock => stock.ItemId == itemId).Sum(stock => stock.Count));
    }

    [Fact]
    public void Naming_item_refuses_unacquired_gf_or_wrong_target_without_inventory_change()
    {
        using GameAssets assets = new(LaunchOptions.DefaultGameDirectory);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        int itemId = Enumerable.Range(1, 197).Single(id => catalog.FieldItem(id)?.Type == 11);
        NativeFieldItem item = catalog.FieldItem(itemId)!;
        NativeEconomy economy = assets.ReadNewGameEconomy().AddItem(itemId, 1, out _);
        string before = JsonSerializer.Serialize(economy);
        Assert.Throws<InvalidOperationException>(() => item.BeginNaming(vitals, economy, itemId, 16));
        Assert.Throws<InvalidOperationException>(() => item.BeginNaming(vitals.AcquireGuardianForce(0), economy, itemId, 0));
        Assert.Equal(before, JsonSerializer.Serialize(economy));
    }

    [Fact]
    public void Required_entity_queue_restores_order_blocks_gameplay_and_completes_without_frames()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using GameWorld world = new(session, LaunchOptions.DefaultGameDirectory);
        world.EnsureField();
        session.EnqueueEntityNames([new PendingEntityName(2, "Angelo", "item:66"), new PendingEntityName(GameNames.BokoSubject, "Boko", "item:67")]);
        long frame = world.Events.Frame;
        Assert.False(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        string name = string.Concat(Enumerable.Repeat("👩🏽‍🚀", 14)) + "{Page}";
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.EnterName, Text: name)).Accepted);
        Assert.Equal(name, world.Events.Scene.Names[2]);
        Assert.Equal(GameNames.BokoSubject, world.PendingEntityName!.Subject);
        Assert.Equal(frame, world.Events.Frame);
        GameState saved = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!;
        GameSession restored = new(saved);
        using GameWorld loaded = new(restored, LaunchOptions.DefaultGameDirectory);
        loaded.EnsureField();
        loaded.CompleteEntityName("{Color Red}");
        Assert.Null(loaded.PendingEntityName);
        Assert.Equal("{Color Red}", loaded.Events.Scene.Names[GameNames.BokoSubject]);
        Assert.Equal(frame, loaded.Events.Frame);
    }
}
