using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Memoria.FF8.Runtime;

public static class NativeBattleRawGeometryPackets
{
    public static ImmutableArray<NativeBattleGeometryPrimitive> Emit5099D0(ImmutableArray<NativeBattleRawMesh> meshes, IReadOnlyList<NativeBattleActionMatrix> matrices, NativeBattleCameraProjection projection, uint colorFlags28, uint objectMask32, short? colorDepth36, int tableBaseBucket, byte depthShift, NativeBattleModelTextureBinding? textureBinding, NativeBattleSummonColors? colorRules = null, Action<IReadOnlyList<NativeBattleProjectedVertex>>? publishProjectedScratch = null, NativeBattleGeometryViewport? viewport = null, NativeBattlePilotVisualIdentity? pilotIdentity = null)
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
            BattleSceneScreenPoint[]? coordinates = null;
            NativeBattleProjectedVertex[] projected;
            if (viewport is { } expanded)
            {
                var unpacked = mesh.Vertices.Select(vertex => NativeBattleGeometryProjection.ProjectUnpacked50F900(vertex.Bone < matrices.Count ? matrices[vertex.Bone] : throw new InvalidDataException("Native5099D0 lacks the original selected bone matrix."), vertex.Position, projection, expanded)).ToArray();
                coordinates = unpacked.Select(vertex => vertex.Position).ToArray();
                projected = unpacked.Select(vertex => new NativeBattleProjectedVertex(unchecked((ushort)vertex.Position.X) | unchecked((uint)vertex.Position.Y << 16), vertex.DepthAndClip)).ToArray();
            }
            else
            {
                projected = Project(new(0, 0, 320, 216));
            }
            if (publishProjectedScratch is not null)
            {
                // Retain native scratch flags while allowing presentation to submit a wider bounded canvas.
                publishProjectedScratch(viewport.HasValue ? Project(new(0, 0, 320, 216)) : projected);
            }
            System.Numerics.Vector3[]? world = null;
            NativeBattlePilotVisualIdentity? meshIdentity = null;
            if (pilotIdentity is { } identity)
            {
                meshIdentity = identity with { Generation = NativeBattlePilotVisualLifetimes.EpochFor(mesh), Primitive = unchecked(identity.Primitive * 257 + index) };
                world = mesh.Vertices.Select(vertex => NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(matrices[vertex.Bone], vertex.Position), projection)).ToArray();
            }
            result.AddRange(EmitProjected5099D0(mesh.Polygons, projected, colorFlags28, tableBaseBucket, depthShift, colorDepth36, textureBinding, colorRules, coordinates, world, meshIdentity));

            NativeBattleProjectedVertex[] Project(NativeBattleGeometryViewport bounds)
            {
                return mesh.Vertices.Select(vertex => NativeBattleGeometryProjection.Project50F900(vertex.Bone < matrices.Count ? matrices[vertex.Bone] : throw new InvalidDataException("Native5099D0 lacks the original selected bone matrix."), vertex.Position, projection, bounds)).ToArray();
            }
        }
        return result.ToImmutable();
    }

    public static ImmutableArray<NativeBattleGeometryPrimitive> EmitProjected5099D0(ImmutableArray<NativeBattleRawPolygon> polygons, IReadOnlyList<NativeBattleProjectedVertex> projected, uint colorFlags28, int tableBaseBucket, byte depthShift, short? colorDepth36, NativeBattleModelTextureBinding? textureBinding = null, NativeBattleSummonColors? colorRules = null, IReadOnlyList<BattleSceneScreenPoint>? projectedCoordinates = null, IReadOnlyList<System.Numerics.Vector3>? pilotWorldVertices = null, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        if (polygons.IsDefault)
        {
            throw new NotSupportedException("Native5099D0 requires its actual four-group polygon records.");
        }
        ImmutableArray<NativeBattleGeometryPrimitive>.Builder result = ImmutableArray.CreateBuilder<NativeBattleGeometryPrimitive>();
        for (int polygonIndex = 0; polygonIndex < polygons.Length; polygonIndex++)
        {
            NativeBattleRawPolygon polygon = polygons[polygonIndex];
            ImmutableArray<byte> source = textureBinding is null ? polygon.Bytes : Relocate507550(polygon, textureBinding);
            NativeBattleGeometryPrimitive? primitive = NativeBattleGeometryPackets.EmitRaw50FDF0(source.AsSpan(), projected, polygon.Quad, colorFlags28, tableBaseBucket, depthShift, colorDepth36, colorRules, projectedCoordinates);
            if (primitive is not null)
            {
                if (pilotIdentity is { } identity && pilotWorldVertices is not null)
                {
                    System.Numerics.Vector3 Corner(int corner)
                    {
                        ushort selector = BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan().Slice(corner * 2));
                        return pilotWorldVertices[corner == 0 ? selector & 0xFFF : selector];
                    }
                    primitive = primitive with { PilotGeometry = new(identity with { Primitive = unchecked(identity.Primitive * 65537 + polygonIndex) }, Corner(0), Corner(1), Corner(2), polygon.Quad ? Corner(3) : default, (byte)(polygon.Quad ? 4 : 3)) };
                }
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
        ReadOnlySpan<byte> source = polygon.Bytes.AsSpan();
        ushort clut = BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(10));
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
        ushort previousPage = BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(14));
        ushort nextClut = (ushort)((clut & 0x803F) | (slot + 224) << 6);
        ushort nextPage = (ushort)((previousPage & 0xFFE0) | page);
        ReadOnlySpan<int> positions = polygon.Quad ? [9, 13, 17, 19] : [7, 9, 13];
        bool changed = nextClut != clut || nextPage != previousPage;
        foreach (int position in positions)
        {
            changed |= source[position] != (byte)((source[position] & 127) | v);
        }
        if (!changed)
        {
            return polygon.Bytes;
        }
        byte[] bytes = source.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), nextClut);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), nextPage);
        foreach (int position in positions)
        {
            bytes[position] = (byte)((bytes[position] & 127) | v);
        }
        return ImmutableCollectionsMarshal.AsImmutableArray(bytes);
    }
}
