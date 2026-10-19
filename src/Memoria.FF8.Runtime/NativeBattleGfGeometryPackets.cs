using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGfGeometryContext(uint Flags, uint FarColor, int DepthCue, ushort? TexturePage, ushort? Clut, int DepthBias, int TableBaseBucket, byte DepthShift);

internal sealed record NativeBattleMeshPacketContext(uint Flags, uint? FarColor, int? DepthCue, ushort? TexturePage, ushort? Clut, uint? PackedUvOffset, int DepthBias, int TableBaseBucket, byte DepthShift);

public enum NativeBattleGfPacketByteState : byte
{
    Untouched,
    Known,
    Unknown
}

/// <summary>Ordered native stores at the current allocation cursor; Untouched bytes retain actual prior arena data.</summary>
public sealed record NativeBattleGfGeometryAttempt(int ByteOffsetRelative, ImmutableArray<byte> Data, ImmutableArray<NativeBattleGfPacketByteState> WriteMask, NativeBattleGeometryPrimitive? Primitive)
{
    public int PacketLength => Data.Length;
}

public sealed record NativeBattleGfGeometryBatch(ImmutableArray<NativeBattleGfGeometryAttempt> Attempts, int AllocationBytes);

/// <summary>7043B0's eight original geometry groups, using actual45FE10/45FAA0 projection and native packet layouts.</summary>
public static class NativeBattleGfGeometryPackets
{
    private static readonly int[] GroupLengths = [12, 12, 20, 24, 20, 24, 28, 36];

    public static ImmutableArray<NativeBattleGeometryPrimitive> Emit7043B0(ImmutableArray<byte> source, int geometryOffset, ImmutableArray<byte> selectedVertices, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, NativeBattleGfGeometryContext context, NativeBattleSummonColors? colors = null)
    {
        return EmitBatch7043B0(source, geometryOffset, selectedVertices, matrix, projection, context, colors).Attempts.Where(attempt => attempt.Primitive is not null).Select(attempt => attempt.Primitive!).ToImmutableArray();
    }

    public static NativeBattleGfGeometryBatch EmitBatch7043B0(ImmutableArray<byte> source, int geometryOffset, ImmutableArray<byte> selectedVertices, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, NativeBattleGfGeometryContext context, NativeBattleSummonColors? colors = null, Func<int, NativeBattleVector>? readSelectedVertex = null, Action<ushort>? observeDepth = null)
    {
        return EmitBatch(source, geometryOffset, selectedVertices, matrix, projection, Normalize(context), colors, readSelectedVertex, observeDepth);
    }

