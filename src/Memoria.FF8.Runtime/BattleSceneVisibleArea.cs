using System.Threading;

namespace Memoria.FF8.Runtime;

/// <summary>The visible scene rectangle in native eighth-pixel projection coordinates.</summary>
public readonly record struct BattleSceneVisibleArea(double Left, double Top, double Right, double Bottom)
{
    private static readonly AsyncLocal<BattleSceneVisibleArea?> Active = new();
    public static BattleSceneVisibleArea Native => new(0, 0, 2560, 1728);
    public static BattleSceneVisibleArea Current => Active.Value ?? Native;
    public static bool IsScoped => Active.Value.HasValue;

    public static BattleSceneVisibleArea FromViewport(double width, double height, double projectionX, double projectionY, double projectionWidth, double projectionHeight)
    {
        if (!double.IsFinite(width) || !double.IsFinite(height) || !double.IsFinite(projectionX) || !double.IsFinite(projectionY) || !double.IsFinite(projectionWidth) || !double.IsFinite(projectionHeight) || width <= 0 || height <= 0 || projectionWidth <= 0 || projectionHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "The scene and projection rectangles must be finite with positive dimensions.");
        }
        return new(-projectionX * 2560 / projectionWidth, -projectionY * 1728 / projectionHeight, (width - projectionX) * 2560 / projectionWidth, (height - projectionY) * 1728 / projectionHeight);
    }

    public static IDisposable Begin(BattleSceneVisibleArea area)
    {
        if (!double.IsFinite(area.Left) || !double.IsFinite(area.Top) || !double.IsFinite(area.Right) || !double.IsFinite(area.Bottom) || area.Right < area.Left || area.Bottom < area.Top)
        {
            throw new ArgumentOutOfRangeException(nameof(area));
        }
        BattleSceneVisibleArea? previous = Active.Value;
        Active.Value = area;
        return new Scope(previous);
    }

    public static bool RejectPacked(IEnumerable<uint> screens)
    {
        IEnumerable<BattleSceneScreenPoint> points = screens.Select(screen => new BattleSceneScreenPoint(unchecked((short)screen), unchecked((short)(screen >> 16))));
        return Current == Native ? Native.RejectNative(points) : Current.Reject(points);
    }

    public bool Reject(IEnumerable<BattleSceneScreenPoint> points)
    {
        bool left = true;
        bool right = true;
        bool top = true;
        bool bottom = true;
        bool any = false;
        foreach (BattleSceneScreenPoint point in points)
        {
            any = true;
            left &= point.X < Left;
            right &= point.X > Right;
            top &= point.Y < Top;
            bottom &= point.Y > Bottom;
        }
        return any && (left || right || top || bottom);
    }

    internal bool RejectNative(IEnumerable<BattleSceneScreenPoint> points)
    {
        bool outsideX = true;
        bool outsideY = true;
        bool any = false;
        foreach (BattleSceneScreenPoint point in points)
        {
            any = true;
            outsideX &= point.X < Left || point.X > Right;
            outsideY &= point.Y < Top || point.Y > Bottom;
        }
        return any && (outsideX || outsideY);
    }

    private sealed class Scope(BattleSceneVisibleArea? previous) : IDisposable
    {
        private bool disposed;
        public void Dispose()
        {
            if (!disposed)
            {
                Active.Value = previous;
                disposed = true;
            }
        }
    }
}
