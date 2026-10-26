using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect338ResourceTests
{
    [Theory]
    [InlineData((byte)1, 308)]
    [InlineData((byte)3, 876)]
    public void Original_reached_camera_tracks_retire_from_the_complete_source_after_JSON(byte selector, int expectedTrackOffset)
    {
        using GameAssets game = Open();
        NativeBattleEffect338Resources resources = game.ReadNativeEffect338Resources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeEffect338Resources());
        Assert.Equal(1172, resources.Camera.Data.Length);
        NativeBattleEffectHeapAllocation restored = JsonSerializer.Deserialize<NativeBattleEffectHeapAllocation>(JsonSerializer.Serialize(resources.Camera))!;
        Assert.Equal(resources.Camera.Data.ToArray(), restored.Data.ToArray());
        NativeBattleCameraBank bank = new(restored.Data.Slice(12, restored.Data.Length - 12));
        Assert.Equal(expectedTrackOffset, bank.TrackOffset(selector));
        NativeBattleCameraState camera = NativeBattleCameraState.FromColdProcess().Start(bank, selector);
        Assert.Single(camera.Order);
        for (int callback = 0; callback < 2000 && !camera.Order.IsEmpty; callback++)
        {
            camera = camera.Tick(0, new() { TransformReference = (_, point) => point });
        }
        Assert.Empty(camera.Order);
        Assert.Equal(new byte[] { 255, 255 }, resources.Camera.Data.AsSpan(1170, 2).ToArray());
    }

    [Theory]
    [InlineData(604)]
    [InlineData(1171)]
    public void Truncated_camera_carriers_are_rejected_without_substituting_another_selector(int length)
    {
        using GameAssets game = Open();
        NativeBattleEffect338Resources resources = game.ReadNativeEffect338Resources();
        NativeBattleEffect338Resources truncated = resources with { Camera = resources.Camera with { Data = resources.Camera.Data.Slice(0, length) } };
        Assert.Throws<InvalidDataException>(() => truncated.Validate());
        NativeBattleCameraBank bank = new(truncated.Camera.Data.Slice(12, truncated.Camera.Data.Length - 12));
        Assert.Throws<InvalidDataException>(() =>
        {
            NativeBattleCameraState camera = NativeBattleCameraState.FromColdProcess().Start(bank, 3);
            for (int callback = 0; callback < 2000 && !camera.Order.IsEmpty; callback++)
            {
                camera = camera.Tick(0, new() { TransformReference = (_, point) => point });
            }
        });
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original effect338 resources.");
        return new(directory!);
    }
}
