using System.Collections.Immutable;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record CrossoverNativeRenzokukenProgramState(CrossoverNativeRenzokukenEntryState Effect, CrossoverNativeRenzokukenTrigger Trigger, NativeBattleGraphicsModel Model, int ApproachDistance, int PublishedContacts, bool Returned, NativeBattleRenzokuken160SharedTask? FinisherHandoff)
{
    public CrossoverNativeCastState? Opening { get; init; }
    public NativeBattleAuxiliaryState? OpeningAuxiliary { get; init; }
    public NativeBattleModuleState? OpeningModules { get; init; }
    public ImmutableArray<byte> OpeningMotion { get; init; }
    public ImmutableArray<NativeBattleModuleDraw> OpeningDraws { get; init; } = [];
    public NativeBattleModuleState? SharedModules { get; init; }
    public byte FinisherReleasePhase { get; init; }
    public bool FinisherReady => FinisherHandoff is not null && FinisherReleasePhase == 2 && Effect.Frame.Effect.ReturnedDescriptor == 0 && SharedModules is { Head: null, PublishedCount: 0 } && Trigger.Widget is null && PreparedContact is null && TargetSequence is null;
    public NativeBattleTriggerReceipt? PreparedContact { get; init; }
    public byte? TargetSequence { get; init; }
    public bool Complete => FinisherHandoff is null && PreparedContact is null && TargetSequence is null && Returned && Effect.Frame.Effect.ReturnedDescriptor == 0 && Trigger.Widget is null && Trigger.Pending is null;
}

internal sealed record CrossoverNativeRenzokukenProgramStep(CrossoverNativeRenzokukenProgramState State, NativeBattleTriggerReceipt? Timing, NativeBattleTriggerReceipt? Contact, byte? TargetCue, ImmutableArray<NativeBattleModuleSound> Sounds, bool TriggerSound);

/// <summary>One original Squall model, effect list and widget under the host callback.</summary>
internal sealed partial class CrossoverNativeRenzokukenProgram(GameAssets game, NativeBattleGraphicsResource original, NativeBattleGraphicsRules rules)
{
    public CrossoverNativeRenzokukenProgramState Begin(NativeBattleGraphicsModel model, int hostTarget, ushort variant, bool finisherPending, int approachDistance, short yaw, NativeCrtRandomState random, ushort options, NativeBattleUiRandomState? uiRandom)
    {
        (model, NativeBattleEffectTextureState textures) = CrossoverNativeActorTextures.Allocate(model, original);
        NativeBattleEffectInitializerAsset common = game.ReadNativeCommonEffectTexture();
        (textures, _) = textures.EnqueueTim505E30(new(common.Name, common.Data));
        textures = textures.Drain505D20(NativeBattleEffectMemory.FromColdProcess().Arena).State;
        CrossoverNativeRenzokukenEntryState effect = CrossoverNativeRenzokukenEntry.Begin(game, hostTarget, variant, finisherPending, random, textures);
        model = model with { Rotation12 = 0, Rotation14 = yaw, Rotation16 = 0 };
        model = model with { GeometryWorldMatrix = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(model, original.PoseSource!) };
        CrossoverNativeCastState opening = CrossoverNativeCastPresentation.Begin(model, original, game.ReadNativeBattleCommandSequences(), 5);
        return new(effect, new(NativeBattleUiServices.Initialize(), options, uiRandom), opening.Model, approachDistance, 0, false, null) { Opening = opening, OpeningAuxiliary = NativeBattleAuxiliaryState.Clear(), OpeningModules = NativeBattleModuleState.Clear(), OpeningMotion = original.TextureAnimations ?? throw new InvalidDataException("Renzokuken lacks its original texture animation table.") };
    }

