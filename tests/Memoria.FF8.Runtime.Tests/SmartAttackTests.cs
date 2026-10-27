using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class SmartAttackTests
{
    [Fact]
    public void OpeningPlayerCommandMenuDoesNotDisableSmartMagicWhenLimitIsUnavailable()
    {
        BattleSession battle = Create([Spell(4, 20)], [new(4, 6)], infinite: true, nativeLimit: true);
        BattleSmartAttackPreview before = battle.PreviewSmartAttack("party", "enemy");
        Assert.Equal(BattleAbility.Magic, before.Ability);
        battle.SelectPlayerCommandActor("party");
        Assert.True(battle.HasNativeLimitSelection("party"));
        Assert.Equal(0, battle.GetNativeLimitLevel("party"));
        BattleSmartAttackPreview preview = battle.PreviewSmartAttack("party", "enemy");
        Assert.Equal(BattleAbility.Magic, preview.Ability);
        Assert.Equal(4, preview.ActionId);
        Assert.Equal(1, preview.CastCount);
        Assert.Equal(BattleAbility.Attack, battle.PreviewSmartAttack("party", "enemy", true).Ability);
        battle.QueueSmartAttack(preview);
        Assert.Equal(BattleAbility.Magic, Assert.Single(battle.QueuedActions).Ability);
    }

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

    [Theory]
    [InlineData(false, false, 6, BattleAbility.Attack)]
    [InlineData(false, false, 80, BattleAbility.Magic)]
    [InlineData(true, false, 6, BattleAbility.Attack)]
    [InlineData(true, false, 80, BattleAbility.Magic)]
    [InlineData(false, true, 6, BattleAbility.Attack)]
    [InlineData(false, true, 80, BattleAbility.Draw)]
    [InlineData(true, true, 6, BattleAbility.Attack)]
    [InlineData(true, true, 80, BattleAbility.Draw)]
    public void NoReliableKillComparesStockedAndDrawnMagicAgainstPhysicalDamage(bool gunblade, bool draw, byte spellPower, BattleAbility expected)
    {
        BattleDefinition definition = Create([Spell(1, spellPower)], [new(1, 100)], enemyHp: 60_000).Capture().Definition;
        BattleCombatantDefinition actor = definition.Combatants[0] with
        {
            Attack = 28,
            Magic = 8,
            ModelId = gunblade ? 0 : 1,
            WeaponId = gunblade ? 0 : null,
            NativeAttack = definition.Combatants[0].NativeAttack! with { Power = 20, AttackType = (byte)(gunblade ? 10 : 1) },
            Abilities = [BattleAbility.Attack, draw ? BattleAbility.Draw : BattleAbility.Magic]
        };
        BattleCombatantDefinition target = definition.Combatants[1] with { Defense = 42, Spirit = 59, NativeDrawSlots = [new(1, 0), new(0, 0), new(0, 0), new(0, 0)] };
        NativeKnownMagic known = new(NativeInputConfiguration.OriginalInitHash, ImmutableArray.Create<uint>(0, 0));
        BattleSession battle = new(definition with { Combatants = [actor, target], NativeKnownMagic = known.Add(1) }, BattleAtbMode.TurnBased);
        battle.SetGunbladeMode(BattleGunbladeMode.Automatic);
        BattleSmartAttackPreview physical = battle.PreviewSmartAttack("party", "enemy", true);
        string before = JsonSerializer.Serialize(battle.Capture());
        BattleSmartAttackPreview preview = battle.PreviewSmartAttack("party", "enemy");
        Assert.Equal(expected, preview.Ability);
        Assert.Equal(0, preview.KillProbability);
        Assert.True(physical.ExpectedDamage > 0);
        if (expected == BattleAbility.Attack)
        {
            Assert.Equal(physical.ExpectedDamage, preview.ExpectedDamage);
        }
        else
        {
            Assert.True(preview.ExpectedDamage > physical.ExpectedDamage);
        }
        Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));
        battle.QueueSmartAttack(preview);
        Assert.Equal(expected, Assert.Single(battle.QueuedActions).Ability);
    }

    [Fact]
    public void EqualPositiveExpectedDamageRetainsPhysicalAttack()
    {
        BattleDefinition definition = Create([Spell(1, 4)], [new(1, 100)], enemyHp: 60_000).Capture().Definition;
        BattleCombatantDefinition actor = definition.Combatants[0] with { Magic = 0, NativeAttack = definition.Combatants[0].NativeAttack! with { Power = 64 } };
        BattleSession battle = new(definition with { Combatants = [actor, definition.Combatants[1]] }, BattleAtbMode.TurnBased);
        BattleSmartAttackPreview physical = battle.PreviewSmartAttack("party", "enemy", true);
        BattleSmartAttackPreview preview = battle.PreviewSmartAttack("party", "enemy");
        BattleDamageForecast magic = BattleActionForecaster.FromNativePhases(random => NativeBattleDamage.Magic(0, battle.Combatants[1].Spirit, 4, random(), false), battle.Combatants[1].CurrentHp);
        Assert.True(physical.ExpectedDamage > 0);
        Assert.Equal(physical.ExpectedDamage, magic.ExpectedDamage);
        Assert.Equal(BattleAbility.Attack, preview.Ability);
        Assert.Equal(physical.ExpectedDamage, preview.ExpectedDamage);
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

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void DamageForecastUsesTheSameNumericCapAfterRestore(int cap)
    {
        BattleDefinition definition = Create([Spell(1, 80)], [new(1, 100)], enemyHp: 60000).Capture().Definition;
        BattleSession battle = new(definition with { Tuning = new() { MaximumDamage = cap } }, BattleAtbMode.TurnBased);
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        BattleSmartAttackPreview preview = restored.PreviewSmartAttack("party", "enemy");
        Assert.InRange(preview.ExpectedDamage, 0, cap * preview.CastCount);
        Assert.Equal(0, preview.KillProbability);
        Assert.Equal(battle.PreviewSmartAttack("party", "enemy"), preview);
    }

    private static BattleSession Create(BattleNativeAction[] spells, BattleStock[] stocks, int enemyHp = 100, bool infinite = false, uint actorStatus = 0, int[]? defenses = null, bool mug = false, uint enemyFlags = 0, bool nativeLimit = false)
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
            NativeLimitsDisabled = false,
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            NativeActions = spells,
            ActionDefinitions = spells.Select(spell => new BattleActionDefinition(BattleAbility.Magic, spell.ActionId, spell.Name!, BattleActionEffect.Native, 0, false) { NativeAction = spell }).ToArray(),
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true, ModelId = 1, MagicRecordId = 1, InitialAtb = BattleSession.AtbMaximum, Attack = 1, Magic = 100, Abilities = [BattleAbility.Attack, BattleAbility.Magic], NativeLimit = nativeLimit ? new(1, 11, 0, 255, new byte[32].ToImmutableArray()) : null, NativeAttack = attack, NativePlayerStatRules = (actorStatus & 0x60000) != 0 ? Rules(vitals) : null, NativeGrowth = new(0, new byte[6].ToImmutableArray()), NativeCommandSlots = [(byte)(mug ? 12 : 1), 2, 0, 0], NativeStatus1 = actorStatus },
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
