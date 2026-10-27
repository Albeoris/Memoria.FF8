using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleContactFlagTests
{
    [Theory]
    [InlineData((byte)4, (byte)2, 2u, false, 200)]
    [InlineData((byte)4, (byte)2, 2u, true, 200)]
    [InlineData((byte)4, (byte)2, 0u, false, 100)]
    [InlineData((byte)4, (byte)2, 0u, true, 100)]
    [InlineData((byte)4, (byte)0, 2u, false, 100)]
    [InlineData((byte)4, (byte)1, 2u, false, 100)]
    [InlineData((byte)4, (byte)3, 2u, false, 100)]
    [InlineData((byte)4, (byte)0, 2u, true, 100)]
    [InlineData((byte)4, (byte)1, 2u, true, 100)]
    [InlineData((byte)4, (byte)3, 2u, true, 100)]
    [InlineData((byte)25, (byte)2, 2u, false, 200)]
    [InlineData((byte)25, (byte)2, 2u, true, 200)]
    [InlineData((byte)32, (byte)2, 2u, false, 200)]
    [InlineData((byte)32, (byte)2, 2u, true, 200)]
    public void FixedRecoveryUsesChemistOnlyForItemAttackClass(byte attackType, byte attackFlags, uint passives, bool zombie, int expected)
    {
        BattleSession session = FixedRecoverySession(true, Rules(passives), zombie);

        NativeBattleContactPacket packet = Resolve(session, Action(attackType, attackFlags));

        Assert.Equal(expected, packet.Primary.PresentedAmount);
        Assert.Equal(zombie ? (byte)0 : (byte)1, (byte)(packet.Primary.HitFlags3 & 1));
        Assert.Equal(400 + (zombie ? -expected : expected), session.Combatants.Single(item => item.Id == "target").CurrentHp);
        Assert.Equal((byte)255, packet.Secondary.ActorIndex);
        Assert.Equal(expected, Assert.Single(session.Events, item => item.Kind == (zombie ? "Damage" : "Healing")).Amount);
    }

    [Theory]
    [InlineData((byte)4)]
    [InlineData((byte)25)]
    [InlineData((byte)32)]
    public void EnemyItemAttackClassDoesNotRequirePlayerPassiveRules(byte attackType)
    {
        BattleSession session = FixedRecoverySession(false, null, false);

        NativeBattleContactPacket packet = Resolve(session, Action(attackType, 2));

        Assert.Equal(100, packet.Primary.PresentedAmount);
        Assert.Equal(500, session.Combatants.Single(item => item.Id == "target").CurrentHp);
    }

    [Theory]
    [InlineData((byte)0, 9_999, 9_999)]
    [InlineData((byte)8, 9_999, 60_000)]
    [InlineData((byte)0, 1_234, 1_234)]
    [InlineData((byte)8, 1_234, 1_234)]
    [InlineData((byte)0, 15_000, 15_000)]
    [InlineData((byte)8, 15_000, 15_000)]
    [InlineData((byte)0, 100_000, 70_000)]
    [InlineData((byte)8, 100_000, 70_000)]
    public void NativeWideDamageFlagAndExplicitTuningKeepContactAndHpInAgreement(byte attackFlags, int configuredLimit, int expected)
    {
        BattleSession session = new(new BattleDefinition
        {
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            Tuning = new() { MaximumDamage = configuredLimit },
            Combatants =
            [
                new() { Id = "actor", Name = "Actor", IsPlayer = true, ModelId = 1, MaximumHp = 14_000, CurrentHp = 14_000 },
                new() { Id = "target", Name = "Target", MaximumHp = 200_000, CurrentHp = 200_000 }
            ]
        }, BattleAtbMode.Normal);

        NativeBattleContactPacket packet = Resolve(session, Action(18, attackFlags));

        Assert.Equal(expected, packet.Primary.PresentedAmount);
        Assert.Equal(unchecked((ushort)expected), packet.Primary.Amount6);
        Assert.Equal(200_000 - expected, session.Combatants.Single(item => item.Id == "target").CurrentHp);
        Assert.Equal(expected, Assert.Single(session.Events, item => item.Kind == "Damage").Amount);
        Assert.Equal((byte)255, packet.Secondary.ActorIndex);
    }

    [Theory]
    [InlineData((byte)1, (byte)128, 0u, false)]
    [InlineData((byte)1, (byte)0, 0x4000000u, true)]
    [InlineData((byte)2, (byte)128, 0u, false)]
    [InlineData((byte)2, (byte)0, 0x4000000u, true)]
    public void ForecastImmunityBypassUsesStatusMaskInsteadOfAttackFlagBitSeven(byte attackType, byte attackFlags, uint statuses, bool bypass)
    {
        BattleNativeAction native = Action(attackType, attackFlags) with { Power = 20, Statuses1 = statuses };
        BattleSession session = new(new BattleDefinition
        {
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            Combatants =
            [
                new() { Id = "actor", Name = "Actor", IsPlayer = true, ModelId = 1, Attack = 40, Magic = 40, NativeAttack = native },
                new() { Id = "target", Name = "Target", MaximumHp = 1_000, CurrentHp = 1_000, NativeStatus0 = 4 }
            ]
        }, BattleAtbMode.Normal);
        BattleCombatant actor = session.Combatants.Single(item => item.Id == "actor");
        BattleCombatant target = session.Combatants.Single(item => item.Id == "target");
        string method = attackType == 1 ? "ForecastPhysical" : "ForecastMagic";
        object?[] arguments = attackType == 1 ? [actor, target] : [actor, target, native, 1, null, 0];

        BattleDamageForecast forecast = (BattleDamageForecast)typeof(BattleSession).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, arguments)!;

        if (bypass)
        {
            Assert.True(forecast.ExpectedDamage > 0);
        }
        else
        {
            Assert.Equal(0, forecast.ExpectedDamage);
        }
    }

    private static BattleSession FixedRecoverySession(bool playerActor, BattleNativePlayerStatRules? rules, bool zombie)
    {
        return new(new BattleDefinition
        {
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            Combatants =
            [
                new() { Id = "actor", Name = "Actor", IsPlayer = playerActor, ModelId = 1, MaximumHp = 1_000, CurrentHp = 900, NativePlayerStatRules = rules },
                new() { Id = "target", Name = "Target", IsPlayer = !playerActor, ModelId = 1, MaximumHp = 800, CurrentHp = 400, NativeStatus0 = zombie ? (ushort)0x40 : (ushort)0 }
            ]
        }, BattleAtbMode.Normal);
    }

    private static BattleNativePlayerStatRules Rules(uint passives)
    {
        byte[] character = new byte[36];
        for (int offset = 12; offset < 36; offset += 4)
        {
            character[offset + 1] = 1;
            character[offset + 2] = 100;
            character[offset + 3] = 1;
        }
        return new(0, 1, character.ToImmutableArray(), new byte[12].ToImmutableArray(), Enumerable.Range(0, 57).Select(_ => new byte[60].ToImmutableArray()).ToImmutableArray(), Enumerable.Range(0, 19).Select(_ => new byte[8].ToImmutableArray()).ToImmutableArray(), new byte[20].ToImmutableArray(), 0, MagicSystemFixture.Create().Definitions) { NativeStatusPassiveFlags = passives };
    }

    private static BattleNativeAction Action(byte attackType, byte attackFlags)
    {
        return new(8, 0, -1, -1) { MagicId = 0, AttackType = attackType, Power = 2, HitCount = 1, AttackFlags = attackFlags, TargetFlags = 0, PowerModifier = 0, LevelModifier = 0, DamagePresentation = 4, Element = 0, ElementPercent = 0, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = 255, Critical = 0, RawRecord = [], SupplementalRecord = [] };
    }

    private static NativeBattleContactPacket Resolve(BattleSession session, BattleNativeAction native)
    {
        BattleCombatant actor = session.Combatants.Single(item => item.Id == "actor");
        BattleCombatant target = session.Combatants.Single(item => item.Id == "target");
        BattleActionPresentation presentation = new(actor.Id, target.Id, BattleActionPhase.Windup, 0, BattleAbility.Attack, 0) { NativeAction = native, NativeQueuedCommand = native.Command };
        BattleActionDefinition action = new(BattleAbility.Attack, 0, "Contact", BattleActionEffect.Native, 20, false) { NativeAction = native };
        typeof(BattleSession).GetMethod("BeginNativeReactionBatch", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, []);
        return (NativeBattleContactPacket)typeof(BattleSession).GetMethod("ResolveInstalledNativeHitWithContact", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, [actor, target, presentation, action, native, (byte)255, false])!;
    }
}
