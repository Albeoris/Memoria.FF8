using Memoria.FF8.Audio;

namespace Memoria.FF8;

internal sealed class PortableMusicOutput : IDisposable
{
    private readonly SdlAudioOutput output;
    private readonly SegmentSynthesizer synthesizer;

    public PortableMusicOutput(string directory)
    {
        var bank = new DlsBank(File.ReadAllBytes(Path.Combine(directory, "FF8.dls")));
        output = new SdlAudioOutput();
        synthesizer = new SegmentSynthesizer(bank, output.Mixer.SampleRate);
        output.Mixer.Add(synthesizer);
    }

    public bool IsPlaying
    {
        get
        {
            output.ThrowIfFailed();
            return synthesizer.IsPlaying;
        }
    }

    public void Play(string path, int volume, int startPoint = 0)
    {
        output.ThrowIfFailed();
        synthesizer.Play(new MusicSegment(File.ReadAllBytes(path)), volume, startPoint);
    }

    public void Volume(int hundredthDecibels)
    {
        output.ThrowIfFailed();
        synthesizer.Volume(hundredthDecibels);
    }

    public void Pause()
    {
        synthesizer.Pause();
    }

    public void Resume()
    {
        output.ThrowIfFailed();
        synthesizer.Resume();
    }

    public void Stop()
    {
        synthesizer.Stop();
    }

    public void Dispose()
    {
        synthesizer.Stop();
        output.Dispose();
    }
}
