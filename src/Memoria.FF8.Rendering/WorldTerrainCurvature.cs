namespace Memoria.FF8.Rendering;

/// <summary>Continuous presentation of the native terrain horizon curve; simulation keeps its integer projection.</summary>
public static class WorldTerrainCurvature
{
    public static float PresentationHeight(float nativeHeight, float cameraDepth)
    {
        float distance = Math.Clamp(cameraDepth, 0, 65535) * 0.25f - 5216;
        if (distance < 0)
        {
            return nativeHeight;
        }
        float squareRoot = distance / 64;
        float height = squareRoot * squareRoot + (4096 - distance) * nativeHeight / 4096;
        return height - 65536 * MathF.Floor((height + 32768) / 65536);
    }
}
