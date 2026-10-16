using System.IO.Compression;
using System.Text;

namespace Albeoris.Games.FF8.Imaging.Workspace.Internal;

internal sealed class OpenRasterWorkspaceWriter : IWorkspaceWriter
{
    private const String MimeTypePath = "mimetype";
    private const String MimeType = "image/openraster";
    private readonly FileStream _stream;
    private readonly ZipArchive _archive;

    public OpenRasterWorkspaceWriter(String path)
    {
        String? directory = Path.GetDirectoryName(path);
        if (directory is not null)
            Directory.CreateDirectory(directory);
        _stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        _archive = new ZipArchive(_stream, ZipArchiveMode.Create, leaveOpen: false);
        ZipArchiveEntry entry = _archive.CreateEntry(MimeTypePath, CompressionLevel.NoCompression);
        using Stream output = entry.Open();
        output.Write(Encoding.ASCII.GetBytes(MimeType));
    }

    public void Write(String relativePath, Action<Stream> write)
    {
        ArgumentNullException.ThrowIfNull(write);
        String normalized = WorkspaceStorage.NormalizeRelativePath(relativePath);
        if (String.Equals(normalized, MimeTypePath, StringComparison.Ordinal))
            throw new InvalidOperationException("The OpenRaster mimetype entry is created automatically.");
        CompressionLevel compression = normalized.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? CompressionLevel.NoCompression : CompressionLevel.Optimal;
        ZipArchiveEntry entry = _archive.CreateEntry(normalized, compression);
        using Stream output = entry.Open();
        write(output);
    }

    public void Dispose() => _archive.Dispose();
}
