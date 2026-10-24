using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeSoftwareReciprocalTable([property: JsonRequired] ImmutableArray<float> Values)
{
    public static NativeSoftwareReciprocalTable Initialize463280()
    {
        ImmutableArray<float>.Builder values = ImmutableArray.CreateBuilder<float>(3000);
        values.Add(0);
        for (int index = 1; index < 3000; index++)
        {
            values.Add((float)(1.0 / index));
        }
        return new(values.MoveToImmutable());
    }

    public float AtHeight(int height, NativeSoftwareScanlineEdges? physicalOwners = null)
    {
        if (Values.IsDefault || Values.Length != 3000 || height >= 3000)
        {
            throw new NotSupportedException($"Native software edge requires its initialized3000-float reciprocal owner and a physical height index0..2999; requested {height}.");
        }
        float value;
        if (height < 0)
        {
            if (physicalOwners is null || height < -28)
            {
                throw new NotSupportedException($"Native software reciprocal index {height} requires its preceding physical edge owners.");
            }
            int offset = 112 + height * 4;
            ImmutableArray<byte> owner;
            if (offset < 40)
            {
                owner = physicalOwners.Left.Current;
            }
            else if (offset < 80)
            {
                owner = physicalOwners.Right.Current;
                offset -= 40;
            }
            else
            {
                owner = physicalOwners.Right.Step;
                offset -= 80;
            }
            if (owner.IsDefault || offset + 4 > owner.Length)
            {
                throw new NotSupportedException("Native software reciprocal prefix requires its complete physical edge field.");
            }
            value = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(owner.AsSpan().Slice(offset, 4)));
        }
        else
        {
            value = Values[height];
        }
        if (!float.IsFinite(value))
        {
            throw new NotSupportedException("Native software edge reciprocal has an unsupported exceptional floating-point value.");
        }
        return value;
    }
}
public readonly record struct NativeSoftwareRasterVertex(int X16_16, int Y, int U1024, int V1024);
public sealed record NativeSoftwareEdgeBuffers([property: JsonRequired] ImmutableArray<byte> Current, [property: JsonRequired] ImmutableArray<byte> Step);

/// <summary>4632C0/463370 edge setup over separate explicit caller records and the retained float reciprocal table.</summary>
public static class NativeSoftwareRasterEdges
{
    public static NativeSoftwareEdgeBuffers SetupTextured4632C0(NativeSoftwareEdgeBuffers buffers, NativeSoftwareRasterVertex start, NativeSoftwareRasterVertex end, NativeSoftwareReciprocalTable reciprocals, NativeSoftwareScanlineEdges? physicalOwners = null)
    {
        return Setup(buffers, start, end, reciprocals, true, physicalOwners);
    }

    public static NativeSoftwareEdgeBuffers SetupUntextured463370(NativeSoftwareEdgeBuffers buffers, NativeSoftwareRasterVertex start, NativeSoftwareRasterVertex end, NativeSoftwareReciprocalTable reciprocals, NativeSoftwareScanlineEdges? physicalOwners = null)
    {
        return Setup(buffers, start, end, reciprocals, false, physicalOwners);
    }

    private static NativeSoftwareEdgeBuffers Setup(NativeSoftwareEdgeBuffers buffers, NativeSoftwareRasterVertex start, NativeSoftwareRasterVertex end, NativeSoftwareReciprocalTable reciprocals, bool textured, NativeSoftwareScanlineEdges? physicalOwners)
    {
        ArgumentNullException.ThrowIfNull(buffers);
        ArgumentNullException.ThrowIfNull(reciprocals);
        int length = textured ? 20 : 4;
        if (buffers.Current.IsDefault || buffers.Step.IsDefault || buffers.Current.Length < length || buffers.Step.Length < length)
        {
            throw new InvalidDataException("Native software edge requires both complete caller-owned field ranges.");
        }
        float reciprocal = reciprocals.AtHeight(unchecked(end.Y - start.Y), physicalOwners);
        byte[] current = buffers.Current.ToArray();
        byte[] step = buffers.Step.ToArray();
        Set(0, start.X16_16, end.X16_16);
        if (textured)
        {
            Set(0xC, start.U1024, end.U1024);
            Set(0x10, start.V1024, end.V1024);
        }
        return new(current.ToImmutableArray(), step.ToImmutableArray());

        void Set(int offset, int from, int to)
        {
            int delta = unchecked(to - from);
            int slope = NativePrecision53.RoundInteger(delta * (double)reciprocal);
            BinaryPrimitives.WriteInt32LittleEndian(step.AsSpan(offset, 4), slope);
            BinaryPrimitives.WriteInt32LittleEndian(current.AsSpan(offset, 4), from);
        }
    }
}
