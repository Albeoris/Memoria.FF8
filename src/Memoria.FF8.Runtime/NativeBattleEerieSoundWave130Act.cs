namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEerieSoundWave130ActStep(NativeBattleEffectMemory Memory, NativeBattleEffectArena Temporary88, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleEerieSoundWave130Act
{
    public static bool Visible(NativeBattleGfAnimationNode node) => NativeBattleDefaultActContext40.Visible(node);

    public static NativeBattleEerieSoundWave130ActStep Draw(NativeBattleEffectMemory memory, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleEffectArena context40, NativeBattleEffectArena temporary88, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, uint morphAddress, int morphLength = 768, short? localVectorW = null, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        NativeBattleDefaultActContext40Step drawn = NativeBattleDefaultActContext40.DrawCore(memory, source, node, context40, temporary88, projection, colors, morphAddress, morphLength, NativeBattleActContext40Policy.EerieSoundWave130, localVectorW, pilotIdentity: pilotIdentity);
        return new(drawn.Memory, drawn.Temporary88, drawn.Batch);
    }
}
