using System.Formats.Tar;
using System.IO.Compression;
using Memoria.FF8.Platform;
using Memoria.Launcher;

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
            if (name.Split('/').Any(part => part.Equals("Girl Next Door", StringComparison.OrdinalIgnoreCase) || part.Equals("GirlNextDoor.png", StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidDataException("The engine archive contains an external component: " + name);
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
        string root = HostPlatforms.IsMacRuntime(rid) ? Path.Combine(destination, "Memoria.FF8.app") : destination;
        if (HostPlatforms.IsMacRuntime(rid))
        {
            string plist = File.ReadAllText(Path.Combine(destination, "Memoria.FF8.app", "Contents", "Info.plist"));
            if (!plist.Contains("<string>26.0</string>", StringComparison.Ordinal) || !plist.Contains("<string>Memoria.Launcher</string>", StringComparison.Ordinal))
            {
                throw new InvalidDataException("macOS bundle minimum or executable is incorrect.");
            }
        }
        Validate(root, rid);
        if (EngineBuildMetadata.ReadInstalledCompatibilityVersion(root) != EngineBuildMetadata.CompatibilityVersion)
        {
            throw new InvalidDataException("The package engine compatibility version does not match its build.");
        }
        if (HostPlatforms.IsMacRuntime(rid))
        {
            MacNativeClosure.Validate(root, rid);
            string molten = Path.Combine(root, "Contents", "Resources", "Data", "Game", "vulkan", "libMoltenVK.dylib");
            if (!File.Exists(molten) || !File.Exists(Path.Combine(Path.GetDirectoryName(molten)!, "MoltenVK.LICENSE")))
            {
                throw new InvalidDataException("The Vulkan/Metal provider or its notice is missing.");
            }
        }
        PayloadImages.Validate(destination, rid);
    }

    public static void Validate(string root, string rid)
    {
        string prefix = HostPlatforms.IsMacRuntime(rid) ? MacCodeSigning.PayloadPrefix : "";
        PackageManifest manifest = PackageManifest.Read(root);
        bool mac = HostPlatforms.IsMacRuntime(rid);
        if (manifest.RuntimeIdentifier != rid || manifest.SchemaVersion != (rid == "win-x64" ? 1 : mac ? 3 : 2))
        {
            throw new InvalidDataException("Package descriptor RID/schema mismatch.");
        }
        if (mac)
        {
            MacBundleVersion.Validate(File.ReadAllBytes(Path.Combine(root, "Contents", "Info.plist")), manifest.Version);
        }
        PackageManifest.Verify(root, manifest);
        HashSet<string> expected = new(manifest.Files.Select(file => file.Path), StringComparer.Ordinal) { prefix + "Data/distribution.json" };
        if (mac)
        {
            expected.Add(MacCodeSigning.ResourceEnvelope);
        }
        string extension = rid == "win-x64" ? ".exe" : "";
        if (mac && !expected.Contains(MacCodeSigning.Launcher))
        {
            throw new InvalidDataException("The macOS bundle has no native entry point.");
        }
        HashSet<string> installed = new(manifest.GetInstalledFiles().Select(file => file.Path), StringComparer.Ordinal);
        foreach (string path in new[] { "Memoria.Launcher" + extension, "Data/Memoria.FF8" + extension, "Data/Game/vulkan/Memoria.FF8" + extension, "Data/Game/vulkan/Memoria.PsfDecoder" + extension })
        {
            if (!installed.Contains(prefix + path))
            {
                throw new InvalidDataException("Package is missing a required process: " + path);
            }
        }
        if (rid == "win-x64" && (!installed.Contains("Data/Game/dx11/Memoria.FF8.exe") || !installed.Contains("Data/Game/dx12/Memoria.FF8.exe") || !installed.Contains("Data/Game/dx11/Memoria.PsfDecoder.exe") || !installed.Contains("Data/Game/dx12/Memoria.PsfDecoder.exe")))
        {
            throw new InvalidDataException("The Windows archive must contain all three isolated backend hosts.");
        }
        if (!expected.SetEquals(Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))))
        {
            throw new InvalidDataException("Package contains files outside its inventory.");
        }
    }
}
