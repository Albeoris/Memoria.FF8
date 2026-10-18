using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGilgameshSwordTrailCurveTests
{
    [Fact]
    public void Coefficients_use_stride16_binomial_products_and_preserve_fourth_DWORD_and_unused_work_tail()
    {
        NativeBattleEffectArena points = NativeBattleEffectArena.Clear();
        for (int index = 0; index < 10; index++)
        {
            byte[] bytes = new byte[6];
            BinaryPrimitives.WriteInt16LittleEndian(bytes, short.MaxValue);
            points = points.Write(index * 8, bytes).MarkUnknown(index * 8 + 6, 2);
        }
        NativeBattleEffectArena work = NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, 0x190).ToArray());
        work = NativeBattleGilgameshSwordTrailCurve.Prepare571620(10, points, 0, work);
        Assert.Equal(294903, BinaryPrimitives.ReadInt32LittleEndian(work.Read(16, 4)));
        Assert.Equal(4128642, BinaryPrimitives.ReadInt32LittleEndian(work.Read(64, 4)));
        for (int index = 0; index < 10; index++)
        {
            Assert.Equal(new byte[] { 0xA5, 0xA5, 0xA5, 0xA5 }, work.Read(index * 16 + 12, 4));
        }
        Assert.Equal(Enumerable.Repeat((byte)0xA5, 0x190 - 160).ToArray(), work.Read(160, 0x190 - 160));
        NativeBattleEffectArena output = NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, 8).ToArray());
        output = NativeBattleGilgameshSwordTrailCurve.Evaluate571690(10, work, 3072, output, 0);
        Assert.Equal((short)27298, BinaryPrimitives.ReadInt16LittleEndian(output.Read(0, 2)));
        Assert.Equal(new byte[] { 0xA5, 0xA5 }, output.Read(6, 2));
        Assert.Equal(short.MaxValue, BinaryPrimitives.ReadInt16LittleEndian(NativeBattleGilgameshSwordTrailCurve.Evaluate571690(10, work, 0, output, 0).Read(0, 2)));
        Assert.Equal(short.MaxValue, BinaryPrimitives.ReadInt16LittleEndian(NativeBattleGilgameshSwordTrailCurve.Evaluate571690(10, work, 4096, output, 0).Read(0, 2)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2048)]
    [InlineData(4096)]
    public void Single_point_helper_is_constant_and_never_reads_unknown_point_fourth_WORD(int fraction)
    {
        byte[] xyz = new byte[6];
        BinaryPrimitives.WriteInt16LittleEndian(xyz, -123);
        NativeBattleEffectArena points = NativeBattleEffectArena.Clear().Write(0, xyz).MarkUnknown(6, 2);
        NativeBattleEffectArena work = NativeBattleGilgameshSwordTrailCurve.Prepare571620(1, points, 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 0x190));
        NativeBattleEffectArena output = NativeBattleGilgameshSwordTrailCurve.Evaluate571690(1, work, fraction, NativeBattleEffectArena.Clear().MarkUnknown(6, 2), 0);
        Assert.Equal((short)-123, BinaryPrimitives.ReadInt16LittleEndian(output.Read(0, 2)));
        Assert.Throws<NotSupportedException>(() => output.Read(6, 2));
    }

    [Fact]
    public void Curve_count_domain_is_explicit()
    {
        Assert.Throws<InvalidDataException>(() => NativeBattleGilgameshSwordTrailCurve.Prepare571620(0, NativeBattleEffectArena.Clear(), 0, NativeBattleEffectArena.Clear()));
        Assert.Throws<InvalidDataException>(() => NativeBattleGilgameshSwordTrailCurve.Evaluate571690(11, NativeBattleEffectArena.Clear(), 0, NativeBattleEffectArena.Clear(), 0));
    }
}
