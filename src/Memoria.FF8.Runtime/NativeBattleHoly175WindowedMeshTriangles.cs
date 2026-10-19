using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleHoly175WindowedMeshPackets
{
    public static NativeBattleGfGeometryBatch EmitTriangle(ReadOnlySpan<byte> face, Func<int, NativeBattleModuleProjectedVertex> readCorner, bool gouraud, NativeBattleHoly175WindowedMeshContext context, NativeBattleSummonColors? colors = null, int byteOffset = 0)
    {
        ValidateContext(context);
        ArgumentNullException.ThrowIfNull(readCorner);
        if (face.Length != (gouraud ? 28 : 20))
        {
            throw new InvalidDataException("Native windowed triangle requires its complete original face.");
        }
        NativeBattleModuleProjectedVertex[] vertices = [readCorner(0), readCorner(1), readCorner(2)];
        int length = gouraud ? 48 : 40;
        int[] uvOffsets = gouraud ? [16, 28, 40] : [16, 24, 32];
        int[] screenOffsets = gouraud ? [12, 24, 36] : [12, 20, 28];
        uint[] uvWords = [BinaryPrimitives.ReadUInt32LittleEndian(face[12..]), BinaryPrimitives.ReadUInt32LittleEndian(face[16..]), BinaryPrimitives.ReadUInt16LittleEndian(face[10..])];
        uint firstColor = gouraud ? BinaryPrimitives.ReadUInt32LittleEndian(face) : context.ConstantColor | 0x24000000;
        if ((context.Flags & (gouraud ? 2u : 1u)) != 0)
        {
            firstColor |= 0x02000000;
        }
        if ((context.Flags & (gouraud ? 8u : 4u)) != 0)
        {
            firstColor &= 0xFDFFFFFF;
        }
        SkinWrites packet = new(length, byteOffset);
        packet.Word(0, (uint)(length / 4 - 1) << 24);
        if (!gouraud)
        {
            packet.Word(8, firstColor);
        }
        for (int corner = 0; corner < 3; corner++)
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
        if (vertices.All(vertex => unchecked((ushort)vertex.Screen) > 2560) || vertices.All(vertex => unchecked((ushort)(vertex.Screen >> 16)) > 1728))
        {
            return Rejected();
        }
        if (gouraud)
        {
            ImmutableArray<uint> vertexColors = [Multiply(firstColor), Multiply(BinaryPrimitives.ReadUInt32LittleEndian(face[20..])), Multiply(BinaryPrimitives.ReadUInt32LittleEndian(face[24..]))];
            if ((context.Flags & 0x80) != 0)
            {
                vertexColors = NativeBattleGteColorInterpolation.Gouraud45F4C0(vertexColors, false, RequiredFade(), context.FarColor, colors ?? throw new NotSupportedException("Native windowed Gouraud depth cue requires its actual color constants."));
            }
            for (int corner = 0; corner < 3; corner++)
            {
                packet.Word(8 + corner * 12, vertexColors[corner]);
            }
        }
        else if ((context.Flags & 0x40) != 0)
        {
            packet.Word(8, (colors ?? throw new NotSupportedException("Native windowed flat depth cue requires its actual color constants.")).Interpolate(firstColor, RequiredFade(), context.FarColor));
        }
        packet.Word(4, NativeBattleEffectTextureWindows.Encode(context.Window));
        packet.Word(length - 4, 0xE2000000);
        int average = (int)(unchecked((uint)vertices.Sum(vertex => (int)vertex.Depth) * 0x155u) >> 12);
        int bucket = checked(context.TableBaseBucket + (average >> context.DepthShift));
        return new([packet.Attempt(new(bucket, packet.Words(), [0, 0, 0, 0]))], length);

        uint Multiply(uint value)
        {
            uint result = value & 0xFF000000;
            for (int channel = 0; channel < 3; channel++)
            {
                int shift = channel * 8;
                uint component = unchecked((byte)(((context.ConstantColor >> shift & 255) * (value >> shift & 255)) >> 7));
                result |= component << shift;
            }
            return result;
        }
        void AdjustAxis(int delta, int axis, short dimension)
        {
            if (delta == 0)
            {
                return;
            }
            ImmutableArray<byte> adjusted = NativeBattleEffectTextureWindows.OffsetAxisSigned(uvWords.Select(value => unchecked((byte)(value >> (axis * 8)))), delta, dimension);
            for (int corner = 0; corner < 3; corner++)
            {
                packet.Byte(uvOffsets[corner] + axis, adjusted[corner]);
            }
        }
        int RequiredFade() => context.Fade ?? throw new NotSupportedException("Native windowed depth cue requires its consumed fade DWORD.");
        NativeBattleGfGeometryBatch Rejected() => new([packet.Attempt(null)], 0);
    }
}
