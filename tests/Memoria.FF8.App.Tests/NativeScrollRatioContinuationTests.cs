using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeScrollRatioContinuationTests
{
    [Fact]
    public void AuthenticNightVisitUsesRetainedRatiosAndNonselectedCurrentWithoutMovingCommonCamera()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_SCROLL_RATIO_SOURCE");
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(source is not null && File.Exists(source) && Directory.Exists(directory), "Select a genuinely regenerated current field248 visit with MEMORIA_FF8_SCROLL_RATIO_SOURCE.");
        GameState actual = GameSession.Load(source!);
        Assert.Equal(248, actual.FieldId);
        GameSession session = new(actual);
        using GameWorld world = new(session, directory);
        world.EnsureField();
        Assert.Equal(8, world.Events.Scene.BackgroundScrollLayers.Count);
        world.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 2));
        Assert.Equal(new BackgroundScrollSettings(0, 0, 0, 0), world.Events.Scene.BackgroundScrollLayers[1]);
        BackgroundScrollPosition position = world.Events.Scene.BackgroundScrollPositions[1];
        Assert.Equal((short)0, position.X);
        Assert.Equal((short)112, position.Y);
        Assert.Equal((byte)2, position.Status);
        FieldAssets field = world.Field!;
        BackgroundScrollFrame frame = DirectBackgroundScrollCertificate.Prepare(field, world.Events.Scene) ?? NormalCameraCertificate.Prepare(field, world.Events.Scene, world.Events.Actors)!;
        Assert.Equal(new Vector2(0, -112) + field.GetCamera(0).Pan, frame.LayerOffsets[1]);
        Assert.Equal(frame.ActorOffset, frame.LayerOffsets[0]);
        Assert.Null(frame.NativeWrap);
        Assert.False(NativeNonselectedBackgroundScrollCertificate.CanSetDirect(field, world.Events.Scene, 1, short.MaxValue, 0));

        // Independent native475480 geometry reference; only test rendering uses this derived tile list.
        List<BackgroundTile> referenceTiles = field.Background.Select(tile =>
        {
            Vector2 offset = frame.LayerOffsets.GetValueOrDefault(tile.Layer);
            return tile with { X = tile.X + (int)offset.X, Y = tile.Y + (int)offset.Y };
        }).ToList();
        FieldRenderer reference = new(field with { Background = referenceTiles }, 640, 448);
        FieldRenderer rendered = new(field, 640, 448);
        byte[] expected = reference.Render(world.Events.BackgroundStates, [], TimeSpan.Zero, backgroundModuleShades: world.Events.GetBackgroundModuleShades());
        byte[] observed = rendered.Render(world.Events.BackgroundStates, [], TimeSpan.Zero, backgroundModuleShades: world.Events.GetBackgroundModuleShades(), backgroundScrollOffsets: frame.LayerOffsets, actorScrollOffset: frame.ActorOffset);
        Assert.Equal(expected, observed);
        world.SetRenderSize(640, 448);
        JsonElement native = world.Events.Capture();
        GameState current = session.State with { EventState = native };
        GameState independent = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(current))!;
        using GameWorld resumed = new(new GameSession(independent), directory);
        resumed.EnsureField();
        resumed.SetRenderSize(640, 448);
        Assert.True(JsonElement.DeepEquals(native, resumed.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), resumed.Render(TimeSpan.Zero));
        Assert.Equal(world.Apply(new PlayerAction(PlayerActionKind.Wait)), resumed.Apply(new PlayerAction(PlayerActionKind.Wait)));
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), resumed.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), resumed.Render(TimeSpan.Zero));
    }

    [Theory]
    [InlineData(0, 112)]
    [InlineData(1, 68)]
    [InlineData(2, 24)]
    [InlineData(3, -20)]
    [InlineData(4, -64)]
    public void NonselectedNativeLinearPhaseUsesItsCurrentInTheSamePlacement(int elapsed, int expectedY)
    {
        short currentY = NativeLinearScrollPhase.Interpolate(112, -64, 4, (short)elapsed);
        Assert.Equal(expectedY, currentY);
        BackgroundScrollPosition phase = new(0, currentY, 0, -64, 0, 112, (short)(elapsed == 4 ? elapsed : elapsed + 1), 4, elapsed == 4 ? (byte)2 : (byte)1, 4, NativeLinearVersion: 1);
        Assert.True(NativeLinearScrollPhase.IsValid(phase));
        Assert.True(BackgroundParallaxPlacement.TryOffset(0, 100, new BackgroundScrollSettings(0, 0, 0, 0), phase, out Vector2 offset));
        Assert.Equal(new Vector2(0, -expectedY), offset);
    }
}
