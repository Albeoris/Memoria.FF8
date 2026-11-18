using System.Runtime.CompilerServices;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8;

internal static class GraphicsDeviceSettings
{
    private static readonly ConditionalWeakTable<GraphicsDevice, SceneQuality> sceneQuality = new();

    internal static int SceneMsaaSamples(GraphicsDevice device)
    {
        return sceneQuality.TryGetValue(device, out SceneQuality? quality) ? quality.Samples : device.PresentationParameters.MultiSampleCount;
    }

    private static void RememberSceneQuality(GraphicsDevice device, int samples)
    {
        sceneQuality.GetValue(device, static _ => new()).Samples = samples;
    }

    internal static GraphicsDeviceManager Create(Game game, UiPreferences preferences, LaunchOptions? options, Func<UiPreferences> currentPreferences)
    {
        (int width, int height) = options is null ? (preferences.WindowWidth, preferences.WindowHeight) : preferences.WindowSize(options);
        GraphicsDeviceManager graphics = new(game)
        {
            PreferredBackBufferWidth = width,
            PreferredBackBufferHeight = height,
            SynchronizeWithVerticalRetrace = preferences.VSync,
            GraphicsProfile = GraphicsProfile.HiDef,
            PreferMultiSampling = false
        };
        // The swapchain receives the resolved scene image and remains single-sample.
        graphics.PreparingDeviceSettings += (_, settings) => settings.GraphicsDeviceInformation.PresentationParameters.MultiSampleCount = 0;
        graphics.DeviceCreated += (_, _) => RememberSceneQuality(graphics.GraphicsDevice, currentPreferences().Graphics.MsaaSamples);
        graphics.DeviceReset += (_, _) => RememberSceneQuality(graphics.GraphicsDevice, currentPreferences().Graphics.MsaaSamples);
        return graphics;
    }

    internal static bool Apply(GraphicsDeviceManager graphics, UiPreferences preferences, ref int appliedMsaaSamples)
    {
        if (graphics.SynchronizeWithVerticalRetrace == preferences.VSync && appliedMsaaSamples == preferences.Graphics.MsaaSamples)
        {
            return false;
        }
        graphics.SynchronizeWithVerticalRetrace = preferences.VSync;
        graphics.PreferMultiSampling = false;
        graphics.ApplyChanges();
        RememberSceneQuality(graphics.GraphicsDevice, preferences.Graphics.MsaaSamples);
        appliedMsaaSamples = preferences.Graphics.MsaaSamples;
        return true;
    }

    private sealed class SceneQuality
    {
        internal int Samples { get; set; }
    }
}
