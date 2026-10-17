using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayTeleportPrecisionTests
{
    [Fact]
    public void NativePlacementUsesIntegerPlaneHeightAndColdRestoresItsCompletedMove()
    {
        string directory = TestResources.GameDirectory;
        GameSession session = new(GameSession.Load(TestResources.FixturePath("App.Tests.AutoPlayTeleportPrecisionTests.1")));
        using GameWorld world = new(session, directory, validateWorldPresentation: false);
        world.EnsureField();
        ActorState player = world.Events.PlayerActor!;
        NativeScriptMovementState previous = Assert.IsType<NativeScriptMovementState>(player.NativeScriptMovement);
        Assert.False(previous.Active);
        Assert.Equal((ushort)2, player.NativeMovementCompletion542);
        long frame = world.Events.Frame;
        float x = player.X + 0.25f;
        float y = -MathF.Abs(player.Y) - 0.5f;
        int fixedX = checked((int)MathF.Round(x * 4096));
        int fixedY = checked((int)MathF.Round(y * 4096));
        Assert.NotEqual(fixedY >> 12, fixedY / 4096);
        int triangle = player.WalkmeshTriangle;
        int height = FieldPartyTraceGeometry.ResolveTriangleHeight(world.Field!.Walkmesh, triangle, fixedX / 4096, fixedY / 4096)!.Value;
        Func<int, int, int, int?> originalPlane = world.Events.ResolveNativeTriangleHeight!;
        int planeReads = 0;
        world.Events.ResolveNativeTriangleHeight = (selected, nativeX, nativeY) =>
        {
            planeReads++;
            Assert.Equal(triangle, selected);
            Assert.Equal(fixedX / 4096, nativeX);
            Assert.Equal(fixedY / 4096, nativeY);
            return originalPlane(selected, nativeX, nativeY);
        };

        world.Events.TeleportPlayer(x, y, player.Z + 0.375f, triangle, (int)player.Direction);

        Assert.Equal(1, planeReads);
        world.Events.ResolveNativeTriangleHeight = originalPlane;
        Assert.Equal(frame, world.Events.Frame);
        Assert.Equal((float)height, player.Z);
        FieldFixedPosition expected = new(fixedX, fixedY, height << 12);
        Assert.Equal(expected, world.Events.Scene.NativePlayerPosition);
        NativeScriptMovementState placed = Assert.IsType<NativeScriptMovementState>(player.NativeScriptMovement);
        Assert.Equal(expected, new FieldFixedPosition(placed.FixedX, placed.FixedY, placed.FixedZ));
        Assert.Equal(0, placed.FixedZ & 4095);
        Assert.Equal(previous.TargetX, placed.TargetX);
        Assert.Equal(previous.TargetY, placed.TargetY);
        Assert.Equal(previous.TargetZ, placed.TargetZ);
        Assert.Equal(previous.Operation, placed.Operation);
        Assert.Equal(previous.FiberSequence, placed.FiberSequence);
        Assert.False(placed.Active);
        Assert.Equal((ushort)2, player.NativeMovementCompletion542);
        JsonElement saved = world.Events.Capture();
        using GameWorld restored = new(new GameSession(session.State with { EventState = saved }), directory, validateWorldPresentation: false);
        restored.EnsureField();
        Assert.True(JsonElement.DeepEquals(saved, restored.Events.Capture()));
        Assert.Equal(world.Apply(new PlayerAction(PlayerActionKind.Wait)), restored.Apply(new PlayerAction(PlayerActionKind.Wait)));
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("unavailable")]
    [InlineData("height-range")]
    public void UnsupportedNativePlaneLeavesTheSnapshotUnchanged(string failure)
    {
        string directory = TestResources.GameDirectory;
        GameSession session = new(GameSession.Load(TestResources.FixturePath("App.Tests.AutoPlayTeleportPrecisionTests.1")));
        using GameWorld world = new(session, directory, validateWorldPresentation: false);
        world.EnsureField();
        ActorState player = world.Events.PlayerActor!;
        world.Events.ResolveNativeTriangleHeight = failure switch
        {
            "missing" => null,
            "unavailable" => (_, _, _) => null,
            _ => (_, _, _) => short.MaxValue + 1
        };
        JsonElement before = world.Events.Capture();

        Assert.Throws<NotSupportedException>(() => world.Events.TeleportPlayer(player.X, player.Y, player.Z + 0.375f, player.WalkmeshTriangle, (int)player.Direction));

        Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));
    }
}
