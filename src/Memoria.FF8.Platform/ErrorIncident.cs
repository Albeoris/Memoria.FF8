using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Memoria.FF8.Platform;

public sealed record ErrorException(string Type, string Message, int HResult, string? Stack, IReadOnlyList<ErrorException> Causes)
{
    internal static ErrorException Capture(Exception exception)
    {
        IEnumerable<Exception> causes = exception is AggregateException aggregate ? aggregate.InnerExceptions : exception.InnerException is { } inner ? new[] { inner } : Array.Empty<Exception>();
        return new(exception.GetType().FullName ?? exception.GetType().Name, exception.Message, exception.HResult, RedactSourcePaths(exception.StackTrace), Array.AsReadOnly(causes.Select(Capture).ToArray()));
    }

    private static string? RedactSourcePaths(string? stack)
    {
        return stack is null ? null : Regex.Replace(stack, @" in (?<path>(?:[A-Za-z]:[\\/]|/)[^\r\n]+):line (?<line>\d+)", match =>
        {
            string path = match.Groups["path"].Value.Replace('\\', '/');
            return " in " + path[(path.LastIndexOf('/') + 1)..] + ":line " + match.Groups["line"].Value;
        });
    }

    public override string ToString()
    {
        StringBuilder text = new();
        Write(this, text, 0);
        return text.ToString();
    }

    private static void Write(ErrorException error, StringBuilder text, int depth)
    {
        text.Append(' ', depth * 2).Append(error.Type).Append(": ").AppendLine(error.Message);
        text.Append("HResult: ").AppendLine(error.HResult.ToString("X8"));
        text.AppendLine(error.Stack ?? "Stack unavailable.");
        foreach (ErrorException cause in error.Causes)
        {
            text.AppendLine("Caused by:");
            Write(cause, text, depth + 1);
        }
    }
}

public sealed record ErrorBreadcrumb(DateTimeOffset Timestamp, string Category, string Message);

public sealed class ErrorBreadcrumbs
{
    private readonly Queue<ErrorBreadcrumb> entries = new();
    private int bytes;

    public void Add(string category, string message)
    {
        ErrorBreadcrumb entry = new(DateTimeOffset.UtcNow, category, message);
        int size = Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(entry));
        if (size > 1024 * 1024)
        {
            return;
        }
        lock (entries)
        {
            entries.Enqueue(entry);
            bytes += size;
            while (entries.Count > 1000 || bytes > 1024 * 1024)
            {
                bytes -= Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(entries.Dequeue()));
            }
        }
    }

    internal ErrorBreadcrumb[] Snapshot()
    {
        lock (entries)
        {
            return entries.ToArray();
        }
    }
}

public sealed class ReportSave
{
    private readonly byte[] bytes;
    public string Kind { get; }
    public DateTimeOffset CapturedUtc { get; }
    public string Validation { get; }
    public int Length => bytes.Length;
    internal string ArchiveFileName => bytes.AsSpan().StartsWith("MFF8SAVE"u8) ? "state.ff8save" : "state.json";

    // Callers supply existing-format bytes only after independent storage/continuation validation.
    public ReportSave(byte[] bytes, string kind, DateTimeOffset capturedUtc, string validation)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(validation);
        this.bytes = (byte[])bytes.Clone();
        Kind = kind;
        CapturedUtc = capturedUtc;
        Validation = validation;
    }

    internal byte[] CopyBytes() => (byte[])bytes.Clone();
}

public sealed class ErrorIncident
{
    internal static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly string sessionId = Guid.NewGuid().ToString("N");
    private readonly string[] logPaths;
    public string Id { get; }
    public string LaunchId { get; }
    public DateTimeOffset Timestamp { get; }
    public string Subsystem { get; }
    public ErrorException Exception { get; }
    public JsonElement Runtime { get; }
    public JsonElement Environment { get; }
    public ReportSave? Save { get; }
    public string SaveUnavailableReason { get; }
    public IReadOnlyList<ErrorBreadcrumb> Events { get; }
    public IReadOnlyList<string> CollectionErrors { get; }
    internal IReadOnlyList<string> LogPaths => logPaths;

    private ErrorIncident(string id, string launchId, DateTimeOffset timestamp, string subsystem, ErrorException exception, JsonElement runtime, JsonElement environment, ReportSave? save, string saveUnavailableReason, ErrorBreadcrumb[] events, string[] collectionErrors, string[] logPaths)
    {
        Id = id;
        LaunchId = launchId;
        Timestamp = timestamp;
        Subsystem = subsystem;
        Exception = exception;
        Runtime = runtime;
        Environment = environment;
        Save = save;
        SaveUnavailableReason = saveUnavailableReason;
        Events = Array.AsReadOnly(events);
        CollectionErrors = Array.AsReadOnly(collectionErrors);
        this.logPaths = logPaths;
    }

