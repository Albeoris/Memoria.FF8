using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Memoria.FF8.GameUi;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class QueuedNamePromptHostTests
{
    [Fact]
    public void Required_item_name_draft_restores_without_reconsuming_and_returns_to_live_inventory()
    {
        string source = TestResources.FixturePath("App.Tests.QueuedNamePromptHostTests.1");
        GameSession session = new(GameSession.Load(source));
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        long frame = world.Events.Frame;
        // Explicit host fixture: the queue starts after the item's already-tested inventory transaction.
        session.EnqueueEntityNames([new PendingEntityName(2, "Angelo", "item:107")]);
        string economy = JsonSerializer.Serialize(session.State.Economy);
        MenuController menus = new();
        RecreateGame host = Host(session, world, menus);
        Assert.True(Invoke(host, "SynchronizeNativeNamePrompt"));
        Assert.False(Invoke(host, "SynchronizeNativeNamePrompt"));
        menus.Execute(new MenuAction("name-clear"));
        menus.EnterNameText("Жёлтый👩🏽‍🚀");
        Invoke(host, "CaptureSession");
        GameSession restoredSession = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        using GameWorld restored = new(restoredSession, TestResources.GameDirectory);
        restored.EnsureField();
        MenuController restoredMenus = new();
        RecreateGame restoredHost = Host(restoredSession, restored, restoredMenus);
        Assert.True(Invoke(restoredHost, "SynchronizeNativeNamePrompt"));
        Assert.Equal(menus.NameDraft, restoredMenus.NameDraft);
        Assert.False(restored.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        menus.Execute(new MenuAction("name-save"));
        restoredMenus.Execute(new MenuAction("name-save"));
        Assert.Null(restored.PendingEntityName);
        Assert.Equal("Жёлтый👩🏽‍🚀", restored.Events.Scene.Names[2]);
        Assert.Equal(frame, restored.Events.Frame);
        Assert.Equal(economy, JsonSerializer.Serialize(restoredSession.State.Economy));
        Assert.False(Invoke(restoredHost, "SynchronizeNativeNamePrompt"));
        Assert.Equal("item-use", restoredMenus.ScreenId);
        Assert.True(restoredMenus.IsOpen);
        Assert.False(restoredMenus.IsDemo);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
    }

    private static RecreateGame Host(GameSession session, GameWorld world, MenuController menus)
    {
        RecreateGame host = (RecreateGame)RuntimeHelpers.GetUninitializedObject(typeof(RecreateGame));
        typeof(RecreateGame).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(host, session);
        typeof(RecreateGame).GetField("world", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(host, world);
        typeof(RecreateGame).GetField("gameMenus", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(host, menus);
        typeof(RecreateGame).GetField("dialoguePages", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(host, new DialoguePageCache());
        return host;
    }

    private static bool Invoke(RecreateGame host, string name)
    {
        object? result = typeof(RecreateGame).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(host, null);
        return result is true;
    }
}
