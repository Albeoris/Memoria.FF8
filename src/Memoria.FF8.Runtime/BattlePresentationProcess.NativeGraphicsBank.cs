using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    private NativeBattleGraphicsState? nativeGraphics;

    public NativeBattleGraphicsState? NativeGraphics
    {
        get => nativeGraphics;
        private set
        {
            if (!ReferenceEquals(nativeGraphics?.OriginalEffect, value?.OriginalEffect))
            {
                AcceptNativeEffectPublication(value?.OriginalEffect, value?.PrimitiveFrame?.Pending);
            }
            nativeGraphics = value;
        }
    }

    private void AcceptNativeEffectPublication(NativeBattleOriginalEffectState? effect, NativeBattleLogicalOrdering? ordering = null)
    {
        if (effect is not null && NativeBattleTaskOperation.IsActive && !NativeBattleTaskCallerOperation.IsActive)
        {
            NativeBattleTaskOperation.AcceptPublishedEffect(effect, NativeEffectMemory, ordering);
        }
    }

    private void InitializeNativeGraphicsProcessOwners()
    {
        if (NativeCamera is not null)
        {
            NativeCamera = NativeCamera.Initialize500400();
        }
        NativeStage = NativeStage?.Reset506C50();
        if (NativeEffectMemory is { } effectMemory)
        {
            NativeFileStaging = NativeBattleTemporaryMemory.Reset5082F0(NativeFileStaging ?? NativeBattleFileStaging.Unknown());
            //500870 calls501160: DB8 is reset, while the pending DB4 field is retained.
            NativeEffectMemory = effectMemory.InitializeReturnedTask500870() with { ScriptedGraphics = effectMemory.ScriptedGraphics?.InitializeGeom56BAA0(), SoundPublishedFlags = 0, Textures = effectMemory.Textures?.Initialize500400(), SummonStream = (effectMemory.SummonStream ?? NativeBattleSummonStreamState.Unknown()).Reset501160() };
        }
        InitializeNativeMusic501C60();
    }

    private void InitializeNativeGraphicsModelOwners()
    {
        NativeModelTextureCache = NativeBattleModelTextureCache.Initialize508260(NativeModelTextureCache);
        NativeModelCpuResources = (NativeModelCpuResources ?? NativeBattleModelCpuResources.Unknown()).InvalidateResourceExtents() with { ScratchMaximum = 0, EnemyCursor = 0, EnemyLinkedSlots = 0 };
    }

    private static byte NativeStandingSequence(uint flags) => NativeBattleGraphicsModels.PlayerStandingSequence(flags);

    private NativeBattleGraphicsBinding? FindNativeGraphicsBinding(string actorId)
    {
        return resources.NativeGraphics?.Bindings.SingleOrDefault(binding => binding.ActorId == actorId);
    }
}
