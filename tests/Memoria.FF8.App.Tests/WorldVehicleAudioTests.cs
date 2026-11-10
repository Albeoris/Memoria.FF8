using System.Collections;
using System.Reflection;
using Memoria.FF8.Audio;
using Memoria.FF8.Assets;

namespace Memoria.FF8.App.Tests;

public sealed class WorldVehicleAudioTests
{
    [Theory]
    [InlineData(48, 0, 60, -108)]
    [InlineData(48, 64, 92, -108)]
    [InlineData(48, 200, 127, -108)]
    [InlineData(50, 0, 80, -106)]
    [InlineData(50, 128, 112, -106)]
    [InlineData(50, 200, 127, -106)]
    public void Original_engine_sample_loops_with_native_speed_gain_without_restarting(int entity, int speed, int volume, int voice)
    {
        PcmMixer mixer = new();
        using FieldAudioPresentation audio = new(TestResources.GameDirectory, "") { OutputFactory = () => new(mixer) };
        audio.PresentWorldVehicleSounds(new(entity, speed, 0, 0));
        Assert.Null(audio.Error);
        Assert.Equal(1, audio.SoundVoiceCount);
        object active = Voices(audio)[voice]!;
        PcmAudioOutput.Buffer primary = Property<PcmAudioOutput.Buffer>(active, "Primary");
        PcmAudioOutput.Buffer? loop = Property<PcmAudioOutput.Buffer?>(active, "Loop");
        Assert.True(primary.IsPlaying);
        Assert.Equal(volume, Property<float>(active, "Volume"));
        GlobalAudioSampleDescriptor descriptor = GlobalAudioFormat.Read(TestResources.GameDirectory, entity == 48 ? 2788 : 2786);
        Assert.True(loop is not null || (descriptor.PlaybackFlags & 1) != 0);
        short[] output = new short[8192];
        mixer.Render(output);
        Assert.Contains(output, sample => sample != 0);
        int position = primary.PositionBytes;
        Assert.True(position > 0);
        audio.PresentWorldVehicleSounds(new(entity, 0, 0, 0));
        Assert.Same(active, Voices(audio)[voice]);
        Assert.Equal(position, primary.PositionBytes);
        Assert.Equal(entity == 48 ? 60 : 80, Property<float>(active, "Volume"));
        audio.PresentWorldVehicleSounds(new(0, 0, 0, 0));
        Assert.Equal(0, audio.SoundVoiceCount);
        Assert.True(primary.IsDisposed);
        Assert.True(loop is null || loop.IsDisposed);
    }

    [Theory]
    [InlineData(33, -400, 0)]
    [InlineData(34, 0, 64)]
    [InlineData(33, 400, 127)]
    public void Garden_water_wake_uses_height_gain_and_stops_when_leaving_water(byte ground, int height, int volume)
    {
        using FieldAudioPresentation audio = new(TestResources.GameDirectory, "") { OutputFactory = () => new(new PcmMixer()) };
        audio.PresentWorldVehicleSounds(new(48, 0, height, ground));
        Assert.Null(audio.Error);
        Assert.Equal(2, audio.SoundVoiceCount);
        object wake = Voices(audio)[-109]!;
        Assert.Equal(volume, Property<float>(wake, "Volume"));
        audio.PresentWorldVehicleSounds(new(48, 0, height, 0));
        Assert.Equal(1, audio.SoundVoiceCount);
        Assert.True(Property<PcmAudioOutput.Buffer>(wake, "Primary").IsDisposed);
        audio.PresentWorldVehicleSounds(new(50, 0, height, ground));
        Assert.Equal(1, audio.SoundVoiceCount);
        Assert.NotNull(Voices(audio)[-106]);
        audio.PresentWorldVehicleSounds(null);
        Assert.Equal(0, audio.SoundVoiceCount);
    }

    private static IDictionary Voices(FieldAudioPresentation audio) => (IDictionary)typeof(FieldAudioPresentation).GetField("voices", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(audio)!;

    private static T Property<T>(object instance, string name) => (T)instance.GetType().GetProperty(name)!.GetValue(instance)!;

}
