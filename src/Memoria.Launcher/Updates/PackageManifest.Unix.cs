using System.Formats.Tar;
using System.IO.Compression;

namespace Memoria.Launcher;

internal sealed partial record PackageManifest
{
    private static void ExtractUnix(string archivePath, string destination, string? expectedRuntimeIdentifier)
    {
        string root = Path.GetFullPath(destination);
        EnsureNoReparsePoints(root);
        if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any())
        {
            throw new InvalidDataException("The extraction destination must be empty.");
        }
        // Validate the entire inventory before creating any file in the staging tree.
        Dictionary<string, (long Length, int Mode)> entries = new(StringComparer.Ordinal);
        PackageManifest? manifest = null;
        long total = 0;
        bool macArchive = expectedRuntimeIdentifier == "osx-x64";
        string inventoryPath = macArchive ? MacManifestPath : ManifestPath;
        string EntryPath(string name)
        {
            const string prefix = "Memoria.FF8.app/";
            if (macArchive)
            {
                if (!name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    throw new InvalidDataException("The macOS archive must contain exactly one fixed application bundle.");
                }
                return name[prefix.Length..];
            }
            return name;
        }
        using (FileStream input = File.OpenRead(archivePath))
        using (GZipStream gzip = new(input, CompressionMode.Decompress))
        using (TarReader reader = new(gzip))
        {
            TarEntry? entry;
            while ((entry = reader.GetNextEntry()) is not null)
            {
                if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile))
                {
                    throw new InvalidDataException("The update contains a link, directory or special entry.");
                }
                string relative = EntryPath(entry.Name);
                ValidateFilePath(relative, true);
                if (entry.Length < 0 || entry.Length > MaximumFileSize || !entries.TryAdd(relative, (entry.Length, (int)entry.Mode)))
                {
                    throw new InvalidDataException("The update contains a duplicate or oversized entry.");
                }
                total = checked(total + entry.Length);
                if (total > MaximumPackageSize)
                {
                    throw new InvalidDataException("The update exceeds the unpacked size limit.");
                }
                if (relative == inventoryPath)
                {
                    using MemoryStream content = new();
                    CopyBounded(entry.DataStream!, content, MaximumManifestSize);
                    content.Position = 0;
                    manifest = Parse(content);
                }
            }
        }
        if (manifest is null)
        {
            throw new InvalidDataException("The update has no distribution manifest.");
        }
        manifest.ValidateTarget(expectedRuntimeIdentifier ?? "linux-x64");
        HashSet<string> expected = new(manifest.Files.Select(file => file.Path), StringComparer.Ordinal) { inventoryPath };
        if (!expected.SetEquals(entries.Keys) || entries[inventoryPath].Mode != 420)
        {
            throw new InvalidDataException("The update entries do not match its inventory.");
        }
        foreach (PackageFile file in manifest.Files)
        {
            if (entries[file.Path] != (file.Size, file.UnixMode))
            {
                throw new InvalidDataException("The update size or executable mode does not match its inventory: " + file.Path);
            }
        }
        using FileStream source = File.OpenRead(archivePath);
        using GZipStream compressed = new(source, CompressionMode.Decompress);
        using TarReader tar = new(compressed);
        TarEntry? next;
        while ((next = tar.GetNextEntry()) is not null)
        {
            string path = ResolvePath(root, EntryPath(next.Name));
            EnsureNoReparsePoints(path);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using (FileStream output = new(path, FileMode.CreateNew))
            {
                CopyBounded(next.DataStream!, output, next.Length);
            }
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(path, next.Mode);
            }
        }
        Verify(root, manifest);
    }
}
