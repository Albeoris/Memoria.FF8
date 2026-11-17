using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;

namespace Memoria.FF8.App.Tests;

public sealed class BattleAudioPresentationTests
{
    [Fact]
    public void InstalledActorSoundsAndMusicResolveThroughOriginalTables()
    {
        string directory = InstalledGame();
        NativeMusicCatalog catalog = new(directory);
        Assert.Equal("005s-battle.sgt", Path.GetFileName(catalog.MusicFile(5, -1)), ignoreCase: true);
        Assert.Equal("001s-win.sgt", Path.GetFileName(catalog.MusicFile(1, -1)), ignoreCase: true);
        Assert.Equal("000s-lose.sgt", Path.GetFileName(catalog.MusicFile(0, -1)), ignoreCase: true);
        Assert.Equal(210009u, catalog.BattleActorSound(0, 1));
        Assert.Equal(159, EventEngine.MapGlobalSoundSample(catalog.BattleActorSound(0, 1)));
        Assert.Equal(0u, catalog.BattleActorSound(16, 0));
        Assert.Equal(220037u, catalog.BattleActorSound(17, 0));
        Assert.Equal(220025u, catalog.BattleActorSound(38, 0));
        foreach (int actor in Enumerable.Range(0, 11).Concat([17, 38]))
        {
            uint effect = catalog.BattleActorSound(actor, actor == 0 ? 1 : 0);
            PcmAudio pcm = PcmAudioDecoder.ReadSample(directory, EventEngine.MapGlobalSoundSample(effect));
            Assert.NotEmpty(pcm.Bytes);
            Assert.Contains(pcm.Bytes, value => value != 0);
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => catalog.BattleActorSound(160, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => catalog.BattleActorSound(0, 7));
        Assert.Equal(1u, catalog.MenuSound((int)MenuSound.Move));
        Assert.Equal(1u, catalog.MenuSound((int)MenuSound.Confirm));
        Assert.Equal(9u, catalog.MenuSound((int)MenuSound.Cancel));
        foreach (MenuSound cue in Enum.GetValues<MenuSound>())
        {
            PcmAudio pcm = PcmAudioDecoder.ReadSample(directory, EventEngine.MapGlobalSoundSample(catalog.MenuSound((int)cue)));
            Assert.NotEmpty(pcm.Bytes);
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => catalog.MenuSound(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => catalog.MenuSound(48));
    }

    [Fact]
    public void SilentDeviceBridgePlaysOriginalBattleMusicAndMenuAudio()
    {
        string directory = InstalledGame();
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                string tools = new MovieClock(GameSession.CreateColdProcess(), directory).ToolsDirectory;
                using FieldAudioPresentation presentation = new(directory, tools);
                presentation.SetOutputPreferences(0, 0);
                presentation.ResumeMusic(NativeMusicPlaybackState.FromColdProcess().PlayEncoded46B530(0, 6, 127));
                Assert.Null(presentation.Error);
                presentation.PlayUiSound(MenuSound.Move);
                presentation.PlayUiSound(MenuSound.Confirm);
                presentation.PlayUiSound(MenuSound.Cancel);
                Assert.Null(presentation.Error);
                presentation.ResumeMusic(NativeMusicPlaybackState.FromColdProcess().PlayEncoded46B530(0, 2, 127));
                Assert.Null(presentation.Error);
                presentation.StopAll();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        if (OperatingSystem.IsWindows())
        {
            thread.SetApartmentState(ApartmentState.STA);
        }
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "Silent battle audio smoke did not finish.");
        if (failure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private static string InstalledGame()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original audio corpus is required.");
        return directory;
    }
}
