using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Shared plain actor faces followed by a per-corner radial tint at the adjacent ordering bucket.</summary>
public static class NativeBattleActorTintPackets
{
    public static NativeBattleGfGeometryBatch Emit(ImmutableArray<NativeBattleRawPolygon> polygons, ImmutableArray<byte> worldVertices, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, uint modelColor, uint tint, NativeBattleSummonColors colors, Func<IReadOnlyList<NativeBattleVector>, short[], bool> writeFactors, short zsf3 = 0x155, short zsf4 = 0x100, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        int polygonIndex = -1;
        foreach (NativeBattleRawPolygon polygon in polygons)
        {
            polygonIndex++;
            int count = polygon.Quad ? 4 : 3;
            if (polygon.Bytes.Length != (polygon.Quad ? 20 : 16))
            {
                continue;
            }
            ReadOnlySpan<byte> face = polygon.Bytes.AsSpan();
            NativeBattleVector[] positions = new NativeBattleVector[count];
            short[] ratios = new short[count];
            NativeBattleModuleProjectedVertex[] projected = new NativeBattleModuleProjectedVertex[count];
            for (int corner = 0; corner < count; corner++)
            {
                int index = BinaryPrimitives.ReadUInt16LittleEndian(face.Slice(corner * 2)) & 0xFFF;
                ReadOnlySpan<byte> bytes = worldVertices.AsSpan().Slice(index * 8, 8);
                positions[corner] = new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes[2..]), BinaryPrimitives.ReadInt16LittleEndian(bytes[4..]));
                ratios[corner] = BinaryPrimitives.ReadInt16LittleEndian(bytes[6..]);
                if (corner < 3)
                {
                    projected[corner] = NativeBattleModuleProjection.Project45FE10(view, positions[corner], projection);
                }
            }
            int length = polygon.Quad ? 40 : 32;
            SkinWrites packet = new(length, allocated);
            packet.Word(0, (uint)(length / 4 - 1) << 24);
            uint page = BinaryPrimitives.ReadUInt32LittleEndian(face[12..]);
            packet.Word(4, (page & 0x2000000) | modelColor | (polygon.Quad ? 0x2C000000u : 0x24000000u));
            packet.Word(12, BinaryPrimitives.ReadUInt32LittleEndian(face[8..]));
            packet.Word(20, page & 0x1FFFFFF);
            if (NativeBattleModulePackets.Nclip(projected[0].Screen, projected[1].Screen, projected[2].Screen) <= 0)
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
                packet.Word(36, unchecked((uint)(BinaryPrimitives.ReadInt32LittleEndian(face[16..]) >> 16)));
                packet.Word(32, projected[3].Screen);
            }
            else
            {
                packet.Word(28, unchecked((uint)(BinaryPrimitives.ReadInt32LittleEndian(face[4..]) >> 16)));
            }
            uint average = unchecked((uint)projected.Sum(corner => (int)corner.Depth) * unchecked((uint)(polygon.Quad ? zsf4 : zsf3))) >> 12;
            int bucket = checked(17 + (int)(average >> 2));
            ImmutableArray<int> depths = polygon.Quad ? projected.Select(corner => (int)corner.Depth).ToImmutableArray() : [projected[0].Depth, projected[1].Depth, projected[2].Depth, 0];
            attempts.Add(packet.Attempt(Capture(new(bucket, packet.Words(), depths), 0)));
            allocated += length;
            bool near = writeFactors(positions, ratios);
            if (!near)
            {
                continue;
            }
            int glowLength = polygon.Quad ? 36 : 28;
            SkinWrites glow = new(glowLength, allocated);
            glow.Word(0, (uint)(glowLength / 4 - 1) << 24);
            uint command = polygon.Quad ? 0x3A000000u : 0x32000000u;
            for (int corner = 0; corner < count; corner++)
            {
                glow.Word(4 + corner * 8, colors.Interpolate(command, ratios[corner], tint));
                glow.Word(8 + corner * 8, projected[corner].Screen);
            }
            attempts.Add(glow.Attempt(Capture(new(bucket - 1, glow.Words(), depths), 1)));
            allocated += glowLength;

            NativeBattleGeometryPrimitive Capture(NativeBattleGeometryPrimitive primitive, int layer)
            {
                if (pilotIdentity is not { } identity)
                {
                    return primitive;
                }
                System.Numerics.Vector3 Point(int corner) => new(positions[corner].X, positions[corner].Y, positions[corner].Z);
                NativeBattlePilotVisualGeometry geometry = new(identity with { Primitive = unchecked((identity.Primitive * 397 + polygonIndex) * 2 + layer) }, Point(0), Point(1), Point(2), count == 4 ? Point(3) : default, (byte)count) { SourceDepths = new(depths[0], depths[1], depths[2], depths[3]), UseSourceDepths = true };
                return primitive with { PilotGeometry = geometry };
            }
        }
        return new(attempts.ToImmutable(), allocated);
    }
}
