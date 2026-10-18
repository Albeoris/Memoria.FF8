namespace Memoria.FF8.Runtime;

public sealed record NativeSoftwareViewportInputs(int Width, int Height, int OffsetX, int OffsetY);
public sealed record NativeSoftwareNormalViewportDerived(float ScaleX, float ScaleY, float LowCoordinateScaleX, float LowCoordinateScaleY, uint ScaleFlags, float NormalOffsetX, float NormalOffsetY);

public static class NativeSoftwareNormalViewport
{
    public static NativeSoftwareNormalViewportDerived Calculate45B6F0(NativeSoftwareDrawingEnvironmentState environment, NativeSoftwareViewportInputs viewport)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(viewport);
        NativeSoftwareEnvironmentDerived derived = environment.Derived ?? throw new NotSupportedException("Normal viewport outputs require the actual recalculated drawing environment.");
        if (environment.TopLeftX is not short left || environment.TopLeftY is not short top || environment.OffsetX is not short offsetX || environment.OffsetY is not short offsetY)
        {
            throw new NotSupportedException("Normal viewport outputs require retained top-left and drawing offset owners.");
        }
        if (derived.Mode is < 1 or > 3)
        {
            throw new NotSupportedException("Normal viewport outputs require an actual supported drawing environment mode.");
        }
        short width = unchecked((short)derived.Width);
        short height = unchecked((short)derived.Height);
        int denominatorWidth = width == 0 ? 1 : width;
        int denominatorHeight = height == 0 ? 1 : height;
        float scaleX = (float)((double)viewport.Width / denominatorWidth);
        double extendedScaleY = (double)viewport.Height / denominatorHeight;
        float scaleY = (float)extendedScaleY;
        float lowScaleX = (float)((double)scaleX * 0.125);
        float lowScaleY = (float)(extendedScaleY * 0.125);
        uint flags = (scaleX > 1.001 ? 1u : 0u) | (extendedScaleY > 1.001 ? 2u : 0u);
        double normalX = viewport.OffsetX;
        double normalY = viewport.OffsetY;
        if (derived.Mode == 1)
        {
            normalX += (offsetX - left) * (double)scaleX;
            normalY += (offsetY - top) * (double)scaleY;
        }
        float outputX = (float)(normalX - 0.5);
        float storedY = (float)normalY;
        float outputY = (float)((double)storedY - 0.5);
        return new(scaleX, scaleY, lowScaleX, lowScaleY, flags, outputX, outputY);
    }
}
