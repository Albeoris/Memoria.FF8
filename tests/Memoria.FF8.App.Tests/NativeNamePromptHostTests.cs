using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeNamePromptHostTests
{
    [Fact]
    public void Actual_native_prompt_uses_shared_editor_and_restores_its_draft_before_game_owned_confirmation()
    {
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(game), "Installed original field resources are required.");
        string source = Path.Combine(Repository(), "../fixtures/external-fixture.json");
        GameSession session = new(GameSession.Load(source));
        using GameWorld world = new(session, game);
        world.EnsureField();
        Evidence(session, world, "native-source.json");
        Assert.True(world.Events.CurrentDialogue!.IsNamePrompt);
        long frame = world.Events.Frame;
        string stateBeforeUi = world.Events.Capture().GetRawText();
        MenuController menus = new();
        RecreateGame host = Host(session, world, menus);
        Assert.True((bool)Invoke(host, "SynchronizeNativeNamePrompt")!);
        Assert.True(menus.IsNamePrompt);
        Assert.Equal("name-input", menus.ScreenId);
        Assert.Equal("Griever", menus.NameDraft);
        Assert.True(Get<bool>(host, "suppressNativeNameInput"));
        Assert.False((bool)Invoke(host, "SynchronizeNativeNamePrompt")!);
        Assert.Equal(stateBeforeUi, world.Events.Capture().GetRawText());
        menus.Execute(new MenuAction("name-clear"));
        Assert.True(menus.EnterNameText("Леон👩🏽‍🚀"));
        menus.Navigate(MenuInput.Cancel);
        Assert.Equal("Леон", menus.NameDraft);
        Assert.True(menus.IsNamePrompt);
        Assert.Equal(stateBeforeUi, world.Events.Capture().GetRawText());
        Invoke(host, "CaptureSession");
        Assert.Equal("Леон", session.State.DialogueDraft!.EnteredName);
        Evidence(session, world, "native-draft.json");
        GameSession restoredSession = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        using GameWorld restored = new(restoredSession, game);
        restored.EnsureField();
        MenuController restoredMenus = new();
        RecreateGame restoredHost = Host(restoredSession, restored, restoredMenus);
        Assert.True((bool)Invoke(restoredHost, "SynchronizeNativeNamePrompt")!);
        Assert.Equal("Леон", restoredMenus.NameDraft);
        menus.Execute(new MenuAction("name-save"));
        restoredMenus.Execute(new MenuAction("name-save"));
        Assert.False(menus.IsOpen);
        Assert.False(restoredMenus.IsOpen);
        Assert.Null(session.State.DialogueDraft);
        Assert.Null(restoredSession.State.DialogueDraft);
        Assert.Null(world.Events.CurrentDialogue);
        Assert.Equal("Леон", world.Events.Scene.Names[GameNames.GrieverSubject]);
        Assert.Equal(frame, world.Events.Frame);
        Assert.True(Get<bool>(host, "drainUiButtons"));
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Evidence(session, world, "native-accepted.json");
        Assert.Equal(world.Apply(new PlayerAction(PlayerActionKind.Wait)), restored.Apply(new PlayerAction(PlayerActionKind.Wait)));
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Evidence(restoredSession, restored, "native-continued.json");
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_NAMING_EVIDENCE");
        if (evidence is not null)
        {
            File.WriteAllText(Path.Combine(evidence, "native-proof.json"), JsonSerializer.Serialize(new
            {
                Source = source,
                Field = session.State.FieldId,
                PromptFrame = frame,
                AcceptedFrame = frame,
                ContinuedFrame = restored.Events.Frame,
                Subject = GameNames.GrieverSubject,
                Name = "Леон",
                EditorStateDoesNotAdvanceNativeFrame = true,
                RestoredDraftAndNativeAcceptMatch = true,
                IdenticalNextWait = true,
                PhysicalInputAndGraphicalHostTested = false
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private static void Evidence(GameSession session, GameWorld world, string name)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_NAMING_EVIDENCE");
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
            session.SetEventState(world.Events.Capture());
            session.Save(Path.Combine(directory, name));
        }
    }

    private static RecreateGame Host(GameSession session, GameWorld world, MenuController menus)
    {
        // Exercise host binding without constructing a graphics device or sampling physical input.
        RecreateGame host = (RecreateGame)RuntimeHelpers.GetUninitializedObject(typeof(RecreateGame));
        Set(host, "session", session);
        Set(host, "world", world);
        Set(host, "gameMenus", menus);
        Set(host, "dialoguePages", new DialoguePageCache());
        return host;
    }

    private static void Set(RecreateGame host, string name, object value)
    {
        typeof(RecreateGame).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(host, value);
    }

    private static T Get<T>(RecreateGame host, string name)
    {
        return (T)typeof(RecreateGame).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host)!;
    }

    private static object? Invoke(RecreateGame host, string name)
    {
        return typeof(RecreateGame).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(host, null);
    }

    private static string Repository()
    {
        DirectoryInfo? directory = new(TestResources.FixtureDirectory);
        return directory?.FullName ?? throw new DirectoryNotFoundException("Research checkout was not found.");
    }
}
