using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeSoftwareClipBounds([property: JsonRequired] int LeftX16_16, [property: JsonRequired] int TopY, [property: JsonRequired] int RightX16_16, [property: JsonRequired] int BottomY);
public sealed record NativeSoftwareClipVertex([property: JsonRequired] int X16_16, [property: JsonRequired] int Y, [property: JsonRequired] int? U1024, [property: JsonRequired] int? V1024, [property: JsonRequired] int? Red, [property: JsonRequired] int? Green, [property: JsonRequired] int? Blue);

/// <summary>Only the clip/raster consumed fields: header bytes4..11 and vertices without unconsumed Z or scratch residue.</summary>
public sealed record NativeSoftwareClippedPolygon([property: JsonRequired] ImmutableArray<byte> HeaderTail, [property: JsonRequired] ImmutableArray<NativeSoftwareClipVertex> Vertices)
{
    [JsonIgnore] public byte Flags => HeaderTail.Length == 8 ? HeaderTail[0] : throw new InvalidDataException("Native clipped polygon lacks its retained header bytes4..11.");
    [JsonIgnore] public byte Variant => HeaderTail.Length == 8 ? HeaderTail[7] : throw new InvalidDataException("Native clipped polygon lacks its retained header bytes4..11.");

    public void ValidateConsumedFields() => NativeSoftwarePolygonClip.Validate(this);

    public NativeSoftwareRasterVertex RasterVertex(int index)
    {
        ValidateConsumedFields();
        if ((uint)index >= (uint)Vertices.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
        NativeSoftwareClipVertex vertex = Vertices[index];
        // The untextured edge path never consumes UV; these normalized unused values do not describe physical residue.
        return new(vertex.X16_16, vertex.Y, vertex.U1024 ?? 0, vertex.V1024 ?? 0);
    }
}

/// <summary>466320/466360/466880's signed inclusive outcodes and ordered four-plane consumed-field clipping.</summary>
public static class NativeSoftwarePolygonClip
{
    public static byte Outcode466320(int x16_16, int y, NativeSoftwareClipBounds bounds)
    {
        ArgumentNullException.ThrowIfNull(bounds);
        byte result = x16_16 < bounds.LeftX16_16 ? (byte)1 : x16_16 > bounds.RightX16_16 ? (byte)4 : (byte)0;
        result |= y < bounds.TopY ? (byte)2 : y > bounds.BottomY ? (byte)8 : (byte)0;
        return result;
    }

    public static NativeSoftwareClippedPolygon ReadConsumedFields(NativeSoftwarePolygonDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        descriptor.ValidateLayout();
        if (descriptor.VertexCount > 14)
        {
            throw new NotSupportedException("Native polygon clip exceeds its fourteen-vertex temporary capacity.");
        }
        ImmutableArray<NativeSoftwareClipVertex>.Builder vertices = ImmutableArray.CreateBuilder<NativeSoftwareClipVertex>();
        for (int index = 0; index < descriptor.VertexCount; index++)
        {
            ImmutableArray<byte> bytes = descriptor.VertexData(index);
            bool uv = (descriptor.Flags & 2) != 0;
            bool rgb = (descriptor.Flags & 1) != 0;
            vertices.Add(new(Read(0), Read(4), uv ? Read(0xC) : null, uv ? Read(0x10) : null, rgb ? Read(0x14) : null, rgb ? Read(0x18) : null, rgb ? Read(0x1C) : null));
            int Read(int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan().Slice(offset));
        }
        return new(descriptor.Data.Slice(4, 8), vertices.ToImmutable());
    }

    public static NativeSoftwareClippedPolygon Clip466360(NativeSoftwarePolygonDescriptor descriptor, NativeSoftwareClipBounds bounds) => Clip466360(ReadConsumedFields(descriptor), bounds);

    public static NativeSoftwareClippedPolygon Clip466360(NativeSoftwareClippedPolygon polygon, NativeSoftwareClipBounds bounds)
    {
        Validate(polygon);
        ArgumentNullException.ThrowIfNull(bounds);
        ImmutableArray<NativeSoftwareClipVertex> vertices = polygon.Vertices;
        for (int plane = 0; plane < 4; plane++)
        {
            if (vertices.Length == 0)
            {
                continue;
            }
            ImmutableArray<NativeSoftwareClipVertex>.Builder output = ImmutableArray.CreateBuilder<NativeSoftwareClipVertex>();
            NativeSoftwareClipVertex previous = vertices[^1];
            bool previousOutside = Outside(previous, plane, bounds);
            foreach (NativeSoftwareClipVertex current in vertices)
            {
                bool currentOutside = Outside(current, plane, bounds);
                if (currentOutside != previousOutside)
                {
                    int boundary = plane switch { 0 => bounds.LeftX16_16, 1 => bounds.TopY, 2 => bounds.RightX16_16, _ => bounds.BottomY };
                    Add(Intersection(current, previous, boundary, plane is 0 or 2, polygon.Flags));
                }
                if (!currentOutside)
                {
                    Add(current);
                }
                previous = current;
                previousOutside = currentOutside;
            }
            vertices = output.ToImmutable();

            void Add(NativeSoftwareClipVertex vertex)
            {
                if (output.Count == 14)
                {
                    throw new NotSupportedException("Native polygon clip exceeds its fourteen-vertex temporary capacity.");
                }
                output.Add(vertex);
            }
        }
        return polygon with { Vertices = vertices };
    }

