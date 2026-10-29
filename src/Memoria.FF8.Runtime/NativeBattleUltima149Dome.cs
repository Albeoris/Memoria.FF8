using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleUltima149Dome
{
    public static NativeBattleGfGeometryBatch Draw(NativeBattleStaticMeshResource dome, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, int frame, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        return NativeBattleEffectMeshPackets.EmitBatch(dome.Bytes, matrix, projection, 0, null, (group, face, project, offset) => group switch
        {
            0 or 1 or 5 => new([], 0),
            3 => EmitQuad(face, project, false, colors, frame, offset),
            7 => EmitQuad(face, project, true, colors, frame, offset),
            _ => throw new NotSupportedException("Native Ultima dome requires its original skipped groups and textured quads.")
        }, projectFourth: vertex => NativeBattleModuleProjection.Project45FAA0(matrix, vertex, projection, 0, 0), pilotIdentity: pilotIdentity);
    }

    public static NativeBattleGfGeometryBatch EmitQuad(ReadOnlySpan<byte> face, Func<int, NativeBattleModuleProjectedVertex> project, bool gouraud, NativeBattleSummonColors colors, int frame, int byteOffset = 0)
    {
        if (face.Length != (gouraud ? 36 : 24))
        {
            throw new InvalidDataException("Native Ultima dome requires a complete textured quad.");
        }
        int length = gouraud ? 64 : 52;
        int stride = gouraud ? 12 : 8;
        int[] uvOffsets = gouraud ? [16, 28, 40, 52] : [16, 24, 32, 40];
        uint finalUvs = BinaryPrimitives.ReadUInt32LittleEndian(face[20..]);
        uint[] uvs = [BinaryPrimitives.ReadUInt32LittleEndian(face[12..]), BinaryPrimitives.ReadUInt32LittleEndian(face[16..]), finalUvs, finalUvs >> 16];
        SkinWrites packet = new(length, byteOffset);
        uint firstColor = BinaryPrimitives.ReadUInt32LittleEndian(face) | 0x02000000;
        packet.Word(0, (uint)(length / 4 - 1) << 24);
        packet.Word(8, firstColor);
        for (int corner = 0; corner < 4; corner++)
        {
            packet.Word(uvOffsets[corner], uvs[corner]);
        }
        NativeBattleModuleProjectedVertex[] vertices = [project(0), project(1), project(2), default];
        if (((vertices[0].Flags | vertices[1].Flags | vertices[2].Flags) & 0x60000) != 0)
        {
            return Rejected();
        }
        if (NativeBattleModulePackets.Nclip(vertices[0].Screen, vertices[1].Screen, vertices[2].Screen) < 0)
        {
            return Rejected();
        }
        for (int corner = 0; corner < 3; corner++)
        {
            packet.Word(12 + corner * stride, vertices[corner].Screen);
        }
        vertices[3] = project(3);
        int vOffset = unchecked(frame << 3) % 128;
        if (vOffset != 0)
        {
            ImmutableArray<byte> adjusted = NativeBattleEffectTextureWindows.OffsetAxisSigned(uvs.Select(uv => unchecked((byte)(uv >> 8))), vOffset, 128);
            for (int corner = 0; corner < 4; corner++)
            {
                packet.Byte(uvOffsets[corner] + 1, adjusted[corner]);
            }
        }
        packet.Word(12 + 3 * stride, vertices[3].Screen);
        if (vertices.All(vertex => unchecked((short)vertex.Screen) is < 0 or > 2560) || vertices.All(vertex => unchecked((short)(vertex.Screen >> 16)) is < 0 or > 1728))
        {
            return Rejected();
        }
        if (gouraud)
        {
            ImmutableArray<uint> vertexColors = [firstColor, BinaryPrimitives.ReadUInt32LittleEndian(face[24..]), BinaryPrimitives.ReadUInt32LittleEndian(face[28..]), BinaryPrimitives.ReadUInt32LittleEndian(face[32..])];
            if (frame >= 54)
            {
                vertexColors = NativeBattleGteColorInterpolation.Gouraud45F4C0(vertexColors, true, unchecked((frame - 54) << 9), 0, colors);
            }
            for (int corner = 0; corner < 4; corner++)
            {
                packet.Word(8 + corner * 12, vertexColors[corner]);
            }
        }
        else if (frame >= 54)
        {
            packet.Word(8, colors.Interpolate(firstColor, unchecked((frame - 54) << 9), 0));
        }
        packet.Word(4, NativeBattleEffectTextureWindows.Encode(new(0, 0, 64, 128)));
        packet.Word(length - 8, 0xE2000000);
        packet.Word(length - 4, 0xE1000220);
        int average = unchecked(vertices.Sum(vertex => (int)vertex.Depth) * 0x100) >> 12;
        int bucket = 17 + Math.Max((average >> 2) - 1, 0);
        return new([packet.Attempt(new(bucket, packet.Words(), [0, 0, 0, 0]))], length);

        NativeBattleGfGeometryBatch Rejected() => new([packet.Attempt(null)], 0);
    }
}
