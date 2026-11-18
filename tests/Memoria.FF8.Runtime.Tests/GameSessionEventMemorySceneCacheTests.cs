using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class GameSessionEventMemorySceneCacheTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void TimerContinuationKeepsDetachedSceneMemoryAndRejectsChangedOwnerRegistrations(int role)
    {
        GameSession session = GameSession.CreateColdProcess();
        NativeEventMemory memory = NativeEventMemory.Empty.Write(201, 1, 0);
        session.SetEventMemory(memory);
        using (JsonDocument document = JsonDocument.Parse(Snapshot(role, new { }, memory).GetRawText()))
        {
            SetSnapshot(session, role, document.RootElement);
        }
        for (int callback = 0; callback < 3; callback++)
        {
            session.SetExamTimer(session.ExamTimer!.AdvanceCallbacks(NativeTimerCallbackKind.Battle, 1));
        }
        GameState before = session.State;
        Assert.Throws<InvalidDataException>(() => SetSnapshot(session, role, Snapshot(role, new { MusicProcess = new { } }, memory)));
        Assert.Same(before, session.State);
        Assert.Throws<InvalidDataException>(() => SetSnapshot(session, role, Snapshot(role, new { DrawPointFieldStateVersion = 2 }, memory)));
        Assert.Same(before, session.State);
        session.SetExamTimer(session.ExamTimer!.AdvanceCallbacks(NativeTimerCallbackKind.Battle, 1));
        Assert.Equal(0, session.EventMemory.Read(201, 1).RequireExact());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void CachedCapturedMemoryIsValidatedAgainstEachNewTimerOwner(int role)
    {
        GameSession session = GameSession.CreateColdProcess();
        SetSnapshot(session, role, Snapshot(role, new { }, NativeEventMemory.Empty.Write(205, 2, 7)));
        session.SetExamTimer(session.ExamTimer!);
        GameState before = session.State;
        Assert.Throws<InvalidDataException>(() => session.SetExamTimer(session.ExamTimer!.SetRemaining(60)));
        Assert.Same(before, session.State);
        Assert.False(session.ExamTimer!.TimerMemoryActivated);
    }

    [Fact]
    public void CachedSceneProjectionStillChecksTheCurrentLiveOwnerProvider()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.SetEventMemory(NativeEventMemory.Empty.Write(201, 1, 0));
        session.SetEventState(Snapshot(1, new { }, session.EventMemory));
        NativeEventSceneMemoryOwners live = NativeEventSceneMemoryOwners.None;
        session.BindEventMemorySceneOwners(() => live);
        session.SetExamTimer(session.ExamTimer!);
        live = NativeEventSceneMemoryOwners.MusicProcess;
        GameState before = session.State;
        Assert.Throws<InvalidDataException>(() => session.SetExamTimer(session.ExamTimer!));
        Assert.Same(before, session.State);
        live = NativeEventSceneMemoryOwners.None;
        session.SetExamTimer(session.ExamTimer!);
        live = (NativeEventSceneMemoryOwners)(1 << 20);
        Assert.Throws<InvalidDataException>(() => session.SetExamTimer(session.ExamTimer!));
    }

    [Fact]
    public void OwnerOnlyAndReplacedSnapshotsDoNotRetainAnEarlierSceneOwnerProjection()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.SetEventState(Snapshot(1, new { MusicProcess = new { } }, NativeEventMemory.Empty));
        Assert.Throws<InvalidDataException>(() => session.SetEventMemory(NativeEventMemory.Empty.Write(201, 1, 0)));
        session.SetEventState(JsonSerializer.SerializeToElement(new { Version = 1 }));
        session.SetEventMemory(NativeEventMemory.Empty.Write(201, 1, 0));
        session.SetEventState(Snapshot(1, new { }, session.EventMemory));
        session.SetExamTimer(session.ExamTimer!);
        GameState before = session.State;
        Assert.Throws<InvalidDataException>(() => session.SetEventState(JsonSerializer.SerializeToElement(new { Scene = new { } })));
        Assert.Same(before, session.State);
        session.SetEventState(null);
        session.SetExamTimer(session.ExamTimer!);
    }

    private static JsonElement Snapshot(int role, object scene, NativeEventMemory memory)
    {
        return role == 0
            ? JsonSerializer.SerializeToElement(new { Version = 1, FieldId = 0, Scene = scene, EventMemory = memory })
            : JsonSerializer.SerializeToElement(new { Scene = scene, EventMemory = memory });
    }

    private static void SetSnapshot(GameSession session, int role, JsonElement snapshot)
    {
        switch (role)
        {
            case 0:
                session.SetWorldEventState(snapshot);
                break;
            case 1:
                session.SetEventState(snapshot);
                break;
            case 2:
                session.SetDepartingFieldState(snapshot);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(role));
        }
    }
}
