using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace Memoria.FF8.Platform;

public sealed class PreparedErrorReport : IDisposable
{
    public string IncidentId { get; }
    public string ArchivePath { get; }
    public string Description { get; }
    public string IssueUrl { get; }
    public long Size => new FileInfo(ArchivePath).Length;
    private int disposed;

    internal PreparedErrorReport(string incidentId, string archivePath, string description, string issueUrl)
    {
        IncidentId = incidentId;
        ArchivePath = archivePath;
        Description = description;
        IssueUrl = issueUrl;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0)
        {
            ErrorReport.Release(ArchivePath);
        }
    }
}

public static class ErrorReport
{
    public const long MaximumArchiveBytes = 20 * 1024 * 1024;
    public const string PublicInstructions = "Sign in to GitHub if needed, drag this ZIP into the issue, wait for the upload, then submit the issue. The report and attached files will be public.";
    private const string IssueBase = "https://github.com/Albeoris/Memoria.FF8/issues/new?title=";
    private static readonly ConcurrentDictionary<string, Lazy<Task<PreparedErrorReport>>> preparations = new();
    private static readonly object retentionLock = new();
    private static readonly HashSet<string> protectedPaths = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, int> leases = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, FileStream> leaseFiles = new(StringComparer.OrdinalIgnoreCase);

    public static Task<PreparedErrorReport> PrepareAsync(ErrorIncident incident, string? reportsDirectory = null)
    {
        string root = Path.GetFullPath(reportsDirectory ?? ApplicationPaths.Current.Reports);
        string key = Path.Combine(root, incident.Id);
        Lazy<Task<PreparedErrorReport>> pending = preparations.GetOrAdd(key, _ => new(() => Task.Run(() => Prepare(incident, root))));
        return AwaitPreparation(key, pending);
    }

    private static async Task<PreparedErrorReport> AwaitPreparation(string key, Lazy<Task<PreparedErrorReport>> pending)
    {
        try
        {
            PreparedErrorReport report = await pending.Value.ConfigureAwait(false);
            lock (retentionLock)
            {
                leases.TryGetValue(report.ArchivePath, out int count);
                leases[report.ArchivePath] = count + 1;
                protectedPaths.Add(report.ArchivePath);
            }
            return new(report.IncidentId, report.ArchivePath, report.Description, report.IssueUrl);
        }
        catch
        {
            preparations.TryRemove(new KeyValuePair<string, Lazy<Task<PreparedErrorReport>>>(key, pending));
            throw;
        }
    }

    internal static void Release(string archivePath)
    {
        lock (retentionLock)
        {
            leases.TryGetValue(archivePath, out int count);
            if (count > 1)
            {
                leases[archivePath] = count - 1;
                return;
            }
            leases.Remove(archivePath);
            protectedPaths.Remove(archivePath);
            if (leaseFiles.Remove(archivePath, out FileStream? stream))
            {
                string leasePath = stream.Name;
                stream.Dispose();
                File.Delete(leasePath);
            }
        }
        preparations.TryRemove(Path.GetDirectoryName(archivePath)!, out _);
    }

