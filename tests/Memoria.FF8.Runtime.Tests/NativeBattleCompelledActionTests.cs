using System.Collections.Immutable;

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

    private static BattleSession Create(ushort saved, uint transient)
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
            Power = 0,
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
            Name = "Compelled action component",
            NativeFieldBattleRules = 0,
            NativeActions = [spell],
            NativeItemInventory = items,
            ActionDefinitions = [new(BattleAbility.Item, 1, "Potion", BattleActionEffect.Heal, 20, true)],
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum, NativeMagicInventory = magic, NativeCommandSlots = ImmutableArray.Create<byte>(1, 0, 0, 0), NativeStatus0 = saved, NativeStatus1 = transient, Abilities = [BattleAbility.Attack, BattleAbility.Item] },
                new() { Id = "enemy", Name = "Enemy", MaximumHp = 500, CurrentHp = 500 }
            ]
        }, BattleAtbMode.Normal);
    }
}
