using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

static class PackagedDistribution
{
    public static void ValidateArchive(string archive, string rid, string destination)
    {
        if (Directory.Exists(destination) && Directory.EnumerateFileSystemEntries(destination).Any())
        {
            throw new InvalidDataException("Archive validation needs an empty extraction destination.");
        }
        Directory.CreateDirectory(destination);
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        void Extract(string name, Stream? content, long length, bool directory, int mode)
        {
            if (name.StartsWith("./", StringComparison.Ordinal))
            {
                name = name[2..];
            }
            if (directory && name.Length == 0)
            {
                return;
            }
            name = directory ? name.TrimEnd('/') : name;
            if (name.Length == 0 || name.Contains('\\') || name.Split('/').Any(part => part is "" or "." or ".." || part.Contains(':')) || !names.Add(name))
            {
                throw new InvalidDataException("Unsafe or colliding package entry: " + name);
            }
            string target = Path.GetFullPath(Path.Combine(destination, name));
            if (!target.StartsWith(Path.GetFullPath(destination) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Package path escapes destination.");
            }
            if (directory)
            {
                Directory.CreateDirectory(target);
                return;
            }
            total = checked(total + length);
            if (length < 0 || length > 2L * 1024 * 1024 * 1024 || total > 4L * 1024 * 1024 * 1024)
            {
                throw new InvalidDataException("Package exceeds extraction bounds.");
            }
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using (FileStream output = new(target, FileMode.CreateNew))
            {
                if (content is not null)
                {
                    byte[] buffer = new byte[81920];
                    long copied = 0;
                    int count;
                    while ((count = content.Read(buffer)) != 0)
                    {
                        copied += count;
                        if (copied > length)
                        {
                            throw new InvalidDataException("Package entry exceeds declared size.");
                        }
                        output.Write(buffer, 0, count);
                    }
                    if (copied != length)
                    {
                        throw new InvalidDataException("Truncated package entry.");
                    }
                }
            }
            if (rid != "win-x64")
            {
                bool executable = Path.GetFileName(target) is "Memoria.Launcher" or "Memoria.FF8" or "Memoria.FF8.Music" or "Memoria.PsfDecoder";
                if (mode != (executable ? 493 : 420))
                {
                    throw new InvalidDataException("Unexpected Unix package mode: " + name);
                }
                if (!OperatingSystem.IsWindows())
                {
                    File.SetUnixFileMode(target, (UnixFileMode)mode);
                }
            }
        }
        if (rid == "win-x64")
        {
            using ZipArchive zip = ZipFile.OpenRead(archive);
            foreach (ZipArchiveEntry entry in zip.Entries)
            {
                bool directory = entry.FullName.EndsWith('/');
                int type = (entry.ExternalAttributes >> 16) & 0xf000;
                if (type != 0 && type != (directory ? 0x4000 : 0x8000))
                {
                    throw new InvalidDataException("Package contains a link or special entry.");
                }
                using Stream content = entry.Open();
                Extract(entry.FullName, content, entry.Length, directory, 0);
            }
        }
        else
        {
            using FileStream input = File.OpenRead(archive);
            using GZipStream gzip = new(input, CompressionMode.Decompress);
            using TarReader tar = new(gzip);
            TarEntry? entry;
            while ((entry = tar.GetNextEntry()) is not null)
            {
                bool directory = entry.EntryType == TarEntryType.Directory;
                if (!directory && entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile))
                {
                    throw new InvalidDataException("Package contains a link or special entry.");
                }
                Extract(entry.Name, entry.DataStream, entry.Length, directory, (int)entry.Mode);
            }
        }
        string root = rid == "osx-x64" ? Path.Combine(destination, "Memoria.FF8.app") : destination;
        if (rid == "osx-x64")
        {
            string plist = File.ReadAllText(Path.Combine(destination, "Memoria.FF8.app", "Contents", "Info.plist"));
            if (!plist.Contains("<string>26.0</string>", StringComparison.Ordinal) || !plist.Contains("<string>Memoria.Launcher</string>", StringComparison.Ordinal))
            {
                throw new InvalidDataException("macOS bundle minimum or executable is incorrect.");
            }
        }
        Validate(root, rid);
        if (rid == "osx-x64")
        {
            MacNativeClosure.Validate(root);
            string molten = Path.Combine(root, "Contents", "MacOS", "Data", "Game", "vulkan", "libMoltenVK.dylib");
            if (!File.Exists(molten) || !File.Exists(Path.Combine(Path.GetDirectoryName(molten)!, "MoltenVK.LICENSE")))
            {
                throw new InvalidDataException("The Vulkan/Metal provider or its notice is missing.");
            }
        }
        PayloadImages.Validate(destination, rid);
    }

