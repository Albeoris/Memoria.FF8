using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace Memoria.FF8.BattleEffects;

public sealed record SupervisionBudgets(TimeSpan Initialization, TimeSpan ActionOne, TimeSpan CleanupRearm, TimeSpan ActionTwo, TimeSpan Teardown, TimeSpan Total, TimeSpan NoProgress)
{
    public TimeSpan Stage(string stage)
    {
        return stage switch
        {
            "initialization" => Initialization,
            "action_one" => ActionOne,
            "cleanup_rearm" => CleanupRearm,
            "action_two" => ActionTwo,
            "teardown" => Teardown,
            _ => throw new ArgumentException("Unsupported supervisor stage.", nameof(stage))
        };
    }

    public void Validate()
    {
        foreach (TimeSpan duration in new[] { Initialization, ActionOne, CleanupRearm, ActionTwo, Teardown, Total, NoProgress })
        {
            if (duration <= TimeSpan.Zero || duration > TimeSpan.FromDays(1))
            {
                throw new ArgumentException("Every supervision bound must be finite, positive, and at most one day.");
            }
        }
    }
}

public sealed record SupervisionRequest(string AttemptId, string Executable, IReadOnlyList<string> Arguments, string WorkingDirectory, string SettingsDirectory, string OutputDirectory, string ControlDirectory, SupervisionBudgets Budgets, IReadOnlyDictionary<string, string>? Environment = null);
public sealed record SupervisionProgress(string Stage, long Sequence, IReadOnlyList<string>? CompletedStages = null);
public sealed record SupervisionOutcome(string AttemptId, string Status, string? ErrorCategory, string LastStage, string? Message, int? ExitCode, long ElapsedMs, ProcessIdentity? Process, bool ProcessExited, string ResultPath, byte[]? CandidatePayload, string? LogTail = null);

