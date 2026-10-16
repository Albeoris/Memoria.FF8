using Memoria.FF8.Runtime;

namespace Memoria.FF8.Battle.Tests;

public sealed class NativeActionReflectionTests
{
    [Fact]
    public void GroupMagicReflectsOnceWithOriginalCasterAndDoesNotBounceAgain()
    {
        BattleNativeAction magic = Magic();
        BattleSession battle = new(new BattleDefinition
        {
            NativeActions = [magic, magic with { Command = 247 }],
            ActionDefinitions = [new(BattleAbility.Magic, 1, "Fire", BattleActionEffect.Native, 10, false, true) { NativeAction = magic }],
            Combatants =
            [
                new() { Id = "player", Name = "Caster", ModelId = 1, IsPlayer = true, MaximumHp = 1000, CurrentHp = 1000, Magic = 30, InitialAtb = BattleSession.AtbMaximum, NativeStatus1 = 0x80, Abilities = [BattleAbility.Magic], MagicStocks = [new(1, 2)] },
                new() { Id = "first", Name = "Reflector1", ModelId = 2, MaximumHp = 1000, CurrentHp = 1000, NativeStatus1 = 0x80 },
                new() { Id = "second", Name = "Reflector2", ModelId = 3, MaximumHp = 1000, CurrentHp = 1000, NativeStatus1 = 0x80 }
            ]
        }, BattleAtbMode.Normal);
        battle.QueueAction("player", "first", BattleAbility.Magic, 1);
        for (int frame = 0; frame <= BattleSession.AttackWindupFrames; frame++)
        {
            battle.Tick();
        }
        Assert.Equal(2, battle.Events.Count(item => item.Kind == "Reflect"));
        BattleEvent damage = Assert.Single(battle.Events, item => item.Kind == "Damage");
        Assert.Equal("player", damage.ActorId);
        Assert.Equal("player", damage.TargetId);
        Assert.Equal(1000, battle.Combatants[1].CurrentHp);
        Assert.Equal(1000, battle.Combatants[2].CurrentHp);
        Assert.Equal(1, Assert.Single(battle.Combatants[0].MagicStocks).Count);
        NativeBattleLastAction reflected = battle.Capture().Combatants[0].NativeParticipant!.LastAction!;
        Assert.Equal((byte)247, reflected.Command);
        Assert.Equal((byte)1, reflected.AttackerModelId);
    }

    [Theory]
    [InlineData(false, "second")]
    [InlineData(true, "first")]
    public void DisabledAutomaticReplacementRetainsTheOriginalNativeMask(bool disabled, string expected)
    {
        BattleNativeAction magic = Magic() with { TargetFlags = 0x10, AttackFlags = 1 };
        BattleDefinition definition = new()
        {
            NativeActions = [magic],
            Combatants =
            [
                new() { Id = "player", Name = "Caster", ModelId = 1, IsPlayer = true, Magic = 30, InitialAtb = BattleSession.AtbMaximum, NativeCommands = [new(2, "Magic", 0x10, 1)], MagicStocks = [new(1, 2)] },
                new() { Id = "first", Name = "Untargetable", MaximumHp = 1000, CurrentHp = 1000, InitialUntargetable = true },
                new() { Id = "second", Name = "Replacement", MaximumHp = 1000, CurrentHp = 1000 }
            ]
        };
        BattleSession battle = new(definition, BattleAtbMode.Normal);
        battle = BattleSession.Restore(battle.Capture() with { NativeScriptState = new() { DisableAutomaticTargetReplacement = disabled } });
        battle.QueueNativePlayerCommand("player", "first", 2, 1);
        for (int frame = 0; frame <= BattleSession.AttackWindupFrames; frame++)
        {
            battle.Tick();
        }
        Assert.Equal(expected, Assert.Single(battle.Events, item => item.Kind == "Damage").TargetId);
    }

    private static BattleNativeAction Magic()
    {
        return new(2, 1, 1, 1) { Name = "Fire", MagicId = 1, AttackType = 2, Power = 10, HitCount = 1, TargetFlags = 0x40, AttackFlags = 0x11, DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = [], SupplementalRecord = [] };
    }
}
