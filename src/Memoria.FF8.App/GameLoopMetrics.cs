using System.Diagnostics;

namespace Memoria.FF8;

internal sealed record GameLoopSample(double Seconds, double DrawsPerSecond, double UpdatesPerSecond, double SimulationFramesPerSecond, int Draws, int Updates, int Renders, double UpdateMilliseconds, double RenderMilliseconds, double UploadMilliseconds, double MaximumRenderMilliseconds);

/// <summary>Measures real wall-clock cadence, independently of the fixed-step game clock.</summary>
internal sealed class GameLoopMetrics
{
    private long started = Stopwatch.GetTimestamp();
    private int draws;
    private int updates;
    private int renders;
    private long simulationFrames;
    private double updateMilliseconds;
    private double renderMilliseconds;
    private double uploadMilliseconds;
    private double maximumRenderMilliseconds;

    public GameLoopSample? Latest { get; private set; }

    public void Update(long start)
    {
        updates++;
        updateMilliseconds += Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    }

    public void Simulate(int frames)
    {
        simulationFrames += frames;
    }

    public void Render(double render, double upload)
    {
        renders++;
        renderMilliseconds += render;
        uploadMilliseconds += upload;
        maximumRenderMilliseconds = Math.Max(maximumRenderMilliseconds, render);
    }

    public void Draw()
    {
        draws++;
        double seconds = Stopwatch.GetElapsedTime(started).TotalSeconds;
        if (seconds < 2)
        {
            return;
        }
        Latest = new GameLoopSample(seconds, draws / seconds, updates / seconds, simulationFrames / seconds, draws, updates, renders, updateMilliseconds / Math.Max(1, updates), renderMilliseconds / Math.Max(1, renders), uploadMilliseconds / Math.Max(1, renders), maximumRenderMilliseconds);
        Console.WriteLine(FormattableString.Invariant($"loop draws/s={Latest.DrawsPerSecond:F2} updates/s={Latest.UpdatesPerSecond:F2} vm/s={Latest.SimulationFramesPerSecond:F2} draws={draws} updates={updates} renders={renders} update-ms={Latest.UpdateMilliseconds:F3} render-ms={Latest.RenderMilliseconds:F3} upload-ms={Latest.UploadMilliseconds:F3} render-max-ms={maximumRenderMilliseconds:F3}"));
        started = Stopwatch.GetTimestamp();
        draws = 0;
        updates = 0;
        renders = 0;
        simulationFrames = 0;
        updateMilliseconds = 0;
        renderMilliseconds = 0;
        uploadMilliseconds = 0;
        maximumRenderMilliseconds = 0;
    }
}
