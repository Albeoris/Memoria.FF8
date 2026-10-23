namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRibbonDirectionStep(int X, int Y, short? PreviousX, short? PreviousY, bool Valid);

public static class NativeBattleRibbonDirection
{
    public static NativeBattleRibbonDirectionStep Resolve(int dx, int dy, int distance, bool hasPrevious, short? previousX, short? previousY)
    {
        if (!hasPrevious)
        {
            if (distance == 0)
            {
                return new(0, 0, previousX, previousY, false);
            }
            int firstX = unchecked(dy << 12) / distance;
            int firstY = unchecked(dx << 12) / distance;
            return new(firstX, firstY, unchecked((short)firstX), unchecked((short)firstY), true);
        }
        int oldX = previousX ?? throw new NotSupportedException("Native ribbon direction requires its produced preceding X component.");
        int oldY = previousY ?? throw new NotSupportedException("Native ribbon direction requires its produced preceding Y component.");
        if (distance == 0)
        {
            return new(oldX, oldY, previousX, previousY, true);
        }
        int normalizedX = unchecked(dy << 12) / distance;
        int normalizedY = unchecked(dx << 12) / distance;
        int sumX = unchecked(normalizedX + oldX);
        int sumY = unchecked(normalizedY + oldY);
        if (sumX == 0 && sumY == 0)
        {
            sumX = unchecked(normalizedX * 2);
            sumY = unchecked(normalizedY * 2);
        }
        int factor = unchecked(0x4000 - (unchecked(unchecked(sumX * sumX) + unchecked(sumY * sumY)) >> 13));
        return new(unchecked(factor * sumX) >> 13, unchecked(factor * sumY) >> 13, unchecked((short)normalizedX), unchecked((short)normalizedY), true);
    }
}
