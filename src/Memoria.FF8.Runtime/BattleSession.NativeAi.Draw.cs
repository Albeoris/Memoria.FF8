namespace Memoria.FF8.Runtime;

public sealed record BattlePendingEnemyDraw(string ActorId, byte SpellId, byte Quantity);

public sealed partial class BattleSession
{
    private BattlePendingEnemyDraw? pendingEnemyDraw;
    private BattlePendingEnemyDraw? expandedEffectEnemyDraw;

    private void DrawNativeAiSpell(BattleCombatant actor, bool reportStock)
    {
        //487DF0 reads the previous queued target WORD B94, rather than the interpreter's local target mask.
        BattleCombatant source;
        if (UsesExpandedRoster)
        {
            if (actor.ExpandedPreviousAiTargetId is string sourceId)
            {
                source = GetActor(sourceId);
            }
            else if (actor.NativePreviousTargetMask != 0)
            {
                int index = System.Numerics.BitOperations.TrailingZeroCount((uint)actor.NativePreviousTargetMask);
                source = FindAllocatedNativeActor(index) ?? throw new NotSupportedException($"Expanded AI Draw previous target mask has no retained native allocation at actor index {index}.");
            }
            else
            {
                actor.NativeDrawnSpell = 255;
                return;
            }
        }
        else
        {
            int index = System.Numerics.BitOperations.TrailingZeroCount((uint)actor.NativePreviousTargetMask);
            source = combatants.FirstOrDefault(candidate => candidate.EncodeNativeSlot() == index) ?? throw new NotSupportedException("Native AI Draw requires an allocated actor in its previous queued target mask.");
        }
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
        ushort mask = (flags & 0x30) switch
        {
            0 => (flags & 0x40) != 0 ? (ushort)0x8007 : (ushort)0x80F8,
            0x10 => ResolveNativeAiTargetMask(actor, (flags & 0x40) != 0 ? (byte)201 : (byte)202),
            0x20 => 0x80FF,
            _ => spell
        };
        if ((flags & 1) != 0)
        {
            mask |= 0x4000;
        }
        if ((flags & 2) != 0)
        {
            mask |= 0x2000;
        }
        QueueNativeCommand(actor, 2, spell, 11, mask);
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
        BattleAiRecipients recipients;
        if ((flags & 0x30) == 0x10)
        {
            BattleCombatant[] living = targets.Where(target => target.IsAlive).ToArray();
            recipients = living.Any(target => (NativeSavedStatusBits(target) & 5) == 0) ? PickExpandedAiTarget(living) : new([]);
            if (random && !recipients.ActorIds.IsEmpty)
            {
                recipients = new([.. targets.Select(target => target.Id)]) { RandomSide = side };
            }
        }
        else
        {
            recipients = new([.. targets.Select(target => target.Id)]) { RandomSide = random ? side : null };
        }
        recipients = recipients with { TargetPolicy = BattleAiTargetPolicy.DrawnMagic };
        if (!ExpandedBattleActionRules.SupportsDrawnEnemyMagic(action))
        {
            throw new NotSupportedException($"Expanded enemy Draw Cast cannot resolve installed spell {spell}.");
        }
        EnqueueExpandedAiAction(actor, action, 11, recipients);
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
