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
        switch (audio.Operation)
        {
            case 0x20 or 0x28 or 0x29 or 0x2A when audio.Parameter1 == 0:
                int[] stoppedVolumes = (int[])Process.SoundChannelVolumes.Clone();
                stoppedVolumes[audio.Operation switch { 0x29 => 1, 0x2A => 2, _ => 0 }] = 127;
                Process = Process with { SoundChannelVolumes = stoppedVolumes };
                break;
            case 0xA0:
            case 0xA1:
            case 0xA2:
                int[] channelVolumes = (int[])Process.SoundChannelVolumes.Clone();
                channelVolumes[audio.Operation - 0xA0] = Math.Min(unchecked((byte)audio.Parameter0), (byte)127);
                Process = Process with { SoundChannelVolumes = channelVolumes };
                break;
            case 0xA6 when unchecked((byte)audio.Parameter0) == 0:
                int[] immediateVolumes = (int[])Process.SoundChannelVolumes.Clone();
                immediateVolumes[2] = Math.Min(unchecked((byte)audio.Parameter1), (byte)127);
                Process = Process with { SoundChannelVolumes = immediateVolumes };
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

    public void AdvanceAudioClock(TimeSpan elapsed, bool paused = false)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(elapsed, TimeSpan.Zero);
        if (paused || Process.MusicVolumeFade is not { } fade)
        {
            return;
        }
        long ticks = checked(elapsed.Ticks + fade.SubmillisecondTicks);
        int milliseconds = unchecked(fade.AccumulatedMilliseconds + (int)(ticks / TimeSpan.TicksPerMillisecond));
        int step = unchecked(milliseconds * fade.RatePerSecond) / 1000;
        int volume = unchecked(Process.MusicVolume + step);
        if (step < 0 && volume < fade.Target || step > 0 && volume > fade.Target)
        {
            volume = fade.Target;
        }
        Process = Process with
        {
            MusicVolume = volume,
            MusicVolumeFade = volume == fade.Target ? null : fade with { AccumulatedMilliseconds = step == 0 ? milliseconds : 0, SubmillisecondTicks = (int)(ticks % TimeSpan.TicksPerMillisecond) }
        };
    }
}
