using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

/// <summary>Backend transaction fixtures; direct preparation does not establish an ordinary player world visit.</summary>
public sealed class WorldEntryTransactionTests
{
    [Fact]
    public void PreparedHostEntryAdoptsItsBackendAndPersistsLoaderControlsBeforeWorldMovement()
    {
        string directory = GameDirectory();
        var session = new GameSession(GameSession.Load(Source()));
        using var host = new GameWorld(session, directory);
        host.EnsureField();
        NativeWorldControlState controls = session.WorldControls!;
        NativeWorldControlState loader = controls.WithWorldEntryMenuFlag();
        var request = new NativeWorldTransitionRequest(1, host.Events.FieldId, host.Events.Frame, NativeWorldTransitionOrigin.Script, new WorldTransition(0, 0, -1), controls, loader, loader);
        string before = JsonSerializer.Serialize(session.State);
        Action commit = host.Events.PrepareWorldTransition!(request)!;
        Assert.Equal(before, JsonSerializer.Serialize(session.State));
        Assert.Null(host.WorldScene);
        commit();
        Assert.Equal(SceneKind.WorldMap, session.State.Scene);
        Assert.Equal(request.Transition, session.State.WorldArrival);
        Assert.Equal(loader, session.WorldControls);
        Assert.NotNull(host.WorldScene);
        Assert.Same(session.State.World, host.WorldScene.State);
        Assert.False(host.EnsureField());
        Assert.Throws<InvalidOperationException>(commit);
        host.SetRenderSize(320, 224);
        byte[] initial = host.Render(TimeSpan.Zero);
        Assert.Equal(320 * 224 * 4, initial.Length);
        Assert.True(host.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1)).Accepted);
        Assert.Equal(1, session.State.World!.Frame);
        Assert.NotEqual(24576, session.State.World.X);
        session.SetEventState(host.Events.Capture());
        string saved = Path.Combine(Path.GetTempPath(), "ff8-prepared-world-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.Save(saved);
            var restoredSession = new GameSession(GameSession.Load(saved));
            using var restored = new GameWorld(restoredSession, directory);
            restored.EnsureField();
            restored.SetRenderSize(320, 224);
            Assert.Equal(host.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
            var next = new PlayerAction(PlayerActionKind.Move, Vertical: -1);
            Assert.Equal(host.Apply(next), restored.Apply(next));
            Assert.Equal(JsonSerializer.Serialize(session.State.World), JsonSerializer.Serialize(restoredSession.State.World));
            Assert.Equal(host.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
        }
        finally
        {
            File.Delete(saved);
        }
    }

    [Fact]
    public void StalePreparedDepartureRefusesBeforePublishingTheWorld()
    {
        var session = new GameSession(GameSession.Load(Source()));
        using var host = new GameWorld(session, GameDirectory());
        host.EnsureField();
        NativeWorldControlState controls = session.WorldControls!;
        NativeWorldControlState loader = controls.WithWorldEntryMenuFlag();
        var request = new NativeWorldTransitionRequest(1, host.Events.FieldId, host.Events.Frame, NativeWorldTransitionOrigin.Script, new WorldTransition(0, 0, 0), controls, loader, loader);
        Action commit = host.Events.PrepareWorldTransition!(request)!;
        session.SetPosition(session.State.X + 1, session.State.Y);
        string before = JsonSerializer.Serialize(session.State);
        Assert.Throws<InvalidOperationException>(commit);
        Assert.Equal(before, JsonSerializer.Serialize(session.State));
        Assert.Null(host.WorldScene);
        Assert.Throws<NotSupportedException>(() => host.Events.PrepareWorldTransition(request with { SourceFrame = request.SourceFrame - 1 }));
    }

    [Theory]
    [InlineData(273, 256)]
    [InlineData(17, 257)]
    [InlineData(17, 264)]
    [InlineData(17, 263)]
    [InlineData(17, 209)]
    public void StoryUpperByteAndAmbiguousAliasesRefuseBeforeLoadingResources(int story, int alias)
    {
        NativeWorldEntryMemory selected = NativeWorldEntryMemory.CreateNewGame().WriteBytes(256, 2, story);
        NativeEventMemory generic = alias == 256 ? NativeEventMemory.Empty : NativeEventMemory.Empty.Write(alias, 4, NativeScalarValue.Exact(alias == 263 ? 256 : 1));
        var departure = new GameState { Scene = SceneKind.Field, SavedWorld = NativeSavedWorldState.CreateNewGame(), WorldControls = NativeWorldControlState.CreateNewGame(), WorldEntryMemory = selected, EventMemory = generic };
        string before = JsonSerializer.Serialize(departure);
        if (alias is 256 or 209)
        {
            Assert.Throws<NotSupportedException>(() => WorldEntryPreflight.Prepare(departure, "missing corpus", new WorldTransition(0, 0, 0)));
        }
        else
        {
            Assert.Throws<InvalidDataException>(() => WorldEntryPreflight.Prepare(departure, "missing corpus", new WorldTransition(0, 0, 0)));
        }
        Assert.Equal(before, JsonSerializer.Serialize(departure));
    }

    private static string Source()
    {
        DirectoryInfo? repository = new(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "docs", "recreate-checkpoint.md")))
        {
            repository = repository.Parent;
        }
        return Path.Combine(repository!.FullName, "../fixtures/external-fixture.json");
    }

    private static string GameDirectory()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field/world corpus is required.");
        return directory;
    }
}
