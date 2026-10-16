using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Map;

namespace Memoria.FF8.Assets;

public sealed record FieldAssets(int Id, string Name, byte[] Jsm, IReadOnlyList<string> Messages, IReadOnlyList<BackgroundTile> Background, FieldCamera Camera, FieldWalkmesh Walkmesh, IReadOnlyList<FieldModel> Models)
{
    public string? WalkmeshHash { get; init; }
    public string? NativeExecutableHash { get; init; }
    public IReadOnlyList<FieldCamera> Cameras { get; init; } = Array.AsReadOnly(new[] { Camera });
    public FieldInformation? Information { get; init; }
    public FieldMovieMask? MovieMask { get; init; }
    public Memoria.FF8.Runtime.NativeFieldEncounterDefinition? RandomEncounters { get; init; }

    public FieldCamera GetCamera(int index)
    {
        if ((uint)index >= Cameras.Count)
        {
            throw new InvalidDataException($"{Name}: missing camera {index}; the CA section contains {Cameras.Count} camera(s).");
        }
        return Cameras[index];
    }

    public int GetAnimationFrameCount(int modelId, int animationId)
    {
        if ((uint)modelId >= Models.Count || (uint)animationId >= Models[modelId].Animations.Count)
        {
            throw new InvalidDataException($"{Name}: missing model {modelId}, animation {animationId}.");
        }
        return Models[modelId].Animations[animationId].Frames.Count;
    }
}

public sealed record BackgroundTile(int X, int Y, int Z, int AnimationId, int AnimationState, int Layer, MapBlendMode BlendMode, bool Blend, bool Draw, PixelImage Image)
{
    public ushort PackedPalette { get; init; }
    public Memoria.FF8.Resources.AssetHandle<Memoria.FF8.Resources.TextureValue>? Texture { get; init; }
    public PixelImage ResolvedImage => Texture?.Value.Image ?? Image;
}
public readonly record struct ProjectedPoint(float X, float Y, float Depth);

public sealed record FieldCamera(Vector3 AxisX, Vector3 AxisY, Vector3 AxisZ, Vector3 Translation, Vector2 Pan, float FocalLength)
{
    public ProjectedPoint Project(Vector3 point)
    {
        Vector3 camera = new(Vector3.Dot(AxisX, point), Vector3.Dot(AxisY, point), Vector3.Dot(AxisZ, point));
        camera += Translation;
        float depth = Math.Max(1, camera.Z);
        return new ProjectedPoint(160 + Pan.X + FocalLength * camera.X / depth, 120 + Pan.Y + FocalLength * camera.Y / depth, camera.Z);
    }

    public static FieldCamera Read(byte[] bytes)
    {
        if (bytes.Length < 38)
        {
            throw new InvalidDataException("A field camera requires at least 38 bytes.");
        }
        using BinaryReader reader = new(new MemoryStream(bytes, false));
        Vector3 Axis() => new(reader.ReadInt16() / 4096f, reader.ReadInt16() / 4096f, reader.ReadInt16() / 4096f);
        Vector3 x = Axis();
        Vector3 y = Axis();
        Vector3 z = Axis();
        reader.ReadUInt16();
        Vector3 translation = new(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
        Vector2 pan = new(reader.ReadInt16(), reader.ReadInt16());
        return new FieldCamera(x, y, z, translation, pan, reader.ReadUInt16());
    }

    public static IReadOnlyList<FieldCamera> ReadAll(byte[] bytes)
    {
        // Native 4767B0 selects CA base or base+40 and consumes through focal WORD+36.
        // An incomplete trailing record does not invalidate the preceding complete camera.
        const int stride = 40;
        const int payloadSize = 38;
        if (bytes.Length < payloadSize)
        {
            throw new InvalidDataException("A CA section requires at least one complete 38-byte camera payload.");
        }
        FieldCamera[] cameras = new FieldCamera[1 + (bytes.Length - payloadSize) / stride];
        for (int index = 0; index < cameras.Length; index++)
        {
            cameras[index] = Read(bytes.AsSpan(index * stride, payloadSize).ToArray());
        }
        return Array.AsReadOnly(cameras);
    }
}

public readonly record struct WalkmeshTriangle(Vector3 A, Vector3 B, Vector3 C, short NeighborA, short NeighborB, short NeighborC);

public sealed record FieldWalkmesh(IReadOnlyList<WalkmeshTriangle> Triangles)
{
    public bool TryPosition(float x, float y, out Vector3 position, out int triangle, IReadOnlySet<int>? lockedTriangles = null)
    {
        Vector2 p = new(x, y);
        for (int i = 0; i < Triangles.Count; i++)
        {
            if (lockedTriangles?.Contains(i) == true)
            {
                continue;
            }
            WalkmeshTriangle t = Triangles[i];
            Vector2 a = new(t.A.X, t.A.Y);
            Vector2 b = new(t.B.X, t.B.Y);
            Vector2 c = new(t.C.X, t.C.Y);
            float determinant = Cross(b - a, c - a);
            if (Math.Abs(determinant) < 0.01f)
            {
                continue;
            }
            float u = Cross(p - a, c - a) / determinant;
            float v = Cross(b - a, p - a) / determinant;
            if (u >= -0.0001f && v >= -0.0001f && u + v <= 1.0001f)
            {
                position = new Vector3(x, y, t.A.Z * (1 - u - v) + t.B.Z * u + t.C.Z * v);
                triangle = i;
                return true;
            }
        }
        position = default;
        triangle = -1;
        return false;
    }

    private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

    public static FieldWalkmesh Read(byte[] bytes)
    {
        using BinaryReader reader = new(new MemoryStream(bytes, false));
        int count = checked((int)reader.ReadUInt32());
        if (count > 10000 || bytes.Length != 4 + count * 30)
        {
            throw new InvalidDataException("Invalid selected field walkmesh size.");
        }
        Vector3[] vertices = new Vector3[count * 3];
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] = new Vector3(reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16());
            reader.ReadInt16();
        }
        WalkmeshTriangle[] triangles = new WalkmeshTriangle[count];
        for (int i = 0; i < count; i++)
        {
            triangles[i] = new WalkmeshTriangle(vertices[i * 3], vertices[i * 3 + 1], vertices[i * 3 + 2], reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16());
        }
        return new FieldWalkmesh(triangles);
    }
}
