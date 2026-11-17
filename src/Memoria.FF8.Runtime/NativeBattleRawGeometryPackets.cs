using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleRawGeometryPackets
{
    public static ImmutableArray<NativeBattleGeometryPrimitive> Emit5099D0(ImmutableArray<NativeBattleRawMesh> meshes, IReadOnlyList<NativeBattleActionMatrix> matrices, NativeBattleCameraProjection projection, uint colorFlags28, uint objectMask32, short? colorDepth36, int tableBaseBucket, byte depthShift, NativeBattleModelTextureBinding? textureBinding, NativeBattleSummonColors? colorRules = null, Action<IReadOnlyList<NativeBattleProjectedVertex>>? publishProjectedScratch = null, NativeBattleGeometryViewport? viewport = null, NativeBattlePilotVisualIdentity? pilotIdentity = null, bool pilotScreenSpace = false, bool pilotContinuous = true, string? actorId = null, NativeBattleModelTextureOwner? material = null)
    {
        return EmitCore5099D0(meshes, matrices, projection, colorFlags28, objectMask32, colorDepth36, tableBaseBucket, depthShift, textureBinding, colorRules, publishProjectedScratch, viewport, pilotIdentity, pilotScreenSpace, pilotContinuous, actorId, material, null);
    }

    internal static NativeBattleModelPrimitiveBatch EmitModel5099D0(ImmutableArray<NativeBattleRawMesh> meshes, IReadOnlyList<NativeBattleActionMatrix> matrices, NativeBattleCameraProjection projection, uint colorFlags28, uint objectMask32, short? colorDepth36, int tableBaseBucket, byte depthShift, NativeBattleModelTextureBinding? textureBinding, NativeBattleSummonColors? colorRules = null, Action<IReadOnlyList<NativeBattleProjectedVertex>>? publishProjectedScratch = null, NativeBattleGeometryViewport? viewport = null, NativeBattlePilotVisualIdentity? pilotIdentity = null, bool pilotScreenSpace = false, bool pilotContinuous = true, string? actorId = null, NativeBattleModelTextureOwner? material = null)
    {
        NativeBattleModelPrimitiveBatch.Builder result = new();
        AppendModel5099D0(result, meshes, matrices, projection, colorFlags28, objectMask32, colorDepth36, tableBaseBucket, depthShift, textureBinding, colorRules, publishProjectedScratch, viewport, pilotIdentity, pilotScreenSpace, pilotContinuous, actorId, material);
        return result.Freeze();
    }

    internal static void AppendModel5099D0(NativeBattleModelPrimitiveBatch.Builder result, ImmutableArray<NativeBattleRawMesh> meshes, IReadOnlyList<NativeBattleActionMatrix> matrices, NativeBattleCameraProjection projection, uint colorFlags28, uint objectMask32, short? colorDepth36, int tableBaseBucket, byte depthShift, NativeBattleModelTextureBinding? textureBinding, NativeBattleSummonColors? colorRules = null, Action<IReadOnlyList<NativeBattleProjectedVertex>>? publishProjectedScratch = null, NativeBattleGeometryViewport? viewport = null, NativeBattlePilotVisualIdentity? pilotIdentity = null, bool pilotScreenSpace = false, bool pilotContinuous = true, string? actorId = null, NativeBattleModelTextureOwner? material = null)
    {
        EmitCore5099D0(meshes, matrices, projection, colorFlags28, objectMask32, colorDepth36, tableBaseBucket, depthShift, textureBinding, colorRules, publishProjectedScratch, viewport, pilotIdentity, pilotScreenSpace, pilotContinuous, actorId, material, result);
    }

    private static ImmutableArray<NativeBattleGeometryPrimitive> EmitCore5099D0(ImmutableArray<NativeBattleRawMesh> meshes, IReadOnlyList<NativeBattleActionMatrix> matrices, NativeBattleCameraProjection projection, uint colorFlags28, uint objectMask32, short? colorDepth36, int tableBaseBucket, byte depthShift, NativeBattleModelTextureBinding? textureBinding, NativeBattleSummonColors? colorRules = null, Action<IReadOnlyList<NativeBattleProjectedVertex>>? publishProjectedScratch = null, NativeBattleGeometryViewport? viewport = null, NativeBattlePilotVisualIdentity? pilotIdentity = null, bool pilotScreenSpace = false, bool pilotContinuous = true, string? actorId = null, NativeBattleModelTextureOwner? material = null, NativeBattleModelPrimitiveBatch.Builder? modelResult = null)
    {
        if (meshes.IsDefault)
        {
            throw new NotSupportedException("Native5099D0 requires its original immutable geometry owner.");
        }
        ImmutableArray<NativeBattleGeometryPrimitive>.Builder? result = modelResult is null ? ImmutableArray.CreateBuilder<NativeBattleGeometryPrimitive>() : null;
        for (int index = 0; index < meshes.Length; index++)
        {
            if ((objectMask32 & (1u << (index & 31))) == 0)
            {
                continue;
            }
            NativeBattleRawMesh mesh = meshes[index];
            mesh.Validate();
            BattleSceneScreenPoint[]? coordinates = null;
            NativeBattleProjectedVertex[]? scratch = null;
            NativeBattleProjectedVertex[] projected;
            if (viewport is { } expanded)
            {
                coordinates = new BattleSceneScreenPoint[mesh.Vertices.Length];
                projected = new NativeBattleProjectedVertex[mesh.Vertices.Length];
                if (publishProjectedScratch is not null)
                {
                    scratch = new NativeBattleProjectedVertex[mesh.Vertices.Length];
                }
                for (int vertexIndex = 0; vertexIndex < mesh.Vertices.Length; vertexIndex++)
                {
                    NativeBattleRawVertex vertex = mesh.Vertices[vertexIndex];
                    var unpacked = NativeBattleGeometryProjection.ProjectUnpacked50F900(vertex.Bone < matrices.Count ? matrices[vertex.Bone] : throw new InvalidDataException("Native5099D0 lacks the original selected bone matrix."), vertex.Position, projection, expanded);
                    coordinates[vertexIndex] = unpacked.Position;
                    projected[vertexIndex] = new(unchecked((ushort)unpacked.Position.X) | unchecked((uint)unpacked.Position.Y << 16), unpacked.DepthAndClip);
                    if (scratch is not null)
                    {
                        scratch[vertexIndex] = NativeBattleGeometryProjection.ReclipNativeScratch50F900(unpacked.Position, projected[vertexIndex]);
                    }
                }
            }
            else
            {
                projected = Project(new(0, 0, 320, 216));
            }
            publishProjectedScratch?.Invoke(scratch ?? projected);
            IReadOnlyList<System.Numerics.Vector3>? world = null;
            NativeBattlePilotVisualIdentity? meshIdentity = null;
            if (pilotIdentity is { } identity)
            {
                meshIdentity = identity with { ResourceGeneration = NativeBattlePilotVisualLifetimes.EpochFor(mesh), Primitive = unchecked(identity.Primitive * 257 + index) };
                if (!pilotScreenSpace)
                {
                    world = new CapturedWorldVertices(mesh.Vertices, matrices, projection);
                }
            }
            AppendProjected5099D0(result, mesh.Polygons, projected, colorFlags28, tableBaseBucket, depthShift, colorDepth36, textureBinding, colorRules, coordinates, world, meshIdentity, pilotScreenSpace, pilotContinuous, actorId, material, modelResult);

            NativeBattleProjectedVertex[] Project(NativeBattleGeometryViewport bounds)
            {
                return mesh.Vertices.Select(vertex => NativeBattleGeometryProjection.Project50F900(vertex.Bone < matrices.Count ? matrices[vertex.Bone] : throw new InvalidDataException("Native5099D0 lacks the original selected bone matrix."), vertex.Position, projection, bounds)).ToArray();
            }
        }
        return result?.ToImmutable() ?? [];
    }

    private sealed class CapturedWorldVertices(ImmutableArray<NativeBattleRawVertex> vertices, IReadOnlyList<NativeBattleActionMatrix> matrices, NativeBattleCameraProjection projection) : IReadOnlyList<System.Numerics.Vector3>
    {
        private readonly System.Numerics.Vector3?[] values = new System.Numerics.Vector3?[vertices.Length];
        public int Count => vertices.Length;
        public System.Numerics.Vector3 this[int index]
        {
            get
            {
                if (values[index] is not System.Numerics.Vector3 value)
                {
                    NativeBattleRawVertex vertex = vertices[index];
                    value = NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(matrices[vertex.Bone], vertex.Position), projection);
                    values[index] = value;
                }
                return value;
            }
        }
        public IEnumerator<System.Numerics.Vector3> GetEnumerator() => Enumerable.Range(0, Count).Select(index => this[index]).GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public static ImmutableArray<NativeBattleGeometryPrimitive> EmitProjected5099D0(ImmutableArray<NativeBattleRawPolygon> polygons, IReadOnlyList<NativeBattleProjectedVertex> projected, uint colorFlags28, int tableBaseBucket, byte depthShift, short? colorDepth36, NativeBattleModelTextureBinding? textureBinding = null, NativeBattleSummonColors? colorRules = null, IReadOnlyList<BattleSceneScreenPoint>? projectedCoordinates = null, IReadOnlyList<System.Numerics.Vector3>? pilotWorldVertices = null, NativeBattlePilotVisualIdentity? pilotIdentity = null, bool pilotScreenSpace = false, bool pilotContinuous = true)
    {
        ImmutableArray<NativeBattleGeometryPrimitive>.Builder result = ImmutableArray.CreateBuilder<NativeBattleGeometryPrimitive>();
        AppendProjected5099D0(result, polygons, projected, colorFlags28, tableBaseBucket, depthShift, colorDepth36, textureBinding, colorRules, projectedCoordinates, pilotWorldVertices, pilotIdentity, pilotScreenSpace, pilotContinuous);
        return result.ToImmutable();
    }

    private static void AppendProjected5099D0(ImmutableArray<NativeBattleGeometryPrimitive>.Builder? result, ImmutableArray<NativeBattleRawPolygon> polygons, IReadOnlyList<NativeBattleProjectedVertex> projected, uint colorFlags28, int tableBaseBucket, byte depthShift, short? colorDepth36, NativeBattleModelTextureBinding? textureBinding, NativeBattleSummonColors? colorRules, IReadOnlyList<BattleSceneScreenPoint>? projectedCoordinates, IReadOnlyList<System.Numerics.Vector3>? pilotWorldVertices, NativeBattlePilotVisualIdentity? pilotIdentity, bool pilotScreenSpace, bool pilotContinuous, string? actorId = null, NativeBattleModelTextureOwner? material = null, NativeBattleModelPrimitiveBatch.Builder? modelResult = null)
    {
        if (polygons.IsDefault)
        {
            throw new NotSupportedException("Native5099D0 requires its actual four-group polygon records.");
        }
        Span<byte> relocation = stackalloc byte[24];
        Span<uint> words = stackalloc uint[10];
        Span<int> depths = stackalloc int[4];
        Span<BattleSceneScreenPoint> coordinates = stackalloc BattleSceneScreenPoint[4];
        for (int polygonIndex = 0; polygonIndex < polygons.Length; polygonIndex++)
        {
            NativeBattleRawPolygon polygon = polygons[polygonIndex];
            scoped ReadOnlySpan<byte> source = polygon.Bytes.AsSpan();
            if (textureBinding is not null && TryRelocate507550(polygon, textureBinding, relocation))
            {
                source = relocation[..source.Length];
            }
            if (NativeBattleGeometryPackets.TryBuildRaw50FDF0(source, projected, polygon.Quad, colorFlags28, tableBaseBucket, depthShift, colorDepth36, colorRules, projectedCoordinates, words, depths, coordinates, out int bucket))
            {
                ReadOnlySpan<uint> packet = words[..(polygon.Quad ? 10 : 8)];
                ReadOnlySpan<BattleSceneScreenPoint> retainedCoordinates = projectedCoordinates is null ? default : coordinates[..(polygon.Quad ? 4 : 3)];
                NativeBattlePilotGeometryReference pilotGeometry = default;
                if (pilotIdentity is { } identity && (pilotWorldVertices is not null || pilotScreenSpace))
                {
                    System.Numerics.Vector3 Corner(int corner)
                    {
                        ushort selector = BinaryPrimitives.ReadUInt16LittleEndian(polygon.Bytes.AsSpan()[(corner * 2)..]);
                        int index = corner == 0 ? selector & 0xFFF : selector;
                        if (pilotScreenSpace)
                        {
                            NativeBattleProjectedVertex vertex = projected[index];
                            return new(unchecked((short)vertex.Screen), unchecked((short)(vertex.Screen >> 16)), vertex.Depth);
                        }
                        return pilotWorldVertices![index];
                    }
                    NativeBattlePilotVisualIdentity polygonIdentity = identity with { Primitive = unchecked(identity.Primitive * 65537 + polygonIndex) };
                    if (modelResult is not null)
                    {
                        pilotGeometry = modelResult.AddPilot(new(polygonIdentity, Corner(0), Corner(1), Corner(2), polygon.Quad ? Corner(3) : default, (byte)(polygon.Quad ? 4 : 3), pilotContinuous, pilotScreenSpace));
                    }
                    else
                    {
                        pilotGeometry = new NativeBattlePilotVisualGeometry(polygonIdentity, Corner(0), Corner(1), Corner(2), polygon.Quad ? Corner(3) : default, (byte)(polygon.Quad ? 4 : 3), pilotContinuous) { ScreenSpace = pilotScreenSpace };
                    }
                }
                if (modelResult is not null)
                {
                    modelResult.AddRaw(bucket, packet, depths, retainedCoordinates, NativeBattleGeometryPackets.RawPhysicalWriteMask(polygon.Quad), pilotGeometry, actorId, material);
                }
                else
                {
                    result!.Add(NativeBattleGeometryPackets.CaptureRawPacket(bucket, polygon.Quad, packet, depths, retainedCoordinates).ToPrimitive(pilotGeometry.ToGeometry(), actorId, material));
                }
            }
        }
    }

    public static ImmutableArray<byte> Relocate507550(NativeBattleRawPolygon polygon, NativeBattleModelTextureBinding binding)
    {
        Span<byte> relocated = stackalloc byte[24];
        return TryRelocate507550(polygon, binding, relocated) ? ImmutableArray.Create(relocated[..polygon.Bytes.Length]) : polygon.Bytes;
    }

    private static bool TryRelocate507550(NativeBattleRawPolygon polygon, NativeBattleModelTextureBinding binding, Span<byte> relocated)
    {
        int plainLength = polygon.Quad ? 20 : 16;
        if (polygon.Bytes.Length == plainLength + 4)
        {
            //507550 visits only original plain groups0/1; it never rewrites colored groups2/3.
            return false;
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
            return false;
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
            return false;
        }
        source.CopyTo(relocated);
        BinaryPrimitives.WriteUInt16LittleEndian(relocated[10..], nextClut);
        BinaryPrimitives.WriteUInt16LittleEndian(relocated[14..], nextPage);
        foreach (int position in positions)
        {
            relocated[position] = (byte)((relocated[position] & 127) | v);
        }
        return true;
    }
}