    public CrossoverNativeRenzokukenProgramStep Advance(CrossoverNativeRenzokukenProgramState source, NativeBattleCameraProjection projection, short roll)
    {
        if (source.Complete)
        {
            throw new InvalidOperationException("The guest Renzokuken source already completed.");
        }
        if (source.Opening is { } cast)
        {
            CrossoverNativeCastPoll opening = CrossoverNativeCastPresentation.Poll(cast, modulesBusy: source.OpeningModules?.PublishedCount != 0, effectComplete: false);
            if (!opening.InvokeEffect)
            {
                return AdvanceOpening(source, opening.State);
            }
            source = source with { Model = opening.State.Model, Opening = null, OpeningAuxiliary = null, OpeningModules = null, OpeningMotion = [], OpeningDraws = [] };
        }
        (CrossoverNativeRenzokukenTrigger trigger, NativeBattleTriggerReceipt? timingReceipt) = source.Trigger.BeforeGraphics();
        if (source.PreparedContact is not null && timingReceipt is not null)
        {
            throw new InvalidDataException("Renzokuken prepared a second timing before the prior B7 publication.");
        }
        NativeBattleTriggerReceipt? prepared = source.PreparedContact ?? timingReceipt;
        CrossoverNativeRenzokukenEntryState effect = source.Effect;
        NativeBattleGraphicsModel model = source.Model;
        NativeBattleGraphicsResource resource = model.ResourceMagicId is null ? original : BindReplacement(effect);
        NativeBattleSummonColors colors = rules.SummonColors ?? throw new InvalidDataException("Renzokuken requires its original stage color tables.");
        NativeBattleRenzokuken160SharedTask? handoff = source.FinisherHandoff;
        NativeBattleModuleState? shared = source.SharedModules;
        byte releasePhase = source.FinisherReleasePhase;
        if (handoff is not null && effect.Frame.Effect.ReturnedDescriptor == 0 && releasePhase < 2)
        {
            releasePhase++;
            if (releasePhase == 2)
            {
                model = model with { ResourceFlags44 = (ushort)(model.ResourceFlags44!.Value | 0x200) };
            }
        }
        NativeBattleLogicalOrdering? sharedOrdering = null;
        ImmutableArray<NativeBattleModuleDraw> moduleDraws = [];
        if (shared is { Head: not null })
        {
            (shared, NativeBattleRenzokuken160Frame frame, sharedOrdering, moduleDraws) = AdvanceShared(shared, effect.Frame, effect.Resources, model, resource, projection, colors);
            effect = effect with { Frame = frame, Ordering = sharedOrdering };
        }
        else if (effect.Frame.Effect.ReturnedDescriptor == 0)
        {
            effect = effect with { Ordering = null };
        }
        ImmutableArray<NativeBattleModuleSound>.Builder sounds = ImmutableArray.CreateBuilder<NativeBattleModuleSound>();
        sounds.AddRange(moduleDraws.Where(draw => draw.Kind == NativeBattleModuleDrawKind.Sound).Select(draw => new NativeBattleModuleSound(draw.Sound, draw.SoundFlags, draw.Anchor)));
        if (effect.Callbacks < 2)
        {
            CrossoverNativeRenzokukenPrelude prelude = CrossoverNativeRenzokukenEntry.AdvancePrelude(effect, model);
            effect = prelude.State;
            foreach (NativeBattleRenzokuken160Operation operation in prelude.Operations)
            {
                switch (operation)
                {
                    case NativeBattleRenzokuken160ActorFlags flags:
                        model = model with { Flags0 = flags.Flags };
                        break;
                    case NativeBattleRenzokuken160ActorResource binding:
                        (model, resource) = CrossoverNativeRenzokukenEntry.ReplaceSource(model, original, binding);
                        break;
                    case NativeBattleRenzokuken160Stage stage:
                        effect = effect with { StageDepth = checked((short)stage.Intensity) };
                        break;
                    case NativeBattleRenzokuken160ActorColor color:
                        model = model with { Color40 = color.Color };
                        break;
                    case NativeBattleRenzokuken160Sound sound:
                        sounds.Add(ToSound(sound));
                        break;
                    default:
                        throw new InvalidDataException("Renzokuken emitted a foreign prelude operation.");
                }
            }
        }
        else if (effect.Frame.Effect.ReturnedDescriptor != 0)
        {
            CrossoverNativeRenzokukenVisualStep step = CrossoverNativeRenzokukenEntry.AdvanceVisuals(effect, model, resource, trigger.Widget, projection, roll, colors, sharedOrdering);
            (effect, model) = (step.State, step.Model);
            sounds.AddRange(step.Sounds.Select(ToSound));
            foreach (NativeBattleRenzokuken160SharedTask task in step.SharedTasks)
            {
                if (handoff is not null)
                {
                    throw new InvalidDataException("Renzokuken published its original finisher handoff twice.");
                }
                handoff = task;
                shared = AppendShared(shared ?? NativeBattleModuleState.Clear(), task);
            }
        }
        NativeBattleEffectMemory memory = effect.Frame.Memory;
        byte? targetSequence = source.TargetSequence;
        byte? targetCue = null;
        NativeBattleTriggerReceipt? contact = null;
        NativeBattleSequenceStep pose = NativeBattleSequenceMachine.Advance(model, resource, new(0, 0)
        {
            OriginalResource = original,
            ReadExternal = variable => variable switch
            {
                17 => unchecked((short)source.ApproachDistance),
                18 or 19 => memory.ReadSequenceGlobal5044B0(variable),
                _ => throw new NotSupportedException($"Renzokuken has no owner for source variable {variable:X2}.")
            },
            WriteExternal = (variable, value) => memory = memory.WriteSequenceGlobal5048E0(variable, value),
            SelectModelSequence = SelectTarget,
            ApplyCurrentContact = PublishContact
        });
        bool returned = source.Returned || model.ResourceMagicId is not null && pose.Model.ResourceMagicId is null;
        foreach (NativeBattleSequenceCue cue in pose.Cues)
        {
            if (cue.Kind == NativeBattleSequenceCueKind.TriggerWindow)
            {
                trigger = trigger.Register(pose.Model, cue);
            }
            else if (cue.Kind != NativeBattleSequenceCueKind.RestoreStanding)
            {
                throw new NotSupportedException($"Renzokuken reached an unowned source cue {cue.Kind}.");
            }
        }
        model = pose.Model with { Flags2 = (ushort)(pose.Model.Flags2!.Value & ~4) };
        model = model with { GeometryWorldMatrix = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(model, (model.ResourceMagicId is null ? original : resource).PoseSource!) };
        effect = effect with { Frame = effect.Frame with { Memory = memory } };
        (trigger, bool triggerSound) = trigger.AfterGraphics(rules.UiPublicationDuration ?? throw new InvalidDataException("Renzokuken lacks its original UI pulse period."));
        return new(source with { Effect = effect, Model = model, Trigger = trigger, PublishedContacts = source.PublishedContacts + (contact is null ? 0 : 1), Returned = returned, FinisherHandoff = handoff, SharedModules = shared, FinisherReleasePhase = releasePhase, OpeningDraws = moduleDraws.Where(draw => draw.Kind != NativeBattleModuleDrawKind.Sound).ToImmutableArray(), PreparedContact = contact is null ? prepared : null, TargetSequence = targetSequence }, timingReceipt, contact, targetCue, sounds.ToImmutable(), triggerSound);

        NativeBattleGraphicsModel SelectTarget(NativeBattleGraphicsModel current, byte opcode, byte selector, byte sequence)
        {
            if (opcode != 0xAF || selector != 0 || sequence is not (4 or 6) || targetSequence is not null)
            {
                throw new NotSupportedException("Renzokuken requested an unsupported foreign target model operation.");
            }
            targetSequence = sequence;
            targetCue = sequence;
            return current;
        }

        NativeBattleGraphicsModel PublishContact(NativeBattleGraphicsModel current)
        {
            if (contact is not null || prepared is not { } timing || timing.ContactIndex != source.PublishedContacts + 1 || targetSequence != (timing.ContactIndex == timing.TotalContacts ? 6 : 4))
            {
                throw new InvalidDataException("Renzokuken B7 lacks its original ordered timing and foreign target occurrence.");
            }
            contact = timing;
            targetSequence = null;
            return current;
        }

        static NativeBattleModuleSound ToSound(NativeBattleRenzokuken160Sound sound) => new(sound.Descriptor, unchecked((uint)sound.First), null) { Pan = checked((byte)sound.Second), RawPan = unchecked((uint)sound.Second) };
    }

    public NativeBattleGraphicsResource BindReplacement(CrossoverNativeRenzokukenEntryState state) => state.Resources.ActorResource.BindGraphicsResource(original.PoseSource!, original.LinkedPoseSource);

    public static CrossoverNativeRenzokukenProgramState Capture(CrossoverNativeRenzokukenProgramState source) => source with { Effect = CrossoverNativeRenzokukenEntry.Capture(source.Effect) };
    public static CrossoverNativeRenzokukenProgramState Restore(CrossoverNativeRenzokukenProgramState source) => source with { Effect = CrossoverNativeRenzokukenEntry.Restore(source.Effect) };
}
