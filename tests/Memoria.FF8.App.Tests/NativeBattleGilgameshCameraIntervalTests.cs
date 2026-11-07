using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleGilgameshCameraIntervalTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(327)]
    [InlineData(328)]
    [InlineData(329)]
    [InlineData(330)]
    public void Original_camera_program_reaches_an_explicit_native_projection_cut_without_changing_its_endpoints(ushort effect)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed resources for the original Gilgamesh camera interval.");
        string temporary = Path.Combine(Path.GetTempPath(), "camera-interval-" + Guid.NewGuid().ToString("N"));
        try
        {
            BattleSnapshot source = NativeBattleLimitComponentSources.Prepare(effect, directory!, temporary);
            using GameAssets game = new(directory!);
            using BattleAssets assets = new(directory!);
            BattleSession battle = BattleSession.Restore(source);
            battle.AttachNativeEffectResourceResolver(id => game.ReadNativeEffectCatalogForEffects([id])[id - 1]);
            NativeBattlePreparedPresentation? earlier = null;
            using IDisposable scope = BattleSceneVisibleArea.Begin(BattleSceneVisibleArea.Native);
            for (int callback = 0; callback < 600; callback++)
            {
                battle.Tick();
                NativeBattlePreparedPresentation? later = battle.PreparedPresentation;
                if (earlier is not null && later is not null && earlier.PilotEffect == effect && later.PilotEffect == effect && earlier.PilotGeneration == later.PilotGeneration && later.Callback == earlier.Callback + 1 && earlier.PilotCamera is { } a && later.PilotCamera is { } b && !NativeBattlePilotVisualGeometry.CanSampleProjection(a, b))
                {
                    string frozen = JsonSerializer.Serialize(new[] { earlier.PilotCamera, later.PilotCamera });
                    BattleRenderer renderer = new(320, 216);
                    Assert.False(renderer.CanSampleNativePilotInterval(earlier, later));
                    Assert.Contains("noninvertible native camera pose basis", renderer.LastPilotFallbackReason!);
                    NativeBattlePrimitiveCapabilities capabilities = source.Definition.NativeGraphics!.PrimitiveCapabilities!;
                    byte[] first = renderer.RenderNativePrimitiveFrame(earlier.Primitives, earlier.Textures, capabilities: capabilities, textureWindowCache: earlier.TextureWindows, uiFrame: earlier.UiFrame, uiTexture: assets.ReadNativePcUiIcons().Resolve, effectTextureSource: assets.EffectTextureSource).ToArray();
                    byte[] last = renderer.RenderNativePrimitiveFrame(later.Primitives, later.Textures, capabilities: capabilities, textureWindowCache: later.TextureWindows, uiFrame: later.UiFrame, uiTexture: assets.ReadNativePcUiIcons().Resolve, effectTextureSource: assets.EffectTextureSource).ToArray();
                    foreach (float alpha in new[] { 0f, 0.25f, 0.5f, 0.75f, 1f })
                    {
                        Assert.Equal(alpha == 1 ? last : first, renderer.RenderNativePilotPrimitiveFrame(earlier, later, alpha, capabilities: capabilities, uiTexture: assets.ReadNativePcUiIcons().Resolve, effectTextureSource: assets.EffectTextureSource).ToArray());
                    }
                    Assert.Equal(frozen, JsonSerializer.Serialize(new[] { earlier.PilotCamera, later.PilotCamera }));
                    output.WriteLine(JsonSerializer.Serialize(new { Effect = effect, EarlierCallback = earlier.Callback, LaterCallback = later.Callback, EarlierPose = a, LaterPose = b, EarlierProjection = earlier.PilotProjection, LaterProjection = later.PilotProjection, renderer.LastPilotFallbackReason, renderer.LastPilotFallbackCount }));
                    return;
                }
                earlier = later;
            }
            Assert.Fail("The original camera program did not expose its expected singular native camera interval within the bounded callback window.");
        }
        finally
        {
            if (Directory.Exists(temporary))
            {
                Directory.Delete(temporary, true);
            }
        }
    }
}
