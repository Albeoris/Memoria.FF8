using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal sealed record NativeBattleEffectMeshWindow(ushort U, ushort V, NativeBattleTextureWindow Before, NativeBattleTextureWindow After);
internal sealed record NativeBattleEffectMeshPacketContext(uint Flags, uint? FarColor, int? Fade, ushort? TexturePage, ushort? Clut, uint PackedUvOffset, bool MaterialOverrides, int? DepthBias, int TableBaseBucket, byte DepthShift, NativeBattleEffectMeshWindow? Window = null);

/// <summary>Shared normalized flat, textured and Gouraud mesh stores used by Curaga, Esuna and Full-life.</summary>
internal static class NativeBattleEffectMeshPackets
{
    internal static NativeBattleGfGeometryBatch EmitTextured(ReadOnlySpan<byte> face, Func<int, NativeBattleModuleProjectedVertex> readCorner, bool quad, bool gouraud, NativeBattleEffectMeshPacketContext context, NativeBattleSummonColors? colors, int byteOffset)
    {
        if (face.Length != (gouraud ? 36 : quad ? 24 : 20) || gouraud && !quad || context.DepthShift > 31)
        {
            throw new InvalidDataException("Native textured mesh leaf requires its original face extent and depth shift.");
        }
        NativeBattleModuleProjectedVertex[] vertices = new NativeBattleModuleProjectedVertex[quad ? 4 : 3];
        for (int corner = 0; corner < 3; corner++)
        {
            vertices[corner] = readCorner(corner);
        }
        uint color = Command(BinaryPrimitives.ReadUInt32LittleEndian(face), context.Flags, gouraud);
        uint offset = context.PackedUvOffset;
        uint firstUv = unchecked(BinaryPrimitives.ReadUInt32LittleEndian(face[12..]) + offset);
        uint secondUv = unchecked(BinaryPrimitives.ReadUInt32LittleEndian(face[16..]) + offset);
        uint finalUvs = quad ? unchecked(BinaryPrimitives.ReadUInt32LittleEndian(face[20..]) + offset + (offset << 16)) : unchecked((uint)BinaryPrimitives.ReadUInt16LittleEndian(face[10..]) + offset);
        int length = gouraud ? 52 : quad ? 40 : 32;
        int screenStride = gouraud ? 12 : 8;
        int secondUvOffset = gouraud ? 24 : 20;
        int[] uvOffsets = quad ? [12, secondUvOffset, gouraud ? 36 : 28, gouraud ? 48 : 36] : [12, 20, 28];
        uint[] uvWords = quad ? [firstUv, secondUv, finalUvs, finalUvs >> 16] : [firstUv, secondUv, finalUvs];
        SkinWrites packet = new(length, byteOffset);
        packet.Word(0, (uint)(length / 4 - 1) << 24);
        packet.Word(4, color);
        for (int corner = 0; corner < uvOffsets.Length; corner++)
        {
            packet.Word(uvOffsets[corner], uvWords[corner]);
        }
        if (((vertices[0].Flags | vertices[1].Flags | vertices[2].Flags) & 0x60000) != 0)
        {
            return Rejected();
        }
        if (context.MaterialOverrides)
        {
            if ((context.Flags & 0x400) != 0)
            {
                uvWords[1] = (uvWords[1] & 0xFFFF) | (uint)unchecked((ushort)((uvWords[1] >> 16) + (context.TexturePage ?? throw new NotSupportedException("Native material-add requires its retained TPAGE WORD.")))) << 16;
                packet.Word16(secondUvOffset + 2, (ushort)(uvWords[1] >> 16));
            }
            else if ((context.Flags & 0x100) != 0)
            {
                packet.Word16(secondUvOffset + 2, context.TexturePage ?? throw new NotSupportedException("Native material replacement requires its retained TPAGE WORD."));
            }
            if ((context.Flags & 0x800) != 0)
            {
                packet.Word16(14, unchecked((ushort)((firstUv >> 16) + (context.Clut ?? throw new NotSupportedException("Native material-add requires its retained CLUT WORD.")))));
            }
            else if ((context.Flags & 0x200) != 0)
            {
                packet.Word16(14, context.Clut ?? throw new NotSupportedException("Native material replacement requires its retained CLUT WORD."));
            }
        }
        if (NativeBattleModulePackets.Nclip(vertices[0].Screen, vertices[1].Screen, vertices[2].Screen) < 0 && (context.Flags & (gouraud ? 0x20u : 0x10u)) == 0)
        {
            return Rejected();
        }
        for (int corner = 0; corner < 3; corner++)
        {
            packet.Word(8 + corner * screenStride, vertices[corner].Screen);
        }
        if (quad)
        {
            vertices[3] = readCorner(3);
            packet.Word(8 + 3 * screenStride, vertices[3].Screen);
        }
        if (OutsideScreen(vertices))
        {
            return Rejected();
        }
        if (gouraud)
        {
            ImmutableArray<uint> vertexColors = [color, BinaryPrimitives.ReadUInt32LittleEndian(face[24..]), BinaryPrimitives.ReadUInt32LittleEndian(face[28..]), BinaryPrimitives.ReadUInt32LittleEndian(face[32..])];
            if ((context.Flags & 0x80) != 0)
            {
                vertexColors = NativeBattleGteColorInterpolation.Gouraud45F4C0(vertexColors, true, RequiredFade(context), RequiredFar(context), RequiredConstants(colors));
            }
            for (int corner = 0; corner < 4; corner++)
            {
                packet.Word(4 + corner * 12, vertexColors[corner]);
            }
            color = vertexColors[0];
        }
        else if ((context.Flags & 0x40) != 0)
        {
            color = RequiredConstants(colors).Interpolate(color, RequiredFade(context), RequiredFar(context));
            packet.Word(4, color);
        }
        int average = Average(vertices);
        if (context.DepthBias is int bias)
        {
            average = Math.Max(0, unchecked(average + bias));
        }
        int bucket = checked(context.TableBaseBucket + (average >> context.DepthShift));
        NativeBattleEffectMeshWindow? window = context.Window;
        if (window is not null && (window.U | window.V) != 0)
        {
            AdjustAxis(window.U, 0, unchecked((byte)window.After.Width));
            AdjustAxis(window.V, 1, unchecked((byte)window.After.Height));
        }
        NativeBattleGeometryPrimitive primitive = new(bucket, packet.Words(), Payload(vertices, color));
        if (window is not null && (window.U | window.V) != 0)
        {
            return NativeBattleEffectTextureWindows.Bracket(packet.Attempt(null), primitive, window.Before, window.After);
        }
        return new([packet.Attempt(primitive)], length);

        void AdjustAxis(ushort amount, int axis, byte dimension)
        {
            if (amount == 0)
            {
                return;
            }
            ImmutableArray<byte> adjusted = NativeBattleEffectTextureWindows.OffsetAxis(uvWords.Select(value => unchecked((byte)(value >> (axis * 8)))), amount, dimension);
            for (int corner = 0; corner < uvOffsets.Length; corner++)
            {
                packet.Byte(uvOffsets[corner] + axis, adjusted[corner]);
            }
        }

        NativeBattleGfGeometryBatch Rejected()
        {
            return new([packet.Attempt(null)], 0);
        }
    }

