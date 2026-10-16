using System.IO.Compression;

namespace Albeoris.Games.FF8.Imaging.Workspace.Internal;

internal sealed class ZipWorkspaceWriter : IWorkspaceWriter
{
    private readonly FileStream _stream;
    private readonly ZipArchive _archive;

    public ZipWorkspaceWriter(String path)
    {
        String? directory = Path.GetDirectoryName(path);
        if (directory is not null)
            Directory.CreateDirectory(directory);
        _stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        _archive = new ZipArchive(_stream, ZipArchiveMode.Create, leaveOpen: false);
    }

    public void Write(String relativePath, Action<Stream> write)
    {
        ArgumentNullException.ThrowIfNull(write);
        String normalized = WorkspaceStorage.NormalizeRelativePath(relativePath);
        ZipArchiveEntry entry = _archive.CreateEntry(normalized, CompressionLevel.Optimal);
        using (Stream output = entry.Open())
        {
            write(output);
        }
    }

    public void Dispose() => _archive.Dispose();
}