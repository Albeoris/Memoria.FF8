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

    public static WorldCamera Foot(int nativeHeading, float farDistance, int skyIndex = 0, int projectionDistance = 1024)
    {
        if (projectionDistance is < 640 or > 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(projectionDistance));
        }
        return new WorldCamera(nativeHeading * MathF.Tau / 4096, 448, 341f / 16, 16, 2 * MathF.Atan(112f / projectionDistance), farDistance, skyIndex, true);
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
public readonly record struct NativeWorldStaticModelPose(Vector3 Anchor, NativeWorldPathAngles Angles)
{
    public Matrix4x4? Rotation { get; init; }
}

public readonly record struct NativeWorldStaticModelDraw(WorldStaticModel Model, NativeWorldStaticModelPose Pose)
{
    public WorldRenderObjectId? ObjectId { get; init; }
}

public readonly record struct NativeWorldAnimatedModelDraw(FieldModel Model, Vector3 Position, float Heading, int AnimationId, float AnimationFrame)
{
    public WorldRenderObjectId? ObjectId { get; init; }
    public WorldAnimationBlend? AnimationBlend { get; init; }
}

/// <summary>Perspective renderer for WMX terrain and world models, with optional retained GPU geometry.</summary>
public sealed partial class WorldTerrainRenderer
{
    private readonly WorldAssets assets;
    private readonly byte[] pixels;
    private readonly float[] depth;
    private Matrix4x4 view;
    private float focal;
    private float far;
    private Rgba32 fog;
    private bool nativeTerrain;
    private uint? nativeMainLoopCounter;
    private byte? worldDiscNumber;
    private ushort terrainReplacementMask;
    private readonly record struct Vertex(Vector3 Position, Vector2 Uv, Vector4? Color = null);
    private readonly record struct ScreenVertex(float X, float Y, float ReciprocalZ, Vector2 UvOverZ, Vector4? ColorOverZ = null);
    private readonly record struct RasterTriangle(ScreenVertex A, ScreenVertex B, ScreenVertex C, PixelImage Texture, bool Transparent, TriangleBlend? Blend = null);
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
    /// <summary>Submitted triangles; retained hardware terrain is clipped by the GPU after submission.</summary>
    public int DrawnTriangles { get; private set; }
    public ITriangleRenderer? TriangleRenderer { get; set; }

    public byte[] Render(Vector3 position, float heading, int animationId, float animationFrame, WorldCamera camera, IReadOnlyDictionary<int, int>? replacements = null, WorldSky? skyOverride = null, bool avatarVisible = true, FieldModel? avatarModel = null, IReadOnlyList<NativeWorldStaticModelDraw>? staticModels = null, NativeWorldForestState? forest = null, IReadOnlyList<NativeWorldAnimatedModelDraw>? animatedModels = null)
    {
        terrainReplacementMask = 0;
        return RenderCore(position, heading, animationId, animationFrame, camera, replacements, skyOverride, avatarVisible, avatarModel, staticModels, forest, animatedModels, null, null);
    }

    public byte[] Render(WorldRenderFrame frame, IReadOnlyDictionary<int, int>? replacements = null)
    {
        ArgumentNullException.ThrowIfNull(frame);
        terrainReplacementMask = frame.TerrainReplacementMask;
        return RenderCore(frame.Position, frame.Heading, frame.AnimationId, frame.AnimationFrame, frame.Camera, replacements, frame.Sky, frame.AvatarVisible, frame.AvatarModel, frame.StaticModels, frame.Forest, frame.AnimatedModels, frame.AvatarAnimationBlend, frame.ForestParticles, frame);
    }

