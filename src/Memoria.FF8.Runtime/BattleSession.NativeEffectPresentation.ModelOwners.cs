using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private string HostedPresentationExecutableHash => definition.NativeRetainedMemory?.ColdExecutableSha256 ?? NativeFieldRuntimeProfile.FF8EnExecutableHash;

    private void InitializeHostedPresentationModelOwners(BattleModelRoster roster, bool useRetainedModelOwners = false)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Hosted model resources require their effect memory owner.");
        NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Hosted model resources require their texture owner.");
        if (textures.PresentationTextures is null)
        {
            NativeBattlePcTextureWindowCache cache = memory.PrimitiveProcess?.TextureWindowCache ?? throw new NotSupportedException("Hosted model textures require their initialized process cache.");
            // Older presentations retain a single published image. Copy its pixels and provenance without replaying its upload queue.
            NativeBattleEffectTextureState leaf = textures with { PresentationTextures = null, Count = 0, Slots = NativeBattleEffectTextureState.FromColdProcess().Slots, Animation = null };
            foreach (BattleModelOwner actor in roster.Owners)
            {
                textures = textures.WithPresentationTexture(ReadGraphicsPresentationId(actor), new(leaf, NativeBattlePcTextureWindowCache.Reset4647A0(cache)));
            }
        }
        else
        {
            ValidateHostedPresentationTextureOwners(roster, textures);
        }
        NativeEffectMemory = memory with { Textures = textures };
        if (hostedBattlePresentation)
        {
            hostedModelRegistry = new(roster, ImmutableDictionary<BattleEnemyActorModel, HostedModelLoadAdmission>.Empty);
        }
        if (useRetainedModelOwners)
        {
            NativeModelCpuResources = (NativeModelCpuResources ?? throw new InvalidDataException("Hosted entry lacks its initialized model CPU owner.")).WithPresentationRoster(roster, nativeActionPackets.ModelAliases);
            NativeModelTextureCache = (NativeModelTextureCache ?? throw new InvalidDataException("Hosted entry lacks its initialized model texture cache.")).WithPresentationRoster(roster);
            return;
        }
        NativeModelCpuResources = (NativeBattleModelCpuResources.FromColdProcess(HostedPresentationExecutableHash) with { EnemyCursor = 0, ScratchMaximum = 0, EnemyLinkedSlots = 0 }).WithPresentationRoster(roster, nativeActionPackets.ModelAliases);
        NativeModelTextureCache = NativeBattleModelTextureCache.Initialize508260(NativeBattleModelTextureCache.FromColdProcess()).WithPresentationRoster(roster);
        NativeEffectIoResult = 0;
    }

    private NativeBattleEffectTextureState AcceptHostedPresentationTextures(NativeBattleEffectTextureState textures)
    {
        if (NativeModelTextureCache?.PresentationRoster is not { } roster)
        {
            return textures;
        }
        if (textures.PresentationTextures is null)
        {
            textures = textures with { PresentationTextures = NativeEffectMemory?.Textures?.PresentationTextures ?? throw new InvalidDataException("Hosted presentation lost its initialized model texture owners.") };
        }
        ValidateHostedPresentationTextureOwners(roster, textures);
        return textures;
    }

    private static void ValidateHostedPresentationTextureOwners(BattleModelRoster roster, NativeBattleEffectTextureState textures)
    {
        textures.Validate();
        foreach (BattleModelOwner actor in roster.Owners)
        {
            textures.RequirePresentationTexture(ReadGraphicsPresentationId(actor));
        }
    }

    private NativeBattleGraphicsResource? ReadOriginalPresentationModelResource(BattleModelOwner owner)
    {
        if (hostedBattlePresentation)
        {
            string actorId = RequireCanonicalGraphicsActorId(owner);
            return FindHostedAllocationBinding(hostedModelRegistry, owner)?.Resource ?? definition.NativeGraphics?.Bindings.SingleOrDefault(binding => binding.ActorId == actorId)?.Resource;
        }
        if (owner == presentationSourceOwner && presentationSourceResource is not null)
        {
            return presentationSourceResource;
        }
        if (presentationNativeTargets?.TryGetValue(owner, out NativeBattleEffectModelBinding? target) == true)
        {
            return target.Resource;
        }
        return presentationForeignActors?.GetValueOrDefault(owner)?.NativeResource ?? (owner is BattlePartyActorModel or BattleEnemyActorModel ? FindNativeGraphicsBinding(RequireCanonicalGraphicsActorId(owner))?.Resource : null);
    }

    private static NativeBattleGraphicsBinding? FindHostedAllocationBinding(HostedModelRegistry? registry, BattleModelOwner owner)
    {
        return owner is BattleEnemyActorModel enemy && registry?.Loads.TryGetValue(enemy, out HostedModelLoadAdmission? admission) == true ? admission.Binding : null;
    }

    private bool OwnsHostedEnemyPoseRow(BattleModelOwner owner, NativeBattleGraphicsModel model)
    {
        return model.ActiveResourceRow100 is int row && NativeModelTextureCache is { PresentationActors: { } owners } cache &&
            NativeModelCpuResources is { } cpu && (uint)row < (uint)cache.Rows.Length && (uint)row < (uint)cpu.Rows.Length &&
            cache.Rows[row] is { Kind: 3 } material && material.ModelId == model.ModelId4 &&
            owners.TryGetValue(row, out BattleModelOwner? actor) && actor == owner;
    }
}
