using System.IO.Compression;

namespace Albeoris.Games.FF8.Imaging.Workspace.Internal;

internal sealed class ZipWorkspaceReader : IWorkspaceReader
{
    private readonly FileStream _stream;
    private readonly ZipArchive _archive;

    public ZipWorkspaceReader(String path)
    {
        _stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        _archive = new ZipArchive(_stream, ZipArchiveMode.Read, leaveOpen: false);
    }

    public Boolean Exists(String relativePath) => GetEntry(relativePath) is not null;

    public Byte[] Read(String relativePath)
    {
        ZipArchiveEntry entry = GetEntry(relativePath)
                                ?? throw new FileNotFoundException($"Workspace entry is missing: {relativePath}", relativePath);
        using (Stream input = entry.Open())
        {
            using (MemoryStream output = new(entry.Length <= Int32.MaxValue ? (Int32)entry.Length : 0))
            {
                input.CopyTo(output);
                return output.ToArray();
            }
        }
    }

    public void Dispose() => _archive.Dispose();

    private ZipArchiveEntry? GetEntry(String relativePath)
    {
        String normalized = WorkspaceStorage.NormalizeRelativePath(relativePath);
        return _archive.GetEntry(normalized);
    }
}