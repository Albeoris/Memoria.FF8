using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBlizzagaModelBandResult(short Minimum, short Maximum, ImmutableArray<NativeBattleGfGeometryAttempt> VertexScratchWrites, ImmutableArray<NativeBattleGeometryPrimitive> Primitives);

/// <summary>Blizzaga's posed body model-band projection and explicit-depth FT3/FT4 packets.</summary>
public static class NativeBattleBlizzagaModelBand
{
    public static NativeBattleBlizzagaModelBandResult Emit6D8D80(ImmutableArray<NativeBattleRawMesh> meshes, IReadOnlyList<NativeBattleActionMatrix> matrices, NativeBattleCameraProjection projection, uint color40, uint meshMask124, short lowY, short highY, NativeBattleModelTextureBinding? textureBinding = null, short priorMinimum = 32767, short priorMaximum = -32767)
    {
        if (meshes.IsDefault)
        {
            throw new NotSupportedException("Native Blizzaga model-band drawing requires its actual current packed geometry.");
        }
        short minimum = priorMinimum;
        short maximum = priorMaximum;
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder scratch = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        ImmutableArray<NativeBattleGeometryPrimitive>.Builder primitives = ImmutableArray.CreateBuilder<NativeBattleGeometryPrimitive>();
        for (int meshIndex = 0; meshIndex < meshes.Length; meshIndex++)
        {
            if ((meshMask124 & (1u << (meshIndex & 31))) == 0)
            {
                continue;
            }
            NativeBattleRawMesh mesh = meshes[meshIndex];
            mesh.Validate();
            List<NativeBattleProjectedVertex> projected = [];
            foreach (NativeBattleRawVertex vertex in mesh.Vertices)
            {
                if (vertex.Bone >= matrices.Count)
                {
                    throw new InvalidDataException("Native Blizzaga model-band vertex has no current composed bone matrix.");
                }
                NativeBattleActionMatrix matrix = matrices[vertex.Bone];
                NativeBattleModuleProjectedVertex result = NativeBattleModuleProjection.Project45FE10(matrix, vertex.Position, projection);
                ushort flags;
                if (result.Depth == 0)
                {
                    flags = 0x10;
                }
                else
                {
                    short height = ReadIr2(matrix, vertex.Position);
                    if (height < minimum)
                    {
                        minimum = height;
                    }
                    else if (height > maximum)
                    {
                        maximum = height;
                    }
                    flags = height >= lowY && height <= highY ? (ushort)0x100 : (ushort)0;
                    short x = unchecked((short)result.Screen);
                    short y = unchecked((short)(result.Screen >> 16));
                    flags |= x < 0 ? (ushort)1 : x >= 320 * 8 ? (ushort)2 : (ushort)0;
                    flags |= y < 0 ? (ushort)4 : y >= 216 * 8 ? (ushort)8 : (ushort)0;
                }
                NativeBattleProjectedVertex stored = new(result.Screen, result.Depth | (uint)flags << 16);
                SkinWrites writes = new(8, projected.Count * 8);
                writes.Word(0, stored.Screen);
                writes.Word(4, stored.DepthAndClip);
                scratch.Add(writes.Attempt(null));
                projected.Add(stored);
            }
            foreach (NativeBattleRawPolygon polygon in mesh.Polygons)
            {
                ImmutableArray<byte> source = textureBinding is null ? polygon.Bytes : NativeBattleRawGeometryPackets.Relocate507550(polygon, textureBinding);
                NativeBattleGeometryPrimitive? primitive = EmitPolygon(source, polygon.Quad, projected, color40);
                if (primitive is not null)
                {
                    primitives.Add(primitive);
                }
            }
        }
        return new(minimum, maximum, scratch.ToImmutable(), primitives.ToImmutable());
    }

    public static NativeBattleGeometryPrimitive? EmitPolygon(ImmutableArray<byte> source, bool quad, IReadOnlyList<NativeBattleProjectedVertex> projected, uint color40)
    {
        if (source.IsDefault || source.Length != (quad ? 20 : 16))
        {
            throw new NotSupportedException("Native Blizzaga model-band drawing requires its actual plain FT3/FT4 model records.");
        }
        int count = quad ? 4 : 3;
        NativeBattleProjectedVertex[] corners = new NativeBattleProjectedVertex[count];
        int anyLow = 0;
        int sharedLow = 255;
        bool band = false;
        for (int index = 0; index < count; index++)
        {
            int selected = U16(index * 2) & 0xFFF;
            if (selected >= projected.Count)
            {
                throw new InvalidDataException("Native Blizzaga polygon has no actual projected vertex owner.");
            }
            NativeBattleProjectedVertex vertex = projected[selected];
            corners[index] = vertex;
            int flags = (int)(vertex.DepthAndClip >> 16);
            anyLow |= flags & 255;
            sharedLow &= flags & 255;
            band |= (flags & 0x100) != 0;
        }
        if (anyLow >= 0x10 || sharedLow != 0 || NativeBattleModulePackets.Nclip(corners[0].Screen, corners[1].Screen, corners[2].Screen) < 0)
        {
            return null;
        }
        ushort[] uv = quad ? [U16(8), U16(12), U16(16), U16(18)] : [U16(8), U16(12), U16(6)];
        ushort clut = U16(10);
        ushort page = (ushort)(U16(14) & 0xFDFF);
        if (band)
        {
            clut = 0x3D14;
            page = 0xBA;
            int aggregate = 0;
            foreach (ushort value in uv)
            {
                aggregate |= value & 255;
            }
            if (aggregate >= 64)
            {
                for (int index = 0; index < count; index++)
                {
                    byte u = unchecked((byte)((uv[index] & 255) - 64));
                    uv[index] = (ushort)((uv[index] & 0xFF00) | (u >= 64 ? 0 : u));
                }
            }
        }
        SkinWrites writes = new(quad ? 40 : 32, 0);
        writes.Word(0, quad ? 0x09000000u : 0x07000000u);
        writes.Word(4, (quad ? 0x2C000000u : 0x24000000u) | ((uint)U16(14) & 0x200) << 16 | color40);
        writes.Word(8, corners[0].Screen);
        writes.Word(12, uv[0] | (uint)clut << 16);
        writes.Word(16, corners[1].Screen);
        writes.Word(20, uv[1] | (uint)page << 16);
        writes.Word(24, corners[2].Screen);
        writes.Word16(28, uv[2]);
        if (quad)
        {
            writes.Word(32, corners[3].Screen);
            writes.Word16(36, uv[3]);
        }
        uint average = unchecked((uint)corners.Sum(vertex => (int)vertex.Depth) * (quad ? 0x100u : 0x155u)) >> 12;
        ImmutableArray<int> proxies = corners.Select(vertex => vertex.Depth * 4).ToImmutableArray();
        if (!quad)
        {
            proxies = proxies.Add(0);
        }
        return new(checked(17 + (int)average), writes.Words(), proxies) { PhysicalWriteMask = writes.Attempt(null).WriteMask };

        ushort U16(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan().Slice(offset, 2));
    }

    private static short ReadIr2(NativeBattleActionMatrix matrix, NativeBattleVector vertex)
    {
        int row = NativeBattlePoseMatrices.RoundNative((float)(matrix.Rotation[5] / 4096d) * (double)vertex.Z + (float)(matrix.Rotation[4] / 4096d) * (double)vertex.Y + (float)(matrix.Rotation[3] / 4096d) * (double)vertex.X);
        return (short)Math.Clamp(unchecked(row + matrix.Y), short.MinValue, short.MaxValue);
    }
}
