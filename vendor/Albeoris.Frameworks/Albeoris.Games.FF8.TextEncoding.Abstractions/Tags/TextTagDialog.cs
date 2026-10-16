namespace Albeoris.Games.FF8.TextEncoding.Tags;

/// <summary>
/// Dialog placeholders that can be referenced by a <see cref="TextTagCode.Dialog"/> tag.
/// </summary>
public enum TextTagDialog : byte
{
    CardLevel = 0x20,
    CurrentValue = 0x22,
    SelectedGF = 0x24,
    SelectedGFAbility = 0x25,
    SelectedMagic = 0x26,
    SelectedCharacter = 0x27,
    SelectedParam = 0x28,
    SelectedBlueMagic = 0x29,
}
