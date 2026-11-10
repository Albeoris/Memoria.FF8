using System.Numerics;

namespace Memoria.FF8.Rendering;

public readonly record struct CameraSpaceVertex(Vector3 Position, Vector2 Uv, Vector4? Color = null);
public readonly record struct CameraSpaceScreenVertex(Vector2 Position, float ReciprocalZ, Vector2 UvOverZ, Vector4? ColorOverZ);

/// <summary>Common camera-space clipping and perspective projection, before raster backend selection.</summary>
public static class CameraSpaceTriangleSubmission
{
    public static int ClipNear(ReadOnlySpan<CameraSpaceVertex> input, Span<CameraSpaceVertex> clipped)
    {
        if (input.Length != 3 || clipped.Length < 4)
        {
            throw new ArgumentException("Triangle near clipping requires three input and four output vertices.");
        }
        int clippedCount = 0;
        CameraSpaceVertex previous = input[^1];
        foreach (CameraSpaceVertex current in input)
        {
            bool currentInside = current.Position.Z >= 1;
            bool previousInside = previous.Position.Z >= 1;
            if (currentInside != previousInside)
            {
                float fraction = (1 - previous.Position.Z) / (current.Position.Z - previous.Position.Z);
                Vector4? color = previous.Color is { } first && current.Color is { } second ? Vector4.Lerp(first, second, fraction) : null;
                clipped[clippedCount++] = new CameraSpaceVertex(Vector3.Lerp(previous.Position, current.Position, fraction), Vector2.Lerp(previous.Uv, current.Uv, fraction), color);
            }
            if (currentInside)
            {
                clipped[clippedCount++] = current;
            }
            previous = current;
        }
        return clippedCount;
    }

    public static CameraSpaceScreenVertex Project(CameraSpaceVertex vertex, int width, int height, float focal)
    {
        float reciprocal = 1 / vertex.Position.Z;
        return new(new Vector2(width / 2f + vertex.Position.X * reciprocal * focal, height / 2f - vertex.Position.Y * reciprocal * focal), reciprocal, vertex.Uv * reciprocal, vertex.Color * reciprocal);
    }

    public static int Draw(ITriangleRenderer renderer, CameraSpaceVertex a, CameraSpaceVertex b, CameraSpaceVertex c, int width, int height, float focal, in TriangleMaterial material)
    {
        Span<CameraSpaceVertex> clipped = stackalloc CameraSpaceVertex[4];
        int count = ClipNear([a, b, c], clipped);
        int submitted = 0;
        for (int index = 1; index + 1 < count; index++)
        {
            TriangleVertex Vertex(CameraSpaceVertex source)
            {
                CameraSpaceScreenVertex vertex = Project(source, width, height, focal);
                return new(vertex.Position, vertex.ReciprocalZ, vertex.ReciprocalZ, vertex.UvOverZ / vertex.ReciprocalZ, vertex.ColorOverZ / vertex.ReciprocalZ ?? Vector4.One);
            }
            renderer.Draw(Vertex(clipped[0]), Vertex(clipped[index]), Vertex(clipped[index + 1]), material);
            submitted++;
        }
        return submitted;
    }
}
