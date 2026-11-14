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
    public ushort FinisherTargets { get; init; }
    public CrossoverNativeLimitCamera? Camera { get; init; }
}

internal sealed partial class CrossoverGuestHostActions
{
    private readonly NativeBattleGraphicsResource limitResource;
    private readonly CrossoverNativeRenzokukenProgram limitProgram;
    private readonly ushort? limitOptions;
    private NativeBattleUiRandomState? limitUiRandom;
    private CrossoverNativeRenzokukenSelection? limitSelection;
    private CrossoverGuestRenzokukenHostState? limit;

    public bool LimitReady => kit.Limit.Command == 5 && limitOptions is ushort options && ((options & 1) == 0 || limitUiRandom is not null) && limitSelection is { Crisis: > 0 } selected && SelectsSupported(selected);
    public int PublishedContacts => limit?.Program.Trigger.Consumed ?? 0;
    public SceneCameraPlacement? LeasedCamera => limit is { } current ? current.Camera?.ForHost(current.Placement) : null;
    public CrossoverNativeRenzokukenTrigger? RenzokukenTrigger => limit?.Program.Trigger;
    public bool SupportsLimit(Ff7BattleGuestAttackTarget target) => LimitTargetUnsupportedReason(target) is null;

    public string? LimitTargetUnsupportedReason(Ff7BattleGuestAttackTarget target)
    {
        if (!attack.Supports(target) || limitSelection is not { Crisis: > 0 } selection)
        {
            return "The original Limit source or selected target is not ready.";
        }
        _ = selection.Select(kit.FinisherFlags, out CrossoverNativeRenzokukenPlan plan);
        return plan.Finisher is not byte finisher ? null : PresentationUnsupportedReason(SelectedFinisherAction(finisher), CrossoverGuestAbilityCatalog.Key(249, finisher), target.Slot);
    }

    public Action? PrepareLimitReady(long hostTick, Ff7BattleAiActor source, int deadParty)
    {
        if (limitSelection is not { } selection)
        {
            return null;
        }
        if (source.Slot != ActorSlot)
        {
            throw new InvalidDataException("The guest Limit readiness used a foreign actor.");
        }
        CrossoverNativeRenzokukenSelection next = selection.ObserveReady(hostTick, source, deadParty, false, kit);
        return () => limitSelection = next;
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
        return plan.Finisher is not byte finisher || CrossoverBattleAbilityRules.UnsupportedReason(SelectedFinisherAction(finisher)) is null;
    }

