namespace Albeoris.Games.FF8.KernelBin.Internal;

/// <summary>
/// Reads null-terminated strings from a single text blob section, by the same relative byte
/// offsets that the corresponding record array stores.
/// </summary>
internal sealed class KernelTextBlobReader
{
    private readonly Byte[] _blob;
    private readonly System.Text.Encoding _encoding;

    public KernelTextBlobReader(Byte[] blob, System.Text.Encoding encoding)
    {
        _blob = blob;
        _encoding = encoding;
    }

    /// <summary>The raw blob bytes. Exposed internally only for coverage/diagnostic tests.</summary>
    internal Byte[] Blob => _blob;

    /// <summary>
    /// Reads the null-terminated string at <paramref name="offset"/>, or returns
    /// <see langword="null"/> if <paramref name="offset"/> is the "no text" sentinel
    /// (<see cref="UInt16.MaxValue"/>).
    /// </summary>
    public String? ReadString(UInt16 offset)
    {
        if (offset == UInt16.MaxValue)
            return null;

        Int32 length = 0;
        while (offset + length < _blob.Length && _blob[offset + length] != 0)
            length++;

        return _encoding.GetString(_blob, offset, length);
    }
}
