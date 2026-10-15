using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Rendering;

/// <summary>Camera dimensions are in WMX/16 units. Caller supplies the researched camera configuration.</summary>
public readonly record struct WorldCamera(float Heading, float Distance, float Height, float TargetHeight, float FieldOfView, float FarDistance, int SkyIndex = 0, bool NativeFoot = false)
{
    public Vector3? TargetPosition { get; init; }
    public WorldCameraFrame? ScriptedFrame { get; init; }
    public int? NativeOffsetZ { get; init; }
    public int? NativeOffsetY { get; init; }
    public short? NativePitch { get; init; }
    public bool NativeProjection => NativeFoot || ScriptedFrame.HasValue;

    public static WorldCamera ScriptedPath(NativeWorldPathCamera camera, float farDistance, int skyIndex = 0)
    {
        NativeWorldPathPosition nativeEye = camera.Eye ?? throw new InvalidDataException("Scripted render camera has no executed eye.");
        int dx = unchecked((short)(camera.Target.X - nativeEye.X));
        int dy = unchecked((short)(camera.Target.Y - nativeEye.Y));
        int dz = unchecked((short)(camera.Target.Z - nativeEye.Z));
        long squared = (long)dx * dx + (long)dy * dy + (long)dz * dz;
        long horizontal = (long)dx * dx + (long)dz * dz;
        if (squared is <= 0 or > int.MaxValue || horizontal > int.MaxValue)
        {
            throw new NotSupportedException("Native scripted camera distance exceeds the proved signed square-root contract.");
        }
        int distance = (int)Math.Sqrt(squared);
        if (distance > short.MaxValue)
        {
            throw new NotSupportedException("Native scripted camera distance exceeds its signed WORD.");
        }
        short pitch = unchecked((short)NativeWorldScriptedPathMath.NativeAngle(-dy, (int)Math.Sqrt(horizontal)));
        short yaw = unchecked((short)NativeWorldScriptedPathMath.NativeAngle(dx, dz));
        const float radiansPerUnit = 0.0015339808305725455f;
        Matrix4x4 rotation = Matrix4x4.CreateRotationX(pitch * radiansPerUnit) * Matrix4x4.CreateRotationY(yaw * radiansPerUnit);
        static Vector3 Canonical(Vector3 native) => new(native.X, -native.Y, -native.Z);
        Vector3 target = Canonical(new Vector3(camera.Target.X, camera.Target.Y, camera.Target.Z));
        Vector3 eye = target + Canonical(Vector3.Transform(new Vector3(0, 0, -distance), rotation));
        Vector3 forward = Canonical(Vector3.TransformNormal(Vector3.UnitZ, rotation));
        Vector3 up = -Canonical(Vector3.TransformNormal(Vector3.UnitY, rotation));
        return new WorldCamera(yaw * radiansPerUnit, distance / 16f, 1, 0, 2 * MathF.Atan(112f / 1024), farDistance, skyIndex)
        {
            TargetPosition = target,
            ScriptedFrame = new WorldCameraFrame(eye, forward, up),
            NativeOffsetZ = -distance
        };
    }

    public static WorldCamera Foot(int nativeHeading, float farDistance, int skyIndex = 0)
    {
        return new WorldCamera(nativeHeading * MathF.Tau / 4096, 448, 341f / 16, 16, 2 * MathF.Atan(112f / 1024), farDistance, skyIndex, true);
    }

    public WorldCameraFrame FootFrame(Vector3 playerPosition)
    {
        Matrix4x4 rotation = Matrix4x4.CreateRotationX((NativePitch ?? -112) * MathF.Tau / 4096) * Matrix4x4.CreateRotationY(Heading);
        static Vector3 Canonical(Vector3 native) => new(native.X, -native.Y, -native.Z);
        Vector3 target = TargetPosition ?? playerPosition + Vector3.UnitY * 256;
        Vector3 eye = target + Canonical(Vector3.Transform(new Vector3(0, NativeOffsetY ?? -341, NativeOffsetZ ?? -7168), rotation));
        Vector3 forward = Canonical(Vector3.TransformNormal(Vector3.UnitZ, rotation));
        Vector3 up = -Canonical(Vector3.TransformNormal(Vector3.UnitY, rotation));
        return new WorldCameraFrame(eye, forward, up);
    }
}

