using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Memoria.FF8.Runtime;

/// <summary>Game-owned session archives. Portable saves carry their own immutable journal prefix.</summary>
public sealed class ReplayJournalArchive
{
    public static string DefaultDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Memoria.FF8", "replay-journals");
    private readonly string directory;

    public ReplayJournalArchive(string directory)
    {
        this.directory = Path.GetFullPath(directory);
    }

    public string GetPath(string sessionId)
    {
        if (!Guid.TryParseExact(sessionId, "N", out _))
        {
            throw new InvalidDataException("Replay session identity must be a compact GUID.");
        }
        return Path.Combine(directory, sessionId + ".journal.json");
    }

    public void Persist(ReplayJournal journal)
    {
        ArgumentNullException.ThrowIfNull(journal);
        WriteAtomic(GetPath(journal.SessionId), JsonSerializer.Serialize(journal));
    }

    public void Pin(string savePath, ReplayJournal journal)
    {
        Persist(journal);
        string capsule = JsonSerializer.Serialize(journal);
        string capsuleId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(capsule)));
        // Immutable prefixes keep branches and earlier checkpoint references independently recoverable.
        WriteAtomic(Path.Combine(directory, journal.SessionId, capsuleId + ".journal.json"), capsule);
        string fullPath = Path.GetFullPath(savePath);
        string identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fullPath.ToUpperInvariant() + "\n" + journal.SessionId)));
        WriteAtomic(Path.Combine(directory, "pins", identity + ".json"), JsonSerializer.Serialize(new SavePin(fullPath, journal.SessionId, capsuleId)));
    }

    public void RegisterSaveDirectory(string saveDirectory)
    {
        string fullPath = Path.GetFullPath(saveDirectory);
        string identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fullPath.ToUpperInvariant())));
        WriteAtomic(Path.Combine(directory, "save-roots", identity + ".json"), JsonSerializer.Serialize(fullPath));
    }

    /// <summary>Retains every referenced archive, all recent archives, and the newest unreferenced sessions.</summary>
    public int Prune(DateTimeOffset now, TimeSpan? retention = null, int keepUnreferenced = 16)
    {
        TimeSpan age = retention ?? TimeSpan.FromDays(14);
        if (age < TimeSpan.Zero || keepUnreferenced < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(retention));
        }
        if (!Directory.Exists(directory))
        {
            return 0;
        }
        HashSet<string> protectedSessions = new(StringComparer.Ordinal);
        HashSet<string> protectedPrefixes = new(StringComparer.Ordinal);
        HashSet<string> uncertainPrefixSessions = new(StringComparer.Ordinal);
        try
        {
            string pins = Path.Combine(directory, "pins");
            if (Directory.Exists(pins))
            {
                foreach (string path in Directory.EnumerateFiles(pins, "*.json"))
                {
                    SavePin pin = JsonSerializer.Deserialize<SavePin>(File.ReadAllText(path)) ?? throw new InvalidDataException("Empty replay pin.");
                    if (string.IsNullOrWhiteSpace(pin.SavePath) || !Path.IsPathFullyQualified(pin.SavePath) || !Guid.TryParseExact(pin.SessionId, "N", out _))
                    {
                        throw new InvalidDataException("Malformed replay pin.");
                    }
                    if (File.Exists(pin.SavePath))
                    {
                        using JsonDocument save = JsonDocument.Parse(File.ReadAllText(pin.SavePath));
                        string? referenced = save.RootElement.TryGetProperty("ReplayJournal", out JsonElement journal) && journal.ValueKind == JsonValueKind.Object ? journal.GetProperty("SessionId").GetString() : null;
                        if (referenced == pin.SessionId)
                        {
                            protectedSessions.Add(pin.SessionId);
                            ReplayJournal savedPrefix = journal.Deserialize<ReplayJournal>() ?? throw new InvalidDataException("Empty pinned replay prefix.");
                            string savedCapsule = JsonSerializer.Serialize(savedPrefix);
                            protectedPrefixes.Add(pin.SessionId + "/" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(savedCapsule))));
                            if (pin.CapsuleId is null)
                            {
                                uncertainPrefixSessions.Add(pin.SessionId);
                            }
                            else if (pin.CapsuleId.Length != 64 || pin.CapsuleId.Any(character => !Uri.IsHexDigit(character)))
                            {
                                throw new InvalidDataException("Malformed replay prefix pin.");
                            }
                            else
                            {
                                protectedPrefixes.Add(pin.SessionId + "/" + pin.CapsuleId);
                            }
                        }
                        else
                        {
                            File.Delete(path);
                        }
                    }
                }
            }
            string roots = Path.Combine(directory, "save-roots");
            if (Directory.Exists(roots))
            {
                foreach (string path in Directory.EnumerateFiles(roots, "*.json"))
                {
                    string root = JsonSerializer.Deserialize<string>(File.ReadAllText(path)) ?? throw new InvalidDataException("Empty save root.");
                    if (!Directory.Exists(root))
                    {
                        continue;
                    }
                    foreach (string save in Directory.EnumerateFiles(root, "*.json"))
                    {
                        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(save));
                        if (document.RootElement.TryGetProperty("ReplayJournal", out JsonElement value) && value.ValueKind != JsonValueKind.Null)
                        {
                            ReplayJournal journal = value.Deserialize<ReplayJournal>() ?? throw new InvalidDataException("Empty saved replay journal.");
                            protectedSessions.Add(journal.SessionId);
                            string capsule = JsonSerializer.Serialize(journal);
                            protectedPrefixes.Add(journal.SessionId + "/" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(capsule))));
                        }
                    }
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or NotSupportedException or KeyNotFoundException or InvalidOperationException or ArgumentException)
        {
            // Retention is optional; uncertain references always keep the archives.
            return 0;
        }
        FileInfo[] candidates = Directory.EnumerateFiles(directory, "*.journal.json").Select(path => new FileInfo(path)).Where(file => !protectedSessions.Contains(file.Name[..^".journal.json".Length])).OrderByDescending(file => file.LastWriteTimeUtc).ToArray();
        int deleted = 0;
        foreach (FileInfo file in candidates.Skip(keepUnreferenced))
        {
            if (now.UtcDateTime - file.LastWriteTimeUtc <= age)
            {
                continue;
            }
            file.Delete();
            string snapshots = Path.Combine(directory, file.Name[..^".journal.json".Length]);
            if (Directory.Exists(snapshots))
            {
                foreach (string snapshot in Directory.EnumerateFiles(snapshots, "*.journal.json"))
                {
                    File.Delete(snapshot);
                }
                if (!Directory.EnumerateFileSystemEntries(snapshots).Any())
                {
                    Directory.Delete(snapshots);
                }
            }
            deleted++;
        }
        foreach (string sessionId in protectedSessions.Where(id => !uncertainPrefixSessions.Contains(id)))
        {
            string snapshots = Path.Combine(directory, sessionId);
            if (!Directory.Exists(snapshots))
            {
                continue;
            }
            FileInfo[] unreferenced = Directory.EnumerateFiles(snapshots, "*.journal.json").Select(path => new FileInfo(path)).Where(file => !protectedPrefixes.Contains(sessionId + "/" + file.Name[..^".journal.json".Length])).OrderByDescending(file => file.LastWriteTimeUtc).ToArray();
            foreach (FileInfo file in unreferenced.Skip(keepUnreferenced))
            {
                if (now.UtcDateTime - file.LastWriteTimeUtc > age)
                {
                    file.Delete();
                    deleted++;
                }
            }
        }
        return deleted;
    }

    private static void WriteAtomic(string path, string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, contents);
            File.Move(temporary, path, true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private sealed record SavePin(string SavePath, string SessionId, string? CapsuleId = null);
}
