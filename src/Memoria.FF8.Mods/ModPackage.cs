namespace Memoria.FF8.Mods;

public enum ModFormat
{
    Tonberry,
    Demaster,
    Junction,
    Memoria
}

public enum ModEffectiveStatus
{
    Active,
    Disabled,
    IncompatibleEdition,
    NoUsableEntries,
    ReloadFailure
}

public sealed record ModDiagnostic(string Code, string Message, string? PackageId = null, string? RelativePath = null);

public sealed record ModPackage(string Id, string Name, ModFormat Format, bool? ExplicitEnabled, bool RequestedEnabled, ModEffectiveStatus Status, string RootDirectory)
{
    public bool HasLocalization { get; init; }
    public bool LocalizationOnly { get; init; }
    public int Priority { get; init; }
    public JunctionManifestMetadata? JunctionMetadata { get; init; }
}

/// <summary>A seekable, uncompressed payload inside a physical file.</summary>
public sealed record ModFileSegment(string Path, long Offset, long Length);

public sealed class ModFile
{
    private readonly Lazy<byte[]> content;
    private readonly Action<Stream>? copyTo;
    private readonly Func<ModFileSegment?>? resolveFileSegment;

    public ModFile(string packageId, ModFormat format, string relativePath, long revision, Func<byte[]> readBytes, Action<Stream>? copyTo = null, Func<ModFileSegment?>? resolveFileSegment = null)
    {
        PackageId = packageId;
        Format = format;
        RelativePath = ModCatalogSnapshot.NormalizePath(relativePath);
        Revision = revision;
        content = new Lazy<byte[]>(() => readBytes().ToArray(), LazyThreadSafetyMode.ExecutionAndPublication);
        this.copyTo = copyTo;
        this.resolveFileSegment = resolveFileSegment;
    }

    public string PackageId { get; }
    public ModFormat Format { get; }
    public string RelativePath { get; }
    public long Revision { get; }

    public ModFileSegment? ResolveFileSegment()
    {
        return resolveFileSegment?.Invoke();
    }

    public byte[] ReadBytes()
    {
        return content.Value.ToArray();
    }

    public void CopyTo(Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (copyTo is not null)
        {
            copyTo(destination);
        }
        else
        {
            destination.Write(content.Value);
        }
    }
}
