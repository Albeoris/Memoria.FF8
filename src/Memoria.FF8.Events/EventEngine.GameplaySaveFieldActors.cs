using System.Numerics;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

/// <summary>Normal-load actor construction, distinct from an executed script or native loader receipt.</summary>
public sealed record GameplayFieldActorReconstruction(int Version, int FieldId, string ProgramHash, int ObjectId, long Frame, FieldFixedPosition? Position, GameplaySaveActorLine Line, GameplaySaveActorAppearance Appearance, GameplaySaveActorFootsteps Footsteps, GameplaySaveActorOffset? Offset);

public sealed partial class EventEngine
{
    public GameplaySaveFieldActors CaptureGameplaySaveFieldActors()
    {
        if (program is null || !defaultsStarted || NativeRuntimeProfile is not { IsVerified: true } || transitionRequested)
        {
            throw new NotSupportedException("Gameplay actor projection requires an allocated stable field program.");
        }
        GameplaySaveFieldActors result = new(1, FieldId, actors.Select(CaptureGameplayActor).ToArray(), Scene.NativeActorObjectIds.ToArray(), Scene.ModelParts?.Resources.ToDictionary(pair => pair.Key, pair => pair.Value) ?? []);
        result.Validate();
        foreach (GameplaySaveFieldActor actor in result.Actors)
        {
            ValidateGameplayNativeActorValues(actor);
        }
        return result.Detach();
    }

    private GameplaySaveFieldActor CaptureGameplayActor(ActorState actor)
    {
        if (actor.NativeScriptMovement is { Active: true } || actor.NativeJump is { Active: true } || actor.NativeLadder is { Active: true } || actor.NativeLadderMode3 is { Active: true } || actor.NativeMovementRequest is not null || actor.MovementTarget is not null || actor.RotationTarget is not null || actor.NativeDisplayOffset is { Mode: 1 or 2 } || actor.NativePushContactPending || actor.NativeLineCrossingPending || actor.DoorLineEntryPending)
        {
            throw new NotSupportedException($"Gameplay actor {actor.ObjectId} has an unfinished movement, rotation, offset or contact operation.");
        }
        ValidateGameplayFaceActor(actor);
        NativeActorAnimationHeader? header = actor.NativeAnimationHeader;
        if (header?.BoundModelId is int bound && bound != (actor.ModelId is -1 or ushort.MaxValue ? -1 : actor.ModelId))
        {
            throw new NotSupportedException($"Gameplay actor {actor.ObjectId} has a model selection that has not been bound.");
        }
        NativeActorAnimationBackup? backup = actor.NativeAnimationBackup;
        GameplaySaveActorAnimation animation = new(header?.AnimationId ?? checked((byte)actor.AnimationId), header?.Frame, header?.Increment ?? checked((ushort)actor.AnimationSpeed), header?.First ?? checked((ushort)(actor.AnimationFirst * 16)), header?.Flags is ushort currentFlags ? (GameplaySaveActorAnimationFlags)currentFlags : null, actor.BaseAnimationId, actor.WalkAnimationId, actor.RunAnimationId, actor.BaseFirstFrame, actor.BaseLastFrame, CaptureGameplayAnimationEnd(actor, header?.AnimationId, header?.Terminal));
        GameplaySaveActorAnimation? savedBackup = backup is null ? null : new(backup.AnimationId, backup.Frame, backup.Increment, backup.First, (GameplaySaveActorAnimationFlags)backup.Flags, actor.BaseAnimationId, actor.WalkAnimationId, actor.RunAnimationId, actor.BaseFirstFrame, actor.BaseLastFrame, CaptureGameplayAnimationEnd(actor, backup.AnimationId, backup.Terminal));
        if (IsGameplayResourceLastPose(actor, animation.Selector, header?.Frame, header?.Terminal, animation.Increment, animation.Flags))
        {
            animation = animation with { End = null, UseResourceLastPose = true };
        }
        if (savedBackup is { } savedPhase && IsGameplayResourceLastPose(actor, savedPhase.Selector, backup!.Frame, backup.Terminal, savedPhase.Increment, savedPhase.Flags))
        {
            savedBackup = savedPhase with { End = null, UseResourceLastPose = true };
        }
        NativeDisplayOffsetState? offset = actor.NativeDisplayOffset;
        return new(actor.ObjectId, actor.Label,
            new(actor.ModelId, actor.CharacterId, actor.Active),
            new(new(actor.X, actor.Y, actor.Z), ReadNativeScriptPosition(actor) is { } raw ? new(raw.X, raw.Y, raw.Z) : null, actor.WalkmeshTriangle, actor.Direction, actor.DisplayOffset, actor.RootTranslationZ),
            new(actor.TalkEnabled, actor.PushEnabled, actor.CollisionEnabled, actor.NativeSavedCollisionDisabled16, actor.TalkRadius, actor.PushRadius, actor.MovementSpeed, actor.NativeMovementBaseSpeed, actor.NativeMovementAccelerationTarget, actor.MovementDirectionLimit, actor.MovementFlags, actor.MovementIgnoresWalkmesh, actor.NativeMovementCompletion542, actor.NativeLandingTriangle508, actor.NativeTurnAccumulator474, actor.NativeMovementGroundContact600, actor.NativeTurnMode580, actor.NativeMotionAuthority?.MovementDirection, actor.NativeMotionAuthority?.FacingHold, actor.NativePartyFlags?.AutoAnimation4, actor.NativePartyFlags?.DetachedFollow64, actor.NativeCardChallenge),
            new(actor.Visible, actor.AdditionalControlDisabled, actor.PrimitiveTransparencyEnabled, actor.NativeRendererMode599, actor.NativeEyeBlinkParameter611, actor.NativeEyes?.Mode, actor.PolygonColor, actor.GetaParameter, checked((byte)actor.ShadeLevel), CaptureGameplayShadowRadii(actor)),
            animation, savedBackup,
            new(actor.FaceFirstAngle, actor.FaceSecondAngle, actor.FaceElapsed, actor.FaceLimits, actor.FacePendingBaseline, actor.FacePitchTarget, actor.FaceYawTarget, actor.FaceDuration, actor.FaceProgress, actor.FaceTargetPoint, actor.FirstControlRotation) { Returning = actor.FaceReturning },
            new(actor.LineStart, actor.LineEnd, actor.LineEnabled, actor.DoorLineEnabled, actor.LineContact, actor.DoorLineContact, actor.NativeLineFacingDirection, actor.NativeLineFacingContact),
            new(actor.FootstepsEnabled, actor.NativeFootstepSpecial, actor.NativeFootstepPair?.First392, actor.NativeFootstepPair?.Second393),
            offset is null ? null : new(offset.X, offset.Y, offset.Z, offset.StartX, offset.StartY, offset.StartZ, offset.TargetX, offset.TargetY, offset.TargetZ, offset.Duration, offset.Counter, offset.Mode))
        {
            LadderAnimations = actor.NativeLadderAnimation is { } ladder ? new(ladder.Start594, ladder.Loop595, ladder.End596) : null
        };
    }

