using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BackgroundOrderingRecreationTests
{
    [Fact]
    public void AuthenticOpeningRecreationRetainsStatePixelsAndOrdinaryContinuation()
    {
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        DirectoryInfo? repository = new(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "docs", "recreate-checkpoint.md")))
        {
            repository = repository.Parent;
        }
        string source = Path.Combine(repository!.FullName, "../fixtures/external-fixture.json");
        Assert.SkipUnless(Directory.Exists(game) && File.Exists(source), "Authentic opening journal and installed resources are required.");
        GameState first = GameWorld.RecreateSave(GameSession.LoadReplaySource(source), game);
        GameState repeated = GameWorld.RecreateSave(new GameState { Scene = first.Scene, FieldId = first.FieldId, ReplayJournal = first.ReplayJournal }, game);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(first with { ReplayJournal = null }), JsonSerializer.SerializeToElement(repeated with { ReplayJournal = null })));
        using GameWorld world = new(new GameSession(first), game, persistReplayJournal: false);
        using GameWorld reference = new(new GameSession(repeated), game, persistReplayJournal: false);
        world.EnsureField();
        reference.EnsureField();
        for (int callback = 0; callback < 60 && (FieldPresentationReadiness.HasPendingInitialization(world.Events.Scene.NativeFieldInitialization) || !world.CanPresentCurrentField); callback++)
        {
            Assert.Equal(world.Apply(new PlayerAction(PlayerActionKind.Wait)), reference.Apply(new PlayerAction(PlayerActionKind.Wait)));
        }
        Assert.True(world.CanPresentCurrentField);
        Assert.Equal(world.Render(TimeSpan.Zero), reference.Render(TimeSpan.Zero));
        Assert.Equal(world.Apply(new PlayerAction(PlayerActionKind.Wait)), reference.Apply(new PlayerAction(PlayerActionKind.Wait)));
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), reference.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), reference.Render(TimeSpan.Zero));
    }
}
