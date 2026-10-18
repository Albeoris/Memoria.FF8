using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeSoftwareScanlineEdges([property: JsonRequired] NativeSoftwareEdgeBuffers Left, [property: JsonRequired] NativeSoftwareEdgeBuffers Right);

/// <summary>463BC0's textured row batch over two canonical edge owners and the explicit opaque8-bit span path.</summary>
public static class NativeSoftwareTexturedScanlines
{
    public static NativeSoftwareScanlineEdges Draw463BC0(byte[] image, NativeSoftwareScanlineEdges edges, NativeSoftwareReciprocalTable reciprocals, int startY, int rowCount, byte descriptorFlags, byte variant, int depth, int texturePageByteOffset, int paletteByteOffset)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(edges);
        RequireOpaque8Bit(descriptorFlags, variant, depth);
        if (rowCount <= 0)
        {
            return edges;
        }
        ArgumentNullException.ThrowIfNull(reciprocals);
        RequireEdge(edges.Left);
        RequireEdge(edges.Right);
        byte[] left = edges.Left.Current.ToArray();
        byte[] right = edges.Right.Current.ToArray();
        bool minimumIsLeft = true;
        int destinationRow = unchecked(startY << 11);
        for (int row = 0; row < rowCount; row++)
        {
            byte[] minimum = minimumIsLeft ? left : right;
            byte[] maximum = minimumIsLeft ? right : left;
            if (Read(minimum, 0) > Read(maximum, 0))
            {
                minimumIsLeft = !minimumIsLeft;
                (minimum, maximum) = (maximum, minimum);
            }
            int leftX = Read(minimum, 0) >> 16;
            int rightX = Read(maximum, 0) >> 16;
            int width = unchecked(rightX - leftX);
            if (width >= 0)
            {
                float reciprocal = reciprocals.AtHeight(width);
                int u = Read(minimum, 0xC);
                int v = Read(minimum, 0x10);
                int du = NativePrecision53.RoundInteger(unchecked(Read(maximum, 0xC) - u) * (double)reciprocal);
                int dv = NativePrecision53.RoundInteger(unchecked(Read(maximum, 0x10) - v) * (double)reciprocal);
                NativeSoftwareVramSpan.WriteOpaque8Bit463650(image, destinationRow, leftX, rightX, u, du, v, dv, texturePageByteOffset, paletteByteOffset);
            }
            Advance(left, edges.Left.Step, 0xC);
            Advance(left, edges.Left.Step, 0x10);
            Advance(right, edges.Right.Step, 0xC);
            Advance(right, edges.Right.Step, 0x10);
            Advance(left, edges.Left.Step, 0);
            Advance(right, edges.Right.Step, 0);
            destinationRow = unchecked(destinationRow + 2048);
        }
        return new(edges.Left with { Current = left.ToImmutableArray() }, edges.Right with { Current = right.ToImmutableArray() });
    }

    private static void RequireOpaque8Bit(byte flags, byte variant, int depth)
    {
        if ((uint)depth >= 3 || (flags & 4) != 0 && variant >= 4)
        {
            throw new NotSupportedException("Native textured span dispatch index leaves its supplied original table.");
        }
        if (depth != 1 || (flags & 4) != 0 && variant is not (0 or 3))
        {
            throw new NotSupportedException("Native textured scanline selected a span writer outside its proved opaque8-bit contract.");
        }
    }
    private static void RequireEdge(NativeSoftwareEdgeBuffers edge)
    {
        if (edge is null || edge.Current.IsDefault || edge.Step.IsDefault || edge.Current.Length < 20 || edge.Step.Length < 20)
        {
            throw new InvalidDataException("Native scanline batch requires its separate canonical current and step field owners.");
        }
    }
    private static int Read(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset, 4));
    private static void Advance(Span<byte> current, ImmutableArray<byte> step, int offset) => BinaryPrimitives.WriteInt32LittleEndian(current.Slice(offset, 4), unchecked(Read(current, offset) + Read(step.AsSpan(), offset)));
}
