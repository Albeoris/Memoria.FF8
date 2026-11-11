using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record CrossoverGuestRenzokukenHostState(Ff7BattleGuestInvocation Invocation, long HostTick, long Subticks, bool HalfSubtick, CrossoverGunbladeHostPlacement Placement, CrossoverNativeRenzokukenPlan Plan, CrossoverNativeRenzokukenProgramState Program)
{
    public long? FinisherNumericTick { get; init; }
    public CrossoverNativeRoughDivideState? Rough { get; init; }
    public CrossoverNativeLimitCamera? Camera { get; init; }
}

internal sealed partial class CrossoverGuestHostActions
{
    private readonly NativeBattleGraphicsResource limitResource;
    private readonly CrossoverNativeRenzokukenProgram limitProgram;
    private readonly ushort? limitOptions;
    private readonly CrossoverNativeRoughDivideProgram roughDivideProgram;
    private NativeBattleUiRandomState? limitUiRandom;
    private CrossoverNativeRenzokukenSelection? limitSelection;
    private CrossoverGuestRenzokukenHostState? limit;

    public bool LimitReady => limitOptions is ushort options && ((options & 1) == 0 || limitUiRandom is not null) && limitSelection is { Crisis: > 0 } selected && SelectsSupported(selected);
    public int PublishedContacts => limit is { } current ? current.Program.Trigger.Consumed + (current.FinisherNumericTick is null ? 0 : 1) : 0;
    public SceneCameraPlacement? LeasedCamera => limit is { } current ? current.Camera?.ForHost(current.Placement) : null;
    public CrossoverNativeRenzokukenTrigger? RenzokukenTrigger => limit?.Program.Trigger;
    public bool SupportsLimit(Ff7BattleGuestAttackTarget target) => attack.Supports(target);

    public void ObserveLimitReady(long hostTick, Ff7BattleAiActor source)
    {
        if (limitOptions is null || limitSelection is not { } selection)
        {
            return;
        }
        if (source.Slot != ActorSlot)
        {
            throw new InvalidDataException("The guest Limit readiness used a foreign actor.");
        }
        int deadParty = host.Actors.Count(actor => actor.Actor.Slot < 3 && actor.Actor.HitPoints == 0);
        limitSelection = selection.ObserveReady(hostTick, source, deadParty, false, kit);
    }

    public void PressTrigger()
    {
        if (limit is { } current)
        {
            limit = current with { Program = current.Program with { Trigger = current.Program.Trigger.Press() } };
            return;
        }
        attack.PressTrigger();
    }

    private bool SelectsSupported(CrossoverNativeRenzokukenSelection selected)
    {
        _ = selected.Select(kit.FinisherFlags, out CrossoverNativeRenzokukenPlan plan);
        return plan.Finisher is null or 0;
    }

    private CrossoverFf8BattleActorVisual? LimitVisual
    {
        get
        {
            if (limit is not { } current || attack.Visual is not { } basis)
            {
                return null;
            }
            NativeBattleGraphicsModel model = current.Rough?.Model ?? current.Program.Model;
            NativeBattleEffectMemory memory = current.Rough?.Frame.Memory ?? current.Program.Effect.Frame.Memory;
            return basis with { Model = model, OriginToHost = current.Placement.Matrix, BodyTextures = model.BodyTextureSlots, Textures = memory.Textures };
        }
    }

    private CrossoverNativeEffectVisual? LimitEffectVisual
    {
        get
        {
            if (limit is not { Program.Effect: { } effect } current)
            {
                return null;
            }
            NativeBattleEffectMemory memory = current.Rough?.Frame.Memory ?? effect.Frame.Memory;
            short depth = current.Rough?.StageDepth ?? effect.StageDepth;
            return new(compositor, current.Rough is null ? current.Program.OpeningDraws : [], memory.Textures!, current.Placement.Matrix, CrossoverGunbladeHostPlacement.OriginToHostScale)
            {
                Ordering = current.Rough?.Ordering ?? effect.Ordering,
                Memory = memory,
                StageColor = depth == 0 ? null : new(graphicsRules.SummonColors!, depth)
            };
        }
    }

