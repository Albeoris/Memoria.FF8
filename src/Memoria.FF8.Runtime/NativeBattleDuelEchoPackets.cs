using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public readonly record struct NativeBattleDuelEchoProjectedVertex(uint Screen, ushort Depth, ushort Clip)
{
    public byte KnownBytesMask { get; init; } = byte.MaxValue;
}
public sealed record NativeBattleDuelEchoPacketsResult(NativeBattleModulePoolTask Context, NativeBattleGfGeometryBatch Batch);

/// <summary>68AAE0's original flat-lit textured echo faces over a retained projected vertex stream.</summary>
public static class NativeBattleDuelEchoPackets
{
    public static NativeBattleDuelEchoPacketsResult Emit68AAE0(NativeBattleModulePoolTask context, int offset, ImmutableArray<byte> faces, Func<int, NativeBattleDuelEchoProjectedVertex> readVertex, int tableBaseBucket, byte depthShift, short zsf3, short zsf4, Action<NativeBattleModulePoolTask, int, int>? observeContext = null, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        if (faces.IsDefault || depthShift > 31)
        {
            throw new InvalidDataException("Native Duel echo requires its actual face owner and depth shift.");
        }
        ushort triangles = context.ReadWord(offset + 8);
        ushort quads = context.ReadWord(offset + 10);
        if (faces.Length != checked(triangles * 16 + quads * 20))
        {
            throw new InvalidDataException("Native Duel echo faces differ from their original context counts.");
        }
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        int source = 0;
        Span<byte> projectedBytes = stackalloc byte[8];
        for (int group = 0; group < 2; group++)
        {
            int countOffset = offset + 8 + group * 2;
            ushort count = context.ReadWord(countOffset);
            context = context.WriteWord(countOffset, unchecked((ushort)(count - 1)));
            observeContext?.Invoke(context, countOffset, 2);
            for (int faceIndex = 0; faceIndex < count; faceIndex++)
            {
                bool quad = group != 0;
                ReadOnlySpan<byte> face = faces.AsSpan().Slice(source, quad ? 20 : 16);
                NativeBattleDuelEchoProjectedVertex[] vertices = new NativeBattleDuelEchoProjectedVertex[quad ? 4 : 3];
                bool clipped = false;
                ushort clipAnd = ushort.MaxValue;
                for (int corner = 0; corner < vertices.Length; corner++)
                {
                    int vertex = BinaryPrimitives.ReadUInt16LittleEndian(face.Slice(corner * 2)) & 0xFFF;
                    NativeBattleDuelEchoProjectedVertex selected = readVertex(vertex);
                    vertices[corner] = selected;
                    int target = offset + 28 + corner * 8;
                    BinaryPrimitives.WriteUInt32LittleEndian(projectedBytes, selected.Screen);
                    BinaryPrimitives.WriteUInt16LittleEndian(projectedBytes.Slice(4), selected.Depth);
                    BinaryPrimitives.WriteUInt16LittleEndian(projectedBytes.Slice(6), selected.Clip);
                    context = context.WriteBytes(target, projectedBytes);
                    for (int current = 0; current < 8; current++)
                    {
                        if ((selected.KnownBytesMask & (1 << current)) == 0)
                        {
                            context = context.MarkPayloadUnknown(target + current, 1);
                        }
                    }
                    observeContext?.Invoke(context, target, 8);
                    if ((selected.KnownBytesMask & 0xC0) != 0xC0)
                    {
                        throw new NotSupportedException("Native Duel echo clip comparison requires its actual projected clip WORD.");
                    }
                    if (selected.Clip >= 16)
                    {
                        clipped = true;
                        break;
                    }
                    clipAnd &= selected.Clip;
                }
                if (!clipped && (clipAnd & 0xFF) == 0)
                {
                    if (vertices.Any(vertex => vertex.KnownBytesMask != byte.MaxValue))
                    {
                        throw new NotSupportedException("Native Duel echo area and depth require the actual projected screen and depth bytes.");
                    }
                    int area = NativeBattleModulePackets.Nclip(vertices[0].Screen, vertices[1].Screen, vertices[2].Screen);
                    context = context.WriteDword(offset + 60, unchecked((uint)area));
                    observeContext?.Invoke(context, offset + 60, 4);
                    if (area >= 0)
                    {
                        uint depth = unchecked((uint)vertices.Sum(vertex => (int)vertex.Depth) * unchecked((uint)(quad ? zsf4 : zsf3))) >> 12;
                        context = context.WriteDword(offset + 68, depth);
                        observeContext?.Invoke(context, offset + 68, 4);
                        int length = quad ? 40 : 32;
                        SkinWrites packet = new(length, allocated);
                        uint color = context.ReadDword(offset + 20) | (quad ? 0x2E000000u : 0x26000000u);
                        packet.Word(0, (uint)(length / 4 - 1) << 24);
                        packet.Word(4, color);
                        for (int corner = 0; corner < vertices.Length; corner++)
                        {
                            packet.Word(8 + corner * 8, vertices[corner].Screen);
                        }
                        packet.Word(12, BinaryPrimitives.ReadUInt32LittleEndian(face.Slice(8)));
                        uint material = BinaryPrimitives.ReadUInt32LittleEndian(face.Slice(12));
                        packet.Word(20, material & ~0x02000000u);
                        packet.Word16(28, BinaryPrimitives.ReadUInt16LittleEndian(face.Slice(quad ? 16 : 6)));
                        if (quad)
                        {
                            packet.Word16(36, BinaryPrimitives.ReadUInt16LittleEndian(face.Slice(18)));
                        }
                        int bucket = checked(tableBaseBucket + (int)(depth >> depthShift));
                        NativeBattleModuleProjectedVertex[] projected = vertices.Select(vertex => new NativeBattleModuleProjectedVertex(vertex.Screen, vertex.Depth, 0)).ToArray();
                        NativeBattleGeometryPrimitive primitive = new(bucket, packet.Words(), NativeBattleEffectMeshPackets.Payload(projected, color));
                        if (pilotIdentity is { } identity)
                        {
                            System.Numerics.Vector3 Point(int corner) => new(unchecked((short)vertices[corner].Screen), unchecked((short)(vertices[corner].Screen >> 16)), 0);
                            System.Numerics.Vector4 depths = primitive.VertexPayload4.Length == 4 ? new(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]) : System.Numerics.Vector4.Zero;
                            primitive = primitive with { PilotGeometry = new(identity with { Primitive = unchecked(identity.Primitive * 397 + source) }, Point(0), Point(1), Point(2), quad ? Point(3) : default, (byte)vertices.Length, false) { ScreenSpace = true, SourceDepths = depths, UseSourceDepths = true } };
                        }
                        attempts.Add(packet.Attempt(primitive));
                        allocated = checked(allocated + length);
                    }
                }
                source += group == 0 ? 16 : 20;
                if (faceIndex + 1 < count)
                {
                    context = context.WriteWord(countOffset, unchecked((ushort)(context.ReadWord(countOffset) - 1)));
                    observeContext?.Invoke(context, countOffset, 2);
                }
            }
            if (count != 0)
            {
                context = context.WriteWord(countOffset, ushort.MaxValue);
                observeContext?.Invoke(context, countOffset, 2);
            }
        }
        return new(context, new(attempts.ToImmutable(), allocated));
    }
}
