using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleStageVariant137Tests
{
    [Fact]
    public void Initialization_preserves_unwritten_rotation_words_and_counters_while_missing_imports_stay_unknown()
    {
        NativeBattleStage137State retained = new() { Deforming = false, ShakeAmplitude = 99, DeformationTick = 71, WaveCounter = 87, Variants = new(6, 200, [-7, null, 9, 11]) };
        NativeBattleStage137State initialized = NativeBattleStage137.Initialize50DF10(retained, 0x193A108);
        Assert.True(initialized.Deforming);
        Assert.Equal(0, initialized.ShakeAmplitude);
        Assert.Equal(71, initialized.DeformationTick);
        Assert.Equal(87, initialized.WaveCounter);
        Assert.Equal(0, initialized.Variants!.Index);
        Assert.Equal(300, initialized.Variants.Counter);
        Assert.Equal(0x193A108u, initialized.Variants.GeometryAddress);
        Assert.Equal(retained.Variants!.RotationWords, initialized.Variants.RotationWords);
        Assert.Equal((short)-7, initialized.Variants.ReadRotationWord(0x1D99BE8));
        Assert.Throws<NotSupportedException>(() => initialized.Variants.ReadRotationWord(0x1D99BEA));
        NativeBattleStage137State imported = JsonSerializer.Deserialize<NativeBattleStage137State>(JsonSerializer.Serialize(new NativeBattleStage137State()))!;
        Assert.Null(imported.Variants);
        Assert.Throws<NotSupportedException>(() => NativeBattleStage137.Initialize50DF10(imported, 0x193A108).Variants!.ReadRotationWord(0x1D99BE8));
        Assert.Equal((short)0, NativeBattleStageExtendedState.FromColdProcess().Stage137.Variants!.ReadRotationWord(0x1D99BE8));
    }

    [Theory]
    [InlineData(-9, -17)]
    [InlineData(-8, -16)]
    [InlineData(-1, -1)]
    [InlineData(0, 0)]
    [InlineData(7, 7)]
    [InlineData(8, 16)]
    [InlineData(9, 17)]
    public void Camera_selector_uses_signed_C_quotient_and_remainder(int index, int expected)
    {
        Assert.Equal(expected, new NativeBattleStageVariant137State(index, null, [null, null, null, null]).SelectCamera50E4E0());
        Assert.Throws<NotSupportedException>(() => NativeBattleStageVariant137State.Unknown().SelectCamera50E4E0());
    }

    [Fact]
    public void Ten_variant_changes_force_a_fresh_wave_only_on_wrap_and_preserve_retained_fields_after_json()
    {
        NativeBattleStage137State state = NativeBattleStage137.Initialize50DF10(NativeBattleStageExtendedState.FromColdProcess().Stage137, 0x193A108) with { DeformationTick = 123, WaveCounter = 55, CameraCounter = 19 };
        Queue<uint> random = new([2, 7, 9]);
        int signs = 0;
        for (int index = 0; index < 10; index++)
        {
            state = JsonSerializer.Deserialize<NativeBattleStage137State>(JsonSerializer.Serialize(state))!;
            (NativeBattleStage137State next, short speed) = NativeBattleStage137.CompleteVariant50E3C0(state, 17, random.Dequeue, () =>
            {
                signs++;
                return 0;
            });
            state = next;
            Assert.Equal((index + 1) % 10, state.Variants!.Index);
            Assert.Equal(200, state.Variants.Counter);
            Assert.Equal(0x193A108u, state.Variants.GeometryAddress);
            Assert.Equal(index == 9 ? (short)0 : (short)17, speed);
            Assert.Equal(123, state.DeformationTick);
            Assert.Equal(19, state.CameraCounter);
            if (index < 9)
            {
                Assert.True(state.Deforming);
                Assert.Equal(55, state.WaveCounter);
                Assert.Equal(3, random.Count);
                Assert.Equal(0, signs);
            }
        }
        Assert.False(state.Deforming);
        Assert.Empty(random);
        Assert.Equal(1, signs);
        Assert.Equal(32, state.WaveDelay);
        Assert.Equal(97, state.WaveCounter);
        Assert.Equal(37, state.WaveRise);
        Assert.Equal(-265, state.WaveAmplitude);
    }

    [Fact]
    public void Rebuild_replaces_current_groups_geometry_and_texture_section_without_resetting_flags_callback_or_source_address()
    {
        byte[] skeleton = new byte[64];
        skeleton[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        byte[] animations = new byte[17];
        BinaryPrimitives.WriteInt32LittleEndian(animations, 1);
        BinaryPrimitives.WriteInt32LittleEndian(animations.AsSpan(4), 8);
        animations[8] = 3;
        NativeBattlePoseSource pose = new(skeleton.ToImmutableArray(), animations.ToImmutableArray());
        NativeBattleStageSource source = new(99, [pose, pose, pose, pose], [3, 4], new("stage", [])) { Geometry = [], ModelColors = new(1, 2) };
        NativeBattleStageState before = NativeBattleStageState.FromColdProcess().Initialize500EA0(source) with { Flags = 0xCAFE, Rotation = 57, SourceAddress1D9A820 = 0x1932000, Task = new(new(NativeBattleStage137.CallbackAddress, null), 1) };
        before = before with { Groups = before.Groups.Select(group => group with { Flags = 0xA1 }).ToImmutableArray() };
        NativeBattleStageState after = before.Rebuild50E3C0(source, new(0xAABBCC, 0xFF112233), -4, () => throw new InvalidOperationException(), () => throw new InvalidOperationException());
        Assert.Equal(before.Flags, after.Flags);
        Assert.Equal(before.Rotation, after.Rotation);
        Assert.Same(before.Task, after.Task);
        Assert.Equal(before.SourceAddress1D9A820, after.SourceAddress1D9A820);
        Assert.All(after.Groups, group => Assert.Equal((byte)0xA1, group.Flags));
        Assert.Equal(new NativeBattleStageColors(0xAABBCC, 0x32112233), after.ModelColors);
        Assert.Equal((short)-4, after.RotationSpeed);
        Assert.Equal(new byte[] { 3, 4 }, after.TextureAnimations!.Value.ToArray());
    }
}
