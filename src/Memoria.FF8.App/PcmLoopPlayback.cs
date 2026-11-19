using System.Diagnostics;

namespace Memoria.FF8;

internal interface IExternalMusicOutput : IDisposable
{
    int Volume { get; }
    void Update();
    void SetGain(int gain);
    void Pause(bool paused);
}

/// <summary>Shared PCM intro-to-loop playback; game adapters retain track identity and saved logical time.</summary>
internal sealed class PcmLoopPlayback(PcmAudioOutput.Buffer primary, PcmAudioOutput.Buffer? loop, int volume, double initialSeconds, int bytesPerSecond, int loopBytes, bool restoredLoop = false, int blockAlignment = 4, Func<double>? elapsedSeconds = null) : IExternalMusicOutput
{
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly double externalClockOrigin = elapsedSeconds?.Invoke() ?? 0;
    private double ElapsedSeconds => elapsedSeconds is null ? clock.Elapsed.TotalSeconds : elapsedSeconds() - externalClockOrigin;
    private bool paused;
    private double tempo = 1;
    private double tempoOriginSeconds;
    private double audioOriginSeconds;
    public bool LoopStarted { get; private set; } = restoredLoop;
    public int Volume { get; } = volume;

    public void Update()
    {
        UpdateAt(ElapsedSeconds);
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
            SetTempoAt(speed, ElapsedSeconds);
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
        (double frames, bool repeating) = CaptureFramePosition(queuedFrames, loopStartBytes / blockAlignment);
        return (checked((int)frames * blockAlignment), repeating);
    }

    internal (double PositionFrames, bool Repeating) CaptureFramePosition(int queuedFrames, int loopStartFrames)
    {
        bool repeating = LoopStarted && loop?.HasStartedBeforeQueuedFrames(queuedFrames) == true;
        double position = repeating ? loopStartFrames + loop!.PositionFramesBeforeQueuedFrames(queuedFrames) : primary.PositionFramesBeforeQueuedFrames(queuedFrames);
        return (position, repeating);
    }

    internal double CaptureHandoffRemainingSeconds(int queuedFrames, int outputRate)
    {
        return CaptureHandoffRemainingAt(ElapsedSeconds, queuedFrames, outputRate);
    }

    internal double CaptureHandoffRemainingAt(double elapsedSeconds, int queuedFrames, int outputRate)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(queuedFrames);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(outputRate);
        double audioSeconds = audioOriginSeconds + (elapsedSeconds - tempoOriginSeconds - queuedFrames / (double)outputRate) * tempo;
        return Math.Max(0, initialSeconds - audioSeconds);
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
