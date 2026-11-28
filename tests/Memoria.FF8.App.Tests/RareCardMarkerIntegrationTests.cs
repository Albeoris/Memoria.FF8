using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class RareCardMarkerIntegrationTests
{
    [Fact]
    public void NativeQueenQuicksaveRendersOwnedCardsAndPrioritizesAnAvailableQuestLoss()
    {
        string directory = TestResources.GameDirectory;
        string source = TestResources.FixturePath("App.Tests.RareCardMarkerIntegrationTests.3");
        var session = new GameSession(GameplaySaveSessionLoader.Load(source, directory));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        Assert.Equal(532, world.Events.FieldId);
        Assert.True(world.Events.IsDialogueOpen);
        Assert.Empty(world.Events.GetRareCardOffers());
        for (int callback = 0; callback < 120 && (!world.Events.IsUserControlEnabled || world.Events.IsDialogueOpen); callback++)
        {
            if (world.Events.CurrentDialogue is { CanConfirm: true } dialogue && world.Events.CanReceiveDialogueInput(dialogue))
            {
                world.Events.ConfirmDialogue(dialogue.LastChoice);
            }
            world.Events.Step();
        }
        Assert.Null(world.Events.LastFault);
        Assert.Null(world.Events.PendingTripleTriad);
        Assert.True(world.Events.IsUserControlEnabled);
        Assert.False(world.Events.IsDialogueOpen);
        world.SetRenderSize(640, 480);
        Assert.Equal([84, 90], world.Events.GetRareCardOffers().Where(offer => offer.ObjectId == 10).Select(offer => offer.CardId).Order().ToArray());
        JsonElement before = world.Events.Capture();
        byte[] disabled = world.Render(TimeSpan.Zero, rareCardMarkers: false).ToArray();
        byte[] ifrit = world.Render(TimeSpan.Zero).ToArray();
        Assert.False(ifrit.SequenceEqual(disabled));
        Assert.False(ifrit.SequenceEqual(world.Render(TimeSpan.FromSeconds(8))));
        Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));

        // Exercise priority with the same opponent and both types of hint available.
        session.SetEventMemory(session.EventMemory.Write(300, 1, 0));
        var offers = world.Events.GetRareCardOffers();
        Assert.Contains(new RareCardOffer(10, 80, IsQuestLoss: true), offers);
        Assert.Contains(new RareCardOffer(10, 84), offers);
        byte[] quest = world.Render(TimeSpan.Zero).ToArray();
        Assert.False(quest.SequenceEqual(ifrit));
        Assert.Equal(quest, world.Render(TimeSpan.FromSeconds(8)));
        Assert.Equal(quest, world.Render(TimeSpan.FromSeconds(16)));
    }

    [Fact]
    public void NativeDelingQuicksaveRendersKirosOverTheBakedPedestrian()
    {
        string directory = TestResources.GameDirectory;
        string source = TestResources.FixturePath("App.Tests.RareCardMarkerIntegrationTests.2");
        var session = new GameSession(GameplaySaveSessionLoader.Load(source, directory));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        world.SetRenderSize(640, 480);
        Assert.Equal(742, world.Events.FieldId);
        Assert.False(world.Events.Actors[12].Visible);
        Assert.Contains(new RareCardOffer(12, 100, UseHiddenInteractionAnchor: true), world.Events.GetRareCardOffers());
        JsonElement before = world.Events.Capture();
        byte[] disabled = world.Render(TimeSpan.Zero, rareCardMarkers: false).ToArray();
        byte[] front = world.Render(TimeSpan.Zero).ToArray();
        byte[] back = world.Render(TimeSpan.FromSeconds(4)).ToArray();
        Assert.False(front.SequenceEqual(disabled));
        Assert.False(back.SequenceEqual(disabled));
        Assert.False(front.SequenceEqual(back));
        Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));
    }

    [Fact]
    public void NativeMartineOfferRendersBothFacesWithoutMutationAndResumesFromStandaloneSave()
    {
        string directory = TestResources.GameDirectory;
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
        string directory = TestResources.GameDirectory;
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
        return TestResources.FixturePath("App.Tests.RareCardMarkerIntegrationTests.1");
    }
}
