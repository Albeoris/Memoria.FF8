using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7FieldHost
{
    private Func<int, Ff7NativeSoundDescription>? nativeSoundDescription;
    public Func<int, int, bool>? NativeSoundLoopAlreadyPlaying { get; set; }
    public Action<TimeSpan, bool>? NativeSoundClockAdvanced { get; set; }

    public void ConfigureNativeSoundResources(Func<int, Ff7NativeSoundDescription> describe, bool restoring = false)
    {
        nativeSoundDescription = describe ?? throw new ArgumentNullException(nameof(describe));
        if (!Process.SoundGainStateInitialized)
        {
            Process = Process with { SoundGainStateInitialized = true, SoundChannelGainPublications = (int[])Process.SoundChannelVolumes.Clone() };
            if (restoring)
            {
                Console.Error.WriteLine("FFVII compatibility: absent gain publication presentation uses recorded channel intensities; captured voice values and recorded progress are preserved.");
            }
        }
        if (!Process.SoundPanStateInitialized)
        {
            Process = Process with { SoundPanStateInitialized = true };
            if (restoring)
            {
                Console.Error.WriteLine("FFVII compatibility: absent channel pan presentation defaults to native center with inactive ramps; captured voice pans and recorded progress are preserved.");
            }
        }
        if (Process.SoundChannelBaseFrequencies.Any(rate => rate == 0))
        {
            int initialRate = describe(1).SampleRate;
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(initialRate);
            Process = Process with { SoundChannelBaseFrequencies = Process.SoundChannelBaseFrequencies.Select(rate => rate == 0 ? initialRate : rate).ToArray() };
            if (restoring)
            {
                Console.Error.WriteLine("FFVII compatibility: absent sound frequency presentation uses the native initial format rate; recorded progress and script PCs are preserved.");
            }
        }
        if (Process.SoundChannelFrequencyPublications.Any(frequency => frequency == 0))
        {
            int[] publications = (int[])Process.SoundChannelFrequencyPublications.Clone();
            for (int index = 0; index < publications.Length; index++)
            {
                if (publications[index] == 0)
                {
                    publications[index] = Ff7NativeSoundFrequency.Frequency(Process.SoundChannelBaseFrequencies[index], Process.SoundChannelPitches[index]);
                }
            }
            Process = Process with { SoundChannelFrequencyPublications = publications };
            if (restoring && Process.SoundChannelFrequencyVersions.Any(version => version != 0))
            {
                Console.Error.WriteLine("FFVII compatibility: absent frequency publication payload uses an approximate current base/control value; captured voice frequencies and recorded progress are preserved.");
            }
        }

    }

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
                int[] loopIds = (int[])Process.SoundChannelLoopIds.Clone();
                if (audio.Parameter1 == 0)
                {
                    loopIds[soundChannel - 1] = 0;
                    SetSoundVolume(soundChannel, 0, 127, publish: false);
                }
                else if (nativeSoundDescription is not null)
                {
                    Ff7NativeSoundDescription description = nativeSoundDescription(audio.Parameter1);
                    int[] frequencies = (int[])Process.SoundChannelBaseFrequencies.Clone();
                    bool? reusedLoop = description.Loop && !Process.SoundChannelStoppedByFade[soundChannel - 1] ? NativeSoundLoopAlreadyPlaying?.Invoke(soundChannel, audio.Parameter1) : false;
                    if (reusedLoop is null && loopIds[soundChannel - 1] == audio.Parameter1 && (frequencies[soundChannel - 1] != description.SampleRate || Process.SoundChannelPans[soundChannel - 1] != unchecked((byte)audio.Parameter0) || Process.SoundChannelPanFades[soundChannel - 1] is not null))
                    {
                        throw new NotSupportedException("FFVII reused looping sound requires its current native playback-status provider.");
                    }
                    if (description.Streamed)
                    {
                        frequencies[0] = description.SampleRate;
                        loopIds[1] = 0;
                        if (audio.Operation != 0x20)
                        {
                            loopIds[soundChannel - 1] = 0;
                        }
                    }
                    else
                    {
                        if (reusedLoop != true)
                        {
                            frequencies[soundChannel - 1] = description.SampleRate;
                            SetSoundPan(soundChannel, 0, unchecked((byte)audio.Parameter0));
                        }
                        loopIds[soundChannel - 1] = description.Loop ? audio.Parameter1 : 0;
                    }
                    Process = Process with { SoundChannelBaseFrequencies = frequencies };
                }
                Process = Process with { SoundChannelLoopIds = loopIds };
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
            case 0xA8:
            case 0xA9:
            case 0xAA:
            case 0xAB:
                SetSoundPan(audio.Operation - 0xA8 + 1, 0, unchecked((byte)audio.Parameter0));
                break;
            case 0xAC:
            case 0xAD:
            case 0xAE:
            case 0xAF:
                SetSoundPan(audio.Operation - 0xAC + 1, unchecked((byte)audio.Parameter0), unchecked((byte)audio.Parameter1));
                break;
            case 0xB0:
            case 0xB1:
            case 0xB2:
            case 0xB3:
                SetSoundFrequency(audio.Operation - 0xB0 + 1, 0, unchecked((sbyte)audio.Parameter0));
                break;
            case 0xB4:
            case 0xB5:
            case 0xB6:
            case 0xB7:
                SetSoundFrequency(audio.Operation - 0xB4 + 1, unchecked((byte)audio.Parameter0), unchecked((sbyte)audio.Parameter1));
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

    private void SetSoundVolume(int channel, byte duration, byte target, bool publish = true)
    {
        target = Math.Min(target, (byte)127);
        int index = channel - 1;
        if (duration != 0 && Process.SoundChannelVolumes[index] == target)
        {
            return;
        }
        int[] publications = (int[])Process.SoundChannelGainPublications.Clone();
        long[] versions = (long[])Process.SoundChannelGainVersions.Clone();
        int[] volumes = (int[])Process.SoundChannelVolumes.Clone();
        Ff7SoundVolumeFade?[] fades = (Ff7SoundVolumeFade?[])Process.SoundChannelVolumeFades.Clone();
        if (duration == 0)
        {
            volumes[index] = target;
            fades[index] = null;
            if (publish)
            {
                publications[index] = target;
                versions[index] = checked(versions[index] + 1);
            }
        }
        else
        {
            int rate = (int)((target - volumes[index]) / (duration / 60d));
            fades[index] = new(target, rate);
        }
        Process = Process with { SoundChannelGainPublications = publications, SoundChannelGainVersions = versions, SoundChannelVolumes = volumes, SoundChannelVolumeFades = fades };
    }

    private void SetSoundPan(int channel, byte duration, byte target)
    {
        int index = channel - 1;
        if (target > 127 || duration != 0 && Process.SoundChannelPans[index] == target)
        {
            return;
        }
        int[] pans = (int[])Process.SoundChannelPans.Clone();
        long[] versions = (long[])Process.SoundChannelPanVersions.Clone();
        Ff7SoundVolumeFade?[] fades = (Ff7SoundVolumeFade?[])Process.SoundChannelPanFades.Clone();
        if (duration == 0)
        {
            pans[index] = target;
            versions[index] = checked(versions[index] + 1);
            fades[index] = null;
        }
        else
        {
            fades[index] = new(target, (int)((target - pans[index]) / (duration / 60d)));
        }
        Process = Process with { SoundChannelPans = pans, SoundChannelPanVersions = versions, SoundChannelPanFades = fades };
    }

    private void SetSoundFrequency(int channel, byte duration, sbyte target)
    {
        int index = channel - 1;
        if (Process.SoundChannelBaseFrequencies[index] == 0)
        {
            throw new NotSupportedException("FFVII native sound frequency requires the selected sound-resource metadata.");
        }
        if (duration != 0 && Process.SoundChannelPitches[index] == target)
        {
            return;
        }
        int[] pitches = (int[])Process.SoundChannelPitches.Clone();
        int[] publications = (int[])Process.SoundChannelFrequencyPublications.Clone();
        long[] versions = (long[])Process.SoundChannelFrequencyVersions.Clone();
        Ff7SoundVolumeFade?[] fades = (Ff7SoundVolumeFade?[])Process.SoundChannelFrequencyFades.Clone();
        if (duration == 0)
        {
            pitches[index] = target;
            publications[index] = Ff7NativeSoundFrequency.Frequency(Process.SoundChannelBaseFrequencies[index], target);
            versions[index] = checked(versions[index] + 1);
            fades[index] = null;
        }
        else
        {
            fades[index] = new(target, (int)((target - pitches[index]) / (duration / 60d)));
        }
        Process = Process with { SoundChannelFrequencyPublications = publications, SoundChannelPitches = pitches, SoundChannelFrequencyVersions = versions, SoundChannelFrequencyFades = fades };
    }

    public void AdvanceAudioClock(TimeSpan elapsed, bool paused = false)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(elapsed, TimeSpan.Zero);
        NativeSoundClockAdvanced?.Invoke(elapsed, paused);
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
        int[] gainPublications = (int[])Process.SoundChannelGainPublications.Clone();
        long[] gainVersions = (long[])Process.SoundChannelGainVersions.Clone();
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
            gainPublications[index] = volume;
            gainVersions[index] = checked(gainVersions[index] + 1);
            fades[index] = volume == current.Target ? null : current with { AccumulatedMilliseconds = accumulated };
            if (volume == current.Target && volume == 0)
            {
                stopped[index] = true;
            }
        }
        int[] pans = (int[])Process.SoundChannelPans.Clone();
        long[] versions = (long[])Process.SoundChannelPanVersions.Clone();
        Ff7SoundVolumeFade?[] panFades = (Ff7SoundVolumeFade?[])Process.SoundChannelPanFades.Clone();
        for (int index = 0; index < panFades.Length; index++)
        {
            if (panFades[index] is not { } current)
            {
                continue;
            }
            if (!current.Started)
            {
                panFades[index] = current with { Started = true };
                continue;
            }
            (int pan, int accumulated) = Ff7NativeVolumeRamp.Advance(pans[index], current.Target, current.RatePerSecond, current.AccumulatedMilliseconds, milliseconds);
            pans[index] = pan;
            versions[index] = checked(versions[index] + 1);
            panFades[index] = pan == current.Target ? null : current with { AccumulatedMilliseconds = accumulated };
        }
        int[] pitches = (int[])Process.SoundChannelPitches.Clone();
        int[] frequencyPublications = (int[])Process.SoundChannelFrequencyPublications.Clone();
        long[] frequencyVersions = (long[])Process.SoundChannelFrequencyVersions.Clone();
        Ff7SoundVolumeFade?[] frequencyFades = (Ff7SoundVolumeFade?[])Process.SoundChannelFrequencyFades.Clone();
        for (int index = 0; index < frequencyFades.Length; index++)
        {
            if (frequencyFades[index] is not { } current)
            {
                continue;
            }
            if (!current.Started)
            {
                frequencyFades[index] = current with { Started = true };
                continue;
            }
            (int pitch, int accumulated) = Ff7NativeVolumeRamp.Advance(pitches[index], current.Target, current.RatePerSecond, current.AccumulatedMilliseconds, milliseconds);
            pitches[index] = pitch;
            frequencyPublications[index] = Ff7NativeSoundFrequency.Frequency(Process.SoundChannelBaseFrequencies[index], pitch);
            frequencyVersions[index] = checked(frequencyVersions[index] + 1);
            frequencyFades[index] = pitch == current.Target ? null : current with { AccumulatedMilliseconds = accumulated };
        }
        Process = Process with { SoundChannelGainPublications = gainPublications, SoundChannelGainVersions = gainVersions, SoundChannelFrequencyPublications = frequencyPublications, SoundChannelPitches = pitches, SoundChannelFrequencyVersions = frequencyVersions, SoundChannelFrequencyFades = frequencyFades, SoundChannelPans = pans, SoundChannelPanVersions = versions, SoundChannelPanFades = panFades, SoundChannelVolumes = volumes, SoundChannelVolumeFades = fades, SoundChannelStoppedByFade = stopped, SoundVolumeClockSubmillisecondTicks = (int)(soundTicks % TimeSpan.TicksPerMillisecond) };
    }
}
