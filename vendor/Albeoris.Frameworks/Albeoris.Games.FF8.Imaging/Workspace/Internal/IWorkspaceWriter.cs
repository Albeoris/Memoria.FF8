namespace Albeoris.Games.FF8.Imaging.Workspace.Internal;

internal interface IWorkspaceWriter : IDisposable
{
    void Write(String relativePath, Action<Stream> write);
}