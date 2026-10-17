using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleModelStatusColorsTests
{
    private static readonly NativeBattleSummonColors Colors = new(0x7F464B6E, 0x32808080, 1f / 4096, 4503601774854144d);

    [Fact]
    public void Unselected_status_does_not_read_model_id_color_or_unknown_timers()
    {
        NativeBattleGraphicsModel model = new(0, 3) { Flags8 = 0x80000000 };
        Assert.Same(model, NativeBattleModelStatusColors.Advance50A1C0(model, Colors));
        Assert.Throws<NotSupportedException>(() => NativeBattleModelStatusColors.Advance50A1C0(model with { Flags8 = null }, Colors));
    }

    [Theory]
    [InlineData(15, 0x7F464B6Eu)]
    [InlineData(16, 0xA5FFFFFFu)]
    [InlineData(255, 0xA5FFFFFFu)]
    public void Petrification_uses_unsigned_model_byte_and_preserves_enemy_command(int modelId, uint expected)
    {
        NativeBattleGraphicsModel model = Model(0xFFFFFFFFu) with { ModelId4 = (byte)modelId, Color40 = 0xA5123456, Alpha7 = 91 };
        NativeBattleGraphicsModel result = NativeBattleModelStatusColors.Advance50A1C0(model, Colors);
        Assert.Equal(expected, result.Color40);
        Assert.Equal((byte)91, result.Alpha7);
        Assert.Equal(model.StatusTimers, result.StatusTimers);
    }

    [Fact]
    public void Invulnerability_precedes_pulsing_statuses_and_clears_alpha()
    {
        NativeBattleGraphicsModel result = NativeBattleModelStatusColors.Advance50A1C0(Model(0x2160500) with { ModelId4 = null, Alpha7 = 255 }, Colors);
        Assert.Equal(0x02464B6Eu, result.Color40);
        Assert.Equal((byte)0, result.Alpha7);
        Assert.All(result.StatusTimers, timer => Assert.Null(timer));
    }

    [Fact]
    public void Player_poison_copies_entire_stage_color_without_reading_timer_or_lower_status()
    {
        NativeBattleGraphicsModel model = Model(0x2060500) with { ModelId4 = 15 };
        NativeBattleGraphicsModel result = NativeBattleModelStatusColors.Advance50A1C0(model, Colors);
        Assert.Equal(Colors.Transition, result.Color40);
        Assert.Equal(model.StatusTimers, result.StatusTimers);
    }

    [Theory]
    [InlineData(0x2060500u, 10, 0x004000u)]
    [InlineData(0x2060100u, 18, 0x2020C0u)]
    [InlineData(0x2060000u, 17, 0x00C0FFu)]
    [InlineData(0x2040000u, 18, 0x202020u)]
    [InlineData(0x2000000u, 25, 0xC060A0u)]
    public void Pulses_select_exact_priority_timer_and_rgb_operands(uint flags, int timerIndex, uint farColor)
    {
        NativeBattleGraphicsModel model = Model(flags) with { Alpha7 = 73 };
        model = model with { StatusTimers = model.StatusTimers.SetItem(timerIndex, 16) };
        NativeBattleGraphicsModel result = NativeBattleModelStatusColors.Advance50A1C0(model, Colors);
        Assert.Equal(farColor, result.Color40);
        Assert.Equal((byte)17, result.StatusTimers[timerIndex]);
        Assert.Equal((byte)73, result.Alpha7);
        for (int index = 0; index < result.StatusTimers.Length; index++)
        {
            if (index != timerIndex)
            {
                Assert.Null(result.StatusTimers[index]);
            }
        }
    }

    [Theory]
    [InlineData(0, 2048, 1)]
    [InlineData(16, 4096, 17)]
    [InlineData(32, 2048, 33)]
    [InlineData(128, 2048, 129)]
    [InlineData(255, 2067, 0)]
    public void Pulse_reads_unsigned_old_byte_then_wraps_increment_and_native_angle(int timer, int factor, int next)
    {
        NativeBattleGraphicsModel model = Model(0x2000000) with { StatusTimers = Model(0).StatusTimers.SetItem(25, (byte)timer) };
        NativeBattleGraphicsModel result = NativeBattleModelStatusColors.Advance50A1C0(model, Colors);
        Assert.Equal(Colors.Interpolate(Colors.Transition, factor, 0xC060A0) & 0xFFFFFFu, result.Color40);
        Assert.Equal((byte)next, result.StatusTimers[25]);
    }

    [Fact]
    public void Selected_unknown_timer_fails_and_json_keeps_next_pulse_exact()
    {
        NativeBattleGraphicsModel model = Model(0x2000000);
        Assert.Throws<NotSupportedException>(() => NativeBattleModelStatusColors.Advance50A1C0(model, Colors));
        model = model with { StatusTimers = model.StatusTimers.SetItem(25, 255) };
        NativeBattleGraphicsModel first = NativeBattleModelStatusColors.Advance50A1C0(model, Colors);
        NativeBattleGraphicsModel restored = JsonSerializer.Deserialize<NativeBattleGraphicsModel>(JsonSerializer.Serialize(first))!;
        Assert.Equal(JsonSerializer.Serialize(NativeBattleModelStatusColors.Advance50A1C0(first, Colors)), JsonSerializer.Serialize(NativeBattleModelStatusColors.Advance50A1C0(restored, Colors)));
    }

    [Fact]
    public void Only_reached_original_model_and_command_owners_are_required()
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleModelStatusColors.Advance50A1C0(Model(0x10) with { ModelId4 = null }, Colors));
        Assert.Throws<NotSupportedException>(() => NativeBattleModelStatusColors.Advance50A1C0(Model(0x10) with { Color40 = null }, Colors));
        NativeBattleGraphicsModel player = Model(0x10) with { ModelId4 = 0, Color40 = null };
        Assert.Equal(Colors.Transition, NativeBattleModelStatusColors.Advance50A1C0(player, Colors).Color40);
        Assert.Throws<NotSupportedException>(() => NativeBattleModelStatusColors.Advance50A1C0(Model(0x400) with { ModelId4 = null }, Colors));
    }

    private static NativeBattleGraphicsModel Model(uint flags) => new(0, 3) { Flags8 = flags, ModelId4 = 16, Color40 = 0xA5123456 };
}
