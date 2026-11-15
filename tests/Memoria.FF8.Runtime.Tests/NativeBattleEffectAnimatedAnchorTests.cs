using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectAnimatedAnchorTests
{
    [Theory]
    [InlineData((byte)0, (byte)0)]
    [InlineData((byte)0, (byte)1)]
    [InlineData((byte)0, (byte)2)]
    [InlineData((byte)0, (byte)3)]
    [InlineData((byte)1, (byte)0)]
    [InlineData((byte)1, (byte)1)]
    [InlineData((byte)1, (byte)2)]
    [InlineData((byte)1, (byte)3)]
    public void Literal_actor_and_anchor_selectors_read_the_correct_context_vectors(byte actor, byte selector)
    {
        List<(byte Actor, int Selector)> reads = [];
        NativeBattleEffectNodeVector Read(byte selectedActor, int selectedAnchor)
        {
            reads.Add((selectedActor, selectedAnchor));
            return new(7u << 16, 11u << 16, 15u << 16, 0);
        }

        NativeBattleAnimatedAnchorPosition position = NativeBattleEffectAnimatedAnchor.Evaluate(new(actor, selector, new(0, 0, 0), actor, selector, new(0, 0, 0), 0), NativeBattleCuragaMatrices.Identity8DD770(), anchor => Read(1, anchor), anchor => Read(0, anchor));

        Assert.Equal(new NativeBattleAnimatedAnchorPosition(7 << 16, 11 << 16, 15 << 16), position);
        Assert.Equal(new[] { (actor, (int)selector), (actor, (int)selector) }, reads);
    }

    [Fact]
    public void Context_Q16_truncates_toward_zero_then_rotated_offsets_wrap_S16_before_interpolation()
    {
        NativeBattleAnimatedAnchorPosition position = NativeBattleEffectAnimatedAnchor.Evaluate(new(0, 0, new(0, 1, -2), 1, 0, new(0, 0, 0), 2048), NativeBattleCuragaMatrices.Identity8DD770(),
            _ => new(10u << 16, 0, 0, 0), _ => new(unchecked((uint)-98304), 32767u << 16, 0x80008000, 0));
        Assert.Equal(new NativeBattleAnimatedAnchorPosition(294912, -1073741824, 1073709056), position);
    }

    [Theory]
    [InlineData((short)-4096, 0)]
    [InlineData((short)8192, 30)]
    public void Signed_Q12_ratio_extrapolates_without_clamping(short ratio, int expected)
    {
        NativeBattleAnimatedAnchorPosition position = NativeBattleEffectAnimatedAnchor.Evaluate(new(0, 0, new(0, 0, 0), 1, 0, new(0, 0, 0), ratio), NativeBattleCuragaMatrices.Identity8DD770(), _ => new(20u << 16, 20u << 16, 20u << 16, 0), _ => new(10u << 16, 10u << 16, 10u << 16, 0));
        Assert.Equal(new NativeBattleAnimatedAnchorPosition(expected << 16, expected << 16, expected << 16), position);
    }

    [Fact]
    public void Rotation_uses_GTE_saturation_before_native_short_add_and_unknown_stack_selectors_fail()
    {
        NativeBattleActionMatrix scale = new([8192, 0, 0, 0, 8192, 0, 0, 0, 8192], 0, 0, 0, 4096);
        NativeBattleAnimatedAnchorChannels channels = new(0, 0, new(32767, 32767, 32767), 0, 0, new(0, 0, 0), 0);
        Assert.Equal(new NativeBattleAnimatedAnchorPosition(int.MinValue, int.MinValue, int.MinValue), NativeBattleEffectAnimatedAnchor.Evaluate(channels, scale, _ => throw new InvalidOperationException(), _ => new(1u << 16, 1u << 16, 1u << 16, 0)));
        Assert.Throws<NotSupportedException>(() => NativeBattleEffectAnimatedAnchor.Evaluate(channels with { StartActor = 2 }, scale, _ => default, _ => default));
        Assert.Throws<NotSupportedException>(() => NativeBattleEffectAnimatedAnchor.Evaluate(channels with { EndAnchor = 4 }, scale, _ => default, _ => default));
    }

    [Fact]
    public void Channel_frame_is_signed_and_reads_bytes_or_little_endian_WORDs_at_their_own_strides()
    {
        byte[] source = new byte[0x158];
        for (int field = 0x100; field <= 0x128; field += 4)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(field), (uint)(0x1000 + field));
        }
        List<(uint Address, int Count)> reads = [];
        NativeBattleAnimatedAnchorChannels channels = NativeBattleEffectAnimatedAnchor.ReadChannels(source, -1, (address, count) =>
        {
            reads.Add((address, count));
            return count == 1 ? [(byte)(address & 1)] : [0xFE, 0xFF];
        });
        Assert.Equal((short)-2, channels.Ratio);
        Assert.Equal(new NativeBattleVector(-2, -2, -2), channels.StartOffset);
        Assert.Contains((0x10FFu, 1), reads);
        Assert.Contains((0x1107u, 1), reads);
        Assert.Contains((0x110Eu, 2), reads);
        Assert.Contains((0x1126u, 2), reads);
    }
}
