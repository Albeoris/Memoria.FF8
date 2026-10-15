using System.Runtime.InteropServices;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeFootstepPlaybackTests
{
    [Fact]
    public void NativeMasksShareActualDeviceVoicesAndNarrowRawPan()
    {
        NativeFootstepSound left = new(3, 0, 3, 255, 17, 0x400000);
        NativeFootstepSound right = new(4, 1, 220037, 0, 119, 0x800000);
        Assert.Equal((3, 23, 17u, 127u), FieldAudioPresentation.ResolveFootstepPlayback(left));
        Assert.Equal((217, 24, 119u, 0u), FieldAudioPresentation.ResolveFootstepPlayback(right));
        Assert.Equal((3, 23, 17u, 64u), FieldAudioPresentation.ResolveFootstepPlayback(left with { Pan = 129 }));
        Assert.Equal(23, FieldAudioPresentation.ResolveFootstepPlayback(right with { Flags = 0x400000 }).Voice);
    }

    [Fact]
    public void UnsupportedChannelsAndOutOfRangeParametersAreRejectedBeforeDeviceOutput()
    {
        NativeFootstepSound footstep = new(0, 0, 3, 128, 119, 0x400000);
        foreach (uint mask in new[] { 0u, 1u, 0xC00000u, 0x1000000u })
        {
            Assert.Throws<NotSupportedException>(() => FieldAudioPresentation.ResolveFootstepPlayback(footstep with { Flags = mask }));
        }
        Assert.Throws<NotSupportedException>(() => FieldAudioPresentation.ResolveFootstepPlayback(footstep with { Pan = 256 }));
        Assert.Throws<NotSupportedException>(() => FieldAudioPresentation.ResolveFootstepPlayback(footstep with { Volume = 128 }));
    }

    [Fact]
    public void InstalledFallbackFootstepsHaveOriginalOneShotDescriptorsAndNonemptyPcm()
    {
        string directory = InstalledGame();
        foreach (int sample in new[] { 2, 3, 0x35, 0x36, 0x3F, 0x40 })
        {
            GlobalAudioSampleDescriptor descriptor = GlobalAudioFormat.Read(directory, sample);
            Assert.Equal(0u, descriptor.PlaybackFlags & 1);
            Assert.Equal(0u, descriptor.LoopStartPcmByte);
            Assert.NotEmpty(PcmAudioDecoder.ReadSample(directory, sample).Bytes);
        }
    }

    [Fact]
    public void SilentDeviceOutputBoundsRepeatedFootstepsAndSharesNativeStopVoices()
    {
        string directory = InstalledGame();
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                using FieldAudioPresentation presentation = new(GetDesktopWindow(), directory, string.Empty);
                presentation.SetOutputPreferences(0, 0);
                for (int step = 0; step < 64; step++)
                {
                    uint mask = step % 2 == 0 ? 0x400000u : 0x800000u;
                    presentation.Execute(new NativeFootstepSound(0, step % 2, step % 2 == 0 ? 3u : 2u, 128, 119, mask));
                    Assert.Null(presentation.Error);
                    Assert.InRange(presentation.SoundVoiceCount, 1, 2);
                }
                Assert.Equal(2, presentation.SoundVoiceCount);
                presentation.Execute(new NativeSoundVolume(1, 100, 0, 0, 0, 0, 0x400000, 17, 0x400000, 23, 17, 0, GlobalSoundBackendStatus.Muted, 0));
                presentation.Update();
                Assert.Null(presentation.Error);
                presentation.Execute(new NativeSoundStop(1, 100, 0, 0, 0, 0, 0x400000, 0x400000, [23], 0, 0, GlobalSoundBackendStatus.Muted));
                Assert.Equal(1, presentation.SoundVoiceCount);
                presentation.Execute(new NativeSoundStop(1, 100, 0, 0, 0, 0, 0x800000, 0x800000, [24], 0, 0, GlobalSoundBackendStatus.Muted));
                Assert.Null(presentation.Error);
                Assert.Equal(0, presentation.SoundVoiceCount);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "Silent footstep output did not finish.");
        if (failure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private static string InstalledGame()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original audio corpus is required.");
        return directory;
    }

    [DllImport("user32.dll")]
    private static extern nint GetDesktopWindow();
}
