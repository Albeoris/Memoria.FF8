namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void QueuePlayerDraw(string actorId, string sourceId, int selection, bool cast, string? castTargetId)
    {
        BattleCombatant actor = GetActor(actorId);
        BattleCombatant source = GetActor(sourceId);
        if (Outcome != BattleOutcome.Active || !actor.IsReady || !actor.Abilities.Contains(BattleAbility.Draw) || HasStatus(actor, BattleStatusKind.Silence) || !source.IsAlive || source.Untargetable || source.IsPlayer == actor.IsPlayer || selection is < 1 or >= 80 || !source.NativeDrawSlots.Any(slot => slot.Id == selection) || cast && selection >= 64 || !cast && castTargetId is not null)
        {
            throw new InvalidOperationException("Draw requires an available source entry, a ready caster and a valid Stock/Cast selection.");
        }
        if (selection < 64 && FindAction(BattleAbility.Magic, selection).NativeAction is not null && nativeKnownMagic is null)
        {
            throw new NotSupportedException("Native Draw requires its saved known magic bank; migrate the unknown owner explicitly.");
        }
        if (cast)
        {
            ValidatePlayerDrawCastTarget(actor, FindAction(BattleAbility.Magic, selection), castTargetId);
        }
        BattleAttackCommand queued = new(actorId, sourceId, BattleAbility.Draw, selection, cast) { NativeQueuedCommand = 6, DrawCastTargetId = castTargetId };
        OnNativePlayerCommandSubmitted(actor, 6);
        EnqueueNativeInputCommand(queued);
        actor.Atb = 0;
        actor.IsBusy = true;
        nativeLimitLevels.Remove(actor.Id);
        Log("Queued", actorId, sourceId, 0, $"{actor.Name}: {ActionName(BattleAbility.Draw, selection)} queued.");
    }

    private BattleCombatant ValidatePlayerDrawCastTarget(BattleCombatant actor, BattleActionDefinition spell, string? targetId)
    {
        if (targetId is null)
        {
            throw new InvalidOperationException("Draw Cast requires its separately selected spell recipient.");
        }
        BattleCombatant recipient = GetActor(targetId);
        bool fixedSelf = spell.NativeAction?.TargetFlags is byte flags && NativeBattleLimitRules.IsFixedAllyTarget(flags);
        if (!recipient.IsActive || recipient.Untargetable || (recipient.IsPlayer == actor.IsPlayer) != spell.Allies || recipient.IsAlive == IsNativeRevive(spell) || fixedSelf && recipient != actor)
        {
            throw new InvalidOperationException("Draw Cast recipient does not satisfy the selected spell's target contract.");
        }
        return recipient;
    }

    private BattleAttackCommand PrepareNativeDrawCommand(BattleCombatant actor, BattleAttackCommand command)
    {
        if (command.Ability != BattleAbility.Draw)
        {
            return command;
        }
        if (command.NativeDraw is not null)
        {
            throw new InvalidDataException("Queued Draw cannot already own a dispatched result.");
        }
        BattleCombatant source = GetActor(command.TargetId);
        bool available = source.NativeDrawSlots.Any(slot => slot.Id == command.ActionId) && source.IsAlive && !HasStatus(actor, BattleStatusKind.Silence);
        bool failed = !available;
        int quantity = 0;
        int added = 0;
        BattleNativeAction? native = null;
        ushort mask = 0;
        if (command.ActionId >= 64)
        {
            int id = command.ActionId - 64;
            BattleGuardianForceVital gf = guardianForces.GetValueOrDefault(id) ?? throw new NotSupportedException("GF Draw requires its canonical saved GF owner.");
            failed |= gf.Acquired;
            if (!failed)
            {
                AcquireNativeGuardianForce(checked((byte)id));
            }
        }
        else
        {
            BattleActionDefinition spell = FindAction(BattleAbility.Magic, command.ActionId);
            if (available)
            {
                NativeBattleDrawSlot entry = source.NativeDrawSlots.First(slot => slot.Id == command.ActionId);
                quantity = Math.Clamp(((((actor.Level - source.Level + 10) >> 1) - spell.DrawResist + (NextRandomByte() & 31) + 1 + actor.Magic) / 5) - entry.Resistance, 0, 9);
                failed = quantity == 0;
                if (!command.DrawCast)
                {
                    added = actor.AddMagic(command.ActionId, quantity);
                }
            }
            if (command.DrawCast && !failed)
            {
                BattleCombatant recipient = GetActor(command.DrawCastTargetId ?? throw new InvalidDataException("Draw Cast lacks its selected recipient."));
                if (nativeKnownMagic is not null || spell.NativeAction is not null)
                {
                    DiscoverNativeMagic(command.ActionId);
                }
                native = spell.NativeAction;
                if (native is not null)
                {
                    mask = NativeBattleLimitRules.PlayerTargetMask(native.TargetFlags ?? throw new InvalidDataException("Draw spell lacks its original target flags."), recipient.NativeIndex);
                }
            }
        }
        BattleNativeDrawContext result = new(checked((byte)command.ActionId), checked((byte)quantity), checked((byte)added), failed);
        PresentNativeDrawResult(actor, source, result, command.DrawCast);
        return command with { NativeDraw = result, NativeAction = native, NativeTargetMask = mask };
    }

    private void ResolvePlayerDraw(BattleCombatant actor, BattleCombatant source, BattleActionPresentation command)
    {
        BattleNativeDrawContext result = command.NativeDraw ?? throw new InvalidDataException("Dispatched Draw requires its prepared native result.");
        if (command.DrawCast && !result.Failed)
        {
            BattleCombatant recipient = GetActor(command.DrawCastTargetId ?? throw new InvalidDataException("Draw Cast lacks its selected recipient."));
            BattleActionDefinition spell = FindAction(BattleAbility.Magic, result.Selection);
            if (spell.NativeAction is not null)
            {
                ResolveInstalledNativeAction(actor, recipient, command, spell);
            }
            else
            {
                ResolveAction(actor, recipient, command with { Ability = BattleAbility.Magic, NativeDraw = null });
            }
            return;
        }
        ApplyDamage(actor, source, 0, "Draw");
    }

    private void PresentNativeDrawResult(BattleCombatant actor, BattleCombatant source, BattleNativeDrawContext result, bool cast)
    {
        string name = result.Selection >= 64 ? $"{{Name {GameNames.DefaultName(GameNames.GuardianForceSubject(result.Selection - 64))}}}" : FindAction(BattleAbility.Magic, result.Selection).Name;
        string text = result.Failed ? "Draw failed." : result.Selection >= 64 ? $"Acquired {name}." : cast ? $"Cast {name}." : $"{actor.Name} stocks {result.Added} {name}.";
        if (definition.NativeGlobalMessages.Length != 0)
        {
            string digits = NativeBattleGlobalText(11);
            if (digits.Length < 11)
            {
                throw new InvalidDataException("Native Draw numeric text requires its separator and ten native digits.");
            }
            string quantity = string.Concat(result.Added.ToString(System.Globalization.CultureInfo.InvariantCulture).Select(digit => digits[digit - '0' + 1]));
            string castName = cast && !result.Failed ? definition.NativeCommandNames.ElementAtOrDefault(9) ?? throw new NotSupportedException("Draw Cast text requires the original kernel command9 name.") : "";
            text = result.Failed ? NativeBattleGlobalText(5) : result.Selection >= 64 ? NativeBattleGlobalText(51) + digits[0] + name + NativeBattleGlobalText(118) : cast ? castName + digits[0] + name : NativeBattleGlobalText(9) + digits[0] + quantity + digits[0] + name + NativeBattleGlobalText(result.Added == 1 ? 118 : 8);
            int duration = 8 * (definition.NativeMessageSpeed ?? throw new NotSupportedException("Native Draw text requires its saved message-speed byte.")) + 8;
            QueueNativeDescriptorMessage(new BattleEvent(Frame, "NativeText", actor.Id, source.Id, duration, text) { NativeMessageMode = 3 });
        }
        Log("Draw", actor.Id, source.Id, result.Added, text);
    }

    private void ResetNativeDrawAllocation(BattleCombatant actor)
    {
        if (actor.NativeTemplate?.NativeDrawTables is not { } tables)
        {
            actor.RestoreDrawSlots(actor.NativeDrawSlots.Select(slot => slot.Id >= 64 && guardianForces.TryGetValue(slot.Id - 64, out BattleGuardianForceVital? gf) && gf.Acquired ? slot with { Id = 0 } : slot).ToArray());
            return;
        }
        if (tables.Length != 3 || actor.NativeAi is null)
        {
            throw new InvalidDataException("Native enemy Draw refresh requires three original DAT banks and its current level band.");
        }
        NativeBattleDrawSlot[] slots = tables[actor.NativeAi.LevelBand].Select(slot => slot.Id >= 64 && guardianForces.TryGetValue(slot.Id - 64, out BattleGuardianForceVital? gf) && gf.Acquired ? slot with { Id = 0 } : slot).ToArray();
        actor.RestoreDrawSlots(slots);
    }

    private void RefreshNativeEnemyDrawSlots()
    {
        //47DE70 calls48B8B0/48C7A0 after queuing pending reactions through484720; their later callbacks see the refreshed Draw bank.
        foreach (BattleCombatant actor in combatants.Where(actor => actor.IsActive && !actor.IsPlayer))
        {
            ResetNativeDrawAllocation(actor);
        }
    }

    private void ValidateNativeDrawPacket(BattleAbility ability, int selection, bool cast, string? recipient, BattleNativeDrawContext? result, bool dispatched, BattleNativeAction? native, ushort targetMask)
    {
        if (ability != BattleAbility.Draw)
        {
            if (recipient is not null || result is not null)
            {
                throw new InvalidDataException("Only Draw can own a prepared Draw result or separate recipient.");
            }
            return;
        }
        if (selection is < 1 or >= 80 || cast != (recipient is not null) || cast && selection >= 64 || dispatched != (result is not null))
        {
            throw new InvalidDataException("Saved Draw requires its original source, selection, recipient and dispatch result.");
        }
        if (recipient is not null)
        {
            BattleCombatant target = GetActor(recipient);
            BattleNativeAction? installed = FindAction(BattleAbility.Magic, selection).NativeAction;
            bool hasEffect = dispatched && result is { Failed: false };
            if (hasEffect && installed is not null && (native is null || target.IsActive && targetMask != NativeBattleLimitRules.PlayerTargetMask(installed.TargetFlags ?? throw new InvalidDataException("Draw spell lacks target flags."), target.NativeIndex)) || !hasEffect && native is not null)
            {
                throw new InvalidDataException("Saved Draw Cast effect disagrees with its prepared native spell and separate recipient.");
            }
        }
        else if (native is not null || targetMask != 0)
        {
            throw new InvalidDataException("Stock Draw has no spell damage metadata or spell recipient mask.");
        }
        if (result is not null && (result.Selection != selection || result.Quantity > 9 || result.Added > result.Quantity || cast && result.Added != 0 || selection >= 64 && (result.Quantity != 0 || result.Added != 0) || selection < 64 && result.Failed != (result.Quantity == 0)))
        {
            throw new InvalidDataException("Saved Draw result contradicts its prepared quantity or acquisition.");
        }
    }
}
