using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleCameraViewTranslationResourcesTests
{
    [Fact]
    public void Authenticated_loader_proves_the_six_zero_filled_camera_translation_bytes_and_retains_them_through_JSON()
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        NativeBattleRetainedMemory cold = game.ReadColdProcessNativeBattleMemory();
        NativeBattleCameraState camera = Assert.IsType<NativeBattleCameraState>(cold.Camera);
        Assert.Equal(new NativeBattleCameraViewTranslationScratch(0, 0, 0), camera.ViewTranslationScratch);
        NativeBattleRetainedMemory restored = JsonSerializer.Deserialize<NativeBattleRetainedMemory>(JsonSerializer.Serialize(cold))!;
        NativeBattleCameraState restoredCamera = Assert.IsType<NativeBattleCameraState>(restored.Camera);
        Assert.Equal(camera.ViewTranslationScratch, restoredCamera.ViewTranslationScratch);
        Assert.Equal(new NativeBattleCameraViewTranslationScratch(0, 32, 0), restoredCamera.AddViewTranslationY61B8D5(32).ViewTranslationScratch);
        Assert.Equal(new NativeBattleCameraViewTranslationScratch(0, 0, 0), cold.Camera!.ViewTranslationScratch);
    }
}
