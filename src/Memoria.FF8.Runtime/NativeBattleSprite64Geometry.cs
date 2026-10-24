using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public readonly record struct NativeBattleSprite64Corner(int X, int Y, int U, int V)
{
    public float TextureU => U / 256f;
    public float TextureV => V / 256f;
}

public static class NativeBattleSprite64Geometry
{
    public static ImmutableArray<NativeBattleSprite64Corner> Expand(ImmutableArray<uint> words, ushort flags20)
    {
        if (words.IsDefault || words.Length != 4 || (words[0] >> 24) is not (0x64 or 0x66))
        {
            throw new InvalidDataException("Native battle sprite geometry requires its four original command words.");
        }
        int x = unchecked((short)words[1]);
        int y = unchecked((short)(words[1] >> 16));
        int width = unchecked((short)words[3]);
        int height = unchecked((short)(words[3] >> 16));
        int unit = (flags20 & 1) != 0 ? 1 : 8;
        int right = unchecked(x + width * unit);
        int bottom = unchecked(y + height * unit);
        int u = (byte)words[2];
        int v = (byte)(words[2] >> 8);
        int endU = unchecked(u + width);
        int endV = unchecked(v + height);
        if ((uint)endU > 256 || (uint)endV > 256)
        {
            throw new NotSupportedException("Native battle sprite UV endpoints require their initialized lookup-table range.");
        }
        return [new(x, y, u, v), new(right, y, endU, v), new(x, bottom, u, endV), new(right, bottom, endU, endV)];
    }
}
