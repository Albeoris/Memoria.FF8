namespace Albeoris.Games.FF8.Imaging.Workspace.Internal;

internal interface IWorkspaceReader : IDisposable
{
    Boolean Exists(String relativePath);
    Byte[] Read(String relativePath);
}