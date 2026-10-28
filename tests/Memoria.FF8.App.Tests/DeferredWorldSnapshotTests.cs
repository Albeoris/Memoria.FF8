using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class DeferredWorldSnapshotTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DirectSavePreparesItsSnapshotOnceWithoutPublishingItToTheLiveSession(bool compact)
    {
        GameSession session = GameSession.CreateColdProcess();
        GameState live = session.State;
        int preparations = 0;
        using IDisposable binding = session.BindSnapshotPreparation(state =>
        {
            preparations++;
            return state with { FieldName = "Prepared snapshot" };
        });
        string path = Path.Combine(Path.GetTempPath(), "Memoria.FF8-snapshot-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.Save(path, compact);
            Assert.Equal(1, preparations);
            Assert.Same(live, session.State);
            Assert.Equal("Prepared snapshot", GameSession.Load(path).FieldName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void DisposingAnOlderAllocationDoesNotUnbindItsReplacement()
    {
        GameSession session = GameSession.CreateColdProcess();
        using IDisposable original = session.BindSnapshotPreparation(state => throw new InvalidOperationException("The replaced allocation must not prepare snapshots."));
        IDisposable replacement = session.BindSnapshotPreparation(state => state with { FieldName = "Replacement" });
        original.Dispose();
        Assert.Equal("Replacement", session.PrepareSnapshot().FieldName);
        replacement.Dispose();
        Assert.Same(session.State, session.PrepareSnapshot());
    }

    [Fact]
    public void CompletingAReplacedAllocationDoesNotPrepareOrPublishEitherAllocation()
    {
        GameSession session = GameSession.CreateColdProcess();
        GameState live = session.State;
        using IDisposable original = session.BindSnapshotPreparation(_ => throw new InvalidOperationException("A replaced allocation must not prepare snapshots."));
        using IDisposable replacement = session.BindSnapshotPreparation(_ => throw new InvalidOperationException("Completing another allocation must not prepare the current allocation."));
        session.CompleteSnapshotPreparation(original);
        Assert.Same(live, session.State);
        Assert.Throws<InvalidOperationException>(() => session.PrepareSnapshot());
    }

    [Fact]
    public void FailedFinalPreparationReleasesItsBindingAndPreservesTheLiveState()
    {
        GameSession session = GameSession.CreateColdProcess();
        GameState live = session.State;
        using IDisposable binding = session.BindSnapshotPreparation(_ => throw new InvalidOperationException("Preparation failed."));
        Assert.Throws<InvalidOperationException>(() => session.CompleteSnapshotPreparation(binding));
        Assert.Same(live, session.State);
        Assert.Same(live, session.PrepareSnapshot());
    }

    [Fact]
    public void AbandoningAFaultedAllocationPreservesPublishedStateWithoutPreparingIt()
    {
        GameSession session = GameSession.CreateColdProcess();
        GameState published = session.State;
        using IDisposable binding = session.BindSnapshotPreparation(_ => throw new InvalidOperationException("The faulted allocation cannot be captured."));
        session.AbandonSnapshotPreparation(binding);
        session.CompleteSnapshotPreparation(binding);
        Assert.Same(published, session.State);
        Assert.Same(published, session.PrepareSnapshot());
    }

    [Fact]
    public void AbandoningAReplacedAllocationPreservesItsReplacement()
    {
        GameSession session = GameSession.CreateColdProcess();
        using IDisposable original = session.BindSnapshotPreparation(_ => throw new InvalidOperationException("Replaced allocation."));
        using IDisposable replacement = session.BindSnapshotPreparation(state => state with { FieldName = "Replacement" });
        session.AbandonSnapshotPreparation(original);
        Assert.Equal("Replacement", session.PrepareSnapshot().FieldName);
        session.CompleteSnapshotPreparation(replacement);
        Assert.Equal("Replacement", session.State.FieldName);
    }

    [Fact]
    public void SnapshotPreparationCannotReenterItselfAndRecoversAfterFailure()
    {
        GameSession session = GameSession.CreateColdProcess();
        IDisposable binding = session.BindSnapshotPreparation(_ => session.PrepareSnapshot());
        Assert.Throws<InvalidOperationException>(() => session.PrepareSnapshot());
        binding.Dispose();
        Assert.Same(session.State, session.PrepareSnapshot());
    }

    [Fact]
    public void OrdinaryWorldCallbacksRetainTheFrozenVmAndDirectSaveCapturesCurrentOwners()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_WORLD_PERFORMANCE_SNAPSHOT");
        Assert.SkipUnless(source is not null && File.Exists(source), "An optional world standalone is required.");
        GameSession session = new(GameSession.Load(source!));
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        string frozen = session.State.EventState!.Value.GetRawText();
        long fieldFrame = world.Events.Frame;
        long initialFrame = session.State.World!.Frame;
        for (int callback = 0; callback < 4; callback++)
        {
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
            Assert.Equal(frozen, session.State.EventState.Value.GetRawText());
        }
        Assert.Equal(initialFrame + 4, session.State.World!.Frame);
        Assert.Equal(fieldFrame, world.Events.Frame);
        NativeEconomy economy = session.State.Economy!;
        session.SetEconomy(economy with { Gil = economy.Gil == NativeEconomy.GilCeiling ? economy.Gil - 1 : economy.Gil + 1 });
        Assert.Equal(frozen, session.State.EventState.Value.GetRawText());
        GameState live = session.State;
        string path = Path.Combine(Path.GetTempPath(), "Memoria.FF8-world-snapshot-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.Save(path, compact: true);
            Assert.Same(live, session.State);
            GameSession resumed = new(GameSession.Load(path));
            Assert.Equal(session.State.Economy!.Gil, resumed.State.Economy!.Gil);
            Assert.Equal(session.State.WorldControls, resumed.State.WorldControls);
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State.ExamTimer), JsonSerializer.SerializeToElement(resumed.State.ExamTimer)));
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State.EventMemory), JsonSerializer.SerializeToElement(resumed.State.EventMemory)));
            Assert.Equal(fieldFrame, resumed.State.EventState!.Value.GetProperty("Frame").GetInt64());
            using GameWorld restored = new(resumed, TestResources.GameDirectory);
            restored.EnsureField();
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
            Assert.True(restored.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State.World), JsonSerializer.SerializeToElement(resumed.State.World)));
            Assert.True(JsonElement.DeepEquals(session.PrepareSnapshot().EventState!.Value, resumed.PrepareSnapshot().EventState!.Value));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void DisposingTheWorldRetainsCurrentOwnersForALaterDirectSave()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_WORLD_PERFORMANCE_SNAPSHOT");
        Assert.SkipUnless(source is not null && File.Exists(source), "An optional world standalone is required.");
        GameSession session = new(GameSession.Load(source!));
        using (GameWorld world = new(session, TestResources.GameDirectory))
        {
            world.EnsureField();
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
            NativeEconomy economy = session.State.Economy!;
            session.SetEconomy(economy with { Gil = economy.Gil == NativeEconomy.GilCeiling ? economy.Gil - 1 : economy.Gil + 1 });
        }
        GameState completed = session.State;
        Assert.Same(completed, session.PrepareSnapshot());
        string path = Path.Combine(Path.GetTempPath(), "Memoria.FF8-disposed-world-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.Save(path, compact: true);
            GameState restored = GameSession.Load(path);
            Assert.Equal(completed.Economy!.Gil, restored.Economy!.Gil);
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(completed.ExamTimer), JsonSerializer.SerializeToElement(restored.ExamTimer)));
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(restored.Economy), restored.EventState!.Value.GetProperty("Economy")));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
