using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeMenuBattleHostTests
{
    [Fact]
    public void InstalledLampEntryRestoresAndDiagnosticVictoryReturnsWithOneItemSpent()
    {
        string game = TestResources.GameDirectory;
        GameSession session = new(GameSession.Load(TestResources.FixturePath("menu-battle-field-entry")));
        using GameWorld world = new(session, game);
        world.EnsureField();
        int fieldId = session.State.FieldId;
        int count = LampCount(session);
        Assert.True(count > 0, "The external fixture must own a Magical Lamp.");
        using GameAssets assets = new(game);
        NativeFieldItem lamp = LiveMenuCatalog.ReadInstalled(assets).FieldItem(168)!;
        Assert.Equal(new NativeFieldItem(14, 1, 43, 3), lamp);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.UseFieldItem, 168, Quantity: 0)).Accepted);
        Assert.Equal(count - 1, LampCount(session));
        Assert.Equal(1, session.State.CharacterVitals!.GuardianForces[5].Flags & 1);
        Assert.Equal((ushort)811, world.Events.PendingMenuBattle!.Formation);
        Assert.Null(world.Events.PendingHostedBattle);
        Assert.Null(session.State.PendingBattleEncounter!.ScriptedOwner);
        Assert.Null(session.State.RandomEncounters?.Pending);
        Assert.False(world.Apply(new PlayerAction(PlayerActionKind.UseFieldItem, 168, Quantity: 0)).Accepted);
        Assert.Equal(count - 1, LampCount(session));
        string pendingSnapshot = TestResources.ArtifactFile("menu-battle-pending.json");
        session.SetEventState(world.Events.Capture());
        session.Save(pendingSnapshot);
        GameSession pendingSession = new(GameSession.Load(pendingSnapshot));
        using (GameWorld pendingWorld = new(pendingSession, game))
        {
            pendingWorld.EnsureField();
            Assert.Equal(world.Events.PendingMenuBattle, pendingWorld.Events.PendingMenuBattle);
            Assert.Equal(session.State.PendingBattleEncounter, pendingSession.State.PendingBattleEncounter);
            Assert.Null(pendingWorld.Battle);
            Assert.Equal(count - 1, LampCount(pendingSession));
            Assert.False(pendingWorld.Apply(new PlayerAction(PlayerActionKind.UseFieldItem, 168, Quantity: 0)).Accepted);
            Assert.Equal(count - 1, LampCount(pendingSession));
        }
        world.ApplyBattleInput(new BattleInput(BattleInputKind.StartEncounter, NativeSeed: 0));
        world.ApplyBattleInput(new BattleInput(BattleInputKind.Tick, 120));
        Assert.True(world.Battle!.Frame > 0);
        Assert.True(world.Events.PendingMenuBattle.Callbacks > 0);
        string snapshot = TestResources.ArtifactFile("menu-battle-mid-combat.json");
        session.SetEventState(world.Events.Capture());
        session.Save(snapshot);
        GameSession restoredSession = new(GameSession.Load(snapshot));
        using GameWorld restored = new(restoredSession, game);
        restored.EnsureField();
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(world.Battle.Capture()), JsonSerializer.SerializeToElement(restored.Battle!.Capture())));
        Assert.Equal(count - 1, LampCount(restoredSession));
        world.ApplyBattleInput(new BattleInput(BattleInputKind.Tick));
        restored.ApplyBattleInput(new BattleInput(BattleInputKind.Tick));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(world.Battle.Capture()), JsonSerializer.SerializeToElement(restored.Battle.Capture())));
        restored.ApplyBattleInput(new BattleInput(BattleInputKind.InstantVictory));
        for (int callback = 0; callback < 2400 && !restored.Battle.CanReturnToField; callback++)
        {
            restored.ApplyBattleInput(new BattleInput(BattleInputKind.Tick));
        }
        Assert.True(restored.Battle.CanReturnToField, "The native victory callbacks must reach the menu battle return boundary.");
        restored.ApplyBattleInput(new BattleInput(BattleInputKind.Return));
        Assert.Null(restored.Battle);
        Assert.Null(restored.Events.PendingMenuBattle);
        Assert.Equal(SceneKind.Field, restoredSession.State.Scene);
        Assert.Equal(fieldId, restoredSession.State.FieldId);
        Assert.Equal(count - 1, LampCount(restoredSession));
        Assert.Equal(1, restoredSession.State.CharacterVitals!.GuardianForces[5].Flags & 1);
    }

    [Fact]
    public void InstalledLampBattleExecutesOriginalEncounterTurn()
    {
        GameSession session = new(GameSession.Load(TestResources.FixturePath("menu-battle-field-entry")));
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.UseFieldItem, 168, Quantity: 0)).Accepted);
        world.ApplyBattleInput(new BattleInput(BattleInputKind.StartEncounter, NativeSeed: 0));
        world.ApplyBattleInput(new BattleInput(BattleInputKind.Tick, 1200));
        Assert.True(world.Events.PendingMenuBattle!.Callbacks >= 1200);
        Assert.Equal(BattleOutcome.Active, world.Battle!.Outcome);
        Assert.DoesNotContain(world.Battle.Events, item => item.Kind == "debug-victory");
        Assert.Contains(world.Battle.Events, item => item.Kind == "ActionComplete" || item.Kind == "Damage");
    }

    private static int LampCount(GameSession session)
    {
        return session.State.Economy!.Items.Where(stock => stock.ItemId == 168).Sum(stock => stock.Count);
    }
}
