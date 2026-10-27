using System.Globalization;
using System.Text;
using Albeoris.Games.FF8.TextEncoding.Tags;

namespace Albeoris.Games.FF8.TextEncoding.Internal;

/// <summary>
/// A single inline tag such as <c>{Line}</c>, <c>{Pause 30}</c> or <c>{Char Squall}</c>,
/// as it appears in both the byte-oriented and text-oriented representations of dialog text.
/// </summary>
internal sealed class TextTag
{
    public const Int32 MaxTagLength = 32;

    public TextTag(TextTagCode code)
    {
        Code = code;
        Parameter = null;
    }

    public TextTag(TextTagCode code, Byte parameter)
    {
        Code = code;
        Parameter = parameter;
    }

    public TextTag(TextTagCode code, Enum parameter)
    {
        Code = code;
        Parameter = parameter;
    }

    public TextTagCode Code { get; }

    public Object? Parameter { get; }

    public Int32 Write(Byte[] bytes, ref Int32 offset)
    {
        if (Code == TextTagCode.Glyph)
        {
            Byte glyph = Convert.ToByte(Parameter, CultureInfo.InvariantCulture);
            if (glyph is < 0xCC or > 0xE2)
            {
                throw new FormatException("A native wordmark glyph must be in the range 204..226.");
            }
            bytes[offset++] = glyph;
            return 1;
        }
        bytes[offset++] = (Byte)Code;
        if (Parameter is null)
            return 1;

        bytes[offset++] = Convert.ToByte(Parameter, CultureInfo.InvariantCulture);
        return 2;
    }

    public Int32 Write(Char[] chars, ref Int32 offset)
    {
        String text = ToString();
        if (text.Length > MaxTagLength)
        {
            throw new FormatException($"Tag text is too long: {text}");
        }

        for (Int32 i = 0; i < text.Length; i++)
        {
            chars[offset++] = text[i];
        }

        return text.Length;
    }

    public override String ToString()
    {
        StringBuilder builder = new StringBuilder(MaxTagLength);
        builder.Append('{');
        builder.Append(Code);
        if (Parameter is not null)
        {
            builder.Append(' ');
            builder.Append(Parameter);
        }

        builder.Append('}');
        return builder.ToString();
    }

    public static TextTag? TryRead(Byte[] bytes, ref Int32 offset, ref Int32 left)
    {
        Int32 startOffset = offset;
        Int32 startLeft = left;

        TextTagCode code = (TextTagCode)bytes[offset];

        switch (code)
        {
            case TextTagCode.End:
            case TextTagCode.Next:
            case TextTagCode.Line:
            case TextTagCode.Speaker:
                offset += 1;
                left -= 1;
                return new TextTag(code);

            case TextTagCode.Speed:
            case TextTagCode.Pause:
            case TextTagCode.Var:
            case TextTagCode.Item:
                if (left < 2)
                {
                    break;
                }

                offset += 2;
                left -= 2;
                return new TextTag(code, bytes[offset - 1]);

            case TextTagCode.Char:
                if (left < 2)
                {
                    break;
                }

                offset += 2;
                left -= 2;
                return new TextTag(code, (TextTagCharacter)bytes[offset - 1]);

            case TextTagCode.Key:
                if (left < 2)
                {
                    break;
                }

                offset += 2;
                left -= 2;
                return new TextTag(code, (TextTagKey)bytes[offset - 1]);

            case TextTagCode.Color:
                if (left < 2)
                {
                    break;
                }

                offset += 2;
                left -= 2;
                return new TextTag(code, (TextTagColor)bytes[offset - 1]);

            case TextTagCode.Dialog:
                if (left < 2)
                {
                    break;
                }

                offset += 2;
                left -= 2;
                return new TextTag(code, (TextTagDialog)bytes[offset - 1]);

            case TextTagCode.Option:
                if (left < 2)
                {
                    break;
                }

                offset += 2;
                left -= 2;
                return new TextTag(code, bytes[offset - 1]);

            case TextTagCode.Term:
                if (left < 2)
                {
                    break;
                }

                offset += 2;
                left -= 2;
                return new TextTag(code, (TextTagTerm)bytes[offset - 1]);

            case TextTagCode.Name:
                if (left < 2)
                {
                    break;
                }

                offset += 2;
                left -= 2;
                return new TextTag(code, (TextTagName)bytes[offset - 1]);
        }

        offset = startOffset;
        left = startLeft;
        return null;
    }

