namespace Albeoris.Games.FF8.Imaging.Workspace;

public sealed record WorkspaceLocation
{
    public WorkspaceLocation(String path, WorkspaceContainerFormat format)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Path = System.IO.Path.GetFullPath(path);
        Format = format;
    }

    public String Path { get; }
    public WorkspaceContainerFormat Format { get; }
}