    /// <summary>Restores logical actors into a fresh allocated program, rebuilding selected resources without INIT or a callback.</summary>
    public void RestoreGameplaySaveFieldActors(GameplaySaveFieldActors saved)
    {
        RestoreGameplaySaveFieldActors(saved, false);
    }

    private void RestoreGameplaySaveFieldActors(GameplaySaveFieldActors saved, bool pendingMenuPartyRefresh)
    {
        ArgumentNullException.ThrowIfNull(saved);
        saved.Validate();
        if (program is null || NativeRuntimeProfile is not { IsVerified: true } || saved.FieldId != FieldId || saved.Actors.Length != actors.Count || saved.Actors.Where((actor, index) => actor.ObjectId != index || actor.Label != actors[index].Label).Any() || transitionRequested || defaultsStarted || Scene.NativeFieldInitialization is not { Origin: NativeFieldInitializationOrigin.GameplayContinuation, Stage: NativeFieldInitializationStage.Completed } proof || !proof.IsFrom(NativeRuntimeProfile, FieldId, programHash) || services is not GameSession)
        {
            throw new InvalidOperationException("Gameplay actors require the matching allocated current program and stable game session.");
        }
        GameplaySaveFieldActors detached = saved.Detach();
        foreach (GameplaySaveFieldActor actor in detached.Actors)
        {
            ValidateGameplayNativeActorValues(actor);
        }
        foreach (GameplaySaveFieldActor savedActor in detached.Actors)
        {
            RestoreGameplayActor(actors[savedActor.ObjectId], savedActor);
        }
        if (!detached.RegisteredActors.SequenceEqual(program.NativeObjects.Take(program.NativeActorCount).Where(detached.RegisteredActors.Contains)))
        {
            throw new InvalidDataException("Gameplay retained actor bindings do not match current native object order.");
        }
        Scene.NativeActorObjectIds.Clear();
        Scene.NativeActorObjectIds.AddRange(detached.RegisteredActors);
        Scene.NativeActorLayoutVersion = 1;
        if (Scene.FieldPartyNativeActorIds.Count != 0 && (Scene.FieldPartyNativeActorIds.Count != 3 || Scene.FieldPartyNativeActorIds.Where((ordinal, slot) => ordinal != 255 && ((uint)ordinal >= Scene.NativeActorObjectIds.Count || !pendingMenuPartyRefresh && (byte)actors[Scene.NativeActorObjectIds[ordinal]].CharacterId != (byte)Scene.Party[slot])).Any()))
        {
            throw new InvalidDataException("Gameplay actor layout does not match the saved logical party bindings.");
        }
        BindNativeAnimationResources();
        foreach (GameplaySaveFieldActor savedActor in detached.Actors.Where(actor => Scene.NativeActorObjectIds.Contains(actor.ObjectId)))
        {
            RestoreGameplayAnimation(actors[savedActor.ObjectId], savedActor);
        }
        InitializeNativeFieldEyeProcess();
        RestoreGameplayActorPresentation(detached);
        InitializeNativeModelParts();
        RestoreGameplayModelParts(detached);
        foreach (ActorState actor in actors)
        {
            ValidateGameplayFaceActor(actor);
        }
    }