    private CrossoverFf8BattleActorVisual? LimitVisual
    {
        get
        {
            if (limit is not { } current || attack.Visual is not { } basis)
            {
                return null;
            }
            NativeBattleGraphicsModel model = current.Program.Model;
            NativeBattleEffectMemory memory = current.Program.Effect.Frame.Memory;
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
            NativeBattleEffectMemory memory = effect.Frame.Memory;
            short depth = effect.StageDepth;
            return new(compositor, current.Program.OpeningDraws, memory.Textures!, current.Placement.Matrix, CrossoverGunbladeHostPlacement.OriginToHostScale)
            {
                Ordering = effect.Ordering,
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
        CrossoverGunbladeHostPlacement placement = new(Ff7BattlePosition.FromVector(source.Position), source.Yaw);
        (_, short yaw, _, int distance) = placement.ResolveContact(target.Placement, target.CollisionRadius);
        CrossoverNativeRenzokukenProgramState program = limitProgram.Begin(visual.Model with { Index = 0 }, target.Slot, plan.Variant, plan.Finisher is not null, distance, yaw, encounter.CrtRandom, options, limitUiRandom);
        NativeBattleGraphicsPresentationStep clock = NativeBattleGraphicsPresentationClock.Advance(0, false, TimeSpan.FromTicks(HostTime(invocation.StartedAt)));
        CrossoverNativeLimitCamera camera = CrossoverNativeLimitCamera.Capture(program.Model, limitResource.PoseSource!, placement, target.Placement.Position, host.GuestEffectTargetAnchor(target.Slot));
        limit = new(invocation, invocation.StartedAt, clock.Subticks, clock.HalfSubtick, placement, plan, program) { Camera = camera, FinisherTargets = plan.Finisher is byte selected ? AbilityTargetMask(SelectedFinisherAction(selected), target.Slot) : (ushort)0 };
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
            CrossoverNativeRenzokukenProgramStep next = limitProgram.Advance(program, projection, roll);
            program = next.State;
            limitUiRandom = program.Trigger.Random;
            encounter.CrtRandom = program.Effect.Frame.Random;
            if (next.Timing is { } timing)
            {
                hit = CrossoverNativeRenzokukenPlan.ProjectContact(kit, timing.Timing);
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
            if (program.Opening is null && program.Effect.Callbacks > 0)
            {
                NativeBattleGraphicsModel model = program.Model;
                NativeBattleEffectTextureState textures = program.Effect.Frame.Memory.Textures!;
                NativeBattleGraphicsResource poseResource = model.ResourceMagicId is null ? limitResource : limitProgram.BindReplacement(program.Effect);
                if (textures.Animation is { } animation)
                {
                    camera = camera.Advance(animation, model, poseResource.PoseSource!, host.RenderCameraOutput, current.Placement);
                }
                current = current with { Camera = camera };
            }
        }
        if (program.FinisherReady)
        {
            BattleNativeAction action = SelectedFinisherAction(current.Plan.Finisher ?? throw new InvalidDataException("The native handoff has no selected child action."));
            ushort targets = current.FinisherTargets != 0 ? current.FinisherTargets : checked((ushort)(1 << current.Invocation.TargetSlot));
            selectedContinuation = new(action, targets, program, current.Placement);
            attack.ResumeStandingFromCast(program.Model with { Index = ActorSlot }, hostTick, clock.Subticks, clock.HalfSubtick, completed: false);
            limit = null;
            return new(hit, true) { LimitPresentedContact = presented, LimitTargetCue = targetCue, Continuation = new(CrossoverGuestAbilityCatalog.Key(action.Command, action.ActionId), targets) };
        }
        bool complete = program.Complete;
        if (complete)
        {
            NativeBattleGraphicsModel final = program.Model;
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

    }

    private BattleNativeAction SelectedFinisherAction(byte id) => assets.NativeActions.Single(action => action.Command == 249 && action.ActionId == id);

    private CrossoverGuestRenzokukenHostState? CaptureLimit() => limit is { } current ? current with { Program = CrossoverNativeRenzokukenProgram.Capture(current.Program) } : null;

    private CrossoverGuestRenzokukenHostState? RestoreLimit(CrossoverGuestRenzokukenHostState? saved)
    {
        if (saved is null)
        {
            return null;
        }
        if (limitOptions != saved.Program.Trigger.Options || limitSelection is null || fire is not null || attack.Invocation is not null || saved.Invocation.GuestCommand != 5 || saved.Invocation.RequestId != host.RequestId || saved.Invocation.FormationId != host.Formation.FormationId || saved.Invocation.ActorSlot != ActorSlot || saved.Invocation.TargetSlot != saved.Program.Effect.HostTarget || saved.HostTick != host.Tick || saved.HostTick < saved.Invocation.StartedAt || saved.Plan.Variant != saved.Program.Effect.Frame.Effect.Binding.Effect || saved.Program.Effect.Frame.Random != encounter.CrtRandom || saved.Program.Trigger.Random != limitUiRandom || saved.FinisherNumericTick > saved.HostTick || saved.FinisherNumericTick is not null || saved.Rough is not null)
        {
            throw new InvalidDataException("The saved guest Renzokuken contradicts its native source and host queue occurrence.");
        }
        CrossoverNativeLimitCamera camera = saved.Camera ?? CrossoverNativeLimitCamera.Capture(saved.Program.Model, limitResource.PoseSource!, saved.Placement, host.VisualPlacement(saved.Invocation.TargetSlot).Position, host.GuestEffectTargetAnchor(saved.Invocation.TargetSlot));
        camera.Validate();
        return saved with { Camera = camera, Program = CrossoverNativeRenzokukenProgram.Restore(saved.Program) };
    }
}
