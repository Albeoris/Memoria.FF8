using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCompelledActionTests
{
    [Fact]
    public void BerserkReadyPlayerAutomaticallyQueuesAttackAgainstAnEnemy()
    {
        BattleSession battle = Create(0x20, 0);

        battle.Tick();

        Assert.Equal("party", battle.ActiveAction!.ActorId);
        Assert.Equal(BattleAbility.Attack, battle.ActiveAction.Ability);
        Assert.Equal((byte)1, battle.ActiveAction.NativeQueuedCommand);
        Assert.Equal((ushort)8, battle.ActiveAction.NativeTargetMask);
    }

    [Fact]
    public void AngelWingMagicUsesTheOriginalOffensiveSlotsAndDoesNotConsumeStock()
    {
        BattleSession battle = Create(0, 0x2000000);

        battle.Tick();

        Assert.Equal((byte)2, battle.ActiveAction!.NativeQueuedCommand);
        Assert.Equal((ushort)0x80F8, battle.ActiveAction.NativeTargetMask);
        Assert.True(battle.ActiveAction.FreeNativeMagic);
        for (int frame = 0; frame < BattleSession.AttackWindupFrames; frame++)
        {
            battle.Tick();
        }
        Assert.Equal(3, Assert.Single(battle.Combatants.Single(actor => actor.Id == "party").MagicStocks).Count);
    }

    [Fact]
    public void ConfuseScansPhysicalCommandHolesAndPreservesItsPreparedPacketAcrossRestore()
    {
        BattleSession battle = Create(0, 0x4000);

        battle.Tick();

        BattleActionPresentation action = battle.ActiveAction!;
        Assert.Equal((byte)1, action.NativeQueuedCommand);
        Assert.Contains(action.NativeTargetMask, new ushort[] { 1, 8 });
        Assert.Equal(action, BattleSession.Restore(battle.Capture()).ActiveAction);
    }

    [Fact]
    public void ReplacingAReservedItemRefundsItsOriginalPhysicalSlot()
    {
        BattleSession battle = Create(0, 0);
        battle.QueueAction("party", "party", BattleAbility.Item, 1);
        Assert.Empty(battle.Items);
        BattleSnapshot snapshot = battle.Capture();
        int party = Array.FindIndex(snapshot.Combatants, actor => actor.Id == "party");
        snapshot.Combatants[party] = snapshot.Combatants[party] with { NativeStatus = snapshot.Combatants[party].NativeStatus! with { Saved = 0x20 } };
        battle = BattleSession.Restore(snapshot);

        battle.Tick();

        Assert.Equal(BattleAbility.Attack, battle.ActiveAction!.Ability);
        Assert.Equal(new NativeItemStock(1, 1), battle.Capture().NativeItemInventory![0]);
        Assert.Equal(new NativeItemStock(0, 0), battle.Capture().NativeItemInventory![5]);
    }

    [Theory]
    [InlineData(0x4000u)]
    [InlineData(0x2000000u)]
    public void SilencedCompelledMagicStillPublishesTheFailedAttemptWithoutSpendingStock(uint compelledStatus)
    {
        BattleSession battle = Create(0x10, compelledStatus, magicOnly: true);

        battle.Tick();

        Assert.Equal((byte)2, battle.ActiveAction!.NativeQueuedCommand);
        AssertSilencedMagicAttempt(battle);
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        for (int frame = 0; frame < BattleSession.AttackWindupFrames; frame++)
        {
            battle.Tick();
            restored.Tick();
        }
        Assert.Equal(3, Assert.Single(battle.Combatants[0].MagicStocks).Count);
        Assert.Equal(500, battle.Combatants[1].CurrentHp);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Fact]
    public void MagicQueuedBeforeSilenceUsesTheSameFailedInvocationContract()
    {
        BattleSession battle = Create(0, 0, magicOnly: true);
        battle.QueueAction("party", "enemy", BattleAbility.Magic, 2);
        BattleSnapshot queued = battle.Capture();
        queued.Combatants[0] = queued.Combatants[0] with { NativeStatus = queued.Combatants[0].NativeStatus! with { Saved = 0x10 } };
        battle = BattleSession.Restore(queued);

        battle.Tick();

        AssertSilencedMagicAttempt(battle);
    }

    private static void AssertSilencedMagicAttempt(BattleSession battle)
    {
        int header = battle.ActiveAction!.NativeHeaderIndex!.Value;
        Assert.Equal((byte)0, battle.NativeActionPackets.Headers[header].ReadByte(1));
        Assert.Equal((byte)10, battle.NativeActionPackets.Headers[header].ReadByte(2));
        Assert.Equal((byte)9, battle.NativeActionPackets.Headers[header].ReadByte(4));
        Assert.Equal((byte)0, battle.NativeActionPackets.Headers[header].ReadByte(5));
        Assert.Equal((byte)0, battle.NativeActionPackets.Headers[header].ReadByte(6));
        Assert.Equal((byte)0, battle.NativeActionPackets.Headers[header].ReadByte(7));
        Assert.Equal(0x47E970u, battle.NativeActionPackets.Headers[header].Data!.ProducerAddress);
        Assert.Equal((ushort)2, battle.NativeActionPackets.Headers[header].Data!.Argument);
        BattleEvent message = Assert.Single(battle.Events, item => item.Kind == "NativeText");
        Assert.Equal("Message100", message.Message);
        Assert.Equal(16, message.Amount);
        Assert.Equal((byte)0, battle.NativeActionPackets.Headers[header].ReadByte(16));
        Assert.Equal(3, Assert.Single(battle.Combatants[0].MagicStocks).Count);
        Assert.Equal(500, battle.Combatants[1].CurrentHp);
        Assert.DoesNotContain(battle.Events, item => item.Kind == "Damage");
    }

    private static BattleSession Create(ushort saved, uint transient, bool magicOnly = false)
    {
        NativeMagicStock[] magic = Enumerable.Repeat(new NativeMagicStock(0, 0), 32).ToArray();
        magic[5] = new(2, 3);
        NativeItemStock[] items = Enumerable.Repeat(new NativeItemStock(0, 0), 198).ToArray();
        items[5] = new(1, 1);
        BattleNativeAction spell = new(2, 2, 1, 2)
        {
            Name = "Offensive spell",
            MagicId = 2,
            AttackType = 2,
            Power = magicOnly ? (byte)32 : (byte)0,
            HitCount = 1,
            TargetFlags = 0x40,
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
        return new(new BattleDefinition
        {
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            Name = "Compelled action component",
            NativeFieldBattleRules = 0,
            NativeMessageSpeed = 1,
            NativeGlobalMessages = Enumerable.Range(0, 101).Select(index => (string?)$"Message{index}").ToArray(),
            NativeActions = [spell],
            NativeItemInventory = items,
            ActionDefinitions = [new(BattleAbility.Item, 1, "Potion", BattleActionEffect.Heal, 20, true), new(BattleAbility.Magic, 2, "Offensive spell", BattleActionEffect.Native, 0, false) { NativeAction = spell }],
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum, NativeMagicInventory = magic, NativeCommandSlots = magicOnly ? ImmutableArray.Create<byte>(2, 2, 2, 2) : ImmutableArray.Create<byte>(1, 0, 0, 0), NativeStatus0 = saved, NativeStatus1 = transient, Abilities = [BattleAbility.Attack, BattleAbility.Item, BattleAbility.Magic] },
                new() { Id = "enemy", Name = "Enemy", MaximumHp = 500, CurrentHp = 500 }
            ]
        }, BattleAtbMode.Normal);
    }
}
