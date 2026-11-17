using System.Runtime.InteropServices;

namespace Memoria.FF8;

internal static class DiagnosticThreadCycleCounter
{
    internal delegate bool Reader(out uint threadId, out ulong cycles);

    internal static bool TryRead(out uint threadId, out ulong cycles)
    {
        threadId = 0;
        cycles = 0;
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        threadId = GetCurrentThreadId();
        return QueryThreadCycleTime(GetCurrentThread(), out cycles);
    }

    [DllImport("kernel32.dll")]
    private static extern nint GetCurrentThread();

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryThreadCycleTime(nint thread, out ulong cycles);
}
