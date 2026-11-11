using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed record Ff7BattleActorModelState(int Slot, short Action = 0, BattleActionPoseState? Pose = null, bool Ready = true, ushort? Limit = null, long? IdleStarted = null, bool Reaction = false, long PendingSince = -1);

public sealed partial class Ff7BattleSession
{
    private int actorModelCursor;

    public bool NativeActorModelsReady => state.ActorModels.All(model => model.Ready);

    private void InitializeActorModels()
    {
        if (animations is null || state.ActorModels.Length != 0)
        {
            return;
        }
        state = state with { ActorModels = state.Actors.Select(actor => new Ff7BattleActorModelState(actor.Actor.Slot)).ToArray() };
        if (state.Action is { Applied: true } action)
        {
            PublishSourceModel(action, action.Pose);
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
        ReplaceActorModel(new(action.Command.ActorSlot, checked((short)action.Animation), Ready: false, Limit: action.Command.CommandId == (byte)Ff7BattleCommand.Limit ? action.Command.Argument : null));
    }

    private void PublishSourceModel(Ff7BattleActionState action, BattleActionPoseState? pose)
    {
        if (state.ActorModels.Length == 0 || action.Animation < 0)
        {
            return;
        }
        Ff7BattleActorModelState model = ActorModelState(action.Command.ActorSlot);
        if (pose is { Completed: true, TerminalOpcode: 0xEE or 0xFF })
        {
            ReplaceActorModel(new(model.Slot, Actor(model.Slot).Actor.IdleAction, Ready: true, IdleStarted: model.IdleStarted ?? PresentationTick));
        }
        else
        {
            ReplaceActorModel(model with { Action = checked((short)action.Animation), Pose = pose, Ready = pose is { Completed: true }, Limit = action.Command.CommandId == (byte)Ff7BattleCommand.Limit ? action.Command.Argument : null });
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
        foreach (Ff7BattleActorModelState model in state.ActorModels.Where(model => model.Slot >= minimum && model.Slot < maximum && model.Reaction && model.PendingSince < PresentationTick).ToArray())
        {
            BattleActionSequence sequence = animations.Get(ActorModel(model.Slot), model.Action);
            BattleActionPoseUpdate update = sequence.Advance(model.Pose ?? new(), effectReady: true, primaryCallbacksReady: !HasPrimaryPresentationCallbacks, linkedActorReady: LinkedModelReady(model.Slot));
            if (update.Events.Any(instruction => instruction.Opcode is not (0xEE or 0xFF or 0x9E)))
            {
                throw new NotSupportedException("FFVII actor reaction requires an additional native model-event owner.");
            }
            if (update.State is { Completed: true, TerminalOpcode: 0xEE or 0xFF })
            {
                ReplaceActorModel(new(model.Slot, Actor(model.Slot).Actor.IdleAction, Ready: true, IdleStarted: PresentationTick));
            }
            else
            {
                ReplaceActorModel(model with { Pose = update.State, Ready = update.State.Completed });
            }
        }
    }

    private void ValidateActorModels(Ff7BattleSnapshot snapshot)
    {
        if (snapshot.ActorModels is null || snapshot.ModelPrimaryTarget is < 0 or > 15 || snapshot.ActorModels.Any(model => model is null) || snapshot.ActorModels.Length != 0 && !snapshot.ActorModels.Select(model => model.Slot).SequenceEqual(snapshot.Actors.Select(actor => actor.Actor.Slot)) || snapshot.ActorModels.Any(model => model.Action < 0 || model.PendingSince < -1 || model.IdleStarted < 0 || model.IdleStarted > (snapshot.Tick + 3) / 4 || model.Pose is { TerminalOpcode: not (null or 0x9E or 0xEE or 0xFF) }))
        {
            throw new InvalidDataException("FFVII saved actor model readiness or pose ownership is invalid.");
        }
    }

    private void BeginActorReaction(int slot, short selector)
    {
        if (state.ActorModels.Length == 0)
        {
            return;
        }
        if (selector == 0x33)
        {
            ReplaceActorModel(ActorModelState(slot) with { Ready = true });
            return;
        }
        ReplaceActorModel(new(slot, selector, Ready: false, Reaction: true, PendingSince: PresentationTick));
    }
}
