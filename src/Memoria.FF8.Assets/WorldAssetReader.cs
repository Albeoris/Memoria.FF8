using System.Buffers.Binary;
using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Tim;

namespace Memoria.FF8.Assets;

internal static class WorldAssetReader
{
    public static WorldSky ReadSky(byte[] bytes, int offset)
    {
        ReadOnlySpan<byte> entry = bytes.AsSpan(offset, 52);
        Vector3 position = new(BinaryPrimitives.ReadInt32LittleEndian(entry), 0, BinaryPrimitives.ReadInt32LittleEndian(entry[4..]));
        return new WorldSky(position, new Rgba32(entry[20], entry[21], entry[22]), new Rgba32(entry[24], entry[25], entry[26]), new Rgba32(entry[28], entry[29], entry[30]), BinaryPrimitives.ReadInt32LittleEndian(entry[8..]));
    }

    public static int[] ReadSections(byte[] bytes)
    {
        if (bytes.Length < 196)
        {
            throw new InvalidDataException("Truncated WMSET section table.");
        }
        int[] sections = Enumerable.Range(0, 48).Select(index => checked((int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index * 4)))).ToArray();
        if (sections.Any(pointer => pointer < 196 || pointer >= bytes.Length) || sections.Zip(sections.Skip(1)).Any(pair => pair.First > pair.Second))
        {
            throw new InvalidDataException("Invalid WMSET section pointers.");
        }
        return sections;
    }

    public static WorldSpawn[] ReadSpawns(byte[] bytes, int[] sections)
    {
        int size = sections[9] - sections[8] - 4;
        if (size < 0 || size % 12 != 0)
        {
            throw new InvalidDataException("Invalid WMSET field-to-world table size.");
        }
        WorldSpawn[] spawns = new WorldSpawn[size / 12];
        for (int i = 0; i < spawns.Length; i++)
        {
            ReadOnlySpan<byte> entry = bytes.AsSpan(sections[8] + i * 12, 12);
            spawns[i] = new WorldSpawn(BinaryPrimitives.ReadInt32LittleEndian(entry), BinaryPrimitives.ReadInt32LittleEndian(entry[4..]), BinaryPrimitives.ReadInt16LittleEndian(entry[8..]), BinaryPrimitives.ReadUInt16LittleEndian(entry[10..]));
        }
        return spawns;
    }

    public static int[] InnerPointers(byte[] bytes, int offset)
    {
        List<int> pointers = [];
        for (int i = 0; i < 256; i++)
        {
            int pointer = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset + i * 4, 4));
            if (pointer == 0)
            {
                return pointers.ToArray();
            }
            if (pointer < 0 || pointer >= bytes.Length - offset)
            {
                throw new InvalidDataException("WMSET inner pointer outside its resource.");
            }
            pointers.Add(pointer);
        }
        throw new InvalidDataException("WMSET inner pointer list is unbounded.");
    }

    public static (TimImage Image, int End) ReadTim(byte[] bytes, int offset)
    {
        int position = offset + 8;
        uint flags = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 4));
        if ((flags & 8) != 0)
        {
            position = checked(position + BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(position)));
        }
        int end = checked(position + BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(position)));
        if (end > bytes.Length || end <= offset)
        {
            throw new InvalidDataException("World TIM lies outside its resource.");
        }
        return (TimFile.Read(bytes.AsSpan(offset, end - offset)).Images.Single(), end);
    }

    public static WorldSegment ReadSegment(byte[] bytes, int mapId, int sourceId)
    {
        List<WorldTriangle> triangles = [];
        uint group = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
        float originX = -131072 + mapId % 32 * 8192;
        float originZ = -98304 + mapId / 32 * 8192;
        for (int block = 0; block < 16; block++)
        {
            int offset = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4 + block * 4)));
            if (offset < 68 || offset > bytes.Length - 4)
            {
                throw new InvalidDataException("World block offset lies outside its segment.");
            }
            int polygons = bytes[offset];
            int vertices = bytes[offset + 1];
            int normals = bytes[offset + 2];
            int vertexOffset = offset + 4 + polygons * 16;
            int end = vertexOffset + vertices * 8 + normals * 8 + 4;
            if (end > bytes.Length)
            {
                throw new InvalidDataException("World block data lies outside its segment.");
            }
            Vector3[] positions = new Vector3[vertices];
            for (int vertex = 0; vertex < vertices; vertex++)
            {
                ReadOnlySpan<byte> item = bytes.AsSpan(vertexOffset + vertex * 8, 8);
                positions[vertex] = new Vector3(originX + block % 4 * 2048 + BinaryPrimitives.ReadInt16LittleEndian(item), -BinaryPrimitives.ReadInt16LittleEndian(item[2..]), originZ + block / 4 * 2048 - BinaryPrimitives.ReadInt16LittleEndian(item[4..]));
            }
            for (int polygon = 0; polygon < polygons; polygon++)
            {
                ReadOnlySpan<byte> item = bytes.AsSpan(offset + 4 + polygon * 16, 16);
                if (item[0] >= vertices || item[1] >= vertices || item[2] >= vertices)
                {
                    throw new InvalidDataException("World polygon references an absent vertex.");
                }
                triangles.Add(new WorldTriangle(positions[item[0]], positions[item[1]], positions[item[2]], new Vector2(item[6], item[7]) / 256, new Vector2(item[8], item[9]) / 256, new Vector2(item[10], item[11]) / 256, (byte)(item[12] >> 4), (byte)(item[12] & 15), item[13], item[14], item[15]) { NativeBlockIndex = block, NativePolygonIndex = polygon });
            }
        }
        return new WorldSegment(mapId, sourceId, group, triangles);
    }

    public static bool TryHeight(WorldTriangle triangle, float x, float z, out float height)
    {
        float denominator = (triangle.B.Z - triangle.C.Z) * (triangle.A.X - triangle.C.X) + (triangle.C.X - triangle.B.X) * (triangle.A.Z - triangle.C.Z);
        if (MathF.Abs(denominator) < 0.001f)
        {
            height = 0;
            return false;
        }
        float a = ((triangle.B.Z - triangle.C.Z) * (x - triangle.C.X) + (triangle.C.X - triangle.B.X) * (z - triangle.C.Z)) / denominator;
        float b = ((triangle.C.Z - triangle.A.Z) * (x - triangle.C.X) + (triangle.A.X - triangle.C.X) * (z - triangle.C.Z)) / denominator;
        float c = 1 - a - b;
        height = a * triangle.A.Y + b * triangle.B.Y + c * triangle.C.Y;
        return a >= -0.00001f && b >= -0.00001f && c >= -0.00001f;
    }

    public static bool TryNativeHeight(WorldTriangle triangle, int x, int z, out int height)
    {
        // Work in native graphics coordinates before the signed >>8. Reflecting the
        // already shifted normal would change negative arithmetic rounding.
        static Vector3 Native(Vector3 point) => new(point.X, -point.Y, -point.Z);
        Vector3 a = Native(triangle.A);
        Vector3 b = Native(triangle.B);
        Vector3 c = Native(triangle.C);
        Vector3 p = new(x, 0, -z);
        Vector3[] vertices = [a, b, c];
        for (int index = 0; index < 3; index++)
        {
            Vector3 origin = vertices[index];
            Vector3 edge = vertices[(index + 1) % 3] - origin;
            Vector3 delta = p - origin;
            long crossY = (long)(short)delta.Z * (short)edge.X - (long)(short)delta.X * (short)edge.Z;
            if (crossY > 0)
            {
                height = 0;
                return false;
            }
        }
        Vector3 ca = c - a;
        Vector3 ba = b - a;
        long nx = ((long)(short)ca.Y * (short)ba.Z - (long)(short)ca.Z * (short)ba.Y) >> 8;
        long ny = ((long)(short)ca.Z * (short)ba.X - (long)(short)ca.X * (short)ba.Z) >> 8;
        long nz = ((long)(short)ca.X * (short)ba.Y - (long)(short)ca.Y * (short)ba.X) >> 8;
        long nativeHeight = ny != 0 ? (ny * (int)a.Y + nx * ((int)a.X - x) + nz * ((int)a.Z + z)) / ny : ((int)a.Y + (int)b.Y + (int)c.Y) / 3;
        height = checked((int)-nativeHeight);
        return true;
    }

    public static FieldModel ReadSquall(byte[] bytes)
    {
        return ReadWorldCharacter(bytes, 0);
    }

    public static FieldModel ReadWorldCharacter(byte[] bytes, int shape)
    {
        if (bytes.Length < 12 || BinaryPrimitives.ReadInt32LittleEndian(bytes) != bytes.Length)
        {
            throw new InvalidDataException("Invalid original world character container length.");
        }
        int count = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(bytes.Length - 4));
        if (count is < 1 or > 64 || shape < 0 || shape >= count)
        {
            throw new InvalidDataException("The selected world character shape is absent from its original footer.");
        }
        // Native653410 walks the footer backward from length-8, skipping each
        // positive TIM pointer list and its sentinel/model pointer pair.
        int cursor = bytes.Length - 8;
        List<int> textures = [];
        int position = -1;
        for (int index = 0; index <= shape; index++)
        {
            textures.Clear();
            while (cursor >= 8 && BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(cursor)) > 0)
            {
                int texture = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(cursor)) & 0x0FFFFFFF;
                if (texture < 4 || texture > bytes.Length - 8 || textures.Count >= 4)
                {
                    throw new InvalidDataException("Invalid original world character TIM footer pointer.");
                }
                textures.Add(texture);
                cursor -= 4;
            }
            if (cursor < 8 || BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(cursor)) != -1)
            {
                throw new NotSupportedException("The selected world character footer requires an implemented embedded model layout.");
            }
            position = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(cursor - 4));
            cursor -= 8;
        }
        if (position < 4 || position > cursor - 64 || textures.Count == 0)
        {
            throw new InvalidDataException("World character geometry lies outside its original data region.");
        }
        int bones = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(position));
        int animationOffset = checked(position + BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(position + 56)));
        IReadOnlyList<ModelAnimation> animations = ReadWorldAnimations(bytes, animationOffset, bones);
        return FieldModelReader.ReadModel($"World character shape {shape}", bytes, position, textures, 0, null, 0, animations);
    }

    private static IReadOnlyList<ModelAnimation> ReadWorldAnimations(byte[] bytes, int offset, int bones)
    {
        using BinaryReader reader = new(new MemoryStream(bytes, false));
        reader.BaseStream.Position = offset;
        int count = reader.ReadUInt16();
        if (count == 0 || count > 100 || bones == 0 || bones > 100)
        {
            throw new InvalidDataException("Invalid world avatar animation header.");
        }
        List<ModelAnimation> animations = [];
        for (int animation = 0; animation < count; animation++)
        {
            int frames = reader.ReadUInt16();
            int animationBones = reader.ReadUInt16();
            if (frames == 0 || frames > 10000 || animationBones != bones)
            {
                throw new InvalidDataException("Invalid world avatar animation frame header.");
            }
            List<AnimationFrame> poses = [];
            for (int frame = 0; frame < frames; frame++)
            {
                Vector3 translation = new(reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16());
                Vector3[] rotations = new Vector3[bones];
                for (int bone = 0; bone < bones; bone++)
                {
                    rotations[bone] = new Vector3(reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16()) * (MathF.Tau / 4096);
                }
                poses.Add(new AnimationFrame(translation, rotations));
            }
            animations.Add(new ModelAnimation(poses));
        }
        return animations;
    }
}
