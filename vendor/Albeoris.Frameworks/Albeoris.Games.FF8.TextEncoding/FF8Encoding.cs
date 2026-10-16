using Albeoris.Games.FF8.TextEncoding.Internal;

namespace Albeoris.Games.FF8.TextEncoding;

/// <summary>
/// Encodes and decodes text between .NET strings and the byte representation used by
/// Final Fantasy VIII's European, Russian and Japanese localizations.
/// </summary>
/// <remarks>
/// The Japanese encoding uses per-field (per-map) extension characters. Assign
/// <see cref="FieldCharacters"/> before decoding or encoding text that relies on them;
/// the same <see cref="FF8Encoding"/> instance can be reused across different fields by
/// reassigning <see cref="FieldCharacters"/> between calls.
/// </remarks>
public sealed class FF8Encoding : System.Text.Encoding
{
    private readonly TextEncoder _encoder;
    private readonly TextDecoder _decoder;

    private FF8Encoding(Codepage codepage)
    {
        _encoder = new TextEncoder(codepage, this);
        _decoder = new TextDecoder(codepage, this);
    }

    /// <summary>
    /// The field-specific extension characters currently used by this instance when
    /// encoding or decoding Japanese text. Required for Japanese text that references
    /// external characters; ignored by the European and Russian encodings.
    /// </summary>
    public FieldCharacterSet? FieldCharacters { get; set; }

    /// <summary>
    /// Creates an encoding for the European release of the game.
    /// </summary>
    public static FF8Encoding CreateEuropean() => new(Codepage.LoadEuropean());

    /// <summary>
    /// Creates an encoding for the Russian release of the game.
    /// </summary>
    public static FF8Encoding CreateRussian() => new(Codepage.LoadRussian());

    /// <summary>
    /// Creates an encoding for the Japanese release of the game, without field-specific
    /// extension characters. Set <see cref="FieldCharacters"/> before using it to encode
    /// or decode text that relies on them.
    /// </summary>
    public static FF8Encoding CreateJapanese() => new(Codepage.LoadJapanese());

    /// <summary>
    /// Creates an encoding for the Japanese release of the game, using the built-in
    /// extension characters registered for the given field (map) name.
    /// </summary>
    public static FF8Encoding CreateJapanese(IFieldCharacterProvider fieldCharacterProvider, String fieldName)
    {
        ArgumentNullException.ThrowIfNull(fieldCharacterProvider);

        FF8Encoding encoding = CreateJapanese();
        encoding.FieldCharacters = fieldCharacterProvider.TryGet(fieldName);
        return encoding;
    }

    /// <summary>
    /// Creates an encoding for the Japanese release of the game, using the built-in
    /// extension characters registered for the given field (map) id.
    /// </summary>
    public static FF8Encoding CreateJapanese(IFieldCharacterProvider fieldCharacterProvider, Int32 fieldId)
    {
        ArgumentNullException.ThrowIfNull(fieldCharacterProvider);

        FF8Encoding encoding = CreateJapanese();
        encoding.FieldCharacters = fieldCharacterProvider.TryGet(fieldId);
        return encoding;
    }

    public override Int32 GetByteCount(Char[] chars, Int32 index, Int32 count)
    {
        return _encoder.GetByteCount(chars, index, count);
    }

    public override Int32 GetBytes(Char[] chars, Int32 charIndex, Int32 charCount, Byte[] bytes, Int32 byteIndex)
    {
        return _encoder.GetBytes(chars, charIndex, charCount, bytes, byteIndex);
    }

    public override Int32 GetCharCount(Byte[] bytes, Int32 index, Int32 count)
    {
        return _decoder.GetCharCount(bytes, index, count);
    }

    public override Int32 GetChars(Byte[] bytes, Int32 byteIndex, Int32 byteCount, Char[] chars, Int32 charIndex)
    {
        return _decoder.GetChars(bytes, byteIndex, byteCount, chars, charIndex);
    }

    public override Int32 GetMaxByteCount(Int32 charCount)
    {
        return _encoder.GetMaxByteCount(charCount);
    }

    public override Int32 GetMaxCharCount(Int32 byteCount)
    {
        return _decoder.GetMaxCharCount(byteCount);
    }
}
