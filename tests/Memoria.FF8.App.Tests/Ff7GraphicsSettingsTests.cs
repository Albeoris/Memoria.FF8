using System.Reflection;
using System.Runtime.CompilerServices;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.GameUi;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vector2N = System.Numerics.Vector2;
using Vector4N = System.Numerics.Vector4;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class Ff7GraphicsSettingsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void Combined_host_applies_Ff7_graphics_choices_to_device_scene_and_overlays(int initialSamples)
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("MEMORIA_FF8_GRAPHICS_HOST_TEST") == "1", "An optional installed-game graphics host diagnostic is required.");
        string? ff7Directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(ff7Directory), "Select original FFVII presentation resources.");
        string directory = TestResources.ArtifactFile("ff7-graphics-settings");
        GlobalSettingsPaths paths = new(isolatedDirectory: directory);
        new UiPreferences { Graphics = new() { MsaaSamples = initialSamples }, VSync = false, MenuFps = 60 }.Save(paths.UiPreferencesPath);
        using RecreateGame game = new(new LaunchOptions { GameDirectory = TestResources.GameDirectory, SettingsDirectory = directory, DebugUiScreen = "config-graphics", MuteAudio = true, Width = 640, Height = 480, ResolutionSpecified = true, ExitAfterSeconds = 20, SavePath = Path.Combine(directory, "unused.json") }, GameSession.CreateColdProcess());
        game.RunOneFrame();
        Ff7Scene scene = new(game.GraphicsDevice, Ff7Installation.Discover(ff7Directory!), "", new(50, 32000), _ => throw new InvalidOperationException("This presentation diagnostic does not play movies."), audioEnabled: false);
        Field(typeof(RecreateGame), "ff7Scene").SetValue(game, scene);
        Field(typeof(RecreateGame), "crossover").SetValue(game, new CombinedSessionSave { ActiveGame = SessionGame.Ff7 });
        Invoke(game, "ConfigureFf7Presentation", scene);
        Invoke(scene, "EnsurePlayerMenu");
        void Choose(string setting, string value) => Invoke(scene, "ApplyPlayerMenuAction", new MenuAction("config", setting, Value: value));
        Assert.Equal(initialSamples, scene.PresentationPreferences.Graphics.MsaaSamples);
        Assert.Equal(0, game.GraphicsDevice.PresentationParameters.MultiSampleCount);
        Assert.Equal(initialSamples, GraphicsDeviceSettings.SceneMsaaSamples(game.GraphicsDevice));
        Choose("Show FPS", "On");
        int[] samples = [0, 2, 4, 8, 0, 2, 4, 8];
        for (int frame = 0; frame < samples.Length; frame++)
        {
            Choose("Window mode", frame == 5 ? "Borderless" : "Windowed");
            WindowModeController window = (WindowModeController)Field(typeof(RecreateGame), "windowMode").GetValue(game)!;
            Assert.Equal(frame == 5, window.CaptureTransfer().Borderless);
            int requested = samples[frame];
            Choose("Anti-aliasing", new GraphicsPreferences { MsaaSamples = requested }.AntiAliasingLabel);
            Choose("V-Sync", frame % 2 == 0 ? "Off" : "On");
            Choose("Menu FPS", frame % 2 == 0 ? "30" : "60");
            Assert.Equal(frame % 2 == 0 ? 30 : 60, Field(typeof(RecreateGame), "appliedFrameLimit").GetValue(game));
            Assert.Equal(requested, UiPreferences.Load(paths.UiPreferencesPath).Graphics.MsaaSamples);
            Assert.Equal(frame % 2 != 0, ((GraphicsDeviceManager)Field(typeof(RecreateGame), "graphics").GetValue(game)!).SynchronizeWithVerticalRetrace);
            GraphicsDevice device = game.GraphicsDevice;
            using RenderTarget2D reference = new(device, 1, 1, false, SurfaceFormat.Color, DepthFormat.Depth24, requested, RenderTargetUsage.PreserveContents);
            int supported = reference.MultiSampleCount;
            Assert.Equal(0, device.PresentationParameters.MultiSampleCount);
            Assert.Equal(requested, GraphicsDeviceSettings.SceneMsaaSamples(device));
            GpuTriangleRenderer gpu = (GpuTriangleRenderer)Field(typeof(Ff7Scene), "triangles").GetValue(scene)!;
            gpu.Begin(64, 64, new Vector4N(0, 0, 0, 1));
            TriangleVertex Vertex(float x, float y) => new(new Vector2N(x, y), 1, 1, Vector2N.Zero, Vector4N.One);
            gpu.Draw(Vertex(3.25f, 2.25f), Vertex(60.25f, 8.25f), Vertex(9.25f, 61.25f), new TriangleMaterial(null));
            gpu.End();
            Assert.Equal(supported, gpu.Target.MultiSampleCount);
            game.RunOneFrame();
            Color[] pixels = GpuTestDevice.Read(gpu.Target);
            Assert.Contains(pixels, pixel => pixel.R == 255);
            Assert.True((supported > 0) == pixels.Any(pixel => pixel.R > 0 && pixel.R < 255), $"Requested {requested}, supported {supported}, rasterizer MSAA {RasterizerState.CullNone.MultiSampleAntiAlias}, nonblack {pixels.Count(pixel => pixel.R > 0)}, colors {string.Join(",", pixels.Select(pixel => pixel.R).Distinct().Order())}.");
            Assert.NotNull(Field(typeof(RecreateGame), "visualFrameRate").GetValue(game));
            Assert.Null(scene.Error);
        }
        Choose("Show FPS", "Off");
        game.RunOneFrame();
        Assert.Null(Field(typeof(RecreateGame), "visualFrameRate").GetValue(game));
        Choose("Field FPS", "30");
        Choose("World FPS", "60");
        Choose("Battle FPS", "15");
        Choose("Menu FPS", "90");
        Ff7Session session = (Ff7Session)RuntimeHelpers.GetUninitializedObject(typeof(Ff7Session));
        Field(typeof(Ff7Scene), "<Session>k__BackingField").SetValue(scene, session);
        Invoke(game, "ApplyDisplaySettings");
        Assert.Equal(30, Field(typeof(RecreateGame), "appliedFrameLimit").GetValue(game));
        Field(typeof(Ff7Session), "<World>k__BackingField").SetValue(session, RuntimeHelpers.GetUninitializedObject(typeof(Ff7WorldRuntime)));
        Invoke(game, "ApplyDisplaySettings");
        Assert.Equal(60, Field(typeof(RecreateGame), "appliedFrameLimit").GetValue(game));
        Field(typeof(Ff7Session), "<Battle>k__BackingField").SetValue(session, RuntimeHelpers.GetUninitializedObject(typeof(Ff7BattleSession)));
        Invoke(game, "ApplyDisplaySettings");
        Assert.Equal(15, Field(typeof(RecreateGame), "appliedFrameLimit").GetValue(game));
        Field(typeof(Ff7Scene), "<Session>k__BackingField").SetValue(scene, null);
        Invoke(game, "ApplyDisplaySettings");
        Assert.Equal(90, Field(typeof(RecreateGame), "appliedFrameLimit").GetValue(game));
        Choose("Graphics backend", "Vulkan");
        Assert.True((bool)Field(typeof(RecreateGame), "graphicsRestartPromptOpen").GetValue(game)!);
        Assert.Equal(Memoria.FF8.Platform.GraphicsBackend.Vulkan, UiPreferences.Load(paths.UiPreferencesPath).Graphics.Backend);
        Assert.Null(Field(typeof(RecreateGame), "error").GetValue(game));
    }

    private static FieldInfo Field(Type owner, string name) => owner.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static object? Invoke(object owner, string name, params object[] arguments) => owner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(owner, arguments);

}
