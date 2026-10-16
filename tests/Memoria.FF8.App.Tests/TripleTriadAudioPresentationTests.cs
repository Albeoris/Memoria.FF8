using System.Diagnostics;
using System.Runtime.InteropServices;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class TripleTriadAudioPresentationTests
{
    [Fact]
    public void NativeModuleSoundIdentitiesRemainDistinctFromMenuCueNumbers()
    {
        Assert.Equal(1u, FieldAudioPresentation.ResolveTripleTriadSound(TripleTriadSound.Move));
        Assert.Equal(1u, FieldAudioPresentation.ResolveTripleTriadSound(TripleTriadSound.Confirm));
        Assert.Equal(9u, FieldAudioPresentation.ResolveTripleTriadSound(TripleTriadSound.Cancel));
        Assert.Equal(16u, FieldAudioPresentation.ResolveTripleTriadSound(TripleTriadSound.Error));
        Assert.Equal(89u, FieldAudioPresentation.ResolveTripleTriadSound(TripleTriadSound.Place));
        Assert.Equal(90u, FieldAudioPresentation.ResolveTripleTriadSound(TripleTriadSound.Flip));
        foreach (TripleTriadSound cue in new[] { TripleTriadSound.Same, TripleTriadSound.Plus, TripleTriadSound.Combo, TripleTriadSound.Lose, TripleTriadSound.Draw })
        {
            Assert.Null(FieldAudioPresentation.ResolveTripleTriadSound(cue));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => FieldAudioPresentation.ResolveTripleTriadSound((TripleTriadSound)100));
    }

    [Fact]
    public void InstalledNativeCardMusicAndEffectPcmReachSilentWindowsDevices()
    {
        string directory = LaunchOptions.DefaultGameDirectory;
        var catalog = new NativeMusicCatalog(directory);
        Assert.Equal("070s-card.sgt", Path.GetFileName(catalog.MusicFile(FieldAudioPresentation.TripleTriadMusicTrack, -1)));
        Assert.Equal("001s-Win.sgt", Path.GetFileName(catalog.MusicFile(FieldAudioPresentation.TripleTriadVictoryTrack, -1)));
        foreach (uint effect in new uint[] { 1, 9, 16, 89, 90 })
        {
            PcmAudio pcm = PcmAudioDecoder.ReadSample(directory, EventEngine.MapGlobalSoundSample(effect));
            Assert.True(pcm.Bytes.Length > 0);
            Assert.Contains(pcm.Bytes, value => value != 0);
        }
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var output = new FieldAudioPresentation(GetDesktopWindow(), directory, "");
                output.SetOutputPreferences(0, 0);
                output.PlayTripleTriadMusic();
                var deadline = Stopwatch.StartNew();
                while (!output.IsMidiPlaying && deadline.ElapsedMilliseconds < 3000)
                {
                    Thread.Sleep(20);
                }
                Assert.Null(output.Error);
                Assert.True(output.IsMidiPlaying, "DirectMusic did not start the original card segment.");
                output.PlayTripleTriadSound(TripleTriadSound.Place);
                output.PlayTripleTriadSound(TripleTriadSound.Flip);
                Assert.Null(output.Error);
                Assert.Equal(2, output.SoundVoiceCount);
                output.PlayTripleTriadMusic(victory: true);
                Assert.Null(output.Error);
                Assert.Equal(1, output.PrimaryMusicId);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "Silent card audio device check did not finish.");
        if (failure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    [DllImport("user32.dll")]
    private static extern nint GetDesktopWindow();
}
