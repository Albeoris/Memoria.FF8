namespace Memoria.FF8.Mods;

public enum ModFormat
{
    Tonberry,
    Demaster
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

public sealed record ModPackage(string Id, string Name, ModFormat Format, bool? ExplicitEnabled, bool RequestedEnabled, ModEffectiveStatus Status, string RootDirectory);

public sealed class ModFile
{
    private readonly Lazy<byte[]> content;

    public ModFile(string packageId, ModFormat format, string relativePath, long revision, Func<byte[]> readBytes)
    {
        PackageId = packageId;
        Format = format;
        RelativePath = ModCatalogSnapshot.NormalizePath(relativePath);
        Revision = revision;
        content = new Lazy<byte[]>(() => readBytes().ToArray(), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public string PackageId { get; }
    public ModFormat Format { get; }
    public string RelativePath { get; }
    public long Revision { get; }

    public byte[] ReadBytes()
    {
        return content.Value.ToArray();
    }
}
