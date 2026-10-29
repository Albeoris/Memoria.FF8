using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleShivaScrollingContext(uint Flags, int? Fade, int UOffset, int VOffset, int TableBaseBucket, byte DepthShift, NativeBattleTextureWindow Window);

/// <summary>64E080's reached textured polygon groups with inline E2 window/reset words and signed scrolling.</summary>
public static class NativeBattleShivaScrollingPackets
{
    public static NativeBattleGfGeometryBatch Emit64E080(NativeBattleStaticMeshResource mesh, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, NativeBattleShivaScrollingContext context, short zsf3, short zsf4, short dqa, int dqb, NativeBattleSummonColors colors, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        return NativeBattleEffectMeshPackets.EmitBatch(mesh.Bytes, matrix, projection, 0, null, (group, face, project, offset) => group switch
        {
            2 or 3 or 6 or 7 => EmitFace(face, project, (group & 1) != 0, group >= 6, context, zsf3, zsf4, colors, offset),
            _ => throw new NotSupportedException("Native Shiva scrolling requires its reached original textured polygon groups.")
        }, projectFourth: vertex => NativeBattleModuleProjection.Project45FAA0(matrix, vertex, projection, dqa, dqb), pilotIdentity: pilotIdentity);
    }

    public static NativeBattleGfGeometryBatch EmitFace(ReadOnlySpan<byte> face, Func<int, NativeBattleModuleProjectedVertex> readCorner, bool quad, bool gouraud, NativeBattleShivaScrollingContext context, short zsf3, short zsf4, NativeBattleSummonColors colors, int byteOffset = 0)
    {
        if (face.Length != (gouraud ? quad ? 36 : 28 : quad ? 24 : 20) || context.DepthShift > 31 || (context.Flags & ~0xFFu) != 0 || context.Window is not { Width: > 0, Height: > 0 })
        {
            throw new InvalidDataException("Native scrolling polygon requires its complete reached face and context.");
        }
        NativeBattleModuleProjectedVertex[] vertices = new NativeBattleModuleProjectedVertex[quad ? 4 : 3];
        for (int corner = 0; corner < 3; corner++)
        {
            vertices[corner] = readCorner(corner);
        }
        int length = gouraud ? quad ? 60 : 48 : quad ? 48 : 40;
        int stride = gouraud ? 12 : 8;
        int[] uvOffsets = quad ? gouraud ? [16, 28, 40, 52] : [16, 24, 32, 40] : gouraud ? [16, 28, 40] : [16, 24, 32];
        uint final = quad ? BinaryPrimitives.ReadUInt32LittleEndian(face[20..]) : BinaryPrimitives.ReadUInt16LittleEndian(face[10..]);
        uint[] uvs = quad ? [BinaryPrimitives.ReadUInt32LittleEndian(face[12..]), BinaryPrimitives.ReadUInt32LittleEndian(face[16..]), final, final >> 16] : [BinaryPrimitives.ReadUInt32LittleEndian(face[12..]), BinaryPrimitives.ReadUInt32LittleEndian(face[16..]), final];
        uint firstColor = BinaryPrimitives.ReadUInt32LittleEndian(face);
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
        packet.Word(8, firstColor);
        for (int corner = 0; corner < uvs.Length; corner++)
        {
            packet.Word(uvOffsets[corner], uvs[corner]);
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
            packet.Word(12 + corner * stride, vertices[corner].Screen);
        }
        if (quad)
        {
            vertices[3] = readCorner(3);
            packet.Word(12 + 3 * stride, vertices[3].Screen);
        }
        if (vertices.All(vertex => unchecked((short)vertex.Screen) is < 0 or > 2560) || vertices.All(vertex => unchecked((short)(vertex.Screen >> 16)) is < 0 or > 1728))
        {
            return Rejected();
        }
        if (gouraud)
        {
            ImmutableArray<uint> sourceColors = quad ? [firstColor, BinaryPrimitives.ReadUInt32LittleEndian(face[24..]), BinaryPrimitives.ReadUInt32LittleEndian(face[28..]), BinaryPrimitives.ReadUInt32LittleEndian(face[32..])] : [firstColor, BinaryPrimitives.ReadUInt32LittleEndian(face[20..]), BinaryPrimitives.ReadUInt32LittleEndian(face[24..])];
            if ((context.Flags & 0x80) != 0)
            {
                sourceColors = NativeBattleGteColorInterpolation.Gouraud45F4C0(sourceColors, quad, RequiredFade(), 0, colors);
            }
            for (int corner = 0; corner < vertices.Length; corner++)
            {
                packet.Word(8 + corner * 12, sourceColors[corner]);
            }
        }
        else if ((context.Flags & 0x40) != 0)
        {
            packet.Word(8, colors.Interpolate(firstColor, RequiredFade(), 0));
        }
        packet.Word(4, NativeBattleEffectTextureWindows.Encode(context.Window));
        packet.Word(length - 4, 0xE2000000);
        uint average = unchecked((uint)vertices.Sum(vertex => (int)vertex.Depth) * (uint)(int)(quad ? zsf4 : zsf3)) >> 12;
        int bucket = unchecked(context.TableBaseBucket + ((int)average >> context.DepthShift));
        NativeBattleGeometryPrimitive primitive = new(bucket, packet.Words(), [0, 0, 0, 0]);
        return new([packet.Attempt(primitive)], length);

        void AdjustAxis(int delta, int axis, short dimension)
        {
            if (delta == 0)
            {
                return;
            }
            ImmutableArray<byte> adjusted = NativeBattleEffectTextureWindows.OffsetAxisSigned(uvs.Select(value => unchecked((byte)(value >> (axis * 8)))), delta, dimension);
            for (int corner = 0; corner < uvs.Length; corner++)
            {
                packet.Byte(uvOffsets[corner] + axis, adjusted[corner]);
            }
        }
        int RequiredFade() => context.Fade ?? throw new NotSupportedException("Native scrolling depth cue requires its consumed context operand.");
        NativeBattleGfGeometryBatch Rejected() => new([packet.Attempt(null)], 0);
    }
}
