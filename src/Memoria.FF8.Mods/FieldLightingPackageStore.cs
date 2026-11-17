using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Memoria.FF8.Mods;

public sealed record FieldLightingSceneSnapshot(FieldLightingScene Scene, string Revision);

/// <summary>Owns only lighting scene payloads. Saving neither changes activation nor rewrites the package manifest.</summary>
public sealed class FieldLightingPackageStore
{
    public const string AbsentRevision = "absent";
    private readonly string root;

    private FieldLightingPackageStore(ModPackage package, bool writable)
    {
        Package = package;
        root = Path.GetFullPath(package.RootDirectory);
        IsWritable = writable;
        FieldLightingSceneIO.ValidateComponent(ReadBounded(Resolve(FieldLightingSceneIO.ComponentPath)));
    }

    public ModPackage Package { get; }
    public bool IsWritable { get; }

    public static FieldLightingPackageStore Open(ModPackage package)
    {
        if (!CanEdit(package, out string? reason))
        {
            throw new IOException(reason);
        }
        return new(package, true);
    }

    public static FieldLightingPackageStore OpenReadOnly(ModPackage package)
    {
        return new(package, false);
    }

    public static bool CanEdit(ModPackage package, out string? reason)
    {
        reason = null;
        if (package.Format != ModFormat.Memoria || !File.Exists(Path.Combine(package.RootDirectory, FieldLightingSceneIO.ComponentPath)))
        {
            reason = "Select a package with a lighting component.";
            return false;
        }
        if (package.IsBundled || package.Official is not null)
        {
            reason = "Managed and bundled lighting packages require a writable custom copy before editing.";
            return false;
        }
        string? probe = null;
        try
        {
            RejectLinks(package.RootDirectory);
            string marker = Path.Combine(package.RootDirectory, FieldLightingSceneIO.ComponentPath);
            RejectLinks(marker);
            if ((File.GetAttributes(marker) & FileAttributes.ReadOnly) != 0)
            {
                reason = "This lighting package is read-only; create a writable custom copy before editing.";
                return false;
            }
            probe = Path.Combine(package.RootDirectory, $".lighting-write-{Guid.NewGuid():N}.tmp");
            using (FileStream stream = new(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Flush(true);
            }
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            reason = "This lighting package requires a writable custom copy before editing: " + exception.Message;
            return false;
        }
        finally
        {
            if (probe is not null && File.Exists(probe))
            {
                File.Delete(probe);
            }
        }
    }

    /// <summary>Creates a disabled package with metadata and a component marker, without scene files or implicit lights.</summary>
    public static FieldLightingPackageStore Create(string modsRoot, string name)
    {
        return CreateCore(modsRoot, name, null);
    }

    public static FieldLightingPackageStore CreateWritableCopy(string modsRoot, ModPackage source, string name)
    {
        _ = OpenReadOnly(source);
        return CreateCore(modsRoot, name, source.RootDirectory);
    }

    private static FieldLightingPackageStore CreateCore(string modsRoot, string name, string? sourceRoot)
    {
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 128 || name.Any(char.IsControl))
        {
            throw new InvalidDataException("A lighting mod requires a name of 1..128 readable characters.");
        }
        string directory = Path.GetFullPath(modsRoot);
        RejectLinks(directory);
        Directory.CreateDirectory(directory);
        string slug = string.Concat(name.ToLowerInvariant().Select(character => char.IsAsciiLetterOrDigit(character) ? character : '-')).Trim('-');
        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }
        slug = slug.Length == 0 ? "lighting" : slug[..Math.Min(slug.Length, 48)].TrimEnd('-');
        string id = slug + "-" + Guid.NewGuid().ToString("N")[..8];
        string destination = Path.Combine(directory, "Memoria", id);
        string staging = Path.Combine(directory, ".lighting-create-" + Guid.NewGuid().ToString("N"));
        RejectLinks(destination);
        try
        {
            Directory.CreateDirectory(staging);
            if (sourceRoot is not null)
            {
                CopyDirectory(Path.GetFullPath(sourceRoot), staging);
            }
            Directory.CreateDirectory(Path.Combine(staging, "lighting"));
            File.WriteAllBytes(Path.Combine(staging, FieldLightingSceneIO.ComponentPath), "{\n  \"schemaVersion\": 1\n}\n"u8.ToArray());
            File.WriteAllText(Path.Combine(staging, "mod.ini"), "[Mod]\nenabled=0\n", new UTF8Encoding(false));
            File.WriteAllBytes(Path.Combine(staging, NativeModDescriptor.FileName), JsonSerializer.SerializeToUtf8Bytes(new { id, name, package = "Custom lighting", description = new Dictionary<string, string> { ["en"] = "Authored positional lighting for individual FFVIII fields." } }, new JsonSerializerOptions { WriteIndented = true }));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            Directory.Move(staging, destination);
            NativeModDescriptor descriptor = NativeModDescriptor.Read(Path.Combine(destination, NativeModDescriptor.FileName));
            ModPackage package = new(descriptor.RuntimePackageId, name, ModFormat.Memoria, false, false, ModEffectiveStatus.Disabled, destination) { Descriptor = descriptor, HasLighting = true };
            return new(package, true);
        }
        finally
        {
            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, true);
            }
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        RejectLinks(source);
        foreach (string file in Directory.EnumerateFiles(source))
        {
            RejectLinks(file);
            string name = Path.GetFileName(file);
            if (name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            string target = Path.Combine(destination, name);
            File.Copy(file, target, false);
            File.SetAttributes(target, FileAttributes.Normal);
        }
        foreach (string child in Directory.EnumerateDirectories(source))
        {
            RejectLinks(child);
            string target = Path.Combine(destination, Path.GetFileName(child));
            Directory.CreateDirectory(target);
            CopyDirectory(child, target);
        }
    }

    public FieldLightingSceneSnapshot ReadScene(string fieldName)
    {
        string path = Resolve(FieldLightingSceneIO.ScenePath(fieldName));
        if (!File.Exists(path))
        {
            return new(FieldLightingScene.Empty(fieldName), AbsentRevision);
        }
        byte[] bytes = ReadBounded(path);
        return new(FieldLightingSceneIO.Deserialize(bytes, fieldName), Revision(bytes));
    }

    /// <summary>Atomically replaces only this field after comparing the exact loaded payload, retaining the draft on failure.</summary>
    public FieldLightingSceneSnapshot SaveScene(FieldLightingScene scene, string expectedRevision)
    {
        if (!IsWritable)
        {
            throw new IOException("This lighting package is read-only. Create a writable custom copy before editing.");
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedRevision);
        byte[] bytes = FieldLightingSceneIO.Serialize(scene);
        string path = Resolve(FieldLightingSceneIO.ScenePath(scene.FieldName));
        using FileStream coordination = Acquire(root);
        FieldLightingSceneIO.ValidateComponent(ReadBounded(Resolve(FieldLightingSceneIO.ComponentPath)));
        string directory = Path.GetDirectoryName(path)!;
        RejectLinks(directory);
        Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using FileStream? original = File.Exists(path) ? new(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete) : null;
            if (original is null ? expectedRevision != AbsentRevision : Revision(ReadBounded(original)) != expectedRevision)
            {
                throw new IOException("This field's lighting changed on disk after the editor opened or saved. Your draft is retained; reload or save it in a custom copy.");
            }
            using (FileStream staged = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                staged.Write(bytes);
                staged.Flush(true);
            }
            RejectLinks(path);
            string currentRevision = File.Exists(path) ? Revision(ReadBounded(path)) : AbsentRevision;
            if (currentRevision != expectedRevision)
            {
                throw new IOException("This field's lighting changed during Save. Your draft is retained.");
            }
            if (original is null)
            {
                File.Move(temporary, path, false);
            }
            else
            {
                File.Replace(temporary, path, null);
            }
            return new(FieldLightingSceneIO.Clone(scene), Revision(bytes));
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private string Resolve(string relative)
    {
        string path = Path.GetFullPath(Path.Combine(root, relative));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            throw new InvalidDataException("Lighting payloads must remain inside their package.");
        }
        RejectLinks(path);
        return path;
    }

    private static string Revision(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static byte[] ReadBounded(string path)
    {
        RejectLinks(path);
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        return ReadBounded(stream);
    }

    private static byte[] ReadBounded(FileStream stream)
    {
        if (stream.Length > FieldLightingSceneIO.MaximumDocumentBytes)
        {
            throw new InvalidDataException("The lighting document exceeds its size limit.");
        }
        byte[] bytes = new byte[stream.Length];
        stream.ReadExactly(bytes);
        return bytes;
    }

    private static FileStream Acquire(string packageRoot)
    {
        string locks = Path.Combine(Path.GetTempPath(), "Memoria.Lighting.Authoring.Locks");
        Directory.CreateDirectory(locks);
        string key = OperatingSystem.IsWindows() ? packageRoot.ToUpperInvariant() : packageRoot;
        string path = Path.Combine(locks, Revision(Encoding.UTF8.GetBytes(key)) + ".lock");
        return new(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }

    private static void RejectLinks(string path)
    {
        for (string? current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
        {
            if ((Directory.Exists(current) || File.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("Lighting package paths must be regular files and directories, without links or reparse points.");
            }
        }
    }
}
