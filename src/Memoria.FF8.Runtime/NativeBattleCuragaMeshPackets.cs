using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCuragaMeshContext(uint Flags, uint? FarColor, int? Fade, ushort? TexturePage, ushort? Clut, uint? UvOffset, int TableBaseBucket, byte DepthShift);

/// <summary>87EEB0's geometry dispatcher and the reached87F6C0/8800B0 textured quad stores and gates.</summary>
public static partial class NativeBattleCuragaMeshPackets
{
    private static readonly string[] Leaves = ["87EFF0", "87F200", "87F480", "87F6C0", "87F960", "87FB90", "87FE50", "8800B0"];

    public static NativeBattleGfGeometryBatch EmitBatch87EEB0(ImmutableArray<byte> mesh, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, NativeBattleCuragaMeshContext context, Func<int, NativeBattleVector>? readSelectedVertex = null, NativeBattleSummonColors? colors = null)
    {
        if (mesh.IsDefault || context.DepthShift > 31)
        {
            throw new InvalidDataException("Native87EEB0 requires actual current mesh bytes and its depth shift.");
        }
        matrix.Validate();
        if ((context.Flags & 0x2000) != 0 && readSelectedVertex is null)
        {
            throw new NotSupportedException("Native87EEB0 preserved vertex-pointer flag requires its current selected XYZ producer.");
        }
        int cursor = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(Read(mesh, 0, 4)));
        int allocated = 0;
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        for (int group = 0; group < 8; group++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(Read(mesh, cursor, 4));
            cursor = checked(cursor + 4);
            if (count == 0)
            {
                continue;
            }
            if (group is not (3 or 4 or 5 or 7))
            {
                throw new NotSupportedException($"Native87EEB0 reached original{Leaves[group]} group{group}; its exact packet leaf is not implemented.");
            }
            for (int index = 0; index < count; index++)
            {
                int stride = group switch { 4 => 20, 7 => 36, _ => 24 };
                ReadOnlySpan<byte> face = Read(mesh, cursor, stride);
                ushort[] indices = new ushort[group == 4 ? 3 : 4];
                for (int corner = 0; corner < indices.Length; corner++)
                {
                    indices[corner] = BinaryPrimitives.ReadUInt16LittleEndian(face[(4 + corner * 2)..]);
                }
                NativeBattleModuleProjectedVertex Project(int corner)
                {
                    int offset = indices[corner] * 4;
                    NativeBattleVector vertex;
                    if ((context.Flags & 0x2000) != 0)
                    {
                        vertex = readSelectedVertex!(offset);
                    }
                    else
                    {
                        ReadOnlySpan<byte> xyz = Read(mesh, checked(8 + offset), 6);
                        vertex = new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz[2..]), BinaryPrimitives.ReadInt16LittleEndian(xyz[4..]));
                    }
                    return NativeBattleModuleProjection.Project45FE10(matrix, vertex, projection);
                }
                NativeBattleGfGeometryBatch emitted = group is 4 or 5 ? EmitGouraud(face, Project, group == 5, context, colors, allocated) : EmitQuad(face, Project, group == 7, context, colors, allocated);
                attempts.AddRange(emitted.Attempts);
                allocated = checked(allocated + emitted.AllocationBytes);
                cursor = checked(cursor + stride);
            }
        }
        return new(attempts.ToImmutable(), allocated);
    }

    public static NativeBattleGfGeometryBatch EmitFt4_87F6C0(ReadOnlySpan<byte> face, Func<int, NativeBattleModuleProjectedVertex> readCorner, NativeBattleCuragaMeshContext context, NativeBattleSummonColors? colors = null, int byteOffset = 0)
    {
        return EmitQuad(face, readCorner, false, context, colors, byteOffset);
    }

    public static NativeBattleGfGeometryBatch EmitGt4_8800B0(ReadOnlySpan<byte> face, Func<int, NativeBattleModuleProjectedVertex> readCorner, NativeBattleCuragaMeshContext context, NativeBattleSummonColors? colors = null, int byteOffset = 0)
    {
        return EmitQuad(face, readCorner, true, context, colors, byteOffset);
    }

    private static NativeBattleGfGeometryBatch EmitQuad(ReadOnlySpan<byte> face, Func<int, NativeBattleModuleProjectedVertex> readCorner, bool gouraud, NativeBattleCuragaMeshContext context, NativeBattleSummonColors? colors, int byteOffset)
    {
        if (face.Length != (gouraud ? 36 : 24) || context.DepthShift > 31)
        {
            throw new InvalidDataException("Native87F6C0/8800B0 requires its original FT4/GT4 source face and depth shift.");
        }
        NativeBattleModuleProjectedVertex[] vertices = [readCorner(0), readCorner(1), readCorner(2), default];
        uint color = BinaryPrimitives.ReadUInt32LittleEndian(face);
        if ((context.Flags & (gouraud ? 2u : 1u)) != 0)
        {
            color |= 0x02000000;
        }
        if ((context.Flags & (gouraud ? 8u : 4u)) != 0)
        {
            color &= 0xFDFFFFFF;
        }
        uint offset = (context.Flags & 0x1000) != 0 ? context.UvOffset ?? throw new NotSupportedException("Native87EEB0 requires its preserved UV-offset DWORD18.") : 0;
        uint firstUv = unchecked(BinaryPrimitives.ReadUInt32LittleEndian(face[12..]) + offset);
        uint secondUv = unchecked(BinaryPrimitives.ReadUInt32LittleEndian(face[16..]) + offset);
        uint finalUvs = unchecked(BinaryPrimitives.ReadUInt32LittleEndian(face[20..]) + offset + (offset << 16));
        int packetLength = gouraud ? 52 : 40;
        int screenStride = gouraud ? 12 : 8;
        int secondUvOffset = gouraud ? 24 : 20;
        SkinWrites packet = new(packetLength, byteOffset);
        packet.Word(0, (uint)(packetLength / 4 - 1) << 24);
        packet.Word(4, color);
        packet.Word(12, firstUv);
        packet.Word(secondUvOffset, secondUv);
        packet.Word(gouraud ? 36 : 28, finalUvs);
        packet.Word(gouraud ? 48 : 36, finalUvs >> 16);
        if (((vertices[0].Flags | vertices[1].Flags | vertices[2].Flags) & 0x60000) != 0)
        {
            return Rejected();
        }
        if ((context.Flags & 0x400) != 0)
        {
            secondUv = (secondUv & 0xFFFF) | (uint)unchecked((ushort)((secondUv >> 16) + (context.TexturePage ?? throw new NotSupportedException("Native87F6C0 requires its retained TPAGE-add WORD10.")))) << 16;
            packet.Word16(secondUvOffset + 2, (ushort)(secondUv >> 16));
        }
        else if ((context.Flags & 0x100) != 0)
        {
            packet.Word16(secondUvOffset + 2, context.TexturePage ?? throw new NotSupportedException("Native87F6C0 requires its retained TPAGE WORD10."));
        }
        if ((context.Flags & 0x800) != 0)
        {
            packet.Word16(14, unchecked((ushort)((firstUv >> 16) + (context.Clut ?? throw new NotSupportedException("Native87F6C0 requires its retained CLUT-add WORD14.")))));
        }
        else if ((context.Flags & 0x200) != 0)
        {
            packet.Word16(14, context.Clut ?? throw new NotSupportedException("Native87F6C0 requires its retained CLUT WORD14."));
        }
        if (NativeBattleModulePackets.Nclip(vertices[0].Screen, vertices[1].Screen, vertices[2].Screen) < 0 && (context.Flags & (gouraud ? 0x20u : 0x10u)) == 0)
        {
            return Rejected();
        }
        for (int corner = 0; corner < 3; corner++)
        {
            packet.Word(8 + corner * screenStride, vertices[corner].Screen);
        }
        vertices[3] = readCorner(3);
        packet.Word(8 + 3 * screenStride, vertices[3].Screen);
        if (vertices.All(vertex => Outside(unchecked((short)vertex.Screen), 2560)) || vertices.All(vertex => Outside(unchecked((short)(vertex.Screen >> 16)), 1728)))
        {
            return Rejected();
        }
        int average = vertices.Sum(vertex => (int)vertex.Depth) >> 4;
        int bucket = checked(context.TableBaseBucket + (average >> context.DepthShift));
        bool fade = (context.Flags & (gouraud ? 0x80u : 0x40u)) != 0;
        if (gouraud)
        {
            ImmutableArray<uint> vertexColors = [color, BinaryPrimitives.ReadUInt32LittleEndian(face[24..]), BinaryPrimitives.ReadUInt32LittleEndian(face[28..]), BinaryPrimitives.ReadUInt32LittleEndian(face[32..])];
            if (fade)
            {
                vertexColors = NativeBattleGteColorInterpolation.Gouraud45F4C0(vertexColors, true, RequiredFade(), RequiredFar(), RequiredConstants());
            }
            for (int corner = 0; corner < 4; corner++)
            {
                packet.Word(4 + corner * 12, vertexColors[corner]);
            }
            color = vertexColors[0];
        }
        else if (fade)
        {
            color = RequiredConstants().Interpolate(color, RequiredFade(), RequiredFar());
            packet.Word(4, color);
        }
        uint opcode = color >> 24 & 0xFC;
        ImmutableArray<int> payload = opcode is < 0x20 or > 0x3C ? [0, 0, 0, 0] : (opcode & 8) != 0 ? [vertices[0].Depth, vertices[1].Depth, vertices[2].Depth, vertices[3].Depth] : [vertices[1].Depth, vertices[2].Depth, vertices[3].Depth, 0];
        return new([packet.Attempt(new(bucket, packet.Words(), payload))], packetLength);

        int RequiredFade()
        {
            return context.Fade ?? throw new NotSupportedException("Native Curaga mesh fade requires signed contextDWORD0C.");
        }

        uint RequiredFar()
        {
            return context.FarColor ?? throw new NotSupportedException("Native87EEB0 fade requires the actually written contextRGB8.");
        }

        NativeBattleSummonColors RequiredConstants()
        {
            return colors ?? throw new NotSupportedException("Native Curaga mesh fade requires authenticated45F270/45F4C0 constants.");
        }

        NativeBattleGfGeometryBatch Rejected()
        {
            return new([packet.Attempt(null)], 0);
        }
    }

    private static bool Outside(short value, int end)
    {
        return value < 0 || value > end;
    }

    private static ReadOnlySpan<byte> Read(ImmutableArray<byte> source, int offset, int length)
    {
        if (offset < 0 || offset > source.Length - length)
        {
            throw new InvalidDataException("Native87EEB0 source access exceeds its current mesh bank.");
        }
        return source.AsSpan().Slice(offset, length);
    }
}