    private byte[] RenderCore(Vector3 position, float heading, int animationId, float animationFrame, WorldCamera camera, IReadOnlyDictionary<int, int>? replacements, WorldSky? skyOverride, bool avatarVisible, FieldModel? avatarModel, IReadOnlyList<NativeWorldStaticModelDraw>? staticModels, NativeWorldForestState? forest, IReadOnlyList<NativeWorldAnimatedModelDraw>? animatedModels, WorldAnimationBlend? avatarAnimationBlend, IReadOnlyList<WorldForestParticleFrame>? forestParticles, WorldRenderFrame? presentation = null)
    {
        if (camera.Distance <= 0 || camera.Height <= 0 || camera.FieldOfView <= 0 || camera.FieldOfView >= MathF.PI || camera.FarDistance <= camera.Distance)
        {
            throw new ArgumentOutOfRangeException(nameof(camera));
        }
        if (TriangleRenderer is null)
        {
            Array.Fill(depth, float.NegativeInfinity);
        }
        else
        {
            TriangleRenderer.Begin(Width, Height, new Vector4(0, 0, 0, 1), 0);
        }
        nativeMainLoopCounter = presentation?.NativeMainLoopCounter;
        worldDiscNumber = presentation?.WorldDiscNumber;
        rasterTriangles.Clear();
        DrawnTriangles = 0;
        WorldSky sky = skyOverride ?? assets.Skies[camera.SkyIndex];
        DrawSky(sky, camera.NativeProjection ? WorldSkyProjection.Native(camera, position, sky) : null);
        DrawWorldClouds(camera, position, sky, presentation?.MapProcessMode ?? 0);
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
                WorldSegment terrain = assets.GetSegment(mapId, replacements);
                if (TriangleRenderer is IWorldTerrainRenderer hardware)
                {
                    DrawRetainedTerrain(hardware, terrain, offset, transparent);
                    continue;
                }
                foreach (WorldTriangle triangle in terrain.Triangles)
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
        DrawWorldShadows(presentation?.Shadows);
        if (avatarVisible)
        {
            DrawAvatar(avatarModel ?? assets.Squall, position, heading, animationId, animationFrame, avatarAnimationBlend);
        }
        if (animatedModels is not null)
        {
            foreach (NativeWorldAnimatedModelDraw draw in animatedModels)
            {
                DrawAvatar(draw.Model, draw.Position, draw.Heading, draw.AnimationId, draw.AnimationFrame, draw.AnimationBlend);
            }
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
            DrawForest(forest, position, forestParticles);
        }
        if (TriangleRenderer is not null)
        {
            DrawWorldMapOverlay(presentation);
            TriangleRenderer.End();
            return [];
        }
        int workers = Math.Clamp(Environment.ProcessorCount / 2, 1, 8);
        Parallel.For(0, workers, worker =>
        {
            int top = worker * Height / workers;
            int bottom = (worker + 1) * Height / workers - 1;
            foreach (RasterTriangle triangle in rasterTriangles)
            {
                Raster(triangle.A, triangle.B, triangle.C, triangle.Texture, triangle.Transparent, top, bottom, triangle.Blend);
            }
        });
        DrawnTriangles += rasterTriangles.Count;
        DrawWorldMapOverlay(presentation);
        return pixels;
    }

