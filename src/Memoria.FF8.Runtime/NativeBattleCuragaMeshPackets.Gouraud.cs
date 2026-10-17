using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleCuragaMeshPackets
{
    public static NativeBattleGfGeometryBatch EmitG3_87F960(ReadOnlySpan<byte> face, Func<int, NativeBattleModuleProjectedVertex> readCorner, NativeBattleCuragaMeshContext context, NativeBattleSummonColors? colors = null, int byteOffset = 0)
    {
        return EmitGouraud(face, readCorner, false, context, colors, byteOffset);
    }

    public static NativeBattleGfGeometryBatch EmitG4_87FB90(ReadOnlySpan<byte> face, Func<int, NativeBattleModuleProjectedVertex> readCorner, NativeBattleCuragaMeshContext context, NativeBattleSummonColors? colors = null, int byteOffset = 0)
    {
        return EmitGouraud(face, readCorner, true, context, colors, byteOffset);
    }

    private static NativeBattleGfGeometryBatch EmitGouraud(ReadOnlySpan<byte> face, Func<int, NativeBattleModuleProjectedVertex> readCorner, bool quad, NativeBattleCuragaMeshContext context, NativeBattleSummonColors? colors, int byteOffset)
    {
        if (face.Length != (quad ? 24 : 20) || context.DepthShift > 31)
        {
            throw new InvalidDataException("Native87F960/87FB90 requires its original G3/G4 face and depth shift.");
        }
        NativeBattleModuleProjectedVertex[] vertices = new NativeBattleModuleProjectedVertex[quad ? 4 : 3];
        for (int corner = 0; corner < 3; corner++)
        {
            vertices[corner] = readCorner(corner);
        }
        uint firstColor = BinaryPrimitives.ReadUInt32LittleEndian(face);
        if ((context.Flags & 2) != 0)
        {
            firstColor |= 0x02000000;
        }
        if ((context.Flags & 8) != 0)
        {
            firstColor &= 0xFDFFFFFF;
        }
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
        if (vertices.All(vertex => Outside(unchecked((short)vertex.Screen), 2560)) || vertices.All(vertex => Outside(unchecked((short)(vertex.Screen >> 16)), 1728)))
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
            vertexColors = NativeBattleGteColorInterpolation.Gouraud45F4C0(vertexColors, quad, context.Fade ?? throw new NotSupportedException("Native Curaga G3/G4 fade requires its actual signed contextDWORD0C."), context.FarColor ?? throw new NotSupportedException("Native Curaga G3/G4 fade requires its actually written contextRGB8."), colors ?? throw new NotSupportedException("Native Curaga G3/G4 fade requires authenticated45F4C0 constants."));
        }
        for (int corner = 0; corner < vertices.Length; corner++)
        {
            packet.Word(4 + corner * 8, vertexColors[corner]);
        }
        uint average = unchecked((uint)vertices.Sum(vertex => (int)vertex.Depth) * (quad ? 0x100u : 0x155u)) >> 12;
        int bucket = checked(context.TableBaseBucket + ((int)average >> context.DepthShift));
        uint opcode = vertexColors[0] >> 24 & 0xFC;
        ImmutableArray<int> payload;
        if (opcode is < 0x20 or > 0x3C)
        {
            payload = [0, 0, 0, 0];
        }
        else if ((opcode & 8) != 0)
        {
            if (!quad)
            {
                throw new NotSupportedException("Native87F960 quad-shaped proxy command requires the original prior depth FIFO word.");
            }
            payload = [vertices[0].Depth, vertices[1].Depth, vertices[2].Depth, vertices[3].Depth];
        }
        else
        {
            payload = quad ? [vertices[1].Depth, vertices[2].Depth, vertices[3].Depth, 0] : [vertices[0].Depth, vertices[1].Depth, vertices[2].Depth, 0];
        }
        return new([packet.Attempt(new(bucket, packet.Words(), payload))], length);

        NativeBattleGfGeometryBatch Rejected() => new([packet.Attempt(null)], 0);
    }
}
