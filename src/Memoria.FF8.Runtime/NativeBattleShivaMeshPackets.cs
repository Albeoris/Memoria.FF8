using System.Buffers.Binary;
using System.Collections;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleShivaMeshPacketResult(NativeBattleGfGeometryBatch Batch, int Visited, int PolygonCursorOffset);

/// <summary>5C1B50/5C1BB0's FT4 prefix UV stream and independently selected suffix geometry.</summary>
public static class NativeBattleShivaMeshPackets
{
    public static NativeBattleShivaMeshPacketResult Emit5C1B50(NativeBattleStaticMeshResource source, ImmutableArray<byte> selectedVertices, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, NativeBattleGfGeometryContext context, int maximumCount, int windowCount, short zsf4, short dqa, int dqb, NativeBattleSummonColors? colors = null, Action<ushort>? observeDepth = null, Func<int, NativeBattleVector>? readSelectedVertex = null)
    {
        if (source.Address != 0xD215C4 || source.Bytes.IsDefault || source.Bytes.Length != 0x5D8 || maximumCount is < 0 or > 36 || windowCount is < 0 or > 36)
        {
            throw new InvalidDataException("Native5C1B50 requires its complete original mesh and bounded FT4 suffix/window counts.");
        }
        RequireContext(context);
        matrix.Validate();
        int group = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(source.Bytes.AsSpan()) + 12);
        if (BinaryPrimitives.ReadUInt32LittleEndian(source.Bytes.AsSpan().Slice(group)) != 36)
        {
            throw new InvalidDataException("Native5C1BB0 requires its original thirty-six FT4 records.");
        }
        int first = checked(group + 4);
        int count = Math.Min(maximumCount, windowCount);
        int step = 128 / (windowCount == 36 ? maximumCount + 5 : windowCount + 2);
        uint packedStep = (uint)(step | step << 8 | step << 16);
        uint shade = 0x2E909090;
        int allocation = 0;
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>(count);
        for (int index = 0; index < count; index++)
        {
            int uvRecord = checked(first + index * 24);
            int geometryRecord = checked(first + (36 - maximumCount + index) * 24);
            byte[] face = source.Bytes.AsSpan().Slice(uvRecord, 24).ToArray();
            source.Bytes.AsSpan().Slice(geometryRecord + 4, 8).CopyTo(face.AsSpan(4));
            BinaryPrimitives.WriteUInt32LittleEndian(face, shade);
            shade = unchecked(shade - packedStep);
            NativeBattleGfGeometryAttempt attempt = EmitFace5C1BB0(face, corner => Project(corner, face), context, zsf4, colors, allocation, observeDepth);
            attempts.Add(attempt);
            if (attempt.Primitive is not null)
            {
                allocation = checked(allocation + 40);
            }
        }
        return new(new(attempts.MoveToImmutable(), allocation), count, checked(first + count * 24));

