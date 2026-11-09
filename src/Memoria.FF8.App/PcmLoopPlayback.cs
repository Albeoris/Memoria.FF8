using System.Diagnostics;

namespace Memoria.FF8;

/// <summary>Shared PCM intro-to-loop playback; game adapters retain track identity and saved logical time.</summary>
internal class PcmLoopPlayback(PcmAudioOutput.Buffer primary, PcmAudioOutput.Buffer? loop, int volume, double initialSeconds, int bytesPerSecond, int loopBytes, bool restoredLoop = false, int blockAlignment = 4) : IDisposable
{
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private bool paused;
    private double tempo = 1;
    private double tempoOriginSeconds;
    private double audioOriginSeconds;
    public bool LoopStarted { get; private set; } = restoredLoop;
    public int Volume { get; } = volume;

    public void Update()
    {
        UpdateAt(clock.Elapsed.TotalSeconds);
    }

    internal void UpdateAt(double elapsedSeconds)
    {
        double audioSeconds = audioOriginSeconds + (elapsedSeconds - tempoOriginSeconds) * tempo;
        if (!paused && loop is not null && !LoopStarted && audioSeconds >= initialSeconds)
        {
            int elapsedBytes = (int)((audioSeconds - initialSeconds) * bytesPerSecond % loopBytes);
            loop.Position(elapsedBytes / blockAlignment * blockAlignment);
            loop.Play(true);
            primary.Stop();
            LoopStarted = true;
        }
    }

    public void SetTempo(double speed)
    {
        if (speed != tempo)
        {
            SetTempoAt(speed, clock.Elapsed.TotalSeconds);
        }
    }

    internal void SetTempoAt(double speed, double elapsedSeconds)
    {
        if (!double.IsFinite(speed) || speed is < 0.5 or > 30)
        {
            throw new ArgumentOutOfRangeException(nameof(speed));
        }
        UpdateAt(elapsedSeconds);
        audioOriginSeconds += (elapsedSeconds - tempoOriginSeconds) * tempo;
        tempoOriginSeconds = elapsedSeconds;
        tempo = speed;
        primary.Tempo(speed);
        loop?.Tempo(speed);
    }

    public (int PositionBytes, bool Repeating) CapturePosition(int queuedFrames, int loopStartBytes)
    {
        bool repeating = LoopStarted && loop?.HasStartedBeforeQueuedFrames(queuedFrames) == true;
        int position = repeating ? checked(loopStartBytes + loop!.PositionBytesBeforeQueuedFrames(queuedFrames)) : primary.PositionBytesBeforeQueuedFrames(queuedFrames);
        return (position, repeating);
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
