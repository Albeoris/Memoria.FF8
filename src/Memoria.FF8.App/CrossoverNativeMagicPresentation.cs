using System.Collections.Immutable;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record CrossoverNativeMagicPresentationState(CrossoverNativeCastState Cast, NativeBattleAuxiliaryState Auxiliary, NativeBattleModuleState Modules, NativeBattleEffectTextureState Textures, NativeBattleModelTextureBinding BodyTextures, ImmutableArray<byte> TextureAnimations, NativeCrtRandomState Random, int HostTarget, NativeBattleContactHalf Contact)
{
    public ushort Ability { get; init; } = 1;
    public byte Command { get; init; } = 2;
    public CrossoverNativeBlizzardState? Blizzard { get; init; }
    public CrossoverNativeThunderState? Thunder { get; init; }
    public CrossoverNativeRemedyState? Remedy { get; init; }
    public CrossoverNativePhoenixState? Phoenix { get; init; }
    public CrossoverNativeCureState? Cure { get; init; }
    public CrossoverNativeFireState? Fire { get; init; }
    public ImmutableArray<NativeBattleModuleDraw> Draws { get; init; } = [];
    public bool Complete => Cast.Complete && Auxiliary.Head is null && Modules.Head is null && EffectComplete;
    public bool EffectComplete => (Command, Ability) switch
    {
        (4, 7) => Phoenix?.Complete == true,
        (4, 16) => Remedy?.Complete == true,
        (4, 1) or (2, 21) => Cure?.Complete == true,
        (2, 1) => Fire?.Complete == true,
        (2, 4) => Blizzard?.Complete == true,
        (2, 7) => Thunder?.Complete == true,
        _ => throw new InvalidDataException("The source casting continuation has a foreign command selector.")
    };
}

internal sealed record CrossoverNativeMagicPresentationStep(CrossoverNativeMagicPresentationState State, bool Contact, ImmutableArray<NativeBattleModuleDraw> Sounds, ImmutableArray<NativeBattleFireSound> FireSounds)
{
    public ImmutableArray<NativeBattleModuleSound> EffectSounds { get; init; } = [];
}

/// <summary>Original casting source and admitted spell callbacks embedded in one host-owned presentation clock.</summary>
internal sealed class CrossoverNativeMagicPresentation(GameAssets game, NativeBattleGraphicsResource resource)
{
    private readonly ImmutableArray<NativeBattleModuleResource> sprites = game.ReadNativeBattleModuleResources();

    public CrossoverNativeMagicPresentationState Begin(NativeBattleGraphicsModel source, int hostTarget, NativeCrtRandomState random, NativeBattleContactHalf contact, ushort ability = 1, byte command = 2)
    {
        bool recovery = command == 4 && ability is 1 or 7 or 16 || command == 2 && ability == 21;
        bool elemental = command == 2 && ability is 1 or 4 or 7;
        if (!recovery && !elemental || recovery && (hostTarget is < 0 or > 2 || contact.ActorIndex is not (0 or 3)) || elemental && (hostTarget is < 4 or > 9 || contact.ActorIndex != 3))
        {
            throw new InvalidDataException("The original guest casting program requires its bound target occurrence.");
        }
        (source, NativeBattleEffectTextureState textures) = CrossoverNativeActorTextures.Allocate(source, resource);
        NativeBattleSummonColors colors = game.ReadNativeBattleGraphicsRules().SummonColors!;
        source = source with { Color40 = source.Color40 ?? colors.Transition, Color44 = source.Color44 ?? colors.Normal };
        NativeBattleEffectInitializerAsset common = game.ReadNativeCommonEffectTexture();
        (textures, _) = textures.EnqueueTim505E30(new(common.Name, common.Data));
        textures = textures.Drain505D20(NativeBattleEffectMemory.FromColdProcess().Arena).State;
        CrossoverNativeCastState cast = CrossoverNativeCastPresentation.Begin(source, resource, game.ReadNativeBattleCommandSequences(), command);
        return new(cast, NativeBattleAuxiliaryState.Clear(), NativeBattleModuleState.Clear(), textures, source.BodyTextureSlots!, resource.TextureAnimations ?? throw new InvalidDataException("Mixed Fire requires the original source texture animation table."), random, hostTarget, contact) { Ability = ability, Command = command };
    }

