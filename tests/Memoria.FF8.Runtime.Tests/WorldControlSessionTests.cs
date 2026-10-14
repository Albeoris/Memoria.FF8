using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class WorldControlSessionTests
{
    [Theory]
    [InlineData(false, 252, 254)]
    [InlineData(true, 255, 253)]
    public void PreparedWorldEntryPublishesStateAndLoaderFlagsTogether(bool disabled, byte menuBefore, byte menuAfter)
    {
        NativeWorldControlState controls = NativeWorldControlState.CreateNewGame().WithPartyPower(disabled ? 0 : 1) with { Menu209 = menuBefore };
        var departure = new GameState { Scene = SceneKind.Field, FieldId = 159, WorldControls = controls, SavedWorld = NativeSavedWorldState.CreateNewGame() };
        var session = new GameSession(departure);
        var transition = new WorldTransition(0, 0, 0);
        var initial = new WorldState { X = 24576, Z = -29406, NativeHeight = -658, BodyYaw = 2048, CameraYaw = 176 };
        int publications = 0;
        session.StateChanged += state =>
        {
            publications++;
            Assert.Equal(SceneKind.WorldMap, state.Scene);
            Assert.Same(initial, state.World);
            Assert.Equal(menuAfter, state.WorldControls!.Menu209);
        };
        session.CommitPreparedWorldEntry(transition, initial, controls, candidate =>
        {
            Assert.Same(departure, session.State);
            Assert.Same(initial, candidate.World);
            Assert.Equal(transition, candidate.WorldArrival);
            Assert.Equal(menuAfter, candidate.WorldControls!.Menu209);
        });
        Assert.Equal(1, publications);
        Assert.Equal(disabled, session.WorldControls!.SourceDisabled);
        Assert.Same(departure.SavedWorld, session.State.SavedWorld);
    }

    [Fact]
    public void PreparedWorldEntryRefusalsPreserveDepartureBeforePublication()
    {
        NativeWorldControlState controls = NativeWorldControlState.CreateNewGame();
        var departure = new GameState { Scene = SceneKind.Field, FieldId = 159, WorldControls = controls };
        var session = new GameSession(departure);
        session.ShowDialog("Departure");
        var transition = new WorldTransition(0, 0, 0);
        var initial = new WorldState();
        int publications = 0;
        session.StateChanged += _ => publications++;
        Assert.Throws<InvalidOperationException>(() => session.CommitPreparedWorldEntry(transition, initial, controls.WithPartyPower(0), _ => Assert.Fail("Stale controls must refuse before ticket validation.")));
        Assert.Throws<InvalidDataException>(() => session.CommitPreparedWorldEntry(transition, initial with { BodyYaw = 4096 }, controls, _ => Assert.Fail("Invalid world state must refuse before ticket validation.")));
        Assert.Throws<NotSupportedException>(() => session.CommitPreparedWorldEntry(transition, initial, controls, _ => throw new NotSupportedException("Ticket mismatch")));
        Assert.Same(departure, session.State);
        Assert.Equal("Departure", session.Dialog);
        Assert.Equal(0, publications);
    }

    [Fact]
    public void FullCheckpointLoadRejectsConflictingNonNullControlCopies()
    {
        NativeWorldControlState controls = NativeWorldControlState.CreateNewGame();
        // A narrow root/VM owner consistency fixture; process message data is
        // genuinely unknown here and recorded explicitly, without a cold seed.
        var session = new GameSession(new GameState { WorldControls = controls, EventState = System.Text.Json.JsonSerializer.SerializeToElement(new { WorldControls = controls.WithPartyPower(0), MessageVariables = (NativeMessageVariables?)null }) });
        string path = Path.Combine(Path.GetTempPath(), "ff8-conflicting-flags-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.Save(path);
            Assert.Throws<InvalidDataException>(() => GameSession.Load(path));
            session.SetEventState(System.Text.Json.JsonSerializer.SerializeToElement(new { WorldControls = controls, MessageVariables = session.State.MessageVariables }));
            session.Save(path);
            Assert.Equal(controls, GameSession.Load(path).WorldControls);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ExplicitNewGameAndNormalFieldsPreserveThePartialControlContract()
    {
        GameSession session = GameSession.CreateColdProcess();
        Assert.Null(session.WorldControls);
        session.StartNewGame();
        Assert.Equal(NativeWorldControlState.CreateNewGame(), session.WorldControls);
        session.SetWorldControls(session.WorldControls!.WithPartyPower(0) with { Menu209 = 255 });
        session.RequestField(232, 0);
        Assert.True(session.WorldControls!.SourceDisabled);
        Assert.Equal((byte)252, session.WorldControls.Menu209);
        session.RequestMovie(1);
        session.CompleteMovie();
        session.RequestWorld(new WorldTransition(0, 0, 0));
        Assert.Equal((byte)252, session.WorldControls.Menu209);
        Assert.Throws<InvalidDataException>(() => session.SetWorldControls(session.WorldControls with { SchemaVersion = 99 }));
        Assert.Equal((byte)252, session.WorldControls.Menu209);
        string path = Path.Combine(Path.GetTempPath(), "ff8-control-flags-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.Save(path);
            Assert.Equal(session.WorldControls, GameSession.Load(path).WorldControls);
            session.Restore(new GameState());
            session.Save(path);
            Assert.Null(GameSession.Load(path).WorldControls);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
