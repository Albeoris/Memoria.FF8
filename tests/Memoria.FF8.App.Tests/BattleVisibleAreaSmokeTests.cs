using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using SkiaSharp;

namespace Memoria.FF8.App.Tests;

public sealed class BattleVisibleAreaSmokeTests
{
    [Fact]
    public void Selected_installed_components_complete_in_the_configured_visible_area()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? resolution = Environment.GetEnvironmentVariable("MEMORIA_FF8_VISIBLE_RESOLUTION");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_VISIBLE_OUTPUT");
        string? selected = Environment.GetEnvironmentVariable("MEMORIA_FF8_VISIBLE_EFFECTS");
        Assert.SkipUnless(directory is not null && resolution is not null && output is not null && selected is not null, "Configure original resources, current resolution, selected effects and an output directory.");
        int[] size = resolution!.Split('x').Select(int.Parse).ToArray();
        Assert.Equal(2, size.Length);
        int width = size[0];
        int height = size[1];
        PixelBounds projection = ViewportLayout.Fit(320, 240, width, height);
        using IDisposable visible = BattleSceneVisibleArea.Begin(BattleSceneVisibleArea.FromViewport(width, height, projection.X, projection.Y, projection.Width, projection.Height));
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        Directory.CreateDirectory(output!);
        File.WriteAllText(Path.Combine(output!, "run.json"), JsonSerializer.Serialize(new { width, height, selected, BinarySha256 = NativeBattleEffectFamilyRunner.CollectBinaryHashes(), AudioOutput = false }));
        foreach (ushort effect in selected!.Split(',').Select(ushort.Parse))
        {
            string destination = Path.Combine(output!, effect.ToString());
            BattleNativeAction shell = assets.NativeActions.Single(action => action.Command == 2 && action.ActionId == 1);
            NativeEffectFixtureRequest request = effect switch
            {
                131 => new("visible-area", effect, 8, 184, 0, 800, 240) { EncounterId = 376, CasterActorIndex = 3, PreserveOriginalFormationEnemies = true },
                139 => new("visible-area", effect, 8, 182, 0, 800, 240) { EncounterId = 384, CasterActorIndex = 3, PreserveOriginalFormationEnemies = true },
                _ => new("visible-area", effect, shell.Command, shell.ActionId, 3, 800, 240) { ControlledShellEffect = shell.MagicId, PartyModelId = effect == 89 ? 4 : 0 }
            };
            NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(request, directory!, destination);
            Assert.True(exported.Status != "blocked", exported.FirstBlocker);
            BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(destination, "queued-action104.battle.json"));
            BattleSession battle = BattleSession.Restore(source);
            battle.AttachNativeEffectResourceResolver(id => game.ReadNativeEffectCatalogForEffects([id])[id - 1]);
            BattleScenePresentation scene = new(assets, source.Definition, true);
            bool entered = false;
            bool restored = false;
            bool captureRendered = false;
            bool captureRestored = false;
            int rendered = 0;
            int callbacks;
            for (callbacks = 0; callbacks < 4096; callbacks++)
            {
                battle.AdvanceNativeGraphicsPresentation(TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30));
                NativeBattleOriginalEffectState? current = battle.NativeGraphics!.OriginalEffect;
                if (current?.Binding.Effect == effect && current.ReturnedDescriptor != 0)
                {
                    entered = true;
                    bool captureVisible = battle.PreparedPresentation?.Primitives.LastCommands.Any(command => command.Command.SceneCapture?.Kind is NativeBattleSceneCaptureKind.Image or NativeBattleSceneCaptureKind.ScreenSample) == true
                        || battle.PreparedPresentation?.Primitives.LastTextureSnapshots is { } snapshots && snapshots.Any(pixels => pixels.SceneCopies is { Length: > 0 });
                    if (callbacks % 180 == 0 && rendered < 4 || captureVisible && !captureRendered)
                    {
                        byte[] rgba = scene.Render(battle, width, height);
                        Assert.Equal(width * height * 4, rgba.Length);
                        using SKBitmap bitmap = new(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
                        System.Runtime.InteropServices.Marshal.Copy(rgba, 0, bitmap.GetPixels(), rgba.Length);
                        using SKImage image = SKImage.FromBitmap(bitmap);
                        using SKData png = image.Encode(SKEncodedImageFormat.Png, 100);
                        using FileStream stream = File.Create(Path.Combine(destination, $"frame-{callbacks}.png"));
                        png.SaveTo(stream);
                        rendered++;
                        captureRendered |= captureVisible;
                        if (!restored || captureVisible && !captureRestored)
                        {
                            BattleSnapshot snapshot = battle.Capture();
                            File.WriteAllText(Path.Combine(destination, "midpoint.battle.json"), JsonSerializer.Serialize(snapshot));
                            BattleSession cold = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(snapshot))!);
                            cold.AttachNativeEffectResourceResolver(id => game.ReadNativeEffectCatalogForEffects([id])[id - 1]);
                            cold.AdvanceNativeGraphicsPresentation(TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30));
                            scene.Render(cold, width, height);
                            restored = true;
                            captureRestored |= captureVisible;
                        }
                    }
                }
                NativeBattleDescriptorState descriptors = battle.NativeGraphics.Descriptors;
                if (entered && !descriptors.Queue.Any(slot => descriptors.Packets[slot].Command == 104))
                {
                    break;
                }
            }
            Assert.True(entered, $"Effect {effect} did not initialize.");
            Assert.True(callbacks < 4096, $"Effect {effect} did not complete.");
            Assert.True(rendered > 0 && restored, $"Effect {effect} did not reach its sampled presentation boundary.");
            if (effect is 89 or 139 or 175 or 206)
            {
                Assert.True(captureRendered && captureRestored, $"Effect {effect} did not render and restore its scene-capture consumer.");
            }
            File.WriteAllText(Path.Combine(destination, "smoke.json"), JsonSerializer.Serialize(new { effect, width, height, callbacks, rendered, restored, captureRendered, captureRestored, AudioOutput = false }));
        }
    }
}
