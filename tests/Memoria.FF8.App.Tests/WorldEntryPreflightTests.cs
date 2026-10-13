using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class WorldEntryPreflightTests
{
    [Fact]
    public void SelectedEntryPreparationPreservesTheCompleteDepartingState()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed world corpus is required.");
        DirectoryInfo? repository = new(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "docs", "recreate-checkpoint.md")))
        {
            repository = repository.Parent;
        }
        GameState departure = LegacyWorldControlProof.Upgrade(File.ReadAllBytes(Path.Combine(repository!.FullName, "../fixtures/external-fixture.json")));
        string before = JsonSerializer.Serialize(departure);
        using PreparedWorldEntry entry = WorldEntryPreflight.Prepare(departure, directory, new WorldTransition(0, 0, 0));
        Assert.Equal((24576, -29406, -658), (entry.InitialState.X, entry.InitialState.Z, entry.InitialState.NativeHeight));
        Assert.Equal(0, entry.InitialState.Frame);
        Assert.Empty(entry.InitialState.Actions);
        Assert.Equal(before, JsonSerializer.Serialize(departure));
        Assert.Equal(17, departure.Variables[256]);
    }

    [Fact]
    public void UnsupportedEntryPreparationPreservesItsSourceBeforeResources()
    {
        GameState departure = new() { Scene = SceneKind.Field, SavedWorld = NativeSavedWorldState.CreateNewGame(), WorldControls = NativeWorldControlState.CreateNewGame() };
        string before = JsonSerializer.Serialize(departure);
        Assert.Throws<NotSupportedException>(() => WorldEntryPreflight.Prepare(departure, "missing corpus", new WorldTransition(0, 1, 0)));
        Assert.Throws<NotSupportedException>(() => WorldEntryPreflight.Prepare(departure, "missing corpus", new WorldTransition(62, 0, 0)));
        Assert.Throws<NotSupportedException>(() => WorldEntryPreflight.Prepare(departure with { SavedWorld = null }, "missing corpus", new WorldTransition(0, 0, 0)));
        Assert.Throws<NotSupportedException>(() => WorldEntryPreflight.Prepare(departure with { WorldControls = null }, "missing corpus", new WorldTransition(0, 0, 0)));
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
        Assert.Throws<InvalidOperationException>(() => entry.ValidateAdoption(candidate with { Variables = new Dictionary<int, int> { [256] = 18 } }));
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
        departure.Variables[256] = 18;
        Assert.Throws<InvalidOperationException>(() => entry.ValidateAdoption(candidate));
        GameState originalCandidate = candidate with { Variables = new Dictionary<int, int> { [256] = 17 } };
        entry.ValidateAdoption(originalCandidate);
        using WorldMapScene scene = entry.Adopt(new GameSession(originalCandidate));
        Assert.Equal(320 * 224 * 4, scene.Render(320, 224).Length);

        GameState unsupportedContact = ComponentDeparture() with { Variables = new Dictionary<int, int> { [256] = 34 } };
        string before = JsonSerializer.Serialize(unsupportedContact);
        Assert.Throws<NotSupportedException>(() => WorldEntryPreflight.Prepare(unsupportedContact, directory, new WorldTransition(0, 0, 0)));
        Assert.Equal(before, JsonSerializer.Serialize(unsupportedContact));
    }

    private static GameState ComponentDeparture()
    {
        // Isolated lifecycle fixture; it does not force a live field gateway or establish a visited world.
        return new GameState { Scene = SceneKind.Field, SavedWorld = NativeSavedWorldState.CreateNewGame(), WorldControls = NativeWorldControlState.CreateNewGame(), Variables = new Dictionary<int, int> { [256] = 17 } };
    }

    private static GameState Candidate(GameState departure, PreparedWorldEntry entry)
    {
        return departure with { Scene = SceneKind.WorldMap, WorldArrival = entry.Transition, World = entry.InitialState, FieldArrival = null, DialogueDraft = null, DebugView = null, WorldControls = departure.WorldControls!.WithWorldEntryMenuFlag() };
    }

    private static string GameDirectory()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed world corpus is required.");
        return directory;
    }
}
