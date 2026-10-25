using System.Diagnostics;

namespace Memoria.FF8.BattleEffects;

/// <summary>Process observations at an execution boundary; the peak is process lifetime, not a per-effect allocation counter.</summary>
public sealed record TelemetryProcessSample(long ManagedHeapBytes, long WorkingSetBytes, long PrivateMemoryBytes, long ProcessLifetimePeakWorkingSetBytes, double CpuMilliseconds)
{
    public static TelemetryProcessSample Capture()
    {
        using Process process = Process.GetCurrentProcess();
        process.Refresh();
        return new(GC.GetTotalMemory(false), process.WorkingSet64, process.PrivateMemorySize64, process.PeakWorkingSet64, process.TotalProcessorTime.TotalMilliseconds);
    }
}
