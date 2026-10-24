using System.Buffers.Binary;
using System.Numerics;
using Albeoris.Games.FF8.Imaging.Tim;

namespace Memoria.FF8.Assets;

/// <summary>Structural static indexed world model layout shared by original shape records; no field or model geometry substitution.</summary>
public static class WorldStaticModelReader
{
    public static WorldStaticModel Read(byte shape, ReadOnlySpan<byte> model, TimImage tim)
    {
        if (shape < 64 || model.Length < 8)
        {
            throw new InvalidDataException("Static world model requires its original shape-table entry and complete model header.");
        }
        int colorsPerPalette = tim.PixelMode switch
        {
            TimPixelMode.Indexed4 => 16,
            TimPixelMode.Indexed8 => 256,
            _ => throw new NotSupportedException("Static world model requires its original indexed4 or indexed8 TIM.")
        };
        if (tim.Clut is null || tim.Clut.Width % colorsPerPalette != 0 || tim.Clut.Height != 1)
        {
            throw new NotSupportedException("Static world model requires complete original palettes in its indexed TIM CLUT row.");
        }
        int triangles = BinaryPrimitives.ReadUInt16LittleEndian(model);
        int quads = BinaryPrimitives.ReadUInt16LittleEndian(model[2..]);
        ushort page = BinaryPrimitives.ReadUInt16LittleEndian(model[4..]);
        int count = BinaryPrimitives.ReadUInt16LittleEndian(model[6..]);
        int vertexStart = checked(8 + triangles * 12 + quads * 16);
        int textureDepth = tim.PixelMode == TimPixelMode.Indexed8 ? 1 : 0;
        if (count == 0 || vertexStart > model.Length || count > (model.Length - vertexStart) / 8 || (page & ~0xFF) != 0 || page >> 7 != textureDepth)
        {
            throw new InvalidDataException("Static world primitive or vertex layout leaves its original member.");
        }
        Vector3[] vertices = new Vector3[count];
        for (int vertex = 0; vertex < count; vertex++)
        {
            ReadOnlySpan<byte> raw = model.Slice(vertexStart + vertex * 8, 8);
            vertices[vertex] = new Vector3(BinaryPrimitives.ReadInt16LittleEndian(raw), BinaryPrimitives.ReadInt16LittleEndian(raw[2..]), BinaryPrimitives.ReadInt16LittleEndian(raw[4..]));
        }
        List<WorldStaticFace> faces = [];
        int position = 8;
        for (int face = 0; face < triangles + quads; face++)
        {
            bool quad = face >= triangles;
            int vertexCount = quad ? 4 : 3;
            ReadOnlySpan<byte> raw = model.Slice(position, quad ? 16 : 12);
            int[] indices = raw[..vertexCount].ToArray().Select(value => (int)value).ToArray();
            ushort clut = BinaryPrimitives.ReadUInt16LittleEndian(raw[(quad ? 12 : 10)..]);
            int paletteX = (clut & 63) * 16;
            int paletteY = clut >> 6;
            int palette = (paletteX - tim.Clut.X) / colorsPerPalette;
            if (indices.Any(value => value >= count) || paletteY != tim.Clut.Y || paletteX < tim.Clut.X || paletteX >= tim.Clut.X + tim.Clut.Width || (paletteX - tim.Clut.X) % colorsPerPalette != 0 || palette >= tim.Palettes.Count)
            {
                throw new InvalidDataException("Static world model references an absent vertex or palette.");
            }
            Vector2[] uv = new Vector2[vertexCount];
            int pixelsPerWord = tim.PixelMode == TimPixelMode.Indexed8 ? 2 : 4;
            int originU = (tim.X - (page & 15) * 64) * pixelsPerWord;
            int originV = tim.Y - ((page & 16) == 0 ? 0 : 256);
            for (int vertex = 0; vertex < vertexCount; vertex++)
            {
                uv[vertex] = new Vector2(raw[4 + 2 * vertex] - originU, raw[5 + 2 * vertex] - originV);
                if (uv[vertex].X < 0 || uv[vertex].X >= tim.PixelWidth || uv[vertex].Y < 0 || uv[vertex].Y >= tim.Height)
                {
                    throw new InvalidDataException("Static world texture coordinates leave the original TIM rectangle.");
                }
            }
            bool transparent = quad ? (BinaryPrimitives.ReadUInt16LittleEndian(raw[14..]) & 1) != 0 : (raw[3] & 1) != 0;
            faces.Add(new WorldStaticFace(indices, uv, palette, transparent));
            position += quad ? 16 : 12;
        }
        return new WorldStaticModel(shape, shape - 64, page, vertices, faces, Enumerable.Range(0, tim.Palettes.Count).Select(palette => tim.Decode(palette)).ToArray());
    }
}
