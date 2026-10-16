using Memoria.FF8.GameUi;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

public sealed class GlobalConfigurationTests
{
    [Fact]
    public void Legacy_preferences_import_once_and_controls_survive_a_different_save_directory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "memoria-global-" + Guid.NewGuid().ToString("N"));
        try
        {
            string firstSave = Path.Combine(directory, "first", "save.json");
            string secondSave = Path.Combine(directory, "second", "save.json");
            Directory.CreateDirectory(Path.GetDirectoryName(firstSave)!);
            Directory.CreateDirectory(Path.GetDirectoryName(secondSave)!);
            UiPreferences preferences = new UiPreferences().WithSetting("Cursor", "Memory").WithSetting("Field message", "5").WithSetting("Music volume", "25%");
            preferences.Save(Path.Combine(Path.GetDirectoryName(firstSave)!, "ui-settings.json"));
            ControlsProfile profile = ControlsProfile.CreateDefault().WithBinding(ControlAction.Confirm, ControlDevice.Keyboard, new([Keys.K], []));
            profile.Save(Path.Combine(Path.GetDirectoryName(firstSave)!, "controls.json"));
            GlobalSettingsPaths settings = new(Path.Combine(directory, "app-data"));
            settings.ImportLegacyFiles(firstSave);
            Assert.Equal(preferences, UiPreferences.Load(settings.UiPreferencesPath));
            ControlsProfile.CreateDefault().Save(Path.Combine(Path.GetDirectoryName(secondSave)!, "controls.json"));
            new UiPreferences().Save(Path.Combine(Path.GetDirectoryName(secondSave)!, "ui-settings.json"));
            settings.ImportLegacyFiles(secondSave);
            Assert.Equal(preferences, UiPreferences.Load(settings.UiPreferencesPath));
            KeyboardInput input = new() { Profile = ControlsProfile.Load(settings.ControlsPreferencesPath) };
            Assert.False(input.Read(new(Keys.X), true).Confirm);
            input.Read(default, true);
            Assert.True(input.Read(new(Keys.K), true).Confirm);
            Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(firstSave)!, "controls.json")));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void Saved_controller_binding_is_consumed_by_the_same_global_input_sampler()
    {
        string path = Path.Combine(Path.GetTempPath(), "memoria-controls-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            ControlsProfile profile = ControlsProfile.CreateDefault().WithBinding(ControlAction.Confirm, ControlDevice.Controller, new([], [Buttons.LeftStick])).WithControllerSettings(0.25f, 0.3f, 0.6f, vibrationEnabled: false);
            profile.Save(path);
            KeyboardInput input = new() { Profile = ControlsProfile.Load(path) };
            Assert.False(input.Profile.VibrationEnabled);
            input.Read(default, new(Vector2.Zero, Vector2.Zero, 0, 0, []), true);
            Assert.True(input.Read(default, new(Vector2.Zero, Vector2.Zero, 0, 0, [Buttons.LeftStick]), true).Confirm);
            Assert.Equal((ushort)0x40, input.HeldFieldButtons);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("0")]
    [InlineData("6")]
    [InlineData("Fast")]
    public void Field_message_preference_rejects_invalid_speed(string value)
    {
        Assert.Throws<ArgumentException>(() => new UiPreferences().WithSetting("Field message", value));
    }

    [Fact]
    public void Cursor_initial_resets_new_entries_and_memory_preserves_them()
    {
        UiPreferences preferences = new();
        MenuController menu = new(() => new MenuData());
        menu.ConnectPreferences(() => preferences, _ => { });
        menu.Open("main");
        menu.Navigate(MenuInput.Down);
        Assert.Equal(1, menu.SelectedIndex);
        menu.Close();
        menu.Open("main");
        Assert.Equal(0, menu.SelectedIndex);
        preferences = preferences.WithSetting("Cursor", "Memory");
        menu.Navigate(MenuInput.Down);
        menu.Close();
        menu.Open("main");
        Assert.Equal(1, menu.SelectedIndex);
        menu.Push("config");
        menu.Back();
        Assert.Equal(1, menu.SelectedIndex);
    }

    [Fact]
    public void Unconnected_menu_keeps_its_selection_without_an_authoritative_cursor_preference()
    {
        MenuController menu = new(() => new MenuData());
        menu.Open("main");
        menu.Navigate(MenuInput.Down);
        menu.Close();
        menu.Open("main");
        Assert.Equal(1, menu.SelectedIndex);
    }
}