    private static void Validate(string root, string rid)
    {
        string prefix = rid == "osx-x64" ? "Contents/MacOS/" : "";
        using JsonDocument json = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, prefix + "Data/distribution.json")));
        JsonElement manifest = json.RootElement;
        if (manifest.GetProperty("RuntimeIdentifier").GetString() != rid || manifest.GetProperty("SchemaVersion").GetInt32() != (rid == "win-x64" ? 1 : 2))
        {
            throw new InvalidDataException("Package descriptor RID/schema mismatch.");
        }
        if (rid == "osx-x64")
        {
            MacBundleVersion.Validate(File.ReadAllBytes(Path.Combine(root, "Contents", "Info.plist")), manifest.GetProperty("Version").GetString()!);
        }
        HashSet<string> expected = new(StringComparer.Ordinal) { prefix + "Data/distribution.json" };
        foreach (JsonElement file in manifest.GetProperty("Files").EnumerateArray())
        {
            string relative = file.GetProperty("Path").GetString()!;
            if (!expected.Add(relative) || relative.Contains('\\') || relative.Split('/').Any(part => part is "" or "." or ".." || part.Contains(':')))
            {
                throw new InvalidDataException("Invalid inventory path.");
            }
            string path = Path.Combine(root, relative);
            if (rid != "win-x64")
            {
                int requiredMode = Path.GetFileName(path) is "Memoria.Launcher" or "Memoria.FF8" or "Memoria.FF8.Music" or "Memoria.PsfDecoder" ? 493 : 420;
                if (file.GetProperty("UnixMode").GetInt32() != requiredMode || (!OperatingSystem.IsWindows() && (int)File.GetUnixFileMode(path) != requiredMode))
                {
                    throw new InvalidDataException("Package executable modes do not match inventory: " + relative);
                }
            }
            using FileStream input = File.OpenRead(path);
            if (input.Length != file.GetProperty("Size").GetInt64() || Convert.ToHexStringLower(SHA256.HashData(input)) != file.GetProperty("Sha256").GetString())
            {
                throw new InvalidDataException("Package inventory hash mismatch: " + relative);
            }
        }
        string extension = rid == "win-x64" ? ".exe" : "";
        foreach (string path in new[] { "Memoria.Launcher" + extension, "Data/Memoria.FF8" + extension, "Data/audio-host/Memoria.FF8.Music" + extension, "Data/Game/vulkan/Memoria.FF8" + extension, "Data/Game/vulkan/Memoria.PsfDecoder" + extension })
        {
            if (!expected.Contains(prefix + path))
            {
                throw new InvalidDataException("Package is missing a required process: " + path);
            }
        }
        if (rid == "win-x64" && (!expected.Contains("Data/Game/dx11/Memoria.FF8.exe") || !expected.Contains("Data/Game/dx12/Memoria.FF8.exe") || !expected.Contains("Data/Game/dx11/Memoria.PsfDecoder.exe") || !expected.Contains("Data/Game/dx12/Memoria.PsfDecoder.exe")))
        {
            throw new InvalidDataException("The Windows archive must contain all three isolated backend hosts.");
        }
        if (!expected.SetEquals(Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))))
        {
            throw new InvalidDataException("Package contains files outside its inventory.");
        }
    }
}
