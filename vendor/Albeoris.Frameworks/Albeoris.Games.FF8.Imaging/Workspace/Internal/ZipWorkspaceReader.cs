using System.IO.Compression;

namespace Albeoris.Games.FF8.Imaging.Workspace.Internal;

internal sealed class ZipWorkspaceReader : IWorkspaceReader
{
    private readonly FileStream _stream;
    private readonly ZipArchive _archive;

    public ZipWorkspaceReader(String path)
    {
        _stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            _archive = new ZipArchive(_stream, ZipArchiveMode.Read, leaveOpen: false);
            HashSet<String> names = new(StringComparer.OrdinalIgnoreCase);
            Int64 total = 0;
            if (_archive.Entries.Count > 100000)
            {
                throw new InvalidDataException("Workspace contains too many entries.");
            }
            foreach (ZipArchiveEntry entry in _archive.Entries)
            {
                String name = entry.FullName;
                if (name.EndsWith('/'))
                {
                    name = name[..^1];
                }
                String normalized = WorkspaceStorage.NormalizeRelativePath(name);
                if (normalized != name || name.Contains(':') || name.Any(Char.IsControl) || !names.Add(normalized) || ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000)
                {
                    throw new InvalidDataException($"Unsafe or duplicate workspace entry '{entry.FullName}'.");
                }
                total = checked(total + entry.Length);
                if (entry.Length > 384L * 1024 * 1024 || total > 1024L * 1024 * 1024)
                {
                    throw new InvalidDataException("Workspace exceeds the bounded uncompressed size limit.");
                }
            }
        }
        catch
        {
            _stream.Dispose();
            throw;
        }
    }

    public Boolean Exists(String relativePath) => GetEntry(relativePath) is not null;

    public Byte[] Read(String relativePath)
    {
        ZipArchiveEntry entry = GetEntry(relativePath)
                                ?? throw new FileNotFoundException($"Workspace entry is missing: {relativePath}", relativePath);
        using (Stream input = entry.Open())
        {
            using (MemoryStream output = new((Int32)entry.Length))
            {
                Byte[] buffer = new Byte[81920];
                Int32 count;
                while ((count = input.Read(buffer)) != 0)
                {
                    if (output.Length + count > entry.Length)
                    {
                        throw new InvalidDataException("Workspace entry exceeds its declared length.");
                    }
                    output.Write(buffer, 0, count);
                }
                if (output.Length != entry.Length)
                {
                    throw new InvalidDataException("Workspace entry is truncated.");
                }
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