    private void DrawForest(NativeWorldForestState forest, Vector3 position, IReadOnlyList<WorldForestParticleFrame>? presentation)
    {
        forest.Validate();
        if (!Matrix4x4.Invert(view, out Matrix4x4 inverseView))
        {
            throw new InvalidDataException("World particle camera has no inverse projection orientation.");
        }
        Vector3 cameraRight = Vector3.TransformNormal(Vector3.UnitX, inverseView);
        Vector3 cameraUp = Vector3.TransformNormal(Vector3.UnitY, inverseView);
        IEnumerable<WorldForestParticleFrame> particles = presentation ?? forest.Particles.Where(particle => particle.Visible).Select(particle => new WorldForestParticleFrame(particle, new Vector3(particle.X, particle.NativeY, -particle.GraphicsZ), Matrix4x4.CreateRotationZ(particle.Roll * 0.0015339808305725455f) * Matrix4x4.CreateRotationY(particle.Yaw * 0.0015339808305725455f) * Matrix4x4.CreateRotationX(particle.RotationX * 0.0015339808305725455f), particle.Scale, false)).ToArray();
        foreach (WorldForestParticleFrame visual in particles)
        {
            NativeWorldForestParticle particle = visual.Particle;
            NativeWorldForestEffect effect = assets.ForestTables.GetEffect(particle.Type);
            int frame = Math.Clamp(particle.Age / effect.FrameDuration, 0, effect.Frames - 1);
            PixelImage texture = assets.GetForestTexture(particle.Type, frame);
            float halfSize = particle.Scale * effect.HalfSize >> 13;
            if (visual.Interpolated)
            {
                float previousHalfSize = visual.PreviousScale * effect.HalfSize >> 13;
                halfSize = previousHalfSize + (halfSize - previousHalfSize) * visual.Blend;
            }
            Matrix4x4 rotation = visual.Rotation;
            //555B80 ->5470E0 selects the nearest periodic image around the current world entity.
            float nearX = position.X + WorldAssets.Wrap(visual.Position.X - position.X, 262144);
            float nearZ = position.Z + WorldAssets.Wrap(visual.Position.Z - position.Z, 196608);
            Vector3 anchor = new(nearX, visual.Position.Y, -nearZ);
            Vector3 Point(float x, float z)
            {
                if (effect.DrawMode == 2)
                {
                    return (new Vector3(anchor.X, -anchor.Y, -anchor.Z) + cameraRight * x + cameraUp * z) / 16;
                }
                if (effect.DrawMode == 3)
                {
                    Vector3 plane = Vector3.Transform(new Vector3(x, 0, 2 * z), rotation) + anchor;
                    return new Vector3(plane.X, -plane.Y, -plane.Z) / 16;
                }
                if (effect.DrawMode == 4)
                {
                    Vector3 vertical = Vector3.Transform(new Vector3(x, -z, 0), rotation) + anchor;
                    return new Vector3(vertical.X, -vertical.Y, -vertical.Z) / 16;
                }
                if (effect.DrawMode != 0)
                {
                    throw new NotSupportedException("This native particle plane mode has no represented current producer.");
                }
                Vector3 native = Vector3.Transform(new Vector3(x, 0, z), rotation) + anchor;
                return new Vector3(native.X, -native.Y, -native.Z) / 16;
            }
            Vertex a = new(Point(-halfSize, halfSize), Vector2.Zero);
            Vertex b = new(Point(halfSize, halfSize), Vector2.UnitX);
            Vertex c = new(Point(-halfSize, -halfSize), Vector2.UnitY);
            Vertex d = new(Point(halfSize, -halfSize), Vector2.One);
            //TODO(TD-008): The shared CPU rasterizer
            //does not reproduce the original GPU ordering-table depth bias or fixed GTE rounding.
            TriangleBlend blend = effect.BlendMode switch
            {
                0 => TriangleBlend.HalfAdd,
                1 => TriangleBlend.Add,
                2 => TriangleBlend.Subtract,
                3 => TriangleBlend.QuarterAdd,
                _ => throw new InvalidDataException("Native world particle has an invalid blend mode.")
            };
            Draw(a, b, c, texture, true, blend);
            Draw(c, b, d, texture, true, blend);
        }
    }

    private void DrawSky(WorldSky sky, WorldSkyGradient? nativeGradient)
    {
        if (TriangleRenderer is not null)
        {
            DrawGpuSky(sky, nativeGradient);
            return;
        }
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
        Draw(new Vertex(TerrainPoint(triangle.A), triangle.UvA * uvScale), new Vertex(TerrainPoint(triangle.B), triangle.UvB * uvScale), new Vertex(TerrainPoint(triangle.C), triangle.UvC * uvScale), assets.GetTexture(triangle, terrainReplacementMask), transparent);
    }

