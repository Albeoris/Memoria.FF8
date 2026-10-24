using System.Numerics;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Rendering;

public readonly record struct WorldMapVertex(Vector2 Position, Vector2 Uv, Vector4 Color);
public readonly record struct WorldMapTriangle(WorldMapVertex A, WorldMapVertex B, WorldMapVertex C);
public sealed record WorldMapLayer(int TextureMember, int Palette, bool HalfTransparent, bool Wrap, IReadOnlyList<WorldMapTriangle> Triangles)
{
    public bool Additive { get; init; }
}

/// <summary>Native logical 320x224 world-map geometry and its independent screen-space presentation.</summary>
public static class WorldMapPresentation
{
    public static Vector2 MapPosition(Vector2 nativePosition)
    {
        int x = (((int)nativePosition.X + 393216) % 262144) >> 11;
        int z = (((int)nativePosition.Y + 294912) % 196608) >> 11;
        return new Vector2(x, z);
    }

    public static IReadOnlyList<WorldMapLayer> Create(byte mode, Vector2 nativePosition, WorldGlobeGeometry? globe = null)
    {
        if (mode > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }
        if (mode == 0)
        {
            return Array.Empty<WorldMapLayer>();
        }
        if (mode is 2 or 3)
        {
            Vector2 origin = mode == 2 ? new Vector2(186, 124) : new Vector2(32, 16);
            Vector2 size = mode == 2 ? new Vector2(128, 96) : new Vector2(256, 192);
            return [new(11, mode == 2 ? 1 : 0, mode == 2, false, Quad(origin, size, new Vector2(0.5f / 256, 0.5f / 192), new Vector2(255.5f / 256, 191.5f / 192), Vector4.One))];
        }
        ArgumentNullException.ThrowIfNull(globe);
        Vector2 center = new(260, 180);
        Vector2 textureOrigin = new(((int)nativePosition.X + 131072) / 1024, ((int)nativePosition.Y + 98304) / 1024);
        WorldMapVertex Convert(int index)
        {
            WorldGlobeVertex vertex = globe.Vertices[index];
            Vector2 position = center + vertex.Position;
            int radius = (int)MathF.Sqrt(Vector2.DistanceSquared(center + new Vector2(16), position));
            byte brightness = unchecked((byte)(-1 - 4 * radius));
            Vector2 uv = textureOrigin + vertex.TextureOffset * 2;
            return new(position, new Vector2((uv.X + 0.5f) / 256, (uv.Y + 0.5f) / 192), new Vector4(new Vector3(brightness / 128f), 1));
        }
        List<WorldMapTriangle> triangles = [];
        foreach (WorldGlobeFace face in globe.Faces)
        {
            WorldMapVertex a = Convert(face.Vertices[0]);
            WorldMapVertex b = Convert(face.Vertices[1]);
            WorldMapVertex c = Convert(face.Vertices[2]);
            triangles.Add(new(a, b, c));
            if (face.Vertices.Count == 4)
            {
                triangles.Add(new(c, b, Convert(face.Vertices[3])));
            }
        }
        return [new(11, 1, false, true, triangles.AsReadOnly())];
    }

    public static IReadOnlyList<WorldMapLayer> Markers(byte mode, Vector2 nativePosition, float bodyHeading, float cameraHeading, long frame, bool showBodyMarker)
    {
        if (mode == 0)
        {
            return Array.Empty<WorldMapLayer>();
        }
        Vector2 location = MapPosition(nativePosition);
        Vector2 position = mode == 1 ? new Vector2(260, 180) : mode == 2 ? new Vector2(186, 124) + location : new Vector2(32, 16) + location * 2;
        List<WorldMapLayer> layers = [];
        void Marker(int member, Vector2 origin, Vector2 size, float angle, float brightness, bool halfTransparent)
        {
            Vector4 color = new(new Vector3(brightness), 1);
            IReadOnlyList<WorldMapTriangle> source = Quad(origin, size, new Vector2(0.5f / 16), new Vector2(15.5f / 16), color);
            WorldMapVertex Rotate(WorldMapVertex vertex)
            {
                Vector2 rotated = Vector2.Transform(vertex.Position, Matrix3x2.CreateRotation(angle));
                return vertex with { Position = position + rotated };
            }
            layers.Add(new(member, 0, halfTransparent, false, source.Select(triangle => new WorldMapTriangle(Rotate(triangle.A), Rotate(triangle.B), Rotate(triangle.C))).ToArray()) { Additive = member == 24 });
        }
        int phase = (int)(frame & 31);
        if (showBodyMarker)
        {
            Marker(25, new Vector2(-13, -8), new Vector2(15), bodyHeading + MathF.PI / 2, (Math.Abs(8 * phase - 128) / 2 + 64) / 128f, false);
        }
        Marker(24, new Vector2(-1), new Vector2(15), cameraHeading - 3 * MathF.PI / 4, 1, true);
        return layers.AsReadOnly();
    }

    private static IReadOnlyList<WorldMapTriangle> Quad(Vector2 origin, Vector2 size, Vector2 uvStart, Vector2 uvEnd, Vector4 color)
    {
        WorldMapVertex a = new(origin, uvStart, color);
        WorldMapVertex b = new(origin + new Vector2(size.X, 0), new Vector2(uvEnd.X, uvStart.Y), color);
        WorldMapVertex c = new(origin + new Vector2(0, size.Y), new Vector2(uvStart.X, uvEnd.Y), color);
        WorldMapVertex d = new(origin + size, uvEnd, color);
        return [new(a, b, c), new(c, b, d)];
    }
}
