using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleModuleImpactTests
{
    [Fact]
    public void New_kinds_append_after_existing_serialized_ids()
    {
        Assert.Equal(4, (int)NativeBattleModuleDrawKind.Mesh);
        Assert.Equal(5, (int)NativeBattleModuleDrawKind.Impact570860);
        Assert.Equal((int)NativeBattleModuleKind.SpectralCastingRing5740D0 + 1, (int)NativeBattleModuleKind.ImpactSeed5707A0);
        Assert.Equal((int)NativeBattleModuleKind.ImpactSeed5707A0 + 1, (int)NativeBattleModuleKind.Impact570860);
    }

    [Fact]
    public void Paused_seed_reads_neither_model_nor_random_and_keeps_its_physical_slot()
    {
        var state = Seed(uint.MaxValue);
        var actual = state.Advance(new(1));
        Assert.Empty(actual.Draws);
        Assert.Equal(state.Slots[0], actual.State.Slots[0]);
        Assert.Equal(0, actual.State.Head);
        Assert.Equal((byte)1, actual.State.PublishedCount);
        Assert.Null(actual.CrtRandomAfter);
    }

    [Fact]
    public void Null_model_seed_retires_without_requiring_RNG_or_a_model_reader()
    {
        var state = Seed(0);
        var actual = state.Advance(new(0));
        Assert.Empty(actual.Draws);
        Assert.Null(actual.State.Head);
        Assert.Null(actual.State.Tail);
        Assert.Equal((ushort)0, actual.State.Slots[0].Flags);
        Assert.Null(actual.CrtRandomAfter);
        Assert.Equal(state.Slots[0].Payload, actual.State.Slots[0].Payload);
    }

    [Fact]
    public void Seed_uses_three_ordered_CRT_draws_captured_yaw_and_retained_tail_then_runs_child_same_pass()
    {
        var state = Seed(ModelAddress(6));
        state = state with { Slots = state.Slots.SetItem(1, state.Slots[1] with { Payload = Enumerable.Repeat((byte)0xA5, 24).ToImmutableArray() }) };
        int modelReads = 0;
        var actual = state.Advance(new(0) { CrtRandom = new(1), ReadModelRotation14 = model =>
        {
            Assert.Equal(6, model);
            modelReads++;
            return -1234;
        } });
        Assert.Equal(1, modelReads);
        Assert.Equal(new NativeCrtRandomState(415139642), actual.CrtRandomAfter);
        Assert.Equal(1, actual.State.Head);
        Assert.Equal(1, actual.State.Tail);
        Assert.Equal((ushort)0, actual.State.Slots[0].Flags);
        var child = actual.State.Slots[1];
        Assert.Equal(NativeBattleModuleKind.Impact570860, child.Kind);
        Assert.Equal((short)1, W(child, 0));
        Assert.Equal((short)-1234, W(child, 12));
        Assert.Equal((short)41, W(child, 14));
        Assert.Equal((short)10437, W(child, 16));
        Assert.Equal((short)1598, W(child, 20));
        foreach (int offset in new[] { 2, 3, 18, 19, 22, 23 })
        {
            Assert.Equal((byte)0xA5, child.Payload[offset]);
        }
        var draw = Assert.Single(actual.Draws);
        Assert.Equal(NativeBattleModuleDrawKind.Impact570860, draw.Kind);
        Assert.Equal(new NativeBattleModuleAnchor(-1, 222, -333, -444), draw.Anchor);
        Assert.Equal((ushort)0, draw.Phase);
        Assert.Equal((short)-1234, draw.Rotation);
        Assert.Equal((short)41, draw.Orientation);
        Assert.Equal(10437, draw.Scale);
        Assert.Equal(1598, draw.ImpactScaleZ);
    }

    [Theory]
    [InlineData(0u, 0)]
    [InlineData(0x00800000u, 0)]
    [InlineData(0x007F0000u, 10437)]
    [InlineData(0xFFFF0000u, 10437)]
    public void Scale_uses_exact_seven_packed_bits_without_clamping_or_high_byte_leak(uint packed, int expected)
    {
        var state = Seed(ModelAddress(3), packed);
        var actual = state.Advance(new(0) { CrtRandom = new(1), ReadModelRotation14 = _ => 0 });
        Assert.Equal(expected, Assert.Single(actual.Draws).Scale);
        Assert.Equal(1598, actual.Draws[0].ImpactScaleZ);
        Assert.Equal(new NativeCrtRandomState(415139642), actual.CrtRandomAfter);
    }

    [Fact]
    public void Imported_full_pool_fails_before_consuming_random_or_reading_model_rotation()
    {
        var state = Seed(ModelAddress(3));
        for (int index = 1; index < 300; index++)
        {
            (state, _) = state.Append(NativeBattleModuleKind.Impact570860);
        }
        int modelReads = 0;
        Assert.Throws<InvalidOperationException>(() => state.Advance(new(0) { CrtRandom = new(1), ReadModelRotation14 = _ =>
        {
            modelReads++;
            return 0;
        } }));
        Assert.Equal(0, modelReads);
    }

    [Theory]
    [InlineData((short)-1, (short)0, false)]
    [InlineData((short)0, (short)1, false)]
    [InlineData((short)1, (short)2, false)]
    [InlineData((short)2, (short)3, false)]
    [InlineData((short)3, (short)4, true)]
    [InlineData((short)4, (short)5, true)]
    [InlineData(short.MaxValue, short.MinValue, false)]
    public void Child_draws_old_signed_age_before_WORD_increment_and_new_signed_retirement(short age, short next, bool release)
    {
        var state = Child(age);
        var actual = state.Advance(new(0));
        var draw = Assert.Single(actual.Draws);
        Assert.Equal(unchecked((ushort)age), draw.Phase);
        Assert.Equal(next, W(actual.State.Slots[0], 0));
        Assert.Equal(release ? (ushort)0 : (ushort)1, actual.State.Slots[0].Flags);
        Assert.Equal(release ? (int?)null : 0, actual.State.Head);
        Assert.Null(actual.CrtRandomAfter);
        Assert.Equal((short)-2345, draw.Rotation);
        Assert.Equal((short)3210, draw.Orientation);
        Assert.Equal(-123, draw.Scale);
        Assert.Equal(-456, draw.ImpactScaleZ);
    }

    [Fact]
    public void Paused_child_repeats_its_draw_without_payload_or_random_changes()
    {
        var state = Child(0);
        var random = new NativeCrtRandomState(9876);
        var first = state.Advance(new(1) { CrtRandom = random });
        var second = first.State.Advance(new(1) { CrtRandom = random });
        Assert.Equal(state.Slots[0].Payload, first.State.Slots[0].Payload);
        Assert.Equal(first.State.Slots[0].Payload, second.State.Slots[0].Payload);
        Assert.Equal(Assert.Single(first.Draws), Assert.Single(second.Draws));
        Assert.Equal((ushort)0, first.Draws[0].Phase);
        Assert.Equal(random, first.CrtRandomAfter);
        Assert.Equal(random, second.CrtRandomAfter);
    }

    [Fact]
    public void Natural_child_has_four_draws_and_cold_JSON_continuation_preserves_captured_fields()
    {
        var state = Child(0);
        for (ushort phase = 0; phase < 4; phase++)
        {
            var restored = JsonSerializer.Deserialize<NativeBattleModuleState>(JsonSerializer.Serialize(state))!;
            restored.Validate();
            var actual = state.Advance(new(0));
            Assert.Equal(JsonSerializer.Serialize(actual), JsonSerializer.Serialize(restored.Advance(new(0))));
            Assert.Equal(phase, Assert.Single(actual.Draws).Phase);
            state = actual.State;
        }
        Assert.Null(state.Head);
        Assert.Null(state.Tail);
        Assert.Equal((short)4, W(state.Slots[0], 0));
        Assert.Empty(state.Advance(new(0)).Draws);
    }

    [Fact]
    public void Two_seed_callbacks_share_the_current_CRT_owner_before_same_pass_children()
    {
        var state = Seed(ModelAddress(3));
        (state, int second) = state.Append(NativeBattleModuleKind.ImpactSeed5707A0);
        state = state with { Slots = state.Slots.SetItem(second, state.Slots[second] with { Payload = state.Slots[0].Payload }) };
        var actual = state.Advance(new(0) { CrtRandom = new(1), ReadModelRotation14 = _ => 100 });
        var expected = new NativeCrtRandomState(1);
        for (int index = 0; index < 6; index++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, actual.CrtRandomAfter);
        Assert.Equal(2, actual.Draws.Length);
        Assert.All(actual.Draws, draw => Assert.Equal((ushort)0, draw.Phase));
        Assert.NotEqual(actual.Draws[0].Orientation, actual.Draws[1].Orientation);
        Assert.Equal((byte)2, actual.State.PublishedCount);
    }

    [Fact]
    public void Resource_callback_dispatch_uses_impact_seed_and_same_pass_child_without_defensive_sound()
    {
        var resource = Resource();
        var state = NativeBattleModuleState.Clear().AppendModelResource(6, resource, 0x007F001F);
        int anchors = 0;
        var actual = state.Advance(new(0) { Resources = [resource], CrtRandom = new(1), ReadModelRotation14 = _ => 2048, ReadModelAnchor = actor =>
        {
            Assert.Equal(6, actor);
            anchors++;
            return new(10, 20, 30, 40);
        } });
        Assert.Equal(1, anchors);
        Assert.Equal(NativeBattleModuleKind.Impact570860, actual.State.Slots[actual.State.Head!.Value].Kind);
        var draw = Assert.Single(actual.Draws);
        Assert.Equal(NativeBattleModuleDrawKind.Impact570860, draw.Kind);
        Assert.Equal(new NativeBattleModuleAnchor(10, 20, 30, 40), draw.Anchor);
        Assert.Equal((short)2048, draw.Rotation);
        Assert.Equal(new NativeCrtRandomState(415139642), actual.CrtRandomAfter);
    }

    private static NativeBattleModuleState Seed(uint model, uint packed = 0xFFFF001F)
    {
        (var state, int slot) = NativeBattleModuleState.Clear().Append(NativeBattleModuleKind.ImpactSeed5707A0);
        byte[] payload = state.Slots[slot].Payload.ToArray();
        WriteAnchor(payload, -1, 222, -333, -444);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(12), model);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(16), packed);
        return state with { Slots = state.Slots.SetItem(slot, state.Slots[slot] with { Payload = payload.ToImmutableArray() }) };
    }

    private static NativeBattleModuleState Child(short age)
    {
        (var state, int slot) = NativeBattleModuleState.Clear().Append(NativeBattleModuleKind.Impact570860);
        byte[] payload = Enumerable.Repeat((byte)0x5A, 24).ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(payload, age);
        WriteAnchor(payload, -1, 222, -333, -444);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(12), -2345);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(14), 3210);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(16), -123);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(20), -456);
        return state with { Slots = state.Slots.SetItem(slot, state.Slots[slot] with { Payload = payload.ToImmutableArray() }) };
    }

    private static void WriteAnchor(byte[] payload, short x, short y, short z, short w)
    {
        short[] values = [x, y, z, w];
        for (int index = 0; index < values.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(4 + index * 2), values[index]);
        }
    }

    private static short W(NativeBattleModuleTask task, int offset) => BinaryPrimitives.ReadInt16LittleEndian(task.Payload.AsSpan().Slice(offset));
    private static uint ModelAddress(int actor) => 0x1D972C0u + (uint)(actor * 156);
    private static NativeBattleModuleResource Resource() => new(31, 0xC70000, 0x570CC0, 0x5707A0, new byte[] { 0, 0, 0, 1 }.ToImmutableArray());
}