    internal static NativeBattleGfGeometryBatch EmitFlatQuad(ReadOnlySpan<byte> face, Func<int, NativeBattleModuleProjectedVertex> readCorner, NativeBattleEffectMeshPacketContext context, NativeBattleSummonColors? colors, int byteOffset)
    {
        if (face.Length != 12 || context.DepthShift > 31)
        {
            throw new InvalidDataException("Native87F200/8846C0 requires its original12-byte F4 face and depth shift.");
        }
        NativeBattleModuleProjectedVertex[] vertices = [readCorner(0), readCorner(1), readCorner(2), default];
        uint color = Command(BinaryPrimitives.ReadUInt32LittleEndian(face), context.Flags, false);
        SkinWrites packet = new(24, byteOffset);
        packet.Word(0, 0x05000000);
        packet.Word(4, color);
        if (((vertices[0].Flags | vertices[1].Flags | vertices[2].Flags) & 0x60000) != 0)
        {
            return Rejected();
        }
        if (NativeBattleModulePackets.Nclip(vertices[0].Screen, vertices[1].Screen, vertices[2].Screen) < 0 && (context.Flags & 0x10) == 0)
        {
            return Rejected();
        }
        for (int corner = 0; corner < 3; corner++)
        {
            packet.Word(8 + corner * 4, vertices[corner].Screen);
        }
        vertices[3] = readCorner(3);
        packet.Word(20, vertices[3].Screen);
        if (OutsideScreen(vertices))
        {
            return Rejected();
        }
        if ((context.Flags & 0x40) != 0)
        {
            color = RequiredConstants(colors).Interpolate(color, RequiredFade(context), RequiredFar(context));
            packet.Word(4, color);
        }
        int bucket = checked(context.TableBaseBucket + (Average(vertices) >> context.DepthShift));
        return new([packet.Attempt(new(bucket, packet.Words(), Payload(vertices, color)))], 24);

        NativeBattleGfGeometryBatch Rejected()
        {
            return new([packet.Attempt(null)], 0);
        }
    }

