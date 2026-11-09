using System.Numerics;

namespace Memoria.FF7.Formats;

public sealed record PcFieldCamera(Vector3 AxisX, Vector3 AxisY, Vector3 AxisZ, short Reserved, Vector3 Translation, int Reserved2, short FocalLength);
public static class PcFieldCameras
{
    public static IReadOnlyList<PcFieldCamera> Read(ReadOnlySpan<byte> source)
    {
        if (source.Length % 38 != 0)
        {
            throw new InvalidDataException("FF7 field camera section requires 38-byte records.");
        }
        BinaryCursor cursor = new(source);
        PcFieldCamera[] cameras = new PcFieldCamera[source.Length / 38];
        for (int i = 0; i < cameras.Length; i++)
        {
            cameras[i] = new PcFieldCamera(cursor.Vector16(), cursor.Vector16(), cursor.Vector16(), cursor.I16(), cursor.Vector32(), cursor.I32(), cursor.I16());
        }
        return Array.AsReadOnly(cameras);
    }
}

public sealed record PcWalkmeshTriangle(int Index, Vector3 A, Vector3 B, Vector3 C, short AdjacentAB, short AdjacentBC, short AdjacentCA);
public static class PcFieldWalkmesh
{
    public static IReadOnlyList<PcWalkmeshTriangle> Read(ReadOnlySpan<byte> source)
    {
        BinaryCursor cursor = new(source);
        int count = cursor.Count32(30);
        if (cursor.Remaining != count * 30)
        {
            throw new InvalidDataException("FF7 walkmesh length does not match its triangle count.");
        }
        Vector3[] vertices = new Vector3[count * 3];
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] = cursor.Vector16();
            cursor.Skip(2);
        }
        PcWalkmeshTriangle[] triangles = new PcWalkmeshTriangle[count];
        for (int i = 0; i < triangles.Length; i++)
        {
            short ab = cursor.I16();
            short bc = cursor.I16();
            short ca = cursor.I16();
            if (ab < -1 || bc < -1 || ca < -1 || ab >= count || bc >= count || ca >= count)
            {
                throw new InvalidDataException($"FF7 walkmesh triangle {i} has invalid adjacency.");
            }
            triangles[i] = new PcWalkmeshTriangle(i, vertices[3 * i], vertices[3 * i + 1], vertices[3 * i + 2], ab, bc, ca);
        }
        return Array.AsReadOnly(triangles);
    }
}

public sealed record PcFieldLight(uint ColorRgb, Vector3 Position);
public sealed record PcFieldModelDescriptor(string Name, ushort Attribute, string Hrc, string Scale, IReadOnlyList<PcFieldLight> Lights, uint AmbientRgb, IReadOnlyList<string> Animations);
public sealed record PcFieldModels(short Reserved, short ModelScale, IReadOnlyList<PcFieldModelDescriptor> Models)
{
    public static PcFieldModels Read(ReadOnlySpan<byte> source)
    {
        BinaryCursor cursor = new(source);
        short reserved = cursor.I16();
        ushort count = cursor.U16();
        short scale = cursor.I16();
        if (count > cursor.Remaining / 48)
        {
            throw new InvalidDataException("FF7 model count exceeds its section.");
        }
        PcFieldModelDescriptor[] models = new PcFieldModelDescriptor[count];
        for (int i = 0; i < models.Length; i++)
        {
            string name = cursor.Ascii(cursor.U16());
            ushort attribute = cursor.U16();
            string hrc = cursor.Ascii(8);
            string modelScale = cursor.Ascii(4);
            ushort animationCount = cursor.U16();
            PcFieldLight[] lights = new PcFieldLight[3];
            for (int light = 0; light < lights.Length; light++)
            {
                uint color = (uint)(cursor.U8() | cursor.U8() << 8 | cursor.U8() << 16);
                lights[light] = new PcFieldLight(color, cursor.Vector16());
            }
            uint ambient = (uint)(cursor.U8() | cursor.U8() << 8 | cursor.U8() << 16);
            if (animationCount > cursor.Remaining / 4)
            {
                throw new InvalidDataException("FF7 model animation directory exceeds its section.");
            }
            string[] animations = new string[animationCount];
            for (int animation = 0; animation < animations.Length; animation++)
            {
                animations[animation] = cursor.Ascii(cursor.U16());
                cursor.Skip(2);
            }
            models[i] = new PcFieldModelDescriptor(name, attribute, hrc, modelScale, Array.AsReadOnly(lights), ambient, Array.AsReadOnly(animations));
        }
        if (cursor.Remaining != 0)
        {
            throw new InvalidDataException("FF7 model section contains trailing records.");
        }
        return new PcFieldModels(reserved, scale, Array.AsReadOnly(models));
    }
}
