namespace Albeoris.Games.FF8.KernelBin.Internal;

/// <summary>
/// Reads the 56 kernel.bin sections in sequence, mirroring the fixed canonical order the
/// game expects (see <see cref="KernelSections"/>).
/// </summary>
internal sealed class KernelSectionReader
{
    private readonly Byte[] _content;
    private readonly Int32[] _offsets;
    private Int32 _currentIndex;

    public KernelSectionReader(Byte[] content)
    {
        _content = content;
        _offsets = KernelSectionTable.ReadOffsets(content);
    }

    public T[] ReadStructArray<T>() where T : unmanaged
    {
        Int32 index = _currentIndex;
        _currentIndex++;

        Int32 offset = _offsets[index];
        Int32 length = KernelSectionTable.GetLength(_offsets, index, _content.Length);
        return KernelStructArray.Read<T>(_content, offset, length);
    }

    public KernelTextBlobReader ReadTextBlob(System.Text.Encoding encoding)
    {
        Int32 index = _currentIndex;
        _currentIndex++;

        Int32 offset = _offsets[index];
        Int32 length = KernelSectionTable.GetLength(_offsets, index, _content.Length);
        Byte[] blob = _content.AsSpan(offset, length).ToArray();
        return new KernelTextBlobReader(blob, encoding);
    }
}
