using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime.Tests;

public sealed class MeltdownDefenseTests
{
    [Theory]
    [InlineData(0, 201, 151)]
    [InlineData(50, 100, 75)]
    [InlineData(100, 0, 0)]
    public void AppliedMeltdownUsesProjectedStatsAndSurvivesJsonWithoutStacking(int reduction, int vitality, int spirit)
    {
        BattleSession battle = Create(reduction);
        Apply(battle, Meltdown());
        Assert.Equal(reduction, Status(battle).MeltdownDefenseReductionPercent);
        AssertDefense(battle, vitality, spirit);
        Apply(battle, Meltdown());
        AssertDefense(battle, vitality, spirit);
        BattleSnapshot snapshot = battle.Capture();
        snapshot = snapshot with { Definition = snapshot.Definition with { Tuning = new() { MeltdownDefenseReductionPercent = 100 - reduction } } };
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(snapshot))!);
        AssertDefense(restored, vitality, spirit);
        Assert.Equal(reduction, Status(restored).MeltdownDefenseReductionPercent);
    }

    [Theory]
    [InlineData(1, 49, 1, 49)]
    [InlineData(2, 48, 1, 49)]
    [InlineData(2, 49, 7, 49)]
    [InlineData(2, 49, 1, 48)]
    public void OtherVitZeroProducersKeepNativeDefenseReduction(int command, int actionId, int section, int index)
    {
        BattleSession battle = Create(50);
        Apply(battle, Meltdown() with { Command = (byte)command, ActionId = (ushort)actionId, KernelSection = section, KernelIndex = index });
        Assert.Null(Status(battle).MeltdownDefenseReductionPercent);
        AssertDefense(battle, 0, 0);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(22)]
    public void ResourceModsCanChangeMeltdownDamageTypeWithoutLosingItsStatusBehavior(byte attackType)
    {
        BattleSession battle = Create(50);
        Apply(battle, Meltdown() with { AttackType = attackType });
        AssertDefense(battle, 100, 75);
    }

    [Fact]
    public void DrawCastMeltdownUsesTheSameReduction()
    {
        BattleSession battle = Create(50);
        Apply(battle, Meltdown() with { Command = 247 });
        AssertDefense(battle, 100, 75);
    }

    [Fact]
    public void ExistingNativeVitZeroIsNotReplacedByMeltdown()
    {
        BattleSession battle = Create(50, initial: 0x1000000);
        Apply(battle, Meltdown());
        Assert.Null(Status(battle).MeltdownDefenseReductionPercent);
        AssertDefense(battle, 0, 0);
    }

    [Fact]
    public void CureAndAiClearRetireProvenanceBeforeAnotherSourceAppliesVitZero()
    {
        BattleSession battle = Create(50);
        Apply(battle, Meltdown());
        Apply(battle, Meltdown(), cure: true);
        Assert.Null(Status(battle).MeltdownDefenseReductionPercent);
        AssertDefense(battle, 201, 151);
        Apply(battle, Meltdown());
        typeof(BattleSession).GetMethod("SetNativeAiStatus", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [battle.Combatants[1], 40, 0]);
        Assert.Null(Status(battle).MeltdownDefenseReductionPercent);
        Apply(battle, Meltdown() with { KernelSection = 7 });
        AssertDefense(battle, 0, 0);
    }

    [Fact]
    public void LegacySnapshotWithoutProvenanceKeepsNativeVitZero()
    {
        BattleSession battle = Create(50, initial: 0x1000000);
        JsonObject json = JsonNode.Parse(JsonSerializer.Serialize(battle.Capture()))!.AsObject();
        json["Definition"]!["Tuning"]!.AsObject().Remove("MeltdownDefenseReductionPercent");
        foreach (JsonNode? actor in json["Combatants"]!.AsArray())
        {
            actor!["NativeStatus"]!.AsObject().Remove("MeltdownDefenseReductionPercent");
        }
        BattleSession restored = BattleSession.Restore(json.Deserialize<BattleSnapshot>()!);
        Assert.Equal(100, restored.Capture().Definition.Tuning.MeltdownDefenseReductionPercent);
        AssertDefense(restored, 0, 0);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(36)]
    [InlineData(2)]
    [InlineData(15)]
    [InlineData(22)]
    [InlineData(5)]
    [InlineData(6)]
    public void DamageConsumersUsePartialDefenseAndRetainNativeBypasses(int attackType)
    {
        ushort saved = attackType is 5 or 6 ? (ushort)0x40 : (ushort)0;
        BattleSession battle = Create(50, saved: saved);
        Apply(battle, Meltdown());
        BattleSession expected = Create(100, vitality: 100, spirit: 75, saved: saved);
        BattleNativeAction attack = Meltdown() with { Command = 249, ActionId = 0, KernelSection = -1, KernelIndex = -1, AttackType = (byte)attackType, Power = 32, Hit = 255, Critical = 0, Statuses1 = 0, StatusAttack = 0, DamagePresentation = 4 };
        Assert.Equal(ResolveDamage(expected, attack), ResolveDamage(battle, attack));
        Assert.Equal(expected.Combatants[1].CurrentHp, battle.Combatants[1].CurrentHp);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(50, 16)]
    [InlineData(100, 16)]
    public void SubsequentStatusApplicationUsesReducedSpirit(int reduction, int expectedSaved)
    {
        BattleSession battle = Create(reduction, spirit: 255, magic: 0);
        Apply(battle, Meltdown());
        Apply(battle, Meltdown() with { ActionId = 1, KernelIndex = 1, Statuses0 = 16, Statuses1 = 0, StatusAttack = 250 });
        Assert.Equal((ushort)expectedSaved, Status(battle).Saved);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void TuningRejectsOutOfRangeReduction(int reduction)
    {
        Assert.Throws<InvalidDataException>(() => new GameplayTuning { MeltdownDefenseReductionPercent = reduction }.Validate());
    }

    private static void AssertDefense(BattleSession battle, int vitality, int spirit)
    {
        MethodInfo effective = typeof(BattleSession).GetMethod("EffectiveNativeDefense", BindingFlags.Static | BindingFlags.NonPublic)!;
        BattleCombatant target = battle.Combatants[1];
        Assert.Equal(vitality, (int)effective.Invoke(null, [target, target.Defense])!);
        Assert.Equal(spirit, (int)effective.Invoke(null, [target, target.Spirit])!);
        Assert.Equal(201, target.Defense);
        Assert.Equal(151, target.Spirit);
    }

    private static NativeBattleStatusSnapshot Status(BattleSession battle)
    {
        return battle.Capture().Combatants[1].NativeStatus!;
    }

    private static void Apply(BattleSession battle, BattleNativeAction action, bool cure = false)
    {
        typeof(BattleSession).GetMethod("ApplyNativeActionStatuses", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(battle, [battle.Combatants[0], battle.Combatants[1], action, cure]);
    }

    private static int ResolveDamage(BattleSession battle, BattleNativeAction native)
    {
        BattleCombatant actor = battle.Combatants[0];
        BattleCombatant target = battle.Combatants[1];
        BattleActionPresentation presentation = new(actor.Id, target.Id, BattleActionPhase.Windup, 0, BattleAbility.Attack, 0) { NativeAction = native, NativeQueuedCommand = native.Command };
        BattleActionDefinition action = new(BattleAbility.Attack, 0, "Contact", BattleActionEffect.Native, 32, false) { NativeAction = native };
        NativeBattleContactPacket packet = (NativeBattleContactPacket)typeof(BattleSession).GetMethod("ResolveInstalledNativeHitWithContact", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(battle, [actor, target, presentation, action, native, (byte)255, false])!;
        return packet.Primary.Amount6;
    }

    private static BattleSession Create(int reduction, uint initial = 0, int vitality = 201, int spirit = 151, ushort saved = 0, int magic = 100)
    {
        return new(new BattleDefinition
        {
            Tuning = new() { MeltdownDefenseReductionPercent = reduction },
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true, Magic = magic, Attack = 100 },
                new() { Id = "enemy", Name = "Enemy", MaximumHp = 60_000, CurrentHp = 60_000, Defense = vitality, Spirit = spirit, NativeStatus0 = saved, NativeStatus1 = initial, NativeStatusResistances = Enumerable.Repeat((byte)199, 40).ToArray() }
            ]
        }, BattleAtbMode.TurnBased);
    }

    private static BattleNativeAction Meltdown()
    {
        return new(2, 49, 1, 49)
        {
            MagicId = 49, AttackType = 2, Power = 1, HitCount = 1, TargetFlags = 16, AttackFlags = 1, DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 255, Statuses0 = 0, Statuses1 = 0x1000000, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = [], SupplementalRecord = []
        };
    }
}
