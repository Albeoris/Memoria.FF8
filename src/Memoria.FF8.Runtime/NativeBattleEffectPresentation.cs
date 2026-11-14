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
    public NativeBattleGraphicsModel? NativeModel { get; init; }
    public NativeBattleGraphicsResource? NativeResource { get; init; }
    public bool CanReact { get; init; }
    public bool? AtStandingPose { get; init; }
    public uint? StatusFlags { get; init; }
    public bool IsParty { get; init; }
}

public sealed record NativeBattleEffectPresentationInputs(BattleDefinition Resources, NativeBattleGraphicsModel Source, NativeBattleGraphicsResource SourceResource, NativeBattleActionPacketsState Packets, NativeBattleEffectParameters Parameters, ushort Effect, NativeBattleEffectTextureState Textures, NativeCrtRandomState Random, NativeBattleCameraProjection Projection, ImmutableArray<NativeBattleForeignActor> ForeignActors)
{
    public NativeBattleArenaHost? Arena { get; init; }
    public NativeBattleEffectSceneContext? SceneContext { get; init; }
    public NativeBattleRibbonProjectionPolicy RibbonProjectionPolicy { get; init; } = NativeBattleRibbonProjectionPolicy.NativeCallerScratch;
    public NativeBattleEffectModelTransformPolicy ModelTransformPolicy { get; init; } = NativeBattleEffectModelTransformPolicy.NativeCallerScratch;
    public bool DeferInvocation { get; init; }
    public int? PartyActorCount { get; init; }
    public int? InitialMusicGain { get; init; }
}

public enum NativeBattleForeignSequenceKind
{
    Hit,
    Standing
}

public sealed record NativeBattleForeignSequenceRequest(int ActorIndex, NativeBattleForeignSequenceKind Kind);

public sealed record NativeBattleEffectPresentationStep(ImmutableArray<NativeBattleContactPacket> Contacts, ImmutableArray<NativeBattleModuleSound> Sounds)
{
    public ImmutableArray<NativeBattleForeignSequenceRequest> Sequences { get; init; } = [];
}

/// <summary>Runs the current native effect dispatcher without owning combat, inventory, rewards or host time.</summary>
public sealed class NativeBattleEffectPresentation : IDisposable
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
    public NativeBattleGraphicsResource SourceResource => owner.ReadEffectPresentationSourceResource();
    public bool Invoked => owner.EffectPresentationInvoked;
    public bool Complete => owner.EffectPresentationInvoked && Memory.ReadReturnedTask5044B0() == 0 && Graphics.ModuleEffects.Head is null && Graphics.Auxiliary.Head is null && !owner.HasPendingEffectPresentationEvents;
    public NativeBattleStageColor StageColor => owner.ReadEffectPresentationStageColor();
    public short StageColorDepth => StageColor.Depth;
    public short ViewTranslationY => owner.NativeCamera?.ViewTranslationScratch?.Y ?? 0;
    public NativeBattleActionMatrix ForeignPlacement(int index) => owner.ReadEffectPresentationForeignPlacement(index);
    public ImmutableArray<NativeBattleArenaReplacement> ArenaReplacements => owner.presentationArena?.Replacements ?? [];
    public NativeBattleUiServices Ui => Graphics.UiServices ?? throw new InvalidDataException("Native presentation lost its UI owner.");
    public NativeBattleStageState Stage => owner.NativeStage ?? throw new InvalidDataException("Native presentation lost its arena owner.");
    public int? MusicGain => owner.ReadEffectPresentationMusicGain();
    public ImmutableArray<NativeBattleArenaPrimitiveId> SuppressedArenaPrimitives => owner.presentationArena?.SuppressedPrimitives(Stage) ?? [];

    public void Dispose() => owner.ReleaseEffectPresentation();

    public void Invoke(NativeBattleGraphicsModel source, NativeCrtRandomState random, NativeBattleEffectTextureState textures) => owner.InvokeEffectPresentation(source, random, textures);
    public void ReloadSourceResources() => owner.ReloadEffectPresentationSourceResources();
    public NativeBattleEffectPresentationStep CompleteInvocation() => owner.CompleteEffectPresentationInvocation();
    public int ReadSourceVariable(NativeBattleGraphicsModel source, byte variable) => owner.ReadEffectPresentationSourceVariable(source, variable);
    public void WriteSourceVariable(byte variable, int value) => owner.WriteEffectPresentationSourceVariable(variable, value);
    public NativeBattleGraphicsModel WriteSourceTargetVariable(NativeBattleGraphicsModel source, byte variable, int value) => owner.WriteEffectPresentationSourceTargetVariable(source, variable, value);
    public ushort NextGlobalRandom() => owner.NextEffectPresentationGlobalRandom();

    public NativeBattleEffectPresentationStep Advance(NativeBattleGraphicsModel source, NativeCrtRandomState random, NativeBattleCameraProjection projection, ImmutableArray<NativeBattleForeignActor> foreignActors, NativeBattleEffectTextureState textures)
    {
        try
        {
            return owner.AdvanceEffectPresentation(source, random, projection, foreignActors, textures);
        }
        catch
        {
            Dispose();
            throw;
        }
    }
}
