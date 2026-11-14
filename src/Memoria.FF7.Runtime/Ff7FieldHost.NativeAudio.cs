using Memoria.FF7.Events;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7FieldHost
{
    public Func<int, Ff7NativeMusicTimeline>? NativeMusicTimeline { get; set; }

    public Ff7NativeMusicTimeline DescribeNativeMusic(int track)
    {
        return (NativeMusicTimeline ?? throw new NotSupportedException("FFVII music playback status requires its original MIDI timeline source."))(NormalizeNativeMusicTrack(track));
    }

    public bool ReadNativeMusicPlaying()
    {
        int track = Process.NativeMusicTrack ?? (Process.Music is null || Process.Music.Track < 0 ? 0 : ResolveNativeMusicTrack(Process.Music.Track));
        return track != 0 && DescribeNativeMusic(track).IsPlaying(Process.MusicTicks);
    }

    /// <summary>Publishes a native module audio command through the shared FFVII audio owner.</summary>
    public void SubmitNativeAudio(FieldAudioCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ApplyAudio(command);
    }

    public void ResetNativeSoundPitches()
    {
        for (int channel = 1; channel <= 5; channel++)
        {
            SetSoundFrequency(channel, 0, 0);
        }
    }

    private void ApplyNativeModuleAudio(FieldAudioCommand command)
    {
        if (command.Operation is 0x10 or 0x14 or 0x18)
        {
            int track = NormalizeNativeMusicTrack(command.Parameter0);
            if (command.Operation == 0x18)
            {
                if (command.Parameter1 is < 0 or >= 10)
                {
                    throw new NotSupportedException("FFVII music transitions of ten or more native duration units require their pending MIDI transition contract.");
                }
                Process = Process with { MusicVolume = 127, MusicVolumeFade = null };
            }
            PlayNativeMusic(track);
        }
        else if (command.Operation == 0xF1)
        {
            Process = Process with { SoundChannelLoopIds = new int[5], SoundChannelStoppedByFade = [true, true, true, true, true] };
        }
    }

    public void AdvanceNativeMusicClock()
    {
        if (Process.NativeMusicTrack is > 0)
        {
            Process = Process with { MusicTicks = checked(Process.MusicTicks + 1) };
        }
    }
}
