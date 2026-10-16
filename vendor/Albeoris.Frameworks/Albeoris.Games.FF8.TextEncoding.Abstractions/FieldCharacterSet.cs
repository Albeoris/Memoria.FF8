namespace Albeoris.Games.FF8.TextEncoding;

/// <summary>
/// A named set of extra characters that a Japanese field (map) contributes on top of
/// the base Japanese codepage. Some maps replace part of the reserved byte range with
/// their own glyphs, so the same byte can represent a different character depending on
/// which field is currently active.
/// </summary>
public sealed class FieldCharacterSet
{
    /// <summary>
    /// The identifier of the field (map) that owns this character set.
    /// </summary>
    public Int32 Id { get; }

    /// <summary>
    /// The name of the field (map) that owns this character set.
    /// </summary>
    public String Name { get; }

    /// <summary>
    /// The characters available for this field, ordered by their escape index.
    /// </summary>
    public String Characters { get; }

    /// <summary>
    /// The character used when a decoded escape index falls outside of <see cref="Characters"/>.
    /// </summary>
    public Char PlaceholderCharacter { get; }

    public FieldCharacterSet(Int32 id, String name, String characters, Char placeholderCharacter)
    {
        Id = id;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Characters = characters ?? throw new ArgumentNullException(nameof(characters));
        PlaceholderCharacter = placeholderCharacter;
    }

    /// <summary>
    /// The number of characters available for this field.
    /// </summary>
    public Int32 Count
    {
        get { return Characters.Length; }
    }
}