    private void RestoreGameplayActor(ActorState actor, GameplaySaveFieldActor saved)
    {
        GameplaySaveActorControl control = saved.Control;
        GameplaySaveActorAppearance appearance = saved.Appearance;
        actor.ModelId = saved.Identity.ModelId;
        actor.CharacterId = saved.Identity.CharacterId;
        actor.Active = saved.Identity.Active;
        actor.X = saved.Pose.Position.X;
        actor.Y = saved.Pose.Position.Y;
        actor.Z = saved.Pose.Position.Z;
        actor.WalkmeshTriangle = saved.Pose.Triangle;
        actor.Direction = saved.Pose.Direction;
        actor.DisplayOffset = saved.Pose.DisplayOffset;
        actor.RootTranslationZ = saved.Pose.RootTranslationZ;
        actor.TalkEnabled = control.TalkEnabled;
        actor.PushEnabled = control.PushEnabled;
        actor.CollisionEnabled = control.CollisionEnabled;
        actor.NativeSavedCollisionDisabled16 = control.SavedCollisionDisabled;
        actor.TalkRadius = control.TalkRadius;
        actor.PushRadius = control.PushRadius;
        actor.MovementSpeed = control.MovementSpeed;
        actor.NativeMovementBaseSpeed = control.MovementBaseSpeed;
        actor.NativeMovementAccelerationTarget = control.AccelerationTarget;
        actor.MovementDirectionLimit = control.DirectionLimit;
        actor.MovementFlags = control.MovementFlags;
        actor.MovementIgnoresWalkmesh = control.IgnoresWalkmesh;
        actor.NativeMovementCompletion542 = control.Completion;
        actor.NativeLandingTriangle508 = control.LandingTriangle;
        actor.NativeTurnAccumulator474 = control.TurnAccumulator;
        actor.NativeMovementGroundContact600 = control.GroundContact;
        actor.NativeTurnMode580 = control.TurnMode;
        actor.NativeMotionAuthority = control.MotionDirection is int direction && control.FacingHold is int hold ? new(1, checked((byte)direction), checked((byte)hold)) : null;
        actor.NativePartyFlags = control.AutoAnimation is bool auto && control.DetachedFollow is bool detached ? new(auto, detached) : null;
        actor.NativeCardChallenge = control.CardChallenge;
        actor.NativeLadderAnimation = saved.LadderAnimations is { } ladder ? new(ladder.Start, ladder.Loop, ladder.End) : null;
        actor.Visible = appearance.Visible;
        actor.AdditionalControlDisabled = appearance.AdditionalControlDisabled;
        actor.PrimitiveTransparencyEnabled = appearance.PrimitiveTransparency;
        actor.NativeRendererMode599 = appearance.RendererMode;
        actor.NativeEyeBlinkParameter611 = appearance.BlinkParameter;
        actor.PolygonColor = appearance.PolygonColor;
        actor.GetaParameter = appearance.GetaParameter;
        actor.ShadeLevel = appearance.ShadeLevel;
        actor.NativeShadowAllocation = null;
        actor.NativeShadowSet = null;
        actor.NativeShadowForm = null;
        actor.NativeShadowLevel = null;
        actor.BaseAnimationId = saved.Animation.Idle;
        actor.WalkAnimationId = saved.Animation.Walk;
        actor.RunAnimationId = saved.Animation.Run;
        actor.BaseFirstFrame = saved.Animation.IdleFirst;
        actor.BaseLastFrame = saved.Animation.IdleLast;
        actor.FaceFirstAngle = saved.Face.FirstAngle;
        actor.FaceSecondAngle = saved.Face.SecondAngle;
        actor.FaceElapsed = saved.Face.Elapsed;
        actor.FaceLimits = saved.Face.Limits;
        actor.FacePendingBaseline = saved.Face.PendingBaseline;
        actor.FacePitchTarget = saved.Face.PitchTarget;
        actor.FaceYawTarget = saved.Face.YawTarget;
        actor.FaceDuration = saved.Face.Duration;
        actor.FaceProgress = saved.Face.Progress;
        actor.FaceReturning = saved.Face.Returning;
        actor.FaceTargetPoint = saved.Face.TargetPoint;
        actor.FirstControlRotation = saved.Face.FirstControlRotation;
        actor.LineStart = saved.Line.Start;
        actor.LineEnd = saved.Line.End;
        actor.LineEnabled = saved.Line.Enabled;
        actor.DoorLineEnabled = saved.Line.DoorEnabled;
        actor.LineContact = saved.Line.Contact;
        actor.DoorLineContact = saved.Line.DoorContact;
        actor.NativeLineContactVersion = saved.Line.Contact || saved.Line.DoorContact ? 2 : 0;
        actor.NativeLineFacingDirection = saved.Line.FacingDirection;
        actor.NativeLineFacingContact = saved.Line.FacingContact;
        actor.LineOperation = -1;
        actor.NativeSetLineReceipt = null;
        actor.FootstepsEnabled = saved.Footsteps.Enabled;
        actor.NativeFootstepSpecial = saved.Footsteps.Special;
        actor.NativeFootstepPair = saved.Footsteps.First is byte first && saved.Footsteps.Second is byte second ? new(1, first, second, new(1, "GameplaySave", FieldId, programHash, NativeRuntimeProfile!.ExecutableHash, actor.ObjectId, -1, -1, Frame)) : null;
        actor.NativeDisplayOffset = saved.Offset is { } offset ? new(1, offset.X, offset.Y, offset.Z, offset.StartX, offset.StartY, offset.StartZ, offset.TargetX, offset.TargetY, offset.TargetZ, offset.Duration, offset.Counter, offset.Mode) : null;
        actor.GameplayReconstruction = new(1, FieldId, programHash, actor.ObjectId, Frame, saved.Pose.FixedPosition is { } raw ? new(raw.X, raw.Y, raw.Z) : null, saved.Line, saved.Appearance, saved.Footsteps, saved.Offset);
    }