    internal static NativeBattleGfGeometryBatch EmitGouraud(ReadOnlySpan<byte> face, Func<int, NativeBattleModuleProjectedVertex> readCorner, bool quad, NativeBattleEffectMeshPacketContext context, NativeBattleSummonColors? colors, int byteOffset)
    {
        if (face.Length != (quad ? 24 : 20) || context.DepthShift > 31)
        {
            throw new InvalidDataException("Native87F960/87FB90/885050 requires its original G3/G4 face and depth shift.");
        }
        NativeBattleModuleProjectedVertex[] vertices = new NativeBattleModuleProjectedVertex[quad ? 4 : 3];
        for (int corner = 0; corner < 3; corner++)
        {
            vertices[corner] = readCorner(corner);
        }
        uint firstColor = Command(BinaryPrimitives.ReadUInt32LittleEndian(face), context.Flags, true);
        int length = quad ? 36 : 28;
        SkinWrites packet = new(length, byteOffset);
        packet.Word(0, (uint)(length / 4 - 1) << 24);
        packet.Word(4, firstColor);
        if (((vertices[0].Flags | vertices[1].Flags | vertices[2].Flags) & 0x60000) != 0)
        {
            return Rejected();
        }
        if (NativeBattleModulePackets.Nclip(vertices[0].Screen, vertices[1].Screen, vertices[2].Screen) < 0 && (context.Flags & 0x20) == 0)
        {
            return Rejected();
        }
        for (int corner = 0; corner < 3; corner++)
        {
            packet.Word(8 + corner * 8, vertices[corner].Screen);
        }
        if (quad)
        {
            vertices[3] = readCorner(3);
            packet.Word(32, vertices[3].Screen);
        }
        if (OutsideScreen(vertices))
        {
            return Rejected();
        }
        ImmutableArray<uint>.Builder colorValues = ImmutableArray.CreateBuilder<uint>(vertices.Length);
        colorValues.Add(firstColor);
        for (int corner = 1; corner < vertices.Length; corner++)
        {
            colorValues.Add(BinaryPrimitives.ReadUInt32LittleEndian(face[(8 + corner * 4)..]));
        }
        ImmutableArray<uint> vertexColors = colorValues.MoveToImmutable();
        if ((context.Flags & 0x80) != 0)
        {
            vertexColors = NativeBattleGteColorInterpolation.Gouraud45F4C0(vertexColors, quad, RequiredFade(context), RequiredFar(context), RequiredConstants(colors));
        }
        for (int corner = 0; corner < vertices.Length; corner++)
        {
            packet.Word(4 + corner * 8, vertexColors[corner]);
        }
        int bucket = checked(context.TableBaseBucket + (Average(vertices) >> context.DepthShift));
        return new([packet.Attempt(new(bucket, packet.Words(), Payload(vertices, vertexColors[0])))], length);

        NativeBattleGfGeometryBatch Rejected()
        {
            return new([packet.Attempt(null)], 0);
        }
    }

    private static uint Command(uint color, uint flags, bool gouraud)
    {
        if ((flags & (gouraud ? 2u : 1u)) != 0)
        {
            color |= 0x02000000;
        }
        if ((flags & (gouraud ? 8u : 4u)) != 0)
        {
            color &= 0xFDFFFFFF;
        }
        return color;
    }

    private static bool OutsideScreen(NativeBattleModuleProjectedVertex[] vertices)
    {
        return vertices.All(vertex => Outside(unchecked((short)vertex.Screen), 2560)) || vertices.All(vertex => Outside(unchecked((short)(vertex.Screen >> 16)), 1728));
    }

