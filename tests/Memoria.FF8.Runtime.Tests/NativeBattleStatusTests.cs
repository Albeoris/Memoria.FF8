using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleStatusTests
{
    [Theory]
    [InlineData(249, 0, true, 2)]
    [InlineData(250, 0, true, 1)]
    [InlineData(250, 200, false, 1)]
    [InlineData(255, 255, true, 1)]
    public void NativeChanceBandsPreserveImmunityBypassAndPerStatusRandomConsumption(byte chance, byte resistance, bool applied, int expectedDraws)
    {
        BattleSession battle = CreateBattle(Action(2, 0, chance), resistance);

        Hit(battle);

        NativeBattleStatusSnapshot status = battle.Capture().Combatants.Single(actor => actor.Id == "enemy").NativeStatus!;
        Assert.Equal(applied, (status.Saved & 2) != 0);
        NativeBattleRandomState expected = NativeBattleRandomState.Initialize(0);
        for (int draw = 0; draw < expectedDraws; draw++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected.Indexes.ToArray(), battle.Capture().Random!.Indexes.ToArray());
        Assert.Equal(expected.Lane, battle.Capture().Random!.Lane);
    }

    [Fact]
    public void NewSpeedStatusReplacesTheOldExclusiveGroupAndSurvivesStandaloneRestore()
    {
        BattleSession battle = CreateBattle(Action(0, 4, 255), initialBattle: 2);

        Hit(battle);

        BattleSnapshot snapshot = battle.Capture();
        NativeBattleStatusSnapshot status = snapshot.Combatants.Single(actor => actor.Id == "enemy").NativeStatus!;
        Assert.Equal(4u, status.Battle & 0xE);
        Assert.InRange(status.Timers[2], (short)1, (short)120);
        Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(BattleSession.Restore(snapshot).Capture()));
    }

    [Fact]
    public void ZombieCannotReceiveDoomEvenWhenTheActionBypassesResistances()
    {
        BattleSession battle = CreateBattle(Action(0, 0x400, 255), initialSaved: 0x40);

        Hit(battle);

        Assert.Equal(0u, battle.Capture().Combatants.Single(actor => actor.Id == "enemy").NativeStatus!.Battle & 0x400);
    }

    [Fact]
    public void ApplyingZombieRemovesExistingDoomAndItsTimer()
    {
        BattleSession battle = CreateBattle(Action(0x40, 0, 255), initialBattle: 0x400);

        Hit(battle);

        NativeBattleStatusSnapshot status = battle.Capture().Combatants.Single(actor => actor.Id == "enemy").NativeStatus!;
        Assert.Equal((ushort)0x40, (ushort)(status.Saved & 0x40));
        Assert.Equal(0u, status.Battle & 0x400);
        Assert.Equal(-1111, status.Timers[10]);
    }

    [Fact]
    public void OriginalStatusWordFlagBitsAreNotTreatedAsInflictableStatuses()
    {
        BattleSession battle = CreateBattle(Action(0x80, 0, 255));

        Hit(battle);

        Assert.Equal((ushort)0, battle.Capture().Combatants.Single(actor => actor.Id == "enemy").NativeStatus!.Saved);
    }

    [Fact]
    public void RawConfuseAndSavedStatusBitsAreRetainedWithoutAddingScaffoldEffects()
    {
        BattleSession battle = CreateBattle(Action(0, 0x4000, 255), initialSaved: 0x80);

        Hit(battle);

        BattleSnapshot snapshot = battle.Capture();
        BattleCombatantSnapshot enemy = snapshot.Combatants.Single(actor => actor.Id == "enemy");
        Assert.Equal((ushort)0x80, enemy.NativeStatus!.Saved);
        Assert.Equal(0x4000u, enemy.NativeStatus.Battle & 0x4000);
        Assert.Empty(enemy.StatusEffects);
        Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(BattleSession.Restore(snapshot).Capture()));
    }

    private static void Hit(BattleSession battle)
    {
        battle.QueueAction("party", "enemy", BattleAbility.Magic, 1);
        for (int frame = 0; frame <= BattleSession.AttackWindupFrames; frame++)
        {
            battle.Tick();
        }
        Assert.Equal(BattleActionPhase.Recovery, battle.ActiveAction!.Phase);
    }

    private static BattleSession CreateBattle(BattleNativeAction native, byte resistance = 0, ushort initialSaved = 0, uint initialBattle = 0)
    {
        BattleDefinition definition = new()
        {
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            Name = "Native status component",
            NativeStatusRules = new(0, Enumerable.Repeat((byte)30, 14).ToImmutableArray()),
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum, Magic = 20, MagicStocks = [new(1, 100)], Abilities = [BattleAbility.Attack, BattleAbility.Magic] },
                new() { Id = "enemy", Name = "Enemy", NativeStatus0 = initialSaved, NativeStatus1 = initialBattle, NativeStatusResistances = Enumerable.Repeat(resistance, 40).ToArray() }
            ],
            ActionDefinitions = [new(BattleAbility.Magic, 1, "Native status", BattleActionEffect.Status, 0, false) { NativeAction = native }]
        };
        return new(definition, BattleAtbMode.Normal);
    }

    private static BattleNativeAction Action(ushort saved, uint battle, byte chance)
    {
        return new(2, 1, 1, 1)
        {
            MagicId = 0,
            AttackType = 2,
            Power = 0,
            HitCount = 1,
            TargetFlags = 16,
            AttackFlags = 1,
            DamagePresentation = 0,
            Element = 0,
            ElementPercent = null,
            StatusAttack = chance,
            Statuses0 = saved,
            Statuses1 = battle,
            Hit = null,
            Critical = null,
            PowerModifier = null,
            LevelModifier = null,
            RawRecord = [],
            SupplementalRecord = []
        };
    }
}
