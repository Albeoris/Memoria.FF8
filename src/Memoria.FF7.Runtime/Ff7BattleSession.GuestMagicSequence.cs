using System.Collections.Immutable;
using System.Numerics;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed record Ff7BattleGuestMagicSequence(int ActorSlot, ushort Ability, ImmutableArray<ushort> Targets, bool Group, bool SingleCost, int Next)
{
    public Ff7BattleQueuedCommand? NativeCommand { get; init; }
    public ImmutableArray<bool> GroupSelections { get; init; } = [];
    public int DamageScalePercent { get; init; } = 100;
    public long? InvocationId { get; init; }

    public bool GroupAt(int index) => GroupSelections.IsDefaultOrEmpty ? Group : GroupSelections[index];
}

public sealed partial class Ff7BattleSession
{
    private bool submittingMagicSequence;

    public IReadOnlyList<int> GuestMagicCastCounts(int slot, ushort ability)
    {
        return [SupportsMagicSequence(slot, ability) ? MagicCastCount(slot) : 1];
    }

    private int MagicCastCount(int slot)
    {
        uint statuses = GetOriginBattleStatuses(slot);
        return (statuses & 0x40000) != 0 ? 3 : (statuses & 0x20000) != 0 ? 2 : 1;
    }

    private bool SupportsMagicSequence(int slot, ushort ability)
    {
        return GetGuestAttack(slot) is IFf7BattleGuestMagic magic ? magic.SupportsMagicSequence(ability) : slot < 3 && NativeBattleSpells(slot).Any(spell => spell.AttackId == ability);
    }

    public bool GuestMagicCastIsFree(int slot)
    {
        return state.GuestMagicSequences.Any(sequence => sequence.ActorSlot == slot && sequence.NativeCommand is null && sequence.SingleCost && sequence.Next > 1);
    }

    public ushort? GuestMagicPrepaidAbility(int slot) => state.GuestMagicSequences.SingleOrDefault(sequence => sequence.ActorSlot == slot && sequence.NativeCommand is null && sequence.SingleCost && sequence.Next > 1)?.Ability;

    public bool SubmitGuestMagicSequence(int slot, ushort ability, IReadOnlyList<ushort> targets, bool group = false)
    {
        ArgumentNullException.ThrowIfNull(targets);
        return SubmitMagicSequence(slot, ability, targets, Enumerable.Repeat(group, targets.Count).ToArray());
    }

