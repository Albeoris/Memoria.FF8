using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Memoria.FF8.LinuxTesting;

public static class Deployment
{
    private const string OwnerFile = ".memoria-linux-test-owner";
    private const string Owner = "memoria-linux-test-v1";

    public static Dictionary<string, string> Manifest(string root)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        CheckTree(root);
        if (!Directory.Exists(root))
        {
            return result;
        }
        foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            result.Add(Path.GetRelativePath(root, file).Replace('\\', '/'), Hash(file));
        }
        return result;
    }

    public static void Initialize(string root)
    {
        root = Path.GetFullPath(root);
        CheckLink(root);
        string marker = Path.Combine(root, OwnerFile);
        CheckLink(marker);
        if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any() && !File.Exists(marker))
        {
            throw new InvalidDataException("Existing nonempty staging directory has no ownership marker; select a fresh directory.");
        }
        if (File.Exists(marker) && File.ReadAllText(marker) != Owner)
        {
            throw new InvalidDataException("Staging ownership marker is unrecognized.");
        }
        foreach (string name in new[] { "Memoria.FF8.LinuxTesting", "target.py", "request.json", "sync.json", "payload-manifest.json", "payload", "state", "runs", "test-results", "deployment.json", "deployment-staging", "deployment-backup", "inbox" })
        {
            CheckTree(Path.Combine(root, name));
        }
        Directory.CreateDirectory(root);
        if (!File.Exists(marker))
        {
            string temporary = marker + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, Owner);
                File.Move(temporary, marker);
            }
            finally
            {
                File.Delete(temporary);
            }
        }
    }

    public static Dictionary<string, string> Prepare(string root, Dictionary<string, string> incoming)
    {
        Validate(incoming);
        Initialize(root);
        Recover(root);
        string payload = Path.Combine(root, "payload");
        string journalPath = Path.Combine(root, "deployment.json");
        string stage = Path.Combine(root, "deployment-staging");
        Dictionary<string, string> previous = ReadReceipt(root);
        Dictionary<string, string> actual = Manifest(payload);
        RequireSubset(actual, previous, "Staging contains an unknown or edited file; all files are preserved.");
        Directory.CreateDirectory(payload);
        if (File.Exists(journalPath))
        {
            Journal journal = ReadJournal(journalPath);
            if (!Equal(journal.Incoming, incoming))
            {
                RequireSubset(Manifest(stage), journal.Incoming, "Pending deployment staging was edited; all files are preserved.");
                string abandoned = Path.Combine(root, "deployment-abandoned-" + Guid.NewGuid().ToString("N"));
                if (Directory.Exists(stage))
                {
                    Directory.Move(stage, abandoned);
                }
                File.Move(journalPath, abandoned + ".json");
            }
        }
        if (!File.Exists(journalPath))
        {
            if (Directory.Exists(stage) || Directory.Exists(Path.Combine(root, "deployment-backup")))
            {
                throw new InvalidDataException("Unowned deployment staging/backup exists; all files are preserved.");
            }
            WriteJson(journalPath, new Journal(actual, incoming));
        }
        Dictionary<string, string> staged = Manifest(stage);
        RequireSubset(staged, incoming, "Pending deployment staging was edited; all files are preserved.");
        Directory.CreateDirectory(stage);
        foreach (var item in incoming)
        {
            if (!staged.ContainsKey(item.Key) && actual.TryGetValue(item.Key, out string? digest) && digest == item.Value)
            {
                string destination = Destination(stage, item.Key);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(Destination(payload, item.Key), destination);
            }
        }
        return Manifest(stage);
    }

    public static void Accept(string root, Dictionary<string, string> incoming, string archive)
    {
        Prepare(root, incoming);
        string inbox = Path.GetFullPath(Path.Combine(root, "inbox"));
        archive = Path.GetFullPath(archive);
        CheckLink(archive);
        if (!string.Equals(Path.GetDirectoryName(archive), inbox, PathComparison) || !File.Exists(archive))
        {
            throw new InvalidDataException("Deployment archives must be regular files inside the owned inbox.");
        }
        string temporary = Path.Combine(inbox, "verified-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            long total = 0;
            using (ZipArchive zip = ZipFile.OpenRead(archive))
            {
                foreach (ZipArchiveEntry entry in zip.Entries)
                {
                    string name = entry.FullName;
                    if (!names.Add(name) || !incoming.ContainsKey(name) || entry.Name.Length == 0 || ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000)
                    {
                        throw new InvalidDataException("Unexpected deployment archive entry.");
                    }
                    total = checked(total + entry.Length);
                    if (entry.Length > 1024L * 1024 * 1024 || total > 4L * 1024 * 1024 * 1024)
                    {
                        throw new InvalidDataException("Deployment archive exceeds size limits.");
                    }
                    string output = Destination(temporary, name);
                    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                    entry.ExtractToFile(output);
                    if (Hash(output) != incoming[name])
                    {
                        throw new InvalidDataException("Deployment archive content hash mismatch.");
                    }
                }
            }
            foreach (string name in names)
            {
                string destination = Destination(Path.Combine(root, "deployment-staging"), name);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Move(Destination(temporary, name), destination, true);
            }
            File.Delete(archive);
        }
        finally
        {
            Directory.Delete(temporary, true);
        }
    }

    public static void Commit(string root, Dictionary<string, string> incoming)
    {
        Validate(incoming);
        Initialize(root);
        Recover(root);
        string journalPath = Path.Combine(root, "deployment.json");
        string payload = Path.Combine(root, "payload");
        string stage = Path.Combine(root, "deployment-staging");
        if (!File.Exists(journalPath))
        {
            if (Equal(Manifest(payload), incoming) && Equal(ReadReceipt(root), incoming))
            {
                return;
            }
            throw new InvalidDataException("No pending deployment matches the requested commit.");
        }
        Journal journal = ReadJournal(journalPath);
        if (!Equal(journal.Incoming, incoming) || !Equal(Manifest(stage), incoming))
        {
            throw new InvalidDataException("Deployment is incomplete or its content hashes do not match; active payload preserved.");
        }
        if (!Equal(Manifest(payload), journal.Previous))
        {
            throw new InvalidDataException("Active payload was edited before deployment; all files are preserved.");
        }
        Directory.Move(payload, Path.Combine(root, "deployment-backup"));
        Directory.Move(stage, payload);
        Recover(root);
    }

    public static void Recover(string root)
    {
        Initialize(root);
        string journalPath = Path.Combine(root, "deployment.json");
        if (!File.Exists(journalPath))
        {
            return;
        }
        Journal journal = ReadJournal(journalPath);
        string payload = Path.Combine(root, "payload");
        string stage = Path.Combine(root, "deployment-staging");
        string backup = Path.Combine(root, "deployment-backup");
        Dictionary<string, string> actual = Manifest(payload);
        if (Directory.Exists(backup))
        {
            if (!Equal(Manifest(backup), journal.Previous))
            {
                throw new InvalidDataException("Deployment backup was edited; all files are preserved.");
            }
            if (!Directory.Exists(payload))
            {
                if (!Directory.Exists(stage) || !Equal(Manifest(stage), journal.Incoming))
                {
                    Directory.Move(backup, payload);
                    throw new InvalidDataException("Interrupted deployment staging is incomplete; original payload restored.");
                }
                Directory.Move(stage, payload);
                actual = journal.Incoming;
            }
            if (!Equal(actual, journal.Incoming))
            {
                throw new InvalidDataException("Interrupted deployment payload was edited; all files are preserved.");
            }
            WriteJson(Path.Combine(root, "payload-manifest.json"), journal.Incoming);
            string retired = Path.Combine(root, "deployment-retired-" + Guid.NewGuid().ToString("N"));
            Directory.Move(backup, retired);
            File.Delete(journalPath);
            Directory.Delete(retired, true);
        }
        else if (Equal(actual, journal.Incoming) && !Directory.Exists(stage))
        {
            WriteJson(Path.Combine(root, "payload-manifest.json"), journal.Incoming);
            File.Delete(journalPath);
        }
        else if (!Equal(actual, journal.Previous))
        {
            throw new InvalidDataException("Pending deployment contains an edited active payload; all files are preserved.");
        }
    }

    public static void Sync(string root, Dictionary<string, string> incoming, string source)
    {
        Dictionary<string, string> staged = Prepare(root, incoming);
        CheckTree(source);
        string inbox = Path.Combine(root, "inbox");
        Directory.CreateDirectory(inbox);
        string temporary = Path.Combine(inbox, "copy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            foreach (var item in incoming)
            {
                if (staged.TryGetValue(item.Key, out string? digest) && digest == item.Value)
                {
                    continue;
                }
                string copied = Path.Combine(temporary, "file");
                File.Copy(Destination(source, item.Key), copied, true);
                if (Hash(copied) != item.Value)
                {
                    throw new InvalidDataException("Source changed during deployment; active payload preserved.");
                }
                string destination = Destination(Path.Combine(root, "deployment-staging"), item.Key);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Move(copied, destination, true);
            }
            Commit(root, incoming);
        }
        finally
        {
            Directory.Delete(temporary, true);
        }
    }

    private static StringComparison PathComparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static string Destination(string root, string name) => Path.Combine(root, name.Replace('/', Path.DirectorySeparatorChar));

    private static string Hash(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static void Validate(Dictionary<string, string> manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var paths = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        foreach (var item in manifest)
        {
            if (string.IsNullOrEmpty(item.Key) || item.Key.Contains('\\') || item.Key.Contains(':') || item.Key.Contains('\0') || item.Key.Split('/').Any(part => part is "" or "." or "..") || item.Value is null || item.Value.Length != 64 || item.Value.Any(character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')) || !paths.Add(item.Key))
            {
                throw new InvalidDataException("Unsafe payload manifest path or digest: " + item.Key);
            }
        }
        foreach (string path in paths)
        {
            int separator = path.IndexOf('/');
            while (separator >= 0)
            {
                if (paths.Contains(path[..separator]))
                {
                    throw new InvalidDataException("Payload manifest file and directory paths conflict.");
                }
                separator = path.IndexOf('/', separator + 1);
            }
        }
    }

    private static void CheckLink(string path)
    {
        if ((File.Exists(path) || Directory.Exists(path)) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0 || new FileInfo(path).LinkTarget is not null)
        {
            throw new InvalidDataException("Staging paths cannot contain symlinks or reparse points.");
        }
    }

    private static void CheckTree(string root)
    {
        CheckLink(root);
        if (!Directory.Exists(root))
        {
            return;
        }
        foreach (string path in Directory.EnumerateFileSystemEntries(root))
        {
            CheckLink(path);
            if (Directory.Exists(path))
            {
                CheckTree(path);
            }
        }
    }

    private static bool Equal(Dictionary<string, string> left, Dictionary<string, string> right) => left.Count == right.Count && left.All(item => right.TryGetValue(item.Key, out string? value) && value == item.Value);

    private static void RequireSubset(Dictionary<string, string> actual, Dictionary<string, string> expected, string message)
    {
        if (actual.Any(item => !expected.TryGetValue(item.Key, out string? digest) || digest != item.Value))
        {
            throw new InvalidDataException(message);
        }
    }

    private static Dictionary<string, string> ReadReceipt(string root)
    {
        string path = Path.Combine(root, "payload-manifest.json");
        Dictionary<string, string> manifest = File.Exists(path) ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ?? throw new InvalidDataException("Invalid payload receipt.") : new(StringComparer.Ordinal);
        Validate(manifest);
        return manifest;
    }

    private static Journal ReadJournal(string path)
    {
        Journal journal = JsonSerializer.Deserialize<Journal>(File.ReadAllText(path)) ?? throw new InvalidDataException("Invalid deployment journal.");
        Validate(journal.Previous);
        Validate(journal.Incoming);
        return journal;
    }

    private static void WriteJson<T>(string path, T value)
    {
        CheckLink(path);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(value));
            File.Move(temporary, path, true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    private sealed record Journal([property: JsonPropertyName("previous")] Dictionary<string, string> Previous, [property: JsonPropertyName("incoming")] Dictionary<string, string> Incoming);
}
