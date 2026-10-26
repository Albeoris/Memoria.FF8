using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleModelColorsTests
{
    private static readonly NativeBattleSummonColors Colors = new(0x00464B6E, 0x32464646, 1f / 4096, 4503601774854144d);
    private static NativeBattleModelColorOverrides Inactive => new([new(255, null), new(255, null)]);
    private static NativeBattleGraphicsModel Model => new(0, 0x403) { Flags2 = 0, Flags8 = 0, Color40 = 0, Color44 = 0x32040404, Kind5 = 0, Phase6 = 16, Transform = new(new(1, 2, 3), new(4, 5, 6), 1234, 27, 4096) };

    [Fact]
    public void Ordinary_callback_restores_actual_stage_RGB_and_clears_only_Vertical34_even_under_pause()
    {
        NativeBattleGraphicsModel model = Model;
        NativeBattleModelColorStep changed = NativeBattleModelColors.Advance50A0A0(model, 1, Colors, Inactive);
        Assert.Equal(Colors.Transition, changed.Model.Color40);
        Assert.Equal((short)0, changed.Model.Transform!.Vertical34);
        Assert.Equal(model.Transform! with { Vertical34 = 0 }, changed.Model.Transform);
        Assert.Equal(model.Color44, changed.Model.Color44);
        Assert.Equal(model.Flags0, changed.Model.Flags0);
        Assert.Equal(model.Flags2, changed.Model.Flags2);
        Assert.Equal(model.Flags8, changed.Model.Flags8);
        Assert.Equal(model.Kind5, changed.Model.Kind5);
        Assert.Equal(model.Phase6, changed.Model.Phase6);
        NativeBattleModelColorStep next = NativeBattleModelColors.Advance50A0A0(changed.Model with { Color40 = 0 }, 0, Colors, changed.Overrides);
        Assert.Equal(Colors.Transition, next.Model.Color40);
    }

    [Fact]
    public void Model_gate_returns_without_reading_status_transform_stage_or_pending_owner()
    {
        NativeBattleGraphicsModel gated = new(0, 3) { Flags2 = 4 };
        NativeBattleModelColorStep result = NativeBattleModelColors.Advance50A0A0(gated, 0, null!, null);
        Assert.Same(gated, result.Model);
        Assert.Null(result.Overrides);
    }

    [Fact]
    public void Stage_writer_runs_before_appearance_service_each_callback_instead_of_after_its_fade()
    {
        NativeBattleGraphicsModel model = Model with { Kind5 = 12, Phase6 = 0 };
        for (int callback = 0; callback < 9; callback++)
        {
            NativeBattleModelColorStep seeded = NativeBattleModelColors.Advance50A0A0(model, 0, Colors, Inactive);
            Assert.Equal(Colors.Transition, seeded.Model.Color40);
            model = NativeBattleSummonModels.Advance(seeded.Model, false, Colors);
            if (model.Kind5 == 12)
            {
                int progress = ((model.Phase6!.Value - 1) << 12) / 15;
                Assert.Equal((Colors.Interpolate(0, progress, Colors.Transition) & 0xFFFFFF) | 0x02000000u, model.Color40);
            }
        }
        Assert.Equal((byte)0, model.Kind5);
        Assert.Equal(Colors.Transition, model.Color40);
        Assert.Equal(Colors.Normal, model.Color44);
    }

    [Fact]
    public void Two_matching_overrides_consume_in_physical_order_and_preserve_RGB_storage()
    {
        NativeBattleModelColorOverrides pending = new([new(0, 0x112233), new(0, 0xAABBCC)]);
        NativeBattleModelColorStep applied = NativeBattleModelColors.Advance50A0A0(Model, 0, Colors, pending);
        Assert.Equal(0xAABBCCu, applied.Model.Color40);
        Assert.Equal(new byte?[] { 255, 255 }, applied.Overrides!.Slots.Select(slot => slot.Actor).ToArray());
        Assert.Equal(new uint?[] { 0x112233, 0xAABBCC }, applied.Overrides.Slots.Select(slot => slot.Color).ToArray());
        NativeBattleModelColorStep next = NativeBattleModelColors.Advance50A0A0(applied.Model, 0, Colors, applied.Overrides);
        Assert.Equal(Colors.Transition, next.Model.Color40);
        Assert.Equal(new byte?[] { 0, 0 }, pending.Slots.Select(slot => slot.Actor).ToArray());
    }

    [Theory]
    [InlineData(255)]
    [InlineData(7)]
    [InlineData(1)]
    public void Inactive_or_other_actor_does_not_read_an_unknown_RGB(byte actor)
    {
        NativeBattleModelColorOverrides pending = new([new(actor, null), new(255, null)]);
        NativeBattleModelColorStep result = NativeBattleModelColors.Advance50A0A0(Model, 0, Colors, pending);
        Assert.Equal(Colors.Transition, result.Model.Color40);
        Assert.Same(pending, result.Overrides);
    }

    [Fact]
    public void Old_missing_owner_or_reached_unknown_actor_RGB_is_strict()
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleModelColors.Advance50A0A0(Model, 0, Colors, null));
        Assert.Throws<NotSupportedException>(() => NativeBattleModelColors.Advance50A0A0(Model, 0, Colors, new([new(null, null), new(255, null)])));
        Assert.Throws<NotSupportedException>(() => NativeBattleModelColors.Advance50A0A0(Model, 0, Colors, new([new(0, null), new(255, null)])));
        NativeBattleEffectMemory old = NativeBattleEffectMemory.FromColdProcess() with { ModelColorOverrides = null };
        Assert.DoesNotContain("ModelColorOverrides", JsonSerializer.Serialize(old));
        Assert.Null(JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(old))!.ModelColorOverrides);
    }

    [Fact]
    public void Cold_slots_are_distinct_and_retained_across_effect_selection_and_JSON()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        Assert.Equal(new byte?[] { 255, 0 }, memory.ModelColorOverrides!.Slots.Select(slot => slot.Actor).ToArray());
        NativeBattleModelColorStep consumed = NativeBattleModelColors.Advance50A0A0(Model, 0, Colors, memory.ModelColorOverrides);
        Assert.Equal(0u, consumed.Model.Color40);
        memory = (memory with { ModelColorOverrides = consumed.Overrides }).BeginLoad(116);
        NativeBattleEffectMemory restored = JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(memory))!;
        Assert.Equal(new byte?[] { 255, 255 }, restored.ModelColorOverrides!.Slots.Select(slot => slot.Actor).ToArray());
        Assert.Equal(Colors.Transition, NativeBattleModelColors.Advance50A0A0(consumed.Model, 0, Colors, restored.ModelColorOverrides).Model.Color40);
    }

    [Fact]
    public void Color_suppression_retains_status_and_color_but_does_not_suppress_override_or_vertical_clear()
    {
        NativeBattleGraphicsModel model = Model with { Flags0 = 0x803, Flags8 = 0x100000, Color40 = 0xA5123456, Alpha7 = 19 };
        NativeBattleModelColorStep changed = NativeBattleModelColors.Advance50A0A0(model, 0, Colors, Inactive);
        Assert.Equal(model.Color40, changed.Model.Color40);
        Assert.Equal(model.StatusTimers, changed.Model.StatusTimers);
        Assert.Equal(model.Flags8, changed.Model.Flags8);
        Assert.Equal(model.Alpha7, changed.Model.Alpha7);
        Assert.Equal((short)0, changed.Model.Transform!.Vertical34);
        NativeBattleModelColorStep overridden = NativeBattleModelColors.Advance50A0A0(model, 0, Colors, new([new(0, 0xFEDCBA), new(255, null)]));
        Assert.Equal(0xFEDCBAu, overridden.Model.Color40);
    }

    [Theory]
    [InlineData(0x80000u, 0u, 0, 0, true)]
    [InlineData(0x2000u, 0u, 0, 0, true)]
    [InlineData(0x82000u, 1u, 0, 0, true)]
    [InlineData(0x80000u, 0u, 8, 0, true)]
    [InlineData(0u, 0u, 0, 0, false)]
    [InlineData(0x80000u, 0x10u, 0, 0, false)]
    [InlineData(0x2000u, 0x400u, 0, 0, false)]
    [InlineData(0x82000u, 0u, 0, 4, false)]
    [InlineData(0x82000u, 0u, 0, 0x4000, false)]
    [InlineData(0x82000u, 0u, 4, 0, false)]
    public void Countdown_service_is_requested_only_at_its_actual_status_and_graphics_gates(uint statuses, uint graphicsFlags, ushort flags2, ushort additionalModelFlags, bool expected)
    {
        NativeBattleGraphicsModel model = Model with { Flags8 = statuses, Flags2 = flags2, Flags0 = (ushort)(Model.Flags0 | additionalModelFlags) };
        NativeBattleModelColorStep step = NativeBattleModelColors.Advance50A0A0(model, graphicsFlags, Colors, Inactive);
        Assert.Equal(expected, step.CountdownRequired);
    }
}
