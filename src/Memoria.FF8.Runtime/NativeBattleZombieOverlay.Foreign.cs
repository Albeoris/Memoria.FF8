using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleZombieOverlay
{
    public static NativeBattleZombieOverlayDraw DrawForeign(NativeBattleForeignModelGeometry geometry, NativeBattleCameraProjection projection, uint color, NativeBattleFileStaging staging, uint workspace, ImmutableArray<short> wave, short originX, short phase, short amplitude, short dqa, int dqb, NativeBattlePilotVisualIdentity identity)
    {
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(projection.View ?? throw new NotSupportedException("Host body waves require the current camera."), geometry.World);
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int index = 0; index < geometry.Faces.Length; index++)
        {
            NativeBattleForeignModelFace face = geometry.Faces[index];
            NativeBattlePilotVisualIdentity selected = identity with { Primitive = unchecked(identity.Primitive * 397 + index) };
            NativeBattleZombieOverlayDraw drawn = Draw([face.Triangle()], [matrix], projection, color, 1, null, workspace, 24, staging, wave, originX, phase, amplitude, dqa, dqb, selected);
            staging = drawn.Staging;
            foreach (NativeBattleGfGeometryAttempt attempt in drawn.Packets.Attempts)
            {
                attempts.Add(attempt with { ByteOffsetRelative = checked(allocated + attempt.ByteOffsetRelative), Primitive = attempt.Primitive is { } primitive ? geometry.BindSurface(primitive, face, selected) : null });
            }
            allocated = checked(allocated + drawn.Packets.AllocationBytes);
        }
        return new(staging, new(attempts.ToImmutable(), allocated));
    }
}
