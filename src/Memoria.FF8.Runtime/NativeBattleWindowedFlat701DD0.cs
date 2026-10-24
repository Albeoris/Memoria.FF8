using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

/// <summary>The windowed mesh dispatcher's untextured flat triangle and quad stores.</summary>
public static class NativeBattleWindowedFlat701DD0
{
    public static NativeBattleGfGeometryBatch EmitF3_701F00(ReadOnlySpan<byte> face, Func<int, NativeBattleModuleProjectedVertex> readCorner, NativeBattleEsunaMeshContext context, NativeBattleSummonColors constants, int byteOffset = 0)
    {
        return Emit(face, readCorner, false, context, constants, byteOffset);
    }

    public static NativeBattleGfGeometryBatch EmitF4_702130(ReadOnlySpan<byte> face, Func<int, NativeBattleModuleProjectedVertex> readCorner, NativeBattleEsunaMeshContext context, NativeBattleSummonColors constants, int byteOffset = 0)
    {
        return Emit(face, readCorner, true, context, constants, byteOffset);
    }

    private static NativeBattleGfGeometryBatch Emit(ReadOnlySpan<byte> face, Func<int, NativeBattleModuleProjectedVertex> readCorner, bool quad, NativeBattleEsunaMeshContext context, NativeBattleSummonColors constants, int byteOffset)
    {
        if (face.Length != 12 || context.DepthShift > 31)
        {
            throw new InvalidDataException("Windowed flat geometry requires its twelve-byte source and native depth shift.");
        }
        NativeBattleModuleProjectedVertex[] vertices = new NativeBattleModuleProjectedVertex[quad ? 4 : 3];
        for (int corner = 0; corner < 3; corner++)
        {
            vertices[corner] = readCorner(corner);
        }
        int length = quad ? 24 : 20;
        uint color = BinaryPrimitives.ReadUInt32LittleEndian(face);
        SkinWrites packet = new(length, byteOffset);
        packet.Word(0, quad ? 0x05000000u : 0x04000000u);
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
        if (quad)
        {
            vertices[3] = readCorner(3);
            packet.Word(20, vertices[3].Screen);
        }
        if (vertices.All(vertex => Outside(unchecked((short)vertex.Screen), 2560)) || vertices.All(vertex => Outside(unchecked((short)(vertex.Screen >> 16)), 1728)))
        {
            return Rejected();
        }
        if ((context.Flags & 0x40) != 0)
        {
            color = constants.Interpolate(color, context.Fade ?? throw new NotSupportedException("Windowed flat shading requires its current depth cue."), context.FarColor ?? throw new NotSupportedException("Windowed flat shading requires its current far RGB."));
        }
        if ((context.Flags & 1) != 0)
        {
            color |= 0x02000000;
        }
        if ((context.Flags & 4) != 0)
        {
            color &= 0xFDFFFFFF;
        }
        packet.Word(4, color);
        int average = (int)(unchecked((uint)vertices.Sum(vertex => (int)vertex.Depth) * (quad ? 0x100u : 0x155u)) >> 12);
        int depth = Math.Max(0, unchecked(average + context.DepthBias));
        int bucket = checked(context.TableBaseBucket + (depth >> context.DepthShift));
        NativeBattleGeometryPrimitive primitive = new(bucket, packet.Words(), quad ? [vertices[0].Depth, vertices[1].Depth, vertices[2].Depth, vertices[3].Depth] : [vertices[0].Depth, vertices[1].Depth, vertices[2].Depth, 0]);
        return new([packet.Attempt(primitive)], length);

        NativeBattleGfGeometryBatch Rejected()
        {
            return new([packet.Attempt(null)], 0);
        }
    }

    private static bool Outside(short coordinate, int maximum) => coordinate < 0 || coordinate > maximum;
}
