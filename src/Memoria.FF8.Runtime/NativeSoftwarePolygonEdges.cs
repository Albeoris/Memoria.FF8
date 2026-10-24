using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeSoftwarePolygonDescriptor([property: JsonRequired] ImmutableArray<byte> Data)
{
    [JsonIgnore]
    public int VertexCount => Data.IsDefault || Data.Length < 4 ? throw new InvalidDataException("Native polygon descriptor lacks its count field.") : BinaryPrimitives.ReadInt32LittleEndian(Data.AsSpan());
    [JsonIgnore]
    public byte Flags => Data.IsDefault || Data.Length < 12 ? throw new InvalidDataException("Native polygon descriptor lacks its complete header.") : Data[4];
    [JsonIgnore]
    public byte Variant => Data.IsDefault || Data.Length < 12 ? throw new InvalidDataException("Native polygon descriptor lacks its complete header.") : Data[11];

    public void ValidateLayout()
    {
        int count = VertexCount;
        if (count < 0 || Data.Length < 12 || 12L + count * 32L > Data.Length)
        {
            throw new InvalidDataException("Native polygon count leaves its supplied32-byte vertex records.");
        }
    }
    public ImmutableArray<byte> VertexData(int index)
    {
        ValidateLayout();
        if ((uint)index >= (uint)VertexCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
        return Data.AsSpan().Slice(checked(12 + index * 32), 32).ToArray().ToImmutableArray();
    }
    public NativeSoftwareRasterVertex Vertex(int index)
    {
        ImmutableArray<byte> data = VertexData(index);
        return new(Read(0), Read(4), Read(0xC), Read(0x10));
        int Read(int offset) => BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan().Slice(offset, 4));
    }
}

public sealed record NativeSoftwarePolygonRasterState([property: JsonRequired] NativeSoftwareScanlineEdges Edges, [property: JsonRequired] NativeSoftwarePolygonDescriptor? PublishedDescriptor)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeSoftwareClippedPolygon? PublishedClippedDescriptor { get; init; }
    [JsonIgnore]
    public uint Color555Pair => BinaryPrimitives.ReadUInt32LittleEndian(Edges.Left.Current.AsSpan().Slice(0x20, 4));
    [JsonIgnore]
    public int SegmentY => BinaryPrimitives.ReadInt32LittleEndian(Edges.Right.Current.AsSpan().Slice(0x20, 4));
}

