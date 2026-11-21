using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Mods;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class FieldLightingTargetTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    public void Shadow_target_switch_preserves_scene_color_and_depth(int samples)
    {
        RequireDesktop();
        using RenderGame game = new(samples, gpu =>
        {
            FieldLight lamp = new() { Id = "lamp", Name = "Lamp", Position = new(0, 0, 10), Range = 30 };
            FieldLightingScene scene = new() { FieldName = "scene_a", Lights = [lamp], PrimaryLightId = lamp.Id };
            PixelImage white = new(1, 1, [new Rgba32(255, 255, 255, 255)]);
            TriangleVertex Vertex(float x, float y, float depth, Vector4 color) => new(new Vector2(x, y), depth, 1, Vector2.Zero, color) { WorldPosition = new((x - 64) / 4, (y - 32) / 4, 5), WorldNormal = Vector3.UnitZ };
            FieldShadowTriangle caster = new(Vertex(60, 28, 0.5f, Vector4.One), Vertex(68, 28, 0.5f, Vector4.One), Vertex(60, 36, 0.5f, Vector4.One), new(white));
            for (int frame = 0; frame < 3; frame++)
            {
                gpu.Begin(128, 64, new Vector4(0.25f, 0.5f, 0.75f, 1));
                FieldLightingScene lights = frame == 1 ? scene with { Lights = [lamp with { Type = FieldLight.SunlightType }] } : scene;
                gpu.SetFieldLighting(lights);
                TriangleMaterial front = new(null) { DepthWrite = true, DepthComparison = TriangleDepthComparison.Less };
                gpu.Draw(Vertex(32, 8, 0.25f, new(1, 0, 0, 1)), Vertex(96, 8, 0.25f, new(1, 0, 0, 1)), Vertex(32, 56, 0.25f, new(1, 0, 0, 1)), front);
                Assert.True(gpu.PrepareFieldShadow([caster], out string? reason), reason);
                TriangleMaterial behind = front with { FieldLighting = FieldLightingPass.Model };
                gpu.Draw(Vertex(32, 8, 0.75f, Vector4.One), Vertex(96, 8, 0.75f, Vector4.One), Vertex(32, 56, 0.75f, Vector4.One), behind);
                gpu.End();
                Color[] pixels = GpuTestDevice.Read(gpu.Target);
                Assert.Equal(Color.Red, pixels[16 * 128 + 40]);
                Color clear = new(0.25f, 0.5f, 0.75f, 1);
                for (int y = 0; y < 64; y++)
                {
                    for (int x = 0; x < 128; x++)
                    {
                        if (x < 24 || x >= 104)
                        {
                            Assert.Equal(clear, pixels[y * 128 + x]);
                        }
                    }
                }
            }
        }, frames: 3);
        game.Run();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(8)]
    public void Installed_scene_preserves_black_margins_without_advancing_save(int samples)
    {
        RequireDesktop();
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_LIGHTING_TARGET_STATE");
        string? document = Environment.GetEnvironmentVariable("MEMORIA_FF8_LIGHTING_TARGET_SCENE");
        Assert.SkipUnless(File.Exists(source) && File.Exists(document), "Select a standalone field save and lighting document with MEMORIA_FF8_LIGHTING_TARGET_STATE and MEMORIA_FF8_LIGHTING_TARGET_SCENE.");
        byte[] sourceHash = SHA256.HashData(File.ReadAllBytes(source!));
        byte[] documentHash = SHA256.HashData(File.ReadAllBytes(document!));
        FieldLightingScene scene = FieldLightingSceneIO.Deserialize(File.ReadAllBytes(document!));
        string temporary = Path.Combine(Path.GetTempPath(), "memoria-lighting-target-" + Guid.NewGuid().ToString("N"));
        try
        {
            string gameDirectory = TestResources.GameDirectory;
            using GameResourceHost host = new(gameDirectory, temporary, watch: false, modsDirectory: Path.Combine(temporary, "Mods"));
            GameSession session = new(PlayerStateLoader.Load(source!, gameDirectory, resources: host.Resources, modelOverrides: host, readGameplayPolicy: () => host.GameplayPolicy, prepareCharacterDefinitions: initialized => GameplaySaveGuardianForceDefinitions.Select(initialized, host)));
            using GameWorld world = new(session, gameDirectory, resources: host.Resources, modelOverrides: host, readSceneMods: () => host.SceneMods, readGameplayPolicy: () => host.GameplayPolicy);
            world.EnsureField();
            world.SetRenderSize(1280, 448);
            Assert.Equal(scene.FieldName, world.Field!.Name);
            JsonElement before = world.Events.Capture();
            using RenderGame game = new(samples, gpu =>
            {
                world.ActiveLighting = null;
                world.Render(TimeSpan.Zero, rareCardMarkers: false, triangleRenderer: gpu);
                Color[] native = GpuTestDevice.Read(gpu.Target);
                foreach (FieldLightingScene lighting in new[] { scene, scene with { PrimaryLightId = null }, scene })
                {
                    world.ActiveLighting = lighting;
                    world.Render(TimeSpan.Zero, rareCardMarkers: false, triangleRenderer: gpu);
                    Assert.Null(world.LightingUnavailableReason);
                    if (lighting.PrimaryLightId is not null)
                    {
                        Assert.Contains(FieldActorShadowState.Silhouette, world.LightingShadowStates.Values);
                    }
                    Color[] lit = GpuTestDevice.Read(gpu.Target);
                    Assert.NotEqual(native, lit);
                    string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_LIGHTING_TEST_OUTPUT");
                    if (!string.IsNullOrWhiteSpace(output) && lighting == scene)
                    {
                        Directory.CreateDirectory(output);
                        using FileStream image = File.Create(Path.Combine(output, $"target-msaa{samples}.png"));
                        gpu.Target.SaveAsPng(image, gpu.Target.Width, gpu.Target.Height);
                    }
                    for (int y = 0; y < 448; y++)
                    {
                        for (int x = 0; x < 1280; x++)
                        {
                            if (x < 64 || x >= 1216)
                            {
                                Assert.Equal(Color.Black, native[y * 1280 + x]);
                                Assert.Equal(Color.Black, lit[y * 1280 + x]);
                            }
                        }
                    }
                    Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));
                }
            });
            game.Run();
        }
        finally
        {
            if (Directory.Exists(temporary))
            {
                Directory.Delete(temporary, true);
            }
        }
        Assert.Equal(sourceHash, SHA256.HashData(File.ReadAllBytes(source!)));
        Assert.Equal(documentHash, SHA256.HashData(File.ReadAllBytes(document!)));
    }

    private static void RequireDesktop()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows() || Environment.GetEnvironmentVariable("MEMORIA_FF8_GRAPHICS_HOST_TEST") == "1", "Set MEMORIA_FF8_GRAPHICS_HOST_TEST=1 to run this graphics host check on a graphical Unix desktop.");
    }

    private sealed class RenderGame : Game
    {
        private readonly Action<GpuTriangleRenderer> render;
        private readonly GraphicsDeviceManager graphics;
        private readonly int samples;
        private readonly int frames;
        private GpuTriangleRenderer? renderer;
        private int draws;

        public RenderGame(int samples, Action<GpuTriangleRenderer> render, int frames = 1)
        {
            this.samples = samples;
            this.render = render;
            this.frames = frames;
            graphics = new(this) { PreferredBackBufferWidth = 128, PreferredBackBufferHeight = 64, GraphicsProfile = GraphicsProfile.HiDef, PreferMultiSampling = samples > 0, SynchronizeWithVerticalRetrace = false };
            graphics.PreparingDeviceSettings += (_, args) => args.GraphicsDeviceInformation.PresentationParameters.MultiSampleCount = samples;
            IsFixedTimeStep = false;
            InactiveSleepTime = TimeSpan.Zero;
        }

        protected override void Draw(GameTime gameTime)
        {
            if (draws >= frames)
            {
                return;
            }
            int actualSamples = GraphicsDevice.PresentationParameters.MultiSampleCount;
            Assert.InRange(actualSamples, samples > 0 ? 2 : 0, samples);
            renderer ??= new(GraphicsDevice);
            render(renderer);
            draws++;
            if (draws == frames)
            {
                Exit();
            }
        }

        protected override void UnloadContent()
        {
            renderer?.Dispose();
            base.UnloadContent();
        }
    }
}
