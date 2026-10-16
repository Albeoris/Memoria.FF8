namespace Albeoris.Games.FF8.TextEncoding.Internal;

/// <summary>
/// Converts characters into the game's byte representation for a single codepage.
/// </summary>
internal sealed class TextEncoder
{
    private readonly Codepage _codepage;
    private readonly FF8Encoding _owner;

    public TextEncoder(Codepage codepage, FF8Encoding owner)
    {
        _codepage = codepage;
        _owner = owner;
    }

    public Int32 GetMaxByteCount(Int32 charCount)
    {
        // Worst case: every character needs a two-byte escape sequence.
        return charCount * 2;
    }

    public Int32 GetByteCount(Char[] chars, Int32 index, Int32 count)
    {
        Int32 result = 0;
        Byte[] discardBuffer = new Byte[2];

        while (count > 0)
        {
            TextTag? tag = TextTag.TryRead(chars, ref index, ref count);
            if (tag is not null)
            {
                Int32 offset = 0;
                result += tag.Write(discardBuffer, ref offset);
                continue;
            }

            if (TextComment.TryRead(chars, ref index, ref count))
            {
                continue;
            }

            result += GetEncodedLength(chars[index]);
            index++;
            count--;
        }

        return result;
    }

    public Int32 GetBytes(Char[] chars, Int32 charIndex, Int32 charCount, Byte[] bytes, Int32 byteIndex)
    {
        Int32 startByteIndex = byteIndex;

        while (charCount > 0)
        {
            TextTag? tag = TextTag.TryRead(chars, ref charIndex, ref charCount);
            if (tag is not null)
            {
                tag.Write(bytes, ref byteIndex);
                continue;
            }

            if (TextComment.TryRead(chars, ref charIndex, ref charCount))
            {
                continue;
            }

            WriteChar(chars[charIndex], bytes, ref byteIndex);
            charIndex++;
            charCount--;
        }

        return byteIndex - startByteIndex;
    }

    private Int32 GetEncodedLength(Char c)
    {
        if (_codepage.TryGetIndex(c, out var index))
        {
            return index < 256 ? 1 : 2;
        }

        FieldCharacterSet? fieldCharacters = _owner.FieldCharacters;
        if (_codepage.IsMultipage && fieldCharacters is not null && fieldCharacters.Characters.IndexOf(c) >= 0)
        {
            return 2;
        }

        return 1;
    }

    private void WriteChar(Char c, Byte[] bytes, ref Int32 byteIndex)
    {
        if (_codepage.TryGetIndex(c, out var index))
        {
            if (index < 256)
            {
                bytes[byteIndex++] = (Byte)index;
                return;
            }

            Int32 page = index / 256;
            Int32 offset = index % 256;
            bytes[byteIndex++] = (Byte)(0x18 + page);
            bytes[byteIndex++] = (Byte)offset;
            return;
        }

        FieldCharacterSet? fieldCharacters = _owner.FieldCharacters;
        if (_codepage.IsMultipage && fieldCharacters is not null)
        {
            Int32 externalIndex = fieldCharacters.Characters.IndexOf(c);
            if (externalIndex >= 0)
            {
                bytes[byteIndex++] = 0x1C;
                bytes[byteIndex++] = (Byte)(0x20 + externalIndex);
                return;
            }
        }

        if (_codepage.TryGetIndex(Codepage.MissingCharToByteFallback, out index))
        {
            bytes[byteIndex++] = (Byte)index;
            return;
        }

        throw new FormatException($"Character '{c}' (U+{(Int32)c:X4}) cannot be represented by this codepage.");
    }
}
