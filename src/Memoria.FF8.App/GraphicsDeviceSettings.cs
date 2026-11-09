using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8;

internal static class GraphicsDeviceSettings
{
    internal static GraphicsDeviceManager Create(Game game, UiPreferences preferences, LaunchOptions? options, Func<UiPreferences> currentPreferences)
    {
        (int width, int height) = options is null ? (preferences.WindowWidth, preferences.WindowHeight) : preferences.WindowSize(options);
        GraphicsDeviceManager graphics = new(game)
        {
            PreferredBackBufferWidth = width,
            PreferredBackBufferHeight = height,
            SynchronizeWithVerticalRetrace = preferences.VSync,
            GraphicsProfile = GraphicsProfile.HiDef,
            PreferMultiSampling = preferences.Graphics.MsaaSamples > 0
        };
        graphics.PreparingDeviceSettings += (_, settings) => settings.GraphicsDeviceInformation.PresentationParameters.MultiSampleCount = currentPreferences().Graphics.MsaaSamples;
        return graphics;
    }

    internal static bool Apply(GraphicsDeviceManager graphics, UiPreferences preferences, ref int appliedMsaaSamples)
    {
        if (graphics.SynchronizeWithVerticalRetrace == preferences.VSync && appliedMsaaSamples == preferences.Graphics.MsaaSamples)
        {
            return false;
        }
        graphics.SynchronizeWithVerticalRetrace = preferences.VSync;
        graphics.PreferMultiSampling = preferences.Graphics.MsaaSamples > 0;
        graphics.ApplyChanges();
        appliedMsaaSamples = preferences.Graphics.MsaaSamples;
        return true;
    }
}
