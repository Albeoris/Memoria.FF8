using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleTextureWindow(short X, short Y, short Width, short Height);
public sealed record NativeBattleQuezacotlGeometryContext(uint FarColor, int DepthCue, ushort VerticalOffset, NativeBattleTextureWindow WindowBefore, NativeBattleTextureWindow WindowAfter, int TableBaseBucket, byte DepthShift)
{
    public ushort? PriorDepthFifo { get; init; }
}

/// <summary>6C5710/6C5750/6C5B50's original captured-geometry GT3/GT4 and ordered texture-window packets.</summary>
public static class NativeBattleQuezacotlGeometryPackets
{
    public static NativeBattleGfGeometryBatch EmitBatch6C5710(ImmutableArray<byte> source, int geometryOffset, ImmutableArray<byte> selectedVertices, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, NativeBattleQuezacotlGeometryContext context, NativeBattleSummonColors? colors = null, Func<int, NativeBattleVector>? readSelectedVertex = null)
    {
        if (source.IsDefault || selectedVertices.IsDefault && readSelectedVertex is null || context.DepthShift > 31)
        {
            throw new InvalidDataException("Native6C5710 requires its current geometry, selected vertex bytes and actual depth shift.");
        }
        matrix.Validate();
        int cursor = checked(geometryOffset + BinaryPrimitives.ReadInt32LittleEndian(Read(source, geometryOffset, 4)) + 0x18);
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        Dictionary<ushort, NativeBattleModuleProjectedVertex> projected = [];
        for (int group = 0; group < 2; group++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(Read(source, cursor, 4));
            cursor = checked(cursor + 4);
            int recordLength = group == 0 ? 28 : 36;
            for (int index = 0; index < count; index++)
            {
                ReadOnlySpan<byte> face = Read(source, cursor, recordLength);
                ushort[] offsets = new ushort[group == 0 ? 3 : 4];
                for (int corner = 0; corner < offsets.Length; corner++)
                {
                    offsets[corner] = BinaryPrimitives.ReadUInt16LittleEndian(face[(4 + corner * 2)..]);
                }
                NativeBattleGfGeometryBatch batch = EmitPolygon(face, new ProjectedCorners(offsets, Project), group != 0, context, colors, allocated);
                attempts.AddRange(batch.Attempts);
                allocated = checked(allocated + batch.AllocationBytes);
                cursor = checked(cursor + recordLength);
            }
        }
        return new(attempts.ToImmutable(), allocated);

        NativeBattleModuleProjectedVertex Project(ushort offset)
        {
            if (!projected.TryGetValue(offset, out NativeBattleModuleProjectedVertex vertex))
            {
                NativeBattleVector position;
                if (readSelectedVertex is not null)
                {
                    position = readSelectedVertex(offset * 4);
                }
                else
                {
                    ReadOnlySpan<byte> xyz = Read(selectedVertices, offset * 4, 6);
                    position = new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz[2..]), BinaryPrimitives.ReadInt16LittleEndian(xyz[4..]));
                }
                vertex = NativeBattleModuleProjection.Project45FE10(matrix, position, projection);
                projected.Add(offset, vertex);
            }
            return vertex;
        }
    }

    public static NativeBattleGfGeometryBatch EmitTriangle6C5750(ReadOnlySpan<byte> face, IReadOnlyList<NativeBattleModuleProjectedVertex> vertices, NativeBattleQuezacotlGeometryContext context, NativeBattleSummonColors? colors = null, int byteOffsetRelative = 0)
    {
        return EmitPolygon(face, vertices, false, context, colors, byteOffsetRelative);
    }

    public static NativeBattleGfGeometryBatch EmitQuad6C5B50(ReadOnlySpan<byte> face, IReadOnlyList<NativeBattleModuleProjectedVertex> vertices, NativeBattleQuezacotlGeometryContext context, NativeBattleSummonColors? colors = null, int byteOffsetRelative = 0)
    {
        return EmitPolygon(face, vertices, true, context, colors, byteOffsetRelative);
    }

    public static uint TextureWindowCommand(NativeBattleTextureWindow window)
    {
        return 0xE2000000u | (uint)((window.Y & 0xF8) << 12) | (uint)((window.X & 0xF8) << 7) | (uint)((~(window.Height - 1) & 0xF8) << 2) | (uint)((~(window.Width - 1) >> 3) & 31);
    }

    private static NativeBattleGfGeometryBatch EmitPolygon(ReadOnlySpan<byte> face, IReadOnlyList<NativeBattleModuleProjectedVertex> vertices, bool quad, NativeBattleQuezacotlGeometryContext context, NativeBattleSummonColors? colors, int byteOffsetRelative)
    {
        int count = quad ? 4 : 3;
        int length = quad ? 52 : 40;
        if (face.Length != (quad ? 36 : 28) || vertices.Count != count || context.DepthShift > 31)
        {
            throw new InvalidDataException("Native6C5750/6C5B50 requires its original GT3/GT4 record and projected vertex inputs.");
        }
        //RTPT executes before these stores; fourth RTPS is conditional on only the first three near flags.
        uint firstFlags = vertices[0].Flags | vertices[1].Flags | vertices[2].Flags;
        Writes packet = new(length, byteOffsetRelative);
        packet.Word(0, (uint)(length / 4 - 1) << 24);
        packet.Word(4, BinaryPrimitives.ReadUInt32LittleEndian(face));
        packet.Word(12, BinaryPrimitives.ReadUInt32LittleEndian(face[12..]));
        packet.Word(24, BinaryPrimitives.ReadUInt32LittleEndian(face[16..]));
        packet.Word(36, quad ? BinaryPrimitives.ReadUInt32LittleEndian(face[20..]) : BinaryPrimitives.ReadUInt32LittleEndian(face[8..]) >> 16);
        if (quad)
        {
            packet.Word(48, BinaryPrimitives.ReadUInt32LittleEndian(face[20..]) >> 16);
        }
        if ((firstFlags & 0x60000) != 0)
        {
            return new([packet.Attempt(null)], 0);
        }
        for (int corner = 0; corner < count; corner++)
        {
            packet.Word(8 + corner * 12, vertices[corner].Screen);
        }
        if (vertices.All(vertex => unchecked((ushort)vertex.Screen) > 0xA00) || vertices.All(vertex => (vertex.Screen >> 16) > 0x6C0))
        {
            return new([packet.Attempt(null)], 0);
        }
        uint[] vertexColors = new uint[count];
        vertexColors[0] = BinaryPrimitives.ReadUInt32LittleEndian(face);
        for (int corner = 1; corner < count; corner++)
        {
            vertexColors[corner] = BinaryPrimitives.ReadUInt32LittleEndian(face[((quad ? 24 : 20) + (corner - 1) * 4)..]);
        }
        if (context.DepthCue != 0)
        {
            NativeBattleSummonColors rules = colors ?? throw new NotSupportedException("Native6C5750/6C5B50 depth cue requires its authenticated PC interpolation constants.");
            uint fifoCommand = vertexColors[quad ? 3 : 0] & 0xFF000000;
            for (int corner = 1; corner < count; corner++)
            {
                vertexColors[corner] = (rules.Interpolate(vertexColors[corner], context.DepthCue, context.FarColor) & 0xFFFFFF) | fifoCommand;
            }
            vertexColors[0] = rules.Interpolate(vertexColors[0], context.DepthCue, context.FarColor);
        }
        for (int corner = 0; corner < count; corner++)
        {
            packet.Word(4 + corner * 12, vertexColors[corner]);
        }
        int[] vertical = Enumerable.Range(0, count).Select(corner => packet.Byte(13 + corner * 12) + context.VerticalOffset).ToArray();
        bool wrap = vertical.Aggregate(0, (combined, value) => combined | value) > 255;
        for (int corner = 0; corner < count; corner++)
        {
            packet.Byte(13 + corner * 12, unchecked((byte)(vertical[corner] - (wrap ? (byte)context.WindowAfter.Height : 0))));
        }
        uint average = unchecked((uint)vertices.Sum(vertex => (int)vertex.Depth) * (quad ? 0x100u : 0x155u)) >> 12;
        int bucket = checked(context.TableBaseBucket + ((int)average >> context.DepthShift));
        //45C7A0 clearsFlags20 and captures the current depth FIFO according to the actual packet opcode.
        uint opcode = vertexColors[0] >> 24 & 0xFC;
        ImmutableArray<int> payload = [0, 0, 0, 0];
        if (opcode is >= 0x20 and <= 0x3C)
        {
            if ((opcode & 8) != 0)
            {
                payload = quad ? [vertices[0].Depth, vertices[1].Depth, vertices[2].Depth, vertices[3].Depth] : [context.PriorDepthFifo ?? throw new NotSupportedException("Native45C7A0 quad opcode after only RTPT requires its retained prior depth FIFO WORD."), vertices[0].Depth, vertices[1].Depth, vertices[2].Depth];
            }
            else
            {
                payload = quad ? [vertices[1].Depth, vertices[2].Depth, vertices[3].Depth, 0] : [vertices[0].Depth, vertices[1].Depth, vertices[2].Depth, 0];
            }
        }
        NativeBattleGeometryPrimitive polygon = new(bucket, packet.Words(), payload);
        Writes firstWindow = Window(length, context.WindowBefore);
        Writes lastWindow = Window(length + 12, context.WindowAfter);
        Writes linkOnly = new(length, byteOffsetRelative);
        return new([packet.Attempt(null), firstWindow.Attempt(new(bucket, firstWindow.Words(), [0, 0, 0, 0])), linkOnly.Attempt(polygon), lastWindow.Attempt(new(bucket, lastWindow.Words(), [0, 0, 0, 0]))], length + 24);

        Writes Window(int relativeOffset, NativeBattleTextureWindow window)
        {
            Writes result = new(12, checked(byteOffsetRelative + relativeOffset));
            result.Word(0, 0x02000000);
            result.Word(4, TextureWindowCommand(window));
            result.Word(8, 0);
            return result;
        }
    }

    private static ReadOnlySpan<byte> Read(ImmutableArray<byte> source, int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > source.Length - length)
        {
            throw new InvalidDataException("Native6C5710 access exceeds its actual geometry or selected vertex bytes.");
        }
        return source.AsSpan().Slice(offset, length);
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

    private sealed class Writes(int length, int offset)
    {
        private readonly byte[] _data = new byte[length];
        private readonly NativeBattleGfPacketByteState[] _mask = new NativeBattleGfPacketByteState[length];

        public void Word(int byteOffset, uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(_data.AsSpan(byteOffset), value);
            _mask.AsSpan(byteOffset, 4).Fill(NativeBattleGfPacketByteState.Known);
        }

        public byte Byte(int byteOffset) => _data[byteOffset];

        public void Byte(int byteOffset, byte value)
        {
            _data[byteOffset] = value;
            _mask[byteOffset] = NativeBattleGfPacketByteState.Known;
        }

        public ImmutableArray<uint> Words()
        {
            ImmutableArray<uint>.Builder words = ImmutableArray.CreateBuilder<uint>(length / 4);
            for (int index = 0; index < length; index += 4)
            {
                words.Add(BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan(index)));
            }
            return words.ToImmutable();
        }

        public NativeBattleGfGeometryAttempt Attempt(NativeBattleGeometryPrimitive? primitive) => new(offset, _data.ToImmutableArray(), _mask.ToImmutableArray(), primitive);
    }
}
