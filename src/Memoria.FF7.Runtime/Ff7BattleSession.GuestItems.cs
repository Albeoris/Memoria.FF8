namespace Memoria.FF7.Runtime;

public sealed partial class Ff7BattleSession
{
    public bool SubmitGuestItem(int slot, int targetSlot, ushort item)
    {
        if (Outcome != Ff7BattleOutcome.Fighting || GetGuestAttack(slot) is not IFf7BattleGuestItems { HasOriginItems: true } items || ReadyPlayer != slot || !CanAct(Actor(slot)) || items.ItemStock(item) <= 0 || !state.Actors.Any(actor => actor.Actor.Slot == targetSlot && GuestItemTarget(items, item, actor)))
        {
            return false;
        }
        Ff7BattleCommandQueueState queue = Ff7BattleCommandQueue.StageReady(state.Queue, slot, (byte)Ff7BattleCommand.Item, item, (ushort)(1 << targetSlot), guestAttack: true, guestAbility: item);
        items.ReserveItem(new(RequestId, Tick, slot, targetSlot, item));
        state = state with { Queue = queue };
        Replace(Actor(slot) with { AwaitingTurn = false });
        return true;
    }

    private void CancelGuestItem(Ff7BattleQueuedCommand command)
    {
        if (command is { GuestAttack: true, CommandId: (byte)Ff7BattleCommand.Item })
        {
            IFf7BattleGuestItems items = RequireGuestAttack(command) as IFf7BattleGuestItems ?? throw new InvalidDataException("The cancelled guest item lost its origin inventory owner.");
            items.CancelItem(System.Numerics.BitOperations.TrailingZeroCount(command.TargetMask), command.GuestAbility);
        }
    }

    private IEnumerable<Ff7BattleCommandOption> GuestItemOptions(IFf7BattleGuestItems items)
    {
        foreach (ushort item in items.ItemIds)
        {
            int stock = items.ItemStock(item);
            if (stock <= 0)
            {
                continue;
            }
            ushort targets = Mask(state.Actors.Where(actor => GuestItemTarget(items, item, actor)));
            if (targets != 0)
            {
                yield return new(Ff7BattleCommand.Item, item, targets, true, false, stock);
            }
            else
            {
                yield return new(Ff7BattleCommand.Item, item, 0, true, false, stock) { UnsupportedReason = items.ItemUnsupportedReason(item) ?? "No supported target is available for this item." };
            }
        }
    }

    private bool GuestItemTarget(IFf7BattleGuestItems items, ushort item, Ff7BattleActorState target)
    {
        if (target.Actor.Slot >= 3 || !items.SupportsItem(item, DescribeGuestTarget(target.Actor.Slot)))
        {
            return false;
        }
        bool revive = items.DescribeItem(item).ReviveDivisor != 0;
        return IsAlive(target) != revive && ((target.Actor.Conditions & 8) != 0 || revive);
    }

    private string? PrepareGuestItem(IFf7BattleGuestItems items, Ff7BattleGuestInvocation invocation, Ff7BattleActorState target)
    {
        Ff7BattleGuestItemEffect effect = items.DescribeItem(invocation.GuestAbility);
        Ff7BattleActionVitals sourceVitals = GuestItemVitals(Actor(invocation.ActorSlot));
        Ff7BattleActionVitals targetVitals = GuestItemVitals(target);
        Ff7BattleActionReceipt receipt = Ff7NativeBattleActionResolver.ApplyGuestItem(sourceVitals, targetVitals, effect);
        Ff7BattleGuestPhysicalHit hit = new(default, new(0, 0, 255, Healing: true), 0) { PreparedContact = new(invocation, sourceVitals, targetVitals, receipt), ItemEffect = effect };
        if (items.PrepareItemUnsupportedReason(hit) is { } reason)
        {
            return reason;
        }
        items.PrepareItem(hit);
        return null;
    }

    private void PublishGuestItem(Ff7BattleQueuedCommand command, Ff7BattleActorState source, Ff7BattleActorState target, Ff7BattleGuestPhysicalHit hit)
    {
        Ff7BattleGuestPreparedContact prepared = hit.PreparedContact ?? throw new InvalidDataException("The guest item lost its prepared native recovery receipt.");
        Ff7BattleGuestItemEffect effect = hit.ItemEffect ?? throw new InvalidDataException("The guest item lost its original recovery contract.");
        Ff7BattleActionVitals currentSource = GuestItemVitals(source);
        Ff7BattleActionVitals currentTarget = GuestItemVitals(target);
        if (prepared.Invocation != GuestInvocation(command, state.Action!.StartedAt) || prepared.TargetBefore.MaxHp != currentTarget.MaxHp || prepared.TargetBefore.MaxMp != currentTarget.MaxMp)
        {
            throw new InvalidDataException("The guest item receipt no longer matches its host recipient.");
        }
        Ff7BattleActionReceipt receipt = Ff7NativeBattleActionResolver.ApplyGuestItem(currentSource, currentTarget, effect, prepared.Receipt.Damage);
        PublishHit(command, target, hit.Attack, receipt);
        if (effect.ReviveDivisor != 0 && (receipt.RemovedStatuses & Ff7NativeBattleActionResolver.Death) != 0 && state.ActorModels.Length != 0)
        {
            Ff7BattleActorModelState model = ActorModelState(target.Actor.Slot);
            ReplaceActorModel(model with { PresentedKnockedOut = false });
        }
    }

    private static Ff7BattleActionVitals GuestItemVitals(Ff7BattleActorState actor)
        => new(actor.Actor.HitPoints, actor.Actor.MaxHitPoints, actor.Actor.MagicPoints, actor.Actor.MaxMagicPoints, actor.Actor.Statuses);
}