        NativeBattleModuleProjectedVertex Project(int corner, byte[] face)
        {
            int offset = checked(BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(4 + corner * 2)) * 4);
            if ((context.Flags & 0x2000) != 0 && readSelectedVertex is not null)
            {
                NativeBattleVector selected = readSelectedVertex(offset);
                return corner == 3 ? NativeBattleModuleProjection.Project45FAA0(matrix, selected, projection, dqa, dqb) : NativeBattleModuleProjection.Project45FE10(matrix, selected, projection);
            }
            ReadOnlySpan<byte> bytes = (context.Flags & 0x2000) != 0
                ? selectedVertices.IsDefault ? throw new NotSupportedException("Native5C1B50 flag2000 requires its actual mutable vertex pointer.") : selectedVertices.AsSpan()
                : source.Bytes.AsSpan().Slice(8);
            if (offset > bytes.Length - 6)
            {
                throw new InvalidDataException("Native5C1BB0 vertex reference leaves its selected source.");
            }
            ReadOnlySpan<byte> xyz = bytes.Slice(offset, 6);
            NativeBattleVector vertex = new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.Slice(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.Slice(4)));
            return corner == 3 ? NativeBattleModuleProjection.Project45FAA0(matrix, vertex, projection, dqa, dqb) : NativeBattleModuleProjection.Project45FE10(matrix, vertex, projection);
        }
    }

    public static NativeBattleGfGeometryAttempt EmitFace5C1BB0(ReadOnlySpan<byte> face, Func<int, NativeBattleModuleProjectedVertex> readCorner, NativeBattleGfGeometryContext context, short zsf4, NativeBattleSummonColors? colors = null, int byteOffsetRelative = 0, Action<ushort>? observeDepth = null)
    {
        RequireContext(context);
        if (face.Length != 24)
        {
            throw new InvalidDataException("Native5C1BB0 requires its complete FT4 record.");
        }
        ProjectedCorners corners = new(readCorner);
        NativeBattleModuleProjectedVertex first = corners[0];
        NativeBattleModuleProjectedVertex second = corners[1];
        NativeBattleModuleProjectedVertex third = corners[2];
        if (((first.Flags | second.Flags | third.Flags) & 0x60000) == 0 && NativeBattleModulePackets.Nclip(first.Screen, second.Screen, third.Screen) == 0)
        {
            observeDepth?.Invoke(first.Depth);
            observeDepth?.Invoke(second.Depth);
            observeDepth?.Invoke(third.Depth);
            return RejectedPrefix(face, byteOffsetRelative);
        }
        NativeBattleGfGeometryAttempt attempt = NativeBattleGfGeometryPackets.EmitAttempt7043B0(face, corners, 3, context, colors, byteOffsetRelative, observeDepth);
        if (attempt.Primitive is not { } primitive)
        {
            return attempt;
        }
        uint average = unchecked((uint)primitive.VertexPayload4.Sum() * (uint)(int)zsf4) >> 12;
        int bucket = unchecked(context.TableBaseBucket + (unchecked((int)average) >> context.DepthShift));
        return attempt with { Primitive = primitive with { Bucket = bucket } };
    }

    private static void RequireContext(NativeBattleGfGeometryContext context)
    {
        if ((context.Flags & ~0x20F0u) != 0 || context.DepthBias != 0 || context.DepthShift > 31)
        {
            throw new NotSupportedException("Native5C1BB0 requires its reached context flags and unmodified AVSZ4 depth.");
        }
    }

    private static NativeBattleGfGeometryAttempt RejectedPrefix(ReadOnlySpan<byte> face, int offset)
    {
        byte[] bytes = new byte[40];
        NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[40];
        Store(0, 0x09000000);
        Store(4, BinaryPrimitives.ReadUInt32LittleEndian(face));
        Store(12, BinaryPrimitives.ReadUInt32LittleEndian(face.Slice(12)));
        Store(20, BinaryPrimitives.ReadUInt32LittleEndian(face.Slice(16)));
        uint finalUv = BinaryPrimitives.ReadUInt32LittleEndian(face.Slice(20));
        Store(28, finalUv);
        Store(36, finalUv >> 16);
        return new(offset, bytes.ToImmutableArray(), mask.ToImmutableArray(), null);

        void Store(int byteOffset, uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(byteOffset), value);
            Array.Fill(mask, NativeBattleGfPacketByteState.Known, byteOffset, 4);
        }
    }

    private sealed class ProjectedCorners(Func<int, NativeBattleModuleProjectedVertex> readCorner) : IReadOnlyList<NativeBattleModuleProjectedVertex>
    {
        private readonly NativeBattleModuleProjectedVertex?[] projected = new NativeBattleModuleProjectedVertex?[4];

        public int Count => 4;

        public NativeBattleModuleProjectedVertex this[int index] => projected[index] ?? (projected[index] = readCorner(index)).Value;

        public IEnumerator<NativeBattleModuleProjectedVertex> GetEnumerator()
        {
            for (int index = 0; index < Count; index++)
            {
                yield return this[index];
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
