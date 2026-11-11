using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public readonly record struct Ff7BattleWorldJoint(float X, float Y, float Z)
{
    public Vector3 ToVector() => new(X, Y, Z);
    public Ff7BattleCameraPoint ToCameraPoint() => new(unchecked((short)(int)X), unchecked((short)(int)Y), unchecked((short)(int)Z));
}

public sealed record Ff7BattleActorRenderState(int Clip, int Frame, Ff7BattlePosition Position, int Yaw, Ff7BattleActorModelModifiers Modifiers, ushort? Limit = null, short ProjectedScreenX = -1);
public sealed record Ff7BattleActorModelState(int Slot, short Action = 0, BattleActionPoseState? Pose = null, bool Ready = true, ushort? Limit = null, long? IdleStarted = null, bool Reaction = false, long PendingSince = -1, Ff7BattleActorModelModifiers? Modifiers = null, Ff7BattlePosition? Position = null, int? FacingYaw = null, Ff7BattleWorldJoint[]? Joints = null, Ff7BattleActorRenderState? Render = null);

public sealed partial class Ff7BattleSession
{
    private int actorModelCursor;
    private readonly HashSet<int> deferredActorSubmissions = [];
    private readonly HashSet<int> actorDeferAfterSubmission = [];

    public short ActorProjectedScreenX(int slot) => ActorModelState(slot).Render?.ProjectedScreenX ?? -1;

    public bool NativeActorModelsReady => state.ActorModels.All(model => model.Ready);
    public Ff7BattleActorModelModifiers ActorModelOutput(int slot) => ActorModelState(slot).Render?.Modifiers ?? LiveActorModelOutput(slot);
    public (int Clip, int Frame) RenderActorPose(int slot) => ActorModelState(slot).Render is { } render ? (render.Clip, render.Frame) : VisualPose(slot);
    public Ff7BattlePlacement RenderActorPlacement(int slot) => ActorModelState(slot).Render is { } render ? new(render.Position.ToVector(), render.Yaw) : VisualPlacement(slot);
    public BattleLimitAnimation? RenderActorLimit(int slot) => ActorModelState(slot).Render is { } render ? render.Limit is ushort limit ? animations!.GetLimit(limit) : null : VisualLimit(slot);

    private Ff7BattleActorModelModifiers LiveActorModelOutput(int slot) => ActorModelState(slot).Modifiers!;

    private void InitializeActorModels(bool fresh = false)
    {
        if (animations is null)
        {
            return;
        }
        bool absent = state.ActorModels.Length == 0;
        if (absent)
        {
            state = state with { ActorModels = state.Actors.Select(actor => new Ff7BattleActorModelState(actor.Actor.Slot)).ToArray() };
        }
        state = state with { ActorModels = state.ActorModels.Select(model => model with { Modifiers = model.Modifiers ?? new(Visible: model.Slot < 3 || IsAlive(Actor(model.Slot)) || state.Action is { } action && (action.Command.TargetMask & (1 << model.Slot)) != 0) }).ToArray() };
        if (absent && state.Action is { Applied: true } source)
        {
            PublishSourceModel(source, source.Pose);
        }
        foreach (Ff7BattleActorModelState model in state.ActorModels.Where(model => model.Joints is null).ToArray())
        {
            Ff7BattleWorldJoint[] joints = new Ff7BattleWorldJoint[animations.BoneCount(ActorModel(model.Slot))];
            if (!fresh || cameraScripts is null)
            {
                for (int bone = 0; bone < joints.Length; bone++)
                {
                    Vector3 point = LiveBonePosition(model.Slot, bone);
                    joints[bone] = new(point.X, point.Y, point.Z);
                }
            }
            ReplaceActorModel(model with { Joints = joints });
        }
    }

    private Ff7BattleActorModelState ActorModelState(int slot) => state.ActorModels.Single(model => model.Slot == slot);

    private void ReplaceActorModel(Ff7BattleActorModelState model)
    {
        state = state with { ActorModels = state.ActorModels.Select(current => current.Slot == model.Slot ? model : current).ToArray() };
    }

    private void BeginSourceModel(Ff7BattleActionState action)
    {
        if (state.ActorModels.Length == 0 || action.Animation < 0)
        {
            return;
        }
        Ff7BattleActorModelState model = ActorModelState(action.Command.ActorSlot);
        ReplaceActorModel(model with { Action = checked((short)action.Animation), Pose = null, Ready = false, Limit = action.Command.CommandId == (byte)Ff7BattleCommand.Limit ? action.Command.Argument : null, IdleStarted = null, Reaction = false, PendingSince = -1 });
    }

