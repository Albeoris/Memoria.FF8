namespace Albeoris.Games.FF8.TextEncoding.Internal;

/// <summary>
/// Recognizes and skips <c>//</c> line comments and <c>/* */</c> block comments while
/// encoding source text. Comments never appear in decoded output.
/// </summary>
internal static class TextComment
{
    public static Boolean TryRead(Char[] chars, ref Int32 index, ref Int32 count)
    {
        if (count < 2 || chars[index] != '/')
        {
            return false;
        }

        if (chars[index + 1] == '/')
        {
            return TryReadLineComment(chars, ref index, ref count);
        }

        if (chars[index + 1] == '*')
        {
            return TryReadBlockComment(chars, ref index, ref count);
        }

        return false;
    }

    private static Boolean TryReadLineComment(Char[] chars, ref Int32 index, ref Int32 count)
    {
        Int32 consumed = 0;
        while (count > 0 && chars[index] != '\n')
        {
            index++;
            count--;
            consumed++;
        }

        if (count > 0)
        {
            // Consume the trailing newline as well.
            index++;
            count--;
            consumed++;
        }

        return consumed > 0;
    }

    private static Boolean TryReadBlockComment(Char[] chars, ref Int32 index, ref Int32 count)
    {
        Int32 startIndex = index;
        Int32 startCount = count;

        // Skip the opening "/*".
        index += 2;
        count -= 2;

        while (count > 0)
        {
            if (chars[index] == '*' && count > 1 && chars[index + 1] == '/')
            {
                index += 2;
                count -= 2;
                return true;
            }

            index++;
            count--;
        }

        // Unterminated block comment: treat the "/*" as ordinary characters.
        index = startIndex;
        count = startCount;
        return false;
    }
}
