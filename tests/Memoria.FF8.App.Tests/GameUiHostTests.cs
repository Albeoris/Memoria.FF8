using Memoria.FF8.GameUi;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

public sealed class GameUiHostTests
{
    [Fact]
    public void Debug_room_and_menu_keys_are_edges_and_do_not_invent_player_actions()
    {
        var keyboard = new KeyboardInput();
        var input = keyboard.Read(new KeyboardState(Keys.F2, Keys.V, Keys.X), true);
        Assert.True(keyboard.DebugRoomRequested);
        Assert.True(keyboard.GameMenuRequested);
        Assert.True(keyboard.UiConfirmRequested);
        Assert.False(input.Confirm);
        Assert.False(input.Save);
        keyboard.Read(new KeyboardState(Keys.F2, Keys.V, Keys.X), true);
        Assert.False(keyboard.DebugRoomRequested);
        Assert.False(keyboard.GameMenuRequested);
        keyboard.Read(new KeyboardState(), true);
        keyboard.Read(new KeyboardState(Keys.F2, Keys.V), false);
        Assert.False(keyboard.DebugRoomRequested);
        Assert.False(keyboard.GameMenuRequested);
    }

    [Fact]
    public void Audio_preferences_attenuate_native_gains_without_replacing_the_native_volume()
    {
        Assert.Equal(-300, FieldAudioPresentation.OutputGain(-300, 100));
        Assert.Equal(-902, FieldAudioPresentation.OutputGain(-300, 50));
        Assert.Equal(-10000, FieldAudioPresentation.OutputGain(0, 0));
        Assert.Equal(-10000, FieldAudioPresentation.OutputGain(-9999, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => FieldAudioPresentation.OutputGain(0, 101));
    }

    [Fact]
    public void Host_preferences_persist_and_validate_in_their_own_file()
    {
        string directory = Path.Combine(Path.GetTempPath(), "memoria-ui-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "ui-settings.json");
        try
        {
            var preferences = UiPreferences.Load(path).WithSetting("Music volume", "25%").WithSetting("Effect volume", "50%");
            preferences.Save(path);
            Assert.Equal(preferences, UiPreferences.Load(path));
            Assert.Throws<NotSupportedException>(() => preferences.WithSetting("ATB", "50"));
            Assert.Throws<ArgumentException>(() => preferences.WithSetting("Music volume", "150%"));
            File.WriteAllText(path, "{\"MusicVolume\":-1}");
            Assert.Throws<InvalidDataException>(() => UiPreferences.Load(path));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory);
            }
        }
    }
}