    private static PreparedErrorReport Prepare(ErrorIncident incident, string root)
    {
        string directory = incident.Persist(root);
        string name = $"Memoria-FF8-report-{incident.Timestamp:yyyyMMdd-HHmmss}-{incident.Id[..8]}.zip";
        string archivePath = Path.Combine(directory, name);
        lock (retentionLock)
        {
            protectedPaths.Add(archivePath);
            string leasePath = Path.Combine(directory, ".lease-" + Guid.NewGuid().ToString("N"));
            leaseFiles[archivePath] = new FileStream(leasePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        }
        string temporary = archivePath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            string evidenceSignature = EvidenceSignature(incident);
            string completedPath = Path.Combine(directory, "completed.json");
            if (File.Exists(completedPath) && File.Exists(archivePath) && File.Exists(Path.Combine(directory, "report.md")))
            {
                using JsonDocument completed = JsonDocument.Parse(File.ReadAllBytes(completedPath));
                if (completed.RootElement.TryGetProperty("evidenceSignature", out JsonElement signature) && signature.GetString() == evidenceSignature)
                {
                    return new(incident.Id, archivePath, File.ReadAllText(Path.Combine(directory, "report.md")), completed.RootElement.GetProperty("issueUrl").GetString()!);
                }
            }
            List<string> omissions = new(incident.CollectionErrors);
            Dictionary<string, byte[]> files = new()
            {
                ["exception.txt"] = Encoding.UTF8.GetBytes(incident.ExceptionText),
                ["runtime.json"] = Encoding.UTF8.GetBytes(incident.Runtime.GetRawText()),
                ["environment.json"] = Encoding.UTF8.GetBytes(incident.Environment.GetRawText()),
                ["events.jsonl"] = Encoding.UTF8.GetBytes(string.Join('\n', incident.Events.Select(entry => JsonSerializer.Serialize(entry))))
            };
            int index = 0;
            if (incident.LogPaths.Count > 8)
            {
                omissions.Add($"Omitted {incident.LogPaths.Count - 8} older log files; retained the latest eight provided logs.");
            }
            foreach (string log in incident.LogPaths.TakeLast(8))
            {
                try
                {
                    using FileStream stream = new(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    const int maximumLogBytes = 1024 * 1024;
                    long skipped = Math.Max(0, stream.Length - maximumLogBytes);
                    stream.Position = skipped;
                    byte[] content = new byte[(int)Math.Min(maximumLogBytes, stream.Length)];
                    int read = stream.ReadAtLeast(content, content.Length, false);
                    files[$"logs/{index++:D2}-{Path.GetFileName(log)}"] = content[..read];
                    if (skipped > 0)
                    {
                        omissions.Add($"Log {Path.GetFileName(log)}: omitted {skipped} older bytes.");
                    }
                }
                catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
                {
                    omissions.Add($"Log {Path.GetFileName(log)} unavailable: {failure.Message}");
                }
            }
            bool includeSave = incident.Save is not null;
            if (includeSave)
            {
                files["repro/state.json"] = incident.Save!.CopyBytes();
            }
            string description;
            while (true)
            {
                string saveStatus = includeSave ? $"{incident.Save!.Kind}; captured {incident.Save.CapturedUtc:O}; age at failure {incident.Timestamp - incident.Save.CapturedUtc}; validation: {incident.Save.Validation}" : incident.Save is null ? "Unavailable: " + incident.SaveUnavailableReason : "Excluded from ZIP to fit the package budget; validated full save retained locally as state.json.";
                description = BuildDescription(incident, name, saveStatus, omissions);
                files["report.md"] = Encoding.UTF8.GetBytes(description);
                files["manifest.json"] = JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 1, incidentId = incident.Id, incident.LaunchId, incident.Timestamp, incident.Subsystem, save = saveStatus, omissions, files = files.Where(file => file.Key != "manifest.json").Select(file => new { name = file.Key, bytes = file.Value.LongLength }).ToArray() }, ErrorIncident.JsonOptions);
                using (FileStream stream = new(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                using (ZipArchive zip = new(stream, ZipArchiveMode.Create))
                {
                    foreach ((string entry, byte[] data) in files)
                    {
                        using Stream target = zip.CreateEntry(entry, CompressionLevel.Optimal).Open();
                        target.Write(data);
                    }
                }
                if (new FileInfo(temporary).Length <= MaximumArchiveBytes)
                {
                    break;
                }
                string? removable = files.Keys.FirstOrDefault(key => key.StartsWith("logs/", StringComparison.Ordinal));
                if (removable is not null)
                {
                    files.Remove(removable);
                    omissions.Add(removable + " excluded to fit package budget.");
                }
                else if (files.Remove("events.jsonl"))
                {
                    omissions.Add("Breadcrumbs excluded to fit package budget.");
                }
                else if (includeSave)
                {
                    includeSave = false;
                    files.Remove("repro/state.json");
                    omissions.Add("Validated reproduction save excluded to fit package budget; full artifact retained locally.");
                }
                else
                {
                    throw new IOException("Required exception and context exceed the 20 MiB package budget. Full captured evidence remains available locally.");
                }
            }
            File.Move(temporary, archivePath, true);
            ErrorIncident.AtomicWrite(Path.Combine(directory, "report.md"), Encoding.UTF8.GetBytes(description));
            string url = BuildIssueUrl(incident, name, includeSave);
            ErrorIncident.AtomicWrite(Path.Combine(directory, "completed.json"), JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 1, incidentId = incident.Id, archive = name, issueUrl = url, evidenceSignature }));
            Cleanup(root);
            return new(incident.Id, archivePath, description, url);
        }
        catch
        {
            Release(archivePath);
            throw;
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    private static string BuildDescription(ErrorIncident incident, string name, string saveStatus, List<string> omissions)
    {
        return $"## Error\n{incident.Exception.Type}: {incident.Exception.Message}\n\nSubsystem: {incident.Subsystem}\nIncident: {incident.Id}\nUTC: {incident.Timestamp:O}\nLaunch: {incident.LaunchId}\n\n## Environment\n```json\n{incident.Environment.GetRawText()}\n```\n\n## Runtime\n```json\n{incident.Runtime.GetRawText()}\n```\n\n## Reproduction material\nSave: {saveStatus}\nAttach: {name}\n{PublicInstructions}\n\n## Complete available exception\n```text\n{incident.Exception}\n```\n\n## Collection omissions\n{(omissions.Count == 0 ? "None." : string.Join('\n', omissions))}\n\n## What I was doing\nOptional: add anything you remember.\n";
    }

    private static string EvidenceSignature(ErrorIncident incident)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new { incident.Id, incident.Runtime, incident.Environment, incident.SaveUnavailableReason, logs = incident.LogPaths, save = incident.Save is null ? null : Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(incident.Save.CopyBytes())) });
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
    }

    public static string BuildIssueUrl(ErrorIncident incident, string archiveName, bool saveIncluded)
    {
        for (int budget = 240; budget >= 0; budget /= 2)
        {
            string title = "[Error] " + Shorten(incident.Exception.Message, Math.Max(20, budget / 3));
            string body = $"## Error\n{Shorten(incident.Exception.Type, Math.Max(30, budget))}: {Shorten(incident.Exception.Message, budget)}\nSubsystem: {Shorten(incident.Subsystem, budget)}\n\nEnvironment: {Shorten(incident.Environment.GetRawText(), budget)}\nContext: {Shorten(incident.Runtime.GetRawText(), budget)}\n\nReport: {incident.Id}\nAttach: {archiveName}\nSave: {(saveIncluded ? "Validated save included" : "Unavailable or excluded; see report.md")}\n\n{Shorten(incident.Exception.Stack ?? "Stack unavailable", budget)}\n\nDrag the ZIP into this issue before submitting. Reports and attachments are public. Full details are in report.md inside the ZIP.\n";
            string url = IssueBase + Uri.EscapeDataString(title) + "&body=" + Uri.EscapeDataString(body);
            if (url.Length <= 2000)
            {
                return url;
            }
            if (budget == 0)
            {
                return IssueBase + Uri.EscapeDataString("Error report " + incident.Id) + "&body=" + Uri.EscapeDataString($"Incident: {incident.Id}\nPlease attach the ZIP. Reports and attachments are public.");
            }
        }
        throw new InvalidOperationException();
    }

    private static string Shorten(string text, int length)
    {
        return string.Concat(text.EnumerateRunes().Take(length).Select(rune => rune.ToString()));
    }

    private static void Cleanup(string root)
    {
        lock (retentionLock)
        {
            string[] completed = Directory.EnumerateFiles(root, "completed.json", SearchOption.AllDirectories).OrderByDescending(File.GetLastWriteTimeUtc).ToArray();
            foreach (string marker in completed.Skip(10))
            {
                string directory = Path.GetDirectoryName(marker)!;
                if (protectedPaths.Any(path => string.Equals(Path.GetDirectoryName(path), directory, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }
                try
                {
                    bool active = false;
                    foreach (string leaseFile in Directory.EnumerateFiles(directory, ".lease-*"))
                    {
                        try
                        {
                            using FileStream probe = new(leaseFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                        }
                        catch (IOException)
                        {
                            active = true;
                            break;
                        }
                    }
                    if (active)
                    {
                        continue;
                    }
                    Directory.Delete(directory, true);
                }
                catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
                {
                    // Housekeeping failure must not prevent access to the new report.
                }
            }
        }
    }
}

public static class ReportDesktop
{
    public static string? OpenUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || uri.Scheme != Uri.UriSchemeHttps || uri.Host != "github.com")
        {
            return "Only HTTPS GitHub issue URLs may be opened.";
        }
        return Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    public static string? RevealFile(string path)
    {
        string fullPath = Path.GetFullPath(path);
        ProcessStartInfo start = new() { UseShellExecute = false };
        if (OperatingSystem.IsWindows())
        {
            start.FileName = "explorer.exe";
            start.ArgumentList.Add("/select," + fullPath);
        }
        else if (OperatingSystem.IsMacOS())
        {
            start.FileName = "open";
            start.ArgumentList.Add("-R");
            start.ArgumentList.Add(fullPath);
        }
        else
        {
            start.FileName = "xdg-open";
            start.ArgumentList.Add(Path.GetDirectoryName(fullPath)!);
        }
        string? failure = Start(start);
        return failure is null ? null : Start(new ProcessStartInfo(Path.GetDirectoryName(fullPath)!) { UseShellExecute = true }) ?? failure;
    }

    private static string? Start(ProcessStartInfo start)
    {
        try
        {
            using Process? process = Process.Start(start);
            return process is null ? "The desktop did not start the requested application." : null;
        }
        catch (Exception failure) when (failure is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            return failure.Message;
        }
    }
}
