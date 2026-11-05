using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class WorldFootVehicleHandoffTests
{
    [Fact]
    public void SelectedFootIdentitySurvivesCarDismountSnapshotsAndReboarding()
    {
        GameSession session = new(GameSession.Load(TestResources.FixturePath("world-car-selected-foot")));
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        Assert.Equal((byte)32, session.State.World!.NativeCarRuntime!.EntityType);
        Assert.Equal((byte)6, session.State.World.NativeCarRuntime.FootEntityType);
        Assert.True(world.Apply(new(PlayerActionKind.Wait, Frames: 24)).Accepted);
        for (int attempt = 0; attempt < 100 && world.CurrentDialogue is not null; attempt++)
        {
            Assert.True(world.Apply(new(PlayerActionKind.WorldButtons, 0x40)).Accepted);
            Assert.True(world.Apply(new(PlayerActionKind.Wait)).Accepted);
        }
        Assert.Null(world.CurrentDialogue);
        Assert.True(world.Apply(new(PlayerActionKind.Wait, Frames: 24)).Accepted);
        Assert.True(world.Apply(new(PlayerActionKind.WorldButtons, 0x40)).Accepted);
        Assert.Null(session.State.World.NativeCarRuntime);
        Assert.Equal((byte)6, session.State.World.NativeFootRuntime!.EntityType);
        Assert.NotEmpty(world.WorldScene!.Render(320, 224));

        string directory = Path.Combine(Path.GetTempPath(), "Memoria.FF8-selected-foot-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string snapshot = Path.Combine(directory, "foot.ff8");
            session.Save(snapshot, compact: true);
            GameSession restored = new(GameSession.Load(snapshot));
            using GameWorld replay = new(restored, TestResources.GameDirectory);
            replay.EnsureField();
            Assert.Equal((byte)6, restored.State.World!.NativeFootRuntime!.EntityType);
            Assert.NotEmpty(replay.WorldScene!.Render(320, 224));
            Assert.True(replay.Apply(new(PlayerActionKind.Wait, Frames: 200)).Accepted);
            (int X, int Z) beforeMove = (restored.State.World.X, restored.State.World.Z);
            Assert.True(replay.Apply(new(PlayerActionKind.Move, Horizontal: 1)).Accepted);
            Assert.NotEqual(beforeMove, (restored.State.World.X, restored.State.World.Z));
            Assert.True(replay.Apply(new(PlayerActionKind.WorldButtons, 0x40)).Accepted);
            Assert.Equal((byte)32, restored.State.World.NativeCarRuntime!.EntityType);
            Assert.Equal((byte)6, restored.State.World.NativeCarRuntime.FootEntityType);
            Assert.True(replay.Apply(new(PlayerActionKind.WorldButtons, 2)).Accepted);
            Assert.Equal((byte)1, restored.State.World.NativeCarRuntime.AnimationVariant);
            Assert.NotEmpty(replay.WorldScene.Render(320, 224));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
