using Memoria.FF8.Runtime;
using Memoria.FF8.Assets;
using System.Runtime.InteropServices;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleModuleSoundTests
{
    [Theory]
    [InlineData(2, 0)]
    [InlineData(2, 31)]
    [InlineData(3, 127)]
    [InlineData(2, 128)]
    [InlineData(3, 175)]
    [InlineData(3, 255)]
    [InlineData(5, 80)]
    [InlineData(6, 175)]
    public void Native501740_uses_the_committed_projection_pan(ushort sound, byte pan)
    {
        Assert.Equal((uint)pan >> 1, FieldAudioPresentation.ResolveNativeModuleSoundPan(new(sound, 0, new(1, 2, 3, 4)) { Pan = pan }));
    }

    [Fact]
    public void Missing_native_pan_is_rejected_instead_of_being_centered()
    {
        Assert.Throws<NotSupportedException>(() => FieldAudioPresentation.ResolveNativeModuleSoundPan(new(2, 0, new(1, 2, 3, 4))));
        Assert.Throws<NotSupportedException>(() => FieldAudioPresentation.ResolveNativeModuleSoundPan(new(0x72, 0x8000, new(1, 2, 3, 4))));
        Assert.Throws<NotSupportedException>(() => FieldAudioPresentation.ResolveNativeModuleSoundPan(new(0x0D, 8, new(1, 2, 3, 4))));
        Assert.Throws<NotSupportedException>(() => FieldAudioPresentation.ResolveNativeModuleSoundPan(new(0x0E, 16, new(1, 2, 3, 4))));
    }

    [Theory]
    [InlineData(0x0D, 8, 80, 4, 40)]
    [InlineData(0x0E, 16, 175, 5, 87)]
    [InlineData(0x72, 0x8000, 129, 16, 64)]
    [InlineData(300000, 0x100, 128, 9, 64)]
    [InlineData(300000, 0x100, 175, 9, 87)]
    public void Positioned_contact_sounds_use_native_attributes_and_shifted_pan(uint sound, ushort flags, byte pan, int voice, uint devicePan)
    {
        NativeBattleModuleSound cue = new(sound, flags, new(1, 2, 3, 4)) { Pan = pan };
        Assert.Equal(devicePan, FieldAudioPresentation.ResolveNativeModuleSoundPan(cue));
        Assert.Equal(voice, FieldAudioPresentation.ResolveNativeModuleSoundVoice(cue, -11));
    }

    [Fact]
    public void Shared_native_sound_routes_use_the_original_mask_instead_of_effect_specific_admission()
    {
        Assert.Equal(64u, FieldAudioPresentation.ResolveNativeModuleSoundPan(new(0x0E, 8, null) { Pan = 128 }));
        Assert.Equal(10u, FieldAudioPresentation.ResolveNativeModuleSoundPan(new(4, 0, null) { Pan = 20 }));
        Assert.Equal(1, FieldAudioPresentation.ResolveNativeModuleSoundVoice(new(0x0D, 1, null), -11));
        Assert.Equal(4, FieldAudioPresentation.ResolveNativeModuleSoundVoice(new(300000, 0x800008, null), -11));
        Assert.Equal(24, FieldAudioPresentation.ResolveNativeModuleSoundVoice(new(4, 0x800000, null), -11));
        Assert.Equal(-11, FieldAudioPresentation.ResolveNativeModuleSoundVoice(new(4, 0xFF000000, null), -11));
        Assert.Equal(-11, FieldAudioPresentation.ResolveNativeModuleSoundVoice(new(2, 0, new(1, 2, 3, 4)), -11));
    }

    [Fact]
    public void Installed_positioned_samples_replace_their_fixed_device_voice_and_other_attributes_overlap()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original audio corpus is required.");
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                string tools = new MovieClock(GameSession.CreateColdProcess(), directory).ToolsDirectory;
                using FieldAudioPresentation output = new(GetDesktopWindow(), directory, tools);
                output.SetOutputPreferences(0, 0);
                foreach ((uint sound, ushort flags) in new (uint, ushort)[] { (0x0D, 8), (0x0E, 16), (0x72, 0x8000), (300000, 0x100) })
                {
                    PcmAudio pcm = PcmAudioDecoder.ReadSample(directory, Memoria.FF8.Events.EventEngine.MapGlobalSoundSample(sound));
                    Assert.NotEmpty(pcm.Bytes);
                    NativeBattleModuleSound cue = new(sound, flags, new(1, 2, 3, 4)) { Pan = 128 };
                    int before = output.SoundVoiceCount;
                    output.PlayBattleModuleSound(cue);
                    Assert.Null(output.Error);
                    Assert.Equal(before + 1, output.SoundVoiceCount);
                    output.PlayBattleModuleSound(cue with { Pan = 175 });
                    Assert.Null(output.Error);
                    Assert.Equal(before + 1, output.SoundVoiceCount);
                }
                Assert.Equal(4, output.SoundVoiceCount);
                foreach (uint castingSound in new uint[] { 5, 6 })
                {
                    Assert.NotEmpty(PcmAudioDecoder.ReadSample(directory, Memoria.FF8.Events.EventEngine.MapGlobalSoundSample(castingSound)).Bytes);
                    output.PlayBattleModuleSound(new(castingSound, 0, new(1, 2, 3, 4)) { Pan = 128 });
                    Assert.Null(output.Error);
                }
                Assert.Equal(6, output.SoundVoiceCount);
                output.StopSounds();
                Assert.Equal(0, output.SoundVoiceCount);
                Assert.Null(output.Error);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "Positioned battle sound output did not finish.");
        if (failure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    [DllImport("user32.dll")]
    private static extern nint GetDesktopWindow();
}
