namespace Memoria.FF8.Mods;

public sealed class ModPreviewTreeNode
{
    internal ModPreviewTreeNode(string label, IReadOnlyList<ModPreviewTreeNode> children, ModPreviewEntry? entry = null)
    {
        Label = label;
        Children = children;
        Entry = entry;
    }

    public string Label { get; }
    public IReadOnlyList<ModPreviewTreeNode> Children { get; }
    public ModPreviewEntry? Entry { get; }
}

public sealed class ModPreviewTree
{
    private ModPreviewTree(IReadOnlyList<ModPreviewTreeNode> branches, IReadOnlyList<ModPreviewTreeNode> initialPath)
    {
        Branches = branches;
        InitialPath = initialPath;
    }

    public IReadOnlyList<ModPreviewTreeNode> Branches { get; }
    public IReadOnlyList<ModPreviewTreeNode> InitialPath { get; }

    internal static ModPreviewTree Create(IReadOnlyList<ModPreviewEntry> entries, IEnumerable<string> physicalPaths)
    {
        HashSet<string> files = new(physicalPaths, StringComparer.OrdinalIgnoreCase);
        var movies = entries.Where(entry => entry is { Category: "Video", Group: "Movies" })
            .ToLookup(entry => Path.ChangeExtension(entry.Id, null), StringComparer.OrdinalIgnoreCase);
        var cameraTracks = entries.Where(entry => entry is { Category: "Video", Group: "Camera tracks" })
            .Select(track => (Track: track, Movie: FindMovie(track)))
            .Where(pair => pair.Movie is not null).ToArray();
        var tracksByMovie = cameraTracks.ToLookup(pair => pair.Movie!, pair => pair.Track);
        HashSet<ModPreviewEntry> nestedTracks = cameraTracks.Select(pair => pair.Track).ToHashSet();
        ModPreviewTreeNode[] branches = entries.GroupBy(entry => entry.Category, StringComparer.OrdinalIgnoreCase)
            .Select(category => Folder(category.Key, category, category.Where(entry => !nestedTracks.Contains(entry)).GroupBy(entry => entry.Group, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Any(entry => entry.Id == ModPreviewDocument.WorldSceneId))
                .Select(group => Folder(group.Key, group, BuildGroup(group))))).ToArray();
        List<ModPreviewTreeNode> initialPath = [];
        if ((entries.FirstOrDefault(entry => IsFile(entry) && !nestedTracks.Contains(entry)) ?? entries.FirstOrDefault()) is { } first)
        {
            FindPath(branches, first, initialPath);
        }
        return new(Array.AsReadOnly(branches), initialPath.AsReadOnly());

        ModPreviewEntry? FindMovie(ModPreviewEntry track)
        {
            string stem = Path.ChangeExtension(track.Id, null);
            ModPreviewEntry? movie = movies[stem + "h"].FirstOrDefault() ?? movies[stem].FirstOrDefault();
            if (movie is not null)
            {
                return movie;
            }
            // Junction also accepts movie and camera files at the package root.
            if (!stem.Contains('/'))
            {
                stem = "movies/" + stem;
            }
            else if (string.Equals(Path.GetDirectoryName(stem), "movies", StringComparison.OrdinalIgnoreCase))
            {
                stem = Path.GetFileName(stem);
            }
            else
            {
                return null;
            }
            return movies[stem + "h"].FirstOrDefault() ?? movies[stem].FirstOrDefault();
        }

        bool IsFile(ModPreviewEntry entry) => files.Contains(entry.SourcePath ?? entry.Id);

        ModPreviewTreeNode Folder(string name, IEnumerable<ModPreviewEntry> entries, IEnumerable<ModPreviewTreeNode> children)
        {
            HashSet<string> countedFiles = new(StringComparer.OrdinalIgnoreCase);
            int count = 0;
            foreach (ModPreviewEntry entry in entries)
            {
                string path = entry.SourcePath ?? entry.Id;
                if (!files.Contains(path) || countedFiles.Add(path))
                {
                    count++;
                }
            }
            return CreateFolder(name, count, children);
        }

        IEnumerable<ModPreviewTreeNode> BuildGroup(IEnumerable<ModPreviewEntry> group)
        {
            foreach (IGrouping<string, ModPreviewEntry> folder in group.GroupBy(entry => entry.SourcePath ?? entry.Id, StringComparer.OrdinalIgnoreCase))
            {
                if (folder.First().Folder is { } name)
                {
                    yield return CreateFolder(name, folder.Count(), folder.Select(Leaf));
                    continue;
                }
                foreach (ModPreviewEntry entry in folder)
                {
                    yield return tracksByMovie.Contains(entry)
                        ? new(entry.Name, Array.AsReadOnly(tracksByMovie[entry].Select(Leaf).ToArray()), entry)
                        : Leaf(entry);
                }
            }
        }
    }

    private static ModPreviewTreeNode Leaf(ModPreviewEntry entry) => new(entry.Name, [], entry);

    private static ModPreviewTreeNode CreateFolder(string name, int count, IEnumerable<ModPreviewTreeNode> children) => new($"{name} ({count})", Array.AsReadOnly(children.ToArray()));

    private static bool FindPath(IEnumerable<ModPreviewTreeNode> branches, ModPreviewEntry entry, List<ModPreviewTreeNode> path)
    {
        foreach (ModPreviewTreeNode branch in branches)
        {
            path.Add(branch);
            if (branch.Entry == entry || FindPath(branch.Children, entry, path))
            {
                return true;
            }
            path.RemoveAt(path.Count - 1);
        }
        return false;
    }
}
