namespace Memoria.FF7.Runtime;

public sealed partial class Ff7BattleSession
{
    private bool HasCompelledPlayerTurn(Ff7BattleActorState actor)
    {
        return actor.Actor.Slot < 3 && (GetGuestAttack(actor.Actor.Slot) is IFf7BattleGuestProcess { ProcessOwnsReadyTurn: true } || (actor.Actor.Statuses & (Ff7NativeBattleStatuses.Confusion | 0x800000)) != 0);
    }

    private bool TryStageCompelledPlayerTurn(Ff7BattleActorState actor)
    {
        if (!actor.AwaitingTurn || !HasCompelledPlayerTurn(actor) || !CanAct(actor))
        {
            return false;
        }
        if (GetGuestAttack(actor.Actor.Slot) is IFf7BattleGuestProcess { ProcessOwnsReadyTurn: true })
        {
            return true;
        }
        int slot = actor.Actor.Slot;
        bool guest = GetGuestAttack(slot) is not null;
        state = state with { Queue = Ff7BattleCommandQueue.StageReady(state.Queue, slot, (byte)Ff7BattleCommand.Attack, guestAttack: guest, compelledAttack: guest) };
        Replace(actor with { AwaitingTurn = false });
        return true;
    }

    private Ff7BattleTargetMasks CompelledTargetMasks()
    {
        Ff7BattleAiMasks masks = Ff7BattleAiCallbacks.ProjectMasks(state.Actors.Select(actor => actor.Actor).ToArray(), NativeImprisonedType);
        return new(masks.TurnEligible, masks.Targetable, masks.KnockedOut, masks.Targetable, Mask(state.Actors.Where(actor => FacingGroup(actor.Actor) == 0)), Mask(state.Actors.Where(actor => FacingGroup(actor.Actor) == 2)));
    }
}
