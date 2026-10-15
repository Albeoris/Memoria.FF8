using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>Owns the loaded world backend until one validated session adopts it or the abandoned preparation is disposed.</summary>
public sealed class PreparedWorldEntry : IDisposable
{
    private WorldMapScene? scene;
    private bool adopted;
    private bool disposed;

    internal PreparedWorldEntry(WorldTransition transition, WorldMapScene scene, NativeSavedWorldState? savedWorld)
    {
        Transition = transition;
        InitialState = scene.State;
        InitialSavedWorld = savedWorld;
        ProgramHash = scene.AllocationProgramSha256;
        this.scene = scene;
    }

    public WorldTransition Transition { get; }
    public WorldState InitialState { get; }
    public NativeSavedWorldState? InitialSavedWorld { get; }
    public string ProgramHash { get; }

    internal void ValidateAdoption(GameState candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ObjectDisposedException.ThrowIf(disposed, this);
        if (adopted)
        {
            throw new InvalidOperationException("The prepared world entry has already been adopted.");
        }
        scene!.ValidatePreparedSession(candidate);
    }

    internal WorldMapScene Adopt(GameSession committedSession)
    {
        ArgumentNullException.ThrowIfNull(committedSession);
        ValidateAdoption(committedSession.State);
        WorldMapScene prepared = scene!;
        prepared.RebindPreparedSession(committedSession);
        scene = null;
        adopted = true;
        return prepared;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        scene?.Dispose();
        scene = null;
        disposed = true;
    }
}

/// <summary>Validates the selected original resources and contact state before a field VM can issue its world packet.</summary>
public static class WorldEntryPreflight
{
    public static PreparedWorldEntry Prepare(GameState departure, string gameDirectory, WorldTransition transition)
    {
        ArgumentNullException.ThrowIfNull(departure);
        ArgumentNullException.ThrowIfNull(transition);
        if (departure.Scene != SceneKind.Field)
        {
            throw new NotSupportedException("Only the original normal field-to-world entry is prepared here.");
        }
        if (departure.WorldControls is null)
        {
            throw new NotSupportedException("Native world-entry control flags have unknown provenance.");
        }
        departure.WorldControls.Validate();
        if (departure.EventMemory.Cells.Any(pair => pair.Key < 108 && (long)pair.Key + pair.Value.Width > 103 || pair.Key <= 207 && (long)pair.Key + pair.Value.Width > 207 || pair.Key <= 209 && (long)pair.Key + pair.Value.Width > 209))
        {
            throw new NotSupportedException("Native world-entry controls overlap an generic event-memory cell.");
        }
        _ = WorldEntryVariables.Read(departure);
        WorldMapScene.RequireUnaliasedFootAuxiliary(departure);
        GameState candidate = departure with { Scene = SceneKind.WorldMap, WorldArrival = transition, World = null, FieldArrival = null, DialogueDraft = null, DebugView = null, EventMemory = departure.EventMemory };
        var temporarySession = new GameSession(candidate);
        var scene = new WorldMapScene(temporarySession, gameDirectory);
        try
        {
            if (!scene.IsPresentationDeferred)
            {
                _ = scene.Render(320, 224);
            }
            return new PreparedWorldEntry(transition, scene, temporarySession.State.SavedWorld);
        }
        catch
        {
            scene.Dispose();
            throw;
        }
    }
}
