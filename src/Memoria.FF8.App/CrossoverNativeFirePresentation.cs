using System.Collections.Immutable;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record CrossoverNativeFirePresentationState(CrossoverNativeCastState Cast, NativeBattleAuxiliaryState Auxiliary, NativeBattleModuleState Modules, NativeBattleEffectTextureState Textures, NativeBattleModelTextureBinding BodyTextures, ImmutableArray<byte> TextureAnimations, NativeCrtRandomState Random, int HostTarget, NativeBattleContactHalf Contact)
{
    public CrossoverNativeFireState? Fire { get; init; }
    public ImmutableArray<NativeBattleModuleDraw> Draws { get; init; } = [];
    public bool Complete => Cast.Complete && Auxiliary.Head is null && Modules.Head is null && Fire?.Complete == true;
}

internal sealed record CrossoverNativeFirePresentationStep(CrossoverNativeFirePresentationState State, bool Contact, ImmutableArray<NativeBattleModuleDraw> Sounds, ImmutableArray<NativeBattleFireSound> FireSounds);

/// <summary>Original casting source and Fire callbacks embedded in one host-owned presentation clock.</summary>
internal sealed class CrossoverNativeFirePresentation(GameAssets game, NativeBattleGraphicsResource resource)
{
    private readonly ImmutableArray<NativeBattleModuleResource> sprites = game.ReadNativeBattleModuleResources();

    public CrossoverNativeFirePresentationState Begin(NativeBattleGraphicsModel source, int hostTarget, NativeCrtRandomState random, NativeBattleContactHalf contact)
    {
        if (hostTarget is < 4 or > 9 || contact.ActorIndex != 3)
        {
            throw new InvalidDataException("Mixed Fire requires a physical host enemy occurrence.");
        }
        NativeBattleModelTextureLoad body = NativeBattleModelTextures.Prepare507400(resource.BodyTexturePack ?? throw new InvalidDataException("Mixed Fire requires Squall's original body textures."), 0);
        NativeBattleEffectTextureState textures = NativeBattleModelTextures.Enqueue507400(NativeBattleEffectTextureState.FromColdProcess(), body);
        NativeBattleEffectInitializerAsset common = game.ReadNativeCommonEffectTexture();
        (textures, _) = textures.EnqueueTim505E30(new(common.Name, common.Data));
        textures = textures.Drain505D20(NativeBattleEffectMemory.FromColdProcess().Arena).State;
        CrossoverNativeCastState cast = CrossoverNativeCastPresentation.Begin(source with { TextureAllocationMask = body.AllocatedSlots }, resource, game.ReadNativeBattleCommandSequences(), 2);
        return new(cast, NativeBattleAuxiliaryState.Clear(), NativeBattleModuleState.Clear(), textures, body.Binding, resource.TextureAnimations ?? throw new InvalidDataException("Mixed Fire requires the original source texture animation table."), random, hostTarget, contact);
    }

