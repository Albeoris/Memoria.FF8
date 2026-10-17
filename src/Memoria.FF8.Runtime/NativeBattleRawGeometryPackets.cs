using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleRawGeometryPackets
{
    public static ImmutableArray<NativeBattleGeometryPrimitive> Emit5099D0(ImmutableArray<NativeBattleRawMesh> meshes, IReadOnlyList<NativeBattleActionMatrix> matrices, NativeBattleCameraProjection projection, uint colorFlags28, uint objectMask32, short? colorDepth36, int tableBaseBucket, byte depthShift, NativeBattleModelTextureBinding? textureBinding, NativeBattleSummonColors? colorRules = null)
    {
        if (meshes.IsDefault)
        {
            throw new NotSupportedException("Native5099D0 requires its original immutable geometry owner.");
        }
        ImmutableArray<NativeBattleGeometryPrimitive>.Builder result = ImmutableArray.CreateBuilder<NativeBattleGeometryPrimitive>();
        for (int index = 0; index < meshes.Length; index++)
        {
            if ((objectMask32 & (1u << (index & 31))) == 0)
            {
                continue;
            }
            NativeBattleRawMesh mesh = meshes[index];
            mesh.Validate();
            NativeBattleProjectedVertex[] projected = mesh.Vertices.Select(vertex => NativeBattleGeometryProjection.Project50F900(vertex.Bone < matrices.Count ? matrices[vertex.Bone] : throw new InvalidDataException("Native5099D0 lacks the original selected bone matrix."), vertex.Position, projection, new(0, 0, 320, 216))).ToArray();
            result.AddRange(EmitProjected5099D0(mesh.Polygons, projected, colorFlags28, tableBaseBucket, depthShift, colorDepth36, textureBinding, colorRules));
        }
        return result.ToImmutable();
    }

    public static ImmutableArray<NativeBattleGeometryPrimitive> EmitProjected5099D0(ImmutableArray<NativeBattleRawPolygon> polygons, IReadOnlyList<NativeBattleProjectedVertex> projected, uint colorFlags28, int tableBaseBucket, byte depthShift, short? colorDepth36, NativeBattleModelTextureBinding? textureBinding = null, NativeBattleSummonColors? colorRules = null)
    {
        if (polygons.IsDefault)
        {
            throw new NotSupportedException("Native5099D0 requires its actual four-group polygon records.");
        }
        ImmutableArray<NativeBattleGeometryPrimitive>.Builder result = ImmutableArray.CreateBuilder<NativeBattleGeometryPrimitive>();
        foreach (NativeBattleRawPolygon polygon in polygons)
        {
            ImmutableArray<byte> source = textureBinding is null ? polygon.Bytes : Relocate507550(polygon, textureBinding);
            NativeBattleGeometryPrimitive? primitive = NativeBattleGeometryPackets.EmitRaw50FDF0(source.AsSpan(), projected, polygon.Quad, colorFlags28, tableBaseBucket, depthShift, colorDepth36, colorRules);
            if (primitive is not null)
            {
                result.Add(primitive);
            }
        }
        return result.ToImmutable();
    }

    public static ImmutableArray<byte> Relocate507550(NativeBattleRawPolygon polygon, NativeBattleModelTextureBinding binding)
    {
        int plainLength = polygon.Quad ? 20 : 16;
        if (polygon.Bytes.Length == plainLength + 4)
        {
            //507550 visits only original plain groups0/1; it never rewrites colored groups2/3.
            return polygon.Bytes;
        }
        if (polygon.Bytes.Length != plainLength || binding.PaletteRows.IsDefault || binding.PaletteRows.Length != 16)
        {
            throw new InvalidDataException("Native507550 requires its original plain polygon and16-row allocation map.");
        }
        byte[] bytes = polygon.Bytes.ToArray();
        ushort clut = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(10));
        int row = clut >> 6;
        if ((clut & 63) >= 16 || row is < 224 or >= 240)
        {
            return polygon.Bytes;
        }
        byte slot = binding.PaletteRows[row - 224] ?? throw new NotSupportedException("Native507550 polygon has no executed507400 palette relocation.");
        if (slot >= 15)
        {
            throw new InvalidDataException("Native507550 slot exceeds the successful original allocation range.");
        }
        int page = slot < 12 ? slot / 2 + 10 : slot - 2;
        int v = slot < 12 ? (slot & 1) << 7 : 128;
        ushort previousPage = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(14));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), (ushort)((clut & 0x803F) | (slot + 224) << 6));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), (ushort)((previousPage & 0xFFE0) | page));
        int[] positions = polygon.Quad ? [9, 13, 17, 19] : [7, 9, 13];
        foreach (int position in positions)
        {
            bytes[position] = (byte)((bytes[position] & 127) | v);
        }
        return bytes.ToImmutableArray();
    }
}
