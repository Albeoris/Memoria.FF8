using System.Globalization;
using System.Text;

namespace Albeoris.Games.FF8.Jsm.Messages;

/// <summary>
/// Describes the presentation parameters associated with one field message identifier.
/// </summary>
public sealed record FieldMessageContext
{
    public FieldMessageContext(Int32 messageId, FieldMessagePosition? position = null, FieldMessageChoices? choices = null, FieldMessageSize? size = null)
    {
        MessageId = messageId;
        Position = position;
        Choices = choices;
        Size = size;
    }

    public Int32 MessageId { get; }

    public FieldMessagePosition? Position { get; }

    public FieldMessageChoices? Choices { get; }

    public FieldMessageSize? Size { get; }

    /// <summary>
    /// Formats the legacy-compatible context tag. Returns an empty string when the context has no presentation parameters.
    /// </summary>
    public String FormatTag()
    {
        if (Position is null && Choices is null && Size is null)
        {
            return String.Empty;
        }

        StringBuilder result = new(32);
        result.Append('{');
        if (Position is not null)
        {
            result.Append('X');
            result.Append(Position.X.ToString(CultureInfo.InvariantCulture));
            result.Append('Y');
            result.Append(Position.Y.ToString(CultureInfo.InvariantCulture));
        }

        if (Size is not null)
        {
            result.Append('W');
            result.Append(Size.Width.ToString(CultureInfo.InvariantCulture));
            result.Append('H');
            result.Append(Size.Height.ToString(CultureInfo.InvariantCulture));
        }

        if (Choices is not null)
        {
            result.Append('F');
            result.Append(Choices.FirstLine.ToString(CultureInfo.InvariantCulture));
            result.Append('L');
            result.Append(Choices.LastLine.ToString(CultureInfo.InvariantCulture));
            result.Append('B');
            result.Append(Choices.BeginLine.ToString(CultureInfo.InvariantCulture));
            result.Append('C');
            result.Append(Choices.CancelLine.ToString(CultureInfo.InvariantCulture));
        }

        result.Append('}');
        return result.ToString();
    }

    /// <summary>Prefixes <paramref name="text"/> with this context's tag.</summary>
    public String FormatText(String text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return FormatTag() + text;
    }

    /// <summary>
    /// Parses a complete context tag at the beginning of <paramref name="text"/>.
    /// A tag may contain complete <c>XY</c>, <c>WH</c>, and <c>FLBC</c> blocks in that order.
    /// </summary>
    public static Boolean TryParseTag(Int32 messageId, String text, out FieldMessageContext? context, out Int32 tagLength)
    {
        ArgumentNullException.ThrowIfNull(text);

        ReadOnlySpan<Char> source = text.AsSpan();
        context = null;
        tagLength = 0;
        if (source.Length < 2 || source[0] != '{')
        {
            return false;
        }

        Int32 position = 1;
        FieldMessagePosition? messagePosition = null;
        FieldMessageChoices? choices = null;
        FieldMessageSize? size = null;

        if (position < source.Length && source[position] == 'X')
        {
            if (!TryReadValue(source, ref position, 'X', out Int32 x)
                || !TryReadValue(source, ref position, 'Y', out Int32 y))
            {
                return false;
            }

            messagePosition = new FieldMessagePosition(x, y);
        }

        if (position < source.Length && source[position] == 'W')
        {
            if (!TryReadValue(source, ref position, 'W', out Int32 width)
                || !TryReadValue(source, ref position, 'H', out Int32 height))
            {
                return false;
            }

            size = new FieldMessageSize(width, height);
        }

        if (position < source.Length && source[position] == 'F')
        {
            if (!TryReadValue(source, ref position, 'F', out Int32 firstLine)
                || !TryReadValue(source, ref position, 'L', out Int32 lastLine)
                || !TryReadValue(source, ref position, 'B', out Int32 beginLine)
                || !TryReadValue(source, ref position, 'C', out Int32 cancelLine))
            {
                return false;
            }

            choices = new FieldMessageChoices(firstLine, lastLine, beginLine, cancelLine);
        }

        if (messagePosition is null && choices is null && size is null)
        {
            return false;
        }
        if (position >= source.Length || source[position] != '}')
        {
            return false;
        }

        tagLength = position + 1;
        context = new FieldMessageContext(messageId, messagePosition, choices, size);
        return true;
    }

    private static Boolean TryReadValue(ReadOnlySpan<Char> source, ref Int32 position, Char marker, out Int32 value)
    {
        value = default;
        if (position >= source.Length || source[position] != marker)
        {
            return false;
        }

        position++;
        Int32 start = position;
        if (position < source.Length && source[position] == '-')
        {
            position++;
        }
        Int32 digitStart = position;
        while (position < source.Length && Char.IsAsciiDigit(source[position]))
        {
            position++;
        }
        if (position == digitStart)
        {
            return false;
        }

        return Int32.TryParse(source[start..position], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
    }
}
