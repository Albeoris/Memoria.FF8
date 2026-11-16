namespace Memoria.FF8.Runtime;

public sealed record BattlePendingEnemyDraw(string ActorId, byte SpellId, byte Quantity);

public sealed partial class BattleSession
{
    private BattlePendingEnemyDraw? pendingEnemyDraw;

    private void DrawNativeAiSpell(BattleCombatant actor, bool reportStock, int? hostedOperationsAtStart = null)
    {
        //487DF0 reads the previous queued target, which can name an original slot with a newer allocation.
        BattlePreviousTarget? previous = actor.PreviousAiTarget;
        if (UsesExpandedRoster && previous is null)
        {
            actor.NativeDrawnSpell = 255;
            return;
        }
        BattleActorReference sourceReference = previous switch
        {
            BattlePreviousTarget.Actor captured => captured.Reference,
            NativeBattleTargetRequestAdapter.OriginalPreviousOperand original => NativeBattleTargetRequestAdapter.ResolvePrevious(original, ReadOriginalTargetOccupant),
            _ => throw new NotSupportedException("Native AI Draw requires an allocated actor in its previous queued target.")
        };
        BattleCombatant source = GetActor(sourceReference.ActorId);
        NativeMagicStock[] inventory = NativeAiDrawInventory(source);
        int occupied = inventory.Count(stock => stock.SpellId != 0);
        if (occupied == 0)
        {
            actor.NativeDrawnSpell = 255;
            return;
        }
        //4837E0 deliberately starts at RNG % count, then scans physical slots; it does not choose the nth nonempty entry.
        int slot = NextRandomByte() % occupied;
        while (inventory[slot].SpellId == 0)
        {
            slot = (slot + 1) % inventory.Length;
        }
        NativeMagicStock selected = inventory[slot];
        actor.NativeDrawnSpell = selected.SpellId;
        if (UsesExpandedRoster)
        {
            // The original E11..E13 owner is global. A new successful Draw replaces its pending result.
            pendingEnemyDraw = new(actor.Id, selected.SpellId, reportStock ? selected.Count : (byte)1);
            if (hostedOperationsAtStart is int start)
            {
                BindPreparedHostedEnemyDraw(actor, sourceReference, start);
            }
        }
        BattleNativeAction spell = FindInstalledNativeAction(2, selected.SpellId);
        string spellName = spell.Name ?? throw new InvalidDataException("Native AI Draw requires the installed spell name.");
        string separator = NativeBattleGlobalText(11);
        if (separator.Length == 0)
        {
            throw new InvalidDataException("Native battle numeric text bank has no separator byte.");
        }
        string message = reportStock ? source.Name + NativeBattleGlobalText(27) + separator[0] + spellName + separator[0] + NativeBattleGlobalText(117) : NativeBattleGlobalText(19) + separator[0] + "1" + separator[0] + spellName + NativeBattleGlobalText(118);
        int duration = 8 * (definition.NativeMessageSpeed ?? throw new NotSupportedException("Native Draw message requires its saved message-speed BYTE.")) + 8;
        QueueNativeDescriptorMessage(new BattleEvent(Frame, "NativeText", actor.Id, source.Id, duration, message) { NativeMessageMode = 3 });
    }

    private void BindPreparedHostedEnemyDraw(BattleCombatant actor, BattleActorReference source, int start)
    {
        for (int index = expandedOperationCursor.Operations.Length - 1; index >= start; index--)
        {
            if (expandedOperationCursor.Operations[index] is not ExpandedHostedInvocation invocation ||
                invocation.Command.ActorId != actor.Id || invocation.Command.NativeAiCallbackStage != nativeAiCallbackStage)
            {
                continue;
            }

            if (invocation.ContactDisposition == ExpandedHostedContactDisposition.RequiredImpacts && !invocation.Contacts.IsEmpty &&
                invocation.Command.TargetRequest.Selection is BattleTargetSelection.Explicit targets && targets.Actors.Contains(source))
            {
                // Opcode41 follows the action queue write. The attached candidate already prepared that action's contacts.
                expandedOperationCursor.Operations = expandedOperationCursor.Operations.SetItem(index, invocation with { EnemyDraw = TakePendingHostedEnemyDraw() });
            }
            return;
        }
    }

    private static NativeMagicStock[] NativeAiDrawInventory(BattleCombatant source)
    {
        bool logical = source.MagicOwner is not null && (source.MagicOwner().MagicSystem.SharedMagicStock || source.MagicOwner().MagicSystem.RecordZeroExtension.Length != 0 && source.MagicRecordId == 0);
        return logical ? source.MagicStocks.Select(stock => new NativeMagicStock((byte)stock.Id, (byte)stock.Count)).ToArray() : source.NativeMagicInventory;
    }

    private void CommitExpandedEnemyDraw(string recipientId, ref BattlePendingEnemyDraw? pending)
    {
        if (pending is not { } draw)
        {
            return;
        }
        // 493D80 debits the primary contact's recipient, including misses, and clears the shared result.
        BattleCombatant recipient = GetActor(recipientId);
        int removed = recipient.RemoveMagicStock(draw.SpellId, draw.Quantity);
        pending = null;
        Log("EnemyDrawStock", draw.ActorId, recipient.Id, removed, NativeBattleGlobalText(19));
    }

