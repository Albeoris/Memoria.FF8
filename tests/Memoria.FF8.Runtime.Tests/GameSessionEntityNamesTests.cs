namespace Memoria.FF8.Runtime.Tests;

public sealed class GameSessionEntityNamesTests
{
    [Fact]
    public void Empty_acquisitions_preserve_the_current_state_and_do_not_publish_a_change()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        session.EnqueueEntityNames([new PendingEntityName(2, "Angelo", "item:66")]);
        GameState before = session.State;
        int publications = 0;
        session.StateChanged += _ => publications++;

        session.EnqueueEntityNames(Enumerable.Empty<PendingEntityName>());

        Assert.Same(before, session.State);
        Assert.Equal(0, publications);
        Assert.Single(session.State.PendingEntityNames!);
    }

    [Fact]
    public void Acquisitions_still_validate_and_publish_the_ordered_queue_atomically()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        PendingEntityName first = new(2, "Angelo", "item:66");
        PendingEntityName second = new(GameNames.BokoSubject, "Boko", "item:67");
        session.EnqueueEntityNames([first]);
        GameState before = session.State;
        Assert.Throws<InvalidDataException>(() => session.EnqueueEntityNames([new PendingEntityName(-1, "", "invalid")]));
        Assert.Same(before, session.State);
        int publications = 0;
        session.StateChanged += _ => publications++;

        session.EnqueueEntityNames([second]);

        Assert.Equal(new[] { first, second }, session.State.PendingEntityNames);
        Assert.Equal(1, publications);
    }
}
