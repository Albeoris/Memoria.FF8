using Memoria.FF8.Audio;

namespace Memoria.FF8;

internal sealed class PortableMusicOutput : IDisposable
{
    private readonly SegmentMusicBank bank;
    private readonly SdlAudioOutput output;
    private SegmentMusicSource? source;

    public PortableMusicOutput(string directory)
    {
        bank = new SegmentMusicBank(File.ReadAllBytes(Path.Combine(directory, "FF8.dls")));
        output = new SdlAudioOutput();
    }

    public bool IsPlaying
    {
        get
        {
            ThrowIfFailed();
            return source?.IsPlaying == true;
        }
    }

    public void Play(string path, int volume, int startPoint = 0)
    {
        Start([path], volume, concert: false, startPoint);
    }

    public void PlayConcert(string[] paths, int volume, int previewMeasure = 0)
    {
        Start(paths, volume, concert: true, previewMeasure: previewMeasure);
    }

    public void PlayAtPosition(string[] paths, MusicPlaybackPosition saved, int volume, bool isConcert, int fallbackStartPoint = 0, int previewMeasure = 0)
    {
        Start(paths, volume, isConcert, fallbackStartPoint, previewMeasure, saved);
    }

    private void Start(string[] paths, int volume, bool concert, int startPoint = 0, int previewMeasure = 0, MusicPlaybackPosition? saved = null)
    {
        output.ThrowIfFailed();
        SegmentMusicSource prepared = bank.PrepareAsync(paths, output.Mixer.SampleRate, volume, concert, startPoint, previewMeasure, saved).GetAwaiter().GetResult();
        Stop();
        output.Mixer.Add(prepared);
        source = prepared;
    }

    public MusicPlaybackPosition? CapturePosition()
    {
        ThrowIfFailed();
        return output.CapturePlaybackPosition(queued => source?.CapturePosition(queued));
    }

    public void Volume(int hundredthDecibels)
    {
        ThrowIfFailed();
        source?.Volume(hundredthDecibels);
    }

    public void Pause()
    {
        source?.Pause();
    }

    public void Resume()
    {
        ThrowIfFailed();
        source?.Resume();
    }

    private void ThrowIfFailed()
    {
        output.ThrowIfFailed();
        if (source?.Error is Exception failure)
        {
            throw new InvalidOperationException(failure.Message, failure);
        }
    }

    public void Stop()
    {
        if (source is { } active)
        {
            output.Mixer.Remove(active);
            source = null;
        }
    }

    public void Dispose()
    {
        Stop();
        output.Dispose();
    }
}
