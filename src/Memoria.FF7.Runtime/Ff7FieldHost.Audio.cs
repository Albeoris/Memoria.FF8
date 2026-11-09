using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7FieldHost
{
    public int ResolveNativeMusicTrack(int localTrack)
    {
        if ((uint)localTrack >= field.Scripts.AkaoOffsets.Count)
        {
            throw new InvalidDataException("FFVII local music index exceeds the original AKAO directory.");
        }
        int offset = checked((int)field.Scripts.AkaoOffsets[localTrack]);
        ReadOnlySpan<byte> data = field.Scripts.Data.Span;
        if (offset > data.Length - 5 || !data.Slice(offset, 4).SequenceEqual("AKAO"u8) || data[offset + 4] == 0)
        {
            throw new InvalidDataException("FFVII local music requires a bounded original AKAO header and nonzero native ID.");
        }
        return data[offset + 4] - 1;
    }

    public static int NormalizeNativeMusicTrack(int track)
    {
        if (track is < 0 or > 98)
        {
            throw new InvalidDataException("FFVII native music index exceeds the admitted table.");
        }
        return track == 1 ? 2 : track;
    }

    public void PlayNativeMusic(int track)
    {
        track = NormalizeNativeMusicTrack(track);
        if (Process.NativeMusicTrack == track)
        {
            return;
        }
        Process = Process with { NativeMusicTrack = track, MusicTicks = 0 };
    }

    private void ApplyMusic(FieldMusicCommand music)
    {
        if (music.Operation == FieldMusicOperation.Lock)
        {
            Process = Process with { MusicLock = checked((byte)music.Track) };
            return;
        }
        if (Process.MusicLock != 0)
        {
            Process = music.Operation switch
            {
                FieldMusicOperation.NextField => Process with { NextFieldMusicTrack = -1 },
                FieldMusicOperation.NextBattle => Process with { NextBattleMusicTrack = -1 },
                _ => Process
            };
            return;
        }
        if (music.Operation == FieldMusicOperation.NextField)
        {
            Process = Process with { NextFieldMusicTrack = music.Track };
            return;
        }
        if (music.Operation == FieldMusicOperation.NextBattle)
        {
            Process = Process with { NextBattleMusicTrack = music.Track };
            return;
        }
        int current = Process.NativeMusicTrack ?? (Process.Music is null || Process.Music.Track < 0 ? 0 : ResolveNativeMusicTrack(Process.Music.Track));
        int target = music.Operation == FieldMusicOperation.SwapSaved ? Process.SavedNativeMusicTrack ?? (Process.SavedMusicTrack < 0 ? 0 : ResolveNativeMusicTrack(Process.SavedMusicTrack)) : ResolveNativeMusicTrack(music.Track);
        Process = music.Operation switch
        {
            FieldMusicOperation.Play => Process with { Music = music },
            FieldMusicOperation.SaveCurrentAndPlay => Process with { SavedMusicTrack = Process.Music?.Track ?? -1, SavedNativeMusicTrack = current, Music = music },
            FieldMusicOperation.SwapSaved => Process with { SavedMusicTrack = Process.Music?.Track ?? -1, SavedNativeMusicTrack = current, Music = new FieldMusicCommand(FieldMusicOperation.Play, Process.SavedMusicTrack) },
            _ => throw new NotSupportedException("Unsupported FFVII music operation.")
        };
        PlayNativeMusic(target);
    }

    private void ApplyAudio(FieldAudioCommand audio)
    {
        Process = Process with { Audio = audio };
        int? restartedChannel = null;
        switch (audio.Operation)
        {
            case 0x20 or 0x28 or 0x29 or 0x2A or 0x2B:
                int soundChannel = audio.Operation switch { 0x29 => 2, 0x2A => 3, 0x2B => 4, _ => 1 };
                if (audio.Parameter1 == 0)
                {
                    SetSoundVolume(soundChannel, 0, 127);
                }
                restartedChannel = soundChannel;
                break;
            case 0xA0:
            case 0xA1:
            case 0xA2:
            case 0xA3:
                SetSoundVolume(audio.Operation - 0xA0 + 1, 0, unchecked((byte)audio.Parameter0));
                break;
            case 0xA4:
            case 0xA5:
            case 0xA6:
            case 0xA7:
                SetSoundVolume(audio.Operation - 0xA4 + 1, unchecked((byte)audio.Parameter0), unchecked((byte)audio.Parameter1));
                break;
            case 0xF0:
                PlayNativeMusic(0);
                break;
            case 0xC0:
                int volume = unchecked((byte)audio.Parameter0);
                if (volume <= 127)
                {
                    Process = Process with { MusicVolume = volume, MusicVolumeFade = null };
                }
                break;
            case 0xC2:
                SetMusicVolumeFade(unchecked((byte)audio.Parameter0), unchecked((byte)audio.Parameter1), unchecked((byte)audio.Parameter2));
                break;
            case 0xC1:
                ChangeMusicVolume(unchecked((byte)audio.Parameter0), unchecked((byte)audio.Parameter1));
                break;
        }
        AudioRequested?.Invoke(audio);
        if (restartedChannel is int restarted)
        {
            bool[] stopped = (bool[])Process.SoundChannelStoppedByFade.Clone();
            stopped[restarted - 1] = false;
            Process = Process with { SoundChannelStoppedByFade = stopped };
        }
    }

    private void SetMusicVolumeFade(byte duration, byte initial, byte target)
    {
        if (target > 127 || duration != 0 && initial > 127)
        {
            return;
        }
        if (duration == 0)
        {
            Process = Process with { MusicVolume = target, MusicVolumeFade = null };
            return;
        }
        Process = Process with { MusicVolume = initial, MusicVolumeFade = null };
        ChangeMusicVolume(duration, target);
    }

    private void ChangeMusicVolume(byte duration, byte target)
    {
        if (target > 127)
        {
            return;
        }
        if (duration == 0)
        {
            Process = Process with { MusicVolume = target, MusicVolumeFade = null };
            return;
        }
        if (Process.MusicVolume == target)
        {
            return;
        }
        int rate = (int)((target - Process.MusicVolume) / (duration / 60d));
        Process = Process with { MusicVolumeFade = new(target, rate) };
    }

    private void SetSoundVolume(int channel, byte duration, byte target)
    {
        target = Math.Min(target, (byte)127);
        int index = channel - 1;
        if (duration != 0 && Process.SoundChannelVolumes[index] == target)
        {
            return;
        }
        int[] volumes = (int[])Process.SoundChannelVolumes.Clone();
        Ff7SoundVolumeFade?[] fades = (Ff7SoundVolumeFade?[])Process.SoundChannelVolumeFades.Clone();
        if (duration == 0)
        {
            volumes[index] = target;
            fades[index] = null;
        }
        else
        {
            int rate = (int)((target - volumes[index]) / (duration / 60d));
            fades[index] = new(target, rate);
        }
        Process = Process with { SoundChannelVolumes = volumes, SoundChannelVolumeFades = fades };
    }

    public void AdvanceAudioClock(TimeSpan elapsed, bool paused = false)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(elapsed, TimeSpan.Zero);
        if (paused)
        {
            return;
        }
        if (Process.MusicVolumeFade is { } fade)
        {
            long ticks = checked(elapsed.Ticks + fade.SubmillisecondTicks);
            (int volume, int accumulated) = Ff7NativeVolumeRamp.Advance(Process.MusicVolume, fade.Target, fade.RatePerSecond, fade.AccumulatedMilliseconds, unchecked((int)(ticks / TimeSpan.TicksPerMillisecond)));
            Process = Process with
            {
                MusicVolume = volume,
                MusicVolumeFade = volume == fade.Target ? null : fade with { AccumulatedMilliseconds = accumulated, SubmillisecondTicks = (int)(ticks % TimeSpan.TicksPerMillisecond) }
            };
        }
        long soundTicks = checked(elapsed.Ticks + Process.SoundVolumeClockSubmillisecondTicks);
        int milliseconds = unchecked((int)(soundTicks / TimeSpan.TicksPerMillisecond));
        int[] volumes = (int[])Process.SoundChannelVolumes.Clone();
        Ff7SoundVolumeFade?[] fades = (Ff7SoundVolumeFade?[])Process.SoundChannelVolumeFades.Clone();
        bool[] stopped = (bool[])Process.SoundChannelStoppedByFade.Clone();
        for (int index = 0; index < fades.Length; index++)
        {
            if (fades[index] is not { } current)
            {
                continue;
            }
            if (!current.Started)
            {
                fades[index] = current with { Started = true };
                continue;
            }
            (int volume, int accumulated) = Ff7NativeVolumeRamp.Advance(volumes[index], current.Target, current.RatePerSecond, current.AccumulatedMilliseconds, milliseconds);
            volumes[index] = volume;
            fades[index] = volume == current.Target ? null : current with { AccumulatedMilliseconds = accumulated };
            if (volume == current.Target && volume == 0)
            {
                stopped[index] = true;
            }
        }
        Process = Process with { SoundChannelVolumes = volumes, SoundChannelVolumeFades = fades, SoundChannelStoppedByFade = stopped, SoundVolumeClockSubmillisecondTicks = (int)(soundTicks % TimeSpan.TicksPerMillisecond) };
    }
}