    private void RestoreExpandedEnemyDraw(BattlePendingEnemyDraw? saved)
    {
        if (saved is not null && (!UsesExpandedRoster || !byId.TryGetValue(saved.ActorId, out BattleCombatant? actor) || actor.IsPlayer || actor.NativeAi is null || saved.SpellId is 0 or >= 64 || saved.Quantity is 0 or > 100))
        {
            throw new InvalidDataException("Battle snapshot has an invalid deferred enemy Draw result.");
        }
        pendingEnemyDraw = saved;
    }

    private void CastNativeAiDrawnSpell(BattleCombatant actor)
    {
        if (UsesExpandedRoster)
        {
            CastExpandedEnemyDrawnSpell(actor);
            return;
        }
        byte spell = actor.NativeDrawnSpell ?? throw new NotSupportedException("Native AI Draw-cast requires an executed drawn-spell byte.");
        if (spell == 0)
        {
            return;
        }
        BattleNativeAction action = FindInstalledNativeAction(2, spell);
        byte flags = action.TargetFlags ?? throw new InvalidDataException("Native drawn spell lacks its kernel target flags.");
        AiTargetSelection selected = (flags & 0x30) switch
        {
            0 => SelectAiGroup((flags & 0x40) != 0 ? BattleTargetSide.Allies : BattleTargetSide.Enemies),
            0x10 => PickAiSide((flags & 0x40) != 0 ? BattleSide.Allies : BattleSide.Enemies),
            0x20 => SelectAiGroup(BattleTargetSide.Both),
            _ => SelectOriginalAiTargets(new(NativeBattleTargetRequestAdapter.CaptureMagicPreflight(spell)))
        };
        BattleTargetRequest request = ApplyOriginalAiTargetFlags(selected, flags);
        BattlePreviousTarget previous = NativeBattleTargetRequestAdapter.CapturePrevious(request.OriginalProjection!, request.AllowsKnockedOutAllies);
        QueueNativeCommand(actor, 2, spell, 11, new(request, previous));
    }

    private void CastExpandedEnemyDrawnSpell(BattleCombatant actor)
    {
        if (actor.NativeDrawnSpell is not byte spell || spell is 0 or 255)
        {
            return;
        }
        BattleNativeAction action = FindInstalledNativeAction(2, spell);
        byte flags = action.TargetFlags ?? throw new InvalidDataException("Native drawn spell lacks its kernel target flags.");
        if ((flags & 0x30) == 0x30)
        {
            // The original selector uses the spell ID as a physical mask for this encoding.
            throw new NotSupportedException("Expanded enemy Draw Cast cannot use a physical spell target encoding.");
        }
        bool random = (flags & 2) != 0;
        BattleSide side = (flags & 0x40) != 0 ? BattleSide.Allies : BattleSide.Enemies;
        bool targetsBothSides = (flags & 0x30) == 0x20;
        if (targetsBothSides && random)
        {
            // Original 0xA0FF requests random selection from the enemy side.
            side = BattleSide.Enemies;
        }
        IEnumerable<BattleCombatant> roster = targetsBothSides && !random ? combatants : Roster(side);
        BattleCombatant[] targets = roster.Where(candidate => IsExpandedDrawCastTarget(candidate, (flags & 1) != 0)).ToArray();
        AiTargetSelection selected;
        if ((flags & 0x30) == 0x10)
        {
            BattleCombatant[] living = targets.Where(target => target.IsAlive).ToArray();
            selected = living.Any(target => (NativeSavedStatusBits(target) & 5) == 0) ? PickAiTarget(living) : SelectAiActors([]);
            if (random && selected.Request.Selection is not BattleTargetSelection.None)
            {
                selected = SelectAiActors(targets, side);
            }
        }
        else
        {
            selected = SelectAiActors(targets, random ? side : null);
        }
        if (!ExpandedBattleActionRules.SupportsDrawnEnemyMagic(action))
        {
            throw new NotSupportedException($"Expanded enemy Draw Cast cannot resolve installed spell {spell}.");
        }
        EnqueueExpandedAiAction(actor, action.Command, action.ActionId, 11, selected, action, eligibility: BattleTargetEligibility.DrawnMagic);
    }

    private static bool IsExpandedDrawCastTarget(BattleCombatant actor, bool canTargetKnockedOutAllies)
    {
        return actor.IsActive && !actor.Untargetable && (actor.IsAlive || canTargetKnockedOutAllies && actor.IsPlayer);
    }

    private string NativeBattleGlobalText(int index)
    {
        if ((uint)index >= definition.NativeGlobalMessages.Length || definition.NativeGlobalMessages[index] is not string text)
        {
            throw new NotSupportedException($"Native battle message requires installed kernel global text {index}.");
        }
        return text;
    }
}
