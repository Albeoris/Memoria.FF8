using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleFlare124ActorDraw(NativeBattleGfGeometryBatch Packets, short Radius, ImmutableArray<byte> Vertices);

public static class NativeBattleFlare124ActorPackets
{
    public static NativeBattleModuleProjectedVertex Ripple(NativeBattleModuleProjectedVertex vertex, short centerX, short centerY, short radius, int step, int amplitude, int phase, NativeBattleFlare124Resources resources, out short extent)
    {
        if (vertex.Depth == 0)
        {
            extent = 0;
            return vertex with { Flags = 16 };
        }
        short x = unchecked((short)vertex.Screen);
        short y = unchecked((short)(vertex.Screen >> 16));
        x >>= 3;
        y >>= 3;
        int dx = unchecked((short)(x - centerX));
        int dy = unchecked((short)(y - centerY));
        int ax = Math.Abs(dx);
        int ay = Math.Abs(dy);
        // The native signed WORD comparisons intentionally retain the -32768 overflow case.
        ax = unchecked((short)ax) >= 128 ? 128 : ax;
        ay = unchecked((short)ay) >= 128 ? 128 : ay;
        extent = unchecked((short)Math.Max(ax, ay));
        int distance = resources.Distance(ax, ay);
        if (distance != 0 && distance < radius)
        {
            int weight = resources.Weight(step, (distance << 7) / radius);
            int wobble = unchecked(resources.Sine(phase + (dy << 6)) * unchecked((short)(amplitude >> 2))) >> 12;
            wobble = unchecked(wobble * radius) >> 14;
            x = unchecked((short)((unchecked(dx * weight) >> 12) + centerX + wobble));
            y = unchecked((short)((unchecked(dy * weight) >> 12) + centerY));
        }
        uint flags = x < 0 ? 1u : x >= 320 ? 2u : 0u;
        flags |= y < 0 ? 4u : y >= 216 ? 8u : 0u;
        return new(unchecked((ushort)(x << 3)) | unchecked((uint)(y << 3) << 16), vertex.Depth, flags);
    }

    public static NativeBattleFlare124ActorDraw Emit(NativeBattleRawMesh mesh, IReadOnlyList<NativeBattleActionMatrix> cameraBones, NativeBattleCameraProjection projection, uint color, short centerX, short centerY, short radius, int step, int amplitude, int phase, NativeBattleFlare124Resources resources, short zsf3, short zsf4, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        mesh.Validate();
        NativeBattleModuleProjectedVertex[] vertices = new NativeBattleModuleProjectedVertex[mesh.Vertices.Length];
        byte[] work = new byte[checked(vertices.Length * 8)];
        short maximum = 0;
        for (int index = 0; index < vertices.Length; index++)
        {
            NativeBattleRawVertex source = mesh.Vertices[index];
            NativeBattleModuleProjectedVertex projected = NativeBattleModuleProjection.Project45FE10(cameraBones[source.Bone], source.Position, projection);
            vertices[index] = Ripple(projected, centerX, centerY, radius, step, amplitude, phase, resources, out short extent);
            maximum = Math.Max(maximum, extent);
            BinaryPrimitives.WriteUInt32LittleEndian(work.AsSpan(index * 8), vertices[index].Screen);
            BinaryPrimitives.WriteUInt16LittleEndian(work.AsSpan(index * 8 + 4), vertices[index].Depth);
            BinaryPrimitives.WriteUInt16LittleEndian(work.AsSpan(index * 8 + 6), unchecked((ushort)vertices[index].Flags));
        }
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocation = 0;
        IEnumerable<int> faceIndices = Enumerable.Range(0, mesh.Polygons.Length).Where(index => !mesh.Polygons[index].Quad && mesh.Polygons[index].Bytes.Length == 16).Concat(Enumerable.Range(0, mesh.Polygons.Length).Where(index => mesh.Polygons[index].Quad && mesh.Polygons[index].Bytes.Length == 20));
        foreach (int faceIndex in faceIndices)
        {
            NativeBattleRawPolygon face = mesh.Polygons[faceIndex];
            int count = face.Quad ? 4 : 3;
            NativeBattleModuleProjectedVertex[] corners = new NativeBattleModuleProjectedVertex[count];
            uint union = 0;
            uint intersection = 255;
            for (int corner = 0; corner < count; corner++)
            {
                int index = BinaryPrimitives.ReadUInt16LittleEndian(face.Bytes.AsSpan().Slice(corner * 2)) & 4095;
                corners[corner] = vertices[index];
                union |= corners[corner].Flags;
                intersection &= corners[corner].Flags;
            }
            if (union >= 16 || intersection != 0 || NativeBattleModulePackets.Nclip(corners[0].Screen, corners[1].Screen, corners[2].Screen) < 0)
            {
                continue;
            }
            int length = face.Quad ? 40 : 32;
            SkinWrites packet = new(length, allocation);
            packet.Word(0, face.Quad ? 0x09000000u : 0x07000000u);
            packet.Word(4, color | (face.Quad ? 0x2C000000u : 0x24000000u) | ((uint)(face.Bytes[15] & 2) << 24));
            packet.Word(8, corners[0].Screen);
            packet.Word(12, BinaryPrimitives.ReadUInt32LittleEndian(face.Bytes.AsSpan().Slice(8)));
            packet.Word(16, corners[1].Screen);
            packet.Word(20, BinaryPrimitives.ReadUInt32LittleEndian(face.Bytes.AsSpan().Slice(12)) & 0xFDFFFFFF);
            packet.Word(24, corners[2].Screen);
            packet.Word16(28, BinaryPrimitives.ReadUInt16LittleEndian(face.Bytes.AsSpan().Slice(face.Quad ? 16 : 6)));
            if (face.Quad)
            {
                packet.Word(32, corners[3].Screen);
                packet.Word16(36, BinaryPrimitives.ReadUInt16LittleEndian(face.Bytes.AsSpan().Slice(18)));
            }
            uint depth = unchecked((uint)(corners.Sum(vertex => (int)vertex.Depth) * (face.Quad ? zsf4 : zsf3))) >> 12;
            ImmutableArray<int> depths = corners.Select(vertex => (int)vertex.Depth).ToImmutableArray();
            NativeBattleGeometryPrimitive primitive = new(checked(17 + (int)depth), packet.Words(), face.Quad ? depths : depths.Add(0));
            if (pilotIdentity is { } identity)
            {
                System.Numerics.Vector3 Screen(int corner) => new(unchecked((short)corners[corner].Screen), unchecked((short)(corners[corner].Screen >> 16)), corners[corner].Depth);
                primitive = primitive with { PilotGeometry = new(identity with { Primitive = unchecked(identity.Primitive * 65537 + faceIndex) }, Screen(0), Screen(1), Screen(2), face.Quad ? Screen(3) : default, (byte)count) { ScreenSpace = true } };
            }
            attempts.Add(packet.Attempt(primitive));
            allocation = checked(allocation + length);
        }
        return new(new(attempts.ToImmutable(), allocation), maximum, work.ToImmutableArray());
    }
}
