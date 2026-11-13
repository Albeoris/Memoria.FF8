using System.Collections.Immutable;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7BattleSession
{
    private void DispatchGuestCompositeAction(Ff7BattleQueuedCommand command, IFf7BattleGuestAttack guest, IFf7BattleGuestCompositeActions composite, Ff7BattleGuestCompositeAction program, Ff7BattleGuestInvocation invocation, bool groupPublication = false)
    {
        if (guest is not IFf7BattleGuestMagic magic)
        {
            throw new InvalidDataException("The composite action lost its origin source owner.");
        }
        if (!CanAct(Actor(command.ActorSlot)) || GuestAbilityRequiresMagic(guest, command.GuestAbility) && !Ff7GuestBattleStatuses.CanCastMagic(Actor(command.ActorSlot).Actor.Statuses))
        {
            FinishTurn(command.ActorSlot);
            return;
        }
        if (program.Phases.Count == 0 || program.Phases.Any(phase => phase is null || phase.Repetitions is < 1 or > 255 || !Enum.IsDefined(phase.TargetPolicy)))
        {
            RejectGuestAbility(command, "The origin action has an unsupported contact schedule.");
            return;
        }
        using IFf7BattleGuestPreparationTransaction? preparation = composite.BeginCompositePreparation(command.GuestAbility);
        Ff7BattleRng candidateRandom = new(kernel[2].Data.Span, random.Capture());
        if (!magic.PrepareSource(command.GuestAbility, candidateRandom.NextByte, out string? failure))
        {
            random.Adopt(candidateRandom);
            state = state with { Messages = Ff7NativeBattleMessage.Enqueue(state.Messages ?? Ff7BattleMessageState.Empty, failure ?? "The source action failed.") };
            FinishTurn(command.ActorSlot);
            return;
        }
        program = composite.DescribeCompositeAction(command.GuestAbility) ?? throw new InvalidDataException("The prepared source lost its composite action description.");
        if (program.Phases.Count == 0 || program.Phases.Any(phase => phase is null || phase.Repetitions is < 1 or > 255 || !Enum.IsDefined(phase.TargetPolicy)))
        {
            throw new InvalidDataException("The prepared source changed to an invalid contact schedule.");
        }
        List<ushort> pools = [];
        foreach (Ff7BattleGuestCompositePhase phase in program.Phases)
        {
            ValidateGuestMagic(command.GuestAbility, phase.Description);
            ushort mask = phase.TargetPolicy == Ff7BattleGuestCompositeTargetPolicy.Source ? (ushort)(1 << command.ActorSlot) : command.TargetMask;
            if (phase.TargetPolicy == Ff7BattleGuestCompositeTargetPolicy.RandomSelected)
            {
                ushort sides = (ushort)(((mask & 7) != 0 ? 7 : 0) | ((mask & 0x3F0) != 0 ? 0x3F0 : 0));
                mask = Mask(state.Actors.Where(actor => (sides & (1 << actor.Actor.Slot)) != 0 && GuestMagicTargetIsLegal(guest, command.GuestAbility, actor)));
            }
            if (mask == 0)
            {
                RejectGuestAbility(command, "The composite action has no eligible target pool.");
                return;
            }
            foreach (Ff7BattleActorState target in state.Actors.Where(actor => (mask & (1 << actor.Actor.Slot)) != 0))
            {
                if (guest is not IFf7BattleGuestAbilities && (!GuestMagicTargetIsLegal(guest, command.GuestAbility, target) || !SupportsGuestAbility(guest, command.GuestAbility, DescribeGuestTarget(target.Actor.Slot))))
                {
                    RejectGuestAbility(command, "The origin ability is unavailable for a selected target.");
                    return;
                }
                if (GuestAbilityTargetUnsupportedReason(command.ActorSlot, Ff7BattleCommand.Magic, command.GuestAbility, target.Actor.Slot) is { } unsupported)
                {
                    RejectGuestAbility(command, unsupported);
                    return;
                }
            }
            pools.Add(mask);
        }
        List<(int Phase, int Hit, ushort Targets)> schedule = [];
        for (int phaseIndex = 0; phaseIndex < program.Phases.Count; phaseIndex++)
        {
            Ff7BattleGuestCompositePhase phase = program.Phases[phaseIndex];
            for (int hit = 0; hit < phase.Repetitions; hit++)
            {
                ushort selected = phase.TargetPolicy == Ff7BattleGuestCompositeTargetPolicy.RandomSelected ? Ff7NativeBattleTargets.ResolveNumericalPass(pools[phaseIndex], 0x80, 0, candidateRandom) : pools[phaseIndex];
                schedule.Add((phaseIndex, hit, selected));
            }
        }
        ImmutableArray<Ff7BattleGuestCompositeContact> contacts = PrepareGuestCompositeContacts(command, invocation, program, schedule, candidateRandom);
        if (contacts.Length is < 1 or > 255)
        {
            RejectGuestAbility(command, "The composite action exceeds the origin contact packet capacity.");
            return;
        }
        if (composite.PrepareCompositeUnsupportedReason(program, contacts) is { } reason)
        {
            RejectGuestAbility(command, reason);
            return;
        }
        composite.PrepareComposite(program, contacts);
        guest.Begin(invocation, VisualPlacement(command.ActorSlot), DescribeGuestTarget(invocation.TargetSlot));
        random.Adopt(candidateRandom);
        preparation?.Commit();
        state = state with { Action = new(command, Tick, -1) { GuestComposite = new(contacts) { GroupPublication = groupPublication } }, GuestAttackUsed = true };
    }

    private ImmutableArray<Ff7BattleGuestCompositeContact> PrepareGuestCompositeContacts(Ff7BattleQueuedCommand command, Ff7BattleGuestInvocation invocation, Ff7BattleGuestCompositeAction program, IReadOnlyList<(int Phase, int Hit, ushort Targets)> schedule, Ff7BattleRng candidateRandom)
    {
        Ff7BattleSnapshot original = state;
        state = state with { Actors = (Ff7BattleActorState[])state.Actors.Clone() };
        ImmutableArray<Ff7BattleGuestCompositeContact>.Builder contacts = ImmutableArray.CreateBuilder<Ff7BattleGuestCompositeContact>();
        try
        {
            foreach ((int phaseIndex, int hit, ushort targets) in schedule)
            {
                Ff7BattleGuestPhysicalHit phaseDescription = program.Phases[phaseIndex].Description;
                foreach (int selected in state.Actors.Where(actor => (targets & (1 << actor.Actor.Slot)) != 0).Select(actor => actor.Actor.Slot).Order())
                {
                    Ff7BattleActorState currentSource = Actor(command.ActorSlot);
                    Ff7BattleGuestPhysicalHit description = phaseDescription with { Source = phaseDescription.Source with { Statuses = currentSource.Actor.Statuses, CurrentHitPoints = currentSource.Actor.HitPoints, OriginSavedStatuses = GetOriginSavedStatuses(command.ActorSlot) } };
                    ReflectionPlan? reflection = PrepareReflections(command with { TargetMask = (ushort)(1 << selected) }, description, candidateRandom, true);
                    ImmutableArray<Ff7BattleGuestCompositeReflection> reflections = reflection is null ? [] : reflection.Counts.Select(pair => new Ff7BattleGuestCompositeReflection(pair.Key, pair.Value, (reflection.RemovedReflect & (1 << pair.Key)) != 0)).ToImmutableArray();
                    if (reflection is not null)
                    {
                        CommitReflections(reflection);
                    }
                    IReadOnlyList<Ff7BattleReflectedContact>? reflectedContacts = reflection?.Contacts;
                    int count = reflectedContacts?.Count ?? 1;
                    for (int index = 0; index < count; index++)
                    {
                        int slot = reflectedContacts?[index].TargetSlot ?? selected;
                        Ff7BattleActorState source = Actor(command.ActorSlot);
                        Ff7BattleActorState target = Actor(slot);
                        Ff7BattleActionVitals sourceVitals = GuestItemVitals(source);
                        Ff7BattleActionVitals targetVitals = GuestItemVitals(target) with { StatusImmunities = (slot >= 4 ? ~Enemy(slot).NativeStatusMask : 0) | target.StatusProtection };
                        Ff7BattleDamageAction attack = reflectedContacts?[index].Attack ?? ProjectGuestOutgoingAttack(slot, description) with { Split = false };
                        Ff7BattleDamageActor sourceStats = description.Source with { Statuses = source.Actor.Statuses, CurrentHitPoints = source.Actor.HitPoints, OriginSavedStatuses = sourceVitals.OriginSavedStatuses };
                        Ff7BattleActionReceipt receipt = reflectedContacts?[index].Receipt ?? Ff7NativeBattleActionResolver.ResolveGuestContact(sourceStats, sourceVitals, Stats(target), targetVitals, attack, description.StatusRecord, candidateRandom, slot == command.ActorSlot, GetGuestProjection(slot)?.OriginStatusResistances, GetGuestBinding(slot) is null ? 0 : GetGuestOriginBattleStatuses(slot), GetGuestBinding(command.ActorSlot) is null ? (ushort)0 : GetGuestOriginSavedStatuses(command.ActorSlot), GetGuestBinding(slot) is null ? (ushort)0 : GetGuestOriginSavedStatuses(slot));
                        receipt = Ff7NativeBattleActionResolver.ApplyPreparedGuestContact(sourceVitals, targetVitals, receipt, attack, slot == command.ActorSlot);
                        Ff7BattleGuestPhysicalHit prepared = description with { Attack = attack, PreparedContact = new(invocation with { TargetSlot = slot }, sourceVitals, targetVitals, receipt) };
                        contacts.Add(new(contacts.Count, phaseIndex, hit, prepared) { Reflected = reflection is not null, Reflections = index == 0 ? reflections : [] });
                        if (slot != command.ActorSlot)
                        {
                            Replace(source with { Actor = source.Actor with { HitPoints = receipt.Source.Hp, MagicPoints = receipt.Source.Mp, Statuses = receipt.Source.Statuses } });
                        }
                        PublishHit(command, target, attack, receipt, runCounters: false);
                        ApplyGuestOriginContact(slot, receipt);
                    }
                }
            }
            return contacts.ToImmutable();
        }
        finally
        {
            state = original;
        }
    }

    private void AdvanceGuestCompositeAction(Ff7BattleActionState active, IFf7BattleGuestAttack guest)
    {
        Ff7BattleGuestCompositeState current = active.GuestComposite ?? throw new InvalidDataException("The composite action lost its contact cursor.");
        Ff7BattleGuestInvocation invocation = GuestInvocation(active.Command, active.StartedAt);
        if ((current.GroupPublication ? guest is not IFf7BattleGuestGroupMagic : guest is not IFf7BattleGuestCompositeActions) || guest.Invocation != invocation || guest.ContactPublished != (current.NextContact != 0))
        {
            throw new InvalidDataException("The composite presentation lost its host queue occurrence.");
        }
        Ff7BattleGuestAttackUpdate update = guest.Advance(Tick);
        if (update.Hit is not null || (current.GroupPublication ? update.CompositeContacts.Count != 0 : update.GroupHits.Count != 0) || update.AttackTargetCue is not null || update.LimitPresentedContact is not null || update.LimitTargetCue is not null)
        {
            throw new InvalidDataException("A composite action published an unowned action cue.");
        }
        IReadOnlyList<int> ordinals = current.GroupPublication ? Enumerable.Range(current.NextContact, update.GroupHits.Count).ToArray() : update.CompositeContacts;
        for (int index = 0; index < ordinals.Count; index++)
        {
            int ordinal = ordinals[index];
            if (ordinal != current.NextContact + index || ordinal >= current.Contacts.Length)
            {
                throw new InvalidDataException("The composite action repeated or reordered a contact occurrence.");
            }
            if (current.GroupPublication && update.GroupHits[index] != current.Contacts[ordinal].HitDescription)
            {
                throw new InvalidDataException("The group action published a different contact occurrence.");
            }
        }
        foreach (int ordinal in ordinals)
        {
            Ff7BattleGuestCompositeContact contact = current.Contacts[ordinal];
            foreach (Ff7BattleGuestCompositeReflection reflection in contact.Reflections)
            {
                Ff7BattleActorState reflector = Actor(reflection.Slot);
                Replace(reflector with { ReflectionCount = reflection.Remaining, Actor = reflector.Actor with { Statuses = reflection.Exhausted ? reflector.Actor.Statuses & ~Ff7NativeBattleStatuses.Reflect : reflector.Actor.Statuses } });
            }
            int target = contact.HitDescription.PreparedContact!.Invocation.TargetSlot;
            ApplyGuestHit(active.Command, target, contact.HitDescription);
            state = state with { DisplayedHit = state.LastHit };
            current = current with { NextContact = current.NextContact + 1 };
        }
        active = active with { GuestComposite = current, Applied = current.NextContact == current.Contacts.Length };
        if (update.Completed)
        {
            if (!active.Applied || guest.Invocation is not null)
            {
                throw new InvalidDataException("The composite presentation completed before settling its contact schedule.");
            }
            PublishGuestSourceInteraction(active.Command, invocation.TargetSlot);
            CompleteGuestMagic(active.Command);
            state = state with { Action = null };
            FlushFormationAi();
        }
        else
        {
            state = state with { Action = active };
        }
    }

    private static void ValidateGuestCompositeSnapshot(Ff7BattleSnapshot snapshot)
    {
        if (snapshot.Action is not { GuestComposite: { } composite } active)
        {
            return;
        }
        if (!active.Command.GuestAttack || active.Command.CommandId != (byte)Ff7BattleCommand.Magic || active.GuestLimit is not null || composite.Contacts.IsDefaultOrEmpty || composite.Contacts.Length > 255 || composite.NextContact < 0 || composite.NextContact > composite.Contacts.Length || active.Applied != (composite.NextContact == composite.Contacts.Length))
        {
            throw new InvalidDataException("The saved composite action has an invalid contact cursor.");
        }
        for (int index = 0; index < composite.Contacts.Length; index++)
        {
            Ff7BattleGuestCompositeContact contact = composite.Contacts[index];
            if (contact is null || contact.Ordinal != index || contact.Phase < 0 || contact.Hit < 0 || contact.HitDescription.PreparedContact is not { } prepared || prepared.Invocation.ActorSlot != active.Command.ActorSlot || prepared.Invocation.StartedAt != active.StartedAt || !snapshot.Actors.Any(actor => actor.Actor.Slot == prepared.Invocation.TargetSlot) || contact.Reflections.IsDefault || contact.Reflections.Any(value => value is null || value.Remaining > 3 || !snapshot.Actors.Any(actor => actor.Actor.Slot == value.Slot)))
            {
                throw new InvalidDataException("The saved composite contact has invalid occurrence ownership.");
            }
        }
    }
}
