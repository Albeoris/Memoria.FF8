using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class OriginalAchievementObservationTests
{
    private static readonly AchievementOwner Owner = new("test", "original", "player", "profile");

    [Fact]
    public void ImmutableReceiptHasDeterministicIdentityAndRejectsTampering()
    {
        OriginalAchievementProgress progress = new() { CardWins = 99, MaximumGil = false };
        OriginalAchievementObservedEvent fact = OriginalAchievementObservedEvent.Create(progress, Owner);
        Assert.Equal(fact, OriginalAchievementObservedEvent.Create(progress, Owner));
        fact.Validate();
        Assert.Throws<InvalidDataException>(() => (fact with { Progress = progress with { CardWins = 100 } }).Validate());
        Assert.Throws<InvalidDataException>(() => (fact with { Owner = Owner with { User = "other" } }).Validate());
    }

    [Fact]
    public void UnknownStateAndTitleDoNotStageAnObservation()
    {
        GameSession session = new(new GameState { Scene = SceneKind.Field });
        session.ObserveOriginalAchievements();
        Assert.Empty(session.State.PendingOriginalAchievementObservations);
        Assert.Null(session.State.LastOriginalAchievementObservation);
        GameSession title = new(new GameState { Scene = SceneKind.Title, CardInventory = NativeCardInventory.CreateNewGame() });
        title.ObserveOriginalAchievements();
        Assert.Empty(title.State.PendingOriginalAchievementObservations);
    }

    [Fact]
    public void UnchangedStateUsesCachedReceiptIncludingAfterAcknowledgement()
    {
        GameSession session = Session();
        session.ObserveOriginalAchievements();
        OriginalAchievementObservedEvent fact = Assert.Single(session.State.PendingOriginalAchievementObservations);
        session.ObserveOriginalAchievements();
        Assert.Single(session.State.PendingOriginalAchievementObservations);
        session.AcknowledgeOriginalAchievementObservations([fact with { Owner = Owner with { User = "other" } }]);
        Assert.Single(session.State.PendingOriginalAchievementObservations);
        session.AcknowledgeOriginalAchievementObservations([fact]);
        Assert.Empty(session.State.PendingOriginalAchievementObservations);
        session.ObserveOriginalAchievements();
        Assert.Empty(session.State.PendingOriginalAchievementObservations);
        Assert.Equal(fact, session.State.LastOriginalAchievementObservation);
        GameState checkpoint = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!;
        GameSession restarted = new(checkpoint) { CaptureAchievementOwner = () => Owner };
        restarted.ObserveOriginalAchievements();
        Assert.Empty(restarted.State.PendingOriginalAchievementObservations);
        Assert.Equal(fact, restarted.State.LastOriginalAchievementObservation);
    }

    [Fact]
    public void OwnerSwitchCreatesSeparatePinnedReceipt()
    {
        GameSession session = Session();
        session.ObserveOriginalAchievements();
        OriginalAchievementObservedEvent first = Assert.Single(session.State.PendingOriginalAchievementObservations);
        AchievementOwner other = Owner with { User = "other" };
        session.CaptureAchievementOwner = () => other;
        session.ObserveOriginalAchievements();
        Assert.Equal(2, session.State.PendingOriginalAchievementObservations.Length);
        OriginalAchievementObservedEvent second = session.State.PendingOriginalAchievementObservations[1];
        Assert.Equal(Owner, first.Owner);
        Assert.Equal(other, second.Owner);
        Assert.Equal(first.EventId, second.EventId);
        Assert.NotEqual(first.ContextId, second.ContextId);
        session.CaptureAchievementOwner = () => Owner;
        session.ObserveOriginalAchievements();
        Assert.Equal(2, session.State.PendingOriginalAchievementObservations.Length);
    }

    [Fact]
    public void PendingObservationSurvivesRollbackAndSerializedCheckpoint()
    {
        GameSession session = Session();
        GameState earlier = session.State;
        session.ObserveOriginalAchievements();
        OriginalAchievementObservedEvent fact = Assert.Single(session.State.PendingOriginalAchievementObservations);
        session.Restore(earlier);
        Assert.Equal(fact, Assert.Single(session.State.PendingOriginalAchievementObservations));
        GameState checkpoint = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!;
        GameSession restarted = new(checkpoint) { CaptureAchievementOwner = () => Owner };
        Assert.Equal(fact, Assert.Single(restarted.State.PendingOriginalAchievementObservations));
        restarted.ObserveOriginalAchievements();
        Assert.Single(restarted.State.PendingOriginalAchievementObservations);
        restarted.StartNewGame();
        Assert.Equal(fact, Assert.Single(restarted.State.PendingOriginalAchievementObservations));
    }

    private static GameSession Session()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        session.CaptureAchievementOwner = () => Owner;
        return session;
    }
}
