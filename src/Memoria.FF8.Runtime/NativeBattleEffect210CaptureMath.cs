using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect210CapturePlan(NativeBattlePackedMatrix32 Matrix, int DivX, int DivZ, int ScaleX, int ScaleZ)
{
    public int OffsetX => 96;
    public int OffsetY => 128;
}

public static class NativeBattleEffect210CaptureMath
{
    public static NativeBattleEffect210CapturePlan Prepare(NativeBattleEffectArena point8, NativeBattleEffectArena bounds12, short focal)
    {
        int left = Word(bounds12, 0);
        int top = Word(bounds12, 4);
        int right = Word(bounds12, 6);
        int bottom = Word(bounds12, 10);
        int width = unchecked(right - left);
        int height = unchecked(bottom - top);
        int divisorX = width / 192;
        if (divisorX % 192 != 0)
        {
            divisorX = unchecked(divisorX + 1);
        }
        int divisorZ = height / 256;
        if (divisorZ % 256 != 0)
        {
            divisorZ = unchecked(divisorZ + 1);
        }
        int baseX = unchecked(-(Word(point8, 0) / divisorX));
        int baseY = Word(point8, 4) / divisorZ;
        int x = unchecked(baseX + (unchecked(-left - right) / 2) / divisorX);
        int y = unchecked(baseY - (unchecked(-top - bottom) / 2) / divisorZ);
        int scaleX = 16777216 / (unchecked(width << 12) / 192);
        int scaleZ = 16777216 / (unchecked(height << 12) / 256);
        NativeBattleActionMatrix rotation = NativeBattlePandemona291Restoration.Rotation701220(-1024);
        NativeBattleActionMatrix multiplied = NativeBattleGfMatrices.Multiply56C090(rotation, NativeBattleCuragaMatrices.Identity8DD770());
        NativeBattleActionMatrix scaled = NativeBattlePoseMatrices.Scale56BEF0(multiplied, scaleX, 0, scaleZ) with { X = x, Y = y, Z = focal };
        return new(NativeBattlePackedMatrix32.FromMatrix(scaled, null), divisorX, divisorZ, scaleX, scaleZ);
    }

    private static short Word(NativeBattleEffectArena source, int offset) => BinaryPrimitives.ReadInt16LittleEndian(source.Read(offset, 2));
}
