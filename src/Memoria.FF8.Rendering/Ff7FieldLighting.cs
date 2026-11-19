using System.Numerics;
using Memoria.FF7.Formats;

namespace Memoria.FF8.Rendering;

/// <summary>Original PC field vertex-color lighting, before material alpha and texture modulation.</summary>
public sealed class Ff7FieldLighting
{
    private readonly Vector3 ambient;
    private readonly Vector3 red;
    private readonly Vector3 green;
    private readonly Vector3 blue;
    private readonly bool zeroFloor;

    public Ff7FieldLighting(PcFieldModelDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (descriptor.Lights.Count != 3)
        {
            throw new InvalidDataException("Native field lighting requires exactly three original directional lights.");
        }
        ambient = Rgb(descriptor.AmbientRgb) / 255f;
        foreach (PcFieldLight light in descriptor.Lights)
        {
            if (!Finite(light.Position))
            {
                throw new InvalidDataException("A native field light has a non-finite direction.");
            }
            Vector3 direction = -light.Position / 4096f;
            Vector3 color = Rgb(light.ColorRgb) / 255f;
            red += direction * color.X;
            green += direction * color.Y;
            blue += direction * color.Z;
        }
    }

    public Ff7FieldLighting(IReadOnlyList<byte> colors, IReadOnlyList<short> directions, Matrix4x4 partNormalTransform)
    {
        ArgumentNullException.ThrowIfNull(colors);
        ArgumentNullException.ThrowIfNull(directions);
        if (colors.Count != 12 || directions.Count != 9)
        {
            throw new InvalidDataException("Custom field lighting requires four RGB triples and nine signed direction words.");
        }
        // The PC WORD converters transpose both matrices; custom color bytes become 8.8 values.
        Matrix4x4 color = new(colors[3] / 256f, colors[4] / 256f, colors[5] / 256f, 0, colors[6] / 256f, colors[7] / 256f, colors[8] / 256f, 0, colors[9] / 256f, colors[10] / 256f, colors[11] / 256f, 0, 0, 0, 0, 1);
        Matrix4x4 direction = new(directions[0] / 4096f, directions[3] / 4096f, directions[6] / 4096f, 0, directions[1] / 4096f, directions[4] / 4096f, directions[7] / 4096f, 0, directions[2] / 4096f, directions[5] / 4096f, directions[8] / 4096f, 0, 0, 0, 0, 1);
        Matrix4x4 rotation = new(partNormalTransform.M11, partNormalTransform.M12, partNormalTransform.M13, 0, partNormalTransform.M21, partNormalTransform.M22, partNormalTransform.M23, 0, partNormalTransform.M31, partNormalTransform.M32, partNormalTransform.M33, 0, 0, 0, 0, 1);
        Matrix4x4 combined = color * direction * Matrix4x4.Transpose(rotation);
        red = new(combined.M11, combined.M12, combined.M13);
        green = new(combined.M21, combined.M22, combined.M23);
        blue = new(combined.M31, combined.M32, combined.M33);
        ambient = new Vector3(colors[0], colors[1], colors[2]) * (1f / 255);
        if (!Finite(red) || !Finite(green) || !Finite(blue))
        {
            throw new InvalidDataException("Custom field lighting requires a finite sampled part transform.");
        }
        zeroFloor = true;
    }

    /// <summary>Native group-local vertex ownership: the first encountered corner supplies its P normal index.</summary>
    public static int[] BindNormals(PcPolygonModel model, PcModelGroup group)
    {
        if (group.VertexStart < 0 || group.VertexCount < 0 || group.VertexStart > model.Vertices.Count - group.VertexCount || group.PolygonStart < 0 || group.PolygonCount < 0 || group.PolygonStart > model.Polygons.Count - group.PolygonCount)
        {
            throw new InvalidDataException("A field lighting group lies outside its original P model.");
        }
        int[] bindings = Enumerable.Repeat(-1, group.VertexCount).ToArray();
        if (model.Normals.Count == 0)
        {
            return bindings;
        }
        for (int index = group.PolygonStart; index < group.PolygonStart + group.PolygonCount; index++)
        {
            PcModelPolygon polygon = model.Polygons[index];
            Bind(polygon.A, polygon.NormalA);
            Bind(polygon.B, polygon.NormalB);
            Bind(polygon.C, polygon.NormalC);
        }
        return bindings;

        void Bind(int vertex, int normal)
        {
            if ((uint)vertex >= bindings.Length || (uint)normal >= model.Normals.Count)
            {
                throw new InvalidDataException("A field lighting corner references an absent native vertex or normal.");
            }
            if (bindings[vertex] < 0)
            {
                bindings[vertex] = normal;
            }
        }
    }
    /// <summary>The caller supplies the original normal rotated into the field light frame, excluding uniform geometry scale. Native normals and light directions are not renormalized.</summary>
    public Vector4 Shade(Vector3 normal, uint vertexArgb, uint? fieldColorArgb = null)
    {
        if (!Finite(normal))
        {
            throw new InvalidDataException("Native field lighting cannot shade a non-finite normal.");
        }
        float Channel(Vector3 coefficients, float minimum, byte original, int tint)
        {
            float factor = MathF.Max(zeroFloor ? 0 : minimum, Vector3.Dot(coefficients, normal) + minimum);
            float value = MathF.Min(255, original * factor);
            return MathF.Round(tint == 255 ? value : value * tint / 255f, MidpointRounding.ToEven) / 255;
        }
        uint tint = fieldColorArgb ?? uint.MaxValue;
        return new(Channel(red, ambient.X, (byte)(vertexArgb >> 16), (byte)(tint >> 16)), Channel(green, ambient.Y, (byte)(vertexArgb >> 8), (byte)(tint >> 8)), Channel(blue, ambient.Z, (byte)vertexArgb, (byte)tint), (vertexArgb >> 24) / 255f);
    }

    private static Vector3 Rgb(uint rgb)
    {
        return new(rgb & 255, rgb >> 8 & 255, rgb >> 16 & 255);
    }

    private static bool Finite(Vector3 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    }
}
