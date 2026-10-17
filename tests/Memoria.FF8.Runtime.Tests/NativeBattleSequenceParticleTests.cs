using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSequenceParticleTests
{
    [Theory]
    [InlineData(0, 0x10E00C)]
    [InlineData(1024, 0x90E00C)]
    [InlineData(3072, 0x10E00C)]
    public void Ordinary_contact_sprite_preserves_parameters_and_camera_flip(int angle, uint expected)
    {
        NativeBattleSequenceParticle particle = NativeBattleSequenceParticles.Read505900(new([12, 1, 224, 0x94], 0), angle);
        Assert.Equal(expected, particle.PackedParameters);
        Assert.Equal(1, particle.Placement);
        Assert.Equal(3, particle.NextOffset);
    }

    [Fact]
    public void Optional_bytes_and_bone_flags_use_native_priority_and_cursor()
    {
        NativeBattleSequenceParticle particle = NativeBattleSequenceParticles.Read505900(new([3, 0x7F, 1, 2, 3, 241, 0x94], 0), 0, () => throw new InvalidOperationException());
        Assert.Equal(0x03020103u, particle.PackedParameters);
        Assert.Equal(3, particle.Placement);
        Assert.Equal(241, particle.Bone);
        Assert.Equal(short.MinValue, particle.Fraction);
        Assert.Equal(6, particle.NextOffset);
    }

    [Fact]
    public void Random_bone_is_selected_once_even_when_the_caller_will_suppress_emission()
    {
        int calls = 0;
        NativeBattleSequenceParticle particle = NativeBattleSequenceParticles.Read505900(new([4, 0x30], 0), 0, () =>
        {
            calls++;
            return 7;
        });
        Assert.Equal(1, calls);
        Assert.Equal(7, particle.Bone);
        Assert.Equal(unchecked((short)0x8800), particle.Fraction);
        Assert.Equal(2, particle.NextOffset);
    }

    [Fact]
    public void Suppressed_explicit_bone_keeps_the_native_operand_cursor_before_the_bone_byte()
    {
        NativeBattleSequenceParticle particle = NativeBattleSequenceParticles.Read505900(new([1, 8, 241], 0), 0, suppressed: true);
        Assert.Equal(2, particle.NextOffset);
    }

    [Fact]
    public void Direct_position_reads_signed_little_endian_words()
    {
        NativeBattleSequenceParticle particle = NativeBattleSequenceParticles.Read505900(new([5, 0x40, 0xFF, 0xFF, 0x00, 0x80, 0x34, 0x12], 0), 0);
        Assert.Equal(4, particle.Placement);
        Assert.Equal(new NativeBattleVector(-1, short.MinValue, 0x1234), particle.Position);
        Assert.Equal(8, particle.NextOffset);
    }

    [Fact]
    public void Truncated_packet_and_missing_random_owner_fail_at_their_boundary()
    {
        Assert.Throws<InvalidDataException>(() => NativeBattleSequenceParticles.Read505900(new([1, 8], 0), 0));
        Assert.Throws<NotSupportedException>(() => NativeBattleSequenceParticles.Read505900(new([1, 16], 0), 0));
    }
}