    public static ErrorIncident Capture(Exception exception, string subsystem, object? runtime = null, object? environment = null, ReportSave? save = null, IEnumerable<string>? logPaths = null, ErrorBreadcrumbs? breadcrumbs = null, string? saveUnavailableReason = null, string? launchId = null)
    {
        ArgumentNullException.ThrowIfNull(exception);
        List<string> errors = new();
        JsonElement Detach(object? value, string name)
        {
            try
            {
                return JsonSerializer.SerializeToElement(value ?? new { unavailable = "Not captured." }, JsonOptions);
            }
            catch (Exception failure)
            {
                errors.Add($"{name}: {failure.GetType().Name}: {failure.Message}");
                return JsonSerializer.SerializeToElement(new { unavailable = "Collection failed." });
            }
        }
        JsonElement runtimeData = Detach(runtime, "runtime");
        JsonElement details = Detach(environment, "environment");
        System.Reflection.Assembly assembly = System.Reflection.Assembly.GetEntryAssembly() ?? typeof(ErrorIncident).Assembly;
        JsonElement environmentData = JsonSerializer.SerializeToElement(new { productVersion = assembly.GetName().Version?.ToString(), build = assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false).OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion, dotnet = System.Environment.Version.ToString(), os = System.Runtime.InteropServices.RuntimeInformation.OSDescription, architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(), details }, JsonOptions);
        return new(Guid.NewGuid().ToString("N"), launchId ?? System.Environment.GetEnvironmentVariable("MEMORIA_FF8_REPORT_LAUNCH_ID") ?? sessionId, DateTimeOffset.UtcNow, subsystem, ErrorException.Capture(exception), runtimeData, environmentData, save, saveUnavailableReason ?? (save is null ? "No safe validated save was available at the failure boundary." : ""), breadcrumbs?.Snapshot() ?? [], errors.ToArray(), logPaths?.ToArray() ?? []);
    }

    public string ExceptionText => Exception.ToString();

    public ErrorIncident WithSave(ReportSave save) => new(Id, LaunchId, Timestamp, Subsystem, Exception, Runtime, Environment, save, SaveUnavailableReason, Events.ToArray(), CollectionErrors.ToArray(), (string[])logPaths.Clone());

    public ErrorIncident WithSaveUnavailableReason(string reason) => new(Id, LaunchId, Timestamp, Subsystem, Exception, Runtime, Environment, Save, reason, Events.ToArray(), CollectionErrors.ToArray(), (string[])logPaths.Clone());

    public ErrorIncident WithLogPaths(IEnumerable<string> paths) => new(Id, LaunchId, Timestamp, Subsystem, Exception, Runtime, Environment, Save, SaveUnavailableReason, Events.ToArray(), CollectionErrors.ToArray(), logPaths.Concat(paths).Distinct().ToArray());

    internal static string PendingRoot(string reportsDirectory) => Path.GetFullPath(reportsDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + ".pending";

    internal string PublicLaunchId => Guid.TryParseExact(LaunchId, "N", out _) ? LaunchId : PublicLaunchIdentity(Id, LaunchId);

    private static string PublicLaunchIdentity(string incidentId, string launchId)
    {
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(incidentId + ":" + launchId)))[..32].ToLowerInvariant();
    }

    internal byte[] EvidenceBytes(bool includeSave, bool published = false)
    {
        object evidence = new { schemaVersion = 1, Id, LaunchId = published ? PublicLaunchId : LaunchId, Timestamp, Subsystem, Exception, Runtime, Environment, SaveUnavailableReason = published && Save is not null && !includeSave ? "Automatic reproduction save excluded because complete snapshots cannot be safely sanitized." : SaveUnavailableReason, Events, CollectionErrors, LogPaths = published ? Array.Empty<string>() : logPaths, Save = !includeSave || Save is null ? null : new { Save.Kind, Save.CapturedUtc, Save.Validation } };
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(evidence, JsonOptions);
        return published ? ReportPrivacy.JsonBytes(bytes) : bytes;
    }

    public string Persist(string? reportsDirectory = null)
    {
        string directory = Path.Combine(PendingRoot(reportsDirectory ?? ApplicationPaths.Current.Reports), Id);
        Directory.CreateDirectory(directory);
        if (Save is not null)
        {
            AtomicWrite(Path.Combine(directory, "state.json"), Save.CopyBytes());
        }
        AtomicWrite(Path.Combine(directory, "incident.json"), EvidenceBytes(Save is not null));
        return directory;
    }

    public void MarkPresented(string? reportsDirectory = null)
    {
        string directory = Path.Combine(PendingRoot(reportsDirectory ?? ApplicationPaths.Current.Reports), Id);
        Directory.CreateDirectory(directory);
        AtomicWrite(Path.Combine(directory, "presented"), []);
    }

    public static ErrorIncident? DiscoverLatest(string? reportsDirectory = null, string? launchId = null, bool pendingOnly = false)
    {
        string root = reportsDirectory ?? ApplicationPaths.Current.Reports;
        string pendingRoot = PendingRoot(root);
        IEnumerable<string> pending = Directory.Exists(pendingRoot) ? Directory.EnumerateFiles(pendingRoot, "incident.json", SearchOption.AllDirectories) : [];
        IEnumerable<string> completed = Directory.Exists(root) ? Directory.EnumerateFiles(root, "*.zip", SearchOption.AllDirectories) : [];
        // Also accept previously persisted incidents until they are packaged by this version.
        IEnumerable<string> legacy = Directory.Exists(root) ? Directory.EnumerateFiles(root, "incident.json", SearchOption.AllDirectories) : [];
        foreach (string file in pending.Concat(completed).Concat(legacy).OrderByDescending(File.GetLastWriteTimeUtc))
        {
            try
            {
                using ZipArchive? archive = file.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ? ZipFile.OpenRead(file) : null;
                using Stream evidence = archive is null ? File.OpenRead(file) : archive.GetEntry("incident.json")?.Open() ?? throw new InvalidDataException("Report recovery metadata is unavailable.");
                using JsonDocument document = JsonDocument.Parse(evidence);
                JsonElement data = document.RootElement;
                string id = data.GetProperty("Id").GetString()!;
                if (!Guid.TryParseExact(id, "N", out _))
                {
                    continue;
                }
                if (pendingOnly && (File.Exists(Path.Combine(pendingRoot, id, "presented")) || File.Exists(Path.Combine(Path.GetDirectoryName(file)!, "presented"))))
                {
                    continue;
                }
                string capturedLaunch = data.GetProperty("LaunchId").GetString()!;
                if (launchId is not null && capturedLaunch != launchId && capturedLaunch != PublicLaunchIdentity(id, launchId))
                {
                    continue;
                }
                JsonElement saveData = data.GetProperty("Save");
                string saveFile = Path.Combine(Path.GetDirectoryName(file)!, "state.json");
                byte[]? saveBytes = null;
                if (saveData.ValueKind != JsonValueKind.Null)
                {
                    if ((archive?.GetEntry("repro/state.ff8save") ?? archive?.GetEntry("repro/state.json")) is { } entry)
                    {
                        using Stream source = entry.Open();
                        using MemoryStream target = new();
                        source.CopyTo(target);
                        saveBytes = target.ToArray();
                    }
                    else if (archive is null && File.Exists(saveFile))
                    {
                        saveBytes = File.ReadAllBytes(saveFile);
                    }
                }
                ReportSave? save = saveBytes is null ? null : new(saveBytes, saveData.GetProperty("Kind").GetString()!, saveData.GetProperty("CapturedUtc").GetDateTimeOffset(), saveData.GetProperty("Validation").GetString()!);
                return new(id, capturedLaunch, data.GetProperty("Timestamp").GetDateTimeOffset(), data.GetProperty("Subsystem").GetString()!, data.GetProperty("Exception").Deserialize<ErrorException>()!, data.GetProperty("Runtime").Clone(), data.GetProperty("Environment").Clone(), save, data.GetProperty("SaveUnavailableReason").GetString()!, data.GetProperty("Events").Deserialize<ErrorBreadcrumb[]>()!, data.GetProperty("CollectionErrors").Deserialize<string[]>()!, data.GetProperty("LogPaths").Deserialize<string[]>()!);
            }
            catch (Exception failure) when (failure is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or InvalidOperationException or KeyNotFoundException or ArgumentException)
            {
                // Incomplete or damaged evidence must not hide another complete incident.
            }
        }
        return null;
    }

    internal static void AtomicWrite(string path, byte[] bytes)
    {
        string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllBytes(temporary, bytes);
            File.Move(temporary, path, true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }
}
