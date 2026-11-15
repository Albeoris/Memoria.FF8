using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class CrossoverNativeRenzokukenProgram
{
    private CrossoverNativeRenzokukenProgramStep AdvanceOpening(CrossoverNativeRenzokukenProgramState source, CrossoverNativeCastState cast)
    {
        NativeBattleAuxiliaryState auxiliary = source.OpeningAuxiliary ?? throw new InvalidDataException("Renzokuken lost its original casting auxiliary list.");
        NativeBattleModuleState modules = source.OpeningModules ?? throw new InvalidDataException("Renzokuken lost its original casting module list.");
        NativeBattleRenzokuken160Frame frame = source.Effect.Frame;
        NativeBattleEffectTextureState textures = frame.Memory.Textures!;
        ImmutableArray<byte> motion = source.OpeningMotion;
        NativeBattlePoseSource pose = original.PoseSource!;
        auxiliary = auxiliary.Advance(new()
        {
            ReadModel = ReadModel,
            ApplyTexture = (_, selector, variant) => ApplyTexture(selector, variant),
            RestoreTextureBlink = actor => cast = cast with { Model = ReadModel(actor) with { Flags0 = (ushort)(cast.Model.Flags0 | 0x400) } }
        }).State;
        NativeBattleModuleStep shared = modules.Advance(new(0)
        {
            Resources = game.ReadNativeBattleModuleResources(),
            CrtRandom = frame.Random,
            ModelAliases = source.Effect.Aliases,
            ReadModelAnchor = actor => NativeBattlePoseMatrices.ReadAnchor(ReadModel(actor), pose),
            ReadModelTransform = actor => ReadModel(actor).Transform!,
            ReadModelBone = (actor, bone, fraction) => NativeBattlePoseMatrices.ReadBone(ReadModel(actor), pose, bone, fraction),
            TransformModelPosition = (actor, local) => NativeBattlePoseMatrices.TransformModulePosition570CC0(ReadModel(actor), local)
        });
        modules = shared.State;
        (CrossoverNativeCastState next, ImmutableArray<NativeBattleSequenceCue> cues) = CrossoverNativeCastPresentation.AdvanceModel(cast, original, new(0, 0)
        {
            ScheduleTextureAnimation = (program, sequence) => auxiliary = auxiliary.AppendTextureAnimation(source.Effect.SourceOwner, sequence, program, source.Effect.Aliases),
            ApplyTexture = ApplyTexture,
            ScheduleModelEffect = effect => modules = modules.AppendModelEffect(source.Effect.SourceOwner, effect, source.Effect.Aliases),
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
        if (cues.Any(cue => cue.Kind != NativeBattleSequenceCueKind.CommandTitle))
        {
            throw new NotSupportedException($"Renzokuken casting reached original cue {cues.First(cue => cue.Kind != NativeBattleSequenceCueKind.CommandTitle).Kind} without a source owner.");
        }
        if (textures.Count != 0)
        {
            textures = textures.Drain505D20(frame.Memory.Arena).State;
        }
        frame = frame with { Memory = frame.Memory with { Textures = textures }, Random = shared.CrtRandomAfter! };
        (CrossoverNativeRenzokukenTrigger ui, bool sound) = source.Trigger.AfterGraphics(source.Effect.Packets.Roster, rules.UiPublicationDuration ?? throw new InvalidDataException("Renzokuken lacks its original UI pulse period."));
        CrossoverNativeRenzokukenProgramState state = source with
        {
            Model = next.Model,
            Opening = next,
            OpeningAuxiliary = auxiliary,
            OpeningModules = modules,
            OpeningMotion = motion,
            OpeningDraws = shared.Draws.Where(draw => draw.Kind != NativeBattleModuleDrawKind.Sound).ToImmutableArray(),
            Trigger = ui,
            Effect = source.Effect with { Frame = frame }
        };
        return new(state, null, null, null, shared.Draws.Where(draw => draw.Kind == NativeBattleModuleDrawKind.Sound).Select(draw => new NativeBattleModuleSound(draw.Sound, draw.SoundFlags, draw.Anchor)).ToImmutableArray(), sound);

        NativeBattleGraphicsModel ReadModel(BattleModelOwner actor) => actor == source.Effect.SourceOwner ? cast.Model : throw new InvalidDataException("Renzokuken casting queried an unbound model.");

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
}
