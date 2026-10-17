using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleModelStatusMotionTests
{
    private static NativeBattleGraphicsModel Model => new(0, 3)
    {
        ModelId4 = 0,
        Flags2 = 0,
        Flags8 = 0,
        Sequence = 1,
        SequenceState = new() { Default2 = 1 },
        GeometryWorldMatrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 100, 200, 300, 4096),
        Transform = new(new(1, 2, 3), new(4, 5, 6), 1234, 77, 4096),
        Rotation12 = 13,
        Rotation14 = 27,
        Rotation16 = 39
    };

    [Theory]
    [InlineData(15, 1, 1, true)]
    [InlineData(15, 8, 1, false)]
    [InlineData(15, 16, 1, true)]
    [InlineData(15, 16, 16, false)]
    [InlineData(15, 19, 19, false)]
    [InlineData(15, 1, 2, false)]
    [InlineData(16, 1, 1, true)]
    [InlineData(16, 8, 1, true)]
    [InlineData(16, 16, 1, false)]
    [InlineData(255, 8, 1, true)]
    public void Rotation_uses_unsigned_model_threshold_and_original_sequence_XOR(byte id, byte sequence, byte standing, bool admitted)
    {
        NativeBattleGraphicsModel source = Model with { ModelId4 = id, Sequence = sequence, SequenceState = new() { Default2 = standing }, Flags8 = 4 };
        NativeBattleGraphicsModel changed = NativeBattleModelStatusMotion.Advance50A370(source);
        if (admitted)
        {
            Assert.Equal(new short[] { -4096, 0, 0, 0, 4096, 0, 0, 0, -4096 }, changed.GeometryWorldMatrix!.Rotation.ToArray());
        }
        else
        {
            Assert.Same(source, changed);
        }
        Assert.Equal(source.StatusTimers, changed.StatusTimers);
    }

    [Fact]
    public void Gated_models_and_player_sequence19_do_not_read_unused_owners()
    {
        NativeBattleGraphicsModel gated = new(0, 3) { Flags2 = 8 };
        Assert.Same(gated, NativeBattleModelStatusMotion.Advance50A370(gated));
        Assert.Same(gated, NativeBattleModelStatusMotion.Advance50A5F0(gated));
        NativeBattleGraphicsModel sequence19 = new(0, 3) { Flags2 = 0, ModelId4 = 15, Sequence = 19 };
        Assert.Same(sequence19, NativeBattleModelStatusMotion.Advance50A370(sequence19));
        NativeBattleGraphicsModel wrongSequence = Model with { Sequence = 2, Flags8 = null, GeometryWorldMatrix = null, Transform = null };
        Assert.Same(wrongSequence, NativeBattleModelStatusMotion.Advance50A370(wrongSequence));
        Assert.Same(wrongSequence, NativeBattleModelStatusMotion.Advance50A5F0(wrongSequence));
    }

    [Fact]
    public void Rotation_left_multiplies_the_retained_matrix_and_preserves_translation_scale_and_base_angles()
    {
        NativeBattleActionMatrix previous = new([1000, 101, 200, 23, 3000, 88, 22, -44, 1024], 111, -222, 333, 2048);
        NativeBattleGraphicsModel source = Model with { Flags8 = 0x800, GeometryWorldMatrix = previous, StatusTimers = Model.StatusTimers.SetItem(11, 4) };
        NativeBattleGraphicsModel changed = NativeBattleModelStatusMotion.Advance50A370(source);
        Assert.Equal(new short[] { 22, -44, 1024, 23, 3000, 88, -1000, -101, -200 }, changed.GeometryWorldMatrix!.Rotation.ToArray());
        Assert.Equal((previous.X, previous.Y, previous.Z, previous.Scale), (changed.GeometryWorldMatrix.X, changed.GeometryWorldMatrix.Y, changed.GeometryWorldMatrix.Z, changed.GeometryWorldMatrix.Scale));
        Assert.Equal((source.Rotation12, source.Rotation14, source.Rotation16, source.Transform), (changed.Rotation12, changed.Rotation14, changed.Rotation16, changed.Transform));
        Assert.Equal((byte?)5, changed.StatusTimers[11]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1024)]
    [InlineData(32768)]
    [InlineData(65280)]
    [InlineData(65536)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void Full_DWORD_rotation_angles_reach_the_shared_trigonometry_without_WORD_truncation(int angle)
    {
        NativeBattleActionMatrix changed = NativeBattleGfMatrices.RotateY56D020(Model.GeometryWorldMatrix!, angle);
        short sine = unchecked((short)NativeBattleCameraMath.Sine(angle));
        short cosine = unchecked((short)NativeBattleCameraMath.Cosine(angle));
        Assert.Equal(new short[] { cosine, 0, sine, 0, 4096, 0, unchecked((short)-sine), 0, cosine }, changed.Rotation.ToArray());
        if (angle == int.MaxValue)
        {
            Assert.NotEqual(NativeBattleCameraMath.Sine(unchecked((short)angle)), changed.Rotation[2]);
        }
    }

    [Fact]
    public void Rotating_status_has_priority_over_constant_rotation_and_wraps_only_selected_BYTE11()
    {
        NativeBattleGraphicsModel source = Model with { Flags8 = 0x804, StatusTimers = Model.StatusTimers.SetItem(11, 255).SetItem(9, 17) };
        NativeBattleGraphicsModel changed = NativeBattleModelStatusMotion.Advance50A370(source);
        Assert.Equal((byte?)0, changed.StatusTimers[11]);
        Assert.Equal((byte?)17, changed.StatusTimers[9]);
        Assert.Equal(unchecked((short)NativeBattleCameraMath.Sine(65280)), changed.GeometryWorldMatrix!.Rotation[2]);
        NativeBattleGraphicsModel constant = NativeBattleModelStatusMotion.Advance50A370(Model with { Flags8 = 4 });
        Assert.Equal((short)0, constant.GeometryWorldMatrix!.Rotation[2]);
        Assert.Equal((short)-4096, constant.GeometryWorldMatrix.Rotation[0]);
    }

    [Theory]
    [InlineData(0, -250)]
    [InlineData(1, -270)]
    [InlineData(4, -300)]
    [InlineData(12, -200)]
    [InlineData(255, -231)]
    public void Float_reads_unsigned_BYTE9_then_uses_signed_negative_SAR_and_wraps_the_counter(byte timer, short vertical)
    {
        NativeBattleGraphicsModel source = Model with { ModelId4 = null, Flags8 = 0x200, StatusTimers = Model.StatusTimers.SetItem(9, timer) };
        NativeBattleGraphicsModel changed = NativeBattleModelStatusMotion.Advance50A5F0(source);
        Assert.Equal(vertical, changed.Transform!.Vertical34);
        Assert.Equal(unchecked((byte)(timer + 1)), changed.StatusTimers[9]);
        Assert.Same(source.GeometryWorldMatrix, changed.GeometryWorldMatrix);
        Assert.Equal(source.Transform! with { Vertical34 = vertical }, changed.Transform);
        Assert.Null(changed.StatusTimers[11]);
    }

    [Theory]
    [InlineData(1, 1, true)]
    [InlineData(8, 1, true)]
    [InlineData(16, 16, true)]
    [InlineData(16, 1, false)]
    [InlineData(19, 19, true)]
    [InlineData(19, 1, false)]
    public void Float_has_its_own_sequence_gate_without_the_player_rotation_exceptions(byte sequence, byte standing, bool admitted)
    {
        NativeBattleGraphicsModel source = Model with { ModelId4 = null, Sequence = sequence, SequenceState = new() { Default2 = standing }, Flags8 = 0x200, StatusTimers = Model.StatusTimers.SetItem(9, 0) };
        NativeBattleGraphicsModel changed = NativeBattleModelStatusMotion.Advance50A5F0(source);
        if (admitted)
        {
            Assert.Equal((short)-250, changed.Transform!.Vertical34);
        }
        else
        {
            Assert.Same(source, changed);
        }
    }

    [Fact]
    public void Same_50A0A0_callback_applies_both_motions_even_under_graphics_pause_and_preserves_prior_matrix_translation()
    {
        NativeBattleSummonColors colors = new(0x123456, 0x32464646, 1f / 4096, 4503601774854144d);
        NativeBattleModelColorOverrides inactive = new([new(255, null), new(255, null)]);
        NativeBattleGraphicsModel source = Model with { Flags8 = 0xA04, StatusTimers = Model.StatusTimers.SetItem(11, 4).SetItem(9, 1) };
        NativeBattleModelColorStep ordinary = NativeBattleModelColors.Advance50A0A0(source, 0, colors, inactive);
        NativeBattleModelColorStep paused = NativeBattleModelColors.Advance50A0A0(source, 1, colors, inactive);
        Assert.Equal(JsonSerializer.Serialize(ordinary), JsonSerializer.Serialize(paused));
        Assert.Equal((short)-270, ordinary.Model.Transform!.Vertical34);
        Assert.Equal((short)4096, ordinary.Model.GeometryWorldMatrix!.Rotation[2]);
        Assert.Equal(200, ordinary.Model.GeometryWorldMatrix.Y);
        Assert.Equal((byte?)5, ordinary.Model.StatusTimers[11]);
        Assert.Equal((byte?)2, ordinary.Model.StatusTimers[9]);
        NativeBattleGraphicsModel suppressed = NativeBattleModelColors.Advance50A0A0(source with { Flags0 = 0x3003 }, 0, colors, inactive).Model;
        Assert.Equal((short)0, suppressed.Transform!.Vertical34);
        Assert.Same(source.GeometryWorldMatrix, suppressed.GeometryWorldMatrix);
        Assert.Equal(source.StatusTimers, suppressed.StatusTimers);
    }

    [Fact]
    public void Selected_missing_timer_or_matrix_is_strict_but_unselected_status_reads_nothing()
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleModelStatusMotion.Advance50A370(Model with { ModelId4 = null, Flags8 = 4 }));
        Assert.Throws<NotSupportedException>(() => NativeBattleModelStatusMotion.Advance50A370(Model with { Flags8 = 0x800 }));
        Assert.Throws<NotSupportedException>(() => NativeBattleModelStatusMotion.Advance50A370(Model with { Flags8 = 4, GeometryWorldMatrix = null }));
        Assert.Throws<NotSupportedException>(() => NativeBattleModelStatusMotion.Advance50A5F0(Model with { Flags8 = 0x200 }));
        Assert.Throws<NotSupportedException>(() => NativeBattleModelStatusMotion.Advance50A5F0(Model with { Flags8 = 0x200, StatusTimers = Model.StatusTimers.SetItem(9, 0), Transform = null }));
        NativeBattleGraphicsModel unselected = Model with { GeometryWorldMatrix = null, Transform = null };
        Assert.Same(unselected, NativeBattleModelStatusMotion.Advance50A370(unselected));
        Assert.Same(unselected, NativeBattleModelStatusMotion.Advance50A5F0(unselected));
    }

    [Fact]
    public void Motion_timers_and_retained_matrix_roundtrip_without_reinitialization()
    {
        NativeBattleGraphicsModel source = Model with { Flags8 = 0xA00, StatusTimers = Model.StatusTimers.SetItem(11, 255).SetItem(9, 255) };
        NativeBattleGraphicsModel first = NativeBattleModelStatusMotion.Advance50A5F0(NativeBattleModelStatusMotion.Advance50A370(source));
        NativeBattleGraphicsModel restored = JsonSerializer.Deserialize<NativeBattleGraphicsModel>(JsonSerializer.Serialize(first))!;
        NativeBattleGraphicsModel next = NativeBattleModelStatusMotion.Advance50A5F0(NativeBattleModelStatusMotion.Advance50A370(first));
        NativeBattleGraphicsModel resumed = NativeBattleModelStatusMotion.Advance50A5F0(NativeBattleModelStatusMotion.Advance50A370(restored));
        Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(resumed));
        Assert.Equal((byte?)1, resumed.StatusTimers[9]);
        Assert.Equal((byte?)1, resumed.StatusTimers[11]);
        Assert.Equal((short)-250, resumed.Transform!.Vertical34);
    }
}
