using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class SavedWorldSessionTests
{
    [Fact]
    public void OnlyExplicitNewGameInitializesSavedWorld()
    {
        GameSession session = GameSession.CreateColdProcess();
        Assert.Null(session.State.SavedWorld);
        session.StartNewGame();
        Assert.Equal(NativeSavedWorldState.CreateNewGame(), session.State.SavedWorld);
        Assert.Throws<InvalidDataException>(() => new GameSession(session.State with { SavedWorld = session.State.SavedWorld! with { SchemaVersion = 99 } }));
    }

    [Fact]
    public void NonzeroSavedSlotsSurviveScenesAndCheckpointReload()
    {
        NativeSavedWorldState saved = NativeSavedWorldState.CreateNewGame() with { Slot4 = new NativeSavedWorldVehicleSlot(73, new NativeSavedWorldPose(-123, 456, -789, 65000)) };
        var session = new GameSession(new GameState { SavedWorld = saved, WorldEntryMemory = NativeWorldEntryMemory.CreateNewGame() });
        session.RequestField(232, 0);
        session.RequestMovie(1);
        session.CompleteMovie();
        session.RequestWorld(new WorldTransition(0, 0, 0));
        session.RequestField(139, 0);
        Assert.Equal(saved, session.State.SavedWorld);
        string path = Path.Combine(Path.GetTempPath(), "ff8-saved-world-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.Save(path);
            Assert.Equal(saved, GameSession.Load(path).SavedWorld);
            session.Restore(new GameState());
            session.Save(path);
            Assert.Null(GameSession.Load(path).SavedWorld);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
