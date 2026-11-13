using System.Numerics;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7BattleSession
{
    public string? GuestAbilityTargetUnsupportedReason(int slot, Ff7BattleCommand command, ushort ability, int targetSlot)
    {
        if (GetGuestAttack(slot) is not { } guest)
        {
            return null;
        }
        if (targetSlot is < 0 or 3 or > 9 || !state.Actors.Any(actor => actor.Actor.Slot == targetSlot))
        {
            return "The selected actor is not present.";
        }
        Ff7BattleGuestAttackTarget target = DescribeGuestTarget(targetSlot);
        if (command == Ff7BattleCommand.Item && guest is IFf7BattleGuestItems { HasOriginItems: true } items)
        {
            return GuestItemTarget(items, ability, Actor(targetSlot)) ? null : "The original item cannot be applied to this target.";
        }
        if (command == Ff7BattleCommand.Attack)
        {
            return guest.Supports(target) ? null : "The original attack cannot reach this target.";
        }
        if (guest is IFf7BattleGuestAbilities catalog)
        {
            if (command == Ff7BattleCommand.Magic && !GuestMagicTargetIsLegal(guest, ability, Actor(targetSlot)))
            {
                return "The selected target does not meet the original ability's living or knocked-out target rule.";
            }
            Ff7BattleGuestAbility? choice = catalog.Abilities.SingleOrDefault(choice => choice.Command == command && choice.Ability == ability);
            if (command == Ff7BattleCommand.Magic && choice is { TargetFlags: var flags } && (flags & 4) != 0 && guest is not IFf7BattleGuestGroupMagic && guest is not IFf7BattleGuestCompositeActions)
            {
                return "The origin has not supplied a group action bridge.";
            }
            return catalog.TargetUnsupportedReason(command, ability, target);
        }
        return null;
    }

    private bool GuestMagicTargetIsLegal(IFf7BattleGuestAttack guest, ushort ability, Ff7BattleActorState target)
    {
        Ff7BattleGuestAbility? choice = (guest as IFf7BattleGuestAbilities)?.Abilities.SingleOrDefault(choice => choice.Command == Ff7BattleCommand.Magic && choice.Ability == ability);
        bool revive = choice?.CanTargetDead == true;
        return target.Actor.Slot != 3 && (revive ? GuestRevivalTarget(target) : IsAlive(target)) && ((target.Actor.Conditions & 8) != 0 || revive && target.Actor.Slot < 3);
    }

    private bool GuestRevivalTarget(Ff7BattleActorState target)
        => target.Actor.Slot < 3 && (target.Actor.HitPoints == 0 || (target.Actor.Statuses & 1) != 0) || IsAlive(target) && Ff7OriginSavedStatusRules.IsZombie(GetOriginSavedStatuses(target.Actor.Slot));

    private bool SubmitGuestMagicTargets(int slot, ushort targets, ushort ability, bool group)
    {
        if (ability == 0 || Outcome != Ff7BattleOutcome.Fighting || GetGuestAttack(slot) is not IFf7BattleGuestAttack guest || ReadyPlayer != slot || !CanAct(Actor(slot)))
        {
            return false;
        }
        Ff7BattleCommandOption? option = Commands(slot).SingleOrDefault(choice => choice.Command == Ff7BattleCommand.Magic && choice.Argument == ability);
        if (option is null || option.UnsupportedReason is not null || targets == 0 || (targets & ~option.ValidTargets) != 0)
        {
            return false;
        }
        bool grouped = group || !option.SelectSingle;
        if (grouped)
        {
            ushort side = option.AllTargets ? (ushort)0x3F7 : (ushort)((targets & 7) != 0 ? 7 : 0x3F0);
            if (guest is not IFf7BattleGuestGroupMagic && guest is not IFf7BattleGuestCompositeActions || option.SelectSingle && option.GroupUses == 0 || targets != ((option.CandidateTargets ?? option.ValidTargets) & side))
            {
                return false;
            }
        }
        else if (!BitOperations.IsPow2(targets))
        {
            return false;
        }
        state = state with { Queue = Ff7BattleCommandQueue.StageReady(state.Queue, slot, (byte)Ff7BattleCommand.Magic, argument: ability, targetMask: targets, groupTarget: grouped, guestAttack: true, guestAbility: ability) };
        Replace(Actor(slot) with { AwaitingTurn = false });
        return true;
    }

    private void DispatchGuestGroupMagic(Ff7BattleQueuedCommand command, IFf7BattleGuestAttack guest, Ff7BattleGuestInvocation invocation)
    {
        if (guest is not IFf7BattleGuestGroupMagic group || guest is not IFf7BattleGuestMagic magic)
        {
            throw new InvalidDataException("The queued group has no origin group action owner.");
        }
        Ff7BattleGuestCompositeAction program = new([new(magic.DescribeMagic(command.GuestAbility))]);
        DispatchGuestCompositeAction(command, guest, new GuestGroupCompositeAdapter(group, magic, program), program, invocation, groupPublication: true);
    }

    private sealed class GuestGroupCompositeAdapter(IFf7BattleGuestGroupMagic group, IFf7BattleGuestMagic magic, Ff7BattleGuestCompositeAction program) : IFf7BattleGuestCompositeActions
    {
        public Ff7BattleGuestCompositeAction DescribeCompositeAction(ushort ability) => program with { Phases = [new(magic.DescribeMagic(ability))] };

        public string? PrepareCompositeUnsupportedReason(Ff7BattleGuestCompositeAction action, IReadOnlyList<Ff7BattleGuestCompositeContact> contacts)
        {
            return group.PrepareGroupUnsupportedReason(contacts.Select(contact => contact.HitDescription).ToArray());
        }

        public void PrepareComposite(Ff7BattleGuestCompositeAction action, IReadOnlyList<Ff7BattleGuestCompositeContact> contacts)
        {
            group.PrepareGroup(contacts.Select(contact => contact.HitDescription).ToArray());
        }
    }
}
