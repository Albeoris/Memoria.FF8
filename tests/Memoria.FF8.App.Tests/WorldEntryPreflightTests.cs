using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class WorldEntryPreflightTests
{
    [Fact]
    public void UnsupportedEntryPreparationPreservesItsSourceBeforeResources()
    {
        GameState departure = ComponentDeparture();
        string before = JsonSerializer.Serialize(departure);
        Assert.Throws<NotSupportedException>(() => WorldEntryPreflight.Prepare(departure, "missing corpus", new WorldTransition(0, 127, 0)));
        Assert.Throws<NotSupportedException>(() => WorldEntryPreflight.Prepare(departure with { WorldEntryMemory = null }, "missing corpus", new WorldTransition(62, 0, 0)));
        Assert.Throws<NotSupportedException>(() => WorldEntryPreflight.Prepare(departure with { SavedWorld = null }, "missing corpus", new WorldTransition(0, 0, 0)));
        Assert.Throws<NotSupportedException>(() => WorldEntryPreflight.Prepare(departure with { WorldControls = null }, "missing corpus", new WorldTransition(0, 0, 0)));
        Assert.Equal(before, JsonSerializer.Serialize(departure));
    }

    [Fact]
    public void MissingFootReloadReceiptRefusesBeforePublishing()
    {
        GameState departure = ComponentDeparture();
        Assert.Null(departure.SavedWorld!.FootReturn);
        string before = JsonSerializer.Serialize(departure);
        Assert.Throws<NotSupportedException>(() => WorldEntryPreflight.Prepare(departure, GameDirectory(), new WorldTransition(62, 0, 0)));
        Assert.Equal(before, JsonSerializer.Serialize(departure));
    }

    [Fact]
    public void PreparedBackendAdoptionUsesTheCommittedSessionAndTransfersDisposalOwnership()
    {
        GameState departure = ComponentDeparture();
        string before = JsonSerializer.Serialize(departure);
        using PreparedWorldEntry entry = WorldEntryPreflight.Prepare(departure, GameDirectory(), new WorldTransition(0, 0, 0));
        GameState candidate = Candidate(departure, entry);
        entry.ValidateAdoption(candidate);
        var actualSession = new GameSession(candidate);
        using WorldMapScene scene = entry.Adopt(actualSession);
        Assert.Same(entry.InitialState, scene.State);
        Assert.Throws<InvalidOperationException>(() => entry.Adopt(actualSession));
        Assert.Throws<InvalidOperationException>(() => scene.RebindPreparedSession(actualSession));
        entry.Dispose();
        byte[] pixels = scene.Render(320, 224);
        Assert.Equal(320 * 224 * 4, pixels.Length);
        Assert.Contains(pixels.Where((value, index) => index % 4 != 3), value => value != 0);
        Assert.True(scene.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.Equal(1, actualSession.State.World!.Frame);
        Assert.Same(actualSession.State.World, scene.State);
        Assert.Equal(before, JsonSerializer.Serialize(departure));
        Assert.Equal(candidate.WorldControls, actualSession.State.WorldControls);
    }

    [Fact]
    public void MismatchedCandidateRefusesBeforeConsumingThePreparedBackend()
    {
        GameState departure = ComponentDeparture();
        using PreparedWorldEntry entry = WorldEntryPreflight.Prepare(departure, GameDirectory(), new WorldTransition(0, 0, 0));
        GameState candidate = Candidate(departure, entry);
        Assert.Throws<InvalidOperationException>(() => entry.ValidateAdoption(departure));
        Assert.Throws<InvalidOperationException>(() => entry.ValidateAdoption(candidate with { WorldArrival = new WorldTransition(0, 0, 1) }));
        Assert.Throws<InvalidOperationException>(() => entry.ValidateAdoption(candidate with { World = entry.InitialState with { Frame = 1 } }));
        Assert.Throws<InvalidOperationException>(() => entry.ValidateAdoption(candidate with { WorldEntryMemory = candidate.WorldEntryMemory!.WriteBytes(256, 2, 18) }));
        Assert.Throws<InvalidOperationException>(() => entry.ValidateAdoption(candidate with { SavedWorld = candidate.SavedWorld! with { Slot4 = candidate.SavedWorld!.Slot4 with { ShapeId = 1 } } }));
        var unchangedSession = new GameSession(departure);
        string before = JsonSerializer.Serialize(unchangedSession.State);
        Assert.Throws<InvalidOperationException>(() => entry.Adopt(unchangedSession));
        Assert.Equal(before, JsonSerializer.Serialize(unchangedSession.State));
        entry.ValidateAdoption(candidate);
        using WorldMapScene adopted = entry.Adopt(new GameSession(candidate));
        Assert.Same(entry.InitialState, adopted.State);
    }

    [Fact]
    public void AbandonedPreparationDisposesOnceAndCannotBeAdopted()
    {
        GameState departure = ComponentDeparture();
        PreparedWorldEntry entry = WorldEntryPreflight.Prepare(departure, GameDirectory(), new WorldTransition(0, 0, 0));
        GameState candidate = Candidate(departure, entry);
        entry.Dispose();
        entry.Dispose();
        Assert.Throws<ObjectDisposedException>(() => entry.ValidateAdoption(candidate));
        Assert.Throws<ObjectDisposedException>(() => entry.Adopt(new GameSession(candidate)));
        Assert.Equal((24576, -29406, -658), (entry.InitialState.X, entry.InitialState.Z, entry.InitialState.NativeHeight));
    }

    [Fact]
    public void DetachedPreparationKeepsItsVariableSnapshotAndResourceFailureKeepsDeparturePure()
    {
        GameState departure = ComponentDeparture();
        string directory = GameDirectory();
        using PreparedWorldEntry entry = WorldEntryPreflight.Prepare(departure, directory, new WorldTransition(0, 0, 0));
        GameState candidate = Candidate(departure, entry);
        GameState changedCandidate = candidate with { WorldEntryMemory = candidate.WorldEntryMemory!.WriteBytes(256, 2, 18) };
        Assert.Throws<InvalidOperationException>(() => entry.ValidateAdoption(changedCandidate));
        GameState originalCandidate = changedCandidate with { WorldEntryMemory = changedCandidate.WorldEntryMemory!.WriteBytes(256, 2, 17) };
        entry.ValidateAdoption(originalCandidate);
        using WorldMapScene scene = entry.Adopt(new GameSession(originalCandidate));
        Assert.Equal(320 * 224 * 4, scene.Render(320, 224).Length);

        GameState unsupportedContact = departure with { WorldEntryMemory = departure.WorldEntryMemory!.WriteBytes(256, 2, 34) };
        string before = JsonSerializer.Serialize(unsupportedContact);
        Assert.Throws<NotSupportedException>(() => WorldEntryPreflight.Prepare(unsupportedContact, directory, new WorldTransition(0, 0, 0)));
        Assert.Equal(before, JsonSerializer.Serialize(unsupportedContact));
    }

    private static GameState ComponentDeparture()
    {
        // Isolated backend lifecycle fixture derived from a genuine standalone field save; no gateway or world visit is executed here.
        return GameSession.Load(TestResources.FixturePath("App.Tests.WorldEntryPreflightTests.1"));
    }

    private static GameState Candidate(GameState departure, PreparedWorldEntry entry)
    {
        return departure with { Scene = SceneKind.WorldMap, WorldArrival = entry.Transition, World = entry.InitialState, FieldArrival = null, DialogueDraft = null, DebugView = null, WorldControls = departure.WorldControls!.WithWorldEntryMenuFlag(), SavedWorld = entry.InitialSavedWorld, EventMemory = entry.InitialEventMemory };
    }

    private static string GameDirectory()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed world corpus is required.");
        return directory;
    }
}
