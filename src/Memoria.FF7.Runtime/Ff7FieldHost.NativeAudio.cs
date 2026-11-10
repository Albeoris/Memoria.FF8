using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7FieldHost
{
    /// <summary>Publishes a native module audio command through the shared FFVII audio owner.</summary>
    public void SubmitNativeAudio(FieldAudioCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ApplyAudio(command);
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
            Process = Process with { SoundChannelLoopIds = new int[4], SoundChannelStoppedByFade = [true, true, true, true] };
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
