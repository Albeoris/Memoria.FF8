using System.Numerics;

namespace Memoria.FF7.Formats;

public sealed record PcModelPolygon(ushort Unknown, ushort A, ushort B, ushort C, ushort NormalA, ushort NormalB, ushort NormalC, ReadOnlyMemory<byte> Raw);
public sealed record PcModelGroup(int PrimitiveType, int PolygonStart, int PolygonCount, int VertexStart, int VertexCount, int TextureCoordinateStart, int TexturesUsed, int TextureNumber, ReadOnlyMemory<byte> Raw);
public sealed record PcPolygonModel(IReadOnlyList<Vector3> Vertices, IReadOnlyList<Vector3> Normals, IReadOnlyList<Vector2> TextureCoordinates, IReadOnlyList<uint> VertexColors, IReadOnlyList<uint> PolygonColors, IReadOnlyList<PcModelPolygon> Polygons, IReadOnlyList<ReadOnlyMemory<byte>> RenderStates, IReadOnlyList<PcModelGroup> Groups, ReadOnlyMemory<byte> Raw)
{
    public static PcPolygonModel Read(ReadOnlySpan<byte> source, int maximumBytes = 64 * 1024 * 1024)
    {
        if (source.Length > maximumBytes)
        {
            throw new InvalidDataException("FF7 P model exceeds its byte limit.");
        }
        BinaryCursor cursor = new(source);
        cursor.Skip(12);
        int[] counts = new int[12];
        for (int i = 0; i < counts.Length; i++)
        {
            counts[i] = cursor.I32();
            if (counts[i] < 0 || counts[i] > source.Length)
            {
                throw new InvalidDataException("FF7 P model contains an invalid record count.");
            }
        }
        long bytes = 128L + 12L * (counts[0] + (long)counts[1] + counts[2]) + 8L * counts[3] + 4L * counts[4] + 4L * counts[5] + 28L * counts[6] + 24L * counts[7] + 3L * counts[8] + 100L * counts[9] + 56L * counts[10];
        if (bytes > source.Length)
        {
            throw new InvalidDataException("FF7 P model record spans exceed its input.");
        }
        cursor.Seek(128);
        Vector3[] vertices = new Vector3[counts[0]];
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] = cursor.VectorFloat();
        }
        Vector3[] normals = new Vector3[counts[1]];
        for (int i = 0; i < normals.Length; i++)
        {
            normals[i] = cursor.VectorFloat();
        }
        cursor.Skip(counts[2] * 12);
        Vector2[] uv = new Vector2[counts[3]];
        for (int i = 0; i < uv.Length; i++)
        {
            uv[i] = new Vector2(cursor.F32(), cursor.F32());
        }
        uint[] colors = new uint[counts[4]];
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = cursor.U32();
        }
        uint[] polygonColors = new uint[counts[6]];
        for (int i = 0; i < polygonColors.Length; i++)
        {
            polygonColors[i] = cursor.U32();
        }
        cursor.Skip(counts[5] * 4);
        PcModelPolygon[] polygons = new PcModelPolygon[counts[6]];
        for (int i = 0; i < polygons.Length; i++)
        {
            byte[] raw = cursor.Take(24).ToArray();
            BinaryCursor polygon = new(raw);
            polygons[i] = new PcModelPolygon(polygon.U16(), polygon.U16(), polygon.U16(), polygon.U16(), polygon.U16(), polygon.U16(), polygon.U16(), raw);
        }
        cursor.Skip(counts[7] * 24 + counts[8] * 3);
        ReadOnlyMemory<byte>[] states = new ReadOnlyMemory<byte>[counts[9]];
        for (int i = 0; i < states.Length; i++)
        {
            states[i] = cursor.Take(100).ToArray();
        }
        PcModelGroup[] groups = new PcModelGroup[counts[10]];
        for (int i = 0; i < groups.Length; i++)
        {
            byte[] raw = cursor.Take(56).ToArray();
            BinaryCursor group = new(raw);
            int type = group.I32();
            int polygonStart = group.I32();
            int polygonCount = group.I32();
            int vertexStart = group.I32();
            int vertexCount = group.I32();
            group.Skip(24);
            int uvStart = group.I32();
            int textured = group.I32();
            int texture = group.I32();
            if (polygonStart < 0 || polygonCount < 0 || (long)polygonStart + polygonCount > polygons.Length || vertexStart < 0 || vertexCount < 0 || (long)vertexStart + vertexCount > vertices.Length || (textured != 0 && (uvStart < 0 || (long)uvStart + vertexCount > uv.Length)))
            {
                throw new InvalidDataException($"FF7 P model group {i} has invalid geometry ranges.");
            }
            for (int polygon = polygonStart; polygon < polygonStart + polygonCount; polygon++)
            {
                PcModelPolygon p = polygons[polygon];
                if (p.A >= vertexCount || p.B >= vertexCount || p.C >= vertexCount || (normals.Length > 0 && (p.NormalA >= normals.Length || p.NormalB >= normals.Length || p.NormalC >= normals.Length)))
                {
                    throw new InvalidDataException($"FF7 P model polygon {polygon} has invalid vertex or normal indices.");
                }
            }
            groups[i] = new PcModelGroup(type, polygonStart, polygonCount, vertexStart, vertexCount, uvStart, textured, texture, raw);
        }
        return new PcPolygonModel(Array.AsReadOnly(vertices), Array.AsReadOnly(normals), Array.AsReadOnly(uv), Array.AsReadOnly(colors), Array.AsReadOnly(polygonColors), Array.AsReadOnly(polygons), Array.AsReadOnly(states), Array.AsReadOnly(groups), source.ToArray());
    }
}
