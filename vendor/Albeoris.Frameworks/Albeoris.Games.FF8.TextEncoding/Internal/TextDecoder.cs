namespace Albeoris.Games.FF8.TextEncoding.Internal;

/// <summary>
/// Converts the game's byte representation back into characters for a single codepage.
/// </summary>
internal sealed class TextDecoder
{
    private readonly Codepage _codepage;
    private readonly FF8Encoding _owner;

    public TextDecoder(Codepage codepage, FF8Encoding owner)
    {
        _codepage = codepage;
        _owner = owner;
    }

    public Int32 GetMaxCharCount(Int32 byteCount)
    {
        return byteCount * TextTag.MaxTagLength;
    }

    public Int32 GetCharCount(Byte[] bytes, Int32 index, Int32 count)
    {
        Int32 result = 0;
        Char[] discardBuffer = new Char[TextTag.MaxTagLength];

        while (count > 0)
        {
            TextTag? tag = TextTag.TryRead(bytes, ref index, ref count);
            if (tag is not null)
            {
                Int32 offset = 0;
                result += tag.Write(discardBuffer, ref offset);
                continue;
            }

            Byte b = bytes[index];
            if (_codepage.IsMultipage && b >= 0x18 && b <= 0x1C)
            {
                result += 1;
                index += 2;
                count -= 2;
            }
            else if (!_codepage.IsMultipage && PackedCharacterPairs.Contains(b))
            {
                result += 2;
                index += 1;
                count -= 1;
            }
            else
            {
                result += 1;
                index += 1;
                count -= 1;
            }
        }

        return result;
    }

    public Int32 GetChars(Byte[] bytes, Int32 byteIndex, Int32 byteCount, Char[] chars, Int32 charIndex)
    {
        Int32 startCharIndex = charIndex;

        while (byteCount > 0)
        {
            TextTag? tag = TextTag.TryRead(bytes, ref byteIndex, ref byteCount);
            if (tag is not null)
            {
                tag.Write(chars, ref charIndex);
                continue;
            }

            Byte b = bytes[byteIndex++];
            byteCount--;

            if (_codepage.IsMultipage && TryReadJapaneseEscape(b, bytes, ref byteIndex, ref byteCount, out var japaneseChar))
            {
                chars[charIndex++] = japaneseChar;
                continue;
            }

            if (!_codepage.IsMultipage && PackedCharacterPairs.TryGet(b, out var b1, out var b2))
            {
                chars[charIndex++] = _codepage[b1];
                chars[charIndex++] = _codepage[b2];
                continue;
            }

            chars[charIndex++] = _codepage[b];
        }

        return charIndex - startCharIndex;
    }

    private Boolean TryReadJapaneseEscape(Byte b, Byte[] bytes, ref Int32 byteIndex, ref Int32 byteCount, out Char result)
    {
        if (b == 0x18)
        {
            throw new FormatException("Byte 0x18 is reserved and cannot start a character.");
        }

        if (b is >= 0x19 and <= 0x1B)
        {
            Int32 page = b - 0x18;
            Byte offsetByte = bytes[byteIndex++];
            byteCount--;

            Int32 characterIndex = page * 256 + offsetByte;
            result = _codepage[characterIndex];
            return true;
        }

        if (b == 0x1C)
        {
            FieldCharacterSet? fieldCharacters = _owner.FieldCharacters;
            if (fieldCharacters is null)
            {
                throw new InvalidOperationException("Decoding this text requires field-specific characters. Set TextEncoding.FieldCharacters before decoding.");
            }

            Byte externalIndex = bytes[byteIndex++];
            byteCount--;

            if (externalIndex < 0x20)
            {
                throw new FormatException($"Invalid external character index: {externalIndex}.");
            }

            Int32 offset = externalIndex - 0x20;
            if (offset < fieldCharacters.Count)
            {
                result = fieldCharacters.Characters[offset];
            }
            else
            {
                result = fieldCharacters.PlaceholderCharacter;
            }

            return true;
        }

        result = '\0';
        return false;
    }
}
