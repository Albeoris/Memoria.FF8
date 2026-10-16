using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Rendering;

public readonly record struct BattleCamera(Vector3 Eye, Vector3 Target, float FieldOfView = 0.8f, float VerticalCenter = 0.5f)
{
    public short NativeRoll { get; init; }
    // Original B8B7F0/B8B800 bootstrap tuple,500541/500560 focal512,500900 projection center(160,108).
    // The live scene selects its installed introduction endpoint instead of this bootstrap view.
    public static BattleCamera NativeInitial => FromNative(new BattleStageCamera(new Vector3(10000, -3000, 5000), new Vector3(0, -500, 0), 512));

    public static BattleCamera FromNative(BattleStageCamera camera, short roll = 0)
    {
        if (camera.FocalLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(camera));
        }
        // Native50CBA0/50F900 use positive-down Y and positive-forward Z. A half-turn about X
        // reconciles that camera basis with CreateLookAt without reversing projected winding.
        Vector3 World(Vector3 native) => new Vector3(native.X, -native.Y, -native.Z) / 100f;
        return new BattleCamera(World(camera.NativeEye), World(camera.NativeTarget), 2 * MathF.Atan(120f / camera.FocalLength), 108f / 240) { NativeRoll = roll };
    }
}

public sealed record BattleActorDraw(BattleModel Model, Vector3 Position, float Facing = 0, int Animation = 0, float Frame = 0, Vector3 AttackOffset = default, bool Visible = true, float Opacity = 1, float GroundOffset = 0, bool LoopAnimation = true)
{
    public NativeBattleDecodedPose? NativePose { get; init; }
    public uint MeshMask { get; init; } = uint.MaxValue;
}

/// <summary>Perspective-correct CPU battle renderer for original stage and skeletal DAT assets.</summary>
public sealed class BattleRenderer
{
    private readonly byte[] pixels;
    private readonly float[] depth;
    private Matrix4x4 view;
    private float focal;
    private float centerY;
    private readonly record struct Vertex(Vector3 Position, Vector2 Uv);
    private readonly record struct Screen(float X, float Y, float InverseZ, Vector2 UvOverZ);