    public CrossoverNativeMagicPresentationStep Advance(CrossoverNativeMagicPresentationState source, Func<NativeBattleModuleAnchor> readTargetAnchor, Func<NativeBattleModelTransform> readTargetTransform, NativeBattleCameraProjection? projection = null, Func<CrossoverNativeCureForeignTarget>? readForeign = null)
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
        CrossoverNativeCureState? cure = source.Cure;
        CrossoverNativePhoenixState? phoenix = source.Phoenix;
        CrossoverNativeRemedyState? remedy = source.Remedy;
        CrossoverNativeThunderState? thunder = source.Thunder;
        CrossoverNativeBlizzardState? blizzard = source.Blizzard;
        CrossoverNativeCastPoll poll = CrossoverNativeCastPresentation.Poll(cast, modules.PublishedCount != 0, source.EffectComplete);
        cast = poll.State;
        if (poll.InvokeEffect)
        {
            if (fire is not null || cure is not null || phoenix is not null || remedy is not null || thunder is not null || blizzard is not null)
            {
                throw new InvalidDataException("Mixed Fire cannot invoke its effect descriptor twice.");
            }
            if (source.Command == 4 && source.Ability == 7)
            {
                phoenix = new CrossoverNativePhoenixProgram(game, resource).Begin(source.HostTarget, source.Contact, random, textures);
                textures = phoenix.Frame.Memory.Textures!;
            }
            else if (source.Command == 4 && source.Ability == 16)
            {
                remedy = new CrossoverNativeRemedyProgram(game, resource).Begin(source.Contact.ActorIndex == 3 ? source.HostTarget : null, source.Contact, random, textures);
                textures = remedy.Frame.Memory.Textures!;
            }
            else if (source.Command == 4 || source.Ability == 21)
            {
                cure = new CrossoverNativeCureProgram(game, resource).Begin(cast.Model, source.Contact, random, textures, source.Contact.ActorIndex == 3 ? source.HostTarget : null, source.Command == 4 ? (ushort)50 : (ushort)1);
            }
            else if (source.Ability == 1)
            {
                fire = CrossoverNativeFireProgram.Begin(game, source.HostTarget, random, source.Contact, textures);
                textures = fire.Textures;
            }
            else if (source.Ability == 4)
            {
                blizzard = new CrossoverNativeBlizzardProgram(game).Begin(source.Contact, random, textures);
                textures = blizzard.Frame.Memory.Textures!;
            }
            else if (source.Ability == 7)
            {
                thunder = CrossoverNativeThunderProgram.Begin(game, source.Contact, textures);
                textures = thunder.Textures;
            }
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
        ImmutableArray<NativeBattleModuleSound> effectSounds = [];
        if (thunder is not null && !thunder.Complete)
        {
            CrossoverNativeThunderStep next = CrossoverNativeThunderProgram.Advance(thunder, () =>
            {
                NativeBattleModuleAnchor anchor = readTargetAnchor();
                return new(anchor.X, anchor.Y, anchor.Z);
            }, readTargetTransform);
            thunder = next.State;
            contact = next.Contact;
            effectSounds = next.Sounds;
            draws = draws.AddRange(thunder.Draws);
        }
        if (blizzard is not null && !blizzard.Complete)
        {
            CrossoverNativeBlizzardStep next = new CrossoverNativeBlizzardProgram(game).Advance(blizzard with { Frame = blizzard.Frame with { Random = random } }, () =>
            {
                NativeBattleModuleAnchor anchor = readTargetAnchor();
                return new(anchor.X, anchor.Y, anchor.Z);
            }, readTargetTransform, projection ?? throw new InvalidDataException("Blizzard requires the host camera."));
            blizzard = next.State;
            random = blizzard.Frame.Random;
            textures = blizzard.Frame.Memory.Textures!;
            contact = next.Contact;
            effectSounds = next.Sounds;
        }
        if (phoenix is not null && !phoenix.Complete)
        {
            CrossoverNativePhoenixStep next = new CrossoverNativePhoenixProgram(game, resource).Advance(phoenix with { Random = random }, cast.Model, (readForeign ?? throw new InvalidDataException("Phoenix Down requires the bound host ally."))(), projection ?? throw new InvalidDataException("Phoenix Down requires the host camera."));
            phoenix = next.State;
            random = phoenix.Random;
            textures = phoenix.Frame.Memory.Textures!;
            contact = next.Contact;
            effectSounds = next.Sounds;
        }
        if (remedy is not null && !remedy.Complete)
        {
            CrossoverNativeCureForeignTarget? target = remedy.ForeignSlot.HasValue ? (readForeign ?? throw new InvalidDataException("Remedy requires the bound host ally."))() : null;
            CrossoverNativeRemedyStep next = new CrossoverNativeRemedyProgram(game, resource).Advance(remedy with { Random = random }, cast.Model, target, projection ?? throw new InvalidDataException("Remedy requires the host camera."));
            remedy = next.State;
            random = remedy.Random;
            textures = remedy.Frame.Memory.Textures!;
            contact = next.Contact;
            effectSounds = next.Sounds;
        }
        if (cure is not null && !cure.Complete)
        {
            CrossoverNativeCureStep next = new CrossoverNativeCureProgram(game, resource).Advance(cure with { Model = cast.Model, Random = random }, projection ?? throw new InvalidDataException("Cure requires the host camera."), cure.ForeignSlot.HasValue ? (readForeign ?? throw new InvalidDataException("Cure requires the bound host geometry."))() : null);
            cure = next.State;
            cast = cast with { Model = cure.Model };
            random = cure.Random;
            textures = cure.Frame.Memory.Textures!;
            contact = next.Contact;
            effectSounds = next.Sounds;
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
        return new(source with { Cast = cast, Auxiliary = auxiliary, Modules = modules, Textures = textures, TextureAnimations = motion, Random = random, Fire = fire, Cure = cure, Phoenix = phoenix, Remedy = remedy, Thunder = thunder, Blizzard = blizzard, Draws = draws }, contact, shared.Draws.Where(draw => draw.Kind == NativeBattleModuleDrawKind.Sound).ToImmutableArray(), fireSounds) { EffectSounds = effectSounds };

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

    public static CrossoverNativeMagicPresentationState Capture(CrossoverNativeMagicPresentationState source) => source with { Textures = NativeBattleEffectComponentStorage.Capture(source.Textures), Fire = source.Fire is { } fire ? CrossoverNativeFireProgram.Capture(fire) : null, Cure = source.Cure is { } cure ? CrossoverNativeCureProgram.Capture(cure) : null, Thunder = source.Thunder is { } thunder ? CrossoverNativeThunderProgram.Capture(thunder) : null, Blizzard = source.Blizzard is { } blizzard ? CrossoverNativeBlizzardProgram.Capture(blizzard) : null, Phoenix = source.Phoenix is { } phoenix ? CrossoverNativePhoenixProgram.Capture(phoenix) : null, Remedy = source.Remedy is { } remedy ? CrossoverNativeRemedyProgram.Capture(remedy) : null };
    public static CrossoverNativeMagicPresentationState Restore(CrossoverNativeMagicPresentationState source) => source with { Textures = NativeBattleEffectComponentStorage.Restore(source.Textures), Fire = source.Fire is { } fire ? CrossoverNativeFireProgram.Restore(fire) : null, Cure = source.Cure is { } cure ? CrossoverNativeCureProgram.Restore(cure) : null, Thunder = source.Thunder is { } thunder ? CrossoverNativeThunderProgram.Restore(thunder) : null, Blizzard = source.Blizzard is { } blizzard ? CrossoverNativeBlizzardProgram.Restore(blizzard) : null, Phoenix = source.Phoenix is { } phoenix ? CrossoverNativePhoenixProgram.Restore(phoenix) : null, Remedy = source.Remedy is { } remedy ? CrossoverNativeRemedyProgram.Restore(remedy) : null };
}
