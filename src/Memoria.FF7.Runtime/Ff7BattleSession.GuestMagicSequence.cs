using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF7.Runtime;

public sealed record Ff7BattleGuestMagicSequence(int ActorSlot, ushort Ability, ImmutableArray<ushort> Targets, bool Group, bool SingleCost, int Next);

public sealed partial class Ff7BattleSession
{
    public IReadOnlyList<int> GuestMagicCastCounts(int slot, ushort ability)
    {
        return GetGuestAttack(slot) is IFf7BattleGuestMagic magic ? magic.MagicCastCounts(ability) : [1];
    }

    public bool GuestMagicCastIsFree(int slot)
    {
        return state.GuestMagicSequences.Any(sequence => sequence.ActorSlot == slot && sequence.SingleCost && sequence.Next > 1);
    }

    public ushort? GuestMagicPrepaidAbility(int slot) => state.GuestMagicSequences.SingleOrDefault(sequence => sequence.ActorSlot == slot && sequence.SingleCost && sequence.Next > 1)?.Ability;

    public bool SubmitGuestMagicSequence(int slot, ushort ability, IReadOnlyList<ushort> targets, bool group = false)
    {
        ArgumentNullException.ThrowIfNull(targets);
        if (targets.Count is < 1 or > 3 || state.GuestMagicSequences.Any(sequence => sequence.ActorSlot == slot) || GetGuestAttack(slot) is not IFf7BattleGuestMagic magic || !magic.MagicCastCounts(ability).Contains(targets.Count))
        {
            return false;
        }
        Ff7BattleCommandOption? option = Commands(slot).SingleOrDefault(choice => choice.Command == Ff7BattleCommand.Magic && choice.Argument == ability);
        if (option is null || option.UnsupportedReason is not null || targets.Any(mask => !ValidGuestMagicSelection(option, mask, group)))
        {
            return false;
        }
        if (!SubmitGuestMagicTargets(slot, targets[0], ability, group))
        {
            return false;
        }
        if (targets.Count > 1)
        {
            state = state with { GuestMagicSequences = state.GuestMagicSequences.Add(new(slot, ability, targets.ToImmutableArray(), group || !option.SelectSingle, magic.MagicCastCost(ability, targets.Count) == 1, 1)) };
        }
        return true;
    }

    private static bool ValidGuestMagicSelection(Ff7BattleCommandOption option, ushort targets, bool group)
    {
        if (targets == 0 || (targets & ~option.ValidTargets) != 0)
        {
            return false;
        }
        if (group || !option.SelectSingle)
        {
            ushort side = option.AllTargets ? (ushort)0x3F7 : (ushort)((targets & 7) != 0 ? 7 : 0x3F0);
            return (!option.SelectSingle || option.GroupUses > 0) && targets == ((option.CandidateTargets ?? option.ValidTargets) & side);
        }
        return BitOperations.IsPow2(targets);
    }

    private void CompleteGuestMagic(Ff7BattleQueuedCommand command)
    {
        if (state.GuestMagicSequences.SingleOrDefault(sequence => sequence.ActorSlot == command.ActorSlot && sequence.Ability == command.GuestAbility) is not { } sequence)
        {
            FinishTurn(command.ActorSlot);
            return;
        }
        IFf7BattleGuestMagic magic = (IFf7BattleGuestMagic)RequireGuestAttack(command);
        if (sequence.Next >= sequence.Targets.Length || Outcome != Ff7BattleOutcome.Fighting || !CanAct(Actor(command.ActorSlot)) || !Ff7GuestBattleStatuses.CanCastMagic(Actor(command.ActorSlot).Actor.Statuses) || !magic.CanRepeatMagic(sequence.Ability, sequence.Next + 1) || !sequence.SingleCost && magic.MagicStock(sequence.Ability) == 0)
        {
            FinishTurn(command.ActorSlot);
            return;
        }
        ushort targets = sequence.Targets[sequence.Next];
        Ff7BattleCommandQueueState queued = Ff7BattleCommandQueue.StageReady(state.Queue, command.ActorSlot, command.CommandId, argument: command.Argument, targetMask: targets, groupTarget: sequence.Group, guestAttack: true, guestAbility: command.GuestAbility);
        state = state with { Queue = queued, GuestMagicSequences = state.GuestMagicSequences.Replace(sequence, sequence with { Next = sequence.Next + 1 }) };
    }

    private bool AdmitsGuestMagicSequence(Ff7BattleQueuedCommand command, IFf7BattleGuestAttack guest)
    {
        return state.GuestMagicSequences.SingleOrDefault(sequence => sequence.ActorSlot == command.ActorSlot && sequence.Ability == command.GuestAbility) is not { Next: > 1 } sequence || guest is IFf7BattleGuestMagic magic && magic.CanRepeatMagic(sequence.Ability, sequence.Next);
    }

    private void CancelGuestMagicSequence(int slot)
    {
        if (state.GuestMagicSequences.Any(sequence => sequence.ActorSlot == slot))
        {
            state = state with { GuestMagicSequences = state.GuestMagicSequences.Where(sequence => sequence.ActorSlot != slot).ToImmutableArray() };
        }
    }

    private static void ValidateGuestMagicSequences(Ff7BattleSnapshot snapshot)
    {
        if (snapshot.GuestMagicSequences.IsDefault || snapshot.GuestMagicSequences.Length > 3 || snapshot.GuestMagicSequences.Any(sequence => sequence is null) || snapshot.GuestMagicSequences.Select(sequence => sequence.ActorSlot).Distinct().Count() != snapshot.GuestMagicSequences.Length)
        {
            throw new InvalidDataException("The guest magic sequences require distinct occupied source slots.");
        }
        foreach (Ff7BattleGuestMagicSequence sequence in snapshot.GuestMagicSequences)
        {
            if (!snapshot.GetGuestSlotStates().Any(guest => guest.Binding.HostSlot == sequence.ActorSlot) || sequence.Ability == 0 || sequence.Targets.IsDefault || sequence.Targets.Length is < 2 or > 3 || sequence.Next < 1 || sequence.Next > sequence.Targets.Length || sequence.Targets.Any(mask => mask == 0 || (mask & ~0x3F7) != 0))
            {
                throw new InvalidDataException("The saved guest magic sequence has invalid source, targets or progress.");
            }
            IEnumerable<Ff7BattleQueuedCommand> commands = snapshot.Queue.Ready.OfType<Ff7BattleQueuedCommand>().Concat(snapshot.Queue.Slots.OfType<Ff7BattleQueueEntry>().Select(entry => entry.Command));
            if (snapshot.Action is { } action)
            {
                commands = commands.Append(action.Command);
            }
            if (commands.Count(command => command.GuestAttack && command.ActorSlot == sequence.ActorSlot && command.GuestAbility == sequence.Ability && command.CommandId == (byte)Ff7BattleCommand.Magic) != 1)
            {
                throw new InvalidDataException("The saved magic sequence has no unique queued or active action.");
            }
        }
    }
}
