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
        int track = ReadNativeMusicPlaybackTrack();
        return track is > 0 and <= 98 && DescribeNativeMusic(track).IsPlaying(Process.MusicTicks);
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
            if (command.Operation == 0x10 && command.Parameter0 is < 0 or > 98)
            {
                return;
            }
            int track = NormalizeNativeMusicTrack(command.Parameter0);
            if (command.Operation == 0x18)
            {
                BeginNativeMusicTransition(track, unchecked((byte)command.Parameter1));
                return;
            }
            PlayNativeMusic(track);
        }
        else if (command.Operation == 0xF1)
        {
            Process = Process with { SoundChannelLoopIds = new int[5], SoundChannelStoppedByFade = [true, true, true, true, true] };
        }
    }

    private void BeginNativeMusicTransition(int track, int duration)
    {
        Process = Process with { MusicTransition = null };
        bool playing = Process.NativeMusicTrack is > 0 && (NativeMusicTimeline is null || ReadNativeMusicPlaying());
        if (track == 0 || !playing)
        {
            Process = Process with { MusicVolume = 127, MusicVolumeFade = null };
            PlayNativeMusic(track);
            return;
        }
        if (Process.NativeMusicTrack == track)
        {
            Process = Process with { MusicTransition = new(track, duration, Ff7MusicTransitionPhase.Gain) };
            BeginNativeTransitionGain(127, duration);
            return;
        }
        int half = duration / 2;
        Process = Process with { MusicTransition = new(track, half, Ff7MusicTransitionPhase.FadeOut) };
        BeginNativeTransitionGain(15, half);
    }

    private void BeginNativeTransitionGain(int target, int duration)
    {
        if (duration < 10)
        {
            Process = Process with { MusicVolume = target, MusicVolumeFade = null };
            CompleteNativeMusicTransitionPhase();
            return;
        }
        int rate = (int)((target - Process.MusicVolume) / (duration / 60d));
        Process = Process with { MusicVolumeFade = new(target, rate) };
        AdvanceNativeMusicTransition(TimeSpan.Zero);
    }

    private void AdvanceNativeMusicTransition(TimeSpan elapsed)
    {
        if (Process.MusicTransition is null || Process.MusicVolumeFade is not { } fade)
        {
            return;
        }
        long ticks = checked(elapsed.Ticks + fade.SubmillisecondTicks);
        int milliseconds = unchecked((int)(ticks / TimeSpan.TicksPerMillisecond));
        int step = unchecked(milliseconds * fade.RatePerSecond) / 1000;
        if (step == 0)
        {
            step = fade.RatePerSecond > 0 ? 1 : -1;
        }
        int volume = unchecked(Process.MusicVolume + step);
        if (step < 0 && volume < fade.Target || step > 0 && volume > fade.Target)
        {
            volume = fade.Target;
        }
        Process = Process with { MusicVolume = volume, MusicVolumeFade = volume == fade.Target ? null : fade with { AccumulatedMilliseconds = 0, SubmillisecondTicks = (int)(ticks % TimeSpan.TicksPerMillisecond) } };
        if (volume == fade.Target)
        {
            CompleteNativeMusicTransitionPhase();
        }
    }

    private void CompleteNativeMusicTransitionPhase()
    {
        Ff7MusicTransition transition = Process.MusicTransition ?? throw new InvalidOperationException("A native MIDI transition completed without its owner.");
        if (transition.Phase == Ff7MusicTransitionPhase.FadeOut)
        {
            Process = Process with { NativeMusicTrack = transition.Track, MusicTicks = 0, MusicTransition = transition with { Phase = Ff7MusicTransitionPhase.FadeIn } };
            BeginNativeTransitionGain(127, transition.Duration);
        }
        else
        {
            Process = Process with { MusicTransition = null };
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
