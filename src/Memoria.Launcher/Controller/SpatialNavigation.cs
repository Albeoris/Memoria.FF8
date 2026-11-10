using Avalonia;

namespace Memoria.Launcher.Controller;

internal static class SpatialNavigation
{
    public static T? FindNext<T>(Rect origin, IEnumerable<(T Value, Rect Bounds)> candidates, ControllerAction direction) where T : class
    {
        Rect from = Orient(origin, direction);
        T? best = null;
        double bestScore = double.PositiveInfinity;
        foreach ((T value, Rect bounds) in candidates)
        {
            Rect to = Orient(bounds, direction);

            // Compare the facing edges so a wider control in the same column
            // cannot steal a horizontal move just because its center is offset.
            double forwardGap = to.Left - from.Right;
            if (forwardGap < -1 || to.Center.X - from.Center.X <= 1)
            {
                continue;
            }

            double sideGap = Math.Max(0, Math.Max(to.Top - from.Bottom, from.Top - to.Bottom));
            double sideOffset = Math.Abs(to.Center.Y - from.Center.Y);
            double score = Math.Max(0, forwardGap) + 5 * sideGap + 0.01 * sideOffset;
            if (score < bestScore)
            {
                best = value;
                bestScore = score;
            }
        }
        return best;
    }

    private static Rect Orient(Rect bounds, ControllerAction direction) => direction switch
    {
        ControllerAction.Right => bounds,
        ControllerAction.Left => new Rect(-bounds.Right, bounds.Y, bounds.Width, bounds.Height),
        ControllerAction.Down => new Rect(bounds.Y, bounds.X, bounds.Height, bounds.Width),
        ControllerAction.Up => new Rect(-bounds.Bottom, bounds.X, bounds.Height, bounds.Width),
        _ => throw new ArgumentOutOfRangeException(nameof(direction))
    };
}