    private void DrawAvatar(FieldModel model, Vector3 position, float heading, int animationId, float frame, WorldAnimationBlend? animationBlend)
    {
        Vector3[] pose = animationBlend is WorldAnimationBlend blend ? WorldModelPose.PoseInterpolated(model, animationId, blend.PreviousFrameIndex, blend.CurrentFrameIndex, blend.Blend, heading) : WorldModelPose.Pose(model, animationId, (int)frame, heading);
        Vector3[] vertices = pose.Select(point => point / 16 + position / 16).ToArray();
        foreach (ModelFace face in model.Faces)
        {
            PixelImage texture = model.Textures[face.Texture];
            Vector2 size = ResourceTextureList.LogicalSize(model.Textures, face.Texture);
            Vertex a = new(vertices[face.Vertices[0]], face.TextureCoordinates[0] / size, face.VertexColors?[0]);
            Vertex b = new(vertices[face.Vertices[1]], face.TextureCoordinates[1] / size, face.VertexColors?[1]);
            Vertex c = new(vertices[face.Vertices[2]], face.TextureCoordinates[2] / size, face.VertexColors?[2]);
            Draw(a, b, c, texture, false);
            if (face.Quad)
            {
                Vertex d = new(vertices[face.Vertices[3]], face.TextureCoordinates[3] / size, face.VertexColors?[3]);
                Draw(c, b, d, texture, false);
            }
        }
    }

    private void DrawStaticModel(WorldStaticModel model, NativeWorldStaticModelPose pose)
    {
        Matrix4x4 rotation = pose.Rotation ?? WorldStaticModelTransform.CreateRotation(model.ShapeId, pose.Angles);
        Vector3 position = pose.Anchor;
        Vector3[] vertices = model.NativeVertices.Select(vertex => Vector3.Transform(vertex, rotation)).Select(vertex => new Vector3(vertex.X, -vertex.Y, -vertex.Z) / 16 + position / 16).ToArray();
        IReadOnlyList<PixelImage> textures = assets.ReadAnimatedStaticTextures(model, worldDiscNumber, nativeMainLoopCounter);
        foreach (WorldStaticFace face in model.Faces)
        {
            PixelImage texture = textures[face.Palette];
            Vector2 size = ResourceTextureList.LogicalSize(textures, face.Palette);
            Vertex a = new(vertices[face.Vertices[0]], face.TextureCoordinates[0] / size, face.VertexColors?[0]);
            Vertex b = new(vertices[face.Vertices[1]], face.TextureCoordinates[1] / size, face.VertexColors?[1]);
            Vertex c = new(vertices[face.Vertices[2]], face.TextureCoordinates[2] / size, face.VertexColors?[2]);
            TriangleBlend? blend = face.Transparent ? ((model.TexturePage >> 5) & 3) switch
            {
                0 => TriangleBlend.HalfAdd,
                1 => TriangleBlend.Add,
                2 => TriangleBlend.Subtract,
                3 => TriangleBlend.QuarterAdd,
                _ => throw new InvalidOperationException("Native texture-page blend leaves its two-bit field.")
            } : null;
            Draw(a, b, c, texture, face.Transparent, blend);
            if (face.Vertices.Length == 4)
            {
                Vertex d = new(vertices[face.Vertices[3]], face.TextureCoordinates[3] / size, face.VertexColors?[3]);
                Draw(c, b, d, texture, face.Transparent, blend);
            }
        }
    }

