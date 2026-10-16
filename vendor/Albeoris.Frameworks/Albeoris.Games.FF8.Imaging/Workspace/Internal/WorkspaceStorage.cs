namespace Albeoris.Games.FF8.Imaging.Workspace.Internal;

internal static class WorkspaceStorage
{
    public static IWorkspaceWriter CreateWriter(WorkspaceLocation location) => location.Format switch
    {
        WorkspaceContainerFormat.Directory => new DirectoryWorkspaceWriter(location.Path),
        WorkspaceContainerFormat.ZipArchive => new ZipWorkspaceWriter(location.Path),
        WorkspaceContainerFormat.OpenRaster => new OpenRasterWorkspaceWriter(location.Path),
        _ => throw new ArgumentOutOfRangeException(nameof(location)),
    };

    public static IWorkspaceReader CreateReader(WorkspaceLocation location) => location.Format switch
    {
        WorkspaceContainerFormat.Directory => new DirectoryWorkspaceReader(location.Path),
        WorkspaceContainerFormat.ZipArchive => new ZipWorkspaceReader(location.Path),
        WorkspaceContainerFormat.OpenRaster => new ZipWorkspaceReader(location.Path),
        _ => throw new ArgumentOutOfRangeException(nameof(location)),
    };

    public static String NormalizeRelativePath(String relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        String normalized = relativePath.Replace('\\', '/');
        if (normalized.StartsWith('/') || normalized.Split('/').Any(part => part is "" or "." or ".."))
            throw new ArgumentException($"Invalid workspace path '{relativePath}'.", nameof(relativePath));
        return normalized;
    }
}
