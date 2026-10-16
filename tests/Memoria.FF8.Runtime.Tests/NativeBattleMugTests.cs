using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMugTests
{
    [Fact]
    public void SuccessfulMugAddsTheOriginalBandItemAndStillPerformsItsWeaponHit()
    {
        BattleSession battle = Create(255);

        Hit(battle);

        Assert.Equal(new BattleStock(1, 2), Assert.Single(battle.Items));
        Assert.True(battle.Combatants.Single(actor => !actor.IsPlayer).CurrentHp < 500);
        Assert.NotEqual(0u, battle.Combatants.Single(actor => !actor.IsPlayer).NativeCombatantFlags & 0x800);
        Assert.Equal("Stole 2 Potions!", Assert.Single(battle.Events, entry => entry.Kind == "NativeText").Message);
        Assert.NotEqual(0u, BattleSession.Restore(battle.Capture()).Combatants.Single(actor => !actor.IsPlayer).NativeCombatantFlags & 0x800);
    }

    [Fact]
    public void ZeroRateUsesTheNothingMessageAndStillHitsWithoutATheftRoll()
    {
        BattleSession battle = Create(0);

        Hit(battle);

        Assert.Empty(battle.Items);
        Assert.True(battle.Combatants.Single(actor => !actor.IsPlayer).CurrentHp < 500);
        Assert.Equal("Nothing to steal", Assert.Single(battle.Events, entry => entry.Kind == "NativeText").Message);
        Assert.Equal(0u, battle.Combatants.Single(actor => !actor.IsPlayer).NativeCombatantFlags & 0x800);
    }

    [Fact]
    public void FailedTheftStillContinuesTheWeaponAttack()
    {
        BattleSession battle = Create(1);

        Hit(battle);

        Assert.Empty(battle.Items);
        Assert.True(battle.Combatants.Single(actor => !actor.IsPlayer).CurrentHp < 500);
        Assert.Equal("Failed", Assert.Single(battle.Events, entry => entry.Kind == "NativeText").Message);
    }

    [Fact]
    public void MuggedEnemyDoesNotProduceADropWhenTheSameAttackDefeatsIt()
    {
        BattleSession battle = Create(255, 5, true);

        Hit(battle);

        Assert.Equal(BattleOutcome.Victory, battle.Outcome);
        Assert.Equal(-1, Assert.Single(battle.EnemyDefeats).DropSelection);
        Assert.Equal(new BattleStock(1, 2), Assert.Single(battle.Items));
    }

    [Fact]
    public void PlayerAllocationOwnsTheAlreadyStolenBitBeforeAnyMugAttempt()
    {
        BattleSession battle = Create(255);

        Assert.Equal(0x8801u, battle.Combatants.Single(actor => actor.IsPlayer).NativeCombatantFlags & 0x8801);
    }

    private static void Hit(BattleSession battle)
    {
        battle.QueueNativePlayerCommand("party-0", "enemy", 12, 0);
        for (int frame = 0; frame <= BattleSession.AttackWindupFrames; frame++)
        {
            battle.Tick();
        }
    }

    private static BattleSession Create(byte rate, int enemyHp = 500, bool rewards = false)
    {
        BattleNativeAction attack = Action(1, 0) with { Name = "Weapon attack", AttackType = 1, Power = 16, Hit = 255, AttackFlags = 0 };
        BattleNativeAction potion = Action(4, 1) with { Name = "Potion" };
        string?[] messages = new string?[120];
        messages[11] = " 0123456789";
        messages[17] = "Failed";
        messages[18] = "Nothing to steal";
        messages[19] = "Stole";
        messages[71] = "";
        messages[115] = "!";
        messages[119] = "s";
        return new(new BattleDefinition
        {
            Name = "Native Mug component",
            NativeMessageSpeed = 0,
            NativeGlobalMessages = messages,
            NativeActions = [potion],
            Combatants =
            [
                new() { Id = "party-0", Name = "Party", IsPlayer = true, ModelId = 1, InitialAtb = BattleSession.AtbMaximum, Attack = 100, Speed = 1, Hit = 255, NativeAttack = attack, NativeCommands = ImmutableArray.Create(new BattleNativeCommandBinding(12, "Mug", 0x50, 0)) },
                new() { Id = "enemy", Name = "Enemy", MaximumHp = enemyHp, CurrentHp = enemyHp, Defense = 0, Evasion = 0, NativeMug = new(rate, 10, 20, Enumerable.Repeat(new BattleStock(1, 2), 12).ToImmutableArray()), RewardSlot = rewards ? 0 : null, Rewards = rewards ? new(0, 0, 10, 20, 255, false, 255, Enumerable.Repeat(new BattleStock(2, 1), 12).ToImmutableArray()) : null }
            ]
        }, BattleAtbMode.Normal);
    }

    private static BattleNativeAction Action(byte command, ushort id)
    {
        return new(command, id, 4, id)
        {
            MagicId = 0,
            AttackType = 4,
            Power = 0,
            HitCount = 1,
            TargetFlags = 0x50,
            AttackFlags = 0,
            DamagePresentation = 0,
            Element = 0,
            ElementPercent = null,
            StatusAttack = 0,
            Statuses0 = 0,
            Statuses1 = 0,
            Hit = null,
            Critical = 0,
            PowerModifier = null,
            LevelModifier = null,
            RawRecord = [],
            SupplementalRecord = []
        };
    }
}
