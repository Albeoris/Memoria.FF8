namespace Memoria.FF8.Media;

/// <summary>Identifies a local movie source version, including the container of an archive payload.</summary>
public sealed record MovieFileVersion(string Path, long Length, long ModifiedTicks)
{
    public static MovieFileVersion? Capture(string source)
    {
        bool archive = source.StartsWith("subfile,", StringComparison.Ordinal);
        if (archive)
        {
            const string marker = ",:file:";
            int markerIndex = source.IndexOf(marker, StringComparison.Ordinal);
            if (markerIndex < 0)
            {
                throw new NotSupportedException("Movie archive sources require a local file payload.");
            }
            source = source[(markerIndex + marker.Length)..];
        }
        if (!archive && !File.Exists(source))
        {
            return null;
        }
        FileInfo file = new(source);
        return new MovieFileVersion(file.FullName, file.Length, file.LastWriteTimeUtc.Ticks);
    }

    public void Validate()
    {
        FileInfo file = new(Path);
        if (!file.Exists || file.Length != Length || file.LastWriteTimeUtc.Ticks != ModifiedTicks)
        {
            throw new IOException("The movie source changed after its presentation metadata was prepared. Prepare the movie again before decoding.");
        }
    }
}