    public BattleRenderer(int width, int height)
    {
        if (width < 1 || height < 1 || width > 4096 || height > 4096)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }
        Width = width;
        Height = height;
        pixels = new byte[checked(width * height * 4)];
        depth = new float[width * height];
    }

    public int Width { get; }
    public int Height { get; }
    public int DrawnTriangles { get; private set; }

    public bool TryProject(Vector3 position, out Vector2 screen)
    {
        Vector3 transformed = Vector3.Transform(position, view);
        if (transformed.Z >= -0.25f)
        {
            screen = default;
            return false;
        }
        screen = new Vector2(Width * 0.5f - transformed.X * focal / transformed.Z, centerY + transformed.Y * focal / transformed.Z);
        return float.IsFinite(screen.X) && float.IsFinite(screen.Y);
    }

    public byte[] Render(BattleStage stage, IReadOnlyList<BattleActorDraw> actors, BattleCamera? camera = null)
    {
        Begin(camera ?? BattleCamera.NativeInitial);
        foreach (BattleMesh mesh in stage.Meshes)
        {
            Draw(mesh, mesh.Vertices.Select(vertex => vertex.Position).ToArray(), stage.Textures, 1);
        }
        foreach (BattleActorDraw actor in actors.Where(actor => actor.Visible))
        {
            DrawActor(actor);
        }
        return pixels;
    }

    private void Begin(BattleCamera camera)
    {
        if (camera.Eye == camera.Target || camera.FieldOfView <= 0 || camera.FieldOfView >= MathF.PI || camera.VerticalCenter is <= 0 or >= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(camera));
        }
        //5033E0 first rotates both native eye/target around Z, then composes the look-at matrix with that rotation.
        //The renderer's half-turn about X reverses the native Z rotation angle.
        Matrix4x4 roll = Matrix4x4.CreateRotationZ(-camera.NativeRoll * (2 * MathF.PI / 4096));
        view = roll * Matrix4x4.CreateLookAt(Vector3.Transform(camera.Eye, roll), Vector3.Transform(camera.Target, roll), Vector3.UnitY);
        focal = Height / (2 * MathF.Tan(camera.FieldOfView / 2));
        centerY = Height * camera.VerticalCenter;
        Array.Clear(pixels);
        Array.Fill(depth, float.NegativeInfinity);
        DrawnTriangles = 0;
        for (int index = 0; index < pixels.Length; index += 4)
        {
            pixels[index] = 17;
            pixels[index + 1] = 20;
            pixels[index + 2] = 29;
            pixels[index + 3] = 255;
        }
    }

    private void DrawActor(BattleActorDraw actor)
    {
        IReadOnlyList<Vector3[]> pose = actor.NativePose is { } native ? actor.Model.PoseNative(native) : actor.Model.Pose(actor.Animation, actor.Frame, actor.LoopAnimation);
        Matrix4x4 transform = Matrix4x4.CreateRotationY(actor.Facing) * Matrix4x4.CreateTranslation(actor.Position + actor.AttackOffset - Vector3.UnitY * actor.GroundOffset);
        for (int mesh = 0; mesh < actor.Model.Meshes.Count; mesh++)
        {
            if ((actor.MeshMask & (1u << (mesh & 31))) == 0)
            {
                continue;
            }
            Draw(actor.Model.Meshes[mesh], pose[mesh].Select(position => Vector3.Transform(position, transform)).ToArray(), actor.Model.Textures, actor.Opacity);
        }
    }

    private void Draw(BattleMesh mesh, Vector3[] positions, IReadOnlyList<PixelImage> textures, float opacity)
    {
        Vector3[] transformed = positions.Select(position => Vector3.Transform(position, view)).ToArray();
        foreach (BattleFace face in mesh.Faces)
        {
            Vector2 textureScale = ResourceTextureList.PhysicalScale(textures, face.Texture);
            Vertex At(int index) => new(transformed[face.Vertices[index]], face.Uv[index] * textureScale);
            // Native50FDF0/510680 accepts positive projected winding for both triangles and quads.
            // The camera-space plane test is equivalent before near clipping; use the original first three corners.
            Vector3 first = At(0).Position;
            Vector3 second = At(1).Position;
            Vector3 third = At(2).Position;
            if (Vector3.Dot(Vector3.Cross(second - first, third - first), first) <= 0)
            {
                continue;
            }
            Triangle(At(0), At(1), At(face.Vertices.Length == 4 ? 3 : 2), textures[face.Texture], face.Color, opacity);
            if (face.Vertices.Length == 4)
            {
                Triangle(At(3), At(2), At(0), textures[face.Texture], face.Color, opacity);
            }
        }
    }

    private void Triangle(Vertex a, Vertex b, Vertex c, PixelImage texture, Rgba32 color, float opacity)
    {
        const float near = 0.25f;
        Span<Vertex> input = stackalloc Vertex[3] { a, b, c };
        Span<Vertex> clipped = stackalloc Vertex[5];
        int count = 0;
        for (int index = 0; index < 3; index++)
        {
            Vertex current = input[index];
            Vertex previous = input[(index + 2) % 3];
            bool inside = current.Position.Z <= -near;
            bool previousInside = previous.Position.Z <= -near;
            if (inside != previousInside)
            {
                float amount = (-near - previous.Position.Z) / (current.Position.Z - previous.Position.Z);
                clipped[count++] = new Vertex(Vector3.Lerp(previous.Position, current.Position, amount), Vector2.Lerp(previous.Uv, current.Uv, amount));
            }
            if (inside)
            {
                clipped[count++] = current;
            }
        }
        for (int index = 1; index + 1 < count; index++)
        {
            Raster(Project(clipped[0]), Project(clipped[index]), Project(clipped[index + 1]), texture, color, opacity);
        }
    }

    private Screen Project(Vertex vertex)
    {
        float inverse = -1 / vertex.Position.Z;
        return new Screen(Width * 0.5f + vertex.Position.X * focal * inverse, centerY - vertex.Position.Y * focal * inverse, inverse, vertex.Uv * inverse);
    }

    private static float Edge(Screen a, Screen b, float x, float y) => (b.X - a.X) * (y - a.Y) - (b.Y - a.Y) * (x - a.X);

    private void Raster(Screen a, Screen b, Screen c, PixelImage texture, Rgba32 color, float opacity)
    {
        float area = Edge(a, b, c.X, c.Y);
        if (Math.Abs(area) < 0.001f)
        {
            return;
        }
        int left = Math.Clamp((int)MathF.Floor(Math.Min(a.X, Math.Min(b.X, c.X))), 0, Width - 1);
        int right = Math.Clamp((int)MathF.Ceiling(Math.Max(a.X, Math.Max(b.X, c.X))), 0, Width - 1);
        int top = Math.Clamp((int)MathF.Floor(Math.Min(a.Y, Math.Min(b.Y, c.Y))), 0, Height - 1);
        int bottom = Math.Clamp((int)MathF.Ceiling(Math.Max(a.Y, Math.Max(b.Y, c.Y))), 0, Height - 1);
        DrawnTriangles++;
        for (int y = top; y <= bottom; y++)
        {
            for (int x = left; x <= right; x++)
            {
                float wa = Edge(b, c, x + 0.5f, y + 0.5f) / area;
                float wb = Edge(c, a, x + 0.5f, y + 0.5f) / area;
                float wc = 1 - wa - wb;
                if (wa < 0 || wb < 0 || wc < 0)
                {
                    continue;
                }
                float inverse = wa * a.InverseZ + wb * b.InverseZ + wc * c.InverseZ;
                int index = y * Width + x;
                if (inverse <= depth[index])
                {
                    continue;
                }
                Vector2 uv = (a.UvOverZ * wa + b.UvOverZ * wb + c.UvOverZ * wc) / inverse;
                Rgba32 texel = texture[Math.Clamp((int)uv.X, 0, texture.Width - 1), Math.Clamp((int)uv.Y, 0, texture.Height - 1)];
                if (texel.A == 0 || opacity <= 0)
                {
                    continue;
                }
                int pixel = index * 4;
                pixels[pixel] = (byte)Math.Clamp(texel.R * color.R / 128, 0, 255);
                pixels[pixel + 1] = (byte)Math.Clamp(texel.G * color.G / 128, 0, 255);
                pixels[pixel + 2] = (byte)Math.Clamp(texel.B * color.B / 128, 0, 255);
                pixels[pixel + 3] = (byte)Math.Clamp((int)(texel.A * opacity), 0, 255);
                depth[index] = inverse;
            }
        }
    }
}
