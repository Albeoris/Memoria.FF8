namespace Albeoris.Games.FF8.Imaging.Workspace.Internal;

internal sealed class DirectoryWorkspaceReader : IWorkspaceReader
{
    private readonly String _root;

    public DirectoryWorkspaceReader(String root)
    {
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException($"Workspace directory does not exist: {root}");
        _root = root;
    }

    public Boolean Exists(String relativePath) => File.Exists(Resolve(relativePath));

    public Byte[] Read(String relativePath)
    {
        String path = Resolve(relativePath);
        return File.Exists(path)
            ? File.ReadAllBytes(path)
            : throw new FileNotFoundException($"Workspace entry is missing: {relativePath}", path);
    }

    public void Dispose()
    {
    }

    private String Resolve(String relativePath)
    {
        String normalized = WorkspaceStorage.NormalizeRelativePath(relativePath);
        return Path.Combine(_root, normalized.Replace('/', Path.DirectorySeparatorChar));
    }
}