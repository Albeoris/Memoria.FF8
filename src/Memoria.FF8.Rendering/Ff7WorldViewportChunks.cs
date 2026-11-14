using System.Numerics;
using Memoria.FF7.Resources;

namespace Memoria.FF8.Rendering;

/// <summary>Extends the original world chunk selection to the actual horizontal viewport.</summary>
public static class Ff7WorldViewportChunks
{
    public static IReadOnlyList<Ff7WorldChunkPlacement> Extend(IReadOnlyList<Ff7WorldChunkPlacement> nativeChunks, Vector3 focus, Ff7WorldView view, float farDepth, int worldState = 0, bool underwater = false, bool snow = false)
    {
        if (view.Width * 3L <= view.Height * 4L)
        {
            return nativeChunks;
        }
        if (!float.IsFinite(farDepth) || farDepth < 1 || !Matrix4x4.Invert(view.NativeView, out Matrix4x4 inverse))
        {
            throw new ArgumentException("World viewport coverage requires an invertible camera and positive native depth.");
        }
        Vector3 minimum = new(float.PositiveInfinity);
        Vector3 maximum = new(float.NegativeInfinity);
        foreach (float depth in new[] { 1f, farDepth })
        {
            foreach (int x in new[] { 0, view.Width })
            {
                foreach (int y in new[] { 0, view.Height })
                {
                    Vector3 camera = new((x - view.Width / 2f - view.OffsetX) * depth / view.Focal, (y - view.CenterY) * depth / view.Focal, depth);
                    Vector3 world = Vector3.Transform(camera, inverse);
                    minimum = Vector3.Min(minimum, world);
                    maximum = Vector3.Max(maximum, world);
                }
            }
        }
        int focusX = (int)focus.X >> 13;
        int focusZ = (int)focus.Z >> 13;
        int firstX = Math.Max(focusX - 18, (int)MathF.Floor(minimum.X / 8192));
        int lastX = Math.Min(focusX + 17, (int)MathF.Floor(maximum.X / 8192));
        int firstZ = Math.Max(focusZ - 14, (int)MathF.Floor(minimum.Z / 8192));
        int lastZ = Math.Min(focusZ + 13, (int)MathF.Floor(maximum.Z / 8192));
        List<Ff7WorldChunkPlacement> result = new(nativeChunks);
        HashSet<Vector3> included = nativeChunks.Select(chunk => chunk.Origin).ToHashSet();
        for (int z = firstZ; z <= lastZ; z++)
        {
            for (int x = firstX; x <= lastX; x++)
            {
                Vector3 origin = new(x * 8192, 0, z * 8192);
                if (!included.Add(origin))
                {
                    continue;
                }
                int actualX = (x % 36 + 36) % 36;
                int actualZ = (z % 28 + 28) % 28;
                if (snow && !Ff7WorldVisibility.TryResolveSnowBlock(actualX, actualZ, out _))
                {
                    continue;
                }
                int block = underwater ? Ff7WorldVisibility.ResolveUnderwaterBlock(actualX, actualZ) : snow ? Ff7WorldVisibility.ResolveSnowBlock(actualX, actualZ) : Ff7WorldVisibility.ResolveBlockIndex((actualZ >> 2) * 9 + (actualX >> 2), worldState);
                result.Add(new(actualX, actualZ, block, (actualZ & 3) * 4 + (actualX & 3), origin));
            }
        }
        return result.AsReadOnly();
    }
}
