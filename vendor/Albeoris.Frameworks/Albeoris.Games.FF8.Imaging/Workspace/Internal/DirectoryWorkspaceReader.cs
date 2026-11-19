namespace Albeoris.Games.FF8.Imaging.Workspace.Internal;

internal sealed class DirectoryWorkspaceReader : IWorkspaceReader
{
    private readonly String _root;

    public DirectoryWorkspaceReader(String root)
    {
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException($"Workspace directory does not exist: {root}");
        _root = Path.GetFullPath(root);
        if ((File.GetAttributes(_root) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("Workspace roots must not be symbolic links or reparse points.");
        }
    }

    public Boolean Exists(String relativePath) => File.Exists(Resolve(relativePath));

    public Byte[] Read(String relativePath)
    {
        String path = Resolve(relativePath);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Workspace entry is missing: {relativePath}", path);
        }
        if (new FileInfo(path).Length > 384L * 1024 * 1024)
        {
            throw new InvalidDataException("Workspace member exceeds its size limit.");
        }
        return File.ReadAllBytes(path);
    }

    public void Dispose()
    {
    }

    private String Resolve(String relativePath)
    {
        String normalized = WorkspaceStorage.NormalizeRelativePath(relativePath);
        String path = Path.Combine(_root, normalized.Replace('/', Path.DirectorySeparatorChar));
        String current = _root;
        foreach (String segment in normalized.Split('/'))
        {
            current = Path.Combine(current, segment);
            if (Path.Exists(current) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("Workspace members must not traverse symbolic links or reparse points.");
            }
        }
        return path;
    }
}
