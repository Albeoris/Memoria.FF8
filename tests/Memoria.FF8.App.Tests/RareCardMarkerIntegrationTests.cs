using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class RareCardMarkerIntegrationTests
{
    [Fact]
    public void NativeMartineOfferRendersBothFacesWithoutMutationAndResumesFromStandaloneSave()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        string source = FixturePath();
        Assert.SkipUnless(Directory.Exists(directory) && File.Exists(source), "Installed resources and the standalone Martine diagnostic are required.");
        var session = new GameSession(GameSession.Load(source));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        world.SetRenderSize(640, 480);
        Assert.Equal(621, world.Events.FieldId);
        Assert.Equal(17, world.Events.Frame);
        Assert.Null(world.Events.LastFault);
        Assert.Null(world.Events.PlayerActor);
        Assert.Equal(PlayerTriggerKind.Waiting, world.NextTrigger.Kind);
        RareCardOffer offer = Assert.Single(world.Events.GetRareCardOffers());
        Assert.Equal(new RareCardOffer(6, 84), offer);
        ActorState martine = Assert.Single(world.Events.Actors, actor => actor.ObjectId == offer.ObjectId);
        Assert.True(martine.Active && martine.Visible && martine.TalkEnabled);
        Assert.Equal(6, martine.ModelId);
        JsonElement eventsBefore = world.Events.Capture();
        JsonElement stateBefore = JsonSerializer.SerializeToElement(session.State);
        byte[] front = world.Render(TimeSpan.Zero).ToArray();
        byte[] back = world.Render(TimeSpan.FromSeconds(4)).ToArray();
        byte[] disabled = world.Render(TimeSpan.Zero, false).ToArray();
        Assert.False(front.SequenceEqual(back));
        Assert.False(front.SequenceEqual(disabled));
        Assert.False(back.SequenceEqual(disabled));
        Assert.True(JsonElement.DeepEquals(eventsBefore, world.Events.Capture()));
        Assert.True(JsonElement.DeepEquals(stateBefore, JsonSerializer.SerializeToElement(session.State)));
        using var restored = new GameWorld(new GameSession(GameSession.Load(source)), directory);
        restored.EnsureField();
        restored.SetRenderSize(640, 480);
        Assert.Equal(front, restored.Render(TimeSpan.Zero));
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.True(restored.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.Null(restored.Events.LastFault);
        Assert.Equal(18, restored.Events.Frame);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
    }

    [Fact]
    public void NativeLocationTransferToPlayerRemovesMartineOfferAndItsRenderedMarker()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        string source = FixturePath();
        Assert.SkipUnless(Directory.Exists(directory) && File.Exists(source), "Installed resources and the standalone Martine diagnostic are required.");
        var session = new GameSession(GameSession.Load(source));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        Assert.Equal(new RareCardOffer(6, 84), Assert.Single(world.Events.GetRareCardOffers()));
        byte[] disabled = world.Render(TimeSpan.Zero, false).ToArray();
        byte[] history = session.CardInventory!.ObtainedRareCards.ToArray();
        session.SetCardInventory(session.CardInventory.Transfer(84, 0xF0, out int result));
        Assert.Equal(0, result);
        Assert.Equal(history, session.CardInventory.ObtainedRareCards);
        Assert.Empty(world.Events.GetRareCardOffers());
        Assert.Equal(disabled, world.Render(TimeSpan.Zero));
    }

    private static string FixturePath()
    {
        DirectoryInfo? repository = new(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "docs", "recreate-checkpoint.md")))
        {
            repository = repository.Parent;
        }
        return Path.Combine(repository!.FullName, "../fixtures/external-fixture.json");
    }
}