    public bool SubmitMagicSequence(int slot, ushort ability, IReadOnlyList<ushort> targets, IReadOnlyList<bool>? groups = null)
    {
        ArgumentNullException.ThrowIfNull(targets);
        int maximumCasts = GuestMagicCastCounts(slot, ability)[0];
        if (targets.Count is < 1 or > 3 || groups is not null && groups.Count != targets.Count || state.GuestMagicSequences.Any(sequence => sequence.ActorSlot == slot) || targets.Count > maximumCasts)
        {
            return false;
        }
        Ff7BattleCommandOption? option = Commands(slot).SingleOrDefault(choice => choice.Command == Ff7BattleCommand.Magic && choice.Argument == ability);
        if (option is null || option.UnsupportedReason is not null || targets.Where((mask, index) => !ValidGuestMagicSelection(option, mask, groups?[index] ?? false)).Any())
        {
            return false;
        }
        bool guest = GetGuestAttack(slot) is IFf7BattleGuestMagic;
        int cost = guest ? ((IFf7BattleGuestMagic)GetGuestAttack(slot)!).MagicCastCost(ability, targets.Count) : targets.Count;
        bool singleCost = guest && cost < targets.Count;
        if (guest ? ((IFf7BattleGuestMagic)GetGuestAttack(slot)!).MagicStock(ability) < cost : (long)option.Cost * cost > Actor(slot).Actor.MagicPoints)
        {
            return false;
        }
        int groupUses = targets.Select((mask, index) => groups?[index] == true ? 1 : 0).Sum();
        if (!guest && groupUses > option.GroupUses)
        {
            return false;
        }
        bool submitted;
        int readyIndex = state.Queue.ReadyWrite;
        submittingMagicSequence = true;
        try
        {
            submitted = Submit(slot, Ff7BattleCommand.Magic, ability, targets[0], groups?[0] ?? false);
        }
        finally
        {
            submittingMagicSequence = false;
        }
        if (!submitted)
        {
            return false;
        }
        int damageScale = maximumCasts == 3 ? 75 : 100;
        Ff7BattleQueuedCommand?[] ready = (Ff7BattleQueuedCommand?[])state.Queue.Ready.Clone();
        Ff7BattleQueuedCommand first = ready[readyIndex] ?? throw new InvalidDataException("The submitted Magic sequence has no first queued cast.");
        if (first.ActorSlot != slot || first.CommandId != (byte)Ff7BattleCommand.Magic || first.Argument != ability)
        {
            throw new InvalidDataException("The submitted Magic sequence lost its first command identity.");
        }
        ready[readyIndex] = first with { MagicDamageScalePercent = damageScale, MagicSelectionConfirmed = true };
        state = state with { Queue = state.Queue with { Ready = ready } };
        if (targets.Count > 1)
        {
            long id = checked(state.NextMagicSequenceId + 1);
            ready[readyIndex] = ready[readyIndex]! with { MagicSequenceId = id };
            Ff7BattleQueuedCommand? native = guest ? null : ready[readyIndex];
            Ff7BattleGuestMagicSequence sequence = new(slot, ability, targets.ToImmutableArray(), guest && !option.SelectSingle, singleCost, 1)
            {
                InvocationId = id,
                NativeCommand = native,
                GroupSelections = targets.Select((_, index) => (groups?[index] ?? false) || guest && !option.SelectSingle).ToImmutableArray(),
                DamageScalePercent = damageScale
            };
            state = state with { Queue = state.Queue with { Ready = ready }, NextMagicSequenceId = id, GuestMagicSequences = state.GuestMagicSequences.Add(sequence) };
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

    private bool NativeMagicCanRepeat(Ff7BattleQueuedCommand command)
    {
        if (command.GuestAttack || command.Priority < 2 || command.StatusCountdown || command.Reflected || command.MagicSelectionConfirmed)
        {
            return false;
        }
        if (command.CommandId == (byte)Ff7BattleCommand.Magic)
        {
            return true;
        }
        return command.CommandId == 0x20 && Ff7BattleDamageAction.FromAttack(Data.Attacks.Single(attack => attack.Slot == command.Argument && attack.Id == command.AttackId).Data).NativePhysicalCategory == false;
    }

    private bool DeferMagicSequenceCommand(Ff7BattleQueuedCommand command)
    {
        if (command.Priority < 2 || command.StatusCountdown || command.CommandId == 0x23)
        {
            return false;
        }
        Ff7BattleGuestMagicSequence? sequence = state.GuestMagicSequences.SingleOrDefault(sequence => sequence.ActorSlot == command.ActorSlot);
        if (sequence is null || MatchesMagicSequence(sequence, command))
        {
            return false;
        }
        state = state with { Queue = Ff7BattleCommandQueue.Enqueue(state.Queue, command) };
        return true;
    }

    private bool AdmitNativeMagicSequence(ref Ff7BattleQueuedCommand command)
    {
        int actorSlot = command.ActorSlot;
        Ff7BattleGuestMagicSequence? sequence = state.GuestMagicSequences.SingleOrDefault(sequence => sequence.ActorSlot == actorSlot);
        if (sequence is not null)
        {
            if (MatchesMagicSequence(sequence, command) && !AdmitsMagicSequence(sequence))
            {
                FinishTurn(command.ActorSlot);
                return false;
            }
            return true;
        }
        int count = MagicCastCount(command.ActorSlot);
        if (count > 1 && NativeMagicCanRepeat(command))
        {
            if (!CanAct(Actor(command.ActorSlot)) || !Ff7GuestBattleStatuses.CanCastMagic(Actor(command.ActorSlot).Actor.Statuses))
            {
                FinishTurn(command.ActorSlot);
                return false;
            }
            long id = checked(state.NextMagicSequenceId + 1);
            command = command with { MagicSequenceId = id, MagicDamageScalePercent = count == 3 ? 75 : 100 };
            state = state with
            {
                NextMagicSequenceId = id,
                GuestMagicSequences = state.GuestMagicSequences.Add(new(command.ActorSlot, command.Argument, Enumerable.Repeat(command.TargetMask, count).ToImmutableArray(), command.GroupTarget, false, 1)
                {
                    InvocationId = id,
                    NativeCommand = command,
                    DamageScalePercent = count == 3 ? 75 : 100
                })
            };
        }
        return true;
    }

    private static bool MatchesMagicSequence(Ff7BattleGuestMagicSequence sequence, Ff7BattleQueuedCommand command)
    {
        return command.Priority >= 2 && sequence.ActorSlot == command.ActorSlot && (sequence.InvocationId is not { } id || command.MagicSequenceId == id) && (sequence.NativeCommand is { } native ? !command.GuestAttack && native.CommandId == command.CommandId && native.Argument == command.Argument && native.AttackId == command.AttackId : command.GuestAttack && command.CommandId == (byte)Ff7BattleCommand.Magic && sequence.Ability == command.GuestAbility);
    }

    private bool AdmitsMagicSequence(Ff7BattleGuestMagicSequence sequence)
    {
        Ff7BattleActorState source = Actor(sequence.ActorSlot);
        return Outcome == Ff7BattleOutcome.Fighting && CanAct(source) && Ff7GuestBattleStatuses.CanCastMagic(source.Actor.Statuses) && MagicCastCount(sequence.ActorSlot) >= sequence.Targets.Length;
    }

    private Ff7BattleDamageAction ApplyMagicSequenceDamage(Ff7BattleQueuedCommand command, Ff7BattleDamageAction action)
    {
        Ff7BattleGuestMagicSequence? sequence = state.GuestMagicSequences.SingleOrDefault(sequence => MatchesMagicSequence(sequence, command));
        int scale = command.MagicDamageScalePercent != 100 ? command.MagicDamageScalePercent : sequence?.DamageScalePercent ?? 100;
        return scale == 100 ? action : action with { DamageScalePercent = checked(action.DamageScalePercent * scale / 100) };
    }

    private void StageGuestProcessAction(int slot, Ff7BattleGuestProcessAction action)
    {
        int count = action.OrdinaryMagic && action.Command == Ff7BattleCommand.Magic ? GuestMagicCastCounts(slot, action.Ability)[0] : 1;
        long? invocation = count > 1 ? checked(state.NextMagicSequenceId + 1) : null;
        int scale = count == 3 ? 75 : action.DamageScalePercent;
        Ff7BattleCommandQueueState queue = Ff7BattleCommandQueue.StageReady(state.Queue, slot, (byte)action.Command, argument: action.Ability, targetMask: action.Targets, groupTarget: action.Group, guestAttack: true, guestAbility: action.Ability, magicSequenceId: invocation, magicDamageScalePercent: scale);
        state = state with { Queue = queue };
        if (invocation is long id)
        {
            Ff7BattleGuestMagicSequence sequence = new(slot, action.Ability, Enumerable.Repeat(action.Targets, count).ToImmutableArray(), action.Group, action.FreeMagic, 1) { InvocationId = id, DamageScalePercent = scale };
            state = state with { NextMagicSequenceId = id, GuestMagicSequences = state.GuestMagicSequences.Add(sequence) };
        }
    }

    private void CompleteGuestMagic(Ff7BattleQueuedCommand command)
    {
        Ff7BattleGuestMagicSequence? sequence = state.GuestMagicSequences.SingleOrDefault(sequence => MatchesMagicSequence(sequence, command));
        if (sequence is null && state.GuestMagicSequences.Any(sequence => sequence.ActorSlot == command.ActorSlot))
        {
            return;
        }
        if (sequence is null || sequence.Next >= sequence.Targets.Length || !AdmitsMagicSequence(sequence))
        {
            CancelGuestMagicSequence(command.ActorSlot);
            if (!CompleteGuestProcess(command))
            {
                FinishTurn(command.ActorSlot);
            }
            return;
        }
        ushort targets = sequence.Targets[sequence.Next];
        bool group = sequence.GroupAt(sequence.Next);
        Ff7BattleCommandQueueState queue;
        if (sequence.NativeCommand is { } native)
        {
            Ff7BattleAttack record = native.CommandId == 0x20 ? Data.Attacks.Single(attack => attack.Slot == native.Argument && attack.Id == native.AttackId).Data : command.ActorSlot < 3 ? NativeBattleSpells(command.ActorSlot).Single(spell => spell.AttackId == command.Argument).Attack : ReadKernelAttack(command.Argument);
            if ((Actor(command.ActorSlot).Actor.Conditions & 0x400) == 0 && record.CastingCost > Actor(command.ActorSlot).Actor.MagicPoints || group && command.ActorSlot < 3 && (Actor(command.ActorSlot).AllUses?.GetValueOrDefault(checked((byte)command.Argument)) ?? 0) == 0)
            {
                FinishTurn(command.ActorSlot);
                return;
            }
            queue = Ff7BattleCommandQueue.Enqueue(state.Queue, native with { Priority = 2, TargetMask = targets, GroupTarget = group });
        }
        else
        {
            IFf7BattleGuestMagic magic = (IFf7BattleGuestMagic)RequireGuestAttack(command);
            if (!sequence.SingleCost && magic.MagicStock(sequence.Ability) == 0)
            {
                FinishTurn(command.ActorSlot);
                return;
            }
            queue = Ff7BattleCommandQueue.StageReady(state.Queue, command.ActorSlot, command.CommandId, argument: sequence.Ability, targetMask: targets, groupTarget: group, guestAttack: true, guestAbility: sequence.Ability, magicSequenceId: sequence.InvocationId, magicDamageScalePercent: sequence.DamageScalePercent);
        }
        state = state with { Queue = queue, GuestMagicSequences = state.GuestMagicSequences.Replace(sequence, sequence with { Next = sequence.Next + 1 }) };
    }

    private bool AdmitsGuestMagicSequence(Ff7BattleQueuedCommand command, IFf7BattleGuestAttack guest)
    {
        Ff7BattleGuestMagicSequence? sequence = state.GuestMagicSequences.SingleOrDefault(sequence => MatchesMagicSequence(sequence, command));
        return sequence is null || AdmitsMagicSequence(sequence);
    }

    private void CancelGuestMagicSequence(int slot)
    {
        if (state.GuestMagicSequences.Any(sequence => sequence.ActorSlot == slot))
        {
            state = state with { GuestMagicSequences = state.GuestMagicSequences.Where(sequence => sequence.ActorSlot != slot).ToImmutableArray() };
        }
    }

    private void CancelUnavailableMagicSequences()
    {
        foreach (Ff7BattleGuestMagicSequence sequence in state.GuestMagicSequences)
        {
            if (state.Action is { } active && MatchesMagicSequence(sequence, active.Command) || AdmitsMagicSequence(sequence) && HasLivingParty() && HasLivingEnemy())
            {
                continue;
            }
            Ff7BattleQueueEntry?[] slots = state.Queue.Slots.Select(entry => entry is not null && MatchesMagicSequence(sequence, entry.Command) ? null : entry).ToArray();
            Ff7BattleQueuedCommand?[] ready = new Ff7BattleQueuedCommand?[state.Queue.Ready.Length];
            int write = state.Queue.ReadyRead;
            for (int read = state.Queue.ReadyRead; read != state.Queue.ReadyWrite; read = (read + 1) % ready.Length)
            {
                Ff7BattleQueuedCommand command = state.Queue.Ready[read]!;
                if (!MatchesMagicSequence(sequence, command))
                {
                    ready[write] = command;
                    write = (write + 1) % ready.Length;
                }
            }
            state = state with { Queue = state.Queue with { Slots = slots, Ready = ready, ReadyWrite = write } };
            FinishTurn(sequence.ActorSlot);
        }
    }

    private static void ValidateGuestMagicSequences(Ff7BattleSnapshot snapshot)
    {
        if (snapshot.NextMagicSequenceId < 0 || snapshot.GuestMagicSequences.IsDefault || snapshot.GuestMagicSequences.Length > 9 || snapshot.GuestMagicSequences.Any(sequence => sequence is null) || snapshot.GuestMagicSequences.Select(sequence => sequence.ActorSlot).Distinct().Count() != snapshot.GuestMagicSequences.Length)
        {
            throw new InvalidDataException("Magic sequences require distinct occupied source slots.");
        }
        foreach (Ff7BattleGuestMagicSequence sequence in snapshot.GuestMagicSequences)
        {
            bool guest = sequence.NativeCommand is null;
            if (!snapshot.Actors.Any(actor => actor.Actor.Slot == sequence.ActorSlot) || sequence.InvocationId is { } id && (id <= 0 || id > snapshot.NextMagicSequenceId) || guest && (!snapshot.GetGuestSlotStates().Any(slot => slot.Binding.HostSlot == sequence.ActorSlot) || sequence.Ability == 0) || sequence.Targets.IsDefault || sequence.Targets.Length is < 2 or > 3 || sequence.Next < 1 || sequence.Next > sequence.Targets.Length || sequence.Targets.Any(mask => (guest || sequence.ActorSlot < 3) && mask == 0 || (mask & ~0x3F7) != 0) || sequence.DamageScalePercent is not (75 or 100) || !sequence.GroupSelections.IsDefaultOrEmpty && sequence.GroupSelections.Length != sequence.Targets.Length)
            {
                throw new InvalidDataException("A saved magic sequence has invalid source, targets or progress.");
            }
            if (sequence.NativeCommand is { } native)
            {
                Ff7BattleCommandQueue.ValidateCommand(native);
                if (native.ActorSlot != sequence.ActorSlot || native.CommandId is not (2 or 0x20) || native.Argument != sequence.Ability || native.GuestAttack || native.MagicSequenceId != sequence.InvocationId)
                {
                    throw new InvalidDataException("A saved native magic sequence has no matching source command.");
                }
            }
            IEnumerable<Ff7BattleQueuedCommand> commands = snapshot.Queue.Ready.OfType<Ff7BattleQueuedCommand>().Concat(snapshot.Queue.Slots.OfType<Ff7BattleQueueEntry>().Select(entry => entry.Command));
            if (snapshot.Action is { } action)
            {
                commands = commands.Append(action.Command);
            }
            if (commands.Count(command => MatchesMagicSequence(sequence, command)) != 1)
            {
                throw new InvalidDataException("A saved magic sequence has no unique queued or active action.");
            }
        }
        IEnumerable<Ff7BattleQueuedCommand> ownedCommands = snapshot.Queue.Ready.OfType<Ff7BattleQueuedCommand>().Concat(snapshot.Queue.Slots.OfType<Ff7BattleQueueEntry>().Select(entry => entry.Command));
        if (snapshot.Action is { } currentAction)
        {
            ownedCommands = ownedCommands.Append(currentAction.Command);
        }
        if (ownedCommands.Any(command => command.MagicSequenceId is { } id && !snapshot.GuestMagicSequences.Any(sequence => sequence.InvocationId == id && MatchesMagicSequence(sequence, command))))
        {
            throw new InvalidDataException("A queued magic continuation has no source occurrence owner.");
        }
    }
}
