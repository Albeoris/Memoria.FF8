using Memoria.FF8.GameUi;

namespace Memoria.FF8.App.Tests;

public sealed class RareCardMarkerSettingsTests
{
    [Fact]
    public void MarkersDefaultOffAndExistingPreferencesWithoutTheSettingRetainThatDefault()
    {
        string path = Path.GetTempFileName();
        try
        {
            Assert.False(new UiPreferences().RareCardMarkers);
            File.WriteAllText(path, "{}");
            Assert.False(UiPreferences.Load(path).RareCardMarkers);
            File.Delete(path);
            Assert.False(UiPreferences.Load(path).RareCardMarkers);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("On", true)]
    [InlineData("Off", false)]
    public void MarkerPreferenceRoundTripsThroughTheLocalSettingsFile(string value, bool enabled)
    {
        string path = Path.GetTempFileName();
        try
        {
            new UiPreferences().WithSetting("Rare card markers", value).Save(path);
            Assert.Equal(enabled, UiPreferences.Load(path).RareCardMarkers);
            Assert.Throws<ArgumentException>(() => new UiPreferences().WithSetting("Rare card markers", "Sometimes"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void DebugUiMenuShowsThePersistedValueAndAppliesTheToggleToItsHost()
    {
        Assert.Equal(new[] { "Off", "On" }, MenuSettings.Definitions.Single(setting => setting.Id == "Rare card markers").Values);
        UiPreferences preferences = new();
        MenuController menus = new();
        menus.ConnectPreferences(() => preferences, action => preferences = preferences.WithSetting(action.SubjectId, action.Value));
        menus.Open("debug-ui", true);
        MenuEntry marker = menus.Entries.Single(entry => entry.Id == "Rare card markers");
        Assert.Contains("Off", marker.Label);
        menus.Execute(marker.Action!);
        Assert.True(preferences.RareCardMarkers);
        Assert.Contains("On", menus.Entries.Single(entry => entry.Id == marker.Id).Label);
        menus.Execute(menus.Entries.Single(entry => entry.Id == marker.Id).Action!);
        Assert.False(preferences.RareCardMarkers);
    }
}
