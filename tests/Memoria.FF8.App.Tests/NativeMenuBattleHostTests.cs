using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeMenuBattleHostTests
{
    [Fact]
    public void NamedMenuBattleDefersEventCaptureUntilSaveAndRestoresCurrentCallbacks()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_BATTLE_PERFORMANCE_SNAPSHOT");
        Assert.SkipUnless(source is not null && File.Exists(source), "An optional standalone menu battle fixture is required.");
        GameSession session = new(GameSession.Load(source!));
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        Assert.NotNull(world.Battle);
        Assert.NotNull(world.Events.PendingMenuBattle);
        JsonElement storedEvents = session.State.EventState!.Value;
        int initialCallbacks = world.Events.PendingMenuBattle!.Callbacks;
        world.ApplyBattleInput(new BattleInput(BattleInputKind.Tick, 2));
        Assert.True(world.Events.PendingMenuBattle.Callbacks > initialCallbacks);
        // JsonElement equality checks the retained document identity, so an equivalent recapture also fails this assertion.
        Assert.True(storedEvents.Equals(session.State.EventState!.Value));

        string snapshot = TestResources.ArtifactFile("menu-battle-deferred-event-capture.json");
        session.Save(snapshot);
        GameSession restoredSession = new(GameSession.Load(snapshot));
        using GameWorld restored = new(restoredSession, TestResources.GameDirectory);
        restored.EnsureField();
        Assert.Equal(world.Events.PendingMenuBattle, restored.Events.PendingMenuBattle);
        Assert.Equal(world.Events.Scene.FieldCallbackClock, restored.Events.Scene.FieldCallbackClock);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(world.Battle!.Capture()), JsonSerializer.SerializeToElement(restored.Battle!.Capture())));

        world.ApplyBattleInput(new BattleInput(BattleInputKind.Tick));
        restored.ApplyBattleInput(new BattleInput(BattleInputKind.Tick));
        Assert.Equal(world.Events.PendingMenuBattle, restored.Events.PendingMenuBattle);
        Assert.Equal(world.Events.Scene.FieldCallbackClock, restored.Events.Scene.FieldCallbackClock);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(world.Battle.Capture()), JsonSerializer.SerializeToElement(restored.Battle.Capture())));
    }

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
        long fieldFrameBeforeReturn = restored.Events.Frame;
        restored.ApplyBattleInput(new BattleInput(BattleInputKind.Return));
        Assert.Equal(fieldFrameBeforeReturn + 1, restored.Events.Frame);
        Assert.Null(restored.Battle);
        Assert.Null(restored.Events.PendingMenuBattle);
        Assert.Equal(SceneKind.Field, restoredSession.State.Scene);
        Assert.Equal(fieldId, restoredSession.State.FieldId);
        Assert.Equal(count - 1, LampCount(restoredSession));
        Assert.Equal(1, restoredSession.State.CharacterVitals!.GuardianForces[5].Flags & 1);
        JsonElement returnedEvents = restored.Events.Capture();
        byte[] returnedPixels = restored.Render(TimeSpan.Zero).ToArray();
        Assert.NotEmpty(returnedPixels);
        Assert.Equal(returnedPixels, restored.Render(TimeSpan.Zero));
        Assert.True(JsonElement.DeepEquals(returnedEvents, restored.Events.Capture()));
        string returnedSnapshot = TestResources.ArtifactFile("menu-battle-returned.json");
        restoredSession.SetEventState(returnedEvents);
        restoredSession.Save(returnedSnapshot);
        GameSession returnedSession = new(GameSession.Load(returnedSnapshot));
        using GameWorld returned = new(returnedSession, game);
        returned.EnsureField();
        Assert.Equal(count - 1, LampCount(returnedSession));
        Assert.True(JsonElement.DeepEquals(restored.Events.Capture(), returned.Events.Capture()));
        Assert.Equal(returnedPixels, returned.Render(TimeSpan.Zero));
    }

    [Fact]
    public void InstalledLampBattleExecutesOriginalEncounterTurn()
    {
        GameSession session = new(GameSession.Load(TestResources.FixturePath("menu-battle-field-entry")));
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.UseFieldItem, 168, Quantity: 0)).Accepted);
        world.ApplyBattleInput(new BattleInput(BattleInputKind.StartEncounter, NativeSeed: 0));
        bool restoredEffect = false;
        bool completedEffect = false;
        for (int callbacks = 0; callbacks < 1200;)
        {
            int batch = Math.Min(10, 1200 - callbacks);
            world.ApplyBattleInput(new BattleInput(BattleInputKind.Tick, batch));
            callbacks += batch;
            NativeBattleOriginalEffectState? effect = world.Battle!.NativeGraphics!.OriginalEffect;
            completedEffect |= restoredEffect && (effect?.Binding.Effect != 107 || effect.ReturnedDescriptor == 0);
            if (!restoredEffect && effect is { Binding.Effect: 107, Pools.Length: 5 } && effect.Pools[1].Slots.Any(task => (task.Flags & 1) != 0 && task.ReadWord(12) is >= 40 and < 70))
            {
                string snapshot = TestResources.ArtifactFile("menu-battle-effect107-mid-action.json");
                session.SetEventState(world.Events.Capture());
                session.Save(snapshot);
                GameSession restoredSession = new(GameSession.Load(snapshot));
                using GameWorld restored = new(restoredSession, TestResources.GameDirectory);
                restored.EnsureField();
                Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(world.Battle.Capture()), JsonSerializer.SerializeToElement(restored.Battle!.Capture())));
                world.ApplyBattleInput(new BattleInput(BattleInputKind.Tick));
                restored.ApplyBattleInput(new BattleInput(BattleInputKind.Tick));
                callbacks++;
                Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(world.Battle.Capture()), JsonSerializer.SerializeToElement(restored.Battle.Capture())));
                restoredEffect = true;
            }
        }
        Assert.True(restoredEffect, "The original effect107 must reach a mid-action hierarchy and particle frame that survives a cold host restore.");
        Assert.True(completedEffect, "The original effect107 callback chain must complete after its restored mid-action frame.");
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
