using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Battle.Tests;

public sealed class NativeBattleLimitTests
{
    [Fact]
    public void Crisis_uses_native_integer_order_rng_and_status_and_dead_party_modifiers()
    {
        byte[] weights = new byte[32];
        weights[16] = 200;
        BattleNativeLimitDefinition definition = new(0, 5, 0, 250, weights.ToImmutableArray());

        Assert.Equal(0, NativeBattleLimitRules.CrisisLevel(definition, 100, 100, 0, 0, 0, 0));
        Assert.Equal(4, NativeBattleLimitRules.CrisisLevel(definition, 10, 100, 0, 0, 0, 0));
        Assert.Equal(0, NativeBattleLimitRules.CrisisLevel(definition, 10, 100, 0, 0, 0, 255));
        Assert.Equal(2, NativeBattleLimitRules.CrisisLevel(definition, 100, 100, 0, 0x100, 0, 0));
        Assert.Equal(1, NativeBattleLimitRules.CrisisLevel(definition, 10, 100, 0, 0, 2, 160));
        Assert.Equal(0, NativeBattleLimitRules.CrisisLevel(definition, 1, 100, 0, 0x200, 2, 0));
        Assert.Equal(0, NativeBattleLimitRules.CrisisLevel(definition, 1, 100, 0, 0x100, 2, 0, true));
        Assert.Equal(3, NativeBattleLimitRules.CrisisLevel(definition, 100, 100, 0, 0x200, 0, 0, true, 3));
    }

    [Theory]
    [InlineData(0x48, 3, 0x8008)]
    [InlineData(0x08, 0, 0x8001)]
    [InlineData(0x42, 3, 0xA008)]
    [InlineData(0x54, 3, 0x0008)]
    [InlineData(0x15, 1, 0x4002)]
    [InlineData(0x20, 3, 0x80FF)]
    public void Player_masks_keep_native_group_random_and_KO_modifiers(byte flags, int nativeSlot, ushort expected)
    {
        Assert.Equal(expected, NativeBattleLimitRules.PlayerTargetMask(flags, nativeSlot));
    }

    [Fact]
    public void Field_battle_rule_byte_is_gated_by_the_actual_field_block_flag()
    {
        NativeEventMemory memory = NativeEventMemory.Empty.Write(243, 1, NativeScalarValue.Exact(0x20));
        Assert.Equal(0, NativeBattleLimitRules.ReadFieldBattleRules(memory));
        memory = memory.Write(104, 4, NativeScalarValue.Exact(0x800));
        Assert.Equal(0x20, NativeBattleLimitRules.ReadFieldBattleRules(memory));
    }

    [Fact]
    public void Limit_selection_owns_one_rng_draw_and_survives_snapshot_restore_without_rerolling()
    {
        BattleNativeLimitDefinition limit = new(3, 15, 0, 250, Enumerable.Repeat((byte)0, 32).ToImmutableArray());
        BattleSession battle = new(new BattleDefinition { NativeLimitsDisabled = false, Combatants = [new() { Id = "quistis", Name = "Quistis", IsPlayer = true, ModelId = 3, MaximumHp = 100, CurrentHp = 10, InitialAtb = BattleSession.AtbMaximum, NativeLimit = limit }, new() { Id = "enemy", Name = "Enemy" }] }, BattleAtbMode.TurnBased);
        NativeBattleRandomState expectedRandom = battle.Capture().Random!.Draw(out byte roll);

        byte crisis = battle.RefreshNativeLimit("quistis");
        Assert.Equal(NativeBattleLimitRules.CrisisLevel(limit, 10, 100, 0, 0, 0, roll), crisis);
        Assert.Equal(JsonSerializer.Serialize(expectedRandom), JsonSerializer.Serialize(battle.Capture().Random));
        string snapshot = JsonSerializer.Serialize(battle.Capture());
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(snapshot)!);

        Assert.Equal(crisis, restored.GetNativeLimitLevel("quistis"));
        Assert.Equal(snapshot, JsonSerializer.Serialize(restored.Capture()));
    }
}
