using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Map;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeField202CosineScrollTests
{
    [Fact]
    public void ActualPlatformMixedLayerCurvesPreserveNativePlacementPixelsAndIndependentSave()
    {
        string directory = TestResources.GameDirectory;
        DirectoryInfo? repository = new(TestResources.FixtureDirectory);
        Assert.NotNull(repository);
        string source = Path.Combine(repository.FullName, "../fixtures/external-fixture.json");
        Assert.SkipUnless(Directory.Exists(directory) && File.Exists(source), "The standalone platform202 snapshot and installed original resources are required.");
        byte[] originalHash = SHA256.HashData(File.ReadAllBytes(source));
        GameSession session = new(GameSession.Load(source));
        using GameWorld world = new(session, directory);
        world.EnsureField();
        world.SetRenderSize(320, 240);
        Assert.Equal(202, world.Field!.Id);
        for (int callback = 0; callback < 10 && (world.Events.Scene.BackgroundScrollPositions[2].Mode != 5 || world.Events.Scene.BackgroundScrollPositions[4].Mode != 5); callback++)
        {
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
            Assert.Null(world.Events.LastFault);
        }
        SceneEventState scene = world.Events.Scene;
        Assert.NotNull(scene.ActorBackgroundScroll);
        Assert.Equal((byte)4, scene.BackgroundScrollPositions[1].Mode);
        Assert.Equal((byte)5, scene.BackgroundScrollPositions[2].Mode);
        Assert.Equal((byte)5, scene.BackgroundScrollPositions[4].Mode);
        BackgroundScrollPosition issued = scene.BackgroundScrollPositions[2];
        Assert.Equal(((short)0, (short)50, (short)0, (short)-448, (short)124), (issued.PreviousX, issued.PreviousY, issued.TargetX, issued.TargetY, issued.Duration));
        CheckPlacement(world, inspectRaster: true);
        for (int callback = 0; callback < 4; callback++)
        {
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
            Assert.Null(world.Events.LastFault);
        }
        string save = Path.Combine(Path.GetTempPath(), "Memoria.FF8-platform202-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.SetEventState(world.Events.Capture());
            session.Save(save);
            using GameWorld restored = new(new GameSession(GameSession.Load(save)), directory);
            restored.EnsureField();
            restored.SetRenderSize(320, 240);
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
            Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
            for (int callback = 0; callback < 8; callback++)
            {
                PlayerAction action = new(PlayerActionKind.Wait);
                Assert.Equal(world.Apply(action), restored.Apply(action));
                Assert.Null(world.Events.LastFault);
                Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
                CheckPlacement(world, inspectRaster: false);
                Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
            }
        }
        finally
        {
            if (File.Exists(save))
            {
                File.Delete(save);
            }
        }
        Assert.Equal(originalHash, SHA256.HashData(File.ReadAllBytes(source)));
    }

    private static void CheckPlacement(GameWorld world, bool inspectRaster)
    {
        FieldAssets field = world.Field!;
        SceneEventState scene = world.Events.Scene;
        NativeActorBackgroundScroll camera = Assert.IsType<NativeActorBackgroundScroll>(scene.ActorBackgroundScroll);
        BackgroundScrollFrame placement = BackgroundScrollCertificate.Prepare(field, scene);
        Assert.Equal(new Vector2(-camera.X!.Value, -camera.Y!.Value), placement.ActorOffset);
        Vector2 pan = field.GetCamera(scene.CameraIndex).Pan;
        foreach (int layer in new[] { 1, 2, 4 })
        {
            BackgroundScrollPosition position = scene.BackgroundScrollPositions[layer];
            BackgroundScrollSettings settings = scene.BackgroundScrollLayers[layer];
            Vector2 expected = new(settings.OffsetX - camera.X.Value * settings.ScaleX / 256 - position.X!.Value + pan.X, settings.OffsetY - camera.Y.Value * settings.ScaleY / 256 - position.Y!.Value + pan.Y);
            Assert.Equal(expected, placement.LayerOffsets.GetValueOrDefault(layer));
            Assert.True(NativeNonselectedBackgroundScrollCertificate.ValidPosition(position));
            if (position.Mode == 5)
            {
                int elapsed = position.Status == 2 ? position.Duration!.Value : position.Counter!.Value - 1;
                Assert.Equal((short)NativeCosineInterpolation.Interpolate(position.PreviousY!.Value, position.TargetY, position.Duration!.Value, elapsed), position.Y);
                Assert.True(BackgroundScrollCertificate.CanSetCosinePosition(field, scene, layer, position.TargetX, position.TargetY, position.Duration.Value));
            }
            if (inspectRaster)
            {
                PixelImage solid = new(16, 16, Enumerable.Repeat(new Rgba32(255, 0, 0, 255), 256).ToArray());
                BackgroundTile tile = new(0, 0, 1000, 255, 0, layer, MapBlendMode.None, false, true, solid);
                FieldAssets isolated = field with { Background = [tile], Information = null };
                FieldRenderer renderer = new(isolated, 320, 224, nativeMovieViewport: true);
                byte[] pixels = renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: placement.LayerOffsets);
                int left = 160 + (int)expected.X;
                int top = 112 + (int)expected.Y;
                for (int y = 0; y < 224; y++)
                {
                    for (int x = 0; x < 320; x++)
                    {
                        Assert.Equal(x >= left && x < left + 16 && y >= top && y < top + 16 ? (byte)255 : (byte)0, pixels[(y * 320 + x) * 4]);
                    }
                }
            }
        }
    }
}
