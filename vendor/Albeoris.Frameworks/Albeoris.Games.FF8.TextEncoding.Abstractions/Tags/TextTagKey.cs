namespace Albeoris.Games.FF8.TextEncoding.Tags;

/// <summary>
/// Input keys, buttons and inline icons that can be referenced by a <see cref="TextTagCode.Key"/> tag.
/// </summary>
public enum TextTagKey : byte
{
    EscapeKey1 = 0x20,
    EscapeKey2 = 0x21,
    Menu = 0x24,
    Cancel = 0x25,
    Confirm = 0x26,
    Cards = 0x27,
    Select = 0x28,
    EndConcertKey = 0x2B,
    UpKey = 0x2C,
    RightKey = 0x2D,
    DownKey = 0x2E,
    LeftKey = 0x2F,
    L1 = 0x32,
    R1 = 0x33,
    Triangle = 0x34,
    Circle = 0x35,
    Cross = 0x36,
    Square = 0x37,
    DefaultKey = 0x38,
    EndKey = 0x3B,
    JunctionMarker = 0x41,
    JunctionAbility = 0x45,
    CommandAbility = 0x46,
    ActionAbility = 0x47,
    CharacterAbility = 0x48,
    PartyAbility = 0x49,
    GuardianForceAbility = 0x4A,
    MenuAbility = 0x4B,
    FireElement = 0x5D,
    IceElement = 0x5E,
    ThunderElement = 0x5F,
    EarthElement = 0x60,
    PoisonElement = 0x61,
    WindElement = 0x62,
    WaterElement = 0x63,
    HolyElement = 0x64,
}
