using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private PcmAudioOutput? audioOutput;
    private PcmAudioOutput.Buffer? musicBuffer;
    private PcmAudioOutput.Buffer? musicLoopBuffer;
    private readonly List<PcmAudioOutput.Buffer> soundBuffers = [];
    private readonly Dictionary<int, PcmAudio> soundCache = [];
    private Ff7PcExecutableData? audioNative;
    private CancellationTokenSource? musicCancellation;
    private Task<(PcmAudio Pcm, ExternalMusicLoop Loop)>? musicDecode;
    private Ff7FieldHost? audioHost;
    private int audioTrack = -1;
    private bool musicLoopStarted;
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
        int track = state.NativeMusicTrack ?? (state.Music is null ? 0 : session.Host.ResolveNativeMusicTrack(state.Music.Track));
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
            musicBuffer = audioOutput.Create(primary, pcm.SampleRate, pcm.Channels);
            musicLoopBuffer = audioOutput.Create(repeat, pcm.SampleRate, pcm.Channels);
            long elapsed = checked((long)(state.MusicTicks / 30d * pcm.SampleRate)) * pcm.Channels * sizeof(short);
            musicLoopStarted = elapsed >= loop.EndByte;
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
            SetAudioPaused(audioPaused);
        }
        if (!audioPaused && musicBuffer is not null && musicLoopBuffer is not null && !musicLoopStarted && !musicBuffer.IsPlaying)
        {
            musicLoopBuffer.Play(true);
            musicLoopStarted = true;
        }
        int gain = LinearGain(state.MusicVolume);
        musicBuffer?.Volume(gain);
        musicLoopBuffer?.Volume(gain);
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
                    foreach (PcmAudioOutput.Buffer buffer in soundBuffers)
                    {
                        buffer.Dispose();
                    }
                    soundBuffers.Clear();
                    return;
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
                int pan = Math.Clamp(command.Parameter0, 0, 127);
                sound.Pan(pan == 64 ? 0 : Math.Clamp((pan - 64) * 10000 / 64, -10000, 10000));
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
        foreach (PcmAudioOutput.Buffer buffer in soundBuffers.Concat(new[] { musicBuffer, musicLoopBuffer }.OfType<PcmAudioOutput.Buffer>()))
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
        musicBuffer?.Dispose();
        musicLoopBuffer?.Dispose();
        musicBuffer = null;
        musicLoopBuffer = null;
        musicLoopStarted = false;
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
