using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class SmartAttackTests
{
    [Fact]
    public void ElementFamilyUsesAggregateAvailabilityThenWeakestReliableSpell()
    {
        BattleSession battle = Create([Spell(1, 20, 1), Spell(2, 40, 1), Spell(4, 80, 2)], [new(1, 40), new(2, 40), new(4, 50)], enemyHp: 100);
        BattleSmartAttackPreview preview = battle.PreviewSmartAttack("party", "enemy");
        Assert.Equal(BattleAbility.Magic, preview.Ability);
        Assert.Equal(1, preview.ActionId);
        Assert.True(preview.KillProbability > 0.9);
    }

    [Fact]
    public void NoReliableKillChoosesStrongestPositiveSpellAndAbsorptionLoses()
    {
        BattleSession battle = Create([Spell(1, 20, 1), Spell(2, 40, 2)], [new(1, 100), new(2, 10)], enemyHp: 60_000, defenses: [1000, 800, 800, 800, 800, 800, 800, 800]);
        BattleSmartAttackPreview preview = battle.PreviewSmartAttack("party", "enemy");
        Assert.Equal(2, preview.ActionId);
        Assert.True(preview.ExpectedDamage > 0);
        Assert.Equal(0, preview.KillProbability);
    }

    [Fact]
    public void PreviewDoesNotMutateRngStocksTicketsOrQueueAndForcePhysicalIsExplicit()
    {
        BattleSession battle = Create([Spell(1, 20)], [new(1, 100)], infinite: true);
        string before = JsonSerializer.Serialize(battle.Capture());
        Assert.Equal(BattleAbility.Magic, battle.PreviewSmartAttack("party", "enemy").Ability);
        Assert.Equal(BattleAbility.Attack, battle.PreviewSmartAttack("party", "enemy", true).Ability);
        Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));
    }

    [Theory]
    [InlineData(0x20000u, 2)]
    [InlineData(0x40000u, 3)]
    public void MultiCastUsesOnlyAffordableNumber(uint status, int expected)
    {
        BattleSession finite = Create([Spell(1, 20)], [new(1, expected)], actorStatus: status);
        Assert.Equal(expected, finite.PreviewSmartAttack("party", "enemy").CastCount);
        BattleSession limited = Create([Spell(1, 20)], [new(1, 100)], infinite: true, actorStatus: status);
        Assert.Equal(1, limited.PreviewSmartAttack("party", "enemy").CastCount);
    }

    [Fact]
    public void MugRetainsPhysicalUntilTargetItemIsMarkedStolen()
    {
        BattleSession stocked = Create([Spell(1, 20)], [new(1, 100)], mug: true);
        Assert.Equal("Mug", stocked.PreviewSmartAttack("party", "enemy").Name);
        Assert.Equal(BattleAbility.Attack, stocked.PreviewSmartAttack("party", "enemy").Ability);
        BattleSession stolen = Create([Spell(1, 20)], [new(1, 100)], mug: true, enemyFlags: 0x800);
        Assert.Equal(BattleAbility.Magic, stolen.PreviewSmartAttack("party", "enemy").Ability);
    }

    [Fact]
    public void NativeMetadataExcludesMixedRandomGroupAndMultipleHitMagic()
    {
        BattleNativeAction pure = Spell(1, 20);
        BattleNativeAction[] excluded = [pure with { Statuses0 = 1 }, pure with { Statuses1 = 1 }, pure with { TargetFlags = 0x30 }, pure with { TargetFlags = 0 }, pure with { HitCount = 2 }, pure with { AttackType = 6 }];
        foreach (BattleNativeAction native in excluded)
        {
            BattleSession battle = Create([native], [new(1, 100)]);
            Assert.Equal(BattleAbility.Attack, battle.PreviewSmartAttack("party", "enemy").Ability);
        }
        BattleSession angelWing = Create([pure], [new(1, 100)], actorStatus: 0x2000000);
        Assert.Equal(BattleAbility.Attack, angelWing.PreviewSmartAttack("party", "enemy").Ability);
    }

    [Fact]
    public void LastUseReservationSurvivesCheckpointAndDoesNotInvalidateItsOwnExecution()
    {
        BattleSession battle = Create([Spell(1, 20)], [new(1, 100)], infinite: true);
        BattleSmartAttackPreview preview = battle.PreviewSmartAttack("party", "enemy");
        battle.QueueSmartAttack(preview);
        BattleSnapshot saved = battle.Capture();
        Assert.Single(saved.MagicVitals!.MagicSystem.Reservations);
        Assert.Equal(0, battle.GetAvailableMagicCasts("party", 1));
        BattleSession restored = BattleSession.Restore(saved);
        Assert.Single(restored.MagicVitals!.MagicSystem.Reservations);
        for (int frame = 0; frame <= BattleSession.AttackWindupFrames; frame++)
        {
            restored.Tick();
        }
        Assert.Equal(100, restored.MagicVitals!.GetMagicCount(1, 1));
        Assert.Equal(1, restored.MagicVitals.MagicSystem.SharedBattleMasks[1]);
        Assert.Empty(restored.MagicVitals.MagicSystem.Reservations);
        Assert.Equal(0, restored.MagicVitals.MagicSystem.SharedFieldMasks[1]);
    }

    [Fact]
    public void ExactNinetyPercentIsNotReliableAndStrictlyGreaterIs()
    {
        Assert.Equal(0.9, BattleActionForecaster.FromSamples([(100, 0.9), (0, 0.1)], 100).KillProbability, 12);
        Assert.True(BattleActionForecaster.FromSamples([(100, 0.9001), (0, 0.0999)], 100).KillProbability > 0.9);
    }

    [Fact]
    public void NativeUnknownPhasePreservesSequentialTableCorrelation()
    {
        BattleDamageForecast correlated = BattleActionForecaster.FromNativePhases(draw => draw() % 33 == draw() % 33 ? 1 : 0, 1);
        double independent = Enumerable.Range(0, 33).Sum(residue => Math.Pow(Enumerable.Range(0, 256).Count(value => value % 33 == residue) / 256.0, 2));
        Assert.Equal(9 / 256.0, correlated.KillProbability, 12);
        Assert.NotEqual(independent, correlated.KillProbability);
        Assert.Equal(1, correlated.Distribution.Values.Sum(), 12);
    }

    private static BattleSession Create(BattleNativeAction[] spells, BattleStock[] stocks, int enemyHp = 100, bool infinite = false, uint actorStatus = 0, int[]? defenses = null, bool mug = false, uint enemyFlags = 0)
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create();
        foreach (BattleStock stock in stocks)
        {
            vitals = vitals.ChangeMagicStock(0, stock.Id, stock.Count, MagicOperationReason.ScriptReward, out _);
        }
        vitals = MagicSystemFixture.Modes(vitals, shared: true, infinite: infinite);
        vitals = vitals with { MagicSystem = vitals.MagicSystem with { SmartAttack = true } };
        vitals.MagicSystem.AttackModes[1] = MagicAttackMode.SmartMagic;
        BattleNativeAction attack = Spell(0, 1) with { Command = 1, AttackType = 1, Hit = 255, Critical = 0, Name = "Attack" };
        return new(new BattleDefinition
        {
            MagicVitals = vitals,
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            NativeActions = spells,
            ActionDefinitions = spells.Select(spell => new BattleActionDefinition(BattleAbility.Magic, spell.ActionId, spell.Name!, BattleActionEffect.Native, 0, false) { NativeAction = spell }).ToArray(),
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true, ModelId = 1, MagicRecordId = 1, InitialAtb = BattleSession.AtbMaximum, Attack = 1, Magic = 100, Abilities = [BattleAbility.Attack, BattleAbility.Magic], NativeAttack = attack, NativePlayerStatRules = (actorStatus & 0x60000) != 0 ? Rules(vitals) : null, NativeGrowth = new(0, new byte[6].ToImmutableArray()), NativeCommandSlots = [(byte)(mug ? 12 : 1), 2, 0, 0], NativeStatus1 = actorStatus },
                new() { Id = "enemy", Name = "Enemy", ModelId = 2, MaximumHp = enemyHp, CurrentHp = enemyHp, ElementDefenses = defenses ?? [800, 800, 800, 800, 800, 800, 800, 800], NativeCombatantFlags = enemyFlags, NativeMug = mug ? new(255, 20, 30, Enumerable.Repeat(new BattleStock(1, 1), 12).ToImmutableArray()) : null }
            ]
        }, BattleAtbMode.TurnBased);
    }

    private static BattleNativePlayerStatRules Rules(NativeCharacterVitals vitals)
    {
        byte[] character = new byte[36];
        for (int offset = 12; offset < 36; offset += 4)
        {
            character[offset + 1] = 1;
            character[offset + 2] = 100;
            character[offset + 3] = 1;
        }
        return new(1, 1, character.ToImmutableArray(), new byte[12].ToImmutableArray(), Enumerable.Range(0, 57).Select(_ => new byte[60].ToImmutableArray()).ToImmutableArray(), Enumerable.Range(0, 19).Select(_ => new byte[8].ToImmutableArray()).ToImmutableArray(), new byte[20].ToImmutableArray(), 0, vitals.Definitions) { NativeStatusPassiveFlags = 0 };
    }

    private static BattleNativeAction Spell(ushort id, byte power, byte element = 0) => new(2, id, 1, id)
    {
        Name = $"Spell{id}", MagicId = id, AttackType = 2, Power = power, HitCount = 1, TargetFlags = 0x50, AttackFlags = 1, DamagePresentation = 0, Element = element, ElementPercent = null, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, RawRecord = [], SupplementalRecord = []
    };
}
