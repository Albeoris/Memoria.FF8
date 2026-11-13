namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAiDrawTests
{
    [Theory]
    [InlineData(41, "Drew 1 Spell2 stock")]
    [InlineData(46, "Party has Spell2 stocked")]
    public void DrawUsesPhysicalInventorySlotsAndThePreviousQueuedTarget(byte opcode, string expected)
    {
        NativeMagicStock[] slots = Enumerable.Repeat(new NativeMagicStock(0, 0), 32).ToArray();
        slots[5] = new(2, 3);
        slots[10] = new(1, 9);
        BattleSession battle = Create([4, 201, 3, 1, 6, 4, 200, opcode, 42, 0], slots);

        BattleSnapshot snapshot = battle.Capture();
        BattleAttackCommand cast = snapshot.QueuedActions.Last();
        Assert.Equal((ushort)1, cast.NativeTargetMask);
        Assert.Equal(2, cast.ActionId);
        Assert.Equal((byte)11, cast.NativeSequence);
        Assert.Equal(expected, Assert.Single(battle.Events, entry => entry.Kind == "NativeText").Message);
        Assert.Equal((byte)2, snapshot.Combatants.Single(actor => actor.Id == "enemy").NativeParticipant!.DrawnSpell);
        Assert.Equal(slots, snapshot.Combatants.Single(actor => actor.Id == "party").NativeMagicInventory);
        Assert.Equal(slots, BattleSession.Restore(snapshot).Capture().Combatants.Single(actor => actor.Id == "party").NativeMagicInventory);
    }

    [Fact]
    public void MagicConsumptionClearsOnlyItsOriginalSlotAndDoesNotCompact()
    {
        NativeMagicStock[] slots = Enumerable.Repeat(new NativeMagicStock(0, 0), 32).ToArray();
        slots[5] = new(1, 1);
        slots[10] = new(2, 9);
        BattleSession battle = Create([0], slots);

        battle.QueueAction("party", "enemy", BattleAbility.Magic, 1);

        Assert.Equal(new NativeMagicStock(1, 1), battle.Capture().Combatants.Single(actor => actor.Id == "party").NativeMagicInventory![5]);
        for (int frame = 0; frame <= BattleSession.AttackWindupFrames; frame++)
        {
            battle.Tick();
        }

        NativeMagicStock[] current = battle.Capture().Combatants.Single(actor => actor.Id == "party").NativeMagicInventory!;
        Assert.Equal(new NativeMagicStock(0, 0), current[5]);
        Assert.Equal(new NativeMagicStock(2, 9), current[10]);
        Assert.Equal(2, Assert.Single(battle.Combatants.Single(actor => actor.Id == "party").MagicStocks).Id);
    }

    [Fact]
    public void DrawFromAnEmptyInventoryStoresTheOriginalSentinelWithoutRandomConsumption()
    {
        BattleSession battle = Create([43, 0, 3, 1, 6, 41, 0], Enumerable.Repeat(new NativeMagicStock(0, 0), 32).ToArray());

        Assert.Equal((byte)255, battle.Capture().Combatants.Single(actor => actor.Id == "enemy").NativeParticipant!.DrawnSpell);
        Assert.DoesNotContain(battle.Events, entry => entry.Kind == "NativeText");
        //Draw consumes no roll, while the preceding admitted Spell1 still performs its zero-power formula roll.
        NativeBattleRandomState expected = NativeBattleRandomState.Initialize(0).Draw(out _);
        Assert.Equal(expected.Indexes.ToArray(), battle.Capture().Random!.Indexes.ToArray());
        Assert.Equal(expected.Lane, battle.Capture().Random!.Lane);
    }

    [Theory]
    [InlineData(41, 1, false, false)]
    [InlineData(46, 3, false, false)]
    [InlineData(41, 1, true, false)]
    [InlineData(46, 3, false, true)]
    public void ExpandedDrawDebitsTheFirstActualContactAndUsesPreviousQueuedRecipient(byte opcode, int removed, bool miss, bool group)
    {
        BattleNativeAction attack = Spell(1) with
        {
            Command = 8, ActionId = 194, KernelSection = 3, KernelIndex = 194, Name = "No damage",
            AttackType = (byte)(miss ? 1 : 0), AttackFlags = (byte)(miss ? 0 : 3), Hit = (byte)(miss ? 0 : 255), TargetFlags = null, MagicId = 37
        };
        NativeBattleAiAbility[] abilities = Enumerable.Repeat(new NativeBattleAiAbility(8, 12, 194), 48).ToArray();
        // Queue a hit on a different ally before selecting the stocked source. Only the second target
        // owns three stocks, but the first actual contact owns the debit.
        NativeBattleAiDefinition ai = new([[0], [4, group ? (byte)204 : (byte)9, 12, 0, 4, 10, 12, 0, 4, 200, opcode, 0], [0], [0], [0]], abilities, 0, []);
        string?[] messages = new string?[119];
        messages[11] = " 0123456789";
        messages[19] = "Drew";
        messages[27] = " has";
        messages[117] = "stocked";
        messages[118] = " stock";
        BattleCombatantDefinition[] allies = Enumerable.Range(0, 11).Select(index => new BattleCombatantDefinition
        {
            Id = $"ally-{index}", Name = $"Ally {index}", IsPlayer = true, ModelId = index, Speed = 1,
            NativeMagicInventory = BattleCombatant.ExpandMagicStocks(index switch { 0 or 9 => [new BattleStock(1, 5)], 10 => [new BattleStock(1, 3)], _ => [] })
        }).ToArray();
        BattleCombatantDefinition[] enemies = Enumerable.Range(0, 11).Select(index => new BattleCombatantDefinition
        {
            Id = $"enemy-{index}", Name = $"Enemy {index}", ModelId = 45, Speed = 1,
            InitialAtb = index == 10 ? BattleSession.AtbMaximum : 0, NativeAi = index == 10 ? ai : null
        }).ToArray();
        BattleSession battle = new(new()
        {
            UsesExpandedRoster = true,
            Combatants = [.. allies, .. enemies],
            NativeActions = [Spell(1), attack],
            NativeSeed = 0,
            NativeGlobalMessages = messages,
            NativeMessageSpeed = 0
        }, BattleAtbMode.Normal);
        battle.Tick();
        BattleSnapshot pending = battle.Capture();
        Assert.Equal(new BattlePendingEnemyDraw("enemy-10", 1, (byte)removed), pending.PendingEnemyDraw);
        string firstRecipient = group ? "ally-0" : "ally-9";
        Assert.Equal(firstRecipient, pending.ActiveAction?.TargetId);
        Assert.Equal("ally-10", Assert.Single(pending.QueuedActions).TargetId);
        BattleSession restored = BattleSession.Restore(pending);

        string[] expectedContacts = group ? [.. allies.Select(ally => ally.Id), "ally-10"] : ["ally-9", "ally-10"];
        for (int frame = 0; frame < 160 && restored.Events.Count(entry => entry.Kind == "EffectContact") < expectedContacts.Length; frame++)
        {
            restored.Tick();
        }

        BattleEvent debit = Assert.Single(restored.Events, entry => entry.Kind == "EnemyDrawStock");
        Assert.Equal(firstRecipient, debit.TargetId);
        Assert.Equal(removed, debit.Amount);
        BattleEvent[] contacts = restored.Events.Where(entry => entry.Kind == "EffectContact").ToArray();
        Assert.Equal(expectedContacts, contacts.Select(entry => entry.TargetId));
        if (miss)
        {
            Assert.Contains(restored.Events, entry => entry.Kind == "Miss" && entry.TargetId == firstRecipient);
        }
        Assert.Equal(contacts[0].Frame, debit.Frame);
        Assert.Equal(5 - removed, Assert.Single(restored.Combatants.Single(actor => actor.Id == firstRecipient).MagicStocks).Count);
        Assert.Equal(3, Assert.Single(restored.Combatants.Single(actor => actor.Id == "ally-10").MagicStocks).Count);
        Assert.Null(restored.Capture().PendingEnemyDraw);
    }

    [Theory]
    [InlineData(1, 0x54, 2, 1, BattleSide.Allies, 1)]
    [InlineData(16, 0x46, 2, 10, BattleSide.Allies, 11)]
    [InlineData(17, 0x44, 2, 1, BattleSide.Allies, 11)]
    [InlineData(21, 0x14, 3, 1, BattleSide.Enemies, 1)]
    [InlineData(24, 0x15, 5, 1, BattleSide.Enemies, 1)]
    [InlineData(0, 0, 0, 0, BattleSide.Allies, 0)]
    public void ExpandedDrawCastUsesTheInstalledSpellTargetModeWithoutCasterStock(ushort id, byte flags, byte type, byte hits, BattleSide side, int queuedCount)
    {
        BattleNativeAction spell = Spell(id) with { AttackType = type, TargetFlags = flags, HitCount = hits };
        BattleNativeAction draw = Spell(1) with { Command = 8, ActionId = 194, KernelSection = 3, AttackType = 0, AttackFlags = 3 };
        NativeBattleAiDefinition ai = new([[0], [4, 10, 12, 0, 41, 42, 0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(8, 12, 194), 48).ToArray(), 0, []);
        Assert.Equal(ExpandedAiAdmission.Supported, NativeBattleExpandedAi.Evaluate(ai, [spell, draw], 0));
        string?[] messages = new string?[119];
        messages[11] = " 0123456789";
        messages[19] = "Drew";
        messages[118] = " stock";
        BattleCombatantDefinition[] allies = Enumerable.Range(0, 11).Select(index => new BattleCombatantDefinition
        {
            Id = $"ally-{index}", Name = $"Ally {index}", IsPlayer = true, ModelId = index, Speed = 1,
            NativeMagicInventory = BattleCombatant.ExpandMagicStocks(index == 10 && id != 0 ? [new BattleStock(id, 2)] : [])
        }).ToArray();
        BattleCombatantDefinition[] enemies = Enumerable.Range(0, 11).Select(index => new BattleCombatantDefinition
        {
            Id = $"enemy-{index}", Name = $"Enemy {index}", ModelId = 45, Speed = 1,
            InitialAtb = index == 10 ? BattleSession.AtbMaximum : 0, NativeAi = index == 10 ? ai : null
        }).ToArray();
        BattleSession battle = new(new()
        {
            UsesExpandedRoster = true, Combatants = [.. allies, .. enemies], NativeActions = [spell, draw],
            NativeGlobalMessages = messages, NativeMessageSpeed = 0
        }, BattleAtbMode.Normal);
        battle.Tick();
        BattleSnapshot snapshot = battle.Capture();
        if (queuedCount == 0)
        {
            Assert.Empty(snapshot.QueuedActions);
            Assert.Null(snapshot.PendingEnemyDraw);
            return;
        }
        BattleAttackCommand queued = Assert.Single(snapshot.QueuedActions);
        Assert.Equal(id, queued.ActionId);
        Assert.Equal((byte)11, queued.NativeSequence);
        Assert.Equal((byte)2, queued.NativeQueuedCommand);
        BattleAiRecipients recipients = Assert.IsType<BattleAiRecipients>(queued.AiRecipients);
        Assert.Equal(queuedCount, recipients.ActorIds.Length);
        Assert.Equal(BattleAiTargetPolicy.DrawnMagic, recipients.TargetPolicy);
        Assert.All(recipients.ActorIds, recipient => Assert.Equal(side, battle.Combatants.Single(actor => actor.Id == recipient).Side));
        Assert.Equal(hits > 1 ? side : (BattleSide?)null, recipients.RandomSide);
        BattleSession restored = BattleSession.Restore(snapshot);

        for (int frame = 0; frame < 180 && !restored.Events.Any(entry => entry is { Kind: "EffectContact", NativeSequence: 11 }); frame++)
        {
            restored.Tick();
        }

        Assert.Contains(restored.Events, entry => entry is { Kind: "EffectContact", NativeSequence: 11 });
        Assert.Equal(1, Assert.Single(restored.Combatants.Single(actor => actor.Id == "ally-10").MagicStocks).Count);
        Assert.Empty(restored.Combatants.Single(actor => actor.Id == "enemy-10").MagicStocks);
        Assert.Single(restored.Events, entry => entry.Kind == "EnemyDrawStock");
    }

    [Fact]
    public void ExpandedDrawCastAdmissionRejectsUnsupportedStockableKernelRows()
    {
        NativeBattleAiDefinition ai = new([[0], [42, 0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []);
        BattleNativeAction unsupported = Spell(1) with { TargetFlags = 0x30 };
        List<string> blockers = [];

        Assert.Equal(ExpandedAiAdmission.RequiresUnsupportedService, NativeBattleExpandedAi.Evaluate(ai, [unsupported], 0, blockers));
        Assert.Equal("drawn-magic-contract", Assert.Single(blockers));
    }

    private static BattleSession Create(byte[] initial, NativeMagicStock[] slots)
    {
        NativeBattleAiDefinition ai = new([initial, [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []);
        string?[] messages = new string?[119];
        messages[11] = " 0123456789";
        messages[19] = "Drew";
        messages[27] = " has";
        messages[117] = "stocked";
        messages[118] = " stock";
        BattleNativeAction[] spells = [Spell(1), Spell(2)];
        BattleSession battle = new(new BattleDefinition
        {
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            Name = "Native AI Draw component",
            NativeMessageSpeed = 0,
            NativeGlobalMessages = messages,
            NativeActions = spells,
            ActionDefinitions = spells.Select(spell => new BattleActionDefinition(BattleAbility.Magic, spell.ActionId, spell.Name!, BattleActionEffect.Native, 0, false) { NativeAction = spell }).ToArray(),
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum, NativeMagicInventory = slots, Abilities = [BattleAbility.Attack, BattleAbility.Magic] },
                new() { Id = "enemy", Name = "Enemy", ModelId = 1, NativeAi = ai }
            ]
        }, BattleAtbMode.Normal);
        battle.Tick();
        return battle;
    }

    private static BattleNativeAction Spell(ushort id)
    {
        return new(2, id, 1, id)
        {
            Name = $"Spell{id}",
            MagicId = id,
            AttackType = 2,
            Power = 0,
            HitCount = 1,
            TargetFlags = 0x50,
            AttackFlags = 1,
            DamagePresentation = 0,
            Element = 0,
            ElementPercent = null,
            StatusAttack = 0,
            Statuses0 = 0,
            Statuses1 = 0,
            Hit = null,
            Critical = null,
            PowerModifier = null,
            LevelModifier = null,
            RawRecord = [],
            SupplementalRecord = []
        };
    }
}