    private void Draw(Vertex a, Vertex b, Vertex c, PixelImage texture, bool transparent, TriangleBlend? blend = null)
    {
        ReadOnlySpan<Vertex> input = [Transform(a), Transform(b), Transform(c)];
        Span<Vertex> clipped = stackalloc Vertex[4];
        int clippedCount = 0;
        Vertex previous = input[^1];
        foreach (Vertex current in input)
        {
            bool currentInside = current.Position.Z >= 1;
            bool previousInside = previous.Position.Z >= 1;
            if (currentInside != previousInside)
            {
                float fraction = (1 - previous.Position.Z) / (current.Position.Z - previous.Position.Z);
                Vector4? color = previous.Color is { } first && current.Color is { } second ? Vector4.Lerp(first, second, fraction) : null;
                clipped[clippedCount++] = new Vertex(Vector3.Lerp(previous.Position, current.Position, fraction), Vector2.Lerp(previous.Uv, current.Uv, fraction), color);
            }
            if (currentInside)
            {
                clipped[clippedCount++] = current;
            }
            previous = current;
        }
        for (int i = 1; i + 1 < clippedCount; i++)
        {
            ScreenVertex first = Project(clipped[0]);
            ScreenVertex second = Project(clipped[i]);
            ScreenVertex third = Project(clipped[i + 1]);
            if (MathF.Max(first.X, MathF.Max(second.X, third.X)) < 0 || MathF.Min(first.X, MathF.Min(second.X, third.X)) > Width || MathF.Max(first.Y, MathF.Max(second.Y, third.Y)) < 0 || MathF.Min(first.Y, MathF.Min(second.Y, third.Y)) > Height)
            {
                continue;
            }
            if (TriangleRenderer is not null)
            {
                DrawGpuTriangle(first, second, third, texture, transparent, blend);
                DrawnTriangles++;
            }
            else
            {
                rasterTriangles.Add(new RasterTriangle(first, second, third, texture, transparent, blend));
            }
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
        return new ScreenVertex(Width / 2f + vertex.Position.X * reciprocal * focal, Height / 2f - vertex.Position.Y * reciprocal * focal, reciprocal, vertex.Uv * reciprocal, vertex.Color * reciprocal);
    }

    private void Raster(ScreenVertex a, ScreenVertex b, ScreenVertex c, PixelImage texture, bool transparent, int stripeTop, int stripeBottom, TriangleBlend? blend = null)
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
        bool IncludesEdge(ScreenVertex start, ScreenVertex end)
        {
            float dx = (end.X - start.X) * (area < 0 ? 1 : -1);
            float dy = (end.Y - start.Y) * (area < 0 ? 1 : -1);
            return dy < 0 || dy == 0 && dx > 0;
        }
        bool includeA = IncludesEdge(b, c);
        bool includeB = IncludesEdge(c, a);
        bool includeC = IncludesEdge(a, b);
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
                if (wa < 0 || wb < 0 || wc < 0 || blend is not null && (wa == 0 && !includeA || wb == 0 && !includeB || wc == 0 && !includeC))
                {
                    continue;
                }
                int index = y * Width + x;
                if ((blend == TriangleBlend.Subtract ? reciprocal < depth[index] : reciprocal <= depth[index]) || reciprocal < 1 / far)
                {
                    continue;
                }
                float distance = 1 / reciprocal;
                int u = Math.Clamp((int)(uNumerator * distance * texture.Width), 0, texture.Width - 1);
                int v = Math.Clamp((int)(vNumerator * distance * texture.Height), 0, texture.Height - 1);
                Rgba32 color = texture.Pixels[v * texture.Width + u];
                if (a.ColorOverZ is { } first && b.ColorOverZ is { } second && c.ColorOverZ is { } third)
                {
                    Vector4 tint = first / a.ReciprocalZ * wa + second / b.ReciprocalZ * wb + third / c.ReciprocalZ * wc;
                    color = new Rgba32((byte)Math.Clamp(color.R * tint.X, 0, 255), (byte)Math.Clamp(color.G * tint.Y, 0, 255), (byte)Math.Clamp(color.B * tint.Z, 0, 255), (byte)Math.Clamp(color.A * tint.W, 0, 255));
                }
                if (color.A == 0)
                {
                    continue;
                }
                float haze = blend is not null ? 0 : Math.Clamp((distance - far * 0.6f) / (far * 0.4f), 0, 1);
                float alpha = transparent ? color.A / 255f * 0.5f : 1;
                int pixel = index * 4;
                if (blend is { } nativeBlend)
                {
                    byte NativeChannel(byte background, byte foreground)
                    {
                        float value = nativeBlend switch
                        {
                            TriangleBlend.HalfAdd => (background + foreground) / 2f,
                            TriangleBlend.Add => background + foreground,
                            TriangleBlend.Subtract => background - foreground,
                            TriangleBlend.QuarterAdd => background + foreground / 4f,
                            _ => throw new NotSupportedException($"Unsupported native world primitive blend {nativeBlend}.")
                        };
                        return (byte)Math.Clamp(MathF.Round(value), 0, 255);
                    }
                    pixels[pixel] = NativeChannel(pixels[pixel], color.R);
                    pixels[pixel + 1] = NativeChannel(pixels[pixel + 1], color.G);
                    pixels[pixel + 2] = NativeChannel(pixels[pixel + 2], color.B);
                }
                else if (!transparent && haze == 0)
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
