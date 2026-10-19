using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace Memoria.Launcher;

internal sealed record PackageFile(string Path, long Size, string Sha256);

internal sealed record PackageManifest(int SchemaVersion, string BuildId, string Version, DateTimeOffset BuiltAt, PackageFile[] Files)
{
    internal const string ManifestPath = "Data/distribution.json";
    internal const string LauncherPath = "Memoria.Launcher.exe";
    private const long MaximumFileSize = 2L * 1024 * 1024 * 1024;
    private const long MaximumPackageSize = 4L * 1024 * 1024 * 1024;
    private const long MaximumManifestSize = 16L * 1024 * 1024;
    private static readonly StringComparer PathComparer = StringComparer.OrdinalIgnoreCase;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    internal static PackageManifest Read(string root)
    {
        string path = ResolvePath(root, ManifestPath);
        EnsureNoReparsePoints(path);
        using FileStream stream = File.OpenRead(path);
        return Parse(stream);
    }

    internal static PackageManifest? LoadOptional(string root)
    {
        string path = ResolvePath(root, ManifestPath);
        EnsureNoReparsePoints(path);
        try
        {
            using FileStream stream = File.OpenRead(path);
            return Parse(stream);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }

    internal void ValidatePaths()
    {
        if (SchemaVersion != 1 || string.IsNullOrWhiteSpace(BuildId) || string.IsNullOrWhiteSpace(Version) || BuiltAt == default || Files is null)
        {
            throw new InvalidDataException("The distribution manifest has an unsupported schema or missing identity.");
        }

        HashSet<string> paths = new(PathComparer);
        long total = 0;
        foreach (PackageFile file in Files)
        {
            if (file is null)
            {
                throw new InvalidDataException("The distribution manifest contains a missing file record.");
            }

            ValidateFilePath(file.Path, false);
            if (!paths.Add(file.Path) || file.Size < 0 || file.Size > MaximumFileSize || file.Sha256 is null || file.Sha256.Length != 64 || !file.Sha256.All(Uri.IsHexDigit))
            {
                throw new InvalidDataException($"Invalid distribution file record: {file.Path}");
            }

            total += file.Size;
            if (total > MaximumPackageSize)
            {
                throw new InvalidDataException("The distribution exceeds the unpacked size limit.");
            }
        }

        if (!paths.Contains(LauncherPath) || !paths.Contains("Data/Memoria.FF8.exe") || !paths.Contains("Data/audio-host/Memoria.FF8.Music.exe"))
        {
            throw new InvalidDataException("The distribution is missing a required executable.");
        }

        foreach (string path in paths)
        {
            for (int slash = path.IndexOf('/'); slash >= 0; slash = path.IndexOf('/', slash + 1))
            {
                if (paths.Contains(path[..slash]))
                {
                    throw new InvalidDataException("A distribution file is also used as a directory.");
                }
            }
        }
    }

    internal static string ResolvePath(string root, string relative)
    {
        ValidateFilePath(relative, true);
        string fullRoot = Path.GetFullPath(root);
        string fullPath = Path.GetFullPath(Path.Combine(fullRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
        string prefix = Path.EndsInDirectorySeparator(fullRoot) ? fullRoot : fullRoot + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The distribution path escapes its root.");
        }

        return fullPath;
    }

    internal static void Verify(string root, PackageManifest manifest)
    {
        manifest.ValidatePaths();
        foreach (PackageFile file in manifest.Files)
        {
            string path = ResolvePath(root, file.Path);
            EnsureNoReparsePoints(path);
            using FileStream stream = File.OpenRead(path);
            if (stream.Length != file.Size || !Convert.ToHexString(SHA256.HashData(stream)).Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Distribution verification failed: {file.Path}");
            }
        }
    }

    internal static void Extract(string zipPath, string destination)
    {
        string root = Path.GetFullPath(destination);
        EnsureNoReparsePoints(root);
        if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any())
        {
            throw new InvalidDataException("The extraction destination must be empty.");
        }

        using ZipArchive archive = ZipFile.OpenRead(zipPath);
        Dictionary<string, ZipArchiveEntry> files = new(PathComparer);
        HashSet<string> entries = new(PathComparer);
        long total = 0;
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            bool directory = entry.FullName.EndsWith("/", StringComparison.Ordinal);
            string name = directory ? entry.FullName[..^1] : entry.FullName;
            ValidateSegments(name);
            int type = (entry.ExternalAttributes >> 16) & 0xF000;
            if ((entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0 || (type != 0 && type != (directory ? 0x4000 : 0x8000)) || !entries.Add(name))
            {
                throw new InvalidDataException($"Unsupported or duplicate archive entry: {entry.FullName}");
            }

            if (directory)
            {
                if (!IsAllowedDirectory(name) || entry.Length != 0)
                {
                    throw new InvalidDataException($"Unexpected archive directory: {entry.FullName}");
                }
            }
            else
            {
                ValidateFilePath(name, true);
                if (entry.Length > MaximumFileSize || entry.Length < 0)
                {
                    throw new InvalidDataException("An archive file exceeds the unpacked size limit.");
                }

                total += entry.Length;
                files.Add(name, entry);
            }

            if (total > MaximumPackageSize)
            {
                throw new InvalidDataException("The archive exceeds the unpacked size limit.");
            }
        }

        if (!files.TryGetValue(ManifestPath, out ZipArchiveEntry? manifestEntry))
        {
            throw new InvalidDataException("The archive has no distribution manifest.");
        }

        PackageManifest manifest;
        using (Stream stream = manifestEntry.Open())
        {
            using MemoryStream content = new();
            CopyBounded(stream, content, MaximumManifestSize);
            content.Position = 0;
            manifest = Parse(content);
        }

        HashSet<string> expected = new(manifest.Files.Select(file => file.Path), PathComparer) { ManifestPath };
        if (!expected.SetEquals(files.Keys))
        {
            throw new InvalidDataException("The archive files do not match the distribution manifest.");
        }

        foreach (PackageFile file in manifest.Files)
        {
            if (files[file.Path].Length != file.Size)
            {
                throw new InvalidDataException($"The archive file size does not match the manifest: {file.Path}");
            }
        }

        foreach (string entry in entries)
        {
            for (int slash = entry.IndexOf('/'); slash >= 0; slash = entry.IndexOf('/', slash + 1))
            {
                if (files.ContainsKey(entry[..slash]))
                {
                    throw new InvalidDataException("An archive file is also used as a directory.");
                }
            }
        }

        Directory.CreateDirectory(root);
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            if (entry.FullName.EndsWith("/", StringComparison.Ordinal))
            {
                string directory = Path.Combine(root, entry.FullName[..^1].Replace('/', Path.DirectorySeparatorChar));
                EnsureNoReparsePoints(directory);
                Directory.CreateDirectory(directory);
                continue;
            }

            string path = ResolvePath(root, entry.FullName);
            EnsureNoReparsePoints(path);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using Stream input = entry.Open();
            using FileStream output = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            CopyBounded(input, output, entry.Length);
        }

        Verify(root, manifest);
    }

