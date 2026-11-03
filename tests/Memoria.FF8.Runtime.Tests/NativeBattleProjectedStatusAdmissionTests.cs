using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleProjectedStatusAdmissionTests
{
    [Theory]
    [InlineData(BattleStatusKind.Poison)]
    [InlineData(BattleStatusKind.Regen)]
    [InlineData(BattleStatusKind.Haste)]
    public void NativeClockRejectsASecondProjectedStatusClockAtConstruction(BattleStatusKind kind)
    {
        BattleDefinition definition = Definition(true);
        definition.Combatants[0] = definition.Combatants[0] with { StatusEffects = [Effect(kind)] };

        InvalidDataException error = Assert.Throws<InvalidDataException>(() => new BattleSession(definition, BattleAtbMode.Normal));

        Assert.Contains("original status WORD/DWORD", error.Message);
    }

    [Fact]
    public void NativeClockRejectsProjectedStatusProducersBeforeTheirActionCanBeQueued()
    {
        BattleDefinition definition = Definition(true) with
        {
            ActionDefinitions = [new(BattleAbility.Magic, 1, "Projected status", BattleActionEffect.Status, 0, false, Statuses: [BattleStatusKind.Poison])]
        };

        InvalidDataException error = Assert.Throws<InvalidDataException>(() => new BattleSession(definition, BattleAtbMode.Normal));

        Assert.Contains("original status producers", error.Message);
    }

    [Fact]
    public void NativeClockRejectsProjectedEffectsIntroducedOnlyInTheSerializedActor()
    {
        BattleSession battle = new(Definition(true), BattleAtbMode.Normal);
        BattleSnapshot saved = battle.Capture();
        BattleCombatantSnapshot[] actors = saved.Combatants.ToArray();
        BattleStatusEffectDefinition effect = Effect(BattleStatusKind.Poison);
        actors[0] = actors[0] with { StatusEffects = [new(effect.Kind, effect.DurationFrames, effect.TickIntervalFrames, effect)] };
        BattleSnapshot conflicting = JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(saved with { Combatants = actors }))!;

        InvalidDataException error = Assert.Throws<InvalidDataException>(() => BattleSession.Restore(conflicting));

        Assert.Contains("projected component status effects", error.Message);
        Assert.Empty(battle.Combatants[0].StatusEffects);
        Assert.Equal(JsonSerializer.Serialize(saved), JsonSerializer.Serialize(battle.Capture()));
    }

    [Fact]
    public void OriginalStatusBitsAndRawGaugeContinueIdenticallyAfterJsonRestore()
    {
        BattleDefinition definition = Definition(true);
        definition.Combatants[0] = definition.Combatants[0] with { NativeStatus0 = 2, NativeStatus1 = 2 };
        BattleSession battle = new(definition, BattleAtbMode.Normal);
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);

        battle.Tick();
        restored.Tick();

        BattleSnapshot next = battle.Capture();
        Assert.Equal((ushort)2, next.Combatants[0].NativeStatus!.Saved);
        Assert.Equal(2u, next.Combatants[0].NativeStatus!.Battle);
        Assert.Equal(46, next.Combatants[0].NativeAtb!.Current);
        Assert.Equal(1000, next.Combatants[0].CurrentHp);
        Assert.Empty(next.Combatants[0].StatusEffects);
        Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(restored.Capture()));
    }

    [Fact]
    public void DeclaredComponentKeepsItsExplicitProjectedPeriodicEffectAcrossJsonRestore()
    {
        BattleDefinition definition = Definition(false);
        definition.Combatants[0] = definition.Combatants[0] with { StatusEffects = [Effect(BattleStatusKind.Poison)] };
        BattleSession battle = new(definition, BattleAtbMode.Normal);
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);

        battle.Tick();
        restored.Tick();

        Assert.Equal(993, battle.Combatants[0].CurrentHp);
        Assert.Single(battle.Combatants[0].StatusEffects);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    private static BattleStatusEffectDefinition Effect(BattleStatusKind kind)
    {
        return new() { Kind = kind, DurationFrames = 300, TickIntervalFrames = 1, Magnitude = 7 };
    }

    private static BattleDefinition Definition(bool nativeClock)
    {
        return new()
        {
            NativeClockRules = nativeClock ? new(10) : null,
            NativeBattleSpeed = nativeClock ? (byte)2 : null,
            Combatants =
            [
                new() { Id = "party", IsPlayer = true, Name = "Party", MaximumHp = 1000, CurrentHp = 1000, Speed = 1 },
                new() { Id = "enemy", Name = "Enemy", MaximumHp = 1000, CurrentHp = 1000, Speed = 1 }
            ]
        };
    }
}