    private void RestoreGameplayAnimation(ActorState actor, GameplaySaveFieldActor saved)
    {
        NativeActorAnimationHeader bound = RequireNativeAnimationHeader(actor);
        GameplaySaveActorAnimation animation = saved.Animation;
        (ushort? phase, ushort? terminal) = RestoreGameplayAnimationPhase(actor, animation);
        actor.NativeAnimationHeader = bound with { AnimationId = animation.Selector, Frame = phase, Increment = animation.Increment, First = animation.First, Terminal = terminal, Flags = animation.Flags is { } flags ? checked((ushort)flags) : null };
        PublishNativeAnimation(actor, true);
        if (saved.AnimationBackup is { } backup)
        {
            (ushort? backupPhase, ushort? backupTerminal) = RestoreGameplayAnimationPhase(actor, backup);
            actor.NativeAnimationBackup = new(1, backup.Selector, backupPhase ?? 0, backup.Increment, backup.First, backupTerminal ?? 0, checked((ushort)(backup.Flags ?? 0)));
        }
        else
        {
            actor.NativeAnimationBackup = null;
        }
    }

    private (ushort? Phase, ushort? Terminal) RestoreGameplayAnimationPhase(ActorState actor, GameplaySaveActorAnimation animation)
    {
        NativeActorAnimationHeader bound = RequireNativeAnimationHeader(actor);
        ushort? resourceTerminal = bound.BoundModelId == -1 ? bound.EmptyModelTerminal : NativeAnimationTerminal(actor, animation.Selector);
        ushort? terminal = animation.End ?? resourceTerminal;
        ushort? phase = animation.Frame;
        if (animation.UseResourceLastPose)
        {
            if (resourceTerminal is not ushort full || animation.Increment > full)
            {
                throw new NotSupportedException("Selected clip cannot represent the saved completed final pose.");
            }
            terminal = checked((ushort)(full - animation.Increment));
            phase = terminal;
        }
        else if (animation.End is null && terminal is ushort end && phase > end && end > animation.First)
        {
            phase = (animation.Flags & GameplaySaveActorAnimationFlags.Repeating) != 0
                ? checked((ushort)(animation.First + (phase.Value - animation.First) % (end - animation.First)))
                : checked((ushort)(end - 1));
        }
        if (terminal is ushort currentEnd && (animation.First > currentEnd || terminal > resourceTerminal || phase > currentEnd))
        {
            throw new NotSupportedException("Saved logical animation range lies outside the selected current model.");
        }
        return (phase, terminal);
    }

