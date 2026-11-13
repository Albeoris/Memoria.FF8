using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleForeignModelDrawing
{
    /// <summary>Runs a world-space vertex/packet consumer on the host's posed meshes and preserves their material ownership.</summary>
    public static NativeBattleGfGeometryBatch DrawWorld(NativeBattleForeignModelGeometry geometry, Func<NativeBattleRawMesh, NativeBattleActionMatrix, NativeBattlePilotVisualIdentity, NativeBattleGfGeometryBatch> emit, NativeBattlePilotVisualIdentity identity)
    {
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int index = 0; index < geometry.Faces.Length; index++)
        {
            NativeBattleForeignModelFace face = geometry.Faces[index];
            NativeBattlePilotVisualIdentity selected = identity with { Primitive = unchecked(identity.Primitive * 397 + index) };
            NativeBattleGfGeometryBatch batch = emit(face.Triangle(), geometry.World, selected);
            foreach (NativeBattleGfGeometryAttempt attempt in batch.Attempts)
            {
                attempts.Add(attempt with { ByteOffsetRelative = checked(attempt.ByteOffsetRelative + allocated), Primitive = attempt.Primitive is { } primitive ? geometry.BindSurface(primitive, face, selected) : null });
            }
            allocated = checked(allocated + batch.AllocationBytes);
        }
        return new(attempts.ToImmutable(), allocated);
    }
}