    public CrossoverNativeFirePresentationStep Advance(CrossoverNativeFirePresentationState source, Func<NativeBattleModuleAnchor> readTargetAnchor, Func<NativeBattleModelTransform> readTargetTransform)
    {
        if (source.Complete)
        {
            return new(source, false, [], []);
        }
        CrossoverNativeCastState cast = source.Cast;
        NativeBattleAuxiliaryState auxiliary = source.Auxiliary;
        NativeBattleModuleState modules = source.Modules;
        NativeBattleEffectTextureState textures = source.Textures;
        NativeCrtRandomState random = source.Random;
        ImmutableArray<byte> motion = source.TextureAnimations;
        CrossoverNativeFireState? fire = source.Fire;
        CrossoverNativeCastPoll poll = CrossoverNativeCastPresentation.Poll(cast, modules.PublishedCount != 0, fire?.Complete == true);
        cast = poll.State;
        if (poll.InvokeEffect)
        {
            if (fire is not null)
            {
                throw new InvalidDataException("Mixed Fire cannot invoke its effect descriptor twice.");
            }
            fire = CrossoverNativeFireProgram.Begin(game, source.HostTarget, random, source.Contact, textures);
            textures = fire.Textures;
        }
        auxiliary = auxiliary.Advance(new()
        {
            ReadModel = ReadSource,
            ApplyTexture = (actor, selector, variant) => ApplyTexture(selector, variant),
            RestoreTextureBlink = actor => cast = cast with { Model = ReadSource(actor) with { Flags0 = (ushort)(cast.Model.Flags0 | 0x400) } }
        }).State;
        NativeBattleModuleStep shared = modules.Advance(new(0)
        {
            Resources = sprites,
            CrtRandom = random,
            ReadModelAnchor = actor => NativeBattlePoseMatrices.ReadAnchor(ReadSource(actor), resource.PoseSource!),
            ReadModelTransform = actor => ReadSource(actor).Transform!,
            ReadModelBone = (actor, bone, fraction) => NativeBattlePoseMatrices.ReadBone(ReadSource(actor), resource.PoseSource!, bone, fraction),
            TransformModelPosition = (actor, local) => NativeBattlePoseMatrices.TransformModulePosition570CC0(ReadSource(actor), local)
        });
        modules = shared.State;
        random = shared.CrtRandomAfter!;
        bool contact = false;
        ImmutableArray<NativeBattleFireSound> fireSounds = [];
        ImmutableArray<NativeBattleModuleDraw> draws = shared.Draws.Where(draw => draw.Kind != NativeBattleModuleDrawKind.Sound).ToImmutableArray();
        if (fire is not null && !fire.Complete)
        {
            CrossoverNativeFireStep next = CrossoverNativeFireProgram.Advance(fire with { Random = random },
                actor => actor == 3 ? readTargetAnchor() : throw new InvalidDataException("Mixed Fire queried an unbound native target alias."),
                actor => actor == 3 ? readTargetTransform() : throw new InvalidDataException("Mixed Fire queried an unbound native target transform."), sprites);
            fire = next.State;
            random = fire.Random;
            contact = next.Contact;
            fireSounds = next.Sounds;
            draws = draws.AddRange(fire.Draws);
        }
        (cast, _) = CrossoverNativeCastPresentation.AdvanceModel(cast, resource, new(0, 0)
        {
            ScheduleTextureAnimation = (program, sequence) => auxiliary = auxiliary.AppendTextureAnimation(0, sequence, program),
            ApplyTexture = ApplyTexture,
            ScheduleModelEffect = effect => modules = modules.AppendModelEffect(0, effect),
            AdvanceTexture = (_, selector) =>
            {
                NativeBattleTextureMotionStep step = NativeBattleTextureMotion.Advance50C950(motion, cast.Model.TextureAllocationMask, selector, true);
                motion = step.Source;
                foreach (NativeBattleTextureCopy copy in step.Copies)
                {
                    Copy(copy);
                }
            }
        });
        textures = textures.Drain505D20(NativeBattleEffectMemory.FromColdProcess().Arena).State;
        return new(source with { Cast = cast, Auxiliary = auxiliary, Modules = modules, Textures = textures, TextureAnimations = motion, Random = random, Fire = fire, Draws = draws }, contact, shared.Draws.Where(draw => draw.Kind == NativeBattleModuleDrawKind.Sound).ToImmutableArray(), fireSounds);

        NativeBattleGraphicsModel ReadSource(int actor) => actor == 0 ? cast.Model : throw new InvalidDataException("The casting source queried an unbound physical model.");

        void ApplyTexture(byte selector, int variant)
        {
            if (NativeBattleTextureMotion.Plan50C780(motion, cast.Model.TextureAllocationMask, selector, variant) is { } copy)
            {
                Copy(copy);
            }
        }

        void Copy(NativeBattleTextureCopy copy)
        {
            textures = textures.EnqueueCopy505EB0(copy.SourceX, copy.SourceY, copy.Width, copy.Height, copy.DestinationX, copy.DestinationY);
        }
    }

    public static CrossoverNativeFirePresentationState Capture(CrossoverNativeFirePresentationState source) => source with { Textures = NativeBattleEffectComponentStorage.Capture(source.Textures), Fire = source.Fire is { } fire ? CrossoverNativeFireProgram.Capture(fire) : null };
    public static CrossoverNativeFirePresentationState Restore(CrossoverNativeFirePresentationState source) => source with { Textures = NativeBattleEffectComponentStorage.Restore(source.Textures), Fire = source.Fire is { } fire ? CrossoverNativeFireProgram.Restore(fire) : null };
}
