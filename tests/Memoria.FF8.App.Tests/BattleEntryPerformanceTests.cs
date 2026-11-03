using System.Diagnostics;
using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleEntryPerformanceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Standalone_field_enters_battle_and_preserves_its_first_callback(bool prepareOnWorker)
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_BATTLE_ENTRY_SNAPSHOT");
        Assert.SkipUnless(source is not null && File.Exists(source), "An optional standalone before battle entry is required.");
        long started = Stopwatch.GetTimestamp();
        GameSession session = new(GameSession.Load(source!));
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        double loadMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        for (int tick = 0; tick < 1500 && !world.HasPendingBattle; tick++)
        {
            DialogueState? dialogue = world.Events.CurrentDialogue;
            PlayerAction input = dialogue?.FirstChoice is int choice ? new(PlayerActionKind.Choose, choice) : dialogue is { CanConfirm: true } ? new(PlayerActionKind.Confirm) : world.Events.NextTrigger.Kind == PlayerTriggerKind.Playable ? new(PlayerActionKind.Move, Vertical: -1) : new(PlayerActionKind.Wait);
            world.Apply(input);
        }
        Assert.True(world.HasPendingBattle);
        Assert.Null(world.Battle);
        long fieldFrame = world.Events.Frame;
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_PRODUCTION_EFFECT_EVIDENCE");
        string mode = prepareOnWorker ? "worker" : "synchronous";
        string id = "entry-" + mode + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff");
        session.SetEventState(world.Events.Capture());
        NativeBattleProductionEffectAdmissionTests.Publish(evidence, id + "-before-battle", session.State);
        started = Stopwatch.GetTimestamp();
        long allocated = GC.GetTotalAllocatedBytes();
        if (prepareOnWorker)
        {
            ushort flags = world.CaptureBattleEntryFlags();
            NativeBattleEntryTransition transition = world.CreateBattleEntryTransition();
            int entryMusicReceipts = 0;
            world.Events.AudioCommandExecuted += command =>
            {
                if (command is NativeBattleMusicEntryReceipt)
                {
                    entryMusicReceipts++;
                }
            };
            NativeBattleMusicEntryPreparation music = world.BeginBattleEntryMusic(session.State.PendingBattleEncounter!.Formation, flags);
            Assert.Equal(1, entryMusicReceipts);
            GameState suspended = session.State;
            JsonElement vm = world.Events.Capture();
            Assert.Throws<InvalidOperationException>(() => world.PrepareCommittedBattleEntry(suspended.PendingBattleEncounter!.Formation, flags, 0, suspended.GameplayPreferences.AtbMode, music with { Receipt = music.Receipt with { } }));
            GameWorld.PreparedBattleEntry entry = await Task.Run(() => world.PrepareCommittedBattleEntry(suspended.PendingBattleEncounter!.Formation, flags, 0, suspended.GameplayPreferences.AtbMode, music));
            Assert.Null(world.Battle);
            Assert.Same(suspended, session.State);
            Assert.True(JsonElement.DeepEquals(vm, world.Events.Capture()));
            Assert.Equal(0, entry.Battle.NativeHostCallbacks);
            Assert.Equal(82, transition.CompletionCounter);
            world.PublishPreparedBattleEntry(entry);
            Assert.Equal(1, entryMusicReceipts);
            Assert.Same(music.Receipt, world.Events.Scene.NativeBattleMusicEntry);
        }
        else
        {
            world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: session.State.GameplayPreferences.AtbMode, NativeSeed: 0));
        }
        double startMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        long startBytes = GC.GetTotalAllocatedBytes() - allocated;
        BattleSession active = world.Battle!;
        started = Stopwatch.GetTimestamp();
        BattleScenePresentation scene = new(world.BattleResources, active.Capture().Definition, true);
        double sceneMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        started = Stopwatch.GetTimestamp();
        active.AdvanceNativeGraphicsPresentation(TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30), TimeSpan.Zero);
        world.SynchronizeBattlePresentation();
        double callbackMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        Assert.Equal(fieldFrame, world.Events.Frame);
        Assert.Equal(1, active.NativeHostCallbacks);
        world.CaptureBattleState();
        session.SetEventState(world.Events.Capture());
        NativeBattleProductionEffectAdmissionTests.Publish(evidence, id + "-measurement", new { LoadMilliseconds = loadMilliseconds, StartMilliseconds = startMilliseconds, StartBytes = startBytes, SceneMilliseconds = sceneMilliseconds, CallbackMilliseconds = callbackMilliseconds });
        NativeBattleProductionEffectAdmissionTests.Publish(evidence, id + "-first-callback", session.State);
        using GameWorld restored = new(new GameSession(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!), TestResources.GameDirectory);
        restored.EnsureField();
        active.AdvanceNativeGraphicsPresentation(TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30), TimeSpan.Zero);
        restored.Battle!.AdvanceNativeGraphicsPresentation(TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30), TimeSpan.Zero);
        world.SynchronizeBattlePresentation();
        restored.SynchronizeBattlePresentation();
        Assert.Equal(JsonSerializer.Serialize(active.Capture()), JsonSerializer.Serialize(restored.Battle.Capture()));
    }
}
