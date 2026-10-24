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
        IEnumerable<(string Root, string Name)> roots = ModsDirectories.Select(directory => Path.Combine(directory, "Junction")).Where(Directory.Exists).SelectMany(directory => EnumerateJunctionRoots(directory).Select(path => (Root: path, Name: Path.GetRelativePath(directory, path).Replace('\\', '/')))).OrderBy(package => package.Name, StringComparer.OrdinalIgnoreCase);
        foreach ((string root, string relativeName) in roots)
        {
            string name = Path.GetFileName(root);
            string id = $"Junction/{relativeName}";
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
            JunctionManifestMetadata? manifestMetadata = null;
            try
            {
                bool isFolder = Directory.Exists(root);
                string metadataPath = isFolder ? Directory.EnumerateFiles(root).FirstOrDefault(path => Path.GetFileName(path).Equals("mod.xml", StringComparison.OrdinalIgnoreCase)) ?? Path.Combine(root, "mod.xml") : root;
                FileInfo metadata = new(metadataPath);
                FileStamp metadataStamp = new(metadata.Length, metadata.LastWriteTimeUtc.Ticks);
                JunctionModPackage imported = JunctionModImporter.Open(root);
                ValidateJunctionVersion(root, metadataPath, metadataStamp);
                name = imported.Name;
                manifestMetadata = imported.Manifest;
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
            packages.Add(new(id, name, ModFormat.Junction, explicitEnabled, requested, status, root) { JunctionMetadata = manifestMetadata });
        }
        ApplyJunctionRelationships(packages, files, notices);
    }

    private static IEnumerable<string> EnumerateJunctionRoots(string directory)
    {
        foreach (string path in Directory.EnumerateFileSystemEntries(directory).OrderBy(path => Path.GetFileName(path), PathOrder))
        {
            if (!Directory.Exists(path))
            {
                if (path.EndsWith(".iroj", StringComparison.OrdinalIgnoreCase))
                {
                    yield return path;
                }
                continue;
            }
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0)
            {
                string[] files;
                try
                {
                    files = Directory.GetFiles(path);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    files = [];
                }
                if (!files.Any(file => Path.GetFileName(file).Equals("mod.xml", StringComparison.OrdinalIgnoreCase)))
                {
                    string[] archives = files.Where(file => file.EndsWith(".iroj", StringComparison.OrdinalIgnoreCase)).OrderBy(file => Path.GetFileName(file), PathOrder).ToArray();
                    if (archives.Length > 0)
                    {
                        foreach (string archive in archives)
                        {
                            yield return archive;
                        }
                        continue;
                    }
                }
            }
            yield return path;
        }
    }

    private static void ApplyJunctionRelationships(List<ModPackage> packages, List<ModFile> files, List<ModDiagnostic> notices)
    {
        List<ModPackage> junction = packages.Where(package => package.Format == ModFormat.Junction).ToList();
        Dictionary<Guid, ModPackage[]> identities = junction.Where(package => package.JunctionMetadata?.Id is not null).GroupBy(package => package.JunctionMetadata!.Id!.Value).ToDictionary(group => group.Key, group => group.ToArray());
        HashSet<string> rejected = new(StringComparer.OrdinalIgnoreCase);
        bool IsActive(ModPackage package)
        {
            return package.Status == ModEffectiveStatus.Active && !rejected.Contains(package.Id);
        }
        void RejectRequirements()
        {
            bool changed;
            do
            {
                changed = false;
                foreach (ModPackage package in junction.Where(IsActive))
                {
                    foreach (Guid required in package.JunctionMetadata?.RequiredMods ?? [])
                    {
                        string? problem = !identities.TryGetValue(required, out ModPackage[]? candidates) ? "missing" : candidates.Length != 1 ? "ambiguous" : !IsActive(candidates[0]) ? "inactive" : null;
                        if (problem is not null)
                        {
                            rejected.Add(package.Id);
                            notices.Add(new("junction-dependency-" + problem, $"Required Junction mod {required} is {problem}.", package.Id));
                            changed = true;
                            break;
                        }
                    }
                }
            }
            while (changed);
        }
        RejectRequirements();
        Dictionary<string, HashSet<string>> edges = junction.Where(IsActive).ToDictionary(package => package.Id, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
        foreach (ModPackage package in junction.Where(IsActive))
        {
            void AddConstraint(Guid target, bool before)
            {
                if (!identities.TryGetValue(target, out ModPackage[]? candidates))
                {
                    return;
                }
                if (candidates.Length != 1)
                {
                    notices.Add(new("junction-order-ambiguous", $"Junction order target {target} is ambiguous.", package.Id));
                    rejected.Add(package.Id);
                    return;
                }
                ModPackage other = candidates[0];
                if (IsActive(other) && edges.ContainsKey(other.Id))
                {
                    edges[before ? package.Id : other.Id].Add(before ? other.Id : package.Id);
                }
            }
            foreach (Guid target in package.JunctionMetadata?.Before ?? [])
            {
                AddConstraint(target, true);
            }
            foreach (Guid target in package.JunctionMetadata?.After ?? [])
            {
                AddConstraint(target, false);
            }
        }
        bool Reaches(string start, string target, HashSet<string> visited)
        {
            return visited.Add(start) && edges[start].Any(next => next.Equals(target, StringComparison.OrdinalIgnoreCase) || Reaches(next, target, visited));
        }
        foreach (string id in edges.Keys)
        {
            if (Reaches(id, id, new HashSet<string>(StringComparer.OrdinalIgnoreCase)))
            {
                rejected.Add(id);
                notices.Add(new("junction-order-cycle", "Junction package participates in a load-order cycle.", id));
            }
        }
        RejectRequirements();
        List<ModPackage> pending = junction.Where(IsActive).ToList();
        List<ModPackage> ordered = [];
        while (pending.Count > 0)
        {
            ModPackage next = pending.First(candidate => !pending.Any(other => edges[other.Id].Contains(candidate.Id)));
            ordered.Add(next);
            pending.Remove(next);
        }
        ordered.AddRange(junction.Where(package => !IsActive(package)).Select(package => rejected.Contains(package.Id) ? package with { Status = ModEffectiveStatus.ReloadFailure } : package));
        int insertion = packages.FindIndex(package => package.Format == ModFormat.Junction);
        if (insertion >= 0)
        {
            packages.RemoveAll(package => package.Format == ModFormat.Junction);
            packages.InsertRange(insertion, ordered);
        }
        ModFile[] original = files.Where(file => file.Format == ModFormat.Junction && !rejected.Contains(file.PackageId)).ToArray();
        int fileInsertion = files.FindIndex(file => file.Format == ModFormat.Junction);
        if (fileInsertion >= 0)
        {
            files.RemoveAll(file => file.Format == ModFormat.Junction);
            files.InsertRange(fileInsertion, ordered.SelectMany(package => original.Where(file => file.PackageId.Equals(package.Id, StringComparison.OrdinalIgnoreCase))));
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