    private bool IsGameplayResourceLastPose(ActorState actor, byte selector, ushort? frame, ushort? terminal, ushort increment, GameplaySaveActorAnimationFlags? flags)
    {
        return actor.NativeAnimationHeader?.BoundModelId is >= 0 && (flags & (GameplaySaveActorAnimationFlags.Complete | GameplaySaveActorAnimationFlags.Frozen)) == (GameplaySaveActorAnimationFlags.Complete | GameplaySaveActorAnimationFlags.Frozen) && NativeAnimationTerminal(actor, selector) is ushort full && increment <= full && frame == terminal && terminal == full - increment;
    }

    private void RestoreGameplayActorPresentation(GameplaySaveFieldActors saved)
    {
        foreach (GameplaySaveFieldActor value in saved.Actors.Where(actor => Scene.NativeActorObjectIds.Contains(actor.ObjectId)))
        {
            ActorState actor = actors[value.ObjectId];
            if (value.Appearance.EyeMode is 0x100000 or 0x200000)
            {
                actor.NativeEyes = PrepareNativeEyes(actor, value.Appearance.EyeMode == 0x200000);
            }
            else if (value.Appearance.EyeMode == 0x80000)
            {
                int model = actor.NativeAnimationHeader!.BoundModelId!.Value;
                NativeFieldEyeDefinition? definition = model < 0 ? null : ReadNativeEyes?.Invoke(model) ?? throw new NotSupportedException("Logical blink restoration requires the selected current model eye bank.");
                actor.NativeEyes = new(1, 0x80000, model, definition?.Bank.Length > 0 ? (byte)0x10 : null, null);
            }
        }
        if (Scene.NativeFieldRegistration is { } registration && Scene.NativeEyeProcess is { } process)
        {
            NativeFieldRetainedPresentation[] presentation = Scene.NativeActorObjectIds.Select(id =>
            {
                ActorState actor = actors[id];
                Vector3 color = actor.PolygonColor ?? throw new NotSupportedException("Logical model restoration requires the saved polygon color.");
                NativeActorAnimationHeader header = RequireNativeAnimationHeader(actor);
                return new NativeFieldRetainedPresentation(id, header.AnimationId, header.Frame, actor.NativePartyFlags!.AutoAnimation4, checked((byte)actor.BaseAnimationId), actor.Visible, actor.NativeRendererMode599!.Value, actor.RootTranslationZ, (byte)color.X, (byte)color.Y, (byte)color.Z, actor.AdditionalControlDisabled, actor.PrimitiveTransparencyEnabled, actor.NativeEyes!.Mode) { EyeBlinkParameter611 = actor.NativeEyeBlinkParameter611 };
            }).ToArray();
            NativeBattleFieldResourceReloadResult rebuilt = NativeFieldRegistrationMachine.RestoreRetainedPresentation(registration, process.Memory, presentation, process.CrtRandom);
            PublishNativeFieldEyeProcess(new(1, rebuilt.Memory, rebuilt.CrtRandomAfter));
        }
    }

