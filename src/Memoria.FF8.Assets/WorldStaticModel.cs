using System.Buffers.Binary;
using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Tim;

namespace Memoria.FF8.Assets;

public sealed record WorldStaticFace(int[] Vertices, Vector2[] TextureCoordinates, int Palette, bool Transparent);
public sealed record WorldStaticModel(byte ShapeId, int ModelIndex, ushort TexturePage, IReadOnlyList<Vector3> NativeVertices, IReadOnlyList<WorldStaticFace> Faces, IReadOnlyList<PixelImage> Textures);

public sealed partial class WorldAssets
{
    private WorldStaticModel? examCar;
    public WorldStaticModel ExamCar => examCar ??= ReadStaticModel(73);

    private WorldStaticModel ReadStaticModel(byte shape)
    {
        int index = shape - 64;
        int model = sections[15] + BinaryPrimitives.ReadInt32LittleEndian(wmset.AsSpan(sections[15] + 4 * index));
        int texture = sections[41] + BinaryPrimitives.ReadInt32LittleEndian(wmset.AsSpan(sections[41] + 4 * index));
        TimImage tim = WorldAssetReader.ReadTim(wmset, texture).Image;
        int triangles = BinaryPrimitives.ReadUInt16LittleEndian(wmset.AsSpan(model));
        int quads = BinaryPrimitives.ReadUInt16LittleEndian(wmset.AsSpan(model + 2));
        ushort page = BinaryPrimitives.ReadUInt16LittleEndian(wmset.AsSpan(model + 4));
        int count = BinaryPrimitives.ReadUInt16LittleEndian(wmset.AsSpan(model + 6));
        if (shape != 73 || triangles != 4 || quads != 54 || count != 84 || page != 15 || tim.PixelMode != TimPixelMode.Indexed4 || tim.X != 992 || tim.Y != 0 || tim.PixelWidth != 64 || tim.Height != 64 || tim.Clut is null || tim.Clut.X != 96 || tim.Clut.Y != 254 || tim.Clut.Width != 32 || tim.Clut.Height != 1)
        {
            throw new NotSupportedException("Original world car model/texture layout differs from the proved static shape73 resource.");
        }
        int vertexStart = model + 8 + triangles * 12 + quads * 16;
        Vector3[] vertices = new Vector3[count];
        for (int vertex = 0; vertex < count; vertex++)
        {
            ReadOnlySpan<byte> raw = wmset.AsSpan(vertexStart + vertex * 8, 8);
            vertices[vertex] = new Vector3(BinaryPrimitives.ReadInt16LittleEndian(raw), BinaryPrimitives.ReadInt16LittleEndian(raw[2..]), BinaryPrimitives.ReadInt16LittleEndian(raw[4..]));
        }
        List<WorldStaticFace> faces = [];
        int position = model + 8;
        for (int face = 0; face < triangles + quads; face++)
        {
            bool quad = face >= triangles;
            ReadOnlySpan<byte> raw = wmset.AsSpan(position, quad ? 16 : 12);
            int vertexCount = quad ? 4 : 3;
            int[] indices = raw[..vertexCount].ToArray().Select(value => (int)value).ToArray();
            ushort clut = BinaryPrimitives.ReadUInt16LittleEndian(raw[(quad ? 12 : 10)..]);
            int paletteX = (clut & 63) * 16;
            int paletteY = clut >> 6;
            int palette = (paletteX - tim.Clut.X) / 16;
            if (indices.Any(value => value >= count) || paletteY != tim.Clut.Y || paletteX < tim.Clut.X || paletteX >= tim.Clut.X + tim.Clut.Width)
            {
                throw new InvalidDataException("Original static model references an absent vertex or texture palette.");
            }
            Vector2[] uv = new Vector2[vertexCount];
            int originU = (tim.X - (page & 15) * 64) * 4;
            int originV = tim.Y - ((page & 16) == 0 ? 0 : 256);
            for (int vertex = 0; vertex < vertexCount; vertex++)
            {
                uv[vertex] = new Vector2(raw[4 + 2 * vertex] - originU, raw[5 + 2 * vertex] - originV);
                if (uv[vertex].X < 0 || uv[vertex].X >= tim.PixelWidth || uv[vertex].Y < 0 || uv[vertex].Y >= tim.Height)
                {
                    throw new InvalidDataException("Original car texture coordinates leave the loaded TIM rectangle.");
                }
            }
            bool transparent = quad ? (BinaryPrimitives.ReadUInt16LittleEndian(raw[14..]) & 1) != 0 : (raw[3] & 1) != 0;
            faces.Add(new WorldStaticFace(indices, uv, palette, transparent));
            position += quad ? 16 : 12;
        }
        return new WorldStaticModel(shape, index, page, vertices, faces, Enumerable.Range(0, tim.Palettes.Count).Select(palette => tim.Decode(palette)).ToArray());
    }
}
