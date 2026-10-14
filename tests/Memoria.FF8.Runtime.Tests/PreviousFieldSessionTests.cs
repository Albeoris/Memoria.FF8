using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class PreviousFieldSessionTests
{
    [Fact]
    public void NormalFieldAndMovieDeparturesPersistTheirActualSourceAcrossReload()
    {
        var session = new GameSession(new GameState { Scene = SceneKind.Field, FieldId = 160 });
        session.State.Variables[256] = 17;
        session.RequestField(new FieldTransition(159, -126, -3803, 244, 0, 1323));
        Assert.Equal(160, session.State.PreviousFieldId);
        Assert.Equal(159, session.State.FieldId);
        Assert.False(session.State.Variables.ContainsKey(84));
        string path = Path.Combine(Path.GetTempPath(), "ff8-previous-field-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.Save(path);
            var restored = new GameSession(GameSession.Load(path));
            Assert.Equal(160, restored.State.PreviousFieldId);
            Assert.Equal(session.State.FieldArrival, restored.State.FieldArrival);
            Assert.Equal(17, restored.State.Variables[256]);
            restored.RequestMovie(1);
            restored.RequestField(161, 0);
            Assert.Equal(159, restored.State.PreviousFieldId);
            Assert.Equal(SceneKind.Field, restored.State.Scene);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void UnknownAndNewGameOriginsRemainNullAndAreOmittedFromCheckpointJson()
    {
        var session = new GameSession(new GameState());
        session.RequestField(159, 0);
        Assert.Null(session.State.PreviousFieldId);
        session.Restore(new GameState());
        session.RequestMovie(1);
        session.RequestField(159, 0);
        Assert.Null(session.State.PreviousFieldId);
        session = GameSession.CreateColdProcess();
        session.StartNewGame();
        Assert.Null(session.State.PreviousFieldId);
        string path = Path.Combine(Path.GetTempPath(), "ff8-unknown-previous-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.Save(path);
            Assert.DoesNotContain("PreviousFieldId", File.ReadAllText(path));
            Assert.Null(GameSession.Load(path).PreviousFieldId);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void InvalidProvenanceOrDepartureRefusesBeforeChangingTheSession()
    {
        Assert.Throws<InvalidDataException>(() => new GameSession(new GameState { PreviousFieldId = -1 }));
        Assert.Throws<InvalidDataException>(() => new GameSession(new GameState { PreviousFieldId = 65536 }));
        var session = new GameSession(new GameState { Scene = SceneKind.Field, FieldId = 160, PreviousFieldId = 161 });
        GameState before = session.State;
        session.ShowDialog("Retained");
        Assert.Throws<ArgumentOutOfRangeException>(() => session.RequestField(65536, 0));
        Assert.Same(before, session.State);
        Assert.Equal("Retained", session.Dialog);
        session.Restore(before with { PreviousFieldId = ushort.MaxValue });
        Assert.Equal(ushort.MaxValue, session.State.PreviousFieldId);
        session.Restore(before with { FieldId = 65536 });
        GameState invalidDeparture = session.State;
        Assert.Throws<InvalidDataException>(() => session.RequestField(159, 0));
        Assert.Same(invalidDeparture, session.State);
    }
}
