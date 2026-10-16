namespace Albeoris.Games.FF8.KernelBin.Internal;

/// <summary>
/// Builds the 56 kernel.bin sections in sequence, mirroring the fixed canonical order the
/// game expects (see <see cref="KernelSections"/>), then assembles the final file.
/// </summary>
internal sealed class KernelSectionWriter
{
    private readonly KernelFileBuilder _builder = new();

    public void AddStructArray<T>(T[] items) where T : unmanaged
    {
        _builder.AddSection(KernelStructArray.Write(items));
    }

    public void AddTextBlob(KernelTextBlobWriter writer)
    {
        _builder.AddSection(writer.ToArray());
    }

    public Byte[] Build()
    {
        return _builder.Build();
    }
}
