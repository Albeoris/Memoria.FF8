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
    ReloadFailure,
    JunctionCompatibilityBlocked
}

public sealed record ModDiagnostic(string Code, string Message, string? PackageId = null, string? RelativePath = null);

public sealed record ModPackage(string Id, string Name, ModFormat Format, bool? ExplicitEnabled, bool RequestedEnabled, ModEffectiveStatus Status, string RootDirectory)
{
    public bool IsBundled { get; init; }
    public OfficialModComponent? Official { get; init; }
    public bool OfficialAvailable { get; init; }
    public NativeModDescriptor? Descriptor { get; init; }
    public bool IsSettingsGroup { get; init; }
    public bool HasLocalization { get; init; }
    public bool IsLanguageProvider { get; init; }
    public bool HasLighting { get; init; }
    public bool HasFieldScene3D { get; init; }
    public bool FieldScenePresentationOnly { get; init; }
    public bool FieldSpringPresentationOnly { get; init; }
    public bool LocalizationOnly { get; init; }
    public bool TextureContentOnly { get; init; }
    public bool MoviePresentationOnly { get; init; }
    public bool GuardianFormContentOnly { get; init; }
    public int Priority { get; init; }
    public JunctionManifestMetadata? JunctionMetadata { get; init; }
}

/// <summary>A seekable, uncompressed payload inside a physical file.</summary>
public sealed record ModFileSegment(string Path, long Offset, long Length);

public sealed class ModFile
{
    private readonly Func<byte[]> readContent;
    private readonly Action<Stream>? copyTo;
    private readonly Func<ModFileSegment?>? resolveFileSegment;

    public ModFile(string packageId, ModFormat format, string relativePath, long revision, Func<byte[]> readBytes, Action<Stream>? copyTo = null, Func<ModFileSegment?>? resolveFileSegment = null)
        : this(packageId, format, relativePath, revision, readBytes, true, copyTo, resolveFileSegment)
    {
    }

    internal ModFile(string packageId, ModFormat format, string relativePath, long revision, Func<byte[]> readBytes, bool cacheContent, Action<Stream>? copyTo = null, Func<ModFileSegment?>? resolveFileSegment = null)
    {
        PackageId = packageId;
        Format = format;
        RelativePath = ModCatalogSnapshot.NormalizePath(relativePath);
        Revision = revision;
        if (cacheContent)
        {
            Lazy<byte[]> content = new(() => readBytes().ToArray(), LazyThreadSafetyMode.ExecutionAndPublication);
            readContent = () => content.Value;
        }
        else
        {
            readContent = readBytes;
        }
        this.copyTo = copyTo;
        this.resolveFileSegment = resolveFileSegment;
    }

    public string PackageId { get; }
    public ModFormat Format { get; }
    public string RelativePath { get; }
    public long Revision { get; }
    public TextureLayoutMetadata? TextureLayout { get; init; }
    public string? AbsolutePath { get; init; }
    /// <summary>Physical metadata version, retained when an explicit retry changes the read revision.</summary>
    public (long Length, long Modified)? ContentStamp { get; init; }

    public ModFileSegment? ResolveFileSegment()
    {
        return resolveFileSegment?.Invoke();
    }

    public byte[] ReadBytes()
    {
        return readContent().ToArray();
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
            destination.Write(readContent());
        }
    }
}