    private static bool Outside(short value, int end) => value < 0 || value > end;
    private static int Average(NativeBattleModuleProjectedVertex[] vertices) => (int)(unchecked((uint)vertices.Sum(vertex => (int)vertex.Depth) * (vertices.Length == 4 ? 0x100u : 0x155u)) >> 12);
    private static int RequiredFade(NativeBattleEffectMeshPacketContext context) => context.Fade ?? throw new NotSupportedException("Native mesh fade requires its actual signed contextDWORD0C.");
    private static uint RequiredFar(NativeBattleEffectMeshPacketContext context) => context.FarColor ?? throw new NotSupportedException("Native mesh fade requires its actually written contextRGB8.");
    private static NativeBattleSummonColors RequiredConstants(NativeBattleSummonColors? colors) => colors ?? throw new NotSupportedException("Native mesh fade requires authenticated45F270/45F4C0 constants.");

    private static ImmutableArray<int> Payload(NativeBattleModuleProjectedVertex[] vertices, uint color)
    {
        uint opcode = color >> 24 & 0xFC;
        if (opcode is < 0x20 or > 0x3C)
        {
            return [0, 0, 0, 0];
        }
        if ((opcode & 8) != 0)
        {
            if (vertices.Length != 4)
            {
                throw new NotSupportedException("Native45C7A0 quad opcode after only RTPT requires its retained prior depth FIFO WORD.");
            }
            return [vertices[0].Depth, vertices[1].Depth, vertices[2].Depth, vertices[3].Depth];
        }
        return vertices.Length == 4 ? [vertices[1].Depth, vertices[2].Depth, vertices[3].Depth, 0] : [vertices[0].Depth, vertices[1].Depth, vertices[2].Depth, 0];
    }

    internal delegate NativeBattleGfGeometryBatch EmitFace(int group, ReadOnlySpan<byte> face, Func<int, NativeBattleModuleProjectedVertex> readCorner, int byteOffset);

    internal static NativeBattleGfGeometryBatch EmitBatch(ImmutableArray<byte> mesh, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, uint flags, Func<int, NativeBattleVector>? readSelectedVertex, EmitFace emit)
    {
        matrix.Validate();
        if (mesh.IsDefaultOrEmpty || mesh.Length < 8)
        {
            throw new InvalidDataException("Native effect mesh dispatcher requires its actual current mesh bytes.");
        }
        if ((flags & 0x2000) != 0 && readSelectedVertex is null)
        {
            throw new NotSupportedException("Native effect preserved vertex pointer requires its selected current XYZ owner.");
        }
        int cursor = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(Read(0, 4)));
        int allocated = 0;
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        for (int group = 0; group < 8; group++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(Read(cursor, 4));
            cursor = checked(cursor + 4);
            int stride = group switch { 0 or 1 => 12, 2 or 4 => 20, 3 or 5 => 24, 6 => 28, _ => 36 };
            for (int index = 0; index < count; index++)
            {
                ReadOnlySpan<byte> face = Read(cursor, stride);
                ushort[] offsets = new ushort[(group & 1) == 0 ? 3 : 4];
                for (int corner = 0; corner < offsets.Length; corner++)
                {
                    offsets[corner] = BinaryPrimitives.ReadUInt16LittleEndian(face[(4 + 2 * corner)..]);
                }
                NativeBattleModuleProjectedVertex Project(int corner)
                {
                    int offset = offsets[corner] * 4;
                    NativeBattleVector vertex;
                    if ((flags & 0x2000) != 0)
                    {
                        vertex = readSelectedVertex!(offset);
                    }
                    else
                    {
                        ReadOnlySpan<byte> xyz = Read(checked(8 + offset), 6);
                        vertex = new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz[2..]), BinaryPrimitives.ReadInt16LittleEndian(xyz[4..]));
                    }
                    return NativeBattleModuleProjection.Project45FE10(matrix, vertex, projection);
                }
                NativeBattleGfGeometryBatch batch = emit(group, face, Project, allocated);
                attempts.AddRange(batch.Attempts);
                allocated = checked(allocated + batch.AllocationBytes);
                cursor = checked(cursor + stride);
            }
        }
        return new(attempts.ToImmutable(), allocated);

        ReadOnlySpan<byte> Read(int offset, int length)
        {
            if (offset < 0 || offset > mesh.Length - length)
            {
                throw new InvalidDataException("Native effect mesh access leaves its current source bank.");
            }
            return mesh.AsSpan().Slice(offset, length);
        }
    }
}
