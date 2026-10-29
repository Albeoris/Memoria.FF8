using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Original additive textured quad leaves: separate E1 allocation, native clipping and mode-before-quad insertion.</summary>
public static class NativeBattleAdditiveQuadPackets
{
    public static NativeBattleGfGeometryBatch Emit(ImmutableArray<byte> source, int geometry, ImmutableArray<byte> vertices, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, int fade, uint color, NativeBattleSummonColors colors, bool includeFlatQuads, int depthBias, byte depthShift, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        int cursor = checked(geometry + BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan().Slice(geometry)));
        int allocated = 0;
        Dictionary<ushort, System.Numerics.Vector3>? world = pilotIdentity.HasValue ? [] : null;
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int[] lengths = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 8; group++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan().Slice(cursor));
            cursor += 4;
            if (count < 0 || (long)cursor + (long)count * lengths[group] > source.Length)
            {
                throw new InvalidDataException("Native additive quad mesh list exceeds its selected original resource.");
            }
            for (int faceIndex = 0; faceIndex < count; faceIndex++, cursor += lengths[group])
            {
                if (group != 7 && (!includeFlatQuads || group != 3))
                {
                    continue;
                }
                ImmutableArray<byte> face = source.Slice(cursor, lengths[group]);
                NativeBattleGfGeometryPackets.ProjectedCorners projected = new(Enumerable.Range(0, 4).Select(corner => BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan().Slice(4 + corner * 2))).ToArray(), Project);
                NativeBattleGfGeometryAttempt quad = NativeBattleGfGeometryPackets.EmitAttempt(face.AsSpan(), projected, group, new(fade == 0 ? 0x30u : 0xF0u, color, fade, null, null, 0, depthBias, 17, depthShift), colors, allocated);
                if (quad.Primitive is { } primitive)
                {
                    if (pilotIdentity is { } identity && world is not null)
                    {
                        System.Numerics.Vector3 Corner(int corner) => world[BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan().Slice(4 + corner * 2))];
                        quad = quad with { Primitive = primitive with { PilotGeometry = new(identity with { Primitive = unchecked(identity.Primitive * 397 + cursor) }, Corner(0), Corner(1), Corner(2), Corner(3), 4) } };
                    }
                    byte[] mode = new byte[8];
                    BinaryPrimitives.WriteUInt32LittleEndian(mode, 0x01000000);
                    BinaryPrimitives.WriteUInt32LittleEndian(mode.AsSpan(4), 0xE1000220);
                    attempts.Add(new(allocated + quad.PacketLength, mode.ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 8).ToImmutableArray(), new(primitive.Bucket, [0x01000000, 0xE1000220], [0, 0, 0, 0])));
                    allocated += quad.PacketLength + 8;
                }
                attempts.Add(quad);
            }
        }
        return new(attempts.ToImmutable(), allocated);

        NativeBattleModuleProjectedVertex Project(ushort index)
        {
            ReadOnlySpan<byte> point = vertices.AsSpan().Slice(index * 4, 6);
            NativeBattleVector local = new(BinaryPrimitives.ReadInt16LittleEndian(point), BinaryPrimitives.ReadInt16LittleEndian(point[2..]), BinaryPrimitives.ReadInt16LittleEndian(point[4..]));
            NativeBattleModuleProjectedVertex projected = NativeBattleModuleProjection.Project45FE10(matrix, local, projection);
            if (world is not null)
            {
                world[index] = NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(matrix, local), projection);
            }
            return projected;
        }
    }
}
