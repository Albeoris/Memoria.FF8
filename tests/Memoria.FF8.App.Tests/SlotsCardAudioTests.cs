using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class SlotsCardAudioTests
{
    [Fact]
    public void Installed_group_cues_preserve_playing_battle_music_and_output_preferences()
    {
        string directory = TestResources.GameDirectory;
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                using FieldAudioPresentation output = new(GetDesktopWindow(), directory, "");
                output.SetOutputPreferences(0, 0);
                output.PlayBattleMusicCommand(new(2, new(0, 5, true, true, false, 127, null), PauseOutput: true));
                Stopwatch deadline = Stopwatch.StartNew();
                while (!output.IsMidiPlaying && deadline.ElapsedMilliseconds < 3000)
                {
                    Thread.Sleep(20);
                }
                Assert.Null(output.Error);
                Assert.True(output.IsMidiPlaying);
                object? music = typeof(FieldAudioPresentation).GetField("music", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(output);
                int track = output.PrimaryMusicId;
                float volume = output.PrimaryMusicVolume;
                SlotsCardController cards = new(9, 0b101010101);
                cards.SoundRequested += output.PlayTripleTriadSound;
                cards.Begin();
                cards.Advance(TimeSpan.FromMilliseconds(340), false, false);
                cards.Navigate(MenuInput.Left);
                cards.Select(cards.FocusIndex);
                cards.Advance(TimeSpan.FromMilliseconds(250), false, false);
                Assert.Null(output.Error);
                Assert.True(output.IsMidiPlaying);
                Assert.Equal(track, output.PrimaryMusicId);
                Assert.Equal(volume, output.PrimaryMusicVolume);
                Assert.Same(music, typeof(FieldAudioPresentation).GetField("music", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(output));
                Assert.InRange(output.SoundVoiceCount, 1, 2);
                output.SetOutputPreferences(25, 40);
                Assert.True(output.IsMidiPlaying);
                output.SetOutputPreferences(0, 0);
                Assert.Null(output.Error);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "The card sound output check did not finish.");
        if (failure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    [DllImport("user32.dll")]
    private static extern nint GetDesktopWindow();
}
