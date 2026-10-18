using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>463D50's uniform opaque WORD fill with only the two canonical X edges advanced.</summary>
public static class NativeSoftwareFlatScanlines
{
    public static NativeSoftwareScanlineEdges Draw463D50(byte[] image, NativeSoftwareScanlineEdges edges, int startY, int rowCount, uint duplicatedColor)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(edges);
        if (rowCount <= 0)
        {
            return edges;
        }
        Require(edges.Left);
        Require(edges.Right);
        byte[] left = edges.Left.Current.ToArray();
        byte[] right = edges.Right.Current.ToArray();
        int leftStep = BinaryPrimitives.ReadInt32LittleEndian(edges.Left.Step.AsSpan());
        int rightStep = BinaryPrimitives.ReadInt32LittleEndian(edges.Right.Step.AsSpan());
        int destinationRow = unchecked(startY * 2048);
        bool leftIsMinimum = true;
        ushort color = unchecked((ushort)duplicatedColor);
        for (int row = 0; row < rowCount; row++)
        {
            int leftX = BinaryPrimitives.ReadInt32LittleEndian(left);
            int rightX = BinaryPrimitives.ReadInt32LittleEndian(right);
            int minimum = leftIsMinimum ? leftX : rightX;
            int maximum = leftIsMinimum ? rightX : leftX;
            if (minimum > maximum)
            {
                leftIsMinimum = !leftIsMinimum;
                (minimum, maximum) = (maximum, minimum);
            }
            int first = minimum >> 16;
            int width = unchecked((maximum >> 16) - first);
            if (width > 0)
            {
                uint address = unchecked((uint)(destinationRow + 2 * first));
                if ((ulong)address + 2UL * (uint)width > (ulong)image.Length)
                {
                    throw new NotSupportedException("Native flat fill leaves its explicitly supplied mutable image owner.");
                }
                for (int pixel = 0; pixel < width; pixel++)
                {
                    BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(checked((int)address + pixel * 2), 2), color);
                }
            }
            BinaryPrimitives.WriteInt32LittleEndian(left, unchecked(leftX + leftStep));
            BinaryPrimitives.WriteInt32LittleEndian(right, unchecked(rightX + rightStep));
            destinationRow = unchecked(destinationRow + 2048);
        }
        return new(edges.Left with { Current = left.ToImmutableArray() }, edges.Right with { Current = right.ToImmutableArray() });

        static void Require(NativeSoftwareEdgeBuffers edge)
        {
            if (edge is null || edge.Current.IsDefault || edge.Current.Length < 4 || edge.Step.IsDefault || edge.Step.Length < 4)
            {
                throw new InvalidDataException("Native flat fill requires the two actual current and step X fields.");
            }
        }
    }
}
