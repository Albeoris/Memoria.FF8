using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect9ParticleTests
{
    [Fact]
    public void Initialization_consumes_three_draws_in_axis_order_and_preserves_age()
    {
        NativeCrtRandomState expected = new(1);
        expected = expected.Draw(out ushort x);
        expected = expected.Draw(out ushort y);
        expected = expected.Draw(out ushort z);
        (NativeCrtRandomState random, NativeBattleModulePoolTask task) = NativeBattleEffect9Particles.Initialize(new(1), Task().WriteWord(0x50, 7));
        Assert.Equal(expected, random);
        Assert.Equal(new ushort[] { (ushort)(x & 4095), (ushort)(y & 4095), (ushort)(z & 4095) }, new[] { task.ReadWord(0x40), task.ReadWord(0x42), task.ReadWord(0x44) });
        Assert.Equal(0x1634B84u, task.ReadDword(0x4C));
        Assert.Equal((ushort)10, task.ReadWord(0x52));
        Assert.Equal((ushort)7, task.ReadWord(0x50));
        Assert.Equal((byte)1, task.ReadByte(0x29));
    }

    [Fact]
    public void Orbit_advances_Y_before_rotation_and_preserves_unknown_packed_W()
    {
        NativeBattleModulePoolTask task = Task().MarkPayloadUnknown(0x22, 2);
        task = NativeBattleEffect9Particles.Position(task, new(32760, -100, 300));
        Assert.Equal((ushort)256, task.ReadWord(0x42));
        Assert.Equal(unchecked((ushort)(32760 + 73)), task.ReadWord(0x1C));
        Assert.Equal(unchecked((ushort)-100), task.ReadWord(0x1E));
        Assert.Equal((ushort)477, task.ReadWord(0x20));
        Assert.Throws<NotSupportedException>(() => task.ReadWord(0x22));
    }

    [Theory]
    [InlineData(9, 10, 0, 1)]
    [InlineData(10, 10, 5, 2)]
    [InlineData(32767, 32768, 0, 1)]
    public void Lifetime_uses_signed_wrapped_WORD_and_strict_greater_comparison(int age, int expectedAge, int flags, int phase)
    {
        NativeBattleModulePoolTask task = Task().WriteWord(0x50, unchecked((ushort)age)).WriteWord(0x52, 10).WriteByte(0x29, 1);
        task = NativeBattleEffect9Particles.AdvanceLife(task);
        Assert.Equal(unchecked((ushort)expectedAge), task.ReadWord(0x50));
        Assert.Equal((byte)flags, task.ReadByte(0x26));
        Assert.Equal((byte)phase, task.ReadByte(0x29));
    }

    private static NativeBattleModulePoolTask Task()
    {
        return new(1, 0, null, 0x8C3E00, new byte[80].ToImmutableArray());
    }
}
