using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBreak117BodyResult(ImmutableArray<NativeBattleGeometryPrimitive> Primitives, byte DitherIndex, ImmutableArray<byte> Projected);

public static class NativeBattleBreak117Body
{
    public static int StoneLevel(int elapsed)
    {
        if ((uint)elapsed >= 60)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsed));
        }
        return elapsed < 9 ? (elapsed << 15) / 9 : elapsed >= 48 ? (0x1E0000 - (elapsed << 15)) / 12 : 32768;
    }

    public static NativeBattleBreak117BodyResult Draw(ImmutableArray<NativeBattleRawMesh> meshes, ImmutableArray<NativeBattleActionMatrix> bones, NativeBattleCameraProjection projection, uint color, uint mask, ImmutableArray<byte> dither, int level, NativeBattleModelTextureBinding? binding, byte ditherIndex, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        if (meshes.IsDefault || bones.IsDefault || dither.IsDefault || dither.Length != 512 || (uint)level > 32768)
        {
            throw new InvalidDataException("Native Break requires its complete posed model and256-word dither owners.");
        }
        ImmutableArray<NativeBattleGeometryPrimitive>.Builder result = ImmutableArray.CreateBuilder<NativeBattleGeometryPrimitive>();
        ImmutableArray<byte> lastProjected = [];
        for (int index = 0; index < meshes.Length; index++)
        {
            if ((mask & (1u << (index & 31))) == 0)
            {
                continue;
            }
            NativeBattleRawMesh mesh = meshes[index];
            mesh.Validate();
            NativeBattleProjectedVertex[] projected = mesh.Vertices.Select(vertex =>
            {
                if (vertex.Bone >= bones.Length)
                {
                    throw new InvalidDataException("Native Break actor vertex leaves its actual posed bone bank.");
                }
                NativeBattleModuleProjectedVertex p = NativeBattleModuleProjection.Project45FE10(bones[vertex.Bone], vertex.Position, projection);
                ushort depth = p.ReadDepth45E250();
                short x = unchecked((short)p.Screen);
                short y = unchecked((short)(p.Screen >> 16));
                int flags = unchecked((short)depth) <= 0 ? 16 : (x < 0 ? 1 : x >= 2560 ? 2 : 0) | (y < 0 ? 4 : y >= 1728 ? 8 : 0);
                return new NativeBattleProjectedVertex(p.Screen, depth | (uint)flags << 16);
            }).ToArray();
            byte[] bytes = new byte[projected.Length * 8];
            for (int vertex = 0; vertex < projected.Length; vertex++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(vertex * 8), projected[vertex].Screen);
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(vertex * 8 + 4), projected[vertex].DepthAndClip);
            }
            lastProjected = bytes.ToImmutableArray();
            for (int polygonIndex = 0; polygonIndex < mesh.Polygons.Length; polygonIndex++)
            {
                NativeBattleRawPolygon polygon = mesh.Polygons[polygonIndex];
                ImmutableArray<byte> source = binding is null ? polygon.Bytes : NativeBattleRawGeometryPackets.Relocate507550(polygon, binding);
                bool stone = BinaryPrimitives.ReadUInt16LittleEndian(dither.AsSpan().Slice(ditherIndex * 2)) <= level;
                ditherIndex = unchecked((byte)(ditherIndex + 1));
                NativeBattleGeometryPrimitive? packet = Polygon(source, polygon.Quad, projected, color, stone);
                if (packet is not null)
                {
                    if (pilotIdentity is { } identity)
                    {
                        System.Numerics.Vector3 World(int corner)
                        {
                            int selector = BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan().Slice(corner * 2)) & 4095;
                            NativeBattleRawVertex vertex = mesh.Vertices[selector];
                            return NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(bones[vertex.Bone], vertex.Position), projection);
                        }
                        packet = packet with { PilotGeometry = new(identity with { Primitive = unchecked((identity.Primitive * 257 + index) * 65537 + polygonIndex) }, World(0), World(1), World(2), polygon.Quad ? World(3) : default, (byte)(polygon.Quad ? 4 : 3)) { DepthQuantum = 4 } };
                    }
                    result.Add(packet);
                }
            }
        }
        return new(result.ToImmutable(), ditherIndex, lastProjected);
    }

    public static NativeBattleGeometryPrimitive? Polygon(ImmutableArray<byte> source, bool quad, IReadOnlyList<NativeBattleProjectedVertex> projected, uint color, bool stone)
    {
        int count = quad ? 4 : 3;
        if (source.IsDefault || source.Length != (quad ? 20 : 16))
        {
            throw new NotSupportedException("Native Break petrification requires the original plain textured actor polygon groups.");
        }
        NativeBattleProjectedVertex[] vertices = new NativeBattleProjectedVertex[count];
        int any = 0;
        int shared = 255;
        for (int index = 0; index < count; index++)
        {
            int vertex = U16(index * 2) & 4095;
            if (vertex >= projected.Count)
            {
                throw new InvalidDataException("Native Break stone polygon leaves its projected vertex bank.");
            }
            vertices[index] = projected[vertex];
            int flags = (int)(vertices[index].DepthAndClip >> 16) & 255;
            any |= flags;
            shared &= flags;
        }
        if (any >= 16 || shared != 0 || NativeBattleModulePackets.Nclip(vertices[0].Screen, vertices[1].Screen, vertices[2].Screen) < 0)
        {
            return null;
        }
        ushort[] uv = quad ? [U16(8), U16(12), U16(16), U16(18)] : [U16(8), U16(12), U16(6)];
        ushort clut = stone ? (ushort)0x3D54 : U16(10);
        ushort page = stone ? (ushort)0xB8 : (ushort)(U16(14) & 0xFDFF);
        SkinWrites writes = new(quad ? 40 : 32, 0);
        writes.Word(0, quad ? 0x9000000u : 0x7000000u);
        writes.Word(4, (quad ? 0x2C000000u : 0x24000000u) | color);
        writes.Word(8, vertices[0].Screen);
        writes.Word(12, uv[0] | (uint)clut << 16);
        writes.Word(16, vertices[1].Screen);
        writes.Word(20, uv[1] | (uint)page << 16);
        writes.Word(24, vertices[2].Screen);
        writes.Word16(28, uv[2]);
        if (quad)
        {
            writes.Word(32, vertices[3].Screen);
            writes.Word16(36, uv[3]);
        }
        uint average = unchecked((uint)vertices.Sum(vertex => (int)vertex.Depth) * (quad ? 256u : 341u)) >> 12;
        ImmutableArray<int> depth = vertices.Select(vertex => vertex.Depth * 4).ToImmutableArray();
        return new(checked(17 + (int)average), writes.Words(), quad ? depth : depth.Add(0)) { PhysicalWriteMask = writes.Attempt(null).WriteMask };

        ushort U16(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan().Slice(offset));
    }
}
