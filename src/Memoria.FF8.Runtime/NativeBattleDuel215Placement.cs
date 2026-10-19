namespace Memoria.FF8.Runtime;

public readonly record struct NativeBattleDuel215TargetBounds(short X, short MinimumX, short MaximumX, short MaximumY);

public static partial class NativeBattleDuel215Controller
{
    public static int PlaceActor69D500(int center, int width, int height, IReadOnlyList<NativeBattleDuel215TargetBounds> targets)
    {
        int halfWidth = width >> 1;
        for (;;)
        {
            int left = unchecked(center - halfWidth);
            bool overlaps = false;
            foreach (NativeBattleDuel215TargetBounds target in targets)
            {
                if (target.MaximumY <= height)
                {
                    continue;
                }
                int minimum = unchecked(target.X + (target.MinimumX >> 1));
                if (minimum <= left)
                {
                    int maximum = unchecked(target.X + (target.MaximumX >> 1));
                    overlaps = maximum > left;
                }
                else
                {
                    overlaps = minimum < unchecked(center + halfWidth);
                }
                if (overlaps)
                {
                    break;
                }
            }
            if (!overlaps)
            {
                return center;
            }
            center = unchecked(center + 128);
        }
    }
}
