using System.Numerics;
using Albeoris.Games.FF8.Imaging;

namespace Memoria.FF8.Assets;

public sealed record WorldStaticFace(int[] Vertices, Vector2[] TextureCoordinates, int Palette, bool Transparent)
{
    public Vector4[]? VertexColors { get; init; }
}
public sealed record WorldStaticModel(byte ShapeId, int ModelIndex, ushort TexturePage, IReadOnlyList<Vector3> NativeVertices, IReadOnlyList<WorldStaticFace> Faces, IReadOnlyList<PixelImage> Textures);

public sealed partial class WorldAssets
{
    public WorldStaticModel ExamCar => GetCarModel(33);

    public WorldStaticModel GetCarModel(byte entityType)
    {
        byte shape = entityType switch
        {
            32 => 74,
            33 => 73,
            _ => throw new NotSupportedException("Original car model requires its actual supported native entity type.")
        };
        return ReadScriptedStaticModel(shape);
    }
}
