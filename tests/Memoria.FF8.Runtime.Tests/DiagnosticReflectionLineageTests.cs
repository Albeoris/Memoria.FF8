using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class DiagnosticReflectionLineageTests
{
    private static BattleDiagnosticEffectDispatch Source => new(1, 10, "enemy", BattleAbility.EnemyAbility, 31, 106, 106, 2) { NativeActorIndex = 3, NativeTargetMask = 8, NativeHeaderIndex = 0, NativeHeaderGeneration = 1 };
    private static BattleDiagnosticEffectDispatch Child => new(2, 11, "enemy", BattleAbility.EnemyAbility, 31, 106, 106, 247) { NativeActorIndex = 3, NativeTargetMask = 1, NativeHeaderIndex = 1, NativeHeaderGeneration = 2, Reflection = new(0x487DF0, 1, 0, 1, 1, 2, "enemy", 3, "enemy", 3, 0x80, "party", 0, 2, 31, 106, 8, 0x10, 31, 106, 0, 1) };

    [Fact]
    public void Reflection_retains_the_caster_and_changes_only_the_proved_recipient()
    {
        Assert.True(BattleDiagnosticReflectionLineage.Matches(Child, Source));
        BattleDiagnosticEffectDispatch restored = JsonSerializer.Deserialize<BattleDiagnosticEffectDispatch>(JsonSerializer.Serialize(Child))!;
        Assert.Equal(Child, restored);
        Assert.True(BattleDiagnosticReflectionLineage.Matches(restored, Source));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    public void Unlinked_tainted_and_reused_header_receipts_are_rejected(int mutation)
    {
        BattleDiagnosticEffectDispatch child = mutation switch
        {
            0 => Child with { Reflection = null },
            1 => Child with { Reflection = Child.Reflection! with { SourceDispatchSerial = 9 } },
            2 => Child with { Reflection = Child.Reflection! with { SourceHeaderGeneration = 2 } },
            3 => Child with { NativeHeaderGeneration = 3 },
            4 => Child with { NativeTargetMask = 8 },
            5 => Child with { ActorId = "party" },
            6 => Child with { RequestedEffect = 107 },
            7 => Child with { Reflection = Child.Reflection! with { ReflectorStatuses1 = 0 } },
            8 => Child with { Reflection = Child.Reflection! with { SourceAttackFlags = 0 } },
            _ => Child with { Ability = BattleAbility.Magic }
        };
        Assert.False(BattleDiagnosticReflectionLineage.Matches(child, Source));
    }

    [Fact]
    public void A_player_Draw_source_produces_a_Magic_child_with_native_target_modifiers()
    {
        BattleDiagnosticEffectDispatch source = Source with { ActorId = "party", Ability = BattleAbility.Draw, NativeActorIndex = 0, NativeTargetMask = 8 };
        BattleDiagnosticEffectDispatch child = Child with { ActorId = "party", Ability = BattleAbility.Magic, NativeActorIndex = 0, NativeTargetMask = 1, Reflection = Child.Reflection! with { ActorId = "party", ActorIndex = 0, ReflectedTargetFlags = 3, ReflectedTargetMask = 0x6001 } };
        Assert.True(BattleDiagnosticReflectionLineage.Matches(child, source));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Contact_proof_requires_the_current_child_header_generation(bool stale)
    {
        BattleDiagnosticReflection proof = Child.Reflection! with { TargetProvenance = new(1, stale ? 3 : 2, 11, [0]) };
        BattleDiagnosticEffectDispatch current = Child with { Reflection = proof };
        Assert.Equal(!stale, BattleDiagnosticReflectionLineage.Matches(current, Source));
    }
}
