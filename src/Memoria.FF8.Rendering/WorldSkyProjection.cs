using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Rendering;

/// <summary>Logical 320x224 sky geometry from native 0x554F70 and 0x555450.</summary>
public static class WorldSkyProjection
{
    public const int LogicalWidth = 320;
    public const int LogicalHeight = 224;
    public const int FootFocalPixels = 1024;
    public const int FootOffsetZ = -7168;
    public const int FootCurvatureThreshold = 5216;

    /// <summary>Uses the renderer's foot optical basis. Native matrix-component rounding remains a separate parity boundary.</summary>
    public static WorldSkyGradient Foot(WorldCamera camera, Vector3 playerPosition, WorldSky sky)
    {
        if (!camera.NativeFoot)
        {
            throw new ArgumentException("Sky projection requires the researched native foot camera.", nameof(camera));
        }
        return Native(camera, playerPosition, sky);
    }

    public static WorldSkyGradient Native(WorldCamera camera, Vector3 playerPosition, WorldSky sky)
    {
        if (!camera.NativeProjection)
        {
            throw new ArgumentException("Sky projection requires an executed native camera configuration.", nameof(camera));
        }
        WorldCameraFrame frame = camera.ScriptedFrame ?? camera.FootFrame(playerPosition);
        int offsetZ = camera.NativeOffsetZ ?? FootOffsetZ;
        int distance = offsetZ + 4 * FootCurvatureThreshold;
        // Native 0x554FCC adds only player X/Z. Horizon point height is zero, not the filtered camera target height.
        Vector3 point = new(playerPosition.X + MathF.Sin(camera.Heading) * distance, 0, playerPosition.Z - MathF.Cos(camera.Heading) * distance);
        Vector3 relative = point - frame.Eye;
        double cameraY = -Vector3.Dot(relative, frame.Up);
        double cameraDepth = Vector3.Dot(relative, frame.Forward);
        return new WorldSkyGradient(ProjectHorizonEighthY(cameraY, cameraDepth, offsetZ), sky.Top, sky.Center, sky.Bottom);
    }

    /// <summary>Native projection rounds transformed coordinates and eighth-pixel output; half-even matches the bounded PC FPU assumption.</summary>
    public static int ProjectHorizonEighthY(double cameraY, double cameraDepth, int offsetZ = FootOffsetZ)
    {
        if (!double.IsFinite(cameraY) || !double.IsFinite(cameraDepth))
        {
            throw new ArgumentOutOfRangeException(nameof(cameraY), "Projected coordinates must be finite.");
        }
        double y = Math.Round(cameraY, MidpointRounding.ToEven);
        double depth = Math.Clamp(Math.Round(cameraDepth, MidpointRounding.ToEven), 0, ushort.MaxValue);
        double denominator = Math.Max(FootFocalPixels / 2, depth);
        double screenY = Math.Round(8 * y * FootFocalPixels / denominator, MidpointRounding.ToEven) + 8 * (LogicalHeight / 2);
        int boundedY = (int)Math.Clamp(screenY, -8192, 8184);
        // C# integer division preserves native truncation toward zero: 7168 / -300 == -23.
        return boundedY + 8 * (Math.Abs(offsetZ) / -300);
    }
}

/// <summary>Original sky color stops in logical pixels; offscreen stops retain their original positions.</summary>
public readonly record struct WorldSkyGradient(int HorizonEighthY, Rgba32 Top, Rgba32 Center, Rgba32 Bottom)
{
    public float TopY => 0;
    public float CenterY => (HorizonEighthY - 640) / 8f;
    public float BottomY => (HorizonEighthY + 192) / 8f;

    /// <summary>Continuous Gouraud sampling with nearest channel rounding; original PC rasterizer pixel parity is not asserted.</summary>
    public Rgba32 Sample(double logicalY)
    {
        if (!double.IsFinite(logicalY))
        {
            throw new ArgumentOutOfRangeException(nameof(logicalY));
        }
        if (logicalY >= BottomY)
        {
            return new Rgba32(Bottom.R, Bottom.G, Bottom.B, 255);
        }
        if (CenterY > 0 && logicalY < CenterY)
        {
            return Blend(Top, Center, logicalY / CenterY);
        }
        return Blend(Center, Bottom, (logicalY - CenterY) / (BottomY - CenterY));
    }

    private static Rgba32 Blend(Rgba32 first, Rgba32 second, double fraction)
    {
        fraction = Math.Clamp(fraction, 0, 1);
        byte Channel(byte start, byte end) => (byte)Math.Clamp(Math.Round(start + (end - start) * fraction, MidpointRounding.ToEven), 0, 255);
        return new Rgba32(Channel(first.R, second.R), Channel(first.G, second.G), Channel(first.B, second.B), 255);
    }
}
