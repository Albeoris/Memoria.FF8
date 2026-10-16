namespace Albeoris.Games.FF8.TextEncoding.Internal;

/// <summary>
/// A small compression table: certain common two-character sequences of the European
/// and Russian codepages are packed into a single byte with a value of 232 or higher.
/// </summary>
internal static class PackedCharacterPairs
{
    // Sourced from the original game's \eng\menu\packcode.bin table.
    private static readonly Byte[] Pairs =
    {
        0xE8, 0xFF, 0x67, 0x6C, 0x63, 0x20, 0x6C, 0x63, 0x72, 0x6D, 0x70, 0x63, 0x4C, 0x54, 0x6A, 0x20,
        0x6A, 0x6A, 0x4B, 0x4A, 0x6C, 0x72, 0x67, 0x6A, 0x6D, 0x20, 0x63, 0x64, 0x6D, 0x6C, 0x20, 0x75,
        0x20, 0x70, 0x75, 0x67, 0x64, 0x67, 0x49, 0x47, 0x71, 0x20, 0x5F, 0x70, 0x4A, 0x49, 0x20, 0x57,
        0x5F, 0x65,
    };

    public static Boolean Contains(Byte value)
    {
        return value >= 232;
    }

    public static Boolean TryGet(Byte value, out Byte first, out Byte second)
    {
        if (value >= 232)
        {
            Int32 index = (value - 231) * 2;
            first = Pairs[index];
            second = Pairs[index + 1];
            return true;
        }

        first = 0;
        second = 0;
        return false;
    }
}
