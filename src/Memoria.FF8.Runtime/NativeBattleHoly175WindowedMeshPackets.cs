using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleHoly175WindowedMeshContext(uint Flags, uint FarColor, int? Fade, uint ConstantColor, int UOffset, int VOffset, NativeBattleTextureWindow Window, int TableBaseBucket, byte DepthShift);

public static partial class NativeBattleHoly175WindowedMeshPackets
{
    public static NativeBattleGfGeometryBatch Draw5D3B80(NativeBattleModulePoolTask task, NativeBattleStaticMeshResource mesh, uint constantColor, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        if (task.Callback != 0x5D3B80)
        {
            throw new NotSupportedException("Native Holy windowed drawing requires its actual caller task.");
        }
        short phase = unchecked((short)task.ReadWord(0xC));
        short scale = unchecked((short)task.ReadWord(0x1C));
        NativeBattleActionMatrix local = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0)), scale, scale, scale) with { X = unchecked((short)task.ReadWord(0x10)), Y = unchecked((short)task.ReadWord(0x12)), Z = unchecked((short)task.ReadWord(0x14)) };
        NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.Compose56C2F0(view, local);
        NativeBattleHoly175WindowedMeshContext context = new(phase < 56 ? 0xF3u : 0x33u, 0, phase < 56 ? unchecked(4096 - 73 * phase) : null, constantColor, 0, unchecked((short)task.ReadWord(0x20) * phase), new(0, 128, 64, 128), 17, 2);
        return EmitBatch650720(mesh.Bytes, matrix, projection, context, colors);
    }

    public static NativeBattleGfGeometryBatch EmitBatch650720(ImmutableArray<byte> mesh, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, NativeBattleHoly175WindowedMeshContext context, NativeBattleSummonColors? colors = null)
    {
        ValidateContext(context);
        return NativeBattleEffectMeshPackets.EmitBatch(mesh, matrix, projection, 0, null, (group, face, project, offset) => group switch
        {
            2 => EmitTriangle(face, project, false, context, colors, offset),
            6 => EmitTriangle(face, project, true, context, colors, offset),
            3 => EmitQuad(face, project, false, context, colors, offset),
            7 => EmitQuad(face, project, true, context, colors, offset),
            _ => throw new NotSupportedException("Native Holy650720 requires its reached textured triangle/quad groups and empty other groups.")
        });
    }

    public static NativeBattleGfGeometryBatch EmitQuad(ReadOnlySpan<byte> face, Func<int, NativeBattleModuleProjectedVertex> readCorner, bool gouraud, NativeBattleHoly175WindowedMeshContext context, NativeBattleSummonColors? colors = null, int byteOffset = 0)
    {
        ValidateContext(context);
        ArgumentNullException.ThrowIfNull(readCorner);
        if (face.Length != (gouraud ? 36 : 24))
        {
            throw new InvalidDataException("Native Holy compound quad requires its complete original face.");
        }
        NativeBattleModuleProjectedVertex[] vertices = new NativeBattleModuleProjectedVertex[4];
        for (int corner = 0; corner < 3; corner++)
        {
            vertices[corner] = readCorner(corner);
        }
        int length = gouraud ? 60 : 48;
        int[] uvOffsets = gouraud ? [16, 28, 40, 52] : [16, 24, 32, 40];
        int[] screenOffsets = gouraud ? [12, 24, 36, 48] : [12, 20, 28, 36];
        uint finalUvs = BinaryPrimitives.ReadUInt32LittleEndian(face[20..]);
        uint[] uvWords = [BinaryPrimitives.ReadUInt32LittleEndian(face[12..]), BinaryPrimitives.ReadUInt32LittleEndian(face[16..]), finalUvs, finalUvs >> 16];
        uint firstColor = gouraud ? BinaryPrimitives.ReadUInt32LittleEndian(face) : context.ConstantColor | 0x2C000000;
        uint enableSemi = gouraud ? 2u : 1u;
        uint disableSemi = gouraud ? 8u : 4u;
        if ((context.Flags & enableSemi) != 0)
        {
            firstColor |= 0x02000000;
        }
        if ((context.Flags & disableSemi) != 0)
        {
            firstColor &= 0xFDFFFFFF;
        }
        SkinWrites packet = new(length, byteOffset);
        packet.Word(0, (uint)(length / 4 - 1) << 24);
        if (!gouraud)
        {
            packet.Word(8, firstColor);
        }
        for (int corner = 0; corner < 4; corner++)
        {
            packet.Word(uvOffsets[corner], uvWords[corner]);
        }
        if (((vertices[0].Flags | vertices[1].Flags | vertices[2].Flags) & 0x60000) != 0)
        {
            return Rejected();
        }
        AdjustAxis(context.UOffset % context.Window.Width, 0, context.Window.Width);
        AdjustAxis(context.VOffset % context.Window.Height, 1, context.Window.Height);
        int nclip = NativeBattleModulePackets.Nclip(vertices[0].Screen, vertices[1].Screen, vertices[2].Screen);
        if (nclip == 0 || nclip < 0 && (context.Flags & (gouraud ? 0x20u : 0x10u)) == 0)
        {
            return Rejected();
        }
        for (int corner = 0; corner < 3; corner++)
        {
            packet.Word(screenOffsets[corner], vertices[corner].Screen);
        }
        vertices[3] = readCorner(3);
        packet.Word(screenOffsets[3], vertices[3].Screen);
        if (vertices.All(vertex => unchecked((short)vertex.Screen) is < 0 or > 2560) || vertices.All(vertex => unchecked((short)(vertex.Screen >> 16)) is < 0 or > 1728))
        {
            return Rejected();
        }
        if (gouraud)
        {
            ImmutableArray<uint> vertexColors = [firstColor, BinaryPrimitives.ReadUInt32LittleEndian(face[24..]), BinaryPrimitives.ReadUInt32LittleEndian(face[28..]), BinaryPrimitives.ReadUInt32LittleEndian(face[32..])];
            if ((context.Flags & 0x80) != 0)
            {
                vertexColors = NativeBattleGteColorInterpolation.Gouraud45F4C0(vertexColors, true, RequiredFade(), context.FarColor, colors ?? throw new NotSupportedException("Native Holy Gouraud depth cue requires its actual color constants."));
            }
            for (int corner = 0; corner < 4; corner++)
            {
                packet.Word(8 + corner * 12, vertexColors[corner]);
            }
        }
        else if ((context.Flags & 0x40) != 0)
        {
            firstColor = (colors ?? throw new NotSupportedException("Native Holy flat depth cue requires its actual color constants.")).Interpolate(firstColor, RequiredFade(), context.FarColor);
            packet.Word(8, firstColor);
        }
        packet.Word(4, NativeBattleEffectTextureWindows.Encode(context.Window));
        packet.Word(length - 4, 0xE2000000);
        int average = unchecked(vertices.Sum(vertex => (int)vertex.Depth) * 0x100) >> 12;
        int bucket = checked(context.TableBaseBucket + (average >> context.DepthShift));
        NativeBattleGeometryPrimitive primitive = new(bucket, packet.Words(), [0, 0, 0, 0]);
        return new([packet.Attempt(primitive)], length);

        void AdjustAxis(int delta, int axis, short dimension)
        {
            if (delta == 0)
            {
                return;
            }
            ImmutableArray<byte> adjusted = NativeBattleEffectTextureWindows.OffsetAxisSigned(uvWords.Select(value => unchecked((byte)(value >> (axis * 8)))), delta, dimension);
            for (int corner = 0; corner < 4; corner++)
            {
                packet.Byte(uvOffsets[corner] + axis, adjusted[corner]);
            }
        }
        int RequiredFade() => context.Fade ?? throw new NotSupportedException("Native Holy depth cue requires its consumed fade DWORD.");
        NativeBattleGfGeometryBatch Rejected() => new([packet.Attempt(null)], 0);
    }

    private static void ValidateContext(NativeBattleHoly175WindowedMeshContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Window);
        if (context.DepthShift > 31 || context.Window.Width <= 0 || context.Window.Height <= 0)
        {
            throw new NotSupportedException("Native Holy compound quads require their actual positive signed window dimensions and depth shift.");
        }
    }
}