    public static void Submit466880(NativeSoftwarePolygonDescriptor descriptor, NativeSoftwareClipBounds bounds, Action<NativeSoftwarePolygonDescriptor> drawDirect, Action<NativeSoftwareClippedPolygon> drawClipped)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        descriptor.ValidateLayout();
        Submit(descriptor.VertexCount, ReadPosition, bounds, () => (drawDirect ?? throw new NotSupportedException("Native polygon admission requires its direct raster consumer."))(descriptor), () => (drawClipped ?? throw new NotSupportedException("Native polygon admission requires its clipped consumed-field raster consumer."))(Clip466360(descriptor, bounds)));

        (int X, int Y) ReadPosition(int index)
        {
            ImmutableArray<byte> vertex = descriptor.VertexData(index);
            return (BinaryPrimitives.ReadInt32LittleEndian(vertex.AsSpan()), BinaryPrimitives.ReadInt32LittleEndian(vertex.AsSpan().Slice(4)));
        }
    }

    public static void Submit466880(NativeSoftwareClippedPolygon descriptor, NativeSoftwareClipBounds bounds, Action<NativeSoftwareClippedPolygon> drawDirect, Action<NativeSoftwareClippedPolygon> drawClipped)
    {
        Validate(descriptor);
        Submit(descriptor.Vertices.Length, index => (descriptor.Vertices[index].X16_16, descriptor.Vertices[index].Y), bounds, () => (drawDirect ?? throw new NotSupportedException("Native polygon admission requires its direct consumed-field raster consumer."))(descriptor), () => (drawClipped ?? throw new NotSupportedException("Native polygon admission requires its clipped consumed-field raster consumer."))(Clip466360(descriptor, bounds)));
    }

    private static void Submit(int count, Func<int, (int X, int Y)> readPosition, NativeSoftwareClipBounds bounds, Action drawDirect, Action drawClipped)
    {
        ArgumentNullException.ThrowIfNull(bounds);
        if (count == 0)
        {
            return;
        }
        byte any = 0;
        byte all = 0xFF;
        for (int index = 0; index < count; index++)
        {
            (int x, int y) = readPosition(index);
            byte code = Outcode466320(x, y, bounds);
            any |= code;
            all &= code;
        }
        if (all != 0)
        {
            return;
        }
        if (any == 0)
        {
            drawDirect();
        }
        else
        {
            drawClipped();
        }
    }

    internal static NativeSoftwareClipVertex Intersection(NativeSoftwareClipVertex current, NativeSoftwareClipVertex previous, int boundary, bool xPlane, byte flags)
    {
        RequireAttributes(current, flags);
        RequireAttributes(previous, flags);
        int dx = unchecked(current.X16_16 - previous.X16_16);
        int dy = unchecked(current.Y - previous.Y);
        int denominator = xPlane ? dx : dy;
        if (denominator == 0)
        {
            denominator = 1;
        }
        int numerator = unchecked(boundary - (xPlane ? current.X16_16 : current.Y));
        double ratio = numerator / (double)denominator;
        int x = xPlane ? boundary : unchecked(current.X16_16 + NativePrecision53.RoundInteger(dx * ratio));
        int y = xPlane ? unchecked(current.Y + NativePrecision53.RoundInteger(dy * ratio)) : boundary;
        bool uv = (flags & 2) != 0;
        bool rgb = (flags & 1) != 0;
        return new(x, y, uv ? Attribute(current.U1024!.Value, previous.U1024!.Value) : null, uv ? Attribute(current.V1024!.Value, previous.V1024!.Value) : null, rgb ? Attribute(current.Red!.Value, previous.Red!.Value) : null, rgb ? Attribute(current.Green!.Value, previous.Green!.Value) : null, rgb ? Attribute(current.Blue!.Value, previous.Blue!.Value) : null);

        int Attribute(int currentValue, int previousValue) => unchecked(currentValue + NativePrecision53.RoundInteger(unchecked(currentValue - previousValue) * ratio));
    }

    private static bool Outside(NativeSoftwareClipVertex vertex, int plane, NativeSoftwareClipBounds bounds) => plane switch { 0 => vertex.X16_16 < bounds.LeftX16_16, 1 => vertex.Y < bounds.TopY, 2 => vertex.X16_16 > bounds.RightX16_16, _ => vertex.Y > bounds.BottomY };

    internal static void Validate(NativeSoftwareClippedPolygon polygon)
    {
        ArgumentNullException.ThrowIfNull(polygon);
        if (polygon.HeaderTail.IsDefault || polygon.HeaderTail.Length != 8 || polygon.Vertices.IsDefault || polygon.Vertices.Length > 14)
        {
            throw new InvalidDataException("Native clip requires its retained eight header bytes and at most fourteen consumed vertices.");
        }
        foreach (NativeSoftwareClipVertex vertex in polygon.Vertices)
        {
            RequireAttributes(vertex, polygon.Flags);
        }
    }

    private static void RequireAttributes(NativeSoftwareClipVertex vertex, byte flags)
    {
        if (vertex is null || (flags & 2) != 0 && (vertex.U1024 is null || vertex.V1024 is null) || (flags & 1) != 0 && (vertex.Red is null || vertex.Green is null || vertex.Blue is null))
        {
            throw new NotSupportedException("Native clip requires every enabled UV and RGB attribute owner.");
        }
    }
}
