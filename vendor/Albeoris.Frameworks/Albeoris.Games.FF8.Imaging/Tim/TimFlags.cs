namespace Albeoris.Games.FF8.Imaging.Tim;

[Flags]
public enum TimFlags
{
    None = 0,
    HasClut = 1 << 3,
    Reserved25 = 1 << 25,
    Reserved26 = 1 << 26,
    Reserved28 = 1 << 28,
}