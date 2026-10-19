using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleBlastingZone164Packets
{
    public static NativeBattleGfGeometryBatch Emit5F2240(ImmutableArray<byte> mesh, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, NativeBattleModuleMeshContext context, uint tint, NativeBattleSummonColors colors, Func<int, NativeBattleVector>? readSelectedVertex = null)
    {
        NativeBattleMeshPacketContext operands = new(context.Flags, context.FarColor, context.Fade, context.TexturePage, context.Clut, context.UvOffset ?? 0, 0, context.TableBaseBucket, context.DepthShift);
        NativeBattleMeshPacketVariant variant = new(tint, tint, true, true);
        return NativeBattleEffectMeshPackets.EmitBatch(mesh, matrix, projection, context.Flags, readSelectedVertex, (group, face, project, offset) =>
        {
            ushort[] indices = (group & 1) == 0 ? [0, 1, 2] : [0, 1, 2, 3];
            NativeBattleGfGeometryPackets.ProjectedCorners vertices = new(indices, index => project(index));
            NativeBattleGfGeometryAttempt attempt = NativeBattleGfGeometryPackets.EmitAttempt(face, vertices, group, operands, colors, offset, variant: variant);
            return new([attempt], attempt.Primitive is null ? 0 : attempt.PacketLength);
        });
    }
}
