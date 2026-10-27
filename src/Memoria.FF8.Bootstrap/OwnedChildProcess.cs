using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace Memoria.FF8.Bootstrap;

internal static class OwnedChildProcess
{
    public static IDisposable? Attach(Process process)
    {
        return OperatingSystem.IsWindows() ? WindowsJob.Attach(process) : null;
    }

    [SupportedOSPlatform("windows")]
    private sealed class WindowsJob(SafeFileHandle handle) : IDisposable
    {
        public static WindowsJob Attach(Process process)
        {
            SafeFileHandle job = CreateJobObjectW(IntPtr.Zero, null);
            try
            {
                if (job.IsInvalid)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
                ExtendedLimitInformation limits = new();
                limits.BasicLimitInformation.LimitFlags = 0x2000;
                if (!SetInformationJobObject(job, 9, ref limits, (uint)Marshal.SizeOf<ExtendedLimitInformation>()))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
                if (!AssignProcessToJobObject(job, process.SafeHandle) && !process.HasExited)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
                return new WindowsJob(job);
            }
            catch
            {
                job.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            handle.Dispose();
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BasicLimitInformation
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public nuint MinimumWorkingSetSize;
            public nuint MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public nuint Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IoCounters
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ExtendedLimitInformation
        {
            public BasicLimitInformation BasicLimitInformation;
            public IoCounters IoInfo;
            public nuint ProcessMemoryLimit;
            public nuint JobMemoryLimit;
            public nuint PeakProcessMemoryUsed;
            public nuint PeakJobMemoryUsed;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateJobObjectW(IntPtr attributes, string? name);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetInformationJobObject(SafeFileHandle job, int informationClass, ref ExtendedLimitInformation information, uint length);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AssignProcessToJobObject(SafeFileHandle job, SafeProcessHandle process);
    }
}
