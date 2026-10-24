using System.Numerics;

namespace Memoria.FF8.Assets;

public sealed record WorldGlobeVertex(Vector2 Position, Vector2 TextureOffset);
public sealed record WorldGlobeFace(IReadOnlyList<int> Vertices);
public sealed record WorldGlobeGeometry(IReadOnlyList<WorldGlobeVertex> Vertices, IReadOnlyList<WorldGlobeFace> Faces);

public sealed partial class WorldAssets
{
    private WorldGlobeGeometry? mapGlobeGeometry;

    public WorldGlobeGeometry ReadMapGlobeGeometry()
    {
        if (mapGlobeGeometry is not null)
        {
            return mapGlobeGeometry;
        }
        byte[] faces = NativeWorldMemory.Read(0xC76BE8, 148);
        byte[] positions = NativeWorldMemory.Read(0xC76C7C, 80);
        byte[] offsets = NativeWorldMemory.Read(0xC76CCC, 80);
        List<WorldGlobeVertex> vertices = [];
        List<WorldGlobeFace> polygons = [];
        for (int index = 0; index < 40; index++)
        {
            vertices.Add(new(new Vector2(unchecked((sbyte)positions[index * 2]), unchecked((sbyte)positions[index * 2 + 1])), new Vector2(unchecked((sbyte)offsets[index * 2]), unchecked((sbyte)offsets[index * 2 + 1]))));
        }
        for (int index = 0; index < 37; index++)
        {
            int count = index < 25 ? 4 : 3;
            int[] corners = faces.AsSpan(index * 4, count).ToArray().Select(value => (int)value).ToArray();
            if (corners.Any(corner => corner >= vertices.Count) || count == 3 && faces[index * 4 + 3] != 255)
            {
                throw new InvalidDataException("Native globe topology leaves its original vertex tables.");
            }
            polygons.Add(new(Array.AsReadOnly(corners)));
        }
        mapGlobeGeometry = new(vertices.AsReadOnly(), polygons.AsReadOnly());
        return mapGlobeGeometry;
    }
}