/// <summary>Owns a suspended Windows child before it can execute and is the sole result publisher.</summary>
public static class OwnedChildSupervisor
{
    private static readonly string[] Stages = ["initialization", "action_one", "cleanup_rearm", "action_two", "teardown"];
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = false, WriteIndented = true };

    public static async Task<SupervisionOutcome> RunAsync(SupervisionRequest request, CancellationToken cancellationToken = default, Func<ProcessIdentity, bool>? acquireSlot = null, Action<byte[]>? persistStarted = null)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Owned-child supervision currently requires Windows job objects.");
        }
        Validate(request);
        string resultPath = Path.Combine(request.OutputDirectory, "result.json");
        string candidatePath = Path.Combine(request.OutputDirectory, "candidate.json");
        string progressPath = Path.Combine(request.ControlDirectory, "progress.json");
        Directory.CreateDirectory(request.OutputDirectory);
        Directory.CreateDirectory(request.SettingsDirectory);
        Directory.CreateDirectory(request.ControlDirectory);
        if (File.Exists(resultPath) || File.Exists(candidatePath) || File.Exists(progressPath) || File.Exists(Path.Combine(request.OutputDirectory, "started.json")))
        {
            throw new InvalidOperationException("An attempt requires fresh channels and cannot replace earlier evidence.");
        }
        byte[] startedPayload = JsonSerializer.SerializeToUtf8Bytes(new { schema_version = 1, attempt_id = request.AttemptId, started_utc = DateTimeOffset.UtcNow, executable = request.Executable, arguments = request.Arguments, settings_directory = request.SettingsDirectory, output_directory = request.OutputDirectory, control_directory = request.ControlDirectory, budgets = request.Budgets }, JsonOptions);
        persistStarted?.Invoke(startedPayload);
        PublishTransientImmutable(Path.Combine(request.OutputDirectory, "started.json"), startedPayload);

        Stopwatch total = Stopwatch.StartNew();
        Stopwatch stageTime = Stopwatch.StartNew();
        Stopwatch progressTime = Stopwatch.StartNew();
        string stage = Stages[0];
        long sequence = -1;
        ProcessIdentity? identity = null;
        NativeChild? child = null;
        int? exitCode = null;
        string? category = null;
        string? message = null;
        byte[]? candidate = null;
        bool exited = true;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            child = NativeChild.Start(request, childIdentity =>
            {
                identity = childIdentity;
                cancellationToken.ThrowIfCancellationRequested();
                bool acquired = acquireSlot?.Invoke(childIdentity) ?? true;
                cancellationToken.ThrowIfCancellationRequested();
                return acquired;
            });
            identity = child.Identity;
            exited = false;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (total.Elapsed >= request.Budgets.Total)
                {
                    category = "TIMEOUT";
                    message = "The process wall-clock bound expired.";
                    break;
                }
                if (stageTime.Elapsed >= request.Budgets.Stage(stage))
                {
                    category = "TIMEOUT";
                    message = "The current stage bound expired.";
                    break;
                }
                if (progressTime.Elapsed >= request.Budgets.NoProgress)
                {
                    category = "TIMEOUT";
                    message = "The independent no-progress bound expired.";
                    break;
                }
                SupervisionProgress? progress = ReadProgress(progressPath);
                if (progress is not null)
                {
                    int current = Array.IndexOf(Stages, stage);
                    int next = Array.IndexOf(Stages, progress.Stage);
                    bool historyComplete = progress.CompletedStages is not null && progress.CompletedStages.SequenceEqual(Stages.Take(Math.Max(0, next)), StringComparer.Ordinal);
                    if (next < current || (next > current + 1 && !historyComplete) || next < 0 || progress.Sequence < sequence)
                    {
                        throw new InvalidDataException("The progress channel regressed or skipped a required stage.");
                    }
                    if (progress.Sequence > sequence)
                    {
                        sequence = progress.Sequence;
                        progressTime.Restart();
                    }
                    else if (next != current)
                    {
                        throw new InvalidDataException("Stage transitions require a new progress sequence.");
                    }
                    if (next != current)
                    {
                        stage = progress.Stage;
                        stageTime.Restart();
                    }
                }
                if (child.HasExited)
                {
                    exitCode = child.ExitCode;
                    exited = true;
                    if (exitCode != 0)
                    {
                        category = "CRASH";
                        message = "The owned process exited with a nonzero code.";
                    }
                    else if (!File.Exists(candidatePath))
                    {
                        category = "MISSING_CANDIDATE";
                        message = "The owned process exited without a candidate report.";
                    }
                    else
                    {
                        FileInfo info = new(candidatePath);
                        if (info.Length == 0 || info.Length > 16 * 1024 * 1024)
                        {
                            throw new InvalidDataException("The candidate report is empty or exceeds the bounded summary size.");
                        }
                        candidate = File.ReadAllBytes(candidatePath);
                        using JsonDocument parsed = JsonDocument.Parse(candidate);
                        if (parsed.RootElement.ValueKind != JsonValueKind.Object)
                        {
                            throw new InvalidDataException("A candidate report must be a JSON object.");
                        }
                        if (parsed.RootElement.TryGetProperty("status", out JsonElement status) && status.GetString() == "ERROR")
                        {
                            category = "HOST_REPORTED_ERROR";
                            message = parsed.RootElement.TryGetProperty("reason", out JsonElement reason) && reason.ValueKind == JsonValueKind.String ? reason.GetString() : "The graphical host reported an error without a reason.";
                        }
                        else if (stage != "teardown")
                        {
                            category = "INCOMPLETE_STAGES";
                            message = "The owned process did not report all mandatory stages.";
                        }
                    }
                    break;
                }
                await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            category = "CANCELLED";
            message = "The attempt was cancelled; completed evidence is preserved.";
        }
        catch (Exception exception) when (exception is Win32Exception or IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException or JsonException or ArgumentException)
        {
            category = "SUPERVISION";
            message = exception.Message;
        }
        finally
        {
            if (child is not null)
            {
                child.StopOwnedTree();
                exited = child.WaitForExit(TimeSpan.FromSeconds(5));
                if (exited)
                {
                    exitCode ??= child.ExitCode;
                }
                child.Dispose();
            }
        }
        if (child is null && identity is not null)
        {
            exited = new OwnedProcessProbe().HasExited(identity);
        }
        if (!exited)
        {
            category = "SUPERVISION";
            message = "Owned process termination was not confirmed; its performance slot must remain held.";
            candidate = null;
        }
        SupervisionOutcome outcome = new(request.AttemptId, category is null ? "candidate_ready" : "error", category, stage, message, exitCode, total.ElapsedMilliseconds, identity, exited, resultPath, candidate, ReadLogTail(Path.Combine(request.OutputDirectory, "host.log")));
        if (category is not null)
        {
            PublishError(outcome);
        }
        return outcome;
    }

    /// <summary>The caller must independently validate schema, identity, evidence and every PASS gate.</summary>
    public static void PublishValidatedCandidate(SupervisionOutcome outcome, Func<byte[], byte[]> validateAndFinalize)
    {
        ArgumentNullException.ThrowIfNull(validateAndFinalize);
        if (outcome.Status != "candidate_ready" || outcome.CandidatePayload is null || !outcome.ProcessExited)
        {
            throw new InvalidOperationException("Only a stopped child with a complete candidate may be finalized.");
        }
        byte[] validated;
        try
        {
            validated = validateAndFinalize(outcome.CandidatePayload);
            using JsonDocument document = JsonDocument.Parse(validated);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("The validated result must be a JSON object.");
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or JsonException or ArgumentException or InvalidOperationException)
        {
            PublishError(outcome with { Status = "error", ErrorCategory = "INVALID_CANDIDATE", Message = exception.Message, CandidatePayload = null });
            return;
        }
        PublishTransientImmutable(outcome.ResultPath, validated);
    }

    private static void PublishError(SupervisionOutcome outcome)
    {
        PublishTransientImmutable(outcome.ResultPath, JsonSerializer.SerializeToUtf8Bytes(new { schema_version = 1, attempt_id = outcome.AttemptId, verdict = "ERROR", error_category = outcome.ErrorCategory, last_stage = outcome.LastStage, message = outcome.Message, exit_code = outcome.ExitCode, elapsed_ms = outcome.ElapsedMs, process = outcome.Process, process_exited = outcome.ProcessExited, log_tail = outcome.LogTail, log_available = outcome.LogTail is not null, completed_utc = DateTimeOffset.UtcNow }, JsonOptions));
    }

    /// <summary>Publishes a temporary process-channel file; durable evidence is imported into the campaign store.</summary>
    public static void PublishTransientImmutable(string path, byte[] content)
    {
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        string temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(content);
                stream.Flush(flushToDisk: true);
            }
            try
            {
                File.Move(temporary, fullPath, overwrite: false);
            }
            catch (IOException) when (File.Exists(fullPath))
            {
                if (!File.ReadAllBytes(fullPath).AsSpan().SequenceEqual(content))
                {
                    throw new InvalidDataException("An immutable temporary publication already exists with different content.");
                }
            }
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    private static string? ReadLogTail(string path)
    {
        try
        {
            using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            stream.Seek(Math.Max(0, stream.Length - 8192), SeekOrigin.Begin);
            byte[] buffer = new byte[8192];
            int length = stream.Read(buffer);
            return Encoding.UTF8.GetString(buffer, 0, length);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static SupervisionProgress? ReadProgress(string path)
    {
        try
        {
            // The host publishes by atomic replacement. Read one opened publication and
            // allow its directory entry to be replaced while this handle remains alive.
            using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            long length = stream.Length;
            if (length == 0 || length > 4096)
            {
                throw new InvalidDataException("The progress channel must be a bounded JSON record.");
            }
            byte[] bytes = new byte[checked((int)length)];
            stream.ReadExactly(bytes);
            return JsonSerializer.Deserialize<SupervisionProgress>(bytes, JsonOptions) ?? throw new InvalidDataException("The progress channel was null.");
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
        catch (IOException exception) when (OperatingSystem.IsWindows() && (exception.HResult & 0xFFFF) is 32 or 33)
        {
            // A concurrent publisher or external reader may briefly deny sharing. This
            // is not progress: all original wall-clock and no-progress bounds still run.
            return null;
        }
    }

    private static void Validate(SupervisionRequest request)
    {
        EvidenceStore.Id(request.AttemptId);
        request.Budgets.Validate();
        if (!Path.IsPathFullyQualified(request.Executable) || !File.Exists(request.Executable) || !Path.IsPathFullyQualified(request.WorkingDirectory) || !Directory.Exists(request.WorkingDirectory))
        {
            throw new ArgumentException("The executable and working directory must be existing explicit absolute paths.");
        }
        string[] isolated = [ValidateTransientDirectory(request.SettingsDirectory), ValidateTransientDirectory(request.OutputDirectory), ValidateTransientDirectory(request.ControlDirectory)];
        for (int index = 0; index < isolated.Length; index++)
        {
            for (int other = 0; other < isolated.Length; other++)
            {
                if (index != other && (string.Equals(isolated[index], isolated[other], StringComparison.OrdinalIgnoreCase) || isolated[index].StartsWith(isolated[other] + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new ArgumentException("Settings, output, and control directories must be distinct and non-overlapping.");
                }
            }
        }
        if (request.Arguments.Any(argument => argument.Contains('\0')) || request.Executable.Contains('\0'))
        {
            throw new ArgumentException("Process arguments cannot contain NUL characters.");
        }
    }

    private static string ValidateTransientDirectory(string path)
    {
        return PrivateEvidencePath.Validate(path);
    }

    private sealed class NativeChild : IDisposable
    {
        private readonly SafeFileHandle job;
        private readonly SafeProcessHandle process;
        public ProcessIdentity Identity { get; }

        private NativeChild(SafeFileHandle job, SafeProcessHandle process, ProcessIdentity identity)
        {
            this.job = job;
            this.process = process;
            Identity = identity;
        }

        public bool HasExited
        {
            get
            {
                return Native.WaitForSingleObject(process, 0) == 0;
            }
        }

        public int ExitCode
        {
            get
            {
                if (!Native.GetExitCodeProcess(process, out uint code))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
                return unchecked((int)code);
            }
        }

        public static NativeChild Start(SupervisionRequest request, Func<ProcessIdentity, bool> acquireSlot)
        {
            SafeFileHandle job = Native.CreateJobObjectW(IntPtr.Zero, null);
            if (job.IsInvalid)
            {
                job.Dispose();
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            SafeProcessHandle? process = null;
            IntPtr environment = IntPtr.Zero;
            try
            {
                Native.ExtendedLimitInformation limits = new();
                limits.BasicLimitInformation.LimitFlags = 0x2000;
                if (!Native.SetInformationJobObject(job, 9, ref limits, (uint)Marshal.SizeOf<Native.ExtendedLimitInformation>()))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
                environment = Marshal.StringToHGlobalUni(EnvironmentBlock(request));
                Native.StartupInfo startup = new() { Size = Marshal.SizeOf<Native.StartupInfo>() };
                StringBuilder command = new(string.Join(" ", new[] { request.Executable }.Concat(request.Arguments).Select(QuoteArgument)));
                if (!Native.CreateProcessW(request.Executable, command, IntPtr.Zero, IntPtr.Zero, false, 0x00000004 | 0x00000400 | 0x08000000, environment, request.WorkingDirectory, ref startup, out Native.ProcessInformation info))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
                process = new SafeProcessHandle(info.Process, ownsHandle: true);
                using SafeFileHandle thread = new(info.Thread, ownsHandle: true);
                try
                {
                    if (!Native.AssignProcessToJobObject(job, process))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }
                    using Process diagnostic = Process.GetProcessById((int)info.ProcessId);
                    ProcessIdentity identity = new((int)info.ProcessId, diagnostic.StartTime.ToUniversalTime());
                    if (!acquireSlot(identity))
                    {
                        throw new CoordinationOwnershipException("The performance slot was denied before child execution.");
                    }
                    if (Native.ResumeThread(thread) == uint.MaxValue)
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }
                    return new NativeChild(job, process, identity);
                }
                catch
                {
                    Native.TerminateProcess(process, 1);
                    Native.WaitForSingleObject(process, 5000);
                    throw;
                }
            }
            catch
            {
                process?.Dispose();
                job.Dispose();
                throw;
            }
            finally
            {
                if (environment != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(environment);
                }
            }
        }

        public void StopOwnedTree()
        {
            Native.TerminateJobObject(job, 1);
        }

        public bool WaitForExit(TimeSpan timeout)
        {
            Stopwatch elapsed = Stopwatch.StartNew();
            while (elapsed.Elapsed < timeout)
            {
                if (!Native.QueryInformationJobObject(job, 1, out Native.BasicAccountingInformation accounting, (uint)Marshal.SizeOf<Native.BasicAccountingInformation>(), IntPtr.Zero))
                {
                    return false;
                }
                if (HasExited && accounting.ActiveProcesses == 0)
                {
                    return true;
                }
                Thread.Sleep(10);
            }
            return false;
        }

        public void Dispose()
        {
            job.Dispose();
            process.Dispose();
        }

        private static string EnvironmentBlock(SupervisionRequest request)
        {
            SortedDictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in System.Environment.GetEnvironmentVariables())
            {
                values[(string)entry.Key] = (string)entry.Value!;
            }
            if (request.Environment is not null)
            {
                foreach ((string key, string value) in request.Environment)
                {
                    if (key.Length == 0 || key.Contains('=') || key.Contains('\0') || value.Contains('\0'))
                    {
                        throw new ArgumentException("Invalid child environment entry.");
                    }
                    values[key] = value;
                }
            }
            values["MEMORIA_FF8_SETTINGS_DIRECTORY"] = request.SettingsDirectory;
            values["MEMORIA_FF8_OUTPUT_DIRECTORY"] = request.OutputDirectory;
            values["MEMORIA_FF8_CONTROL_DIRECTORY"] = request.ControlDirectory;
            return string.Join('\0', values.Select(pair => pair.Key + "=" + pair.Value)) + "\0\0";
        }

        private static string QuoteArgument(string argument)
        {
            if (argument.Length > 0 && !argument.Any(character => char.IsWhiteSpace(character) || character == '"'))
            {
                return argument;
            }
            StringBuilder quoted = new("\"");
            int slashes = 0;
            foreach (char character in argument)
            {
                if (character == '\\')
                {
                    slashes++;
                    continue;
                }
                if (character == '"')
                {
                    quoted.Append('\\', slashes * 2 + 1);
                    quoted.Append('"');
                }
                else
                {
                    quoted.Append('\\', slashes);
                    quoted.Append(character);
                }
                slashes = 0;
            }
            quoted.Append('\\', slashes * 2);
            quoted.Append('"');
            return quoted.ToString();
        }
    }

    private static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct BasicLimitInformation
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct IoCounters
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct ExtendedLimitInformation
        {
            public BasicLimitInformation BasicLimitInformation;
            public IoCounters IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct BasicAccountingInformation
        {
            public long TotalUserTime;
            public long TotalKernelTime;
            public long ThisPeriodTotalUserTime;
            public long ThisPeriodTotalKernelTime;
            public uint TotalPageFaultCount;
            public uint TotalProcesses;
            public uint ActiveProcesses;
            public uint TotalTerminatedProcesses;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct StartupInfo
        {
            public int Size;
            public string? Reserved;
            public string? Desktop;
            public string? Title;
            public uint X;
            public uint Y;
            public uint XSize;
            public uint YSize;
            public uint XCountChars;
            public uint YCountChars;
            public uint FillAttribute;
            public uint Flags;
            public ushort ShowWindow;
            public ushort ReservedSize;
            public IntPtr ReservedPointer;
            public IntPtr StandardInput;
            public IntPtr StandardOutput;
            public IntPtr StandardError;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct ProcessInformation
        {
            public IntPtr Process;
            public IntPtr Thread;
            public uint ProcessId;
            public uint ThreadId;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern SafeFileHandle CreateJobObjectW(IntPtr attributes, string? name);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetInformationJobObject(SafeFileHandle job, int informationClass, ref ExtendedLimitInformation information, uint length);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CreateProcessW(string application, StringBuilder commandLine, IntPtr processAttributes, IntPtr threadAttributes, [MarshalAs(UnmanagedType.Bool)] bool inheritHandles, uint flags, IntPtr environment, string workingDirectory, ref StartupInfo startup, out ProcessInformation information);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool AssignProcessToJobObject(SafeFileHandle job, SafeProcessHandle process);
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern uint ResumeThread(SafeFileHandle thread);
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern uint WaitForSingleObject(SafeProcessHandle process, uint milliseconds);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetExitCodeProcess(SafeProcessHandle process, out uint exitCode);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool TerminateJobObject(SafeFileHandle job, uint exitCode);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool TerminateProcess(SafeProcessHandle process, uint exitCode);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool QueryInformationJobObject(SafeFileHandle job, int informationClass, out BasicAccountingInformation information, uint length, IntPtr returnLength);
    }
}
