namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect187Texture
{
    public static byte[] Warp648490(ReadOnlySpan<byte> source, short phase)
    {
        if (source.Length != 128 * 128)
        {
            throw new InvalidDataException("Effect187 texture warp requires its current 128 by 128 source bytes.");
        }
        byte[] destination = new byte[source.Length];
        int horizontalPhase = phase;
        int verticalPhase = phase;
        for (int row = 0; row < 128; row++)
        {
            int amplitude = unchecked(-12 * NativeBattleCameraMath.Sine(horizontalPhase)) >> 12;
            int horizontalOffset = unchecked(NativeBattleCameraMath.Sine(row * 16) * amplitude) >> 12;
            int verticalOffset = unchecked(NativeBattleCameraMath.Sine(verticalPhase) * 2) >> 12;
            int sourceRow = 127 - ((row - verticalOffset) & 127);
            for (int column = 0; column < 128; column++)
            {
                int sourceColumn = (column - horizontalOffset) & 127;
                destination[row * 128 + column] = source[sourceRow * 128 + sourceColumn];
            }
            horizontalPhase = unchecked(horizontalPhase + 32);
            verticalPhase = unchecked(verticalPhase + 112);
        }
        return destination;
    }
}
