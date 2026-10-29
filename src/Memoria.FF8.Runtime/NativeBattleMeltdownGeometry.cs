using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleMeltdownGeometry
{
    public static ImmutableArray<byte> MeltVertices(NativeBattleRawMesh mesh, ImmutableArray<NativeBattleActionMatrix> bones, NativeBattleModuleAnchor target, NativeBattleModuleAnchor caster, int strength)
    {
        short planeY = unchecked((short)(target.Y + 500));
        if (planeY > 0)
        {
            planeY = 0;
        }
        var normalized = NativeBattleActionGeometryRules.NormalizeVector56BC50(caster.X - target.X, caster.Y - planeY, caster.Z - target.Z);
        short nx = unchecked((short)normalized.X);
        short ny = unchecked((short)normalized.Y);
        short nz = unchecked((short)normalized.Z);
        int plane = unchecked(nx * target.X + ny * planeY + nz * target.Z);
        short intercept = unchecked((short)-(plane >> 12));
        int divisor = ny == 0 ? 1 : -ny;
        byte[] result = new byte[checked(mesh.Vertices.Length * 8)];
        for (int index = 0; index < mesh.Vertices.Length; index++)
        {
            NativeBattleRawVertex vertex = mesh.Vertices[index];
            NativeBattleFullMacVertex transformed = NativeBattleGfMatrices.TransformMac460840(bones[vertex.Bone], vertex.Position);
            short x = (short)Math.Clamp(transformed.X, short.MinValue, short.MaxValue);
            short y = (short)Math.Clamp(transformed.Y, short.MinValue, short.MaxValue);
            short z = (short)Math.Clamp(transformed.Z, short.MinValue, short.MaxValue);
            short dx = unchecked((short)(x - target.X));
            short dy = unchecked((short)(y - target.Y));
            short dz = unchecked((short)(z - target.Z));
            int squared = unchecked(dx * dx + dy * dy + dz * dz);
            if (squared < 6250000)
            {
                squared = Math.Max(squared, 640000);
                int weight = unchecked((unchecked(squared << 12) / 6250000) * strength) >> 9;
                int distance = unchecked(((intercept << 12) + nx * x + ny * y + nz * z) >> 12);
                int push = unchecked((unchecked(plane - (distance << 12)) / divisor) * weight) >> 12;
                if (push < 0)
                {
                    push = unchecked(-push);
                }
                if (push > 15000)
                {
                    push = 15000;
                }
                x = unchecked((short)(x - unchecked((short)(unchecked(nx * push) >> 12))));
                y = unchecked((short)(y - unchecked((short)(unchecked(ny * push) >> 12))));
                z = unchecked((short)(z - unchecked((short)(unchecked(nz * push) >> 12))));
                if (y > 0)
                {
                    y = 0;
                }
            }
            BinaryPrimitives.WriteInt16LittleEndian(result.AsSpan(index * 8), x);
            BinaryPrimitives.WriteInt16LittleEndian(result.AsSpan(index * 8 + 2), y);
            BinaryPrimitives.WriteInt16LittleEndian(result.AsSpan(index * 8 + 4), z);
        }
        return result.ToImmutableArray();
    }

    public static NativeBattleGfGeometryBatch Emit612B40(NativeBattleRawMesh mesh, ImmutableArray<byte> world, NativeBattleActionMatrix camera, NativeBattleCameraProjection projection, uint color, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        int polygonIndex = 0;
        System.Numerics.Vector3[]? pilotWorld = pilotIdentity.HasValue ? new System.Numerics.Vector3[4] : null;
        int pilotCorner = 0;
        foreach (NativeBattleRawPolygon polygon in mesh.Polygons)
        {
            int sourcePolygon = polygonIndex++;
            pilotCorner = 0;
            int corners = polygon.Quad ? 4 : 3;
            if (polygon.Bytes.Length != (polygon.Quad ? 20 : 16))
            {
                continue;
            }
            ReadOnlySpan<byte> face = polygon.Bytes.AsSpan();
            int length = polygon.Quad ? 40 : 32;
            byte[] data = new byte[length];
            NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[length];
            uint page = BinaryPrimitives.ReadUInt32LittleEndian(face[12..]);
            Put(0, (uint)(length / 4 - 1) << 24);
            Put(4, (page & 0x2000000) | color | (polygon.Quad ? 0x2C000000u : 0x24000000u));
            Put(12, BinaryPrimitives.ReadUInt32LittleEndian(face[8..]));
            Put(20, page & 0x1FFFFFF);
            NativeBattleModuleProjectedVertex[] vertices = new NativeBattleModuleProjectedVertex[corners];
            for (int corner = 0; corner < 3; corner++)
            {
                vertices[corner] = Project(BinaryPrimitives.ReadUInt16LittleEndian(face[(corner * 2)..]) & 0xFFF);
            }
            if (((vertices[0].Flags | vertices[1].Flags | vertices[2].Flags) & 0x60000) != 0 || NativeBattleModulePackets.Nclip(vertices[0].Screen, vertices[1].Screen, vertices[2].Screen) <= 0)
            {
                attempts.Add(new(allocated, data.ToImmutableArray(), mask.ToImmutableArray(), null));
                continue;
            }
            Put(8, vertices[0].Screen);
            Put(16, vertices[1].Screen);
            Put(24, vertices[2].Screen);
            if (polygon.Quad)
            {
                vertices[3] = Project(BinaryPrimitives.ReadUInt16LittleEndian(face[6..]) & 0xFFF);
                uint uv = BinaryPrimitives.ReadUInt32LittleEndian(face[16..]);
                Put(28, uv);
                Put(36, unchecked((uint)((int)uv >> 16)));
                Put(32, vertices[3].Screen);
            }
            else
            {
                Put(28, unchecked((uint)(BinaryPrimitives.ReadInt32LittleEndian(face[4..]) >> 16)));
            }
            uint average = unchecked((uint)vertices.Sum(vertex => (int)vertex.Depth) * (polygon.Quad ? 256u : 341u)) >> 12;
            int bucket = checked(17 + ((int)average >> 2));
            ImmutableArray<uint> words = Enumerable.Range(0, length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(index * 4))).ToImmutableArray();
            ImmutableArray<int> depths = polygon.Quad ? [vertices[0].Depth, vertices[1].Depth, vertices[2].Depth, vertices[3].Depth] : [vertices[0].Depth, vertices[1].Depth, vertices[2].Depth, 0];
            NativeBattlePilotVisualGeometry? visual = pilotIdentity is { } identity ? new(identity with { Primitive = unchecked(identity.Primitive * 65537 + sourcePolygon) }, pilotWorld![0], pilotWorld[1], pilotWorld[2], polygon.Quad ? pilotWorld[3] : default, (byte)corners) : null;
            attempts.Add(new(allocated, data.ToImmutableArray(), mask.ToImmutableArray(), new(bucket, words, depths) { PilotGeometry = visual }));
            allocated += length;

            void Put(int offset, uint value)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), value);
                mask.AsSpan(offset, 4).Fill(NativeBattleGfPacketByteState.Known);
            }
        }
        return new(attempts.ToImmutable(), allocated);

        NativeBattleModuleProjectedVertex Project(int index)
        {
            ReadOnlySpan<byte> bytes = world.AsSpan().Slice(index * 8, 6);
            NativeBattleVector point = new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes[2..]), BinaryPrimitives.ReadInt16LittleEndian(bytes[4..]));
            if (pilotWorld is not null)
            {
                pilotWorld[pilotCorner++] = new(point.X, point.Y, point.Z);
            }
            return NativeBattleModuleProjection.Project45FE10(camera, point, projection);
        }
    }
}
