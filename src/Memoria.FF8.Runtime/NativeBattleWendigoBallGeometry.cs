using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleWendigoBallGeometry
{
    public static NativeBattleVector Morph(NativeBattleRawVertex vertex, NativeBattleActionMatrix bone, NativeBattleActionMatrix rotation, NativeBattleVector pivot, short weight, NativeBattleVector radii)
    {
        NativeBattleVector position = NativeBattleGfMatrices.Transform460840(bone, vertex.Position);
        NativeBattleVector relative = new(unchecked((short)(position.X - pivot.X)), unchecked((short)(position.Y - pivot.Y)), unchecked((short)(position.Z - pivot.Z)));
        // 56BDE0 is the signed-WORD store variant of the same normalized-vector core.
        var normalized = NativeBattleActionGeometryRules.NormalizeVector56BC50(relative.X, relative.Y, relative.Z);
        NativeBattleVector sphere = new(unchecked((short)(unchecked(radii.X * unchecked((short)normalized.X)) / 4096)), unchecked((short)(unchecked(radii.Y * unchecked((short)normalized.Y)) / 4096)), unchecked((short)(unchecked(radii.Z * unchecked((short)normalized.Z)) / 4096)));
        short firstWeight = unchecked((short)(4096 - unchecked((ushort)weight)));
        NativeBattleVector mixed = new(Blend(relative.X, sphere.X), Blend(relative.Y, sphere.Y), Blend(relative.Z, sphere.Z));
        NativeBattleVector turned = NativeBattleGfMatrices.Transform460840(rotation with { X = 0, Y = 0, Z = 0 }, mixed);
        return new(unchecked((short)(turned.X + pivot.X)), unchecked((short)(turned.Y + pivot.Y)), unchecked((short)(turned.Z + pivot.Z)));

        short Blend(short first, short second)
        {
            int mac = unchecked(NativePrecision53.RoundInteger(first * (firstWeight / 4096d)) + NativePrecision53.RoundInteger(second * (weight / 4096d)));
            return (short)Math.Clamp(mac, short.MinValue, short.MaxValue);
        }
    }

    public static NativeBattleActionMatrix Rotation(NativeBattleActionMatrix basis, NativeBattleVector angles)
    {
        if (angles.X != 0)
        {
            basis = NativeBattleCuragaMatrices.RotateX8DD7E0(basis, angles.X);
        }
        if (angles.Z != 0)
        {
            basis = NativeBattleCuragaMatrices.RotateZ8DD960(basis, angles.Z);
        }
        if (angles.Y != 0)
        {
            basis = NativeBattleCuragaMatrices.RotateY8DD8A0(basis, angles.Y);
        }
        return basis;
    }

    public static NativeBattleActionMatrix Camera(NativeBattleActionMatrix view, NativeBattleActionMatrix world)
    {
        NativeBattleActionMatrix camera = NativeBattlePoseMatrices.MultiplyGteRotation45E470(view, world);
        NativeBattleFullMacVertex translation = NativeBattleGfMatrices.TransformMac460840(view, new(unchecked((short)world.X), unchecked((short)world.Y), unchecked((short)world.Z)));
        return camera with { X = translation.X, Y = translation.Y, Z = translation.Z };
    }

    public static NativeBattleGfGeometryBatch Packets(ImmutableArray<NativeBattleRawPolygon> polygons, ImmutableArray<NativeBattleModuleProjectedVertex> projected, uint color, IReadOnlyList<System.Numerics.Vector3>? pilotWorldVertices = null, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocation = 0;
        for (int polygonIndex = 0; polygonIndex < polygons.Length; polygonIndex++)
        {
            NativeBattleRawPolygon polygon = polygons[polygonIndex];
            if (polygon.Bytes.Length != (polygon.Quad ? 20 : 16))
            {
                continue;
            }
            int count = polygon.Quad ? 4 : 3;
            NativeBattleModuleProjectedVertex[] points = new NativeBattleModuleProjectedVertex[count];
            for (int corner = 0; corner < count; corner++)
            {
                int index = BinaryPrimitives.ReadUInt16LittleEndian(polygon.Bytes.AsSpan().Slice(corner * 2)) & 0xFFF;
                points[corner] = projected[index];
            }
            if (NativeBattleModulePackets.Nclip(points[0].Screen, points[1].Screen, points[2].Screen) <= 0)
            {
                continue;
            }
            int length = polygon.Quad ? 40 : 32;
            SkinWrites packet = new(length, allocation);
            uint page = BinaryPrimitives.ReadUInt32LittleEndian(polygon.Bytes.AsSpan().Slice(12));
            packet.Word(0, (uint)(length / 4 - 1) << 24);
            packet.Word(4, (color & 0xFFFFFF) | (polygon.Quad ? 0x2C000000u : 0x24000000u) | (page & 0x2000000));
            packet.Word(12, BinaryPrimitives.ReadUInt32LittleEndian(polygon.Bytes.AsSpan().Slice(8)));
            packet.Word(20, page);
            for (int corner = 0; corner < count; corner++)
            {
                packet.Word(8 + corner * 8, points[corner].Screen);
            }
            if (polygon.Quad)
            {
                packet.Word16(28, BinaryPrimitives.ReadUInt16LittleEndian(polygon.Bytes.AsSpan().Slice(16)));
                packet.Word16(36, BinaryPrimitives.ReadUInt16LittleEndian(polygon.Bytes.AsSpan().Slice(18)));
            }
            else
            {
                packet.Word16(28, BinaryPrimitives.ReadUInt16LittleEndian(polygon.Bytes.AsSpan().Slice(6)));
            }
            int sum = points.Sum(point => (int)point.Depth);
            int key = polygon.Quad ? sum >> 6 : (sum / 3) >> 4;
            ImmutableArray<int> depths = polygon.Quad ? points.Select(point => (int)point.Depth).ToImmutableArray() : [points[0].Depth, points[1].Depth, points[2].Depth, 0];
            NativeBattleGeometryPrimitive primitive = new(17 + key, packet.Words(), depths);
            if (pilotIdentity is { } identity && pilotWorldVertices is not null)
            {
                System.Numerics.Vector3 Corner(int corner) => pilotWorldVertices[BinaryPrimitives.ReadUInt16LittleEndian(polygon.Bytes.AsSpan().Slice(corner * 2)) & 0xFFF];
                primitive = primitive with { PilotGeometry = new(identity with { Primitive = unchecked(identity.Primitive * 65537 + polygonIndex) }, Corner(0), Corner(1), Corner(2), polygon.Quad ? Corner(3) : default, (byte)count) };
            }
            attempts.Add(packet.Attempt(primitive));
            allocation += length;
        }
        return new(attempts.ToImmutable(), allocation);
    }
}
