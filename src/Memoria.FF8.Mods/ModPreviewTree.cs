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
        ModPreviewTreeNode[] branches = entries.GroupBy(entry => entry.Category, StringComparer.OrdinalIgnoreCase)
            .Select(category => Folder(category.Key, category, category.GroupBy(entry => entry.Group, StringComparer.OrdinalIgnoreCase)
                .Select(group => Folder(group.Key, group, BuildGroup(group))))).ToArray();
        List<ModPreviewTreeNode> initialPath = [];
        if ((entries.FirstOrDefault(IsFile) ?? entries.FirstOrDefault()) is { } first)
        {
            FindPath(branches, first, initialPath);
        }
        return new(Array.AsReadOnly(branches), initialPath.AsReadOnly());

        bool IsFile(ModPreviewEntry entry) => files.Contains(entry.SourcePath ?? entry.Id);

        ModPreviewTreeNode Folder(string name, IEnumerable<ModPreviewEntry> entries, IEnumerable<ModPreviewTreeNode> children)
        {
            int count = entries.Where(IsFile).Select(entry => entry.SourcePath ?? entry.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count();
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
                    yield return Leaf(entry);
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
