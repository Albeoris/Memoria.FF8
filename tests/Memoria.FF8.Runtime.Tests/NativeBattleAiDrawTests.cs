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
        Assert.Equal(NativeBattleRandomState.Initialize(0).Indexes.ToArray(), battle.Capture().Random!.Indexes.ToArray());
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
        return new(new BattleDefinition
        {
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
