using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Memoria.FF8.Bootstrap;

internal static class UnixHostProcess
{
    internal const string Command = "--run-owned-host";

    internal static ProcessStartInfo Wrap(ProcessStartInfo host)
    {
        string executable = Path.Combine(AppContext.BaseDirectory, "Memoria.FF8");
        ProcessStartInfo start = new(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = host.WorkingDirectory
        };
        start.ArgumentList.Add(Command);
        start.ArgumentList.Add(host.FileName);
        foreach (string argument in host.ArgumentList)
        {
            start.ArgumentList.Add(argument);
        }
        start.Environment.Clear();
        foreach ((string key, string? value) in host.Environment)
        {
            start.Environment[key] = value;
        }
        return start;
    }

    internal static void Execute(string[] arguments)
    {
        if (OperatingSystem.IsWindows() || arguments.Length < 2 || !Path.IsPathFullyQualified(arguments[1]))
        {
            throw new ArgumentException("An owned Unix host requires an absolute executable path.");
        }
        if (setsid() == -1 && getsid(0) != Environment.ProcessId)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create an owned game process session.");
        }
        string[] values = arguments[1..];
        nint[] strings = values.Select(Marshal.StringToCoTaskMemUTF8).ToArray();
        nint vector = Marshal.AllocHGlobal((strings.Length + 1) * IntPtr.Size);
        try
        {
            for (int i = 0; i < strings.Length; i++)
            {
                Marshal.WriteIntPtr(vector, i * IntPtr.Size, strings[i]);
            }
            Marshal.WriteIntPtr(vector, strings.Length * IntPtr.Size, 0);
            _ = execv(values[0], vector);
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not execute the selected game host.");
        }
        finally
        {
            Marshal.FreeHGlobal(vector);
            foreach (nint value in strings)
            {
                Marshal.FreeCoTaskMem(value);
            }
        }
    }

    internal sealed class Session(int processId) : IDisposable
    {
        public void Dispose()
        {
            if (processId <= 1)
            {
                throw new InvalidOperationException("Invalid owned Unix process group.");
            }
            if (kill(-processId, 15) == 0)
            {
                Stopwatch timer = Stopwatch.StartNew();
                while (timer.Elapsed < TimeSpan.FromMilliseconds(500) && kill(-processId, 0) == 0)
                {
                    Thread.Sleep(10);
                }
                _ = kill(-processId, 9);
            }
        }
    }

    [DllImport("libc", SetLastError = true)]
    private static extern int setsid();

    [DllImport("libc", SetLastError = true)]
    private static extern int getsid(int processId);

    [DllImport("libc", SetLastError = true)]
    private static extern int execv([MarshalAs(UnmanagedType.LPUTF8Str)] string path, nint arguments);

    [DllImport("libc", SetLastError = true)]
    private static extern int kill(int processId, int signal);
}
