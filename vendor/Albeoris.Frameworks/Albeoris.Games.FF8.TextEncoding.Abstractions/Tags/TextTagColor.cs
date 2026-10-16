namespace Albeoris.Games.FF8.TextEncoding.Tags;

/// <summary>
/// Text colors that can be referenced by a <see cref="TextTagCode.Color"/> tag.
/// </summary>
public enum TextTagColor : byte
{
    Disabled = 0x20,
    Grey,
    Yellow,
    Red,
    Green,
    Blue,
    Purple,
    White,
    DisabledBlink,
    GreyBlink,
    YellowBlink,
    RedBlink,
    GreenBlink,
    BlueBlink,
    PurpleBlink,
    WhiteBlink,
}
