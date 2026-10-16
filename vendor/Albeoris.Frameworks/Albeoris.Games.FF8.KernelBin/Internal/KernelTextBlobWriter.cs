namespace Albeoris.Games.FF8.KernelBin.Internal;

/// <summary>
/// Builds a text blob section by appending null-terminated strings in the same order the
/// owning record array is written, mirroring how the original file packs its text. The
/// resulting blob is zero-padded to a multiple of 4 bytes, matching the original format.
/// </summary>
internal sealed class KernelTextBlobWriter
{
    private readonly List<Byte> _buffer = new();
    private readonly System.Text.Encoding _encoding;

    public KernelTextBlobWriter(System.Text.Encoding encoding)
    {
        _encoding = encoding;
    }

    /// <summary>
    /// Appends <paramref name="value"/> followed by a null terminator and returns its offset,
    /// or returns the "no text" sentinel (<see cref="UInt16.MaxValue"/>) without writing
    /// anything if <paramref name="value"/> is <see langword="null"/>.
    /// </summary>
    public UInt16 Write(String? value)
    {
        if (value == null)
            return UInt16.MaxValue;

        Int32 offset = _buffer.Count;
        if (offset >= UInt16.MaxValue)
            throw new InvalidOperationException("The kernel.bin text blob exceeded the maximum addressable size of 64 KB.");

        Byte[] bytes = _encoding.GetBytes(value);
        _buffer.AddRange(bytes);
        _buffer.Add(0);
        return (UInt16)offset;
    }

    public Byte[] ToArray()
    {
        while (_buffer.Count % 4 != 0)
            _buffer.Add(0);

        return _buffer.ToArray();
    }
}
