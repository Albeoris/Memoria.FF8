using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Memoria.FF8.LinuxTesting;

internal static class OwnedCommand
{
    public static ProcessStartInfo Wrap(ProcessStartInfo original, string ready)
    {
        string assembly = Path.Combine(AppContext.BaseDirectory, "Memoria.FF8.LinuxTesting.dll");
        string apphost = Path.Combine(AppContext.BaseDirectory, "Memoria.FF8.LinuxTesting");
        bool native = File.Exists(apphost);
        ProcessStartInfo wrapper = new(native ? apphost : DotNetHost())
        {
            WorkingDirectory = original.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        wrapper.Environment.Clear();
        foreach (var item in original.Environment)
        {
            wrapper.Environment[item.Key] = item.Value;
        }
        if (!native)
        {
            wrapper.ArgumentList.Add(assembly);
        }
        wrapper.ArgumentList.Add("--owned-command");
        wrapper.ArgumentList.Add(ready);
        wrapper.ArgumentList.Add(original.FileName);
        foreach (string argument in original.ArgumentList)
        {
            wrapper.ArgumentList.Add(argument);
        }
        return wrapper;
    }

    private static string DotNetHost()
    {
        string? configured = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (!string.IsNullOrEmpty(configured) && File.Exists(configured))
        {
            return configured;
        }
        string installed = Path.GetFullPath(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", "..", "dotnet"));
        if (File.Exists(installed))
        {
            return installed;
        }
        foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            string candidate = Path.Combine(directory, "dotnet");
            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }
        throw new FileNotFoundException("The owned command requires its published apphost or the configured dotnet host.");
    }

    public static int Execute(string ready, string executable, string[] arguments)
    {
        if (!OperatingSystem.IsLinux() || setsid() != Environment.ProcessId)
        {
            throw new IOException("Cannot establish an owned Linux session.");
        }
        File.WriteAllText(ready + ".tmp", Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        File.Move(ready + ".tmp", ready);
        string[] values = [executable, .. arguments];
        IntPtr[] strings = values.Select(Marshal.StringToCoTaskMemUTF8).ToArray();
        IntPtr argv = Marshal.AllocHGlobal((strings.Length + 1) * IntPtr.Size);
        try
        {
            Marshal.Copy(strings, 0, argv, strings.Length);
            Marshal.WriteIntPtr(argv, strings.Length * IntPtr.Size, IntPtr.Zero);
            execvp(executable, argv);
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError(), "Cannot execute the owned command.");
        }
        finally
        {
            foreach (IntPtr value in strings)
            {
                Marshal.FreeCoTaskMem(value);
            }
            Marshal.FreeHGlobal(argv);
        }
    }

    public static void TerminateGroup(int group)
    {
        if (!OperatingSystem.IsLinux() || group <= 0)
        {
            return;
        }
        if (kill(-group, 15) != 0)
        {
            if (Marshal.GetLastPInvokeError() == 3)
            {
                return;
            }
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError(), "Cannot stop the owned Linux session.");
        }
        // Give the dispatcher time to cancel and clean its separately owned game session.
        Stopwatch grace = Stopwatch.StartNew();
        while (grace.Elapsed < TimeSpan.FromSeconds(2))
        {
            if (kill(-group, 0) != 0 && Marshal.GetLastPInvokeError() == 3)
            {
                return;
            }
            Thread.Sleep(25);
        }
        if (kill(-group, 9) != 0 && Marshal.GetLastPInvokeError() != 3)
        {
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError(), "Cannot terminate the owned Linux session.");
        }
    }

    [DllImport("libc", SetLastError = true)]
    private static extern int setsid();

    [DllImport("libc", SetLastError = true)]
    private static extern int execvp([MarshalAs(UnmanagedType.LPUTF8Str)] string path, IntPtr argv);

    [DllImport("libc", SetLastError = true)]
    private static extern int kill(int pid, int signal);
}