    private void PublishSourceModel(Ff7BattleActionState action, BattleActionPoseState? pose)
    {
        if (state.ActorModels.Length == 0 || action.Animation < 0)
        {
            return;
        }
        Ff7BattleActorModelState model = ActorModelState(action.Command.ActorSlot);
        ObserveModelClip(model, pose);
        model = ActorModelState(model.Slot);
        if (pose is { Completed: true, TerminalOpcode: 0xEE or 0xFF })
        {
            ReplaceActorModel(model with { Action = Actor(model.Slot).Actor.IdleAction, Pose = null, Ready = true, Limit = null, IdleStarted = model.IdleStarted ?? PresentationTick, Reaction = false });
        }
        else
        {
            ReplaceActorModel(model with { Action = checked((short)action.Animation), Pose = pose, Ready = pose is { Completed: true }, Limit = action.Command.CommandId == (byte)Ff7BattleCommand.Limit ? action.Command.Argument : null });
        }
    }

    private void PublishSourceMotion(Ff7BattleActionState action)
    {
        if (state.ActorModels.Length != 0 && action.Animation >= 0)
        {
            Ff7BattleActorModelState model = ActorModelState(action.Command.ActorSlot);
            ReplaceActorModel(model with { Position = action.Motion?.Position ?? model.Position, FacingYaw = action.FacingYaw ?? model.FacingYaw });
        }
    }

    private void ResetActorPosition(int slot)
    {
        if (state.ActorModels.Length != 0)
        {
            ReplaceActorModel(ActorModelState(slot) with { Position = Ff7BattlePosition.FromVector(HomePlacement(slot).Position) });
        }
    }

    private void ResetActorFacing(int slot)
    {
        if (state.ActorModels.Length != 0)
        {
            ReplaceActorModel(ActorModelState(slot) with { FacingYaw = null });
        }
    }

    private void ObserveModelClip(Ff7BattleActorModelState model, BattleActionPoseState? pose)
    {
        if (pose is { ClipIndex: >= 0, ClipFrame: 0, TerminalOpcode: null } && (model.Pose is null || model.Pose.ClipIndex != pose.ClipIndex || model.Pose.ClipFrame != 0 || model.Pose.NextInstruction != pose.NextInstruction))
        {
            SetActorModelDeferral(model.Slot, false);
        }
        if (pose?.TerminalOpcode == 0x9E)
        {
            actorDeferAfterSubmission.Add(model.Slot);
        }
    }

    private bool LinkedModelReady(int actor)
    {
        return actor == state.ModelPrimaryTarget || state.ActorModels.Length == 0 || state.ModelPrimaryTarget < 10 && (state.ActorModels.SingleOrDefault(model => model.Slot == state.ModelPrimaryTarget)?.Ready ?? true);
    }

    private void AdvanceActorReactionModels(int minimum, int maximum)
    {
        if (animations is null)
        {
            return;
        }
        foreach (Ff7BattleActorModelState previous in state.ActorModels.Where(model => model.Slot >= minimum && model.Slot < maximum && model.Reaction && model.PendingSince < PresentationTick).ToArray())
        {
            BattleActionSequence sequence = animations.Get(ActorModel(previous.Slot), previous.Action);
            BattleActionPoseUpdate update = sequence.Advance(previous.PendingSince >= 0 ? new() : previous.Pose ?? new(), effectReady: true, primaryCallbacksReady: !HasPrimaryPresentationCallbacks, linkedActorReady: LinkedModelReady(previous.Slot));
            ObserveModelClip(previous, update.State);
            Ff7BattleActorModelState model = ActorModelState(previous.Slot);
            ReplaceActorModel(model with { Pose = update.State, Ready = update.State.Completed, Limit = null, PendingSince = -1 });
            foreach (BattleActionInstruction instruction in update.Events)
            {
                switch (instruction.Opcode)
                {
                    case 0xF2:
                    case 0x9E:
                        break;
                    case 0xF6:
                        if (!IsAlive(Actor(previous.Slot)))
                        {
                            ScheduleActorDeath(previous.Slot);
                        }
                        break;
                    case 0xE5:
                        ResetActorFacing(previous.Slot);
                        break;
                    case 0xEE:
                    case 0xFF:
                        model = ActorModelState(previous.Slot);
                        ReplaceActorModel(model with { Action = Actor(model.Slot).Actor.IdleAction, Pose = null, Ready = true, IdleStarted = PresentationTick, Reaction = false });
                        break;
                    default:
                        throw new NotSupportedException($"FFVII actor reaction opcode {instruction.Opcode:X2} requires its native model-event owner.");
                }
            }
        }
    }