    private static PackageManifest Parse(Stream stream)
    {
        if (stream.CanSeek && stream.Length > MaximumManifestSize)
        {
            throw new InvalidDataException("The distribution manifest exceeds its size limit.");
        }

        try
        {
            PackageManifest manifest = JsonSerializer.Deserialize<PackageManifest>(stream, JsonOptions) ?? throw new InvalidDataException("The distribution manifest is empty.");
            manifest.ValidatePaths();
            return manifest;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The distribution manifest is malformed.", exception);
        }
    }

    private static void ValidateFilePath(string path, bool allowManifest)
    {
        ValidateSegments(path);
        if ((!PathComparer.Equals(path, LauncherPath) && !path.StartsWith("Data/", StringComparison.OrdinalIgnoreCase) && !IsBundledModPath(path)) || (!allowManifest && PathComparer.Equals(path, ManifestPath)) || path.StartsWith(ManifestPath + "/", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"The path is not owned by the distribution: {path}");
        }
    }

    private static void ValidateSegments(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\'))
        {
            throw new InvalidDataException("The distribution contains an unsafe path.");
        }

        foreach (string segment in path.Split('/'))
        {
            if (segment.Length == 0 || segment is "." or ".." || segment.EndsWith(' ') || segment.EndsWith('.') || segment.Any(character => character < 32 || "<>:\"|?*".Contains(character)))
            {
                throw new InvalidDataException($"The distribution contains an unsafe path: {path}");
            }

            string stem = segment.Split('.')[0];
            if (stem.Equals("CON", StringComparison.OrdinalIgnoreCase) || stem.Equals("PRN", StringComparison.OrdinalIgnoreCase) || stem.Equals("AUX", StringComparison.OrdinalIgnoreCase) || stem.Equals("NUL", StringComparison.OrdinalIgnoreCase) || stem.Equals("CLOCK$", StringComparison.OrdinalIgnoreCase) || stem.Equals("CONIN$", StringComparison.OrdinalIgnoreCase) || stem.Equals("CONOUT$", StringComparison.OrdinalIgnoreCase) || (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) && "123456789¹²³".Contains(stem[3])))
            {
                throw new InvalidDataException($"The distribution contains a reserved device name: {path}");
            }
        }
    }

    internal static bool IsBundledModPath(string path)
    {
        return (path.StartsWith("Mods/Memoria/", StringComparison.OrdinalIgnoreCase) && path.Count(character => character == '/') >= 3) || (path.StartsWith("Data/BundledMods/Memoria/", StringComparison.OrdinalIgnoreCase) && path.Count(character => character == '/') >= 4);
    }

    private static bool IsAllowedDirectory(string path)
    {
        return PathComparer.Equals(path, "Data") || path.StartsWith("Data/", StringComparison.OrdinalIgnoreCase) || PathComparer.Equals(path, "Mods") || PathComparer.Equals(path, "Mods/Tonberry") || PathComparer.Equals(path, "Mods/Junction") || PathComparer.Equals(path, "Mods/Demaster") || PathComparer.Equals(path, "Mods/Memoria") || path.StartsWith("Mods/Memoria/", StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureNoReparsePoints(string path)
    {
        for (string? current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
        {
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidDataException("Distribution paths cannot pass through links or reparse points.");
                }
            }
            catch (FileNotFoundException)
            {
            }
            catch (DirectoryNotFoundException)
            {
            }
        }
    }

    private static void CopyBounded(Stream input, Stream output, long limit)
    {
        byte[] buffer = new byte[81920];
        long count = 0;
        int read;
        while ((read = input.Read(buffer)) != 0)
        {
            count += read;
            if (count > limit)
            {
                throw new InvalidDataException("An archive entry exceeds its declared size or configured limit.");
            }

            output.Write(buffer, 0, read);
        }
    }
}
