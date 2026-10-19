using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSleepingGas168Tests
{
    [Fact]
    public void Native_normalize_return_square_retains_the_original_float_store_before_integer_conversion()
    {
        Assert.Equal(16785408, NativeBattleSleepingGas168.NormalizeSquare(4097, 0, 0));
        Assert.Equal(0, NativeBattleSleepingGas168.NormalizeSquare(0, 0, 0));
    }

    [Fact]
    public void Wave_decays_its_along_cloud_speed_permanently_but_advances_base_phase_only_one_hundred()
    {
        byte[] cloud = new byte[28];
        byte[] offsets = new byte[61 * 8];
        NativeBattleSleepingGas168.PutWord(cloud, 10, 64);
        NativeBattleSleepingGas168.PutWord(cloud, 14, 100);
        NativeBattleSleepingGas168.PutWord(cloud, 16, 5);
        NativeBattleSleepingGas168.PutWord(cloud, 18, 2048);
        NativeBattleSleepingGas168.AdvanceWave(cloud, offsets);
        Assert.Equal((short)100, NativeBattleSleepingGas168.Word(cloud, 8));
        Assert.Equal((short)31, NativeBattleSleepingGas168.Word(cloud, 10));
        Assert.Equal((short)0, NativeBattleSleepingGas168.Word(cloud, 12));
        Assert.Equal((short)2048, NativeBattleSleepingGas168.Word(cloud, 18));
        NativeBattleSleepingGas168.AdvanceWave(cloud, offsets);
        Assert.Equal((short)200, NativeBattleSleepingGas168.Word(cloud, 8));
        Assert.Equal((short)31, NativeBattleSleepingGas168.Word(cloud, 10));
        Assert.Equal((short)100, NativeBattleSleepingGas168.Word(cloud, 12));
    }

    [Fact]
    public void Partial_cloud_preserves_active_fourth_words_and_collapses_tail_from_the_last_actual_vertex()
    {
        byte[] cloud = new byte[28];
        BinaryPrimitives.WriteInt32LittleEndian(cloud, 2);
        BinaryPrimitives.WriteInt32LittleEndian(cloud.AsSpan(4), 2);
        byte[] wave = new byte[61 * 8];
        BinaryPrimitives.WriteInt32LittleEndian(wave, 10);
        BinaryPrimitives.WriteInt32LittleEndian(wave.AsSpan(4), 20);
        BinaryPrimitives.WriteInt32LittleEndian(wave.AsSpan(8), 30);
        BinaryPrimitives.WriteInt32LittleEndian(wave.AsSpan(12), 40);
        byte[] source = new byte[8 + 122 * 8];
        byte[] output = new byte[122 * 8];
        for (int index = 0; index < 122; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(8 + index * 8), (short)(index * 100));
            BinaryPrimitives.WriteUInt16LittleEndian(output.AsSpan(index * 8 + 6), (ushort)(0xA000 + index));
        }
        ImmutableArray<int> order = Enumerable.Range(0, 122).ToImmutableArray();
        NativeBattleSleepingGas168.BuildShape(cloud, wave, output, order, source.ToImmutableArray(), true);
        Assert.Equal(2, BinaryPrimitives.ReadInt32LittleEndian(cloud));
        Assert.Equal((short)330, NativeBattleSleepingGas168.Word(output, 4 * 8));
        Assert.Equal((short)15, NativeBattleSleepingGas168.Word(output, 4 * 8 + 2));
        Assert.Equal((short)40, NativeBattleSleepingGas168.Word(output, 4 * 8 + 4));
        Assert.Equal((ushort)0xA003, BinaryPrimitives.ReadUInt16LittleEndian(output.AsSpan(4 * 8 + 6)));
        Assert.Equal((ushort)0xA000, BinaryPrimitives.ReadUInt16LittleEndian(output.AsSpan(6)));
        NativeBattleSleepingGas168.BuildShape(cloud, wave, output, order, source.ToImmutableArray(), false);
        Assert.Equal(4, BinaryPrimitives.ReadInt32LittleEndian(cloud));
    }
}