    public static TextTag? TryRead(Char[] chars, ref Int32 offset, ref Int32 left)
    {
        Int32 startOffset = offset;
        Int32 startLeft = left;

        if (left < 2 || chars[offset] != '{' || !TryReadTagText(chars, offset, left, out String name, out String parameterText, out Int32 tagLength))
        {
            return null;
        }

        if (!Enum.TryParse(name, out TextTagCode code))
        {
            return null;
        }

        TextTag? tag = TryCreateTag(code, parameterText);
        if (tag is null)
        {
            return null;
        }

        offset += tagLength;
        left -= tagLength;
        return tag;
    }

    private static TextTag? TryCreateTag(TextTagCode code, String parameterText)
    {
        switch (code)
        {
            case TextTagCode.End:
            case TextTagCode.Next:
            case TextTagCode.Line:
            case TextTagCode.Speaker:
                return parameterText.Length == 0 ? new TextTag(code) : null;

            case TextTagCode.Glyph:
                if (!Byte.TryParse(parameterText, NumberStyles.Integer, CultureInfo.InvariantCulture, out Byte glyph) || glyph is < 0xCC or > 0xE2)
                {
                    throw new FormatException("A native wordmark glyph must be in the range 204..226.");
                }
                return new TextTag(code, glyph);

            case TextTagCode.Speed:
            case TextTagCode.Pause:
            case TextTagCode.Var:
            case TextTagCode.Item:
                if (Byte.TryParse(parameterText, NumberStyles.Integer, CultureInfo.InvariantCulture, out Byte numericParameter))
                {
                    return new TextTag(code, numericParameter);
                }

                return null;

            case TextTagCode.Char:
                if (Enum.TryParse(parameterText, out TextTagCharacter characterParameter))
                {
                    return new TextTag(code, characterParameter);
                }

                return null;

            case TextTagCode.Key:
                if (Enum.TryParse(parameterText, out TextTagKey keyParameter))
                {
                    return new TextTag(code, keyParameter);
                }

                return null;

            case TextTagCode.Color:
                if (Enum.TryParse(parameterText, out TextTagColor colorParameter))
                {
                    return new TextTag(code, colorParameter);
                }

                return null;

            case TextTagCode.Dialog:
                if (Enum.TryParse(parameterText, out TextTagDialog dialogParameter))
                {
                    return new TextTag(code, dialogParameter);
                }

                return null;

            case TextTagCode.Option:
                if (Byte.TryParse(parameterText, NumberStyles.Integer, CultureInfo.InvariantCulture, out Byte numericParameter1))
                {
                    return new TextTag(code, numericParameter1);
                }

                return null;

            case TextTagCode.Term:
                if (Enum.TryParse(parameterText, out TextTagTerm termParameter))
                {
                    return new TextTag(code, termParameter);
                }

                return null;

            case TextTagCode.Name:
                if (Enum.TryParse(parameterText, out TextTagName nameParameter))
                {
                    return new TextTag(code, nameParameter);
                }

                return null;

            default:
                return null;
        }
    }

    private static Boolean TryReadTagText(Char[] chars, Int32 offset, Int32 left, out String name, out String parameter, out Int32 tagLength)
    {
        name = String.Empty;
        parameter = String.Empty;
        tagLength = 0;

        Int32 maxLength = Math.Min(left, MaxTagLength);
        Int32 closingBraceOffset = -1;
        for (Int32 i = 1; i < maxLength; i++)
        {
            if (chars[offset + i] == '}')
            {
                closingBraceOffset = i;
                break;
            }
        }

        if (closingBraceOffset < 0)
        {
            return false;
        }

        Int32 spaceOffset = -1;
        for (Int32 i = 1; i < closingBraceOffset; i++)
        {
            if (chars[offset + i] == ' ')
            {
                spaceOffset = i;
                break;
            }
        }

        if (spaceOffset < 0)
        {
            name = new String(chars, offset + 1, closingBraceOffset - 1);
        }
        else
        {
            name = new String(chars, offset + 1, spaceOffset - 1);
            parameter = new String(chars, offset + spaceOffset + 1, closingBraceOffset - spaceOffset - 1);
        }

        tagLength = closingBraceOffset + 1;
        return true;
    }
}
