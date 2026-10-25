using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Memoria.FF8.BattleEffects;

public sealed class EvidenceStore
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private sealed record RecordEnvelope(int SchemaVersion, string Sha256, string? PayloadUtf8, string? PayloadBase64);
    public string Root { get; }

    public EvidenceStore(string root)
    {
        Root = PrivateEvidencePath.Validate(root);
        Directory.CreateDirectory(Root);
    }

    public static string Id(string value)
    {
        if (!Regex.IsMatch(value, "^[a-zA-Z0-9][a-zA-Z0-9._-]{0,119}$", RegexOptions.CultureInvariant) || value.EndsWith('.') || value.Contains("..", StringComparison.Ordinal) || Regex.IsMatch(value, "^(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])(?:\\.|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            throw new ArgumentException($"Unsafe record identifier: {value}");
        }

        return value;
    }

    public string Resolve(string relative)
    {
        if (Path.IsPathRooted(relative) || relative.Split('/', '\\').Any(part => part is ".." or "." or ""))
        {
            throw new ArgumentException("Evidence paths must be normalized relative paths.");
        }

        string path = Path.GetFullPath(Path.Combine(Root, relative));
        if (!path.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Evidence path escapes the store.");
        }

        for (string? current = path; current != null && current != Root; current = Path.GetDirectoryName(current))
        {
            if (Path.Exists(current) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new ArgumentException("Evidence paths must not traverse links or junctions.");
            }
        }

        return path;
    }

    public T Read<T>(string relative) where T : notnull => JsonSerializer.Deserialize<T>(ReadBytes(relative), RecordJson.Options) ?? throw new InvalidDataException($"Empty record: {relative}");

    public void WriteNew<T>(string relative, T record)
    {
        WriteBytesNew(relative, JsonSerializer.SerializeToUtf8Bytes(record, RecordJson.Options));
    }

    public byte[] ReadBytes(string relative)
    {
        string key = RecordKey(relative);
        RecordEnvelope envelope = JsonSerializer.Deserialize<RecordEnvelope>(File.ReadAllBytes(EnvelopePath(key)), RecordJson.Options) ?? throw new InvalidDataException($"Empty evidence envelope: {key}");
        if (envelope.SchemaVersion != 1 || (envelope.PayloadUtf8 is null) == (envelope.PayloadBase64 is null))
        {
            throw new InvalidDataException($"Invalid evidence envelope: {key}");
        }
        byte[] payload;
        try
        {
            payload = envelope.PayloadUtf8 is not null ? StrictUtf8.GetBytes(envelope.PayloadUtf8) : Convert.FromBase64String(envelope.PayloadBase64!);
        }
        catch (Exception exception) when (exception is EncoderFallbackException or FormatException)
        {
            throw new InvalidDataException($"Invalid evidence payload encoding: {key}", exception);
        }
        string actualHash = Convert.ToHexStringLower(SHA256.HashData(payload));
        if (!string.Equals(envelope.Sha256, actualHash, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Evidence record SHA256 mismatch: {key}");
        }
        return payload;
    }

    public void WriteBytesNew(string relative, byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        string key = RecordKey(relative);
        byte[] payload = bytes.ToArray();
        string hash = Convert.ToHexStringLower(SHA256.HashData(payload));
        string? text = null;
        string? binary = null;
        try
        {
            text = StrictUtf8.GetString(payload);
        }
        catch (DecoderFallbackException)
        {
            binary = Convert.ToBase64String(payload);
        }
        byte[] envelope = JsonSerializer.SerializeToUtf8Bytes(new RecordEnvelope(1, hash, text, binary), RecordJson.Options);
        string path = EnvelopePath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        EnvelopePath(key);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(envelope);
                stream.Flush(true);
            }
            EnvelopePath(key);
            File.Move(temporary, path, false);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    public bool Exists(string relative)
    {
        return File.Exists(EnvelopePath(RecordKey(relative)));
    }

    public IReadOnlyList<string> ListPaths(string prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        string normalized = prefix.Length == 0 ? "" : RecordKey(prefix.TrimEnd('/', '\\')) + (prefix.EndsWith('/') || prefix.EndsWith('\\') ? "/" : "");
        List<string> paths = [];
        string records = Resolve("records");
        if (!Directory.Exists(records))
        {
            return paths;
        }
        Stack<string> pending = new();
        pending.Push(records);
        const string suffix = ".record.json";
        while (pending.TryPop(out string? directory))
        {
            foreach (string path in Directory.EnumerateFileSystemEntries(directory))
            {
                string relative = Path.GetRelativePath(Root, path);
                Resolve(relative);
                if (Directory.Exists(path))
                {
                    pending.Push(path);
                }
                else if (path.EndsWith(suffix, StringComparison.Ordinal))
                {
                    string key = RecordKey(Path.GetRelativePath(records, path)[..^suffix.Length]);
                    if (key.StartsWith(normalized, StringComparison.Ordinal))
                    {
                        paths.Add(key);
                    }
                }
            }
        }
        return paths.Order(StringComparer.Ordinal).ToArray();
    }

    private static string RecordKey(string relative)
    {
        ArgumentNullException.ThrowIfNull(relative);
        string normalized = relative.Replace('\\', '/');
        foreach (string segment in normalized.Split('/'))
        {
            Id(segment);
        }
        return normalized;
    }

    private string EnvelopePath(string key)
    {
        return Resolve("records/" + key + ".record.json");
    }

    public static string HashFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    public static string HashRecord<T>(T record) => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(record, RecordJson.Options)));

    public static BuildFile[] DescribeFiles(string directory)
    {
        string root = Path.GetFullPath(directory);
        for (DirectoryInfo? ancestor = new(root); ancestor != null; ancestor = ancestor.Parent)
        {
            if (ancestor.Exists && (ancestor.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("Build input ancestors cannot contain links or junctions.");
            }
        }
        List<BuildFile> result = [];
        Stack<string> pending = new();
        pending.Push(root);
        while (pending.TryPop(out string? currentDirectory))
        {
            foreach (string path in Directory.EnumerateFileSystemEntries(currentDirectory).Order(StringComparer.Ordinal))
            {
                FileAttributes attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidDataException("Build inputs cannot contain links or junctions.");
                }

                if ((attributes & FileAttributes.Directory) != 0)
                {
                    pending.Push(path);
                }
                else
                {
                    result.Add(new BuildFile(Path.GetRelativePath(root, path).Replace('\\', '/'), new FileInfo(path).Length, HashFile(path)));
                }
            }
        }

        if (result.Count == 0)
        {
            throw new InvalidDataException("An immutable build must contain files.");
        }

        return result.OrderBy(file => file.Path, StringComparer.Ordinal).ToArray();
    }
}
