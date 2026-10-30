using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCardDevourAdmissionTests
{
    [Theory]
    [InlineData(17, 0, 182)]
    [InlineData(19, 10, 62)]
    public void Successful_conversion_preserves_formula_amount_separate_from_status_removal(byte type, int amount, int effect)
    {
        BattleSession battle = Create(type);
        Use(battle, type);
        NativeBattleContactHalf contact = battle.NativeActionPackets.ReadContact(0).Primary;
        Assert.Equal((ushort)amount, contact.Amount6);
        Assert.Equal(1, contact.Flags2 & 1);
        Assert.Equal(0, contact.HitFlags3 & 4);
        Assert.Equal(0x10000u, contact.BattleStatuses8 & 0x10000);
        Assert.Equal(0, battle.Combatants[1].CurrentHp);
        Assert.Equal((byte)(amount == 0 ? 1 : 0), battle.Combatants[1].LastNativeHealing);
        Assert.Equal((ushort)effect, NativeBattleActionGraphics.Word(battle.NativeActionPackets.Headers[0], 6));
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        Complete(battle);
        Complete(restored);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Devour_formula_miss_keeps_eight_damage_and_exact_native_roll_admission(bool equalHp)
    {
        BattleSession battle = Create(19, actorHp: 400, targetHp: equalHp ? 400 : 600);
        NativeBattleRandomState before = battle.Capture().Random!;
        NativeBattleRandomState expected = equalHp ? before.Draw(out _) : before;
        Use(battle, 19);
        NativeBattleContactHalf contact = battle.NativeActionPackets.ReadContact(0).Primary;
        Assert.Equal((ushort)8, contact.Amount6);
        Assert.Equal(4, contact.HitFlags3 & 4);
        Assert.Equal(0, contact.Flags2 & 1);
        Assert.Equal(0u, contact.BattleStatuses8 & 0x10000);
        Assert.Equal((equalHp ? 400 : 600) - 8, battle.Combatants[1].CurrentHp);
        Assert.Equal((byte)0, battle.Combatants[1].LastNativeHealing);
        Assert.True(battle.Combatants[1].Atb > 0);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(battle.Capture().Random));
        BattleEvent damage = Assert.Single(battle.Events, item => item.Kind == "Damage" && item.TargetId == "enemy");
        Assert.Equal(8, damage.Amount);
        Assert.Equal((ushort)62, NativeBattleActionGraphics.Word(battle.NativeActionPackets.Headers[0], 6));
        Complete(battle);
        Assert.Equal((equalHp ? 400 : 600) - 8, battle.Combatants[1].CurrentHp);
        Assert.Single(battle.Events, item => item.Kind == "Damage" && item.TargetId == "enemy");
    }

    [Fact]
    public void Explicit_special_source_uses_the_same_native_half_amount_tail()
    {
        BattleSession battle = Create(19, actorHp: 400, targetHp: 600);
        BattleCombatant actor = battle.Combatants[0];
        BattleCombatant target = battle.Combatants[1];
        BattleNativeAction native = battle.Capture().Definition.NativeActions[0];
        BattleAttackCommand input = new(actor.Id, target.Id, ActionId: 9) { NativeQueuedCommand = 7, NativeAction = native, NativeTargetMask = 8 };
        MethodInfo prepare = typeof(BattleSession).GetMethod("PrepareNativeSpecialCommand", BindingFlags.NonPublic | BindingFlags.Instance)!;
        BattleAttackCommand prepared = (BattleAttackCommand)prepare.Invoke(battle, [actor, input])!;
        BattleActionPresentation command = new(actor.Id, target.Id, BattleActionPhase.Windup, 0, ActionId: 9) { NativeQueuedCommand = 7, NativeAction = native, NativeTargetMask = 8, NativeSpecial = prepared.NativeSpecial };
        BattleActionDefinition action = new(BattleAbility.Attack, 9, "Explicit Devour formula", BattleActionEffect.Native, 0, false) { NativeAction = native };
        MethodInfo resolve = typeof(BattleSession).GetMethod("ResolveInstalledNativeHitWithContact", BindingFlags.NonPublic | BindingFlags.Instance)!;
        target.NativeRemainingHits = 1;
        typeof(BattleSession).GetField("nativeActionScratch", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(battle, new NativeBattleActionScratch { ActorIndex = 0, Command = 7, Action = 9, EventActorB0F = 0, EventLane = 2, DeferredEffectsE00 = false });
        NativeBattleRandomState before = battle.Capture().Random!;
        NativeBattleContactPacket packet = (NativeBattleContactPacket)resolve.Invoke(battle, [actor, target, command, action, native, (byte)0, false])!;
        Assert.Equal((ushort)4, packet.Primary.Amount6);
        Assert.Equal(4, packet.Primary.HitFlags3 & 4);
        Assert.Equal(0x08, packet.Primary.HitFlags3 & 0x08);
        Assert.Equal(596, target.CurrentHp);
        Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(battle.Capture().Random));
        Assert.Equal(4, Assert.Single(battle.Events, item => item.Kind == "Damage").Amount);
    }

    [Fact]
    public void Native_eight_damage_can_KO_a_low_HP_target_without_Eject_even_when_the_formula_misses()
    {
        BattleSession battle = Create(19, actorHp: 4, targetHp: 8);
        NativeBattleRandomState before = battle.Capture().Random!;
        Use(battle, 19);
        NativeBattleContactHalf contact = battle.NativeActionPackets.ReadContact(0).Primary;
        Assert.Equal((ushort)8, contact.Amount6);
        Assert.Equal(4, contact.HitFlags3 & 4);
        Assert.Equal(0u, contact.BattleStatuses8 & 0x10000);
        Assert.Equal(0, battle.Combatants[1].CurrentHp);
        Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(battle.Capture().Random));
        Assert.Equal(8, Assert.Single(battle.Events, item => item.Kind == "Damage").Amount);
    }

    [Fact]
    public void Ordinary_Card_chance_miss_retains_its_real_effect_and_miss_contact()
    {
        BattleSession battle = Create(17, targetHp: 1000);
        NativeBattleRandomState before = battle.Capture().Random!;
        NativeBattleRandomState expected = before.Draw(out byte roll);
        Assert.InRange(roll, (byte)2, byte.MaxValue);
        Use(battle, 17);
        NativeBattleContactHalf contact = battle.NativeActionPackets.ReadContact(0).Primary;
        Assert.Equal((ushort)0, contact.Amount6);
        Assert.Equal(4, contact.HitFlags3 & 4);
        Assert.Equal(1000, battle.Combatants[1].CurrentHp);
        Assert.Equal((ushort)182, NativeBattleActionGraphics.Word(battle.NativeActionPackets.Headers[0], 6));
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(battle.Capture().Random));
    }

    [Theory]
    [InlineData(17, false)]
    [InlineData(17, true)]
    [InlineData(19, false)]
    [InlineData(19, true)]
    public void Genuine_invocation_rejection_remaps_to_control_zero_without_publishing_conversion(byte type, bool alternate)
    {
        NativeCardInventory cards = NativeCardInventory.CreateNewGame();
        if (type == 17 && alternate)
        {
            for (int count = 0; count < 100; count++)
            {
                cards = cards.Acquire(4, out _);
            }
        }
        BattleNativeEnemySpecial special = type == 17 && !alternate ? new(255, 255, [0, 0, 0], 10) : type == 19 && !alternate ? new(4, 4, [255, 255, 255], 10) : new(4, 4, [0, 0, 0], 10);
        BattleSession battle = Create(type, cards: cards, special: special, saved: type == 19 && alternate ? (ushort)4 : (ushort)0);
        int beforeHp = battle.Combatants[1].CurrentHp;
        battle.Combatants[1].LastNativeAction = new(1, 81, 82, 83, 84, 6);
        ImmutableArray<byte?> beforeMemory = battle.NativeActionMemory.Rows[3];
        Use(battle, type);
        NativeBattleActionHeaderSlot header = battle.NativeActionPackets.Headers[0];
        Assert.Equal((byte)0, header.ReadByte(1));
        Assert.Equal((byte)10, header.ReadByte(2));
        Assert.Equal((ushort)9, NativeBattleActionGraphics.Word(header, 4));
        Assert.Equal((ushort)0, NativeBattleActionGraphics.Word(header, 6));
        Assert.Equal((byte)0, header.ReadByte(16));
        Assert.Equal((uint)0x47EC70, header.Data!.ProducerAddress);
        Assert.Equal((ushort)(type == 17 ? 50 : 112), header.Data.Argument);
        Assert.Equal(beforeHp, battle.Combatants[1].CurrentHp);
        Assert.Equal(0u, battle.Combatants[1].NativeStatus1 & 0x10000);
        Assert.DoesNotContain(battle.Events, item => item.Kind == "Damage");
        Assert.DoesNotContain(battle.Events, item => item.Kind == "Miss");
        ImmutableArray<byte?> afterMemory = battle.NativeActionMemory.Rows[3];
        Assert.Equal((byte?)3, afterMemory[1]);
        Assert.Equal((byte?)1, afterMemory[6]);
        foreach (int offset in new[] { 0, 2, 3, 4, 5, 7 })
        {
            Assert.Equal(beforeMemory[offset], afterMemory[offset]);
        }
    }

    [Theory]
    [InlineData(17, 1000, 1, 0, false)]
    [InlineData(17, 1000, 1, 0x20, false)]
    [InlineData(19, 1000, 1, 0, false)]
    [InlineData(19, 1000, 1, 0x20, false)]
    [InlineData(19, 400, 600, 0, true)]
    [InlineData(19, 4, 8, 0, false)]
    [InlineData(19, 4, 8, 0x20, true)]
    public void Full_action_memory_follows_native_survival_death_and_Eject_gates_while_partial_fields_still_update(byte type, int actorHp, int targetHp, uint enemyFlags, bool fullMemory)
    {
        BattleSession battle = Create(type, actorHp: actorHp, targetHp: targetHp, enemyFlags: enemyFlags);
        BattleCombatant target = battle.Combatants[1];
        NativeBattleLastAction sentinel = new(1, 81, 82, 83, 84, 6);
        target.LastNativeAction = sentinel;
        target.LastNativeHealing = 1;
        ImmutableArray<byte?> before = battle.NativeActionMemory.Rows[3];
        bool success = targetHp == 1;
        Use(battle, type);
        NativeBattleContactHalf contact = battle.NativeActionPackets.ReadContact(0).Primary;
        ImmutableArray<byte?> after = battle.NativeActionMemory.Rows[3];
        Assert.Equal((byte?)3, after[1]);
        Assert.Equal((byte?)(type == 17 ? 1 : 0), after[6]);
        Assert.Equal(before[2], after[2]);
        if (fullMemory)
        {
            Assert.Equal((byte?)0, after[0]);
            Assert.Equal((byte?)7, after[3]);
            Assert.Equal((byte?)0, after[4]);
            Assert.Equal((byte?)0, after[5]);
            Assert.Equal((byte?)9, after[7]);
        }
        else
        {
            foreach (int offset in new[] { 0, 3, 4, 5, 7 })
            {
                Assert.Equal(before[offset], after[offset]);
            }
        }
        Assert.Equal(success ? 0x10000u : 0u, contact.BattleStatuses8 & 0x10000);
        Assert.Equal(success ? 0 : 4, contact.HitFlags3 & 4);
        Assert.Equal(success || targetHp <= 8 ? 0 : targetHp - 8, target.CurrentHp);
        Assert.Equal(target.CurrentHp == 0 && enemyFlags == 0 ? 8 : 0, contact.Flags2 & 8);
    }

    private static void Use(BattleSession battle, byte type)
    {
        battle.QueueNativePlayerCommand("party-0", "enemy", type == 17 ? (byte)29 : (byte)7, type == 17 ? (ushort)10 : (ushort)9);
        battle.Tick();
    }

    private static void Complete(BattleSession battle)
    {
        for (int frame = 0; frame < 40 && (battle.ActiveAction is not null || battle.QueuedActions.Count != 0); frame++)
        {
            battle.Tick();
        }
    }


    private static BattleSession Create(byte type, int actorHp = 1000, int targetHp = 1, NativeCardInventory? cards = null, BattleNativeEnemySpecial? special = null, ushort saved = 0, uint enemyFlags = 0)
    {
        byte command = type == 17 ? (byte)29 : (byte)7;
        ushort id = type == 17 ? (ushort)10 : (ushort)9;
        BattleNativeAction action = new(command, id, 10, id) { MagicId = type == 17 ? (ushort)182 : (ushort)62, AttackType = type, Power = 0, HitCount = 1, TargetFlags = 0x50, AttackFlags = 3, DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = [1], SupplementalRecord = [] };
        string?[] messages = new string?[119];
        messages[9] = "Converted";
        messages[11] = " ";
        messages[16] = "card";
        messages[50] = "Card cannot be used";
        messages[71] = "";
        messages[112] = "Cannot devour";
        NativeBattleAiDefinition ai = new([[0], [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []);
        BattleCombatantDefinition[] combatants =
        [
            new() { Id = "party-0", Name = "Squall", IsPlayer = true, MaximumHp = 1000, CurrentHp = actorHp, Speed = 1, InitialAtb = BattleSession.AtbMaximum, NativeCommands = [new(command, "Special", 0x50, (byte)id)] },
            new() { Id = "enemy", Name = "Enemy", ModelId = 22, MaximumHp = 1000, CurrentHp = targetHp, Speed = 1, NativeStatus0 = saved, NativeCombatantFlags = enemyFlags, NativeAi = ai, NativeSpecial = special ?? new(4, 4, [0, 0, 0], 10) }
        ];
        if ((saved & 4) != 0)
        {
            combatants = [.. combatants, new() { Id = "enemy-active", Name = "Active enemy", ModelId = 22, MaximumHp = 1000, CurrentHp = 1000, Speed = 1, NativeAi = ai }];
        }
        BattleSession battle = new(new()
        {
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            NativeActions = [action],
            NativeCards = cards ?? NativeCardInventory.CreateNewGame(),
            NativeMessageSpeed = 1,
            NativeGlobalMessages = messages,
            NativeCardNames = Enumerable.Range(0, 110).Select(index => (string?)$"Card{index}").ToArray(),
            NativeDevourEffects = [new(30, 0, 0, 0, 0, 0, "Devour effect")],
            Combatants = combatants
        }, BattleAtbMode.Normal);
        Assert.Equal((saved & 4) != 0 ? 2 : 1, battle.QueuedActions.Count);
        for (int tick = 0; tick < 8 && battle.QueuedActions.Count != 0; tick++)
        {
            Assert.All(battle.QueuedActions, queued =>
            {
                Assert.NotNull(queued.NativeCallback);
                Assert.Equal(0, queued.NativeCallback.Stage);
            });
            battle.Tick();
            Assert.Equal(BattleOutcome.Active, battle.Outcome);
        }
        Assert.Empty(battle.QueuedActions);
        Assert.Null(battle.ActiveAction);
        Assert.Equal(3, battle.Combatants[1].NativeIndex);
        Assert.Equal(saved, (ushort)(battle.Combatants[1].NativeStatus0 & 0xFCFF));
        return battle;
    }
}
