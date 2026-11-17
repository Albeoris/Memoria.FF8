using System.Numerics;

namespace Memoria.FF8.Runtime;

/// <summary>Corner sampling for one pair of immutable battle geometry endpoints.</summary>
public readonly struct NativeBattlePilotCornerSampler
{
    internal enum Selection : byte
    {
        Earlier,
        Later,
        EarlierCorners,
        Interpolate,
        Sprite
    }

    private readonly NativeBattlePilotGeometryReference first;
    private readonly NativeBattlePilotGeometryReference later;
    private readonly float alpha;
    private readonly Selection selection;
    private readonly NativeBattlePilotSpriteGeometry? earlierSprite;
    private readonly NativeBattlePilotSpriteGeometry? laterSprite;

    internal NativeBattlePilotCornerSampler(NativeBattlePilotGeometryReference earlier, NativeBattlePilotGeometryReference later, float alpha, Selection selection, NativeBattlePilotSpriteGeometry? earlierSprite = null, NativeBattlePilotSpriteGeometry? laterSprite = null)
    {
        first = selection == Selection.Later ? later : earlier;
        this.later = later;
        this.alpha = alpha;
        this.selection = selection;
        this.earlierSprite = earlierSprite;
        this.laterSprite = laterSprite;
    }

    internal bool KeepsEarlierGeometry => selection == Selection.Earlier;
    internal bool KeepsLaterGeometry => selection == Selection.Later;

    public Vector3 SampleCorner(int corner)
    {
        if (selection == Selection.Interpolate)
        {
            return Vector3.Lerp(first.Corner(corner), later.Corner(corner), alpha);
        }
        if (selection == Selection.Sprite)
        {
            NativeBattlePilotSpriteGeometry a = earlierSprite!;
            NativeBattlePilotSpriteGeometry b = laterSprite!;
            Vector3 local = a.LocalCorner(corner);
            return Vector3.Lerp(a.Origin, b.Origin, alpha) + local.X * Vector3.Lerp(a.AxisX, b.AxisX, alpha) + local.Y * Vector3.Lerp(a.AxisY, b.AxisY, alpha) + local.Z * Vector3.Lerp(a.AxisZ, b.AxisZ, alpha);
        }
        return first.Corner(corner);
    }
}
