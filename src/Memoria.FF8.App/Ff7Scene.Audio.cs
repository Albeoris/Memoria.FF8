using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private PcmAudioOutput? audioOutput;
    private PcmLoopPlayback? musicPlayback;
    private readonly List<PcmAudioOutput.Buffer> soundBuffers = [];
    private readonly Dictionary<int, PcmAudio> soundCache = [];
    private Ff7PcExecutableData? audioNative;
    private CancellationTokenSource? musicCancellation;
    private Task<(PcmAudio Pcm, ExternalMusicLoop Loop)>? musicDecode;
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
        }
        if (!audioEnabled)
        {
            return;
        }
        Ff7FieldProcess state = session.Host.Process;
        int track = state.NativeMusicTrack ?? (state.Music is null || state.Music.Track < 0 ? 0 : session.Host.ResolveNativeMusicTrack(state.Music.Track));
        track = Ff7FieldHost.NormalizeNativeMusicTrack(track);
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
                    return (pcm, ExternalMusicLoop.Read(encoded, pcm));
                }, token);
                Console.WriteLine($"ff7-music={audioNative.MusicNames[track]} nativeTrack={track}");
            }
        }
        if (musicDecode is { IsCompleted: true })
        {
            (PcmAudio pcm, ExternalMusicLoop loop) = musicDecode.GetAwaiter().GetResult();
            musicDecode = null;
            audioOutput ??= new PcmAudioOutput();
            byte[] primary = pcm.Bytes.AsSpan(0, loop.EndByte).ToArray();
            byte[] repeat = pcm.Bytes.AsSpan(loop.StartByte, loop.EndByte - loop.StartByte).ToArray();
            PcmAudioOutput.Buffer musicBuffer = audioOutput.Create(primary, pcm.SampleRate, pcm.Channels);
            PcmAudioOutput.Buffer musicLoopBuffer = audioOutput.Create(repeat, pcm.SampleRate, pcm.Channels);
            long elapsed = checked((long)(state.MusicTicks / 30d * pcm.SampleRate)) * pcm.Channels * sizeof(short);
            bool musicLoopStarted = elapsed >= loop.EndByte;
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
            SetAudioPaused(audioPaused);
        }
        musicPlayback?.Update();
        int gain = LinearGain(state.MusicVolume * (dialoguePreferences?.Invoke().MusicVolume ?? 100) / 100);
        musicPlayback?.SetGain(gain);
        foreach (PcmAudioOutput.Buffer finished in soundBuffers.Where(buffer => !buffer.IsPlaying && !audioPaused).ToArray())
        {
            finished.Dispose();
            soundBuffers.Remove(finished);
        }
        audioOutput?.ThrowIfFailed();
    }

    private void PlayAudio(FieldAudioCommand command)
    {
        if (!audioEnabled)
        {
            return;
        }
        switch (command.Operation)
        {
            case 0xF0:
            case 0xC0:
                return;
            // The admitted PC sound dispatcher treats the CD-volume operations as no-ops.
            case 0xC8:
            case 0xC9:
            case 0xCA:
                return;
            case 0x20:
                if (command.Parameter1 == 0)
                {
                    throw new NotSupportedException("FFVII sound ID zero requires its native channel control contract.");
                }
                if (!soundCache.TryGetValue(command.Parameter1 - 1, out PcmAudio? pcm))
                {
                    Ff7SoundSample sample = Ff7SoundResources.Read(installation, command.Parameter1 - 1);
                    pcm = PcmAudioDecoder.ReadWave(sample.WaveBytes);
                    if (sample.Loop)
                    {
                        throw new NotSupportedException("FFVII looping sound effects require their original channel continuation.");
                    }
                    soundCache.Add(command.Parameter1 - 1, pcm);
                }
                audioOutput ??= new PcmAudioOutput();
                PcmAudioOutput.Buffer sound = audioOutput.Create(pcm.Bytes, pcm.SampleRate, pcm.Channels);
                if (command.Parameter0 is < 0 or > 127)
                {
                    sound.Dispose();
                    throw new NotSupportedException("FFVII sound panning exceeds the admitted native table.");
                }
                int pan = command.Parameter0;
                audioNative ??= Ff7PcExecutableData.Load(installation);
                sound.Pan(audioNative.NativeSoundPan[pan]);
                sound.Volume(LinearGain(127 * (dialoguePreferences?.Invoke().SoundVolume ?? 100) / 100));
                sound.Play(false);
                soundBuffers.Add(sound);
                return;
            default:
                throw new NotSupportedException($"FFVII audio operation {command.Operation:X2} requires its native output contract.");
        }
    }

    private static int LinearGain(int volume)
    {
        return volume == 0 ? -10000 : checked((int)Math.Round(2000 * Math.Log10(Math.Clamp(volume, 1, 127) / 127d)));
    }

    private void SetAudioPaused(bool paused)
    {
        audioPaused = paused;
        musicPlayback?.Pause(paused);
        foreach (PcmAudioOutput.Buffer buffer in soundBuffers)
        {
            if (paused)
            {
                buffer.Pause();
            }
            else
            {
                buffer.Resume();
            }
        }
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
    }

    private void StopAudio()
    {
        if (audioHost is not null)
        {
            audioHost.AudioRequested = null;
            audioHost = null;
        }
        StopMusicAudio();
        foreach (PcmAudioOutput.Buffer buffer in soundBuffers)
        {
            buffer.Dispose();
        }
        soundBuffers.Clear();
        audioOutput?.Dispose();
        audioOutput = null;
        audioTrack = -1;
        audioPaused = false;
    }
}
