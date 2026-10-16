namespace Albeoris.Games.FF8.TextEncoding.Tags;

/// <summary>
/// The kind of an inline tag embedded in FF8 dialog text, e.g. <c>{Line}</c> or <c>{Color Red}</c>.
/// </summary>
public enum TextTagCode
{
    /// <summary>Terminates the text.</summary>
    End = 0x00,

    /// <summary>Starts a new message page.</summary>
    Next = 0x01,

    /// <summary>Starts a new line within the current page.</summary>
    Line = 0x02,

    /// <summary>References a <see cref="TextTagCharacter"/> parameter.</summary>
    Char = 0x03,

    /// <summary>References a numbered variable parameter.</summary>
    Var = 0x04,

    /// <summary>References a <see cref="TextTagKey"/> parameter.</summary>
    Key = 0x05,

    /// <summary>References a <see cref="TextTagColor"/> parameter.</summary>
    Color = 0x06,

    /// <summary>Pauses the text with a numeric duration parameter.</summary>
    Pause = 0x09,

    /// <summary>References a <see cref="TextTagDialog"/> parameter.</summary>
    Dialog = 0x0A,

    /// <summary>References a <see cref="TextTagOption"/> parameter.</summary>
    Option = 0x0B,

    /// <summary>References a <see cref="TextTagTerm"/> parameter.</summary>
    Term = 0x0E,

    /// <summary>References a <see cref="TextTagName"/> parameter.</summary>
    Name = 0x0C,

    /// <summary>Marks the name of the speaker.</summary>
    Speaker = 0x12,
}