    private void BeginActorReaction(int slot, short selector)
    {
        if (state.ActorModels.Length == 0)
        {
            return;
        }
        Ff7BattleActorModelState model = ActorModelState(slot);
        if (selector == 0x33)
        {
            ReplaceActorModel(model with { Ready = true });
            return;
        }
        (int clip, int frame) = VisualPose(slot);
        ReplaceActorModel(model with { Action = selector, Pose = new(ClipIndex: clip, ClipFrame: frame), Ready = false, IdleStarted = null, Reaction = true, PendingSince = PresentationTick });
    }

    private void SetActorModelDeferral(int slot, bool deferred)
    {
        Ff7BattleActorModelState model = ActorModelState(slot);
        ReplaceActorModel(model with { Modifiers = model.Modifiers! with { Deferred = deferred } });
    }

    private void PublishActorFrames(bool deferred)
    {
        if (animations is null)
        {
            return;
        }
        foreach (Ff7BattleActorModelState model in state.ActorModels.ToArray())
        {
            Ff7BattleActorModelModifiers modifiers = model.Modifiers!;
            if (deferred && !deferredActorSubmissions.Contains(model.Slot))
            {
                continue;
            }
            if (!modifiers.Visible)
            {
                if (model.Render is { } previous)
                {
                    ReplaceActorModel(model with { Render = previous with { Modifiers = modifiers } });
                }
                continue;
            }
            if (state.Intro is { } intro && intro.Callbacks + 1 < (model.Slot >= 4 ? 6 : 7))
            {
                continue;
            }
            if (!deferred)
            {
                if (modifiers.Deferred)
                {
                    deferredActorSubmissions.Add(model.Slot);
                    continue;
                }
            }
            else if (!deferredActorSubmissions.Contains(model.Slot))
            {
                continue;
            }
            (int clip, int frame) = VisualPose(model.Slot);
            Ff7BattlePlacement placement = VisualPlacement(model.Slot);
            Ff7BattleWorldJoint[] joints = new Ff7BattleWorldJoint[animations.BoneCount(ActorModel(model.Slot))];
            for (int bone = 0; bone < joints.Length; bone++)
            {
                Vector3 point = LiveBonePosition(model.Slot, bone);
                joints[bone] = new(point.X, point.Y, point.Z);
            }
            int selector = animations.JointSelector(ActorModel(model.Slot), 0);
            Vector3 projected = BattleCameraBasis.Create(RenderCameraOutput).Transform(joints[selector].ToVector());
            short screenX = projected.Z <= 0 ? (short)-1 : unchecked((short)(int)(160 + projected.X * 512 / projected.Z));
            ReplaceActorModel(model with { Joints = joints, Render = new(clip, frame, Ff7BattlePosition.FromVector(placement.Position), placement.Yaw, modifiers, VisualLimitId(model.Slot), screenX) });
        }
        if (!deferred)
        {
            foreach (int slot in actorDeferAfterSubmission)
            {
                SetActorModelDeferral(slot, true);
            }
        }
    }

    private void ApplyActorModelWrite(Ff7BattleActorModelWrite write)
    {
        Ff7BattleActorModelState model = ActorModelState(write.Target);
        Ff7BattlePosition position = model.Position ?? Ff7BattlePosition.FromVector(VisualPlacement(write.Target).Position);
        if (write.WorldZ is short z)
        {
            position = position with { Z = z };
        }
        if (write.RestoreHome)
        {
            position = Ff7BattlePosition.FromVector(HomePlacement(write.Target).Position);
        }
        ReplaceActorModel(model with { Modifiers = write.Modifiers ?? model.Modifiers, Position = position, Ready = write.Ready || model.Ready });
    }

    private void ValidateActorModels(Ff7BattleSnapshot snapshot)
    {
        if (snapshot.ActorModels is null || snapshot.ModelPrimaryTarget is < 0 or > 15 || snapshot.ActorModels.Any(model => model is null) || snapshot.ActorModels.Length != 0 && !snapshot.ActorModels.Select(model => model.Slot).SequenceEqual(snapshot.Actors.Select(actor => actor.Actor.Slot)) || snapshot.ActorModels.Any(model => model.Action < 0 || model.PendingSince < -1 || model.IdleStarted < 0 || model.IdleStarted > (snapshot.Tick + 3) / 4 || model.Pose is { TerminalOpcode: not (null or 0x9E or 0xEE or 0xFF) } || model.Joints is { } joints && (animations is null || joints.Length != animations.BoneCount(Ff7BattleAnimationResources.ModelPrefix(snapshot.Actors.Single(actor => actor.Actor.Slot == model.Slot).Identity, model.Slot >= 4)) || joints.Any(joint => !float.IsFinite(joint.X) || !float.IsFinite(joint.Y) || !float.IsFinite(joint.Z)))))
        {
            throw new InvalidDataException("FFVII saved actor model readiness, joint cache or pose ownership is invalid.");
        }
    }
}
