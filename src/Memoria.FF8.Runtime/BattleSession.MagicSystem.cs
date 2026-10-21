namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeCharacterVitals? magicVitals;
    public NativeCharacterVitals? MagicVitals => magicVitals?.DeepCopy();
    public bool InfiniteMagic => magicVitals?.MagicSystem.InfiniteMagic == true;
    public int? GetMagicJunctionOwner(int spellId) => magicVitals is null ? null : MagicJunctionPolicy.GetOwner(magicVitals, spellId);

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
            actor.MagicOwner = () => magicVitals!;
            actor.SetMagicOwner = next =>
            {
                magicVitals = next;
                foreach (BattleCombatant player in combatants.Where(candidate => candidate.IsPlayer))
                {
                    ReprojectNativePlayerStatistics(player);
                }
            };
        }
    }

    public MagicCastQuote QuoteMagic(string actorId, int spellId)
    {
        BattleCombatant actor = GetActor(actorId);
        if (magicVitals is not null && actor.MagicRecordId is int row && spellId is >= 1 and <= 56)
        {
            return MagicCastingService.Quote(magicVitals, row, spellId, MagicCastContext.Battle);
        }
        int stock = actor.MagicStocks.FirstOrDefault(stock => stock.Id == spellId)?.Count ?? 0;
        return new(stock, stock, stock > 0);
    }

    public int GetAvailableMagicCasts(string actorId, int spellId, string? actionId = null)
    {
        BattleCombatant actor = GetActor(actorId);
        return magicVitals is not null && actor.MagicRecordId is int row && spellId is >= 1 and <= 56 ? MagicCastingService.Quote(magicVitals, row, spellId, MagicCastContext.Battle, actionId).Available : QuoteMagic(actorId, spellId).Available;
    }

    private int MagicCost(BattleCombatant actor, int casts)
    {
        uint flags = actor.NativePlayerStatRules?.NativeStatusPassiveFlags ?? 0;
        return magicVitals?.MagicSystem.InfiniteMagic != true && (casts == 2 && (flags & 0x20) != 0 || casts == 3 && (flags & 0x40) != 0) ? 1 : casts;
    }

    private BattleAttackCommand EnsureMagicReservation(BattleCombatant actor, BattleAttackCommand command)
    {
        if (magicVitals is null || actor.MagicRecordId is not int row || !actor.IsPlayer || command.FreeNativeMagic || command.Ability == BattleAbility.Draw || command.MagicReservationId is not null || command.NativeHeaderIndex is not null || command.Ability != BattleAbility.Magic && command.NativeAction?.Command != 2)
        {
            return command;
        }
        int spell = command.NativeAction?.ActionId ?? command.ActionId;
        if (spell is < 1 or > 56)
        {
            throw new InvalidOperationException("Inventory magic requires a supported stock identity.");
        }
        string identity = Guid.NewGuid().ToString("N");
        int count = MagicCost(actor, Math.Max(1, command.NativeMagicTargets.Length));
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

    private void ValidateBattleMagicSnapshot()
    {
        if (magicVitals is null)
        {
            return;
        }
        magicVitals.ValidateMagicSystemConsistency();
        if (definition.MagicVitals is { } initial)
        {
            MagicSystemState before = initial.MagicSystem;
            MagicSystemState current = magicVitals.MagicSystem;
            if (before.SharedMagicStock != current.SharedMagicStock || before.InfiniteMagic != current.InfiniteMagic || before.SmartAttack != current.SmartAttack || before.HistoriesInitialized != current.HistoriesInitialized || !before.AttackModes.SequenceEqual(current.AttackModes) || !before.SharedFieldMasks.SequenceEqual(current.SharedFieldMasks) || !before.PersonalFieldMasks.Zip(current.PersonalFieldMasks).All(pair => pair.First.SequenceEqual(pair.Second)))
            {
                throw new InvalidDataException("A battle checkpoint cannot change its saved magic rules or field histories.");
            }
        }
        bool NeedsReservation(string actorId, BattleAbility ability, BattleNativeAction? native, bool free, int? header)
        {
            return header is null && !free && ability != BattleAbility.Draw && GetActor(actorId).MagicRecordId is not null && (ability == BattleAbility.Magic || native?.Command == 2);
        }
        List<(string? Identity, string Actor, int Spell, bool Required)> commands = actions.Concat(nativeCommandBanks.BoundCommands).Where(command => command.NativeCallback is null).Select(command => (command.MagicReservationId, command.ActorId, (int)(command.NativeAction?.ActionId ?? command.ActionId), NeedsReservation(command.ActorId, command.Ability, command.NativeAction, command.FreeNativeMagic, command.NativeHeaderIndex))).ToList();
        if (ActiveAction is { } active)
        {
            commands.Add((active.MagicReservationId, active.ActorId, active.NativeAction?.ActionId ?? active.ActionId, NeedsReservation(active.ActorId, active.Ability, active.NativeAction, active.FreeNativeMagic, active.NativeHeaderIndex)));
        }
        if (nativeTimedAction is { } timed)
        {
            BattleActionPresentation action = timed.Command;
            commands.Add((action.MagicReservationId, action.ActorId, action.NativeAction?.ActionId ?? action.ActionId, NeedsReservation(action.ActorId, action.Ability, action.NativeAction, action.FreeNativeMagic, action.NativeHeaderIndex)));
        }
        if (commands.Any(command => command.Required && command.Identity is null))
        {
            throw new InvalidDataException("A saved ordinary spell requires its original cast reservation identity.");
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

    private bool CanExecuteMagicReservation(BattleCombatant actor, BattleAttackCommand command)
    {
        if (command.NativeHeaderIndex is not null || command.MagicReservationId is null || magicVitals is null || actor.MagicRecordId is not int row)
        {
            return true;
        }
        MagicCastReservation? reservation = magicVitals.MagicSystem.Reservations.FirstOrDefault(candidate => candidate.ActionId == command.MagicReservationId);
        return reservation is not null && MagicCastingService.Quote(magicVitals, row, reservation.SpellId, MagicCastContext.Battle, reservation.ActionId).Allowed && (command.Ability != BattleAbility.Magic || (NativeSavedStatusBits(actor) & 0x10) == 0 && actor.Abilities.Contains(BattleAbility.Magic));
    }

    private BattleAttackCommand RevalidateSmartAttack(BattleCombatant actor, BattleAttackCommand command)
    {
        if (command.SmartAttack is not { } confirmed)
        {
            return command;
        }
        BattleCombatant target = GetActor(confirmed.TargetId);
        BattleSmartAttackPreview now = SelectSmartAttack(actor, target, confirmed.ForcePhysical, command.MagicReservationId);
        bool same = target.NativeIndex == confirmed.TargetIndex && target.IsAlive && target.IsActive && !target.Untargetable && now.Ability == BattleAbility.Magic && now.ActionId == confirmed.ActionId && now.CastCount == confirmed.CastCount;
        if (same)
        {
            return command;
        }
        ReleaseMagicReservation(command.MagicReservationId);
        BattleCombatant physicalTarget = target.IsActive && target.IsAlive && !target.Untargetable ? target : combatants.FirstOrDefault(candidate => candidate.IsActive && candidate.IsAlive && !candidate.IsPlayer && !candidate.Untargetable) ?? target;
        byte physical = PhysicalCommand(actor);
        return new(actor.Id, physicalTarget.Id) { NativeQueuedCommand = physical, NativeAction = physical == 12 ? FindNativeCommandAction(actor, 12, 0) : null };
    }
}
