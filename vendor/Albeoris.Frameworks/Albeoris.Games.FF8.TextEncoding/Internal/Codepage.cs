namespace Albeoris.Games.FF8.TextEncoding.Internal;

/// <summary>
/// Maps between characters and their byte-oriented codepage index.
/// The European and Russian codepages occupy indexes 0-255 (a single byte).
/// The Japanese codepage occupies indexes 0-1023, laid out as four 256-entry pages;
/// pages beyond the first are addressed by a two-byte escape sequence.
/// </summary>
internal sealed class Codepage
{
    /// <summary>
    /// The character substituted for a byte that has no assigned glyph.
    /// </summary>
    internal const Char MissingByteToCharFallback = '�';

    /// <summary>
    /// The character substituted for a glyph that has no assigned byte.
    /// </summary>
    internal const Char MissingCharToByteFallback = '■';

    // The bytes corresponding to these glyphs are never used in the original game.
    // These glyphs are display-only filler entries repeated many times in the raw tables.
    private static readonly Char[] DisplayOnlyCharacters = { '¥', '☻', 'ⱷ' };

    private readonly Char[] _characters;
    private readonly Dictionary<Char, Int32> _indexByCharacter;

    private Codepage(Char[] characters, Boolean isMultipage)
    {
        _characters = characters;
        IsMultipage = isMultipage;
        _indexByCharacter = BuildIndex(characters);
    }

    /// <summary>
    /// Whether this codepage has multiple 256-entry pages (Japanese), as opposed to a single page.
    /// </summary>
    public Boolean IsMultipage { get; }

    /// <summary>
    /// Returns the character stored at the given absolute index (page * 256 + byte offset).
    /// </summary>
    public Char this[Int32 index]
    {
        get
        {
            Char c = _characters[index];
            if (c == '\0') throw new FormatException(); // return MissingByteToCharFallback;
            return c;
        }
    }

    /// <summary>
    /// Attempts to find the absolute index of a character in this codepage.
    /// </summary>
    public Boolean TryGetIndex(Char c, out Int32 index)
    {
        return _indexByCharacter.TryGetValue(c, out index);
    }

    public static Codepage LoadEuropean() => Load(CodepageResources.European, isMultipage: false);
    public static Codepage LoadJapanese() => Load(CodepageResources.Japanese, isMultipage: true);
    public static Codepage LoadRussian() => Load(CodepageResources.Russian, isMultipage: false);

    public static Codepage Load(String[] entries, Boolean isMultipage)
    {
        Char[] characters = new Char[entries.Length];
        for (Int32 i = 0; i < entries.Length; i++)
        {
            String entry = entries[i];
            characters[i] = entry.Length == 0 ? '\0' : entry[0];
        }

        return new Codepage(characters, isMultipage);
    }

    private static Dictionary<Char, Int32> BuildIndex(Char[] characters)
    {
        Dictionary<Char, Int32> map = new Dictionary<Char, Int32>(characters.Length);

        // Iterate from the end so that the lowest index wins when a character repeats.
        for (Int32 i = characters.Length - 1; i >= 0; i--)
        {
            Char c = characters[i];
            if (c == '\0' || Array.IndexOf(DisplayOnlyCharacters, c) >= 0)
                continue;

            map[c] = i;
        }

        return map;
    }
}