    private void BeginLimit(Ff7BattleGuestInvocation invocation, Ff7BattlePlacement source, Ff7BattleGuestAttackTarget target)
    {
        if (!LimitReady || limitOptions is not ushort options || Invocation is not null || invocation.RequestId != host.RequestId || invocation.FormationId != host.Formation.FormationId || invocation.ActorSlot != ActorSlot || invocation.TargetSlot != target.Slot || !SupportsLimit(target) || attack.Visual is not { } visual)
        {
            throw new InvalidDataException("The guest Renzokuken lacks its retained Crisis selection or loaded source occurrence.");
        }
        CrossoverNativeRenzokukenSelection next = limitSelection!.Select(kit.FinisherFlags, out CrossoverNativeRenzokukenPlan plan);
        if (plan.Finisher is not (null or 0))
        {
            throw new NotSupportedException("The guest Revolver has an unsupported native finisher index.");
        }
        CrossoverGunbladeHostPlacement placement = new(Ff7BattlePosition.FromVector(source.Position), source.Yaw);
        (_, short yaw, _, int distance) = placement.ResolveContact(target.Placement, target.CollisionRadius);
        CrossoverNativeRenzokukenProgramState program = limitProgram.Begin(visual.Model with { Index = 0 }, target.Slot, plan.Variant, plan.Finisher is not null, distance, yaw, random, options, limitUiRandom);
        NativeBattleGraphicsPresentationStep clock = NativeBattleGraphicsPresentationClock.Advance(0, false, TimeSpan.FromTicks(HostTime(invocation.StartedAt)));
        CrossoverNativeLimitCamera camera = CrossoverNativeLimitCamera.Capture(program.Model, limitResource.PoseSource!, placement, target.Placement.Position, host.GuestEffectTargetAnchor(target.Slot));
        limit = new(invocation, invocation.StartedAt, clock.Subticks, clock.HalfSubtick, placement, plan, program) { Camera = camera };
        limitSelection = next;
    }

