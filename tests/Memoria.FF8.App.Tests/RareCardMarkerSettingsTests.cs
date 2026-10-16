using Memoria.FF8.GameUi;

namespace Memoria.FF8.App.Tests;

public sealed class RareCardMarkerSettingsTests
{
    [Fact]
    public void MarkersDefaultOnAndExistingPreferencesWithoutTheSettingRetainThatDefault()
    {
        string path = Path.GetTempFileName();
        try
        {
            Assert.True(new UiPreferences().RareCardMarkers);
            File.WriteAllText(path, "{}");
            Assert.True(UiPreferences.Load(path).RareCardMarkers);
            File.Delete(path);
            Assert.True(UiPreferences.Load(path).RareCardMarkers);
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
        UiPreferences preferences = new();
        MenuController menus = new();
        menus.ConnectPreferences(() => preferences, action => preferences = preferences.WithSetting(action.SubjectId, action.Value));
        menus.Open("debug-ui", true);
        MenuEntry marker = menus.Entries.Single(entry => entry.Id == "Rare card markers");
        Assert.Contains("On", marker.Label);
        menus.Execute(marker.Action!);
        Assert.False(preferences.RareCardMarkers);
        Assert.Contains("Off", menus.Entries.Single(entry => entry.Id == marker.Id).Label);
        menus.Execute(menus.Entries.Single(entry => entry.Id == marker.Id).Action!);
        Assert.True(preferences.RareCardMarkers);
    }
}
