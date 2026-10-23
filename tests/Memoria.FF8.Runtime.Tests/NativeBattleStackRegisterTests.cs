using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleStackRegisterTests
{
    [Fact]
    public void Unknown_DI_preserves_known_zero_high_WORD_without_fabricating_low_bytes()
    {
        var stack = NativeBattleStackBytes.Unknown("root", -8, 8).StoreUInt64(-8, ulong.MaxValue, 0x1000, "old-literal");
        stack = stack.StoreRegister32(-8, 0x0000ABCD, 0x0C, 0x67FDB4, "partial-EDI");
        stack.Validate();
        Assert.Equal(new byte[] { 0, 0, 0, 0, 255, 255, 255, 255 }, stack.Bytes.ToArray());
        Assert.Equal(new[] { false, false, true, true, true, true, true, true }, stack.Known.ToArray());
        Assert.Equal((ushort)0, stack.ReadUInt16(-6));
        Assert.Throws<NotSupportedException>(() => stack.ReadUInt16(-8));
        Assert.Throws<NotSupportedException>(() => stack.ReadUInt32(-8));
        Assert.Equal(uint.MaxValue, stack.ReadUInt32(-4));
        var observation = stack.Observations[^1];
        Assert.Equal(NativeBattleStackOperation.RegisterStore, observation.Operation);
        Assert.Equal(4, observation.Length);
        Assert.Equal(2L, observation.Order);
        Assert.Equal(new[] { false, false, true, true }, observation.RegisterKnownBytes!.Value.ToArray());
        Assert.Equal(new byte[4], observation.Literal.ToArray());
    }

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)15)]
    public void Full_register_store_mask_defines_knowledge_independently_of_prior_literal(byte mask)
    {
        var stack = NativeBattleStackBytes.Unknown("root", -4, 4).StoreUInt32(-4, uint.MaxValue, 0x1000, "prior");
        stack = stack.StoreRegister32(-4, 0x12345678, mask, 0x1001, "register");
        stack.Validate();
        Assert.All(stack.Known, value => Assert.Equal(mask == 15, value));
        if (mask == 15)
        {
            Assert.Equal(0x12345678u, stack.ReadUInt32(-4));
        }
        else
        {
            Assert.Equal(new byte[4], stack.Bytes.ToArray());
            Assert.Throws<NotSupportedException>(() => stack.ReadUInt16(-4));
        }
    }

    [Fact]
    public void Masked_register_overwrite_removes_the_entire_overlapping_symbolic_pointer()
    {
        var original = NativeBattleStackBytes.Unknown("root", -8, 8).StorePointer(-8, new(NativeBattleStackPointerKind.Frame, "root", -0x20), 0x1000, "pointer");
        var overwritten = original.StoreRegister32(-6, 0x11223344, 0x0A, 0x1001, "overlap");
        overwritten.Validate();
        Assert.Empty(overwritten.Pointers);
        Assert.Equal(new byte[] { 0, 0, 0, 0x33, 0, 0x11, 0, 0 }, overwritten.Bytes.ToArray());
        Assert.Equal(new[] { false, false, false, true, false, true, false, false }, overwritten.Known.ToArray());
        Assert.Throws<NotSupportedException>(() => overwritten.EqualsUInt32(-8, 0));
        Assert.Throws<InvalidDataException>(() => (overwritten with { Pointers = original.Pointers }).Validate());
    }

    [Fact]
    public void JSON_restore_replays_register_mask_and_next_store_in_identical_order()
    {
        var first = NativeBattleStackBytes.Unknown("root", -16, 16).StoreRegister32(-16, 0xAABBCCDD, 5, 0x1000, "masked");
        first = first.StorePointer(-8, new(NativeBattleStackPointerKind.Allocation, "temporary128", 0), 0x1001, "pointer");
        string json = JsonSerializer.Serialize(first);
        var second = JsonSerializer.Deserialize<NativeBattleStackBytes>(json)!;
        second.Validate();
        Assert.Equal(json, JsonSerializer.Serialize(second));
        Assert.Equal(new byte[] { 0xDD, 0, 0xBB, 0 }, second.Bytes.Take(4).ToArray());
        Assert.Equal(new[] { true, false, true, false }, second.Known.Take(4).ToArray());
        first = first.StoreRegister32(-16, 0x11223344, 0x0C, 0x1002, "next-register");
        second = second.StoreRegister32(-16, 0x11223344, 0x0C, 0x1002, "next-register");
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
        Assert.Equal((ushort)0x1122, second.ReadUInt16(-14));
        Assert.Throws<NotSupportedException>(() => second.ReadUInt16(-16));
        Assert.Equal(3L, second.Observations[^1].Order);
        Assert.True(second.IsNonzero(-8));
    }

    [Theory]
    [InlineData((byte)16)]
    [InlineData((byte)255)]
    public void Register_masks_cannot_claim_bytes_outside_the_DWORD(byte mask)
    {
        var stack = NativeBattleStackBytes.Unknown("root", -4, 4);
        Assert.Throws<InvalidDataException>(() => stack.StoreRegister32(-4, 0, mask, 0x1000, "invalid-mask"));
    }

    [Fact]
    public void Malformed_JSON_register_observations_and_forged_snapshot_knowledge_fail_replay()
    {
        var stack = NativeBattleStackBytes.Unknown("root", -4, 4).StoreRegister32(-4, 0, 0x0C, 0x1000, "register");
        var writer = stack.Observations.Single();
        Reject(stack with { Observations = [writer with { RegisterKnownBytes = null }] });
        Reject(stack with { Observations = [writer with { RegisterKnownBytes = ImmutableArray.Create(true) }] });
        Reject(stack with { Observations = [writer with { Length = 2 }] });
        Reject(stack with { Observations = [writer with { Literal = ImmutableArray.Create((byte)1, (byte)0, (byte)0, (byte)0) }] });
        Reject(stack with { Observations = [writer with { Operation = NativeBattleStackOperation.LiteralStore }] });
        Reject(stack with { Known = ImmutableArray.Create(true, false, true, true) });
        Reject(stack with { Bytes = ImmutableArray.Create((byte)1, (byte)0, (byte)0, (byte)0) });

        static void Reject(NativeBattleStackBytes malformed)
        {
            string json = JsonSerializer.Serialize(malformed);
            var restored = JsonSerializer.Deserialize<NativeBattleStackBytes>(json)!;
            Assert.Throws<InvalidDataException>(() => restored.Validate());
        }
    }

    [Theory]
    [InlineData(false, 0x433000008000002AUL)]
    [InlineData(true, 0x432FFFE900300000UL)]
    public void Stored_Z_QWORD_publishes_actual_high_WORD_before_integer_wrap_or_translation(bool extreme, ulong expected)
    {
        NativeBattleActionMatrix matrix = extreme ? new([4096, 0, 0, 0, 4096, 0, 32767, 32767, 32767], 111, 222, 333, 4096) : new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 111, 222, 333, 4096);
        int coordinate = extreme ? int.MinValue : 42;
        ulong stored = NativeBattleGfMatrices.StoredZ56C600(matrix, coordinate, coordinate, coordinate);
        Assert.Equal(expected, stored);
        Assert.Equal(unchecked((int)expected + int.MinValue), NativeBattleGfMatrices.RotateLong56C600(matrix, coordinate, coordinate, coordinate).Z);
        var stack = NativeBattleStackBytes.Unknown("root", -0x7C, 0x3C).StoreUInt64(-0x7C, stored, 0x56C6E5, "compose-last-bone");
        stack.Validate();
        Assert.Equal((ushort)(expected >> 48), stack.ReadUInt16(-0x76));
        Assert.NotEqual(ushort.MaxValue, stack.ReadUInt16(-0x76));
        Assert.Equal(expected, BinaryPrimitives.ReadUInt64LittleEndian(stack.Bytes.AsSpan()));
        Assert.All(stack.Known.Take(8), value => Assert.True(value));
        Assert.All(stack.Known.Skip(8), value => Assert.False(value));
        Assert.Equal(8, stack.Observations.Single().Length);
        var restored = JsonSerializer.Deserialize<NativeBattleStackBytes>(JsonSerializer.Serialize(stack))!;
        restored.Validate();
        Assert.Equal(stack.ReadUInt16(-0x76), restored.ReadUInt16(-0x76));
        Assert.Throws<NotSupportedException>(() => restored.ReadUInt16(-0x74));
    }
}
