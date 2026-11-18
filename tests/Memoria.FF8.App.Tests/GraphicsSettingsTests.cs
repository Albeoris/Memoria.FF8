using System.Reflection;
using System.Text.Json;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class GraphicsSettingsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void Standalone_devices_retain_their_actual_scene_sample_count(int samples)
    {
        using GpuTestDevice device = new(samples);
        Assert.Equal(device.Device.PresentationParameters.MultiSampleCount, GraphicsDeviceSettings.SceneMsaaSamples(device.Device));
    }

    [Theory]
    [InlineData("Off", 0)]
    [InlineData("MSAA 2x", 2)]
    [InlineData("MSAA 4x", 4)]
    [InlineData("MSAA 8x", 8)]
    public void Graphics_quality_persists_in_its_own_section_and_is_shared_by_active_saves(string label, int samples)
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "{\"FieldFps\":30}");
            UiPreferences legacy = UiPreferences.Load(path);
            Assert.Equal(8, legacy.Graphics.MsaaSamples);
            UiPreferences changed = legacy.WithInstallationSetting("Anti-aliasing", label, activeGame: true);
            changed.Save(path);
            Assert.Equal(samples, changed.Graphics.MsaaSamples);
            Assert.Equal(label, changed.Graphics.AntiAliasingLabel);
            Assert.Equal(30, changed.FieldFps);
            Assert.Equal(changed, UiPreferences.Load(path));
            using JsonDocument json = JsonDocument.Parse(File.ReadAllText(path));
            Assert.Equal(samples, json.RootElement.GetProperty("Graphics").GetProperty("MsaaSamples").GetInt32());
            Assert.False(json.RootElement.GetProperty("Graphics").TryGetProperty("AntiAliasingLabel", out _));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Native_field_shadow_option_is_on_for_legacy_preferences_and_persists()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "{}");
            UiPreferences legacy = UiPreferences.Load(path);
            Assert.True(legacy.NativeFieldShadows);
            UiPreferences disabled = legacy.WithInstallationSetting("Native field shadows", "Off", activeGame: true);
            disabled.Save(path);
            Assert.False(UiPreferences.Load(path).NativeFieldShadows);
            Assert.True(disabled.WithSetting("Native field shadows", "On").NativeFieldShadows);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("{\"Graphics\":null}")]
    [InlineData("{\"Graphics\":{\"MsaaSamples\":-1}}")]
    [InlineData("{\"Graphics\":{\"MsaaSamples\":1}}")]
    [InlineData("{\"Graphics\":{\"MsaaSamples\":3}}")]
    [InlineData("{\"Graphics\":{\"MsaaSamples\":16}}")]
    public void Unsupported_graphics_configuration_is_rejected(string json)
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, json);
            Assert.Throws<InvalidDataException>(() => UiPreferences.Load(path));
            Assert.Throws<InvalidDataException>(() => (new UiPreferences() with { Graphics = new() { MsaaSamples = 3 } }).Save(path));
            Assert.Throws<ArgumentException>(() => new UiPreferences().WithSetting("Anti-aliasing", "MSAA 16x"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    public void Complete_host_applies_initial_and_live_quality_without_repeating_device_changes(int initialSamples)
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("MEMORIA_FF8_GRAPHICS_HOST_TEST") == "1", "An optional installed-game graphics host diagnostic is required.");
        string directory = TestResources.ArtifactFile("graphics-settings-" + initialSamples);
        GlobalSettingsPaths paths = new(directory);
        UiPreferences preferences = new() { Graphics = new() { MsaaSamples = initialSamples }, VSync = false };
        preferences.Save(paths.UiPreferencesPath);
        GameSession session = GameSession.CreateColdProcess();
        using RecreateGame game = new(new LaunchOptions { GameDirectory = TestResources.GameDirectory, DebugUiScreen = "config-graphics", MuteAudio = true, ExitAfterSeconds = 10, SavePath = Path.Combine(directory, "unused.json") }, session);
        Field("globalSettings").SetValue(game, paths);
        Field("uiPreferences").SetValue(game, preferences);
        Field("appliedMsaaSamples").SetValue(game, initialSamples);
        using QualityObserver observer = new(game, paths, initialSamples);
        game.Components.Add(observer);
        game.Run();
        Assert.Equal(5, observer.CheckedChanges);
        Assert.Null(Field("error").GetValue(game));
        Assert.Equal(SceneKind.Title, session.State.Scene);
    }

    private static FieldInfo Field(string name) => typeof(RecreateGame).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;

    private sealed class QualityObserver(RecreateGame game, GlobalSettingsPaths paths, int initialSamples) : GameComponent(game)
    {
        private readonly int[] samples = [2, 4, 8, 0, 8];
        public int CheckedChanges { get; private set; }

        public override void Update(GameTime gameTime)
        {
            AssertSamples(CheckedChanges == 0 ? initialSamples : samples[CheckedChanges - 1]);
            if (CheckedChanges == samples.Length)
            {
                typeof(RecreateGame).GetMethod("ExitGame", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(game, null);
                return;
            }
            int requested = samples[CheckedChanges];
            GpuTriangleRenderer renderer = (GpuTriangleRenderer)Field("battleGpu").GetValue(game)!;
            long version = renderer.PresentationVersion;
            string label = new GraphicsPreferences { MsaaSamples = requested }.AntiAliasingLabel;
            typeof(RecreateGame).GetMethod("ApplyLiveMenuAction", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(game, [new MenuAction("config", "Anti-aliasing", Value: label)]);
            Assert.Equal(requested, UiPreferences.Load(paths.UiPreferencesPath).Graphics.MsaaSamples);
            AssertSamples(requested);
            Assert.True(renderer.PresentationVersion > version);
            int preparations = 0;
            GraphicsDeviceManager manager = (GraphicsDeviceManager)Field("graphics").GetValue(game)!;
            EventHandler<PreparingDeviceSettingsEventArgs> changed = (_, _) => preparations++;
            manager.PreparingDeviceSettings += changed;
            try
            {
                typeof(RecreateGame).GetMethod("ApplyDisplaySettings", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(game, null);
                Assert.Equal(0, preparations);
            }
            finally
            {
                manager.PreparingDeviceSettings -= changed;
            }
            CheckedChanges++;
        }

        private void AssertSamples(int requested)
        {
            GraphicsDevice device = game.GraphicsDevice;
            using RenderTarget2D reference = new(device, 1, 1, false, SurfaceFormat.Color, DepthFormat.Depth24, requested, RenderTargetUsage.PreserveContents);
            int supported = reference.MultiSampleCount;
            Assert.Equal(0, device.PresentationParameters.MultiSampleCount);
            Assert.Equal(requested, GraphicsDeviceSettings.SceneMsaaSamples(device));
            GpuTriangleRenderer renderer = (GpuTriangleRenderer)Field("battleGpu").GetValue(game)!;
            renderer.Begin(16, 16, System.Numerics.Vector4.One);
            renderer.End();
            Assert.Equal(supported, renderer.Target.MultiSampleCount);
        }
    }
}
