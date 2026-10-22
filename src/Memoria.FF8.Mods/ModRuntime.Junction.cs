namespace Memoria.FF8.Mods;

public sealed partial class ModRuntime
{
    private static string ConfigurationPath(string root, ModFormat format)
    {
        return format == ModFormat.Junction && root.EndsWith(".iroj", StringComparison.OrdinalIgnoreCase) ? root + ".ini" : Path.Combine(root, "mod.ini");
    }

    private void DiscoverJunction(bool nextAutoEnable, List<ModPackage> packages, List<ModFile> files, List<ModDiagnostic> notices, List<string> identities, HashSet<string> visitedFiles)
    {
        HashSet<string> packageIds = new(StringComparer.OrdinalIgnoreCase);
        IEnumerable<string> roots = ModsDirectories.Select(directory => Path.Combine(directory, "Junction")).Where(Directory.Exists).SelectMany(directory => Directory.EnumerateFileSystemEntries(directory).Where(path => Directory.Exists(path) || path.EndsWith(".iroj", StringComparison.OrdinalIgnoreCase)).OrderBy(path => Path.GetFileName(path), PathOrder)).OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase);
        foreach (string root in roots)
        {
            string name = Path.GetFileName(root);
            string id = $"Junction/{name}";
            if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
            {
                notices.Add(new("package-link-rejected", "Junction package roots must not be links.", id));
                continue;
            }
            if (!packageIds.Add(id))
            {
                identities.Add($"duplicate\0{root}");
                notices.Add(new("duplicate-package", $"A preceding mod directory owns {id}; ignoring duplicate at {root}.", id));
                continue;
            }
            bool? explicitEnabled = ModConfiguration.ReadBoolean(ConfigurationPath(root, ModFormat.Junction), "enabled");
            bool requested = explicitEnabled ?? nextAutoEnable;
            bool compatible = isCompatible(ModFormat.Junction);
            identities.Add($"{id}\0{root}\0{explicitEnabled}\0{requested}\0{compatible}");
            ModEffectiveStatus status = !compatible ? ModEffectiveStatus.IncompatibleEdition : !requested ? ModEffectiveStatus.Disabled : ModEffectiveStatus.Active;
            try
            {
                bool isFolder = Directory.Exists(root);
                string metadataPath = isFolder ? Directory.EnumerateFiles(root).FirstOrDefault(path => Path.GetFileName(path).Equals("mod.xml", StringComparison.OrdinalIgnoreCase)) ?? Path.Combine(root, "mod.xml") : root;
                FileInfo metadata = new(metadataPath);
                FileStamp metadataStamp = new(metadata.Length, metadata.LastWriteTimeUtc.Ticks);
                JunctionModPackage imported = JunctionModImporter.Open(root);
                ValidateJunctionVersion(root, metadataPath, metadataStamp);
                name = imported.Name;
                identities.Add($"{metadataPath}\0{metadataStamp.Length}\0{metadataStamp.LastWriteTicks}\0{name}");
                foreach (string diagnostic in imported.Diagnostics)
                {
                    notices.Add(new("junction-setting-ignored", diagnostic, id));
                    identities.Add(diagnostic);
                }
                foreach (JunctionReplacement replacement in imported.Files)
                {
                    string physical = isFolder ? Path.Combine(root, replacement.SourcePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar)) : root;
                    FileInfo info = new(physical);
                    FileStamp stamp = isFolder ? new(info.Length, info.LastWriteTimeUtc.Ticks) : metadataStamp;
                    string key = id + "/" + replacement.RelativePath + "\0" + physical + "\0" + replacement.SourcePath;
                    visitedFiles.Add(key);
                    if (!fileEntries.TryGetValue(key, out FileEntry? previous) || previous.Stamp != stamp || forceRevalidate)
                    {
                        void Copy(Stream destination)
                        {
                            ValidateJunctionVersion(root, physical, stamp);
                            replacement.CopyTo(destination);
                            ValidateJunctionVersion(root, physical, stamp);
                        }
                        ModFileSegment? ResolveSegment()
                        {
                            ValidateJunctionVersion(root, physical, stamp);
                            return replacement.FileSegment;
                        }
                        byte[] Read()
                        {
                            using MemoryStream destination = new();
                            Copy(destination);
                            return destination.ToArray();
                        }
                        previous = new(stamp, new ModFile(id, ModFormat.Junction, replacement.RelativePath, ++fileRevision, Read, Copy, ResolveSegment));
                        fileEntries[key] = previous;
                    }
                    identities.Add($"{key}\0{previous.File.Revision}");
                    if (requested && compatible)
                    {
                        files.Add(previous.File);
                    }
                }
                if (status == ModEffectiveStatus.Active && imported.Files.Count == 0)
                {
                    status = ModEffectiveStatus.NoUsableEntries;
                }
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or NotSupportedException or ArgumentException or UnauthorizedAccessException or System.Xml.XmlException)
            {
                files.RemoveAll(file => file.PackageId == id);
                status = ModEffectiveStatus.ReloadFailure;
                notices.Add(new("junction-import-failed", exception.Message, id));
                identities.Add(exception.Message);
            }
            packages.Add(new(id, name, ModFormat.Junction, explicitEnabled, requested, status, root));
        }
    }

    private static void ValidateJunctionVersion(string root, string path, FileStamp expected)
    {
        if (Directory.Exists(root))
        {
            ValidateContentPath(root, path);
        }
        else if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"Junction archive became a link: {root}.");
        }
        FileInfo info = new(path);
        if (!info.Exists || new FileStamp(info.Length, info.LastWriteTimeUtc.Ticks) != expected)
        {
            throw new IOException($"Junction content changed during preparation: {path}.");
        }
    }
}
