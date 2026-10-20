using System.Globalization;
using System.Text.Json;
using Memoria.FF8.GameUi;

namespace Memoria.FF8.App.Tests;

public sealed class FieldFpsSettingsTests
{
    [Fact]
    public void Missing_field_limit_defaults_to_60()
    {
        Assert.Equal(60, new UiPreferences().FieldFps);
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "{}");
            Assert.Equal(60, UiPreferences.Load(path).FieldFps);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(0, 60)]
    [InlineData(15, 30)]
    [InlineData(30, 30)]
    [InlineData(60, 60)]
    [InlineData(90, 60)]
    [InlineData(120, 60)]
    [InlineData(144, 60)]
    [InlineData(240, 60)]
    public void Existing_supported_field_limits_load_as_30_or_60_and_round_trip(int storedLimit, int expectedLimit)
    {
        UiPreferences stored = new() { FieldFps = storedLimit, BattleFps = 120, MenuFps = 0, MusicVolume = 25 };
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(stored));
            UiPreferences loaded = UiPreferences.Load(path);
            Assert.Equal(stored with { FieldFps = expectedLimit }, loaded);
            loaded.Save(path);
            Assert.Equal(loaded, UiPreferences.Load(path));
            using JsonDocument saved = JsonDocument.Parse(File.ReadAllText(path));
            Assert.Equal(expectedLimit, saved.RootElement.GetProperty("FieldFps").GetInt32());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    [InlineData(55)]
    [InlineData(90)]
    [InlineData(120)]
    [InlineData(144)]
    [InlineData(240)]
    public void Setting_and_saving_field_limits_require_30_or_60(int frameLimit)
    {
        UiPreferences preferences = new();
        Assert.Throws<ArgumentException>(() => preferences.WithSetting("Field FPS", frameLimit.ToString(CultureInfo.InvariantCulture)));
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        Assert.Throws<InvalidDataException>(() => (preferences with { FieldFps = frameLimit }).Save(path));
        Assert.False(File.Exists(path));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    [InlineData(55)]
    [InlineData(75)]
    [InlineData(300)]
    public void Unknown_stored_field_limits_remain_invalid(int frameLimit)
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(new UiPreferences { FieldFps = frameLimit }));
            Assert.Throws<InvalidDataException>(() => UiPreferences.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("Unlimited")]
    [InlineData("invalid")]
    [InlineData("99999999999999999999")]
    public void Field_limit_rejects_non_numeric_and_unlimited_values(string value)
    {
        Assert.Throws<ArgumentException>(() => new UiPreferences().WithSetting("Field FPS", value));
    }

    [Fact]
    public void Display_menu_exposes_only_30_and_60_field_choices()
    {
        MenuSetting setting = Assert.Single(MenuSettings.Definitions, setting => setting.Id == "Field FPS");
        Assert.Equal(["30", "60"], setting.Values);
        MenuData data = new();
        data.Settings["Field FPS"] = "60";
        data.SupportedActions.Add("config:Field FPS");
        List<MenuAction> applied = [];
        MenuController controller = new(() => data, action =>
        {
            applied.Add(action);
            data.Settings[action.SubjectId] = action.Value;
        });
        controller.Open("config-display");
        Assert.Equal("Field FPS", Assert.Single(controller.Entries).Id);
        controller.Navigate(MenuInput.Left);
        Assert.Equal("Field FPS", Assert.Single(applied).SubjectId);
        Assert.Equal("30", applied[0].Value);
        controller.Navigate(MenuInput.Right);
        Assert.Equal("60", applied[1].Value);
    }
}
