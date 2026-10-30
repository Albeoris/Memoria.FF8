using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleAcid317ActMeshStep(NativeBattleGfGeometryBatch Batch, NativeBattleEffectArena Temporary88, NativeBattleEffectArena Arena, NativeBattleGeometryRegisterStep Registers, NativeBattleModelRendererRegisters FarControls);

public static class NativeBattleAcid317ActMesh
{
    public static bool Visible(NativeBattleGfAnimationNode node) => NativeBattleBillboardActContext12.Visible(node);

    public static NativeBattleAcid317ActMeshStep Draw(NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleEffectArena context12, NativeBattlePackedMatrix32 camera, NativeBattlePackedMatrix32 normalizedView, NativeBattleEffectArena temporary88, NativeBattleEffectArena arena, NativeBattleGeometryRegisterStep registers, NativeBattleModelRendererRegisters farControls, NativeBattleCameraProjection projection, short dqa, int dqb, short? zsf3, short? zsf4, NativeBattleSummonColors constants, short? localVectorW = null, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        NativeBattleBillboardActContext12Step step = NativeBattleBillboardActContext12.Draw(source, node, context12, camera, normalizedView, temporary88, arena, registers, farControls, projection, dqa, dqb, zsf3, zsf4, constants, NativeBattleAcid317Profile.ArenaAddress + NativeBattleAcid317Profile.MorphOffset, NativeBattleAcid317Profile.MorphBytes, localVectorW, pilotIdentity: pilotIdentity);
        return new(PreserveNativeDepths(step.Batch), step.Temporary88, step.Arena, step.Registers, step.FarControls);
    }
    internal static NativeBattleGfGeometryBatch PreserveNativeDepths(NativeBattleGfGeometryBatch batch)
    {
        if (!batch.Attempts.Any(attempt => attempt.Primitive?.PilotGeometry is { Identity.Effect: 317, UseSourceDepths: false }))
        {
            return batch;
        }
        return batch with { Attempts = batch.Attempts.Select(attempt => attempt.Primitive is { PilotGeometry: { Identity.Effect: 317, UseSourceDepths: false } geometry } primitive && primitive.Words.Length > 2 ? attempt with { Primitive = primitive with { PilotGeometry = geometry with { SourceDepths = Payload(primitive), UseSourceDepths = true } } } : attempt).ToImmutableArray() };

        static System.Numerics.Vector4 Payload(NativeBattleGeometryPrimitive primitive)
        {
            return primitive.VertexPayload4.Length switch
            {
                0 => System.Numerics.Vector4.Zero,
                4 => new(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]),
                _ => throw new NotSupportedException("Effect317 capture requires its actual empty or four-lane native depth payload.")
            };
        }
    }

}
