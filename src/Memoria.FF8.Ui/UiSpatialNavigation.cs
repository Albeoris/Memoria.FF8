namespace Memoria.FF8.Ui;

/// <summary>Chooses neighbours across the requested edge, preferring aligned rows and columns.</summary>
internal static class UiSpatialNavigation
{
    public static UiElement? FindNext(UiElement origin, IEnumerable<UiElement> candidates, UiKey direction)
    {
        bool horizontal = direction is UiKey.Left or UiKey.Right;
        UiRect source = origin.Bounds;
        UiElement? best = null;
        float bestScore = float.PositiveInfinity;
        foreach (UiElement candidate in candidates)
        {
            if (ReferenceEquals(origin, candidate))
            {
                continue;
            }
            UiRect bounds = candidate.Bounds;
            float gap = direction switch
            {
                UiKey.Left => source.X - bounds.Right,
                UiKey.Right => bounds.X - source.Right,
                UiKey.Up => source.Y - bounds.Bottom,
                UiKey.Down => bounds.Y - source.Bottom,
                _ => throw new ArgumentOutOfRangeException(nameof(direction))
            };
            float center = direction switch
            {
                UiKey.Left => source.X + source.Width / 2 - bounds.X - bounds.Width / 2,
                UiKey.Right => bounds.X + bounds.Width / 2 - source.X - source.Width / 2,
                UiKey.Up => source.Y + source.Height / 2 - bounds.Y - bounds.Height / 2,
                _ => bounds.Y + bounds.Height / 2 - source.Y - source.Height / 2
            };
            if (bounds.IsEmpty || gap < -1 || center <= 1)
            {
                continue;
            }
            float sourceStart = horizontal ? source.Y : source.X;
            float sourceEnd = horizontal ? source.Bottom : source.Right;
            float targetStart = horizontal ? bounds.Y : bounds.X;
            float targetEnd = horizontal ? bounds.Bottom : bounds.Right;
            float perpendicularGap = Math.Max(0, Math.Max(targetStart - sourceEnd, sourceStart - targetEnd));
            float perpendicularCenter = Math.Abs((sourceStart + sourceEnd - targetStart - targetEnd) / 2);
            float score = Math.Max(0, gap) + perpendicularGap * 5 + perpendicularCenter * 0.01f;
            if (score < bestScore)
            {
                best = candidate;
                bestScore = score;
            }
        }
        return best;
    }
}
