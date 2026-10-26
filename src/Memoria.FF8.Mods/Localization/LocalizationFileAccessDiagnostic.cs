using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace Memoria.FF8.Mods.Localization;

public sealed record LocalizationFileApplication(string DisplayName, int ProcessId);

public sealed class LocalizationFileAccessException : IOException
{
    public LocalizationFileAccessDiagnostic Diagnostic { get; }
    public string LocalizationPath { get; }

    public LocalizationFileAccessException(string path, IOException cause) : this(path, cause, LocalizationFileAccessDiagnostic.FromException(path, cause))
    {
    }

    private LocalizationFileAccessException(string path, IOException cause, LocalizationFileAccessDiagnostic diagnostic) : base(diagnostic.Message, cause)
    {
        HResult = cause.HResult;
        Diagnostic = diagnostic;
        LocalizationPath = Path.GetFullPath(path);
    }
}

/// <summary>Read-only Windows Restart Manager evidence; never releases another process's file handles.</summary>
public sealed record LocalizationFileAccessDiagnostic(bool IsSharingViolation, string Message, IReadOnlyList<LocalizationFileApplication> Applications)
{
    public static LocalizationFileAccessDiagnostic FromException(string path, Exception exception)
    {
        if (exception is LocalizationFileAccessException contextual)
        {
            return contextual.Diagnostic;
        }
        bool sharing = exception is IOException && (exception.HResult & 0xffff) == 32;
        IReadOnlyList<LocalizationFileApplication> applications = sharing ? FindApplications(path) : [];
        string owners = applications.Count > 0 ? string.Join(", ", applications.Select(application => $"{application.DisplayName} (PID {application.ProcessId})")) : "an application whose identity could not be determined";
        string message = sharing
            ? $"Cannot reload {Path.GetFileName(path)} while {owners} is using it and Windows denies read access. Save and close the workbook or the listed application, then Retry. The application list indicates file use; it does not establish which application caused the lock."
            : $"Cannot reload {Path.GetFileName(path)}: {exception.Message}";
        return new LocalizationFileAccessDiagnostic(sharing, message, applications);
    }

    private static IReadOnlyList<LocalizationFileApplication> FindApplications(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }
        uint session = 0;
        bool started = false;
        try
        {
            if (RmStartSession(out session, 0, Guid.NewGuid().ToString("N")) != 0)
            {
                return [];
            }
            started = true;
            if (RmRegisterResources(session, 1, [Path.GetFullPath(path)], 0, IntPtr.Zero, 0, IntPtr.Zero) != 0)
            {
                return [];
            }
            uint count = 0;
            uint reasons = 0;
            int status = RmGetList(session, out uint needed, ref count, null, ref reasons);
            if (status != 234 || needed > 1024)
            {
                return [];
            }
            ProcessInfo[] processes = new ProcessInfo[needed];
            count = needed;
            if (RmGetList(session, out needed, ref count, processes, ref reasons) != 0)
            {
                return [];
            }
            return processes.Take((int)count).Select(process => new LocalizationFileApplication(process.ApplicationName, process.Process.ProcessId)).ToArray();
        }
        catch (DllNotFoundException)
        {
            return [];
        }
        catch (EntryPointNotFoundException)
        {
            return [];
        }
        finally
        {
            if (started)
            {
                RmEndSession(session);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct UniqueProcess
    {
        public int ProcessId;
        public FILETIME StartTime;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessInfo
    {
        public UniqueProcess Process;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string ApplicationName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string ServiceShortName;
        public uint ApplicationType;
        public uint ApplicationStatus;
        public uint TerminalServicesSessionId;
        [MarshalAs(UnmanagedType.Bool)]
        public bool Restartable;
    }

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmStartSession(out uint sessionHandle, uint flags, string sessionKey);
    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmRegisterResources(uint sessionHandle, uint fileCount, string[] filenames, uint applicationCount, IntPtr applications, uint serviceCount, IntPtr serviceNames);
    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmGetList(uint sessionHandle, out uint needed, ref uint count, [In, Out] ProcessInfo[]? applications, ref uint rebootReasons);
    [DllImport("rstrtmgr.dll")]
    private static extern int RmEndSession(uint sessionHandle);
}
