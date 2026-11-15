namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTaskContactTraversalTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(32768)]
    [InlineData(65535)]
    [InlineData(65536)]
    public void Count_keeps_its_full_value_and_projects_only_an_exact_original_word(int count)
    {
        NativeBattleModulePoolTask task = Task();
        task.WriteContactCount(0x5A, count);

        Assert.Equal(count, task.ReadContactCount(0x5A));
        if (count <= ushort.MaxValue)
        {
            Assert.Equal(count, (int)task.ReadWord(0x5A));
        }
        else
        {
            Assert.Throws<NotSupportedException>(() => task.ReadWord(0x5A));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(127)]
    [InlineData(128)]
    [InlineData(255)]
    [InlineData(256)]
    public void Traversal_keeps_its_full_value_and_projects_only_an_exact_original_byte(int position)
    {
        NativeBattleModulePoolTask task = Task();
        task.WriteTraversalPosition(0x2B, position);

        Assert.Equal(position, task.ReadTraversalPosition(0x2B));
        if (position <= byte.MaxValue)
        {
            Assert.Equal(position, (int)task.ReadByte(0x2B));
        }
        else
        {
            Assert.Throws<NotSupportedException>(() => task.ReadByte(0x2B));
        }
    }

    [Fact]
    public void Raw_overwrites_invalidate_live_count_and_position_without_a_scalar_fallback()
    {
        NativeBattleModulePoolTask task = Task();
        task.WriteContactCount(0x5A, 4).WriteTraversalPosition(0x2B, 5);
        Assert.Throws<ArgumentOutOfRangeException>(() => task.WriteContactCount(0x5A, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => task.WriteTraversalPosition(0x2B, -1));
        Assert.Equal(4, task.ReadContactCount(0x5A));
        Assert.Equal(5, task.ReadTraversalPosition(0x2B));

        task.WriteByte(0x5B, 1).WriteByte(0x2B, 8);

        Assert.Equal(260, (int)task.ReadWord(0x5A));
        Assert.Equal(8, (int)task.ReadByte(0x2B));
        Assert.Throws<InvalidDataException>(() => task.ReadContactCount(0x5A));
        Assert.Throws<InvalidDataException>(() => task.ReadTraversalPosition(0x2B));
    }

    private static NativeBattleModulePoolTask Task() => NativeBattleEffectTaskPool.Initialize(0x2000, 0x1000, 100, 1).Slots[0];
}
