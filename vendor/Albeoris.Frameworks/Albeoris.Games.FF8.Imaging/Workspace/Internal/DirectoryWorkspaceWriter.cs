namespace Albeoris.Games.FF8.Imaging.Workspace.Internal;

internal sealed class DirectoryWorkspaceWriter : IWorkspaceWriter
{
    private readonly String _root;

    public DirectoryWorkspaceWriter(String root)
    {
        if (Directory.Exists(root) || File.Exists(root))
            throw new IOException($"Workspace destination already exists: {root}");

        Directory.CreateDirectory(root);
        _root = root;
    }

    public void Write(String relativePath, Action<Stream> write)
    {
        ArgumentNullException.ThrowIfNull(write);
        String path = Resolve(relativePath);
        String? directory = Path.GetDirectoryName(path);
        if (directory is not null)
            Directory.CreateDirectory(directory);
        using (FileStream output = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            write(output);
        }
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