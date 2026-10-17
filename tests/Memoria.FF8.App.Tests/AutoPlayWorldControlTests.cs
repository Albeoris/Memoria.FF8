using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayWorldControlTests
{
    [Theory]
    [InlineData(0, 0, false, 24)]
    [InlineData(23, 0, false, 1)]
    [InlineData(24, 0, false, 0)]
    [InlineData(24, 14, false, 1)]
    [InlineData(24, 0, true, 1)]
    public void EntryHoldAndBothNativeUiOwnersPreventPlanning(long frame, byte mode, bool message, int expectedWait)
    {
        NativeWorldEventUi ui = NativeWorldEventUi.Create().WithScriptMode(mode);
        if (message)
        {
            ui = ui.Show(new NativeWorldMessage(9, 49, 0, 0, 0, 0, 0, new string('A', 64), false));
        }
        Assert.Equal(expectedWait, AutoPlayController.WorldControlWaitFrames(new WorldState { Frame = frame, EventUi = ui }));
    }

    [Fact]
    public void MissingNativeUiAuthorityCannotBeTreatedAsFreeControl()
    {
        Assert.Throws<NotSupportedException>(() => AutoPlayController.WorldControlWaitFrames(new WorldState { Frame = 24 }));
    }

    [Fact]
    public void WorldConditionsExcludeSpatialAndTransientUiState()
    {
        GameState original = InitialState();
        string signature = AutoPlayController.WorldConditionSignature(original);
        GameState changed = original with
        {
            World = original.World! with { Frame = 123, X = 987, Z = -654, BodyYaw = 700, CameraYaw = 1200, AnimationCounter = 9, EventUi = NativeWorldEventUi.Create().WithScriptMode(14) },
            SavedWorld = original.SavedWorld! with { Slot4 = original.SavedWorld.Slot4 with { Pose = new NativeSavedWorldPose(12, -34, -56, 78) }, FuelDistance112 = 123 }
        };
        Assert.Equal(signature, AutoPlayController.WorldConditionSignature(changed));
        Assert.Equal(signature, AutoPlayController.WorldConditionSignature(original with { WorldEntryMemory = original.WorldEntryMemory! with { Origin = NativeWorldEntryMemoryOrigin.ExecutedFieldClear } }));
    }

    [Fact]
    public void WorldConditionsPreserveTypedPersistentValuesAndUnknownAuthority()
    {
        GameState original = InitialState();
        string signature = AutoPlayController.WorldConditionSignature(original);
        GameState[] changes =
        [
            original with { WorldEntryMemory = original.WorldEntryMemory!.WriteBytes(256, 2, 40) },
            original with { WorldEntryMemory = original.WorldEntryMemory!.WriteBytes(264, 1, 1) },
            original with { SavedWorld = original.SavedWorld! with { WorldEventFlags116 = original.SavedWorld.WorldEventFlags116!.WithOperand(0x100) } },
            original with { SavedWorld = original.SavedWorld! with { WorldEventFlags116 = new NativeWorldEventFlags(0, 0) } },
            original with { SavedWorld = original.SavedWorld! with { WorldEventVariable124 = 1 } },
            original with { SavedWorld = original.SavedWorld! with { WorldEventVariable125 = null } },
            original with { SavedWorld = original.SavedWorld! with { Slot4 = original.SavedWorld.Slot4 with { ShapeId = 73 } } },
            original with { EventMemory = original.EventMemory.Write(271, 1, NativeScalarValue.Exact(1)) }
        ];
        foreach (GameState changed in changes)
        {
            Assert.NotEqual(signature, AutoPlayController.WorldConditionSignature(changed));
        }
        Assert.Throws<NotSupportedException>(() => AutoPlayController.WorldConditionSignature(original with { WorldEntryMemory = null }));
        Assert.Throws<NotSupportedException>(() => AutoPlayController.WorldConditionSignature(original with { SavedWorld = null }));
    }

    [Fact]
    public void RejectedWorldAttemptHasNoTravelCreditAndReopensOnlyForChangedPersistentConditions()
    {
        GameState source = InitialState();
        AutoPlayPlanner planner = new();
        AutoPlayVisit visit = new("world:0", "world");
        AutoPlayEvent entrance = new("entrance", AutoPlayEventKind.Transition, ConditionSignature: AutoPlayController.WorldConditionSignature(source));
        planner.Observe(visit, [entrance]);
        planner.BeginEvent(visit.Key, entrance.Id);
        Assert.Equal(1, AutoPlayController.WorldControlWaitFrames(source.World! with { EventUi = NativeWorldEventUi.Create().WithScriptMode(14) }));
        Assert.Null(Assert.Single(Assert.Single(planner.State.Visits).Events).BlockedReason);
        AutoPlayController.RecordWorldControlReturn(planner, visit.Key, entrance.Id, visit.Key, 0, 0);
        AutoPlayEventCoverage rejected = Assert.Single(Assert.Single(planner.State.Visits).Events);
        Assert.False(rejected.Completed);
        Assert.Null(rejected.Event.DestinationVisitKey);
        Assert.NotNull(rejected.BlockedReason);
        planner.Observe(visit, [entrance]);
        Assert.Null(planner.Plan(visit.Key));
        string changed = AutoPlayController.WorldConditionSignature(source with { EventMemory = source.EventMemory.Write(271, 1, NativeScalarValue.Exact(1)) });
        planner.Observe(visit, [entrance with { ConditionSignature = changed }]);
        Assert.Equal(entrance.Id, planner.Plan(visit.Key)!.Event.Id);
    }

    [Fact]
    public void PassiveWorldFlagSignaturePreservesMissingAndUncertainDomainsWithoutReadingAPredicate()
    {
        GameState source = InitialState();
        string missing = AutoPlayController.WorldConditionSignature(source);
        GameState zero = source with { EventMemory = source.EventMemory.Write(271, 1, NativeScalarValue.Exact(0)) };
        Assert.NotEqual(missing, AutoPlayController.WorldConditionSignature(zero));
        NativeExamTimerState timer = NativeExamTimerState.FromReachedSet(2) with { Alternatives = [new NativeTimerAlternative(0, 0, 1), new NativeTimerAlternative(1, 1, 2)] };
        NativeTimerReadOrigin origin = new(1, 999, new string('A', 64), 0, 0, 1, 2, timer);
        NativeScalarValue domain = NativeScalarValue.FromTimer(origin);
        GameState uncertain = source with { EventMemory = source.EventMemory.Write(271, 1, domain) };
        string signature = AutoPlayController.WorldConditionSignature(uncertain);
        Assert.NotEqual(missing, signature);
        Assert.NotEqual(AutoPlayController.WorldConditionSignature(zero), signature);
        GameState laterRead = source with { EventMemory = source.EventMemory.Write(271, 1, NativeScalarValue.FromTimer(origin with { Frame = 500, Operation = 99 })) };
        Assert.Equal(signature, AutoPlayController.WorldConditionSignature(laterRead));
        GameState equivalentWord = source with { EventMemory = source.EventMemory.Write(270, 2, NativeScalarValue.Exact(0)) };
        Assert.Equal(AutoPlayController.WorldConditionSignature(zero), AutoPlayController.WorldConditionSignature(equivalentWord));
    }

    [Fact]
    public void ActualCarEntryHoldAdvancesNativeCallbacksBeforeDiscoveringOrTeleporting()
    {
        string gameDirectory = TestResources.GameDirectory;
        string fixture = TestResources.FixturePath("App.Tests.AutoPlayCarWorldTests.1");
        string directory = Path.Combine(Path.GetTempPath(), "ff8-world-control-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        GameWorld? world = null;
        try
        {
            string source = Path.Combine(directory, "source.json");
            File.Copy(fixture, source);
            string manifest = Path.Combine(directory, "source.run.json");
            File.WriteAllText(manifest, JsonSerializer.Serialize(new AutoPlayRunState { Snapshot = "source.json" }, AutoPlayController.JsonOptions));
            GameSession session = GameSession.CreateColdProcess();
            GameWorld GetWorld() => world ??= new GameWorld(session, gameDirectory, validateWorldPresentation: false);
            void Restore(GameState state)
            {
                world?.Dispose();
                world = null;
                session.Restore(state);
            }
            AutoPlayController controller = new(session, GetWorld, Restore, Path.Combine(directory, "output"));
            controller.ResumeFrom(manifest);
            WorldState before = session.State.World!;
            Assert.Equal(0, before.Frame);
            controller.Advance(1);
            Assert.Null(controller.Error);
            Assert.Equal(SceneKind.WorldMap, session.State.Scene);
            Assert.Equal((before.X, before.Z), (session.State.World!.X, session.State.World.Z));
            Assert.Equal(24, session.State.World.Frame);
            Assert.Null(controller.State.ActiveEvent);
            Assert.Empty(controller.State.Coverage.Visits);
            Assert.Equal((byte)33, session.State.World.NativeCarRuntime!.EntityType);
        }
        finally
        {
            world?.Dispose();
            Directory.Delete(directory, true);
        }
    }

    private static GameState InitialState()
    {
        return new GameState { Scene = SceneKind.WorldMap, World = new WorldState { Frame = 24, EventUi = NativeWorldEventUi.Create() }, WorldEntryMemory = NativeWorldEntryMemory.CreateNewGame(), SavedWorld = NativeSavedWorldState.CreateNewGame() };
    }
}