public readonly record struct WorldCameraFrame(Vector3 Eye, Vector3 Forward, Vector3 Up);

/// <summary>Canonical raw native anchor and proved rail orientation; the renderer divides the anchor by16.</summary>
public readonly record struct NativeWorldStaticModelPose(Vector3 Anchor, NativeWorldPathAngles Angles);

public readonly record struct NativeWorldStaticModelDraw(WorldStaticModel Model, NativeWorldStaticModelPose Pose);

/// <summary>Perspective rasterizer for selected original WMX terrain and world Squall; no installed resources are altered.</summary>
public sealed class WorldTerrainRenderer
{
    private readonly WorldAssets assets;
    private readonly byte[] pixels;
    private readonly float[] depth;
    private Matrix4x4 view;
    private float focal;
    private float far;
    private Rgba32 fog;
    private bool nativeTerrain;
    private readonly record struct Vertex(Vector3 Position, Vector2 Uv);
    private readonly record struct ScreenVertex(float X, float Y, float ReciprocalZ, Vector2 UvOverZ);
    private readonly record struct RasterTriangle(ScreenVertex A, ScreenVertex B, ScreenVertex C, PixelImage Texture, bool Transparent);
    private readonly List<RasterTriangle> rasterTriangles = [];

    public WorldTerrainRenderer(WorldAssets assets, int width, int height)
    {
        if (width < 1 || height < 1 || width > 8192 || height > 8192)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }
        this.assets = assets;
        Width = width;
        Height = height;
        pixels = new byte[checked(width * height * 4)];
        depth = new float[width * height];
    }

    public int Width { get; }
    public int Height { get; }
    public int DrawnTriangles { get; private set; }

    public byte[] Render(Vector3 position, float heading, int animationId, float animationFrame, WorldCamera camera, IReadOnlyDictionary<int, int>? replacements = null, WorldSky? skyOverride = null, bool avatarVisible = true, FieldModel? avatarModel = null, IReadOnlyList<NativeWorldStaticModelDraw>? staticModels = null, NativeWorldForestState? forest = null)
    {
        if (camera.Distance <= 0 || camera.Height <= 0 || camera.FieldOfView <= 0 || camera.FieldOfView >= MathF.PI || camera.FarDistance <= camera.Distance)
        {
            throw new ArgumentOutOfRangeException(nameof(camera));
        }
        Array.Fill(depth, float.NegativeInfinity);
        rasterTriangles.Clear();
        DrawnTriangles = 0;
        WorldSky sky = skyOverride ?? assets.Skies[camera.SkyIndex];
        DrawSky(sky, camera.NativeProjection ? WorldSkyProjection.Native(camera, position, sky) : null);
        fog = sky.Center;
        far = camera.FarDistance;
        nativeTerrain = camera.NativeProjection;
        Vector3 center = position / 16;
        Vector3 backward = new(MathF.Sin(camera.Heading), 0, MathF.Cos(camera.Heading));
        Vector3 eye = center - backward * camera.Distance + Vector3.UnitY * camera.Height;
        Vector3 target = center + Vector3.UnitY * camera.TargetHeight;
        if (camera.NativeProjection)
        {
            // Native column Ry(yaw)*Rx(pitch) becomes row Rx*Ry. Keep the optical axis;
            // looking at the orbit target would change the original pitch because offset Y is nonzero.
            WorldCameraFrame frame = camera.ScriptedFrame ?? camera.FootFrame(position);
            eye = frame.Eye / 16;
            view = Matrix4x4.CreateLookAt(eye, eye + frame.Forward, frame.Up);
        }
        else
        {
            view = Matrix4x4.CreateLookAt(eye, target, Vector3.UnitY);
        }
        focal = Height / (2 * MathF.Tan(camera.FieldOfView / 2));
        int segment = WorldAssets.SegmentAt(position.X, position.Z);
        int radius = Math.Min(4, (int)MathF.Ceiling(camera.FarDistance / 512));
        List<(WorldTriangle Triangle, Vector3 Offset)> transparent = [];
        for (int row = -radius; row <= radius; row++)
        {
            for (int column = -radius; column <= radius; column++)
            {
                int rawColumn = segment % 32 + column;
                int rawRow = segment / 32 + row;
                int mapId = ((rawRow % 24 + 24) % 24) * 32 + (rawColumn % 32 + 32) % 32;
                Vector3 offset = new(MathF.Floor(rawColumn / 32f) * 16384, 0, MathF.Floor(rawRow / 24f) * 12288);
                foreach (WorldTriangle triangle in assets.GetSegment(mapId, replacements).Triangles)
                {
                    if (triangle.Transparent)
                    {
                        transparent.Add((triangle, offset));
                    }
                    else
                    {
                        DrawTerrain(triangle, offset, false);
                    }
                }
            }
        }
        if (avatarVisible)
        {
            DrawAvatar(avatarModel ?? assets.Squall, position, heading, animationId, animationFrame);
        }
        if (staticModels is not null)
        {
            foreach (NativeWorldStaticModelDraw draw in staticModels)
            {
                DrawStaticModel(draw.Model, draw.Pose);
            }
        }
        foreach ((WorldTriangle triangle, Vector3 offset) in transparent.OrderByDescending(item => Vector3.DistanceSquared((item.Triangle.A + item.Triangle.B + item.Triangle.C) / 48 + item.Offset, eye)))
        {
            DrawTerrain(triangle, offset, true);
        }
        if (forest is not null)
        {
            DrawForest(forest, position);
        }
        int workers = Math.Clamp(Environment.ProcessorCount / 2, 1, 8);
        Parallel.For(0, workers, worker =>
        {
            int top = worker * Height / workers;
            int bottom = (worker + 1) * Height / workers - 1;
            foreach (RasterTriangle triangle in rasterTriangles)
            {
                Raster(triangle.A, triangle.B, triangle.C, triangle.Texture, triangle.Transparent, top, bottom);
            }
        });
        DrawnTriangles = rasterTriangles.Count;
        return pixels;
    }

    private void DrawForest(NativeWorldForestState forest, Vector3 position)
    {
        forest.Validate();
        foreach (NativeWorldForestParticle particle in forest.Particles.Where(particle => particle.Visible))
        {
            NativeWorldForestEffect effect = assets.ForestTables.Effects[particle.Type - 4];
            int frame = Math.Clamp(particle.Age / effect.FrameDuration, 0, effect.Frames - 1);
            PixelImage texture = assets.GetForestTexture(particle.Type, frame);
            int halfSize = particle.Scale * effect.HalfSize >> 13;
            Matrix4x4 rotation = Matrix4x4.CreateRotationZ(particle.Roll * 0.0015339808305725455f) * Matrix4x4.CreateRotationY(particle.Yaw * 0.0015339808305725455f);
            //555B80 ->5470E0 selects the nearest periodic image around the current world entity.
            float nearX = position.X + WorldAssets.Wrap(particle.X - position.X, 262144);
            float nearZ = position.Z + WorldAssets.Wrap(-particle.GraphicsZ - position.Z, 196608);
            Vector3 anchor = new(nearX, particle.NativeY, -nearZ);
            Vector3 Point(int x, int z)
            {
                Vector3 native = Vector3.Transform(new Vector3(x, 0, z), rotation) + anchor;
                return new Vector3(native.X, -native.Y, -native.Z) / 16;
            }
            Vertex a = new(Point(-halfSize, halfSize), Vector2.Zero);
            Vertex b = new(Point(halfSize, halfSize), Vector2.UnitX);
            Vertex c = new(Point(-halfSize, -halfSize), Vector2.UnitY);
            Vertex d = new(Point(halfSize, -halfSize), Vector2.One);
            //TODO(TD-008):. The shared CPU rasterizer
            //does not reproduce the original GPU ordering-table depth bias or fixed GTE rounding.
            Draw(a, b, c, texture, true);
            Draw(c, b, d, texture, true);
        }
    }

    private void DrawSky(WorldSky sky, WorldSkyGradient? nativeGradient)
    {
        for (int y = 0; y < Height; y++)
        {
            float fraction = y / (float)Math.Max(1, Height - 1);
            Rgba32 first = fraction < 0.5f ? sky.Top : sky.Center;
            Rgba32 second = fraction < 0.5f ? sky.Center : sky.Bottom;
            float blend = fraction < 0.5f ? fraction * 2 : fraction * 2 - 1;
            Rgba32 color = nativeGradient is WorldSkyGradient gradient ? gradient.Sample((y + 0.5) * WorldSkyProjection.LogicalHeight / Height) : new Rgba32((byte)MathF.Round(first.R + (second.R - first.R) * blend), (byte)MathF.Round(first.G + (second.G - first.G) * blend), (byte)MathF.Round(first.B + (second.B - first.B) * blend), 255);
            for (int x = 0; x < Width; x++)
            {
                int index = (y * Width + x) * 4;
                pixels[index] = color.R;
                pixels[index + 1] = color.G;
                pixels[index + 2] = color.B;
                pixels[index + 3] = 255;
            }
        }
    }

    private void DrawTerrain(WorldTriangle triangle, Vector3 offset, bool transparent)
    {
        Vector2 uvScale = triangle.Water ? new Vector2(1, 2) : Vector2.One;
        Vector3 TerrainPoint(Vector3 raw)
        {
            Vector3 point = raw / 16 + offset;
            if (nativeTerrain)
            {
                int cameraDepth = (int)MathF.Round(-Vector3.Transform(point, view).Z * 16);
                int distance = (Math.Clamp(cameraDepth, 0, 65535) >> 2) - 5216;
                if (distance >= 0)
                {
                    // Original 0x4022D0 lowers distant WMX geometry to make the horizon;
                    // 5216 is a quarter-depth threshold, not the terrain's far clip.
                    int nativeHeight = -(int)raw.Y;
                    int curvedHeight = (distance >> 6) * (distance >> 6) + (((4096 - distance) * nativeHeight) >> 12);
                    point.Y = -(short)curvedHeight / 16f;
                }
            }
            return point;
        }
        Draw(new Vertex(TerrainPoint(triangle.A), triangle.UvA * uvScale), new Vertex(TerrainPoint(triangle.B), triangle.UvB * uvScale), new Vertex(TerrainPoint(triangle.C), triangle.UvC * uvScale), assets.GetTexture(triangle), transparent);
    }

    private void DrawAvatar(FieldModel model, Vector3 position, float heading, int animationId, float frame)
    {
        Vector3[] pose = WorldModelPose.Pose(model, animationId, (int)frame, heading);
        Vector3[] vertices = pose.Select(point => point / 16 + position / 16).ToArray();
        foreach (ModelFace face in model.Faces)
        {
            PixelImage texture = model.Textures[face.Texture];
            Vector2 size = new(texture.Width, texture.Height);
            Vertex a = new(vertices[face.Vertices[0]], face.TextureCoordinates[0] / size);
            Vertex b = new(vertices[face.Vertices[1]], face.TextureCoordinates[1] / size);
            Vertex c = new(vertices[face.Vertices[2]], face.TextureCoordinates[2] / size);
            Draw(a, b, c, texture, false);
            if (face.Quad)
            {
                Vertex d = new(vertices[face.Vertices[3]], face.TextureCoordinates[3] / size);
                Draw(c, b, d, texture, false);
            }
        }
    }

    private void DrawStaticModel(WorldStaticModel model, NativeWorldStaticModelPose pose)
    {
        Matrix4x4 rotation = WorldStaticModelTransform.CreateRotation(model.ShapeId, pose.Angles);
        Vector3 position = pose.Anchor;
        Vector3[] vertices = model.NativeVertices.Select(vertex => Vector3.Transform(vertex, rotation)).Select(vertex => new Vector3(vertex.X, -vertex.Y, -vertex.Z) / 16 + position / 16).ToArray();
        foreach (WorldStaticFace face in model.Faces)
        {
            PixelImage texture = model.Textures[face.Palette];
            Vector2 size = new(texture.Width, texture.Height);
            Vertex a = new(vertices[face.Vertices[0]], face.TextureCoordinates[0] / size);
            Vertex b = new(vertices[face.Vertices[1]], face.TextureCoordinates[1] / size);
            Vertex c = new(vertices[face.Vertices[2]], face.TextureCoordinates[2] / size);
            Draw(a, b, c, texture, face.Transparent);
            if (face.Vertices.Length == 4)
            {
                Vertex d = new(vertices[face.Vertices[3]], face.TextureCoordinates[3] / size);
                Draw(c, b, d, texture, face.Transparent);
            }
        }
    }

    private void Draw(Vertex a, Vertex b, Vertex c, PixelImage texture, bool transparent)
    {
        Vertex[] input = [Transform(a), Transform(b), Transform(c)];
        List<Vertex> clipped = [];
        Vertex previous = input[^1];
        foreach (Vertex current in input)
        {
            bool currentInside = current.Position.Z >= 1;
            bool previousInside = previous.Position.Z >= 1;
            if (currentInside != previousInside)
            {
                float fraction = (1 - previous.Position.Z) / (current.Position.Z - previous.Position.Z);
                clipped.Add(new Vertex(Vector3.Lerp(previous.Position, current.Position, fraction), Vector2.Lerp(previous.Uv, current.Uv, fraction)));
            }
            if (currentInside)
            {
                clipped.Add(current);
            }
            previous = current;
        }
        for (int i = 1; i + 1 < clipped.Count; i++)
        {
            ScreenVertex first = Project(clipped[0]);
            ScreenVertex second = Project(clipped[i]);
            ScreenVertex third = Project(clipped[i + 1]);
            if (MathF.Max(first.X, MathF.Max(second.X, third.X)) < 0 || MathF.Min(first.X, MathF.Min(second.X, third.X)) > Width || MathF.Max(first.Y, MathF.Max(second.Y, third.Y)) < 0 || MathF.Min(first.Y, MathF.Min(second.Y, third.Y)) > Height)
            {
                continue;
            }
            rasterTriangles.Add(new RasterTriangle(first, second, third, texture, transparent));
        }
    }

    private Vertex Transform(Vertex vertex)
    {
        Vector3 camera = Vector3.Transform(vertex.Position, view);
        return vertex with { Position = new Vector3(camera.X, camera.Y, -camera.Z) };
    }

    private ScreenVertex Project(Vertex vertex)
    {
        float reciprocal = 1 / vertex.Position.Z;
        return new ScreenVertex(Width / 2f + vertex.Position.X * reciprocal * focal, Height / 2f - vertex.Position.Y * reciprocal * focal, reciprocal, vertex.Uv * reciprocal);
    }

    private void Raster(ScreenVertex a, ScreenVertex b, ScreenVertex c, PixelImage texture, bool transparent, int stripeTop, int stripeBottom)
    {
        float area = Edge(a.X, a.Y, b.X, b.Y, c.X, c.Y);
        if (MathF.Abs(area) < 0.001f)
        {
            return;
        }
        int left = Math.Max(0, (int)MathF.Floor(MathF.Min(a.X, MathF.Min(b.X, c.X))));
        int right = Math.Min(Width - 1, (int)MathF.Ceiling(MathF.Max(a.X, MathF.Max(b.X, c.X))));
        int top = Math.Max(stripeTop, (int)MathF.Floor(MathF.Min(a.Y, MathF.Min(b.Y, c.Y))));
        int bottom = Math.Min(stripeBottom, (int)MathF.Ceiling(MathF.Max(a.Y, MathF.Max(b.Y, c.Y))));
        float waStep = (c.Y - b.Y) / area;
        float wbStep = (a.Y - c.Y) / area;
        float wcStep = -waStep - wbStep;
        float reciprocalStep = waStep * a.ReciprocalZ + wbStep * b.ReciprocalZ + wcStep * c.ReciprocalZ;
        float uStep = waStep * a.UvOverZ.X + wbStep * b.UvOverZ.X + wcStep * c.UvOverZ.X;
        float vStep = waStep * a.UvOverZ.Y + wbStep * b.UvOverZ.Y + wcStep * c.UvOverZ.Y;
        for (int y = top; y <= bottom; y++)
        {
            float wa = Edge(b.X, b.Y, c.X, c.Y, left + 0.5f, y + 0.5f) / area;
            float wb = Edge(c.X, c.Y, a.X, a.Y, left + 0.5f, y + 0.5f) / area;
            float wc = 1 - wa - wb;
            float reciprocal = wa * a.ReciprocalZ + wb * b.ReciprocalZ + wc * c.ReciprocalZ;
            float uNumerator = wa * a.UvOverZ.X + wb * b.UvOverZ.X + wc * c.UvOverZ.X;
            float vNumerator = wa * a.UvOverZ.Y + wb * b.UvOverZ.Y + wc * c.UvOverZ.Y;
            for (int x = left; x <= right; x++, wa += waStep, wb += wbStep, wc += wcStep, reciprocal += reciprocalStep, uNumerator += uStep, vNumerator += vStep)
            {
                if (wa < 0 || wb < 0 || wc < 0)
                {
                    continue;
                }
                int index = y * Width + x;
                if (reciprocal <= depth[index] || reciprocal < 1 / far)
                {
                    continue;
                }
                float distance = 1 / reciprocal;
                int u = Math.Clamp((int)(uNumerator * distance * texture.Width), 0, texture.Width - 1);
                int v = Math.Clamp((int)(vNumerator * distance * texture.Height), 0, texture.Height - 1);
                Rgba32 color = texture.Pixels[v * texture.Width + u];
                if (color.A == 0)
                {
                    continue;
                }
                float haze = Math.Clamp((distance - far * 0.6f) / (far * 0.4f), 0, 1);
                float alpha = transparent ? color.A / 255f * 0.5f : 1;
                int pixel = index * 4;
                if (!transparent && haze == 0)
                {
                    pixels[pixel] = color.R;
                    pixels[pixel + 1] = color.G;
                    pixels[pixel + 2] = color.B;
                }
                else
                {
                    pixels[pixel] = Blend(pixels[pixel], color.R + (fog.R - color.R) * haze, alpha);
                    pixels[pixel + 1] = Blend(pixels[pixel + 1], color.G + (fog.G - color.G) * haze, alpha);
                    pixels[pixel + 2] = Blend(pixels[pixel + 2], color.B + (fog.B - color.B) * haze, alpha);
                }
                if (!transparent)
                {
                    depth[index] = reciprocal;
                }
            }
        }
    }

    private static byte Blend(byte background, float foreground, float alpha)
    {
        return (byte)Math.Clamp(MathF.Round(background + (foreground - background) * alpha), 0, 255);
    }

    private static float Edge(float ax, float ay, float bx, float by, float x, float y)
    {
        return (x - ax) * (by - ay) - (y - ay) * (bx - ax);
    }
}
