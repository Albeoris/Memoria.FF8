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
            if (command == Ff7BattleCommand.Magic && choice is { TargetFlags: var flags } && (flags & 4) != 0 && guest is not IFf7BattleGuestGroupMagic)
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
        return target.Actor.Slot != 3 && (!revive || target.Actor.Slot < 3) && (revive ? target.Actor.HitPoints == 0 || (target.Actor.Statuses & 1) != 0 : IsAlive(target)) && ((target.Actor.Conditions & 8) != 0 || revive && target.Actor.Slot < 3);
    }

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
            if (guest is not IFf7BattleGuestGroupMagic || option.SelectSingle && option.GroupUses == 0 || targets != ((option.CandidateTargets ?? option.ValidTargets) & side))
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
        int[] targets = state.Actors.Where(actor => (command.TargetMask & (1 << actor.Actor.Slot)) != 0).Select(actor => actor.Actor.Slot).Order().ToArray();
        if (!CanAct(Actor(command.ActorSlot)) || GuestAbilityRequiresMagic(guest, command.GuestAbility) && !Ff7GuestBattleStatuses.CanCastMagic(Actor(command.ActorSlot).Actor.Statuses))
        {
            FinishTurn(command.ActorSlot);
            return;
        }
        foreach (int target in targets)
        {
            if (GuestAbilityTargetUnsupportedReason(command.ActorSlot, Ff7BattleCommand.Magic, command.GuestAbility, target) is { } reason)
            {
                RejectGuestAbility(command, reason);
                return;
            }
        }
        Ff7BattleRng candidateRandom = new(kernel[2].Data.Span, random.Capture());
        if (!magic.PrepareSource(command.GuestAbility, candidateRandom.NextByte, out string? failure))
        {
            random.Adopt(candidateRandom);
            state = state with { Messages = Ff7NativeBattleMessage.Enqueue(state.Messages ?? Ff7BattleMessageState.Empty, failure ?? "The source action failed.") };
            FinishTurn(command.ActorSlot);
            return;
        }
        Ff7BattleGuestPhysicalHit description = magic.DescribeMagic(command.GuestAbility);
        ValidateGuestMagic(command.GuestAbility, description);
        List<Ff7BattleGuestPhysicalHit> prepared = [];
        foreach (int slot in targets)
        {
            Ff7BattleActorState source = Actor(command.ActorSlot);
            Ff7BattleActorState target = Actor(slot);
            Ff7BattleActionVitals sourceVitals = GuestItemVitals(source);
            Ff7BattleActionVitals targetVitals = GuestItemVitals(target) with { StatusImmunities = slot >= 4 ? ~Enemy(slot).NativeStatusMask : 0 };
            Ff7BattleDamageAction attack = ProjectGuestOutgoingAttack(slot, description) with { Split = false };
            Ff7BattleActionReceipt receipt = Ff7NativeBattleActionResolver.ResolveGuestContact(description.Source, sourceVitals, Stats(target), targetVitals, attack, description.StatusRecord, candidateRandom, sameActor: slot == command.ActorSlot, originStatusResistances: GetGuestProjection(slot)?.OriginStatusResistances);
            prepared.Add(description with { Attack = attack, PreparedContact = new(invocation with { TargetSlot = slot }, sourceVitals, targetVitals, receipt) });
        }
        if (group.PrepareGroupUnsupportedReason(prepared) is { } unsupported)
        {
            RejectGuestAbility(command, unsupported);
            return;
        }
        group.PrepareGroup(prepared);
        guest.Begin(invocation, VisualPlacement(command.ActorSlot), DescribeGuestTarget(invocation.TargetSlot));
        random.Adopt(candidateRandom);
        state = state with { Action = new(command, Tick, -1), GuestAttackUsed = true };
    }

    private void AdvanceGuestGroupMagic(Ff7BattleActionState active, IFf7BattleGuestAttack guest)
    {
        Ff7BattleGuestInvocation invocation = GuestInvocation(active.Command, active.StartedAt);
        if (guest.Invocation != invocation)
        {
            throw new InvalidDataException("The group presentation lost its host queue occurrence.");
        }
        Ff7BattleGuestAttackUpdate update = guest.Advance(Tick);
        if (update.Hit is not null || update.AttackTargetCue is not null || update.LimitPresentedContact is not null || update.LimitTargetCue is not null)
        {
            throw new InvalidDataException("A group action published an unowned single-action cue.");
        }
        foreach (Ff7BattleGuestPhysicalHit hit in update.GroupHits)
        {
            Ff7BattleGuestPreparedContact prepared = hit.PreparedContact ?? throw new InvalidDataException("The group contact lost its prepared receipt.");
            ushort bit = checked((ushort)(1 << prepared.Invocation.TargetSlot));
            if ((active.Command.TargetMask & bit) == 0 || (active.GuestAppliedTargets & bit) != 0)
            {
                throw new InvalidDataException("The native group repeated a contact or published an unselected target.");
            }
            ApplyGuestHit(active.Command, prepared.Invocation.TargetSlot, hit);
            state = state with { DisplayedHit = state.LastHit };
            active = active with { GuestAppliedTargets = (ushort)(active.GuestAppliedTargets | bit) };
        }
        active = active with { Applied = active.GuestAppliedTargets == active.Command.TargetMask };
        if (update.Completed)
        {
            if (!active.Applied || guest.Invocation is not null)
            {
                throw new InvalidDataException("The native group completed without settling every selected target exactly once.");
            }
            FinishTurn(active.Command.ActorSlot);
            state = state with { Action = null };
        }
        else
        {
            state = state with { Action = active };
        }
    }
}
