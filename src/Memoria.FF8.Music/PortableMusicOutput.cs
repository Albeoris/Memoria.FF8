using Memoria.FF8.Audio;

namespace Memoria.FF8;

internal sealed class PortableMusicOutput : IDisposable
{
    private readonly SdlAudioOutput output;
    private readonly SegmentSynthesizer synthesizer;
    private readonly SegmentComposition concert;

    public PortableMusicOutput(string directory)
    {
        var bank = new DlsBank(File.ReadAllBytes(Path.Combine(directory, "FF8.dls")));
        output = new SdlAudioOutput();
        synthesizer = new SegmentSynthesizer(bank, output.Mixer.SampleRate);
        concert = new SegmentComposition(bank, output.Mixer.SampleRate);
        output.Mixer.Add(concert);
        output.Mixer.Add(synthesizer);
    }

    public bool IsPlaying
    {
        get
        {
            output.ThrowIfFailed();
            return synthesizer.IsPlaying || concert.IsPlaying;
        }
    }

    public void Play(string path, int volume, int startPoint = 0)
    {
        output.ThrowIfFailed();
        synthesizer.Play(new MusicSegment(File.ReadAllBytes(path)), volume, startPoint);
        concert.Stop();
    }

    public void PlayConcert(string[] paths, int volume)
    {
        output.ThrowIfFailed();
        concert.Play(paths.Select(path => new MusicSegment(File.ReadAllBytes(path))).ToArray(), volume);
        synthesizer.Stop();
    }

    public void Volume(int hundredthDecibels)
    {
        output.ThrowIfFailed();
        synthesizer.Volume(hundredthDecibels);
        concert.Volume(hundredthDecibels);
    }

    public void Pause()
    {
        synthesizer.Pause();
        concert.Pause();
    }

    public void Resume()
    {
        output.ThrowIfFailed();
        synthesizer.Resume();
        concert.Resume();
    }

    public void Stop()
    {
        synthesizer.Stop();
        concert.Stop();
    }

    public void Dispose()
    {
        synthesizer.Stop();
        concert.Stop();
        output.Dispose();
    }
}
