#nullable enable
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Memoria.FF8.BattleEffects;

public sealed record WindowsGpuSessionValidity(string Status, string Reason);

public sealed record WindowsGpuSessionObservation(WindowsGpuSessionValidity Validity, int CurrentSession, int? ReportedSession, int? Level, int? ConnectionState, int? SessionFlags, string? CurrentDesktop, string? InputDesktop, int? SessionError, int? DesktopError);

public static class WindowsGpuSessionProbe
{
    [StructLayout(LayoutKind.Explicit)]
    private struct SessionInfoPrefix
    {
        [FieldOffset(0)] public uint Level;
        [FieldOffset(8)] public uint Session;
        [FieldOffset(12)] public int State;
        [FieldOffset(16)] public int Flags;
    }

    [DllImport("wtsapi32.dll", EntryPoint = "WTSQuerySessionInformationW", SetLastError = true)]
    private static extern bool QuerySession(IntPtr server, int session, int information, out IntPtr buffer, out int bytes);

    [DllImport("wtsapi32.dll", EntryPoint = "WTSFreeMemory")]
    private static extern void FreeSession(IntPtr buffer);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr OpenInputDesktop(uint flags, bool inherit, uint access);

    [DllImport("user32.dll")]
    private static extern IntPtr GetThreadDesktop(uint thread);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", EntryPoint = "GetUserObjectInformationW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetDesktopName(IntPtr desktop, int index, StringBuilder name, int bytes, out int required);

    [DllImport("user32.dll")]
    private static extern bool CloseDesktop(IntPtr desktop);

    public static WindowsGpuSessionValidity Classify(int currentSession, int? reportedSession, int? level, int? connectionState, int? sessionFlags, bool inputDesktopAvailable, bool desktopMatches)
    {
        if (level != 1 || reportedSession != currentSession || connectionState is null || sessionFlags is null)
        {
            return new("unknown", "windows-session-state-unavailable");
        }
        if (sessionFlags == 0)
        {
            return new("invalid", "windows-session-locked");
        }
        if (sessionFlags != 1)
        {
            return new("unknown", "windows-session-lock-state-unavailable");
        }
        if (connectionState != 0)
        {
            return new("invalid", "windows-session-not-active");
        }
        if (!inputDesktopAvailable)
        {
            return new("invalid", "windows-input-desktop-unavailable");
        }
        return desktopMatches ? new("valid", "windows-session-interactive") : new("invalid", "windows-input-desktop-not-current");
    }

    public static WindowsGpuSessionObservation Observe()
    {
        if (!OperatingSystem.IsWindows())
        {
            return new(new("not_applicable", "not-windows"), 0, null, null, null, null, null, null, null, null);
        }
        using Process process = Process.GetCurrentProcess();
        int session = process.SessionId;
        int? reportedSession = null;
        int? level = null;
        int? state = null;
        int? flags = null;
        int? sessionError = null;
        if (QuerySession(IntPtr.Zero, session, 25, out IntPtr buffer, out int bytes))
        {
            try
            {
                if (bytes >= Marshal.SizeOf<SessionInfoPrefix>())
                {
                    SessionInfoPrefix prefix = Marshal.PtrToStructure<SessionInfoPrefix>(buffer);
                    level = checked((int)prefix.Level);
                    reportedSession = checked((int)prefix.Session);
                    state = prefix.State;
                    flags = prefix.Flags;
                }
            }
            finally
            {
                FreeSession(buffer);
            }
        }
        else
        {
            sessionError = Marshal.GetLastWin32Error();
        }
        string? currentDesktop = Name(GetThreadDesktop(GetCurrentThreadId()), out int? currentDesktopError);
        IntPtr input = OpenInputDesktop(0, false, 1);
        string? inputDesktop = null;
        int? desktopError;
        if (input == IntPtr.Zero)
        {
            desktopError = Marshal.GetLastWin32Error();
        }
        else
        {
            try
            {
                inputDesktop = Name(input, out desktopError);
            }
            finally
            {
                CloseDesktop(input);
            }
        }
        WindowsGpuSessionValidity validity = Classify(session, reportedSession, level, state, flags, inputDesktop is not null && currentDesktop is not null, string.Equals(currentDesktop, inputDesktop, StringComparison.OrdinalIgnoreCase));
        return new(validity, session, reportedSession, level, state, flags, currentDesktop, inputDesktop, sessionError, desktopError ?? currentDesktopError);
    }

    private static string? Name(IntPtr desktop, out int? error)
    {
        StringBuilder name = new(256);
        if (desktop != IntPtr.Zero && GetDesktopName(desktop, 2, name, name.Capacity * sizeof(char), out _))
        {
            error = null;
            return name.ToString();
        }
        error = desktop == IntPtr.Zero ? 6 : Marshal.GetLastWin32Error();
        return null;
    }
}
