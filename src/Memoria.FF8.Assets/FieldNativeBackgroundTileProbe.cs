using System.Collections.Immutable;

namespace Memoria.FF8.Assets;

public readonly record struct FieldNativeBackgroundWrapLayer(byte Layer, short OffsetX, short OffsetY, short PeriodX, short PeriodY);
public readonly record struct FieldNativeBackgroundLayerCurrent(byte Layer, short X, short Y, short OffsetX, short OffsetY, short ScaleX, short ScaleY);
public sealed record FieldNativeBackgroundWrapFrame(int FieldId, string ProgramHash, short Left, short Right, short Top, short Bottom, ImmutableArray<FieldNativeBackgroundWrapLayer> Layers, int NativeNormalVersion = 0, ImmutableArray<FieldNativeBackgroundLayerCurrent> LayerCurrents = default);
public readonly record struct FieldNativeBackgroundTileOrigin(short X, short Y);

/// <summary>Native475480 signed tile-origin relocation and strict origin culling, before eighth-pixel packing.</summary>
public static class FieldNativeBackgroundTileProbe
{
    public static FieldNativeBackgroundTileOrigin? Relocate(short tileX, short tileY, FieldNativeBackgroundWrapLayer layer, FieldNativeBackgroundWrapFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        short x = unchecked((short)(tileX + layer.OffsetX));
        short y = unchecked((short)(tileY + layer.OffsetY));
        // A clear INF mask skips period relocation, but native475480 still
        // reaches the same strict origin culling before assigning CLUT ranks.
        if (layer.PeriodX != 0 && x < frame.Left)
        {
            x = unchecked((short)(x + layer.PeriodX));
        }
        else if (layer.PeriodX != 0 && x > frame.Right)
        {
            x = unchecked((short)(x - layer.PeriodX));
        }
        if (layer.PeriodY != 0 && y < frame.Top)
        {
            y = unchecked((short)(y + layer.PeriodY));
        }
        else if (layer.PeriodY != 0 && y > frame.Bottom)
        {
            y = unchecked((short)(y - layer.PeriodY));
        }
        if (x <= frame.Left || x >= frame.Right || y <= frame.Top || y >= frame.Bottom)
        {
            return null;
        }
        return new FieldNativeBackgroundTileOrigin(x, y);
    }
}
