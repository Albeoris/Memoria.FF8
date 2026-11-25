namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeCharacterVitals? magicVitals;
    public NativeCharacterVitals? MagicVitals => magicVitals?.DeepCopy();
    public bool InfiniteMagic => magicVitals?.MagicSystem.InfiniteMagic == true && !UnlimitedMagic;
    public bool UnlimitedMagic => magicVitals?.MagicSystem.UnlimitedMagic ?? definition.Tuning.UnlimitedMagic;

    public int GetMagicStockCapacity(string actorId, int spellId)
    {
        BattleCombatant actor = GetActor(actorId);
        if (magicVitals is not null && actor.MagicRecordId is int row)
        {
            return MagicInventoryResolver.Resolve(magicVitals, row).Capacity(spellId);
        }
        IReadOnlyList<NativeMagicStock> inventory = actor.NativeMagicSlots;
        NativeMagicStock? entry = inventory.FirstOrDefault(stock => stock.SpellId == spellId);
        if (entry is not null)
        {
            return Math.Max(0, 100 - entry.Count);
        }
        return inventory.Any(stock => stock.SpellId == 0) ? 100 : 0;
    }

    public int? GetSharedMagicJunctionOwnerCharacterId(string actorId, int spellId)
    {
        if (magicVitals?.MagicSystem.SharedMagicStock != true)
        {
            return null;
        }
        int? owner = GetMagicJunctionOwner(actorId, spellId);
        return owner is int row ? magicVitals.Records[row].CharacterId : null;
    }

    public int? GetMagicJunctionOwner(string actorId, int spellId)
    {
        if (magicVitals is null)
        {
            return null;
        }
        if (magicVitals.MagicSystem.SharedMagicStock)
        {
            return MagicJunctionPolicy.GetOwner(magicVitals, spellId);
        }
        int? row = GetActor(actorId).MagicRecordId;
        return row is int owner && Enumerable.Range(0, 19).Any(slot => magicVitals.Records[owner].Junction.MagicJunctions[slot] == spellId && magicVitals.JunctionMagicSlotAvailable(owner, slot)) ? owner : null;
    }

    private void InitializeBattleMagic(NativeCharacterVitals? source, bool newEncounter)
    {
        magicVitals = source?.DeepCopy();
        if (magicVitals is null)
        {
            return;
        }
        magicVitals.Validate();
        magicVitals.ValidateMagicSystemConsistency();
        if (newEncounter)
        {
            magicVitals = MagicCastingService.BeginEncounter(magicVitals, Guid.NewGuid().ToString("N"));
        }
        foreach (BattleCombatant actor in combatants.Where(actor => actor.IsPlayer && actor.MagicRecordId is not null))
        {
            BindBattleMagicOwner(actor);
        }
    }

    private void BindBattleMagicOwner(BattleCombatant actor)
    {
        actor.MagicOwner = () => magicVitals!;
        actor.SetMagicOwner = next =>
        {
            magicVitals = next;
            foreach (BattleCombatant player in allies)
            {
                ReprojectNativePlayerStatistics(player);
            }
        };
    }

    public MagicCastQuote QuoteMagic(string actorId, int spellId)
    {
        BattleCombatant actor = GetActor(actorId);
        if (magicVitals is not null && actor.MagicRecordId is int row && spellId is >= 1 and <= 56)
        {
            return MagicCastingService.Quote(magicVitals, row, spellId, MagicCastContext.Battle);
        }
        int stock = actor.MagicStocks.FirstOrDefault(stock => stock.Id == spellId)?.Count ?? 0;
        return new(actor.IsPlayer && UnlimitedMagic && stock > 0 ? int.MaxValue : stock, stock, stock > 0);
    }

    public int GetAvailableMagicCasts(string actorId, int spellId, string? actionId = null)
    {
        BattleCombatant actor = GetActor(actorId);
        return magicVitals is not null && actor.MagicRecordId is int row && spellId is >= 1 and <= 56 ? MagicCastingService.Quote(magicVitals, row, spellId, MagicCastContext.Battle, actionId).Available : QuoteMagic(actorId, spellId).Available;
    }

    private int MagicCost(BattleCombatant actor, int casts)
    {
        uint flags = actor.NativePlayerStatRules?.NativeStatusPassiveFlags ?? 0;
        return NativeBattleMagicRepetition.Cost(casts, flags, magicVitals?.MagicSystem.InfiniteMagic == true, magicVitals?.MagicSystem.UnlimitedMagic == true);
    }

    private int MagicCost(BattleCombatant actor, BattleAttackCommand command)
    {
        int targets = Math.Max(1, command.MagicCasts.Length);
        return command.NativeMagicCastMode == 0 ? MagicCost(actor, targets) : Math.Min(targets, MagicCost(actor, command.NativeMagicCastMode));
    }

    private BattleAttackCommand EnsureMagicReservation(BattleCombatant actor, BattleAttackCommand command)
    {
        if (magicVitals is null || actor.MagicRecordId is not int row || !actor.IsPlayer || command.FreeNativeMagic || command.Ability == BattleAbility.Draw || command.MagicReservationId is not null || command.Ability != BattleAbility.Magic && command.NativeAction?.Command != 2)
        {
            return command;
        }
        int spell = command.NativeAction?.ActionId ?? command.ActionId;
        if (spell is < 1 or > 56)
        {
            throw new InvalidOperationException("Inventory magic requires a supported stock identity.");
        }
        string identity = Guid.NewGuid().ToString("N");
        int count = Math.Min(MagicCost(actor, command), GetAvailableMagicCasts(actor.Id, spell));
        magicVitals = MagicCastingService.Reserve(magicVitals, row, spell, MagicCastContext.Battle, identity, count);
        return command with { MagicReservationId = identity };
    }

    private void CommitMagicReservation(string? identity, int performedCount = 1)
    {
        if (identity is not null && magicVitals is not null)
        {
            magicVitals = MagicCastingService.Commit(magicVitals, identity, performedCount);
            foreach (BattleCombatant player in combatants.Where(candidate => candidate.IsPlayer))
            {
                ReprojectNativePlayerStatistics(player);
            }
        }
    }

    private void ReleaseMagicReservation(string? identity)
    {
        if (identity is not null && magicVitals is not null)
        {
            magicVitals = MagicCastingService.Release(magicVitals, identity);
        }
    }

    private void ValidateBattleMagicSnapshot(BattleActionPresentation? settledHostedRecovery, string? hostedReservation)
    {
        if (magicVitals is null)
        {
            return;
        }
        magicVitals.ValidateMagicSystemConsistency();
        if (string.IsNullOrWhiteSpace(magicVitals.MagicSystem.EncounterId))
        {
            throw new InvalidDataException("A saved battle magic ledger requires its genuine encounter identity.");
        }
        if (definition.MagicVitals is { } initial)
        {
            MagicSystemState before = initial.MagicSystem;
            MagicSystemState current = magicVitals.MagicSystem;
            if (before.SharedMagicStock != current.SharedMagicStock || before.InfiniteMagic != current.InfiniteMagic || before.UnlimitedMagic != current.UnlimitedMagic || before.HistoriesInitialized != current.HistoriesInitialized || !before.AttackModes.SequenceEqual(current.AttackModes) || !before.SharedFieldMasks.SequenceEqual(current.SharedFieldMasks) || !before.PersonalFieldMasks.Zip(current.PersonalFieldMasks).All(pair => pair.First.SequenceEqual(pair.Second)))
            {
                throw new InvalidDataException("A battle checkpoint cannot change its saved magic rules or field histories.");
            }
            if (definition.Combatants.Any(actor => actor.MagicRecordId is int row && initial.Records[row].CharacterId != magicVitals.Records[row].CharacterId))
            {
                throw new InvalidDataException("A saved magic bank cannot change the current caster identity or profile.");
            }
        }
        bool NeedsReservation(string actorId, BattleAbility ability, BattleNativeAction? native, bool free, BattleGuardianFormAction? formAction)
        {
            bool innate = formAction is { } binding && GetGuardianForm(actorId) is { EntryCompleted: true } form && ReadGuardianFormBinding(form, binding).Cost == BattleGuardianFormActionCost.Innate;
            if (free && !(innate && ability == BattleAbility.Magic && native?.Command == 2 || ability == BattleAbility.Magic && native?.Command == 247 || ability == BattleAbility.EnemyAbility && native?.Command == 2 && (NativeBattleStatusBits(GetActor(actorId)) & 0x2000000) != 0))
            {
                throw new InvalidDataException("A saved free spell requires its original reflected or Angel Wing source.");
            }
            return !free && ability != BattleAbility.Draw && GetActor(actorId).MagicRecordId is not null && (ability == BattleAbility.Magic || native?.Command == 2);
        }
        List<(string? Identity, string Actor, int Spell, bool Required)> commands = actions.Select(command => (command.MagicReservationId, command.ActorId, (int)(command.NativeAction?.ActionId ?? command.ActionId), NeedsReservation(command.ActorId, command.Ability, command.NativeAction, command.FreeNativeMagic, command.GuardianFormAction))).ToList();
        if (hostedReservation is not null && magicVitals.MagicSystem.Reservations.Any(reservation => reservation.ActionId == hostedReservation))
        {
            throw new InvalidDataException("A hosted completion cannot carry an unsettled magic reservation.");
        }
        if (ActiveAction is { } active)
        {
            bool required = NeedsReservation(active.ActorId, active.Ability, active.NativeAction, active.FreeNativeMagic, active.GuardianFormAction);
            bool settled = hostedReservation is not null && ReferenceEquals(active, settledHostedRecovery) && active is { Phase: BattleActionPhase.Recovery, MagicReservationId: null };
            commands.Add((active.MagicReservationId, active.ActorId, active.NativeAction?.ActionId ?? active.ActionId, required && !settled));
        }
        if (nativeTimedAction is { } timed)
        {
            BattleActionPresentation action = timed.Command;
            commands.Add((action.MagicReservationId, action.ActorId, action.NativeAction?.ActionId ?? action.ActionId, NeedsReservation(action.ActorId, action.Ability, action.NativeAction, action.FreeNativeMagic, action.GuardianFormAction)));
        }
        if (commands.Any(command => command.Required && command.Identity is null))
        {
            throw new InvalidDataException("A saved ordinary spell requires its original cast reservation identity.");
        }
        foreach (BattleAttackCommand command in actions.Where(command => command.MagicReservationId is not null))
        {
            MagicCastReservation? reservation = magicVitals.MagicSystem.Reservations.FirstOrDefault(candidate => candidate.ActionId == command.MagicReservationId);
            int cost = Math.Min(MagicCost(GetActor(command.ActorId), command), GetAvailableMagicCasts(command.ActorId, command.ActionId, command.MagicReservationId));
            if (reservation is null || reservation.Count != cost)
            {
                throw new InvalidDataException("A saved repeated spell requires its complete quoted cast reservation.");
            }
        }
        foreach (MagicCastReservation reservation in magicVitals.MagicSystem.Reservations)
        {
            if (reservation.Context != MagicCastContext.Battle || !commands.Any(command => command.Identity == reservation.ActionId && command.Spell == reservation.SpellId && GetActor(command.Actor).MagicRecordId == reservation.ActorRow))
            {
                throw new InvalidDataException("A saved battle reservation lacks its exact queued spell and physical actor record.");
            }
        }
        foreach (var command in commands.Where(command => command.Identity is not null))
        {
            if (!magicVitals.MagicSystem.Reservations.Any(reservation => reservation.ActionId == command.Identity) && !magicVitals.MagicSystem.CommittedActionIds.Contains(command.Identity!, StringComparer.Ordinal))
            {
                throw new InvalidDataException("A queued spell lost its saved resource identity.");
            }
        }
    }

    private bool CanExecuteMagicReservation(BattleCombatant actor, BattleAttackCommand command, bool admitSilencedInvocation = false)
    {
        if (command.MagicReservationId is null || magicVitals is null || actor.MagicRecordId is not int row)
        {
            return true;
        }
        MagicCastReservation? reservation = magicVitals.MagicSystem.Reservations.FirstOrDefault(candidate => candidate.ActionId == command.MagicReservationId);
        if (reservation is null)
        {
            return false;
        }
        if (command.MagicCasts.Length > 1)
        {
            return GetAvailableMagicCasts(actor.Id, command.ActionId, reservation.ActionId) > 0 && (NativeSavedStatusBits(actor) & 0x10) == 0 && actor.Abilities.Contains(BattleAbility.Magic);
        }
        uint statuses = NativeBattleStatusBits(actor);
        int mode = command.NativeMagicCastMode == 0 ? command.MagicCasts.Length : command.NativeMagicCastMode;
        int casts = mode >= 3 && (statuses & 0x40000) != 0 ? 3 : command.MagicCasts.Length >= 2 && (statuses & 0x20000) != 0 ? 2 : 1;
        MagicCastQuote quote = MagicCastingService.Quote(magicVitals, row, reservation.SpellId, MagicCastContext.Battle, reservation.ActionId);
        return quote.Available >= Math.Min(MagicCost(actor, casts), reservation.Count) && (command.Ability != BattleAbility.Magic || (admitSilencedInvocation || (NativeSavedStatusBits(actor) & 0x10) == 0) && (actor.Abilities.Contains(BattleAbility.Magic) || HasRetainedReservedCommand(actor, command.Ability, command.NativeQueuedCommand)));
    }

    private BattleAttackCommand RevalidateSmartAttack(BattleCombatant actor, BattleAttackCommand command)
    {
        if (command.SmartAttack is not { } confirmed)
        {
            return command;
        }
        BattleCombatant target = GetActor(confirmed.TargetId);
        BattleSmartAttackPreview now = SelectSmartAttack(actor, target, confirmed.ForcePhysical, command.MagicReservationId);
        bool same = now.TargetSlot == confirmed.TargetSlot && target.IsAlive && target.IsActive && !target.Untargetable && now.Ability == confirmed.Ability && now.Ability is BattleAbility.Magic or BattleAbility.Draw && now.ActionId == confirmed.ActionId && now.CastCount == confirmed.CastCount && now.DrawSourceId == confirmed.DrawSourceId && now.DrawSourceIndex == confirmed.DrawSourceIndex && now.DrawSourceSlot == confirmed.DrawSourceSlot;
        if (same)
        {
            return command;
        }
        ReleaseMagicReservation(command.MagicReservationId);
        BattleCombatant physicalTarget = target.IsActive && target.IsAlive && !target.Untargetable ? target : combatants.FirstOrDefault(candidate => candidate.IsActive && candidate.IsAlive && !candidate.IsPlayer && !candidate.Untargetable) ?? target;
        byte physical = PhysicalCommand(actor);
        return new(actor.Id, physicalTarget.Id, SelectBattleTarget(physicalTarget)) { NativeQueuedCommand = physical, NativeAction = physical == 12 ? FindNativeCommandAction(actor, 12, 0) : null };
    }
}