/// <summary>463DF0's ordered polygon edge chains and shared edge budget, with explicit supported row delivery.</summary>
public static class NativeSoftwarePolygonEdges
{
    public static NativeSoftwarePolygonRasterState Rasterize463DF0(byte[] image, NativeSoftwarePolygonRasterState state, NativeSoftwarePolygonDescriptor descriptor, NativeSoftwareReciprocalTable reciprocals, int depth, int texturePageByteOffset, int paletteByteOffset)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(descriptor);
        int count = descriptor.VertexCount;
        if (count < 3)
        {
            return state;
        }
        descriptor.ValidateLayout();
        state = state with { PublishedDescriptor = descriptor, PublishedClippedDescriptor = null };
        return Rasterize(image, state, count, descriptor.Flags, descriptor.Variant, PackColor(descriptor.Data[8], descriptor.Data[9], descriptor.Data[10]), descriptor.Vertex, reciprocals, depth, texturePageByteOffset, paletteByteOffset);
    }

    public static NativeSoftwarePolygonRasterState Rasterize463DF0(byte[] image, NativeSoftwarePolygonRasterState state, NativeSoftwareClippedPolygon descriptor, NativeSoftwareReciprocalTable reciprocals, int depth, int texturePageByteOffset, int paletteByteOffset)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(descriptor);
        if (descriptor.Vertices.IsDefault)
        {
            throw new InvalidDataException("Native clipped polygon has no known vertex-count owner.");
        }
        int count = descriptor.Vertices.Length;
        if (count < 3)
        {
            return state;
        }
        if (descriptor.HeaderTail.IsDefault || descriptor.HeaderTail.Length != 8)
        {
            throw new InvalidDataException("Native clipped polygon requires its exact retained header fields.");
        }
        state = state with { PublishedDescriptor = null, PublishedClippedDescriptor = descriptor };
        return Rasterize(image, state, count, descriptor.Flags, descriptor.Variant, PackColor(descriptor.HeaderTail[4], descriptor.HeaderTail[5], descriptor.HeaderTail[6]), descriptor.RasterVertex, reciprocals, depth, texturePageByteOffset, paletteByteOffset);
    }

    private static NativeSoftwarePolygonRasterState Rasterize(byte[] image, NativeSoftwarePolygonRasterState state, int count, byte flags, byte variant, uint color, Func<int, NativeSoftwareRasterVertex> vertex, NativeSoftwareReciprocalTable reciprocals, int depth, int texturePageByteOffset, int paletteByteOffset)
    {
        RequireCurrent(state.Edges.Left);
        RequireCurrent(state.Edges.Right);
        bool textured = (flags & 2) != 0;
        int minimumY = 9999999;
        int minimumIndex = -1;
        for (int index = 0; index < count; index++)
        {
            int y = vertex(index).Y;
            if (y is <= -9999999 or >= 9999999)
            {
                throw new NotSupportedException("Native polygon minimum selection requires its proved bounded float-exact integer Y domain.");
            }
            if (y < minimumY)
            {
                minimumY = y;
                minimumIndex = index;
            }
        }
        NativeSoftwareScanlineEdges edges = state.Edges with { Left = WriteCurrent(state.Edges.Left, 0x20, color) };
        int startY = minimumY;
        int leftIndex = minimumIndex;
        int rightIndex = minimumIndex;
        int leftEndY = unchecked(startY - 1);
        int rightEndY = leftEndY;
        int remaining = count;
        while (remaining > 0)
        {
            edges = edges with { Right = WriteCurrent(edges.Right, 0x20, unchecked((uint)startY)) };
            if (leftEndY <= startY)
            {
                while (remaining > 0)
                {
                    remaining--;
                    int next = leftIndex == 0 ? count - 1 : leftIndex - 1;
                    NativeSoftwareRasterVertex from = vertex(leftIndex);
                    NativeSoftwareRasterVertex to = vertex(next);
                    edges = edges with { Left = Setup(edges.Left, from, to) };
                    leftEndY = to.Y;
                    leftIndex = next;
                    if (leftEndY > startY)
                    {
                        break;
                    }
                }
            }
            if (rightEndY <= startY)
            {
                while (remaining > 0)
                {
                    remaining--;
                    int next = rightIndex + 1 == count ? 0 : rightIndex + 1;
                    NativeSoftwareRasterVertex from = vertex(rightIndex);
                    NativeSoftwareRasterVertex to = vertex(next);
                    edges = edges with { Right = Setup(edges.Right, from, to) };
                    rightEndY = to.Y;
                    rightIndex = next;
                    if (rightEndY > startY)
                    {
                        break;
                    }
                }
            }
            int nextY = Math.Min(leftEndY, rightEndY);
            int rows = unchecked(nextY - startY);
            if (rows > 0)
            {
                edges = textured ? NativeSoftwareTexturedScanlines.Draw463BC0(image, edges, reciprocals, startY, rows, flags, variant, depth, texturePageByteOffset, paletteByteOffset) : NativeSoftwareFlatScanlines.Draw463D50(image, edges, startY, rows, color);
            }
            startY = nextY;
        }
        return state with { Edges = edges };

        NativeSoftwareEdgeBuffers Setup(NativeSoftwareEdgeBuffers owner, NativeSoftwareRasterVertex from, NativeSoftwareRasterVertex to) => textured ? NativeSoftwareRasterEdges.SetupTextured4632C0(owner, from, to, reciprocals, edges) : NativeSoftwareRasterEdges.SetupUntextured463370(owner, from, to, reciprocals, edges);
    }

    private static uint PackColor(byte red, byte green, byte blue)
    {
        uint color = (uint)((red >> 3) | ((green & 0xF8) << 2) | ((blue & 0xF8) << 7));
        return color | color << 16;
    }

    private static void RequireCurrent(NativeSoftwareEdgeBuffers edge)
    {
        if (edge is null || edge.Current.IsDefault || edge.Current.Length < 0x28)
        {
            throw new InvalidDataException("Native polygon edge owner requires its complete40-byte current record including physical aliases.");
        }
    }
    private static NativeSoftwareEdgeBuffers WriteCurrent(NativeSoftwareEdgeBuffers owner, int offset, uint value)
    {
        byte[] current = owner.Current.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(current.AsSpan(offset, 4), value);
        return owner with { Current = current.ToImmutableArray() };
    }
}
