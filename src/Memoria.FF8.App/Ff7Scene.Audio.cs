using System.Security.Cryptography;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private PcmAudioOutput? audioOutput;
    private PcmLoopPlayback? musicPlayback;
    private readonly Ff7SoundChannels soundChannels = new();
    private readonly Dictionary<int, (PcmAudio Pcm, bool Streamed, bool Loop, int LoopStart, int LoopEnd, string Fingerprint, int NativeRate, double? LoopDelay)> soundCache = [];
    private Ff7PcExecutableData? audioNative;
    private CancellationTokenSource? musicCancellation;
    private Task<(PcmAudio Pcm, ExternalMusicLoop Loop, string Fingerprint)>? musicDecode;
    private Ff7FieldHost? audioHost;
    private int audioTrack = -1;
    private bool audioPaused;

    private void UpdateAudio()
    {
        Ff7Session session = Session!;
        if (!ReferenceEquals(audioHost, session.Host))
        {
            if (audioHost is not null)
            {
                audioHost.AudioRequested = null;
            }
            audioHost = session.Host;
            audioHost.AudioRequested = PlayAudio;
            audioHost.NativeSoundLoopAlreadyPlaying = soundChannels.IsLoopPlaying;
        }
        if (!audioEnabled)
        {
            return;
        }
        ApplyNativeResidentStops();
        RestorePendingSounds();
        Ff7FieldProcess state = session.Host.Process;
        RefreshNativeGains();
        RefreshNativePans();
        RefreshNativeFrequencies();
        soundChannels.SetTempo(sharedClock.EffectiveSpeed(session.World is not null ? SceneKind.WorldMap : state.Movie is { Playing: true } ? SceneKind.Movie : SceneKind.Field, false, false));
        int track = state.NativeMusicTrack ?? (state.Music is null || state.Music.Track < 0 ? 0 : session.Host.ResolveNativeMusicTrack(state.Music.Track));
        track = Ff7FieldHost.NormalizeNativeMusicTrack(track);
        if (track == 0)
        {
            pendingMusic = null;
        }
        if (track != audioTrack)
        {
            StopMusicAudio();
            audioTrack = track;
            if (track != 0)
            {
                audioNative ??= Ff7PcExecutableData.Load(installation);
                if ((uint)track >= audioNative.MusicNames.Count)
                {
                    throw new InvalidDataException("FFVII music state exceeds the admitted native table.");
                }
                string path = Path.Combine(installation.GetRequired(Ff7ResourceRole.Music).Path, audioNative.MusicNames[track].ToLowerInvariant() + ".ogg");
                if (!File.Exists(path))
                {
                    throw new NotSupportedException("FFVII music playback requires the selected installation's original OGG track.");
                }
                musicCancellation = new CancellationTokenSource();
                CancellationToken token = musicCancellation.Token;
                musicDecode = Task.Run(async () =>
                {
                    PcmAudio pcm = await PcmAudioDecoder.ReadFileAsync(mediaToolsDirectory, path, token).ConfigureAwait(false);
                    byte[] encoded = await File.ReadAllBytesAsync(path, token).ConfigureAwait(false);
                    return (pcm, ExternalMusicLoop.Read(encoded, pcm), Convert.ToHexString(SHA256.HashData(encoded)));
                }, token);
                Console.WriteLine($"ff7-music={audioNative.MusicNames[track]} nativeTrack={track}");
            }
        }
        if (musicDecode is { IsCompleted: true })
        {
            (PcmAudio pcm, ExternalMusicLoop loop, string fingerprint) = musicDecode.GetAwaiter().GetResult();
            musicDecode = null;
            audioOutput ??= new PcmAudioOutput();
            byte[] primary = pcm.Bytes.AsSpan(0, loop.EndByte).ToArray();
            byte[] repeat = pcm.Bytes.AsSpan(loop.StartByte, loop.EndByte - loop.StartByte).ToArray();
            PcmAudioOutput.Buffer musicBuffer = audioOutput.Create(primary, pcm.SampleRate, pcm.Channels);
            PcmAudioOutput.Buffer musicLoopBuffer = audioOutput.Create(repeat, pcm.SampleRate, pcm.Channels);
            Ff7MusicVoicePosition? savedPosition = ResumeMusicPosition(track, fingerprint, pcm, loop);
            long elapsed = savedPosition is not null ? savedPosition.PositionFrames * pcm.Channels * sizeof(short) : legacyMusicPosition ? checked((long)(state.MusicTicks / 30d * pcm.SampleRate)) * pcm.Channels * sizeof(short) : 0;
            legacyMusicPosition = false;
            bool musicLoopStarted = savedPosition?.LoopStarted ?? elapsed >= loop.EndByte;
            if (musicLoopStarted)
            {
                musicLoopBuffer.Position(checked((int)((elapsed - loop.StartByte) % repeat.Length)) / 4 * 4);
                musicLoopBuffer.Play(true);
            }
            else
            {
                musicBuffer.Position(checked((int)elapsed) / 4 * 4);
                musicBuffer.Play(false);
            }
            int bytesPerSecond = checked(pcm.SampleRate * pcm.Channels * sizeof(short));
            musicPlayback = new PcmLoopPlayback(musicBuffer, musicLoopBuffer, 100, (loop.EndByte - elapsed) / (double)bytesPerSecond, bytesPerSecond, repeat.Length, musicLoopStarted, pcm.Channels * sizeof(short));
            musicSource = new(track, fingerprint, pcm.SampleRate, pcm.Channels, 0, loop.StartByte / (pcm.Channels * sizeof(short)), loop.EndByte / (pcm.Channels * sizeof(short)), false);
            SetAudioPaused(audioPaused);
        }
        musicPlayback?.Update();
        int gain = NativeGain(state.MusicVolume, dialoguePreferences?.Invoke().MusicVolume ?? 100);
        musicPlayback?.SetGain(gain);
        soundChannels.RetireFinished(audioPaused);
        audioOutput?.ThrowIfFailed();
    }

    private void RefreshNativeGains()
    {
        if (Session is null)
        {
            return;
        }
        Ff7FieldProcess state = Session.Host.Process;
        int soundVolume = dialoguePreferences?.Invoke().SoundVolume ?? 100;
        soundChannels.RefreshNativeGains(state.SoundChannelGainPublications, state.SoundChannelGainVersions, volume => NativeGain(volume, soundVolume));
        soundChannels.RefreshGains(volume => NativeGain(volume, soundVolume));
    }

    private void RefreshNativeFrequencies()
    {
        if (Session is null)
        {
            return;
        }
        Ff7FieldProcess state = Session.Host.Process;
        soundChannels.RefreshFrequencies(state.SoundChannelFrequencyPublications, state.SoundChannelFrequencyVersions);
    }

    private void RefreshNativePans()
    {
        if (Session is null)
        {
            return;
        }
        audioNative ??= Ff7PcExecutableData.Load(installation);
        Ff7FieldProcess state = Session.Host.Process;
        soundChannels.RefreshPans(state.SoundChannelPans, state.SoundChannelPanVersions, audioNative.NativeSoundPan);
    }

    private void ApplyNativeResidentStops()
    {
        if (Session is null)
        {
            return;
        }
        bool[] stopped = Session.Host.Process.SoundChannelStoppedByFade;
        for (int index = 0; index < stopped.Length; index++)
        {
            if (stopped[index])
            {
                soundChannels.StopResidentChannel(index + 1);
            }
        }
        if (pendingSounds is not null)
        {
            pendingSounds = Ff7SoundChannels.FilterStoppedResidentPositions(pendingSounds, stopped);
        }
    }

    private void PlayAudio(FieldAudioCommand command)
    {
        if (!audioEnabled)
        {
            return;
        }
        ApplyNativeResidentStops();
        RefreshNativeGains();
        RefreshNativePans();
        RefreshNativeFrequencies();
        switch (command.Operation)
        {
            case 0x10:
            case 0x14:
            case 0x18:
                return;
            case 0xF1:
                soundChannels.StopAll();
                pendingSounds = null;
                return;
            case 0xA8:
            case 0xA9:
            case 0xAA:
            case 0xAB:
            case 0xAC:
            case 0xAD:
            case 0xAE:
            case 0xAF:
                RefreshNativePans();
                return;
            case 0xB0:
            case 0xB1:
            case 0xB2:
            case 0xB3:
            case 0xB4:
            case 0xB5:
            case 0xB6:
            case 0xB7:
                RefreshNativeFrequencies();
                return;
            case 0xA0:
            case 0xA1:
            case 0xA2:
            case 0xA3:
                return;
            case 0xA4:
            case 0xA5:
            case 0xA6:
            case 0xA7:
                return;
            case 0xF0:
            case 0xC0:
            case 0xC1:
            case 0xC2:
                return;
            // The admitted PC sound dispatcher treats the CD-volume operations as no-ops.
            case 0xC8:
            case 0xC9:
            case 0xCA:
                return;
            case 0x2B when command.Parameter1 == 0:
                soundChannels.StopNativeChannel(4);
                return;
            case 0x29 when command.Parameter1 == 0:
                soundChannels.StopNativeChannel(2);
                return;
            case 0x2A when command.Parameter1 == 0:
                soundChannels.StopNativeChannel(3);
                return;
            case 0x2B:
            case 0x29:
            case 0x2A:
            case 0x28:
            case 0x20:
                if (command.Parameter1 == 0)
                {
                    // Native sound zero stops only its selected resident channel and the global SFX stream.
                    soundChannels.StopNativeChannel(1);
                    return;
                }
                var decoded = DecodeSound(command.Parameter1);
                audioOutput ??= new PcmAudioOutput();
                int pan = Session!.Host.ResolveNativeSoundPlaybackPan(command, decoded.Streamed);
                audioNative ??= Ff7PcExecutableData.Load(installation);
                int channel = command.Operation switch { 0x29 => 2, 0x2A => 3, 0x2B => 4, _ => 1 };
                int soundIntensity = Session!.Host.Process.SoundChannelVolumes[decoded.Streamed ? 0 : channel - 1];
                soundChannels.Play(audioOutput, decoded.Pcm.Bytes, decoded.Pcm.SampleRate, decoded.Pcm.Channels, decoded.Streamed, audioNative.NativeSoundPan[pan], NativeGain(soundIntensity, dialoguePreferences?.Invoke().SoundVolume ?? 100), command.Parameter1, decoded.Loop, decoded.LoopStart, decoded.LoopEnd, decoded.Fingerprint, channel: channel, stopStream: command.Operation == 0x20, nativeVolume: soundIntensity, nativeSampleRate: decoded.NativeRate, nativePitch: Session.Host.Process.SoundChannelPitches[decoded.Streamed ? 0 : channel - 1], nativeLoopDelaySeconds: decoded.LoopDelay, nativePanVersion: Session.Host.Process.SoundChannelPanVersions[decoded.Streamed ? 0 : channel - 1], nativeFrequencyVersion: Session.Host.Process.SoundChannelFrequencyVersions[decoded.Streamed ? 0 : channel - 1], nativeGainVersion: Session.Host.Process.SoundChannelGainVersions[decoded.Streamed ? 0 : channel - 1]);
                soundChannels.Pause(audioPaused);
                return;
            default:
                throw new NotSupportedException($"FFVII audio operation {command.Operation:X2} requires its native output contract.");
        }
    }

    private int NativeGain(int volume, int globalPercent)
    {
        audioNative ??= Ff7PcExecutableData.Load(installation);
        return FieldAudioPresentation.OutputGain(audioNative.NativeSoundGain(Math.Clamp(volume, 0, 127)), globalPercent);
    }

    private void SetAudioPaused(bool paused)
    {
        audioPaused = paused;
        musicPlayback?.Pause(paused);
        soundChannels.Pause(paused);
    }

    private void StopMusicAudio()
    {
        musicCancellation?.Cancel();
        musicCancellation?.Dispose();
        musicCancellation = null;
        if (musicDecode is not null)
        {
            _ = musicDecode.ContinueWith(task => _ = task.Exception, TaskContinuationOptions.OnlyOnFaulted);
            musicDecode = null;
        }
        musicPlayback?.Dispose();
        musicPlayback = null;
        musicSource = null;
    }

    private void StopAudio()
    {
        if (audioHost is not null)
        {
            audioHost.AudioRequested = null;
            audioHost = null;
        }
        StopMusicAudio();
        soundChannels.Dispose();
        audioOutput?.Dispose();
        audioOutput = null;
        audioTrack = -1;
        audioPaused = false;
    }
}
