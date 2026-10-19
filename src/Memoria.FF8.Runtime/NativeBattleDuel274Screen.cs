namespace Memoria.FF8.Runtime;

public static class NativeBattleDuel274Screen
{
    public static NativeBattleGfGeometryBatch Draw68D4B0(byte amount)
    {
        int value = 80 * NativeBattleCameraMath.Sine(16 * amount) >> 12;
        uint top = unchecked((ushort)(8 * value - 640));
        uint middle = unchecked((ushort)(8 * value));
        uint lower = unchecked((ushort)(8 * (216 - value)));
        uint bottom = unchecked((ushort)(8 * (296 - value)));
        return new([Packet(0, [0x01000000, 0xE1000020]), Packet(8, [0x08000000, 0x3A404040, top << 16, 0x00404040, top << 16 | 2560, 0, middle << 16, 0, middle << 16 | 2560]), Packet(44, [0x08000000, 0x3A000000, lower << 16, 0, lower << 16 | 2560, 0x00404040, bottom << 16, 0x00404040, bottom << 16 | 2560]), Packet(80, [0x01000000, 0xE1000040])], 88);
    }

    private static NativeBattleGfGeometryAttempt Packet(int offset, System.Collections.Immutable.ImmutableArray<uint> words)
    {
        SkinWrites stores = new(words.Length * 4, offset);
        for (int index = 0; index < words.Length; index++)
        {
            stores.Word(index * 4, words[index]);
        }
        return stores.Attempt(new(8, words, [0, 0, 0, 0]));
    }
}