    internal static NativeBattleGfGeometryBatch EmitBatch(ImmutableArray<byte> source, int geometryOffset, ImmutableArray<byte> selectedVertices, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, NativeBattleMeshPacketContext context, NativeBattleSummonColors? colors, Func<int, NativeBattleVector>? readSelectedVertex = null, Action<ushort>? observeDepth = null, Action<int>? observeSourceCursor = null)
    {
        if (source.IsDefault || context.DepthShift > 31)
        {
            throw new InvalidDataException("Native7043B0 requires its actual current geometry source and depth shift.");
        }
        matrix.Validate();
        ImmutableArray<byte> vertices;
        if ((context.Flags & 0x2000) != 0)
        {
            vertices = selectedVertices.IsDefault && readSelectedVertex is null ? throw new NotSupportedException("Native7043B0 flag2000 requires its retained selected or morphed vertex bytes.") : selectedVertices;
        }
        else
        {
            int start = checked(geometryOffset + 8);
            vertices = Read(source, start, source.Length - start).ToArray().ToImmutableArray();
        }
        int cursor = checked(geometryOffset + Int32(source, geometryOffset));
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        Dictionary<ushort, NativeBattleModuleProjectedVertex> projected = [];
        for (int group = 0; group < GroupLengths.Length; group++)
        {
            int count = Int32(source, cursor);
            cursor = checked(cursor + 4);
            if (count <= 0)
            {
                continue;
            }
            for (int index = 0; index < count; index++)
            {
                ReadOnlySpan<byte> face = Read(source, cursor, GroupLengths[group]);
                ushort[] offsets = new ushort[(group & 1) == 0 ? 3 : 4];
                for (int corner = 0; corner < offsets.Length; corner++)
                {
                    offsets[corner] = BinaryPrimitives.ReadUInt16LittleEndian(face.Slice(4 + corner * 2));
                }
                ProjectedCorners corners = new(offsets, Project);
                NativeBattleGfGeometryAttempt attempt = EmitAttempt(face, corners, group, context, colors, allocated, observeDepth);
                attempts.Add(attempt);
                if (attempt.Primitive is not null)
                {
                    allocated = checked(allocated + attempt.PacketLength);
                }
                cursor = checked(cursor + GroupLengths[group]);
            }
        }
        observeSourceCursor?.Invoke(cursor);
        return new(attempts.ToImmutable(), allocated);

        NativeBattleModuleProjectedVertex Project(ushort offset)
        {
            if (!projected.TryGetValue(offset, out NativeBattleModuleProjectedVertex vertex))
            {
                NativeBattleVector position;
                if ((context.Flags & 0x2000) != 0 && readSelectedVertex is not null)
                {
                    position = readSelectedVertex(offset * 4);
                }
                else
                {
                    ReadOnlySpan<byte> xyz = Read(vertices, offset * 4, 6);
                    position = new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz[2..]), BinaryPrimitives.ReadInt16LittleEndian(xyz[4..]));
                }
                vertex = NativeBattleModuleProjection.Project45FE10(matrix, position, projection);
                projected.Add(offset, vertex);
            }
            return vertex;
        }
    }

    public static NativeBattleGeometryPrimitive? EmitTriangle7049A0(ReadOnlySpan<byte> face, IReadOnlyList<NativeBattleModuleProjectedVertex> vertices, NativeBattleGfGeometryContext context, NativeBattleSummonColors? colors = null)
    {
        return EmitPolygon7043B0(face, vertices, 2, context, colors);
    }

    public static NativeBattleGeometryPrimitive? EmitPolygon7043B0(ReadOnlySpan<byte> face, IReadOnlyList<NativeBattleModuleProjectedVertex> vertices, int group, NativeBattleGfGeometryContext context, NativeBattleSummonColors? colors = null)
    {
        return EmitAttempt7043B0(face, vertices, group, context, colors).Primitive;
    }

    public static NativeBattleGfGeometryAttempt EmitAttempt7043B0(ReadOnlySpan<byte> face, IReadOnlyList<NativeBattleModuleProjectedVertex> vertices, int group, NativeBattleGfGeometryContext context, NativeBattleSummonColors? colors = null, int byteOffsetRelative = 0, Action<ushort>? observeDepth = null)
    {
        return EmitAttempt(face, vertices, group, Normalize(context), colors, byteOffsetRelative, observeDepth);
    }

    internal static NativeBattleGfGeometryAttempt EmitAttempt(ReadOnlySpan<byte> face, IReadOnlyList<NativeBattleModuleProjectedVertex> vertices, int group, NativeBattleMeshPacketContext context, NativeBattleSummonColors? colors, int byteOffsetRelative = 0, Action<ushort>? observeDepth = null)
    {
        bool quad = (group & 1) != 0;
        bool textured = (group & 2) != 0;
        bool gouraud = (group & 4) != 0;
        int count = quad ? 4 : 3;
        if ((uint)group >= GroupLengths.Length || face.Length != GroupLengths[group] || vertices.Count != count || context.DepthShift > 31)
        {
            throw new InvalidDataException("Native7043B0 requires the selected original group record, native projected vertices and depth shift.");
        }
        // NativeRTPT precedes the packet prefix stores; missing first-three sources cannot publish a prefix attempt.
        NativeBattleModuleProjectedVertex first = vertices[0];
        NativeBattleModuleProjectedVertex second = vertices[1];
        NativeBattleModuleProjectedVertex third = vertices[2];
        observeDepth?.Invoke(first.Depth);
        observeDepth?.Invoke(second.Depth);
        observeDepth?.Invoke(third.Depth);
        uint firstFlags = first.Flags | second.Flags | third.Flags;
        int length = gouraud ? 1 + count * (textured ? 3 : 2) : 2 + count * (textured ? 2 : 1);
        PacketWrites writes = new(length, byteOffsetRelative);
        writes.Word(0, (uint)(length - 1) << 24);
        uint flags = context.Flags;
        uint command = BinaryPrimitives.ReadUInt32LittleEndian(face);
        if ((flags & (gouraud ? 2u : 1u)) != 0)
        {
            command |= 0x2000000;
        }
        if ((flags & (gouraud ? 8u : 4u)) != 0)
        {
            command &= 0xFDFFFFFF;
        }
        writes.Word(1, command);
        uint[] uvs = new uint[textured ? count : 0];
        if (textured)
        {
            uint offset = context.PackedUvOffset ?? throw new NotSupportedException("Native textured mesh requires its preserved contextDWORD18 UV offset.");
            uvs[0] = unchecked(BinaryPrimitives.ReadUInt32LittleEndian(face[12..]) + offset);
            uvs[1] = unchecked(BinaryPrimitives.ReadUInt32LittleEndian(face[16..]) + offset);
            uvs[2] = quad ? unchecked(BinaryPrimitives.ReadUInt32LittleEndian(face[20..]) + offset + (offset << 16)) : unchecked(BinaryPrimitives.ReadUInt16LittleEndian(face[10..]) + offset);
            if (quad)
            {
                uvs[3] = uvs[2] >> 16;
            }
            for (int corner = 0; corner < count; corner++)
            {
                writes.Word(ScreenWord(corner) + 1, uvs[corner]);
            }
        }
        // Quad leaves gate the first RTPT flags only; following45FAA0 RTPS flags are never tested.
        if ((firstFlags & 0x60000) != 0)
        {
            return writes.Finish(null);
        }
        if (textured)
        {
            if ((flags & 0x400) != 0)
            {
                uvs[1] = Material(uvs[1], context.TexturePage, true, "TPAGE");
            }
            else if ((flags & 0x100) != 0)
            {
                uvs[1] = Material(uvs[1], context.TexturePage, false, "TPAGE");
            }
            if ((flags & 0x800) != 0)
            {
                uvs[0] = Material(uvs[0], context.Clut, true, "CLUT");
            }
            else if ((flags & 0x200) != 0)
            {
                uvs[0] = Material(uvs[0], context.Clut, false, "CLUT");
            }
            writes.Word(ScreenWord(0) + 1, uvs[0]);
            writes.Word(ScreenWord(1) + 1, uvs[1]);
        }
        int nclip = NativeBattleModulePackets.Nclip(vertices[0].Screen, vertices[1].Screen, vertices[2].Screen);
        if (nclip < 0 && (flags & (gouraud ? 0x20u : 0x10u)) == 0)
        {
            return writes.Finish(null);
        }
        for (int corner = 0; corner < 3; corner++)
        {
            writes.Word(ScreenWord(corner), vertices[corner].Screen);
        }
        if (quad)
        {
            // The fourth RTPS occurs only after first-three near and NCLIP admission.
            observeDepth?.Invoke(vertices[3].Depth);
            writes.Word(ScreenWord(3), vertices[3].Screen);
        }
        if (vertices.All(vertex => unchecked((ushort)vertex.Screen) > 0xA00) || vertices.All(vertex => (vertex.Screen >> 16) > 0x6C0))
        {
            return writes.Finish(null);
        }
        uint[] vertexColors = new uint[gouraud ? count : 1];
        vertexColors[0] = command;
        if (gouraud)
        {
            int colorOffset = textured ? (quad ? 24 : 20) : 12;
            for (int corner = 1; corner < count; corner++)
            {
                vertexColors[corner] = BinaryPrimitives.ReadUInt32LittleEndian(face[(colorOffset + (corner - 1) * 4)..]);
            }
        }
        if ((flags & (gouraud ? 0x80u : 0x40u)) != 0)
        {
            NativeBattleSummonColors rules = colors ?? throw new NotSupportedException("Native7043B0 depth cue requires its authenticated45F270/45F4C0 PC interpolation constants.");
            int factor = context.DepthCue ?? throw new NotSupportedException("Native mesh depth cue requires its actual signed contextDWORD0C.");
            uint farColor = context.FarColor ?? throw new NotSupportedException("Native mesh depth cue requires its actually written contextRGB8.");
            if (gouraud)
            {
                vertexColors = NativeBattleGteColorInterpolation.Gouraud45F4C0(vertexColors.ToImmutableArray(), quad, factor, farColor, rules).ToArray();
            }
            else
            {
                vertexColors[0] = rules.Interpolate(command, factor, farColor);
            }
        }
        // 56BAA0 establishes ZSF3=155h/ZSF4=100h;AVSZ performs unsigned SHR12 before context bias and signed clamp.
        uint average = unchecked((uint)vertices.Sum(vertex => (int)vertex.Depth) * (quad ? 0x100u : 0x155u)) >> 12;
        int biased = Math.Max(unchecked((int)average + context.DepthBias), 0);
        int bucket = checked(context.TableBaseBucket + (biased >> context.DepthShift));
        for (int corner = 0; corner < vertexColors.Length; corner++)
        {
            writes.Word(gouraud ? ScreenWord(corner) - 1 : 1, vertexColors[corner]);
        }
        ImmutableArray<int> payload = quad ? [vertices[0].Depth, vertices[1].Depth, vertices[2].Depth, vertices[3].Depth] : [vertices[0].Depth, vertices[1].Depth, vertices[2].Depth, 0];
        return writes.Finish(new(bucket, writes.Words(), payload));

        int ScreenWord(int corner) => gouraud ? 2 + corner * (textured ? 3 : 2) : 2 + corner * (textured ? 2 : 1);
    }

    private static NativeBattleMeshPacketContext Normalize(NativeBattleGfGeometryContext context) => new(context.Flags, context.FarColor, context.DepthCue, context.TexturePage, context.Clut, 0, context.DepthBias, context.TableBaseBucket, context.DepthShift);

    private static uint Material(uint original, ushort? contextWord, bool add, string name)
    {
        ushort word = contextWord ?? throw new NotSupportedException($"Native7043B0 textured leaf requires its actual context {name} WORD for the selected material flag.");
        ushort result = add ? unchecked((ushort)((original >> 16) + word)) : word;
        return (original & 0xFFFF) | (uint)result << 16;
    }

    private static int Int32(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(Read(bytes, offset, 4));

    private static ReadOnlySpan<byte> Read(ImmutableArray<byte> bytes, int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > bytes.Length - length)
        {
            throw new InvalidDataException("Native7043B0 access exceeds its current original geometry/vertex bytes.");
        }
        return bytes.AsSpan().Slice(offset, length);
    }

    private sealed class ProjectedCorners(ushort[] offsets, Func<ushort, NativeBattleModuleProjectedVertex> project) : IReadOnlyList<NativeBattleModuleProjectedVertex>
    {
        public int Count => offsets.Length;
        public NativeBattleModuleProjectedVertex this[int index] => project(offsets[index]);

        public IEnumerator<NativeBattleModuleProjectedVertex> GetEnumerator()
        {
            foreach (ushort offset in offsets)
            {
                yield return project(offset);
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class PacketWrites(int wordCount, int byteOffsetRelative)
    {
        // Zero backing values for Untouched mask entries are not writes and must never replace the retained arena bytes.
        private readonly byte[] _bytes = new byte[wordCount * 4];
        private readonly NativeBattleGfPacketByteState[] _mask = new NativeBattleGfPacketByteState[wordCount * 4];

        public void Word(int index, uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(_bytes.AsSpan(index * 4), value);
            _mask.AsSpan(index * 4, 4).Fill(NativeBattleGfPacketByteState.Known);
        }

        public ImmutableArray<uint> Words()
        {
            ImmutableArray<uint>.Builder words = ImmutableArray.CreateBuilder<uint>(wordCount);
            for (int index = 0; index < wordCount; index++)
            {
                words.Add(BinaryPrimitives.ReadUInt32LittleEndian(_bytes.AsSpan(index * 4)));
            }
            return words.ToImmutable();
        }

        // TAG links are still the explicit native initial zeros. Only the caller's successful45C870 insertion rewrites them.
        public NativeBattleGfGeometryAttempt Finish(NativeBattleGeometryPrimitive? primitive) => new(byteOffsetRelative, _bytes.ToImmutableArray(), _mask.ToImmutableArray(), primitive);
    }
}
