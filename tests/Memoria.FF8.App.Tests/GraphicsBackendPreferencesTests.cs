using Memoria.FF8.GameUi;
using Memoria.FF8.Platform;

namespace Memoria.FF8.App.Tests;

public sealed class GraphicsBackendPreferencesTests
{
    [Fact]
    public void ExistingPreferencesRemainCompatibleAndSharedBackendRoundTrips()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            File.WriteAllText(path, "{\"Graphics\":{\"MsaaSamples\":4}}");
            UiPreferences preferences = UiPreferences.Load(path);
            Assert.Equal(GraphicsBackend.Auto, preferences.Graphics.Backend);
            preferences = preferences.WithInstallationSetting("Graphics backend", "Vulkan", activeGame: true);
            Assert.Equal(4, preferences.Graphics.MsaaSamples);
            preferences.Save(path);
            Assert.Equal(GraphicsBackend.Vulkan, GraphicsBackendPreferences.Read(path));
            GraphicsBackendPreferences.Write(path, GraphicsBackend.Direct3D11);
            Assert.Equal(GraphicsBackend.Direct3D11, UiPreferences.Load(path).Graphics.Backend);
            Assert.Equal(4, UiPreferences.Load(path).Graphics.MsaaSamples);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ConfigOffersRestartPreferenceAndCopiedUnavailablePreferenceCanRecover()
    {
        MenuData data = new();
        data.Settings["Graphics backend"] = "Direct3D12";
        data.SupportedActions.Add("config:Graphics backend");
        MenuAction? applied = null;
        MenuController menus = new(() => data, action => applied = action);
        menus.Open("config-graphics", false);
        MenuEntry entry = Assert.Single(menus.Entries, entry => entry.Id == "Graphics backend");
        Assert.Contains("Requires restart", entry.Help);
        Assert.Contains("Try Auto", entry.Help);
        Assert.True(entry.Enabled);
        menus.Execute(entry.Action!);
        Assert.Equal(new MenuAction("config", "Graphics backend", Value: "Auto"), applied);
    }
}
