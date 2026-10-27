using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace Memoria.Launcher;

internal sealed record PackageFile(string Path, long Size, string Sha256, int UnixMode = 0);

internal sealed partial record PackageManifest(int SchemaVersion, string BuildId, string Version, DateTimeOffset BuiltAt, PackageFile[] Files)
{
    public string RuntimeIdentifier { get; init; } = "win-x64";
    public int LayoutVersion { get; init; } = 1;
    internal string LauncherEntryPoint => RuntimeIdentifier == "win-x64" ? LauncherPath : RuntimeIdentifier == "osx-x64" ? "Contents/MacOS/Memoria.Launcher" : "Memoria.Launcher";
    internal static StringComparer FileSystemComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    internal void ValidateTarget(string runtimeIdentifier)
    {
        ValidatePaths();
        if (RuntimeIdentifier != runtimeIdentifier)
        {
            throw new InvalidDataException("The update targets a different runtime identifier.");
        }
    }

    internal const string ManifestPath = "Data/distribution.json";
    internal const string MacManifestPath = "Contents/MacOS/Data/distribution.json";
    internal static string ManifestRelativePath(string root) => MacBundleUpdate.IsBundleRoot(root) ? MacManifestPath : ManifestPath;
    internal const string LauncherPath = "Memoria.Launcher.exe";
    private const long MaximumFileSize = 2L * 1024 * 1024 * 1024;
    private const long MaximumPackageSize = 4L * 1024 * 1024 * 1024;
    private const long MaximumManifestSize = 16L * 1024 * 1024;
    private static readonly StringComparer PathComparer = StringComparer.OrdinalIgnoreCase;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    internal static PackageManifest Read(string root)
    {
        string path = ResolvePath(root, ManifestRelativePath(root));
        EnsureNoReparsePoints(path);
        using FileStream stream = File.OpenRead(path);
        return Parse(stream);
    }

    internal static PackageManifest? LoadOptional(string root)
    {
        string path = ResolvePath(root, ManifestRelativePath(root));
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
        if ((SchemaVersion != 1 && SchemaVersion != 2) || LayoutVersion != 1 || RuntimeIdentifier is not ("win-x64" or "linux-x64" or "osx-x64") || (SchemaVersion == 1 && RuntimeIdentifier != "win-x64") || (SchemaVersion == 2 && RuntimeIdentifier == "win-x64") || string.IsNullOrWhiteSpace(BuildId) || string.IsNullOrWhiteSpace(Version) || BuiltAt == default || Files is null)
        {
            throw new InvalidDataException("The distribution manifest has an unsupported schema or missing identity.");
        }

        HashSet<string> paths = new(RuntimeIdentifier == "win-x64" ? PathComparer : StringComparer.Ordinal);
        long total = 0;
        foreach (PackageFile file in Files)
        {
            if (file is null)
            {
                throw new InvalidDataException("The distribution manifest contains a missing file record.");
            }

            ValidateFilePath(file.Path, false);
            if (RuntimeIdentifier != "osx-x64" && file.Path.StartsWith("Contents/", StringComparison.Ordinal))
            {
                throw new InvalidDataException("Bundle paths require the macOS runtime inventory.");
            }
            if (RuntimeIdentifier == "osx-x64" && file.Path != "Contents/Info.plist" && !file.Path.StartsWith("Contents/MacOS/", StringComparison.Ordinal))
            {
                throw new InvalidDataException("The macOS inventory contains a file outside the fixed bundle layout.");
            }
            if (RuntimeIdentifier == "win-x64" && !PathComparer.Equals(file.Path, LauncherPath) && !file.Path.StartsWith("Data/", StringComparison.OrdinalIgnoreCase) && !IsBundledModPath(file.Path))
            {
                throw new InvalidDataException("The Windows inventory contains an unowned path.");
            }
            if (RuntimeIdentifier != "win-x64" && file.UnixMode != (System.IO.Path.GetFileName(file.Path) is "Memoria.Launcher" or "Memoria.FF8" or "Memoria.FF8.Music" ? 493 : 420))
            {
                throw new InvalidDataException("The Unix inventory has an unsupported file mode.");
            }
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

        string executablePrefix = RuntimeIdentifier == "osx-x64" ? "Contents/MacOS/" : "";
        if (RuntimeIdentifier == "osx-x64" && !paths.Contains("Contents/Info.plist"))
        {
            throw new InvalidDataException("The macOS bundle has no Info.plist.");
        }
        string extension = RuntimeIdentifier == "win-x64" ? ".exe" : "";
        if (!paths.Contains(LauncherEntryPoint) || !paths.Contains(executablePrefix + "Data/Memoria.FF8" + extension) || !paths.Contains(executablePrefix + "Data/audio-host/Memoria.FF8.Music" + extension))
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
        string fullRoot = DistributionPaths.GetFullPath(root);
        string fullPath = Path.GetFullPath(Path.Combine(fullRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
        string prefix = Path.EndsInDirectorySeparator(fullRoot) ? fullRoot : fullRoot + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(prefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
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
            if (!OperatingSystem.IsWindows() && manifest.RuntimeIdentifier != "win-x64" && (int)File.GetUnixFileMode(path) != file.UnixMode)
            {
                throw new InvalidDataException("Distribution executable mode verification failed: " + file.Path);
            }
            using FileStream stream = File.OpenRead(path);
            if (stream.Length != file.Size || !Convert.ToHexString(SHA256.HashData(stream)).Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Distribution verification failed: {file.Path}");
            }
        }
    }

    internal static void Extract(string zipPath, string destination, string? expectedRuntimeIdentifier = null)
    {
        if (zipPath.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
        {
            ExtractUnix(zipPath, destination, expectedRuntimeIdentifier);
            return;
        }
        string root = DistributionPaths.GetFullPath(destination);
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

        if (expectedRuntimeIdentifier is not null)
        {
            manifest.ValidateTarget(expectedRuntimeIdentifier);
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
        bool portableRoot = !path.Contains('/') && (path == "Memoria.Launcher" || path.EndsWith(".dll", StringComparison.Ordinal) || path.EndsWith(".json", StringComparison.Ordinal) || path.EndsWith(".so", StringComparison.Ordinal) || path.EndsWith(".dylib", StringComparison.Ordinal));
        if ((!portableRoot && path != "Contents/Info.plist" && !path.StartsWith("Contents/MacOS/", StringComparison.Ordinal) && !PathComparer.Equals(path, LauncherPath) && !path.StartsWith("Data/", StringComparison.OrdinalIgnoreCase) && !IsBundledModPath(path)) || (!allowManifest && (PathComparer.Equals(path, ManifestPath) || path == MacManifestPath)) || path.StartsWith(ManifestPath + "/", StringComparison.OrdinalIgnoreCase))
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
        for (string? current = DistributionPaths.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
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
