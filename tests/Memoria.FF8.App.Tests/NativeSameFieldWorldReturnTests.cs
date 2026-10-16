using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeSameFieldWorldReturnTests
{
    [Fact]
    public void IncorrectActiveVmFromThePreservedMalformedReturnStillRefuses()
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "AGENTS.md")))
        {
            root = Directory.GetParent(root)!.FullName;
        }
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => GameSession.Load(Path.Combine(root, "../fixtures/external-fixture.json")));
        Assert.Contains("BYTE86", error.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ActualFootReturnTo547KeepsDormantVisitSeparateAndCopiesCurrentEntityIntoFreshAllocation(bool refreshDormantProcessOwners)
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "AGENTS.md")))
        {
            root = Directory.GetParent(root)!.FullName;
        }
        GameSession session = new(GameSession.Load(Path.Combine(root, "../fixtures/external-fixture.json")));
        using GameWorld world = new(session, @"C:\Steam\steamapps\common\FINAL FANTASY VIII");
        world.EnsureField();
        Assert.Equal((byte)48, world.Events.Scene.PreviousWorldEntityByte86);
        JsonElement frozenWorldDeparture = world.Events.Capture();
        Assert.True(world.Apply(new(PlayerActionKind.Move, Horizontal: -0.8226757f, Vertical: -0.56851095f)).Accepted);
        Assert.Equal(SceneKind.Field, session.State.Scene);
        Assert.Equal(547, session.State.FieldId);
        Assert.Equal((byte)0, session.State.WorldConfiguration!.EntityByte);
        Assert.False(world.HasCurrentFieldAllocation);
        Assert.Null(session.State.EventState);
        string snapshot = Path.Combine(Path.GetTempPath(), "Memoria-FF8-same-field-return-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            //Same branch as headless Save before the pending field allocation.
            session.SetDepartingFieldState(refreshDormantProcessOwners ? world.Events.Capture() : frozenWorldDeparture);
            session.Save(snapshot);
            GameSession coldSession = new(GameSession.Load(snapshot));
            using GameWorld cold = new(coldSession, @"C:\Steam\steamapps\common\FINAL FANTASY VIII");
            cold.EnsureField();
            world.EnsureField();
            Assert.True(world.HasCurrentFieldAllocation);
            Assert.Equal((byte)0, world.Events.Scene.PreviousWorldEntityByte86);
            Assert.Equal((byte)0, cold.Events.Scene.PreviousWorldEntityByte86);
            Assert.True(world.Apply(new(PlayerActionKind.Wait, Frames: 2)).Accepted);
            Assert.True(cold.Apply(new(PlayerActionKind.Wait, Frames: 2)).Accepted);
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), cold.Events.Capture()));
            session.SetEventState(world.Events.Capture());
            session.Save(snapshot);
            GameSession restoredSession = new(GameSession.Load(snapshot));
            using GameWorld restored = new(restoredSession, @"C:\Steam\steamapps\common\FINAL FANTASY VIII");
            restored.EnsureField();
            Assert.True(world.Apply(new(PlayerActionKind.Wait)).Accepted);
            Assert.True(restored.Apply(new(PlayerActionKind.Wait)).Accepted);
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
            //The next original foot packet's loader keeps live Garden parking,
            //independently of whether the room's recon story has completed.
            using PreparedWorldEntry next = WorldEntryPreflight.Prepare(session.State, @"C:\Steam\steamapps\common\FINAL FANTASY VIII", new(24, 0, -1));
            Assert.Equal((byte)0, next.InitialState.NativeFootRuntime!.EntityType);
            NativeGardenWorldRuntimeState nextGarden = next.InitialState.NativeGardenRuntime!;
            Assert.Equal(new NativeWorldPathPosition(20360, 200, 3850), nextGarden.Objects[nextGarden.Allocation.Garden64Index].Position);
            Assert.Equal(0, next.InitialEventMemory.Read(269, 2).RequireExact());
        }
        finally
        {
            File.Delete(snapshot);
        }
    }
}
