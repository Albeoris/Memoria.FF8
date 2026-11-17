using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class StatusStatRuleTests
{
    private const uint Vit0Mask = NativeBattleParameterRules.DefenseReductionStatus;

    [Theory]
    [InlineData(80, 60, 50, 25, 40, 45)]
    [InlineData(201, 151, 50, 25, 100, 113)]
    [InlineData(80, 60, 0, 0, 80, 60)]
    [InlineData(80, 60, 100, 100, 0, 0)]
    public void InitialStatusAndReapplicationUseOneCapturedRuleSet(int vitality, int spirit, int vitalityReduction, int spiritReduction, int effectiveVitality, int effectiveSpirit)
    {
        BattleSession battle = Create(Vit0(vitalityReduction, spiritReduction), Vit0Mask, vitality, spirit);
        AssertStats(battle, effectiveVitality, effectiveSpirit);
        Apply(battle, StatusAction());
        AssertStats(battle, effectiveVitality, effectiveSpirit);

        string json = JsonSerializer.Serialize(battle.Capture());
        Assert.DoesNotContain("ReductionPercent", json);
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(json)!);
        AssertStats(restored, effectiveVitality, effectiveSpirit);
        Assert.Equal(vitality, restored.Combatants[1].Defense);
        Assert.Equal(spirit, restored.Combatants[1].Spirit);

        Apply(restored, StatusAction(), cure: true);
        AssertStats(restored, vitality, spirit);
        Apply(restored, StatusAction());
        AssertStats(restored, effectiveVitality, effectiveSpirit);
    }

    [Theory]
    [InlineData(2, 49, 1, 49)]
    [InlineData(2, 1, 1, 1)]
    [InlineData(247, 49, 1, 49)]
    [InlineData(4, 17, 7, 17)]
    [InlineData(249, 2, -1, -1)]
    public void StatusRuleIsIndependentOfActionSource(int command, int actionId, int section, int index)
    {
        BattleSession battle = Create(Vit0(50, 25));
        BattleNativeAction action = StatusAction() with { Command = (byte)command, ActionId = (ushort)actionId, KernelSection = section, KernelIndex = index };
        Apply(battle, action);
        AssertStats(battle, 40, 45);
    }

    [Fact]
    public void NativeContributionIsInheritedOnlyForOmittedPairs()
    {
        BattleSession battle = Create([new(NativeStatusId.Vit0, BattleStatId.Vitality, StatusStatOperation.ReducePercent, 50)], Vit0Mask);
        AssertStats(battle, 40, 0);
        AssertStats(Create([], Vit0Mask), 0, 0);
    }

    [Fact]
    public void SavedStatusCanContributeToStrengthAndNativePhysicalDamage()
    {
        BattleSession battle = Create([new(NativeStatusId.Poison, BattleStatId.Strength, StatusStatOperation.ReducePercent, 60)], actorStatus: 2);
        BattleSession expected = Create([], actorStrength: 40);
        Assert.Equal(100, battle.Combatants[0].Attack);
        Assert.Equal(40, battle.ReadEffectiveStat("party", BattleStatId.Strength));
        BattleNativeAction physical = StatusAction() with { Command = 249, AttackType = 1, Power = 32, Hit = 255, Critical = 0, Statuses1 = 0 };
        Assert.Equal(ResolveDamage(expected, physical), ResolveDamage(battle, physical));
    }

    [Fact]
    public void EffectiveStatQueriesUseTheSameProjection()
    {
        BattleSession battle = Create(Vit0(50, 25), Vit0Mask);
        MethodInfo selector = typeof(BattleSession).GetMethod("NativeAiTargetStat", BindingFlags.Instance | BindingFlags.NonPublic)!;
        BattleCombatant target = battle.Combatants[1];
        Assert.Equal(battle.ReadEffectiveStat("enemy", BattleStatId.Vitality), (int)selector.Invoke(battle, [target, 1])!);
        Assert.Equal(battle.ReadEffectiveStat("enemy", BattleStatId.Spirit), (int)selector.Invoke(battle, [target, 3])!);
        MethodInfo scan = typeof(BattleSession).GetMethod("CaptureNativeScanDescriptor208", BindingFlags.Instance | BindingFlags.NonPublic)!;
        NativeBattleScanByteOwner descriptor = (NativeBattleScanByteOwner)scan.Invoke(battle, [target])!;
        Assert.Equal(40, descriptor.ReadByte(0xBE));
        Assert.Equal(45, descriptor.ReadByte(0xC0));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(36)]
    [InlineData(2)]
    [InlineData(15)]
    [InlineData(22)]
    [InlineData(5)]
    [InlineData(6)]
    public void DamageConsumersUseProjectedDefenseAndRetainNativeBypasses(int attackType)
    {
        ushort saved = attackType is 5 or 6 ? (ushort)0x40 : (ushort)0;
        BattleSession battle = Create(Vit0(50, 25), Vit0Mask, vitality: 201, spirit: 151, saved: saved);
        BattleSession expected = Create([], vitality: 100, spirit: 113, saved: saved);
        BattleNativeAction attack = StatusAction() with { Command = 249, ActionId = 0, KernelSection = -1, KernelIndex = -1, AttackType = (byte)attackType, Power = 32, Hit = 255, Critical = 0, Statuses1 = 0, StatusAttack = 0, DamagePresentation = 4 };
        Assert.Equal(ResolveDamage(expected, attack), ResolveDamage(battle, attack));
        Assert.Equal(expected.Combatants[1].CurrentHp, battle.Combatants[1].CurrentHp);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(50, 16)]
    [InlineData(100, 16)]
    public void SubsequentStatusApplicationUsesProjectedSpirit(int spiritReduction, int expectedSaved)
    {
        BattleSession battle = Create(Vit0(0, spiritReduction), Vit0Mask, spirit: 255, magic: 0);
        Apply(battle, StatusAction() with { Statuses0 = 16, Statuses1 = 0, StatusAttack = 250 });
        Assert.Equal((ushort)expectedSaved, battle.Capture().Combatants[1].NativeStatus!.Saved);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void SnapshotRulesRejectOutOfRangeReduction(int reduction)
    {
        Assert.Throws<InvalidDataException>(() => Create(Vit0(reduction, 0)));
    }

    [Fact]
    public void SnapshotRulesRejectDuplicateAndUnsupportedContributions()
    {
        StatusStatModifier modifier = new(NativeStatusId.Vit0, BattleStatId.Vitality, StatusStatOperation.ReducePercent, 50);
        Assert.Throws<InvalidDataException>(() => Create([modifier, modifier]));
        Assert.Throws<InvalidDataException>(() => Create([modifier with { Operation = (StatusStatOperation)1 }]));
        Assert.Throws<InvalidDataException>(() => Create([modifier with { Stat = (BattleStatId)77 }]));
        Assert.Throws<InvalidDataException>(() => Create([modifier with { Status = (NativeStatusId)77 }]));
    }

    [Fact]
    public void OldNativeSnapshotInheritsBaselineAndCurrentRuleSnapshotRestoresIndependently()
    {
        JsonObject old = JsonSerializer.SerializeToNode(Create([], Vit0Mask).Capture())!.AsObject();
        JsonObject definition = old["Definition"]!.AsObject();
        definition.Remove("StatusStatModifiers");
        definition["Tuning"]!["MeltdownVitalityReductionPercent"] = 100;
        old["Combatants"]![1]!["NativeStatus"]!["MeltdownSpiritReductionPercent"] = 100;
        AssertStats(BattleSession.Restore(old.Deserialize<BattleSnapshot>()!), 0, 0);

        BattleSnapshot original = Create(Vit0(50, 25), Vit0Mask).Capture();
        BattleSession next = new(original.Definition with { StatusStatModifiers = Vit0(25, 50) }, BattleAtbMode.TurnBased);
        AssertStats(next, 60, 30);
        AssertStats(BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(original))!), 40, 45);
    }

    [Fact]
    public void SourceSpecificLegacySnapshotRequiresExplicitMigration()
    {
        JsonObject old = JsonSerializer.SerializeToNode(Create([], Vit0Mask).Capture())!.AsObject();
        JsonObject definition = old["Definition"]!.AsObject();
        definition.Remove("StatusStatModifiers");
        definition["Tuning"]!["MeltdownDefenseReductionPercent"] = 50;
        BattleSnapshot snapshot = old.Deserialize<BattleSnapshot>()!;
        Assert.Contains("Migrate", Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot)).Message);

        definition["Tuning"]!.AsObject().Remove("MeltdownDefenseReductionPercent");
        old["Combatants"]![1]!["NativeStatus"]!["MeltdownVitalityReductionPercent"] = 50;
        Assert.Contains("Migrate", Assert.Throws<InvalidDataException>(() => old.Deserialize<BattleSnapshot>()).Message);
    }

    internal static ImmutableArray<StatusStatModifier> Vit0(int vitality, int spirit)
    {
        return
        [
            new(NativeStatusId.Vit0, BattleStatId.Vitality, StatusStatOperation.ReducePercent, vitality),
            new(NativeStatusId.Vit0, BattleStatId.Spirit, StatusStatOperation.ReducePercent, spirit)
        ];
    }

    private static void AssertStats(BattleSession battle, int vitality, int spirit)
    {
        Assert.Equal(vitality, battle.ReadEffectiveStat("enemy", BattleStatId.Vitality));
        Assert.Equal(spirit, battle.ReadEffectiveStat("enemy", BattleStatId.Spirit));
    }

    private static void Apply(BattleSession battle, BattleNativeAction action, bool cure = false)
    {
        typeof(BattleSession).GetMethod("ApplyNativeActionStatuses", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(battle, [battle.Combatants[0], battle.Combatants[1], action, cure]);
    }

    private static int ResolveDamage(BattleSession battle, BattleNativeAction native)
    {
        BattleCombatant actor = battle.Combatants[0];
        BattleCombatant target = battle.Combatants[1];
        BattleActionPresentation presentation = new(actor.Id, target.Id, BattleActionPhase.Windup, new BattleTargetRequest(new BattleTargetSelection.Explicit([new BattleEnemyActorReference(target.Id)]), BattleTargetEligibility.SavedStatus, false, BattleTargetReplacement.None), BattleAbility.Attack, 0) { NativeAction = native, NativeQueuedCommand = native.Command };
        BattleActionDefinition action = new(BattleAbility.Attack, 0, "Contact", BattleActionEffect.Native, 32, false) { NativeAction = native };
        NativeBattleContactPacket packet = (NativeBattleContactPacket)typeof(BattleSession).GetMethod("ResolveInstalledNativeHitWithContact", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(battle, [actor, target, presentation, action, native, null, false])!;
        return packet.Primary.Amount6;
    }

    private static BattleSession Create(ImmutableArray<StatusStatModifier> rules, uint initial = 0, int vitality = 80, int spirit = 60, ushort saved = 0, int magic = 100, ushort actorStatus = 0, int actorStrength = 100)
    {
        return new(new BattleDefinition
        {
            StatusStatModifiers = rules,
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true, Magic = magic, Attack = actorStrength, NativeStatus0 = actorStatus },
                new() { Id = "enemy", Name = "Enemy", MaximumHp = 60_000, CurrentHp = 60_000, Defense = vitality, Spirit = spirit, NativeStatus0 = saved, NativeStatus1 = initial, NativeStatusResistances = Enumerable.Repeat((byte)199, 40).ToArray() }
            ]
        }, BattleAtbMode.TurnBased);
    }

    private static BattleNativeAction StatusAction()
    {
        return new(2, 49, 1, 49)
        {
            MagicId = 49, AttackType = 2, Power = 1, HitCount = 1, TargetFlags = 16, AttackFlags = 1, DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 255, Statuses0 = 0, Statuses1 = Vit0Mask, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = [], SupplementalRecord = []
        };
    }
}
