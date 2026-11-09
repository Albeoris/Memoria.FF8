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

    public void PlayNativeMusic(int track)
    {
        if (track is < 0 or > 98)
        {
            throw new InvalidDataException("FFVII native music index exceeds the admitted table.");
        }
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
        int current = Process.NativeMusicTrack ?? (Process.Music is null ? 0 : ResolveNativeMusicTrack(Process.Music.Track));
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
            case 0xF0:
                if (Process.MusicLock == 0)
                {
                    PlayNativeMusic(0);
                }
                break;
            case 0xC0:
                Process = Process with { MusicVolume = Math.Clamp(audio.Parameter0, 0, 127) };
                break;
        }
        AudioRequested?.Invoke(audio);
    }
}