    private Ff7BattleGuestAttackUpdate AdvanceLimit(CrossoverGuestRenzokukenHostState current, long hostTick)
    {
        if (hostTick != current.HostTick + 1)
        {
            throw new InvalidDataException("Guest Renzokuken advances once per native FFVII host tick.");
        }
        NativeBattleGraphicsPresentationStep clock = NativeBattleGraphicsPresentationClock.Advance(current.Subticks, current.HalfSubtick, TimeSpan.FromTicks(HostTime(hostTick) - HostTime(current.HostTick)));
        Ff7BattleGuestPhysicalHit? hit = null;
        int? presented = null;
        byte? targetCue = null;
        CrossoverNativeRenzokukenProgramState program = current.Program;
        if (clock.Callbacks != 0)
        {
            if (clock.Callbacks != 1)
            {
                throw new InvalidDataException("Guest Renzokuken received multiple source callbacks in one host tick.");
            }
            CrossoverNativeLimitCamera camera = current.Camera ?? throw new InvalidDataException("The active guest Limit has no source camera lease.");
            (NativeBattleCameraProjection projection, short roll) = camera.ForEffects(host.RenderCameraOutput, current.Placement);
            if (current.Rough is { } rough || program.FinisherReady && current.FinisherNumericTick is not null)
            {
                rough = current.Rough ?? BeginRough(current);
                CrossoverNativeRoughDivideStep next = roughDivideProgram.Advance(rough, limitProgram.BindReplacement(program.Effect), ReadForeignPosition, projection, roll);
                current = current with { Rough = next.State };
                random = next.State.Frame.Random;
                if (next.Contact)
                {
                    presented = program.Trigger.Consumed + 1;
                }
                targetCue = next.TargetCue;
                foreach (NativeBattleModuleSound sound in next.Sounds)
                {
                    QueueLimitSound(sound, projection);
                }
            }
            else
            {
                CrossoverNativeRenzokukenProgramStep next = limitProgram.Advance(program, projection, roll);
                program = next.State;
                limitUiRandom = program.Trigger.Random;
                random = program.Effect.Frame.Random;
                if (next.Timing is { } timing)
                {
                    hit = CrossoverNativeRenzokukenPlan.ProjectContact(kit, timing.Timing);
                }
                if (program.FinisherReleasePhase == 2 && current.FinisherNumericTick is null)
                {
                    if (hit is not null || current.Plan.Finisher != 0 || program.PreparedContact is not null)
                    {
                        throw new InvalidDataException("Rough Divide reached its native formula before the final ordinary contact was consumed.");
                    }
                    limitSelection = limitSelection!.ProjectRoughDivide(kit, RoughDivideAction(), out Ff7BattleGuestPhysicalHit finisher);
                    hit = finisher;
                    current = current with { FinisherNumericTick = hostTick };
                }
                presented = next.Contact?.ContactIndex;
                targetCue = next.TargetCue;
                foreach (NativeBattleModuleSound sound in next.Sounds)
                {
                    QueueLimitSound(sound, projection);
                }
                if (next.TriggerSound)
                {
                    NativeBattleGraphicsResource resource = program.Model.ResourceMagicId is null ? limitResource : limitProgram.BindReplacement(program.Effect);
                    NativeBattleVector point = NativeBattlePoseMatrices.ReadBone(program.Model, resource.PoseSource!, 241, 0);
                    NativeBattleModuleSound sound = NativeBattleTriggerAudio.Resolve501640(0, point, projection.ReadPan(point), graphicsRules.ActorSounds1120);
                    QueueSound(hostTick, CrossoverGuestAudioKind.Trigger, sound, current.Placement.Matrix);
                }
            }
            if (program.Opening is null && program.Effect.Callbacks > 0)
            {
                NativeBattleGraphicsModel model = current.Rough?.Model ?? program.Model;
                NativeBattleEffectTextureState textures = current.Rough?.Frame.Memory.Textures ?? program.Effect.Frame.Memory.Textures!;
                NativeBattleGraphicsResource poseResource = model.ResourceMagicId switch
                {
                    163 => roughDivideProgram.ReadResource(current.Rough!.Frame),
                    null => limitResource,
                    _ => limitProgram.BindReplacement(program.Effect)
                };
                if (textures.Animation is { } animation)
                {
                    camera = camera.Advance(animation, model, poseResource.PoseSource!, host.RenderCameraOutput, current.Placement);
                }
                current = current with { Camera = camera };
            }
        }
        bool complete = current.Rough?.Complete ?? program.Complete;
        if (complete)
        {
            NativeBattleGraphicsModel final = current.Rough?.Model ?? program.Model;
            attack.ResumeStandingFromCast(final with { Index = ActorSlot }, hostTick, clock.Subticks, clock.HalfSubtick);
            limit = null;
        }
        else
        {
            limit = current with { HostTick = hostTick, Subticks = clock.Subticks, HalfSubtick = clock.HalfSubtick, Program = program };
        }
        return new(hit, complete) { LimitPresentedContact = presented, LimitTargetCue = targetCue };

        void QueueLimitSound(NativeBattleModuleSound sound, NativeBattleCameraProjection projection)
        {
            if (sound.Pan is null && sound.Anchor is { } anchor)
            {
                sound = sound with { Pan = projection.ReadPan(new(anchor.X, anchor.Y, anchor.Z)) };
            }
            QueueSound(hostTick, CrossoverGuestAudioKind.Module, sound, current.Placement.Matrix);
        }

        NativeBattleModuleAnchor ReadForeignPosition()
        {
            if (!Matrix4x4.Invert(current.Placement.Matrix, out Matrix4x4 inverse))
            {
                throw new InvalidDataException("Rough Divide lost its origin-to-host placement.");
            }
            Vector3 anchor = Vector3.Transform(host.VisualPlacement(current.Invocation.TargetSlot).Position, inverse);
            return new(Word(anchor.X), Word(anchor.Y), Word(anchor.Z), 0);
        }
    }

