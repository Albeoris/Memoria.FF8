using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleBadBreath113ActorDrawing
{
    public static NativeBattleGfGeometryBatch Emit(NativeBattleCureActorGeometry geometry, ImmutableArray<NativeBattleActionMatrix> matrices, NativeBattleCameraProjection projection, uint color, short originX, short amplitude, short phase, ImmutableArray<short> wave, Span<byte> projectedScratch, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        NativeBattleRawMesh mesh = geometry.Mesh;
        mesh.Validate();
        if (projectedScratch.Length != checked(mesh.Vertices.Length * 8))
        {
            throw new InvalidDataException("Native Bad Breath requires its actual selected vertex projection workspace.");
        }
        NativeBattleProjectedVertex[] projected = new NativeBattleProjectedVertex[mesh.Vertices.Length];
        for (int index = 0; index < mesh.Vertices.Length; index++)
        {
            NativeBattleRawVertex vertex = mesh.Vertices[index];
            if (vertex.Bone >= matrices.Length)
            {
                throw new InvalidDataException("Native Bad Breath reaches a vertex outside its actual actor skeleton.");
            }
            NativeBattleModuleDepthCueVertex point = NativeBattleModuleProjection.ProjectWithCue45FAA0(matrices[vertex.Bone], vertex.Position, projection, 0, 0);
            NativeBattleProjectedVertex retained = new(BinaryPrimitives.ReadUInt32LittleEndian(projectedScratch.Slice(index * 8)), BinaryPrimitives.ReadUInt32LittleEndian(projectedScratch.Slice(index * 8 + 4)));
            projected[index] = NativeBattleBadBreath113.Sway6CB7D0(point.Projected, point.Ir2, originX, amplitude, phase, wave, retained);
            BinaryPrimitives.WriteUInt32LittleEndian(projectedScratch.Slice(index * 8), projected[index].Screen);
            BinaryPrimitives.WriteUInt32LittleEndian(projectedScratch.Slice(index * 8 + 4), projected[index].DepthAndClip);
        }
        NativeBattleGfGeometryBatch batch = NativeBattleEffect107Geometry.Emit6BCB50(mesh.Polygons, projected, color, geometry.AlreadyRelocated ? null : geometry.Binding, pilotIdentity: pilotIdentity);
        if (geometry.MaterialOwner is { } material)
        {
            batch = batch with { Attempts = batch.Attempts.Select(attempt => attempt.Primitive is { } primitive ? attempt with { Primitive = primitive with { ModelTextureOwner = material } } : attempt).ToImmutableArray() };
        }
        return batch;
    }
}
