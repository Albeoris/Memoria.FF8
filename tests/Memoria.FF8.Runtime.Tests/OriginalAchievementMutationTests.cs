using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class OriginalAchievementMutationTests
{
    [Theory]
    [InlineData(0, 0, 1, 1)]
    [InlineData(2, 0, 1, 1)]
    [InlineData(4, 0, 1, 1)]
    [InlineData(6, 0, 1, 1)]
    [InlineData(0, 1, 2, 1)]
    [InlineData(2, 1, 2, 0)]
    [InlineData(4, 1, 2, 0)]
    [InlineData(6, 1, 2, 0)]
    [InlineData(0, 2, 3, 0)]
    [InlineData(0, 1, 0, 0)]
    [InlineData(0, 1, 1, 0)]
    public void DrawPointUsesOriginalUnshiftedPreviousMask(int shift, int before, int after, int count)
    {
        GameSession session = Session((byte)(before << shift));
        SetFirstByte(session, (byte)(after << shift));
        Assert.Equal(count, session.State.PendingOriginalAchievementActions.Length);
        Assert.All(session.State.PendingOriginalAchievementActions, fact => Assert.Equal(OriginalAchievementActionKind.DrawPoint, fact.Kind));
    }

    [Fact]
    public void OneAcceptedBankMutationStagesEveryQualifyingFieldOnce()
    {
        GameSession session = Session();
        AchievementOwner owner = new("test", "original", "player", "profile");
        session.CaptureAchievementOwner = () => owner;
        NativeEventRandomState random = session.EventRandom!;
        NativeDrawPointMemory bank = random.DrawPoints!.Write(116, 1, 0x55).Write(179, 1, 0xC0);
        session.SetEventRandom(random with { DrawPoints = bank });
        Assert.Equal(5, session.State.PendingOriginalAchievementActions.Length);
        Assert.Equal(5, session.State.OriginalAchievementMutationSequence);
        Assert.All(session.State.PendingOriginalAchievementActions, fact => Assert.Equal(owner, fact.Owner));
        session.SetEventRandom(session.EventRandom!);
        Assert.Equal(5, session.State.PendingOriginalAchievementActions.Length);
    }

    [Fact]
    public void RegeneratedPointGetsNewIdentityAfterAcknowledgementAndRestore()
    {
        GameSession session = Session();
        SetFirstByte(session, 1);
        OriginalAchievementActionEvent first = Assert.Single(session.State.PendingOriginalAchievementActions);
        session.AcknowledgeOriginalAchievementActions([first]);
        SetFirstByte(session, 0);
        Assert.Empty(session.State.PendingOriginalAchievementActions);
        GameState checkpoint = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!;
        GameSession restored = new(checkpoint);
        SetFirstByte(restored, 1);
        OriginalAchievementActionEvent second = Assert.Single(restored.State.PendingOriginalAchievementActions);
        Assert.NotEqual(first.ContextId, second.ContextId);
        Assert.Equal(1, restored.State.OriginalAchievementMutationSequence);
    }

    [Fact]
    public void DrawExecutedAgainAfterRollbackGetsNewBranchIdentity()
    {
        GameSession session = Session();
        SetFirstByte(session, 1);
        session.AcknowledgeOriginalAchievementActions(session.State.PendingOriginalAchievementActions);
        SetFirstByte(session, 0);
        GameState earlier = session.State;
        SetFirstByte(session, 1);
        OriginalAchievementActionEvent first = Assert.Single(session.State.PendingOriginalAchievementActions);
        session.Restore(earlier);
        SetFirstByte(session, 1);
        Assert.Equal(2, session.State.PendingOriginalAchievementActions.Length);
        OriginalAchievementActionEvent repeated = session.State.PendingOriginalAchievementActions[1];
        Assert.NotEqual(first.ContextId, repeated.ContextId);
        Assert.Equal(1, session.State.OriginalAchievementMutationSequence);
        Assert.Equal(OriginalAchievementActionKind.DrawPoint, repeated.Kind);
    }

    [Fact]
    public void EarlierSerializedCheckpointDoesNotReuseCommittedDrawIdentity()
    {
        GameSession session = Session();
        SetFirstByte(session, 1);
        session.AcknowledgeOriginalAchievementActions(session.State.PendingOriginalAchievementActions);
        SetFirstByte(session, 0);
        string checkpoint = JsonSerializer.Serialize(session.State);
        SetFirstByte(session, 1);
        OriginalAchievementActionEvent first = Assert.Single(session.State.PendingOriginalAchievementActions);
        session.AcknowledgeOriginalAchievementActions([first]);
        GameSession restarted = new(JsonSerializer.Deserialize<GameState>(checkpoint)!);
        SetFirstByte(restarted, 1);
        OriginalAchievementActionEvent repeated = Assert.Single(restarted.State.PendingOriginalAchievementActions);
        Assert.NotEqual(first.ContextId, repeated.ContextId);
        Assert.Equal(1, restarted.State.OriginalAchievementMutationSequence);
    }

    [Fact]
    public void RollbackPreservesPendingDrawBeforeNewSalaryReceipt()
    {
        GameSession session = Session();
        SetFirstByte(session, 1);
        session.AcknowledgeOriginalAchievementActions(session.State.PendingOriginalAchievementActions);
        SetFirstByte(session, 0);
        GameState earlier = session.State;
        SetFirstByte(session, 1);
        OriginalAchievementActionEvent draw = Assert.Single(session.State.PendingOriginalAchievementActions);
        session.Restore(earlier);
        session.RecordOriginalSalaryPayment();
        Assert.Equal(draw, session.State.PendingOriginalAchievementActions[0]);
        OriginalAchievementActionEvent salary = session.State.PendingOriginalAchievementActions[1];
        Assert.Equal(OriginalAchievementActionKind.FirstSalary, salary.Kind);
        Assert.NotEqual(draw.ContextId, salary.ContextId);
    }

    [Fact]
    public void GeneratorAndRegenerationCounterChangesDoNotInventDrawActions()
    {
        GameSession session = Session();
        NativeEventRandomState random = session.EventRandom!.Draw(out _);
        session.SetEventRandom(random with { DrawPoints = random.DrawPoints!.Write(180, 2, 123) });
        Assert.Empty(session.State.PendingOriginalAchievementActions);
        Assert.Null(session.State.OriginalAchievementMutationContext);
        Assert.Equal(0, session.State.OriginalAchievementMutationSequence);
    }

    private static GameSession Session(byte initial = 0)
    {
        GameSession cold = GameSession.CreateColdProcess();
        cold.StartNewGame();
        NativeEventRandomState random = cold.EventRandom!;
        return new GameSession(cold.State with { EventRandom = random with { DrawPoints = random.DrawPoints!.Write(116, 1, initial) } });
    }

    private static void SetFirstByte(GameSession session, byte value)
    {
        NativeEventRandomState random = session.EventRandom!;
        session.SetEventRandom(random with { DrawPoints = random.DrawPoints!.Write(116, 1, value) });
    }
}