    private BattleNativeAction RoughDivideAction() => assets.NativeActions.Single(action => action is { Command: 249, ActionId: 0, MagicId: 163 });

    private CrossoverNativeRoughDivideState BeginRough(CrossoverGuestRenzokukenHostState current)
    {
        if (host.LastHit is not { } hit || hit.Tick != current.FinisherNumericTick || hit.Source != ActorSlot || hit.Target != current.Invocation.TargetSlot)
        {
            throw new InvalidDataException("Rough Divide lost its original host numerical receipt.");
        }
        BattleNativeAction action = RoughDivideAction();
        byte flags = hit.Damage.Missed ? (byte)4 : hit.Damage.Critical ? (byte)2 : (byte)0;
        NativeBattleContactPresentation presentation = NativeBattleContactPresentations.Complete(action.DamagePresentation, 0, flags, (byte)(action.AttackFlags & 3), 255);
        ushort death = (ushort)(host.Actors.Single(actor => actor.Actor.Slot == hit.Target).Actor.Statuses & 1);
        NativeBattleContactHalf contact = NativeBattleContactHalf.FromScratch(3, presentation.Sequence, presentation.Flags2, presentation.HitFlags3, death, hit.Damage.Damage, 0);
        return roughDivideProgram.Begin(current.Program, contact);
    }

    private CrossoverGuestRenzokukenHostState? CaptureLimit() => limit is { } current ? current with { Program = CrossoverNativeRenzokukenProgram.Capture(current.Program), Rough = current.Rough is { } rough ? CrossoverNativeRoughDivideProgram.Capture(rough) : null } : null;

    private CrossoverGuestRenzokukenHostState? RestoreLimit(CrossoverGuestRenzokukenHostState? saved)
    {
        if (saved is null)
        {
            return null;
        }
        if (limitOptions != saved.Program.Trigger.Options || limitSelection is null || fire is not null || attack.Invocation is not null || saved.Invocation.GuestCommand != 5 || saved.Invocation.RequestId != host.RequestId || saved.Invocation.FormationId != host.Formation.FormationId || saved.Invocation.ActorSlot != ActorSlot || saved.Invocation.TargetSlot != saved.Program.Effect.HostTarget || saved.HostTick != host.Tick || saved.HostTick < saved.Invocation.StartedAt || saved.Plan.Finisher is not (null or 0) || saved.Plan.Variant != saved.Program.Effect.Frame.Effect.Binding.Effect || (saved.Rough?.Frame.Random ?? saved.Program.Effect.Frame.Random) != random || saved.Program.Trigger.Random != limitUiRandom || saved.FinisherNumericTick > saved.HostTick || saved.Plan.Finisher is null && (saved.FinisherNumericTick is not null || saved.Rough is not null) || saved.Rough is not null && (saved.FinisherNumericTick is null || !saved.Program.FinisherReady))
        {
            throw new InvalidDataException("The saved guest Renzokuken contradicts its native source and host queue occurrence.");
        }
        CrossoverNativeLimitCamera camera = saved.Camera ?? CrossoverNativeLimitCamera.Capture(saved.Program.Model, limitResource.PoseSource!, saved.Placement, host.VisualPlacement(saved.Invocation.TargetSlot).Position, host.GuestEffectTargetAnchor(saved.Invocation.TargetSlot));
        camera.Validate();
        return saved with { Camera = camera, Program = CrossoverNativeRenzokukenProgram.Restore(saved.Program), Rough = saved.Rough is { } rough ? CrossoverNativeRoughDivideProgram.Restore(rough) : null };
    }
}
