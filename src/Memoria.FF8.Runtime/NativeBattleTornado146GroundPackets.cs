using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>The ground's flat and crater-weighted textured triangle/quad stores.</summary>
public static class NativeBattleTornado146GroundPackets
{
    public static NativeBattleGfGeometryBatch Emit616AC0(ImmutableArray<NativeBattleRawPolygon> polygons, ImmutableArray<NativeBattleTornado146GroundVertex> vertices, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, short zsf3, short zsf4, short dqa, int dqb, NativeBattleSummonColors colors, Action<int, uint>? storeContext = null, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int polygonIndex = 0; polygonIndex < polygons.Length; polygonIndex++)
        {
            NativeBattleRawPolygon polygon = polygons[polygonIndex];
            int count = polygon.Quad ? 4 : 3;
            ReadOnlySpan<byte> face = polygon.Bytes.AsSpan();
            if (face.Length != (polygon.Quad ? 24 : 20))
            {
                throw new NotSupportedException("Native crater ground requires its actual colored stage face groups.");
            }
            NativeBattleModuleProjectedVertex[] projected = new NativeBattleModuleProjectedVertex[count];
            NativeBattleTornado146GroundVertex[] corners = new NativeBattleTornado146GroundVertex[count];
            int weight = 0;
            for (int corner = 0; corner < count; corner++)
            {
                int index = BinaryPrimitives.ReadUInt16LittleEndian(face.Slice(corner * 2)) & 0xFFF;
                if ((uint)index >= vertices.Length)
                {
                    throw new InvalidDataException("Native crater ground face leaves its current transformed vertex owner.");
                }
                corners[corner] = vertices[index];
                weight += corners[corner].Weight;
                NativeBattleVector point = corners[corner].Position;
                storeContext?.Invoke(0x68 + corner * 8, (ushort)point.X | ((uint)(ushort)point.Y << 16));
                storeContext?.Invoke(0x6C + corner * 8, (ushort)point.Z | ((uint)(ushort)corners[corner].Weight << 16));
                if (corner < 3)
                {
                    projected[corner] = NativeBattleModuleProjection.Project45FE10(view, point, projection);
                }
            }
            weight /= count;
            storeContext?.Invoke(0x44, unchecked((uint)weight));
            bool deformed = weight != 0;
            int length = deformed ? polygon.Quad ? 52 : 40 : polygon.Quad ? 40 : 32;
            SkinWrites packet = new(length, allocated);
            packet.Word(0, (uint)(length / 4 - 1) << 24);
            uint command = BinaryPrimitives.ReadUInt32LittleEndian(face.Slice(polygon.Quad ? 20 : 16));
            packet.Word(4, command);
            packet.Word(12, BinaryPrimitives.ReadUInt32LittleEndian(face[8..]));
            packet.Word(deformed ? 24 : 20, BinaryPrimitives.ReadUInt32LittleEndian(face[12..]));
            if (deformed)
            {
                command = (command & 0xFFFFFF) | (polygon.Quad ? 0x3C000000u : 0x34000000u);
                packet.Byte(7, polygon.Quad ? (byte)0x3C : (byte)0x34);
            }
            else
            {
                uint flags = projected[0].Flags | projected[1].Flags | projected[2].Flags;
                storeContext?.Invoke(0x4C, flags);
                if ((flags & 0x60000) != 0)
                {
                    attempts.Add(packet.Attempt(null));
                    continue;
                }
            }
            for (int corner = 0; corner < 3; corner++)
            {
                packet.Word(8 + corner * (deformed ? 12 : 8), projected[corner].Screen);
            }
            if (polygon.Quad)
            {
                projected[3] = NativeBattleModuleProjection.Project45FAA0(view, corners[3].Position, projection, dqa, dqb);
                uint uv = BinaryPrimitives.ReadUInt32LittleEndian(face[16..]);
                packet.Word(deformed ? 36 : 28, uv);
                packet.Word(deformed ? 48 : 36, unchecked((uint)((int)uv >> 16)));
                packet.Word(deformed ? 44 : 32, projected[3].Screen);
            }
            else if (deformed)
            {
                packet.Word(36, unchecked((uint)(BinaryPrimitives.ReadInt32LittleEndian(face[4..]) >> 16)));
            }
            if (!deformed && (projected.All(point => unchecked((short)point.Screen) is < 0 or > 2560) || projected.All(point => unchecked((short)(point.Screen >> 16)) is < 0 or > 1728)))
            {
                attempts.Add(packet.Attempt(null));
                continue;
            }
            if (!polygon.Quad && !deformed)
            {
                packet.Word(28, unchecked((uint)(BinaryPrimitives.ReadInt32LittleEndian(face[4..]) >> 16)));
            }
            uint average = unchecked((uint)projected.Sum(point => (int)point.Depth) * unchecked((uint)(polygon.Quad ? zsf4 : zsf3))) >> 12;
            storeContext?.Invoke(0x48, average);
            if (deformed)
            {
                for (int corner = 0; corner < count; corner++)
                {
                    packet.Word(4 + corner * 12, colors.Interpolate(command, corners[corner].Weight, 0));
                }
            }
            ImmutableArray<int> depths = projected.Select(point => (int)point.Depth).ToImmutableArray();
            if (!polygon.Quad)
            {
                depths = depths.Add(0);
            }
            NativeBattleGeometryPrimitive primitive = new(checked(4122 + (int)(average >> (deformed ? 6 : 12))), packet.Words(), depths);
            if (pilotIdentity is { } identity)
            {
                System.Numerics.Vector3 World(int corner)
                {
                    NativeBattleVector point = corners[corner].Position;
                    return new(point.X, point.Y, point.Z);
                }
                primitive = primitive with { PilotGeometry = new(identity with { Primitive = unchecked(identity.Primitive * 65537 + polygonIndex) }, World(0), World(1), World(2), polygon.Quad ? World(3) : default, (byte)count) };
            }
            attempts.Add(packet.Attempt(primitive));
            allocated = checked(allocated + length);
        }
        return new(attempts.ToImmutable(), allocated);
    }
}
