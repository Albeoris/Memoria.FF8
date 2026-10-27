using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class GameplayLaboratoryTuningTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EditedLaboratoryStockUsesCapturedUnlimitedRuleWithoutAttachingSavedVitals(bool native)
    {
        BattleSession battle = Create(native, unlimited: true);
        Assert.Null(battle.MagicVitals);
        Assert.True(battle.UnlimitedMagic);
        Assert.False(battle.QuoteMagic("caster", 2).Allowed);
        Assert.Equal(int.MaxValue, battle.GetAvailableMagicCasts("caster", 1));
        battle.QueueAction("caster", "enemy", BattleAbility.Magic, 1);
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        Complete(battle);
        Complete(restored);
        Assert.Null(restored.MagicVitals);
        Assert.Equal(1, Assert.Single(restored.Combatants[0].MagicStocks).Count);
        Assert.Equal(123, restored.Combatants[0].Magic);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        Assert.Equal(1, restored.Events.Count(entry => entry.Kind == "Damage" && entry.ActorId == "caster" && entry.TargetId == "enemy"));
        Assert.Equal(int.MaxValue, restored.GetAvailableMagicCasts("caster", 1));
        if (native)
        {
            restored.Combatants[0].Atb = BattleSession.AtbMaximum;
            restored.QueueAction("caster", "enemy", BattleAbility.Magic, 1);
            Complete(restored);
            Assert.Equal(1, Assert.Single(restored.Combatants[0].MagicStocks).Count);
            Assert.Equal(2, restored.Events.Count(entry => entry.Kind == "Damage" && entry.ActorId == "caster" && entry.TargetId == "enemy"));
        }
    }

    [Fact]
    public void LaboratoryWithoutUnlimitedRuleStillConsumesItsLastEditedStock()
    {
        BattleSession battle = Create(native: true, unlimited: false);
        Assert.Equal(1, battle.GetAvailableMagicCasts("caster", 1));
        battle.QueueAction("caster", "enemy", BattleAbility.Magic, 1);
        Complete(battle);
        Assert.Empty(battle.Combatants[0].MagicStocks);
        Assert.Equal(0, battle.GetAvailableMagicCasts("caster", 1));
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(64u)]
    public void LaboratoryTripleUsesOneEditedStockWithoutDepletionOrCanonicalVitals(uint passives)
    {
        NativeCharacterVitals fixture = MagicSystemFixture.Create();
        byte[] character = new byte[36];
        for (int offset = 12; offset < 36; offset += 4)
        {
            character[offset + 1] = 1;
            character[offset + 2] = 100;
            character[offset + 3] = 1;
        }
        BattleNativePlayerStatRules rules = new(0, 1, character.ToImmutableArray(), new byte[12].ToImmutableArray(), Enumerable.Range(0, 57).Select(_ => new byte[60].ToImmutableArray()).ToImmutableArray(), Enumerable.Range(0, 19).Select(_ => new byte[8].ToImmutableArray()).ToImmutableArray(), new byte[20].ToImmutableArray(), 0, fixture.Definitions) { NativeStatusPassiveFlags = passives };
        BattleDefinition definition = Create(native: true, unlimited: true).Capture().Definition;
        BattleCombatantDefinition[] actors = definition.Combatants.ToArray();
        BattleNativeCharacterGrowth growth = new(0, new byte[6].ToImmutableArray());
        int maximumHp = rules.Project([new NativeMagicStock(1, 1)], growth).MaximumHp;
        actors[0] = actors[0] with { MaximumHp = maximumHp, CurrentHp = maximumHp, NativeStatus1 = 0x40000, NativePlayerStatRules = rules, NativeGrowth = growth, NativeStatusResistances = rules.ProjectStatusResistances([new NativeMagicStock(1, 1)]).ToArray() };
        BattleSession battle = new(definition with { Combatants = actors }, BattleAtbMode.TurnBased);
        Assert.Null(battle.MagicVitals);
        Assert.Contains(3, battle.GetNativeMagicCastCounts("caster", 1));
        battle.QueueAction("caster", "enemy", BattleAbility.Magic, 1, magicTargets: ["enemy", "enemy", "enemy"]);
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        Complete(restored);
        Assert.Equal(3, restored.Events.Count(entry => entry.Kind == "Damage" && entry.ActorId == "caster" && entry.TargetId == "enemy"));
        Assert.Equal(1, Assert.Single(restored.Combatants[0].MagicStocks).Count);
        Assert.Null(restored.MagicVitals);
    }

    private static BattleSession Create(bool native, bool unlimited)
    {
        BattleNativeAction spell = new(2, 1, 1, 1) { Name = "Spell", MagicId = 1, AttackType = 2, Power = 20, HitCount = 1, TargetFlags = 0x50, AttackFlags = 1, DamagePresentation = 0, Element = 0, ElementPercent = null, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, RawRecord = [], SupplementalRecord = [] };
        return new(new BattleDefinition
        {
            Tuning = new() { UnlimitedMagic = unlimited, MaximumDamage = 1234 },
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            NativeActions = native ? [spell] : [],
            ActionDefinitions = [new(BattleAbility.Magic, 1, "Spell", native ? BattleActionEffect.Native : BattleActionEffect.Damage, 20, false) { NativeAction = native ? spell : null }],
            Combatants =
            [
                new() { Id = "caster", Name = "Caster", IsPlayer = true, Magic = 123, InitialAtb = BattleSession.AtbMaximum, MagicStocks = [new(1, 1)], Abilities = [BattleAbility.Magic], NativeCommandSlots = [2, 0, 0, 0] },
                new() { Id = "enemy", Name = "Enemy", MaximumHp = 60000, CurrentHp = 60000 }
            ]
        }, BattleAtbMode.TurnBased);
    }

    private static void Complete(BattleSession battle)
    {
        for (int frame = 0; frame < 30; frame++)
        {
            battle.Tick();
        }
    }
}
