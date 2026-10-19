using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleBombActorPackets
{
    public static NativeBattleGfGeometryBatch Emit(ImmutableArray<NativeBattleRawPolygon> polygons, ImmutableArray<byte> vertices, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, uint modelColor, uint overlayColor, int scroll, bool textured, short zsf3, short zsf4)
    {
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocation = 0;
        foreach (NativeBattleRawPolygon polygon in polygons)
        {
            int count = polygon.Quad ? 4 : 3;
            if (polygon.Bytes.Length != (polygon.Quad ? 20 : 16))
            {
                continue;
            }
            ReadOnlySpan<byte> face = polygon.Bytes.AsSpan();
            NativeBattleVector[] positions = new NativeBattleVector[count];
            ushort[] uvs = new ushort[count];
            NativeBattleModuleProjectedVertex[] projected = new NativeBattleModuleProjectedVertex[count];
            for (int corner = 0; corner < count; corner++)
            {
                int index = BinaryPrimitives.ReadUInt16LittleEndian(face.Slice(corner * 2)) & 0xFFF;
                ReadOnlySpan<byte> vertex = vertices.AsSpan().Slice(index * 8, 8);
                positions[corner] = new(BinaryPrimitives.ReadInt16LittleEndian(vertex), BinaryPrimitives.ReadInt16LittleEndian(vertex[2..]), BinaryPrimitives.ReadInt16LittleEndian(vertex[4..]));
                uvs[corner] = BinaryPrimitives.ReadUInt16LittleEndian(vertex[6..]);
                if (corner < 3)
                {
                    projected[corner] = NativeBattleModuleProjection.Project45FE10(view, positions[corner], projection);
                }
            }
            int length = polygon.Quad ? 40 : 32;
            SkinWrites packet = new(length, allocation);
            uint page = BinaryPrimitives.ReadUInt32LittleEndian(face[12..]);
            packet.Word(0, (uint)(length / 4 - 1) << 24);
            packet.Word(4, modelColor | (polygon.Quad ? 0x2C000000u : 0x24000000u) | (textured ? 0 : page & 0x2000000));
            packet.Word(12, BinaryPrimitives.ReadUInt32LittleEndian(face[8..]));
            packet.Word(20, textured ? page : page & 0x1FFFFFF);
            if (((projected[0].Flags | projected[1].Flags | projected[2].Flags) & 0x60000) != 0 || NativeBattleModulePackets.Nclip(projected[0].Screen, projected[1].Screen, projected[2].Screen) <= 0)
            {
                attempts.Add(packet.Attempt(null));
                continue;
            }
            for (int corner = 0; corner < 3; corner++)
            {
                packet.Word(8 + corner * 8, projected[corner].Screen);
            }
            if (polygon.Quad)
            {
                projected[3] = NativeBattleModuleProjection.Project45FE10(view, positions[3], projection);
                packet.Word(28, BinaryPrimitives.ReadUInt32LittleEndian(face[16..]));
                packet.Word(32, projected[3].Screen);
                packet.Word(36, unchecked((uint)(BinaryPrimitives.ReadInt32LittleEndian(face[16..]) >> 16)));
            }
            else
            {
                packet.Word(28, unchecked((uint)(BinaryPrimitives.ReadInt32LittleEndian(face[4..]) >> 16)));
            }
            uint average = unchecked((uint)projected.Sum(vertex => (int)vertex.Depth) * unchecked((uint)(polygon.Quad ? zsf4 : zsf3))) >> 12;
            int depth = (int)(average >> 2);
            int bucket = 17 + depth;
            ImmutableArray<int> payload = polygon.Quad ? projected.Select(vertex => (int)vertex.Depth).ToImmutableArray() : [projected[0].Depth, projected[1].Depth, projected[2].Depth, 0];
            attempts.Add(packet.Attempt(new(bucket, packet.Words(), payload)));
            allocation += length;
            int overlayLength = textured ? polygon.Quad ? 48 : 40 : polygon.Quad ? 24 : 20;
            SkinWrites overlay = new(overlayLength, allocation);
            overlay.Word(0, (uint)(overlayLength / 4 - 1) << 24);
            if (textured)
            {
                int[] v = positions.Select(position => (position.Y + scroll) / 3).ToArray();
                if (v.Select(value => value & 0xFF00).Distinct().Count() > 1)
                {
                    for (int corner = 0; corner < count; corner++)
                    {
                        uvs[corner] = unchecked((ushort)(((v[corner] - 128) << 8) ^ (byte)uvs[corner]));
                    }
                }
                overlay.Word(4, 0xE2080218);
                overlay.Word(8, overlayColor | (polygon.Quad ? 0x2E000000u : 0x26000000u));
                for (int corner = 0; corner < count; corner++)
                {
                    overlay.Word(12 + corner * 8, projected[corner].Screen);
                    overlay.Word16(16 + corner * 8, uvs[corner]);
                }
                overlay.Word16(18, 0x3F54);
                overlay.Word16(26, 0xBA);
                overlay.Word(overlayLength - 4, 0xE2000000);
            }
            else
            {
                overlay.Word(4, overlayColor | (polygon.Quad ? 0x2A000000u : 0x22000000u));
                for (int corner = 0; corner < count; corner++)
                {
                    overlay.Word(8 + corner * 4, projected[corner].Screen);
                }
            }
            attempts.Add(overlay.Attempt(new(17 + Math.Max(0, depth - 1), overlay.Words(), payload)));
            allocation += overlayLength;
        }
        return new(attempts.ToImmutable(), allocation);
    }
}
