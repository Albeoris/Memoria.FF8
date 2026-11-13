namespace Memoria.FF7.Runtime;

public sealed partial class Ff7BattleSession
{
    public bool IsGuestTargetStanding(int slot)
    {
        if (!CanPresentGuestReaction(slot))
        {
            throw new NotSupportedException("The target has no original standing animation owner.");
        }
        Ff7BattleActorModelState model = ActorModelState(slot);
        return model.Ready && model.Pose is null && !model.Reaction && model.Action == Actor(slot).Actor.IdleAction;
    }

    public bool CanPresentGuestReaction(int slot)
    {
        return animations is not null && state.ActorModels.Any(model => model.Slot == slot) && state.Actors.Any(actor => actor.Actor.Slot == slot);
    }

    /// <summary>Publishes a presentation request independently of damage and contact accounting.</summary>
    public void PresentGuestReaction(int sourceSlot, int targetSlot)
    {
        if (!CanPresentGuestReaction(targetSlot))
        {
            throw new NotSupportedException("The target has no original reaction animation owner.");
        }
        Ff7BattleAiActor actor = Actor(targetSlot).Actor;
        AdmitImpactModel(new(sourceSlot, targetSlot, -1, -1, -1) { ModelAction = actor.HurtAction, PostActionKnockedOut = !IsAlive(Actor(targetSlot)) });
        state = state with { Reaction = new(targetSlot, Tick) };
    }
}
