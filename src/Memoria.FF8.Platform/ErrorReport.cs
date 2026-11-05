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
                stream.Dispose();
            }
        }
        preparations.TryRemove(Path.GetDirectoryName(archivePath)!, out _);
    }

    private static PreparedErrorReport Prepare(ErrorIncident incident, string root)
    {
        string pendingDirectory = incident.Persist(root);
        string directory = Path.Combine(root, incident.Id);
        Directory.CreateDirectory(directory);
        string name = $"Memoria-FF8-report-{incident.Timestamp:yyyyMMdd-HHmmss}-{incident.Id[..8]}.zip";
        string archivePath = Path.Combine(directory, name);
        lock (retentionLock)
        {
            protectedPaths.Add(archivePath);
        }
        string temporary = archivePath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            string evidenceSignature = EvidenceSignature(incident);
            List<string> omissions = new(incident.CollectionErrors);
            if (!string.IsNullOrWhiteSpace(incident.SaveUnavailableReason))
            {
                omissions.Add(incident.SaveUnavailableReason);
            }
            if (!string.IsNullOrWhiteSpace(incident.SaveUnavailableReason))
            {
                omissions.Add(incident.SaveUnavailableReason);
            }
            Dictionary<string, byte[]> files = new()
            {
                ["exception.txt"] = Encoding.UTF8.GetBytes(ReportPrivacy.Text(incident.ExceptionText)),
                ["runtime.json"] = Encoding.UTF8.GetBytes(ReportPrivacy.Json(incident.Runtime).GetRawText()),
                ["environment.json"] = Encoding.UTF8.GetBytes(ReportPrivacy.Json(incident.Environment).GetRawText()),
                ["events.jsonl"] = Encoding.UTF8.GetBytes(string.Join('\n', incident.Events.Select(entry => ReportPrivacy.Json(JsonSerializer.SerializeToElement(entry)).GetRawText())))
            };
            if (incident.LogPaths.Count > 0)
            {
                omissions.Add($"Excluded {incident.LogPaths.Count} raw log file(s): arbitrary logs may contain account identity, paths, credentials and other private data.");
            }
            if (incident.Save is not null)
            {
                omissions.Add("Automatic reproduction save excluded: complete snapshots may contain account identity, player names and opaque native buffers that cannot be safely sanitized.");
            }
            string description;
            while (true)
            {
                string saveStatus = incident.Save is null ? "Unavailable: " + incident.SaveUnavailableReason : "Excluded from ZIP; see collection omissions.";
                description = ReportPrivacy.Text(BuildDescription(incident, name, saveStatus, omissions));
                files["incident.json"] = incident.EvidenceBytes(false, published: true);
                files["completed.json"] = JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 2, privacyPolicyVersion = 1, incidentId = incident.Id, archive = name, issueUrl = BuildIssueUrl(incident, name, false), evidenceSignature });
                files["report.md"] = Encoding.UTF8.GetBytes(description);
                files["manifest.json"] = ReportPrivacy.JsonBytes(JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 1, incidentId = incident.Id, LaunchId = incident.PublicLaunchId, incident.Timestamp, Subsystem = ReportPrivacy.Text(incident.Subsystem), save = ReportPrivacy.Text(saveStatus), omissions = omissions.Select(ReportPrivacy.Text).ToArray(), files = files.Where(file => file.Key != "manifest.json").Select(file => new { name = file.Key, bytes = file.Value.LongLength }).ToArray() }, ErrorIncident.JsonOptions));
                if (File.Exists(archivePath) && ArchiveMatches(archivePath, files))
                {
                    AcquireArchiveLease(archivePath);
                    RemoveStagedEvidence(pendingDirectory, directory, archivePath);
                    return new(incident.Id, archivePath, description, BuildIssueUrl(incident, name, false));
                }
                using (FileStream stream = new(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                using (ZipArchive zip = new(stream, ZipArchiveMode.Create))
                {
                    foreach ((string entry, byte[] data) in files)
                    {
                        ZipArchiveEntry archived = zip.CreateEntry(entry, CompressionLevel.Optimal);
                        archived.LastWriteTime = incident.Timestamp.ToUniversalTime();
                        using Stream target = archived.Open();
                        target.Write(data);
                    }
                }
                if (new FileInfo(temporary).Length <= MaximumArchiveBytes)
                {
                    break;
                }
                if (files.Remove("events.jsonl"))
                {
                    omissions.Add("Breadcrumbs excluded to fit package budget.");
                }
                else
                {
                    throw new IOException("Required exception and context exceed the 20 MiB package budget. Pending evidence remains available for recovery.");
                }
            }
            File.Move(temporary, archivePath, true);
            AcquireArchiveLease(archivePath);
            string url = BuildIssueUrl(incident, name, false);
            RemoveStagedEvidence(pendingDirectory, directory, archivePath);
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
        return $"## Error\n{incident.Exception.Type}: {incident.Exception.Message}\n\nSubsystem: {incident.Subsystem}\nIncident: {incident.Id}\nUTC: {incident.Timestamp:O}\nLaunch: {incident.PublicLaunchId}\n\n## Environment\n```json\n{ReportPrivacy.Json(incident.Environment).GetRawText()}\n```\n\n## Runtime\n```json\n{ReportPrivacy.Json(incident.Runtime).GetRawText()}\n```\n\n## Reproduction material\nSave: {saveStatus}\nAttach: {name}\n{PublicInstructions}\n\n## Complete available exception\n```text\n{incident.Exception}\n```\n\n## Collection omissions\n{(omissions.Count == 0 ? "None." : string.Join('\n', omissions))}\n\n## What I was doing\nOptional: add anything you remember.\n";
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
            string title = "[Error] " + Shorten(ReportPrivacy.Text(incident.Exception.Message), Math.Max(20, budget / 3));
            string body = $"## Error\n{Shorten(ReportPrivacy.Text(incident.Exception.Type), Math.Max(30, budget))}: {Shorten(ReportPrivacy.Text(incident.Exception.Message), budget)}\nSubsystem: {Shorten(ReportPrivacy.Text(incident.Subsystem), budget)}\n\nEnvironment: {Shorten(ReportPrivacy.Json(incident.Environment).GetRawText(), budget)}\nContext: {Shorten(ReportPrivacy.Json(incident.Runtime).GetRawText(), budget)}\n\nReport: {incident.Id}\nAttach: {ReportPrivacy.Text(archiveName)}\nSave: {(saveIncluded ? "Save included; see report.md for capture age and limitations" : "Unavailable or excluded; see report.md")}\n\n{Shorten(ReportPrivacy.Text(incident.Exception.Stack ?? "Stack unavailable"), budget)}\n\nDrag the ZIP into this issue before submitting. Reports and attachments are public. Full details are in report.md inside the ZIP.\n";
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

    private static bool ArchiveMatches(string path, Dictionary<string, byte[]> files)
    {
        try
        {
            using ZipArchive archive = ZipFile.OpenRead(path);
            if (archive.Entries.Count != files.Count)
            {
                return false;
            }
            foreach ((string name, byte[] bytes) in files)
            {
                ZipArchiveEntry? entry = archive.GetEntry(name);
                if (entry is null || entry.Length != bytes.LongLength)
                {
                    return false;
                }
                using Stream source = entry.Open();
                byte[] existing = new byte[bytes.Length];
                source.ReadExactly(existing);
                if (!existing.AsSpan().SequenceEqual(bytes))
                {
                    return false;
                }
            }
            return true;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    private static void AcquireArchiveLease(string archivePath)
    {
        lock (retentionLock)
        {
            if (!leaseFiles.ContainsKey(archivePath))
            {
                leaseFiles[archivePath] = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            }
        }
    }

    private static void RemoveStagedEvidence(string pendingDirectory, string directory, string archivePath)
    {
        foreach (string file in Directory.EnumerateFiles(pendingDirectory))
        {
            if (Path.GetFileName(file) != "presented")
            {
                File.Delete(file);
            }
        }
        if (!Directory.EnumerateFileSystemEntries(pendingDirectory).Any())
        {
            Directory.Delete(pendingDirectory);
        }
        foreach (string file in Directory.EnumerateFiles(directory))
        {
            if (!string.Equals(file, archivePath, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(file);
            }
        }
    }

    private static void Cleanup(string root)
    {
        lock (retentionLock)
        {
            string[] completed = Directory.EnumerateFiles(root, "*.zip", SearchOption.AllDirectories).OrderByDescending(File.GetLastWriteTimeUtc).ToArray();
            foreach (string archive in completed.Skip(10))
            {
                if (protectedPaths.Contains(archive))
                {
                    continue;
                }
                try
                {
                    using (FileStream probe = new(archive, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                    }
                    string directory = Path.GetDirectoryName(archive)!;
                    Directory.Delete(directory, true);
                    string pendingDirectory = Path.Combine(ErrorIncident.PendingRoot(root), Path.GetFileName(directory));
                    if (Directory.Exists(pendingDirectory) && !File.Exists(Path.Combine(pendingDirectory, "incident.json")))
                    {
                        Directory.Delete(pendingDirectory, true);
                    }
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
