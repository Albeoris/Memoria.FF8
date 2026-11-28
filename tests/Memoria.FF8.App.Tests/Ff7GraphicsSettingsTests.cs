using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
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
            WindowModeController window = (WindowModeController)Field(typeof(GamePresentationHost), "windowMode").GetValue(game)!;
            Assert.Equal(frame == 5, window.CaptureTransfer().Borderless);
            int requested = samples[frame];
            Choose("Anti-aliasing", new GraphicsPreferences { MsaaSamples = requested }.AntiAliasingLabel);
            Choose("V-Sync", frame % 2 == 0 ? "Off" : "On");
            Choose("Menu FPS", frame % 2 == 0 ? "30" : "60");
            Assert.Equal(frame % 2 == 0 ? 30 : 60, Field(typeof(GamePresentationHost), "appliedFrameLimit").GetValue(game));
            Assert.Equal(requested, UiPreferences.Load(paths.UiPreferencesPath).Graphics.MsaaSamples);
            Assert.Equal(frame % 2 != 0, ((GraphicsDeviceManager)game.Services.GetService<IGraphicsDeviceManager>()!).SynchronizeWithVerticalRetrace);
            GraphicsDevice device = game.GraphicsDevice;
            int expected = GraphicsHostReadiness.Backend == "Vulkan" && requested == 8 ? 4 : requested;
            using RenderTarget2D reference = new(device, 1, 1, false, SurfaceFormat.Color, DepthFormat.Depth24, expected, RenderTargetUsage.PreserveContents);
            int supported = reference.MultiSampleCount;
            Assert.Equal(0, device.PresentationParameters.MultiSampleCount);
            Assert.Equal(expected, GraphicsDeviceSettings.SceneMsaaSamples(device));
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
        Assert.Equal(30, Field(typeof(GamePresentationHost), "appliedFrameLimit").GetValue(game));
        Field(typeof(Ff7Session), "<World>k__BackingField").SetValue(session, RuntimeHelpers.GetUninitializedObject(typeof(Ff7WorldRuntime)));
        Invoke(game, "ApplyDisplaySettings");
        Assert.Equal(60, Field(typeof(GamePresentationHost), "appliedFrameLimit").GetValue(game));
        Field(typeof(Ff7Session), "<Battle>k__BackingField").SetValue(session, RuntimeHelpers.GetUninitializedObject(typeof(Ff7BattleSession)));
        Invoke(game, "ApplyDisplaySettings");
        Assert.Equal(15, Field(typeof(GamePresentationHost), "appliedFrameLimit").GetValue(game));
        Field(typeof(Ff7Scene), "<Session>k__BackingField").SetValue(scene, null);
        Invoke(game, "ApplyDisplaySettings");
        Assert.Equal(90, Field(typeof(GamePresentationHost), "appliedFrameLimit").GetValue(game));
        Choose("Graphics backend", "Vulkan");
        Assert.True((bool)Field(typeof(RecreateGame), "graphicsRestartPromptOpen").GetValue(game)!);
        Assert.Equal(Memoria.FF8.Platform.GraphicsBackend.Vulkan, UiPreferences.Load(paths.UiPreferencesPath).Graphics.Backend);
        Assert.Null(Field(typeof(RecreateGame), "error").GetValue(game));
    }

    [Fact]
    public void Standalone_host_reports_first_present_and_applies_live_display_settings()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("MEMORIA_FF8_GRAPHICS_HOST_TEST") == "1", "An optional installed-game graphics host diagnostic is required.");
        string? ff7Directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(ff7Directory), "Select original FFVII presentation resources.");
        string directory = TestResources.ArtifactFile("ff7-standalone-graphics");
        Directory.CreateDirectory(directory);
        string readyPath = Path.Combine(directory, "ready.json");
        const string readinessVariable = "MEMORIA_FF8_HOST_READY_FILE";
        string? previousReadiness = Environment.GetEnvironmentVariable(readinessVariable);
        string savePath = Path.Combine(directory, "unused.ffsave");
        LaunchOptions options = new() { GameDirectory = TestResources.GameDirectory, SettingsDirectory = directory, ControlDirectory = directory, ControlExclusive = true, MuteAudio = true, Width = 640, Height = 480, ResolutionSpecified = true, SavePath = savePath };
        GlobalSettingsPaths paths = new(isolatedDirectory: directory);
        UiPreferences preferences = new() { Graphics = new() { MsaaSamples = 0 }, VSync = false, MenuFps = 60 };
        preferences.Save(paths.UiPreferencesPath);
        try
        {
            Environment.SetEnvironmentVariable(readinessVariable, readyPath);
            using Ff7Game game = new(Ff7Installation.Discover(ff7Directory!), "", savePath, new(50, 32000), _ => throw new InvalidOperationException("This presentation diagnostic does not play movies."), launchOptions: options);
            Assert.False(File.Exists(readyPath));
            game.RunOneFrame();
            using JsonDocument report = JsonDocument.Parse(File.ReadAllText(readyPath));
            Assert.Equal(GraphicsHostReadiness.Backend, report.RootElement.GetProperty("backend").GetString());
            Assert.Equal(Environment.ProcessId, report.RootElement.GetProperty("processId").GetInt32());
            Assert.Equal(0, report.RootElement.GetProperty("capabilities").GetProperty("backBufferSamples").GetInt32());

            Ff7Scene scene = (Ff7Scene)Field(typeof(Ff7Game), "scene").GetValue(game)!;
            Invoke(scene, "EnsurePlayerMenu");
            Invoke(scene, "ApplyPlayerMenuAction", new MenuAction("config", "Anti-aliasing", Value: new GraphicsPreferences { MsaaSamples = 4 }.AntiAliasingLabel));
            Invoke(scene, "ApplyPlayerMenuAction", new MenuAction("config", "V-Sync", Value: "On"));
            Invoke(scene, "ApplyPlayerMenuAction", new MenuAction("config", "Menu FPS", Value: "30"));
            game.RunOneFrame();

            GraphicsDeviceManager graphics = (GraphicsDeviceManager)game.Services.GetService<IGraphicsDeviceManager>()!;
            Assert.Equal(4, GraphicsDeviceSettings.SceneMsaaSamples(game.GraphicsDevice));
            Assert.Equal(0, game.GraphicsDevice.PresentationParameters.MultiSampleCount);
            Assert.True(graphics.SynchronizeWithVerticalRetrace);
            Assert.Equal(30, Field(typeof(GamePresentationHost), "appliedFrameLimit").GetValue(game));
            Assert.Null(scene.Error);
        }
        finally
        {
            Environment.SetEnvironmentVariable(readinessVariable, previousReadiness);
        }
    }

    private static FieldInfo Field(Type owner, string name) => owner.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static object? Invoke(object owner, string name, params object[] arguments) => owner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(owner, arguments);

}
