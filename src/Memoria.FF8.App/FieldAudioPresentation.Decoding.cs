using Memoria.FF8.Audio;

namespace Memoria.FF8;

internal sealed partial class FieldAudioPresentation
{
    private AudioDecodeQueue? decodeQueue;
    private readonly HashSet<int> pendingDialogueDecodes = [];
    private bool audioDisposed;
    internal Func<string, byte[]?, CancellationToken, Task<PcmAudio>>? DecodeOverride { get; init; }
    internal Func<PcmAudioOutput>? OutputFactory { get; init; }
    private AudioDecodeQueue Decoder => decodeQueue ??= !audioDisposed ? new AudioDecodeQueue(ReportOutputFailure) : throw new ObjectDisposedException(nameof(FieldAudioPresentation));

    private async Task<PcmAudio> DecodeAsync(string path, byte[]? input, CancellationToken token)
    {
        if (DecodeOverride is { } decode)
        {
            return await decode(path, input, token).ConfigureAwait(false);
        }
        byte[] pcm = await PcmAudioDecoder.DecodeAsync(toolsDirectory, path, input, 44100, 2, token).ConfigureAwait(false);
        return new PcmAudio(pcm, 44100, 2);
    }

    private void CancelMusicDecode(MusicKind kind)
    {
        if (kind != MusicKind.None)
        {
            decodeQueue?.Cancel(kind);
        }
    }

    private void QueueStreamMusic(string path, int slot, int track, int volume)
    {
        musicKinds[slot] = MusicKind.Stream;
        musicIds[slot] = track;
        streamVolume = volume;
        streamRamp = null;
        streamedMusicPaused = false;
        nativeMidiPaused = false;
        try
        {
            Decoder.Queue(MusicKind.Stream, token => DecodeAsync(path, null, token), pcm =>
            {
                streamedMusic?.Dispose();
                PcmAudioOutput.Buffer buffer = Sound.Create(pcm.Bytes, pcm.SampleRate, pcm.Channels);
                try
                {
                    buffer.Volume(OutputGain(Catalog.SoundVolume(Math.Clamp((int)streamVolume, 0, 127)), musicPreference));
                    buffer.Play(true);
                    if (nativeMidiPaused)
                    {
                        buffer.Stop();
                        streamedMusicPaused = true;
                    }
                    else if (focusPaused)
                    {
                        buffer.Pause();
                    }
                }
                catch
                {
                    buffer.Dispose();
                    throw;
                }
                streamedMusic = buffer;
                Console.WriteLine($"field-music=Playing track={track} slot={slot} file={Path.GetFileName(path)}");
            }, exception => MusicDecodeFailed(slot, exception));
        }
        catch
        {
            musicKinds[slot] = MusicKind.None;
            musicIds[slot] = -1;
            throw;
        }
    }

    private void MusicDecodeFailed(int slot, Exception exception)
    {
        musicKinds[slot] = MusicKind.None;
        musicIds[slot] = -1;
        ReportOutputFailure(exception);
    }

    private readonly record struct DialogueDecodeOwner(int Channel);
}