    private void RestoreGameplayModelParts(GameplaySaveFieldActors saved)
    {
        if (Scene.ModelParts is not { } bank)
        {
            if (saved.ModelParts.Count != 0)
            {
                throw new NotSupportedException("Logical model-part restoration requires selected current model definitions.");
            }
            return;
        }
        Dictionary<int, NativeModelPartResourceState> resources = new(bank.Resources);
        foreach ((int model, NativeModelPartResourceState state) in saved.ModelParts)
        {
            if (!resources.ContainsKey(model))
            {
                throw new NotSupportedException("Saved logical model-part state has no current actor resource binding.");
            }
            state.Validate(ReadNativeModelParts!(model)!);
            resources[model] = state;
        }
        Dictionary<int, NativeModelPartDraw> draws = [];
        foreach (NativeFieldModelInstance instance in bank.Instances.Where(instance => actors[instance.ObjectId].Visible))
        {
            draws[instance.ObjectId] = new(Frame, instance.ModelId, resources[instance.ModelId]) { Origin = NativeModelPartDrawOrigin.GameplayReconstruction };
            if (CaptureFootstepModelOrigin is not null)
            {
                ActorState actor = actors[instance.ObjectId];
                actor.NativeFootstepModelOrigin = CaptureFootstepModelOrigin(new(actor, ReadNativeScriptPosition(actor), instance.ModelId, Frame));
            }
        }
        Scene.ModelParts = bank with { Resources = resources, Draws = draws };
    }

    private ushort? CaptureGameplayAnimationEnd(ActorState actor, byte? selector, ushort? terminal)
    {
        if (terminal is null || selector is null)
        {
            return null;
        }
        ushort? full = actor.NativeAnimationHeader?.BoundModelId is >= 0 ? NativeAnimationTerminal(actor, selector.Value) : actor.NativeAnimationHeader?.EmptyModelTerminal;
        return full == terminal ? null : terminal;
    }

    private void ValidateGameplayNativeActorValues(GameplaySaveFieldActor actor)
    {
        if (!program!.NativeObjects.Take(program.NativeActorCount).Contains(actor.ObjectId))
        {
            return;
        }
        GameplaySaveActorControl control = actor.Control;
        if (control.SavedCollisionDisabled is null || control.MovementBaseSpeed is null || control.Completion is null || control.LandingTriangle is null || control.TurnAccumulator is null || control.GroundContact is null || control.TurnMode is null || control.MotionDirection is null || control.FacingHold is null || control.AutoAnimation is null || control.DetachedFollow is null || actor.Appearance.RendererMode is null || actor.Appearance.BlinkParameter is null || actor.Appearance.PolygonColor is null || actor.Appearance.EyeMode is null || actor.AnimationBackup is null || actor.LadderAnimations is null || actor.Offset is null || actor.Footsteps.Special is null || actor.Footsteps.First is null)
        {
            throw new NotSupportedException("Gameplay native actor projection requires represented logical control, appearance, animation and footstep values.");
        }
    }

    private static byte[]? CaptureGameplayShadowRadii(ActorState actor)
    {
        if (actor.ResolvedShadowRadii is { } radii)
        {
            return radii.ToArray();
        }
        byte? radius = actor.NativeShadowSet?.Radius601To608 ?? (actor.NativeShadowAllocation is not null ? NativeActorShadowAllocation.DefaultRadius601To608 : null);
        return radius is byte value ? Enumerable.Repeat(value, 8).ToArray() : null;
    }

    private void ValidateGameplayActorReconstruction(ActorState actor, long frame, NativeFieldInitializationProof? proof)
    {
        if (actor.GameplayReconstruction is not null && (!ValidGameplayActorReconstruction(actor, frame) || proof?.Origin != NativeFieldInitializationOrigin.GameplayContinuation || actor.ShadeLevel is < 0 or > 255 || actor.GameplayReconstruction.Footsteps is null || actor.GameplayReconstruction.Appearance is null || actor.GameplayReconstruction.Line is null || actor.GameplayReconstruction.Appearance.ShadowRadii is { Length: not 8 }))
        {
            throw new InvalidDataException("Saved logical actor reconstruction does not belong to this field program.");
        }
    }

    private bool ValidGameplayActorReconstruction(ActorState actor, long frame)
    {
        return actor.GameplayReconstruction is { Version: 1 } origin && origin.FieldId == FieldId && origin.ProgramHash == programHash && origin.ObjectId == actor.ObjectId && origin.Frame >= 0 && origin.Frame <= frame && program is not null && (uint)actor.ObjectId < program.Objects.Count;
    }
}
