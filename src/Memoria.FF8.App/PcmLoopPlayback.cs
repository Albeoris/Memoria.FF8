using System.Diagnostics;

namespace Memoria.FF8;

/// <summary>Shared PCM intro-to-loop playback; game adapters retain track identity and saved logical time.</summary>
internal class PcmLoopPlayback(PcmAudioOutput.Buffer primary, PcmAudioOutput.Buffer? loop, int volume, double initialSeconds, int bytesPerSecond, int loopBytes, bool restoredLoop = false, int blockAlignment = 4) : IDisposable
{
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private bool paused;
    public bool LoopStarted { get; private set; } = restoredLoop;
    public int Volume { get; } = volume;

    public void Update()
    {
        UpdateAt(clock.Elapsed.TotalSeconds);
    }

    internal void UpdateAt(double elapsedSeconds)
    {
        if (!paused && loop is not null && !LoopStarted && elapsedSeconds >= initialSeconds)
        {
            int elapsedBytes = (int)((elapsedSeconds - initialSeconds) * bytesPerSecond % loopBytes);
            loop.Position(elapsedBytes / blockAlignment * blockAlignment);
            loop.Play(true);
            primary.Stop();
            LoopStarted = true;
        }
    }

    public void SetGain(int gain)
    {
        primary.Volume(gain);
        loop?.Volume(gain);
    }

    public void Pause(bool value)
    {
        paused = value;
        if (value)
        {
            clock.Stop();
            primary.Pause();
            loop?.Pause();
        }
        else
        {
            clock.Start();
            primary.Resume();
            loop?.Resume();
        }
    }

    public void Dispose()
    {
        primary.Dispose();
        loop?.Dispose();
    }
}
