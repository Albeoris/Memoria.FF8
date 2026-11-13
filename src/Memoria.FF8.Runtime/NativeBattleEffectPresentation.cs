using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>A host actor exposes the native services it can actually represent. Missing services reject the selected presentation.</summary>
public sealed record NativeBattleForeignActor(int Index, NativeBattleModelTransform Transform, NativeBattleModuleAnchor Anchor, Func<byte, short, NativeBattleVector> ReadBone)
{
    public short Facing { get; init; }
    public int BoneCount { get; init; }
    public NativeBattleForeignModelGeometry? Geometry { get; init; }
    public Func<NativeBattleEffectActorGeometry>? ReadGeometry { get; init; }
    public NativeBattlePosedActor? Pose { get; init; }
}

public sealed record NativeBattleEffectPresentationInputs(BattleDefinition Resources, NativeBattleGraphicsModel Source, NativeBattleGraphicsResource SourceResource, NativeBattleActionPacketsState Packets, NativeBattleEffectParameters Parameters, ushort Effect, NativeBattleEffectTextureState Textures, NativeCrtRandomState Random, NativeBattleCameraProjection Projection, ImmutableArray<NativeBattleForeignActor> ForeignActors)
{
    public NativeBattleArenaHost? Arena { get; init; }
}

public sealed record NativeBattleEffectPresentationStep(ImmutableArray<NativeBattleContactPacket> Contacts, ImmutableArray<NativeBattleModuleSound> Sounds);

/// <summary>Runs the current native effect dispatcher without owning combat, inventory, rewards or host time.</summary>
public sealed class NativeBattleEffectPresentation
{
    private readonly BattleSession owner;

    public NativeBattleEffectPresentation(NativeBattleEffectPresentationInputs inputs)
    {
        owner = BattleSession.CreateEffectPresentation(inputs);
    }

    public NativeBattleGraphicsState Graphics => owner.NativeGraphics!;
    public NativeBattleEffectMemory Memory => owner.NativeEffectMemory!;
    public NativeCrtRandomState Random => owner.ReadEffectPresentationRandom();
    public NativeGlobalRandomState? GlobalRandom => owner.ReadEffectPresentationGlobalRandom();
    public NativeBattleGraphicsModel Source => Graphics.Models[0];
    public bool Complete => Memory.ReadReturnedTask5044B0() == 0 && Graphics.ModuleEffects.Head is null && Graphics.Auxiliary.Head is null && !owner.HasPendingEffectPresentationEvents;
    public NativeBattleStageColor StageColor => owner.ReadEffectPresentationStageColor();
    public short StageColorDepth => StageColor.Depth;
    public short ViewTranslationY => owner.NativeCamera?.ViewTranslationScratch?.Y ?? 0;
    public NativeBattleActionMatrix ForeignPlacement(int index) => owner.ReadEffectPresentationForeignPlacement(index);
    public ImmutableArray<NativeBattleArenaReplacement> ArenaReplacements => owner.presentationArena?.Replacements ?? [];

    public NativeBattleEffectPresentationStep Advance(NativeBattleGraphicsModel source, NativeCrtRandomState random, NativeBattleCameraProjection projection, ImmutableArray<NativeBattleForeignActor> foreignActors, NativeBattleEffectTextureState textures)
    {
        return owner.AdvanceEffectPresentation(source, random, projection, foreignActors, textures);
    }
}
