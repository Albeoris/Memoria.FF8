using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeChocoboWorldReaderTests
{
    [Theory]
    [InlineData(ChocoboWorldMode.NativeImport)]
    [InlineData(ChocoboWorldMode.AngeloSearch)]
    [InlineData(ChocoboWorldMode.Memoria)]
    public void Native_export_refuses_a_saved_provider_choice(ChocoboWorldMode mode)
    {
        GameState state = new() { GameplayPreferences = new() { ChocoboWorldMode = mode } };
        Assert.Contains("Chocobo World", Assert.Throws<NotSupportedException>(() => NativePcSaveConverter.Export(state)).Message);
    }

    [Fact]
    public void Native_export_refuses_history_even_without_a_provider_choice()
    {
        GameState state = new() { ChocoboRewards = new() { AcquiredUniqueRewardIds = ImmutableHashSet.Create("ribbon") } };
        Assert.Contains("Chocobo World", Assert.Throws<NotSupportedException>(() => NativePcSaveConverter.Export(state)).Message);
    }

    [Fact]
    public void Configured_native_save_is_decoded_without_filename_assumptions()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_CHOCOBO_WORLD_SAVE");
        Assert.SkipUnless(path is not null, "Select a native Chocobo World save with MEMORIA_FF8_CHOCOBO_WORLD_SAVE.");
        NativeChocoboWorldImport decoded = NativeChocoboWorldReader.Read(File.ReadAllBytes(path!));
        Assert.InRange(decoded.Level, 1, 100);
        Assert.Equal(4, decoded.ClassCounts.Length);
        Assert.Equal(64, decoded.RawState.Length);
    }

    [Fact]
    public void Installed_transfer_table_contains_the_native_class_probabilities()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed resources with MEMORIA_FF8_GAME_DIR.");
        using GameAssets assets = new(directory!);
        NativeChocoboWorldTransferTable table = assets.ReadNativeChocoboWorldTransferTable();
        (byte Item, int[] Counts)[] expected = [(100, [10, 1, 0, 0]), (65, [0, 1, 0, 0]), (32, [10, 5, 0, 0]), (165, [0, 2, 3, 0])];
        foreach ((byte item, int[] counts) in expected)
        {
            Assert.Equal(counts, Enumerable.Range(0, 4).Select(rewardClass => Enumerable.Range(0, 128).Count(row => table.Select(rewardClass, (byte)row) == item)));
        }
        (byte Item, int[] Even, int[] Odd)[] reachable = [(100, [5, 1, 0, 0], [5, 0, 0, 0]), (65, [0, 1, 0, 0], [0, 0, 0, 0]), (32, [5, 3, 0, 0], [5, 2, 0, 0]), (165, [0, 0, 2, 0], [0, 2, 1, 0])];
        foreach ((byte item, int[] even, int[] odd) in reachable)
        {
            foreach (int parity in new[] { 0, 1 })
            {
                NativeMenuRandomState random = new((ushort)parity);
                List<byte> draws = [];
                for (int index = 0; index < 64; index++)
                {
                    random = random.Advance(1);
                    draws.Add((byte)(random.Seed & 127));
                }
                Assert.Equal(64, draws.Distinct().Count());
                Assert.Equal(parity == 0 ? even : odd, Enumerable.Range(0, 4).Select(rewardClass => draws.Count(draw => table.Select(rewardClass, draw) == item)));
            }
        }
    }

    [Fact]
    public void Pc_decodes_packed_decimal_and_preserves_unknown_world_bytes()
    {
        byte[] image = PcImage();
        image[3] = 0;
        image[2 + 0x14] = 0x42;
        image[400] = 0xBA;
        NativeChocoboWorldImport decoded = NativeChocoboWorldReader.Read(image);
        Assert.Equal(100, decoded.Level);
        Assert.Equal(42, decoded.ClassCounts[0]);
        Assert.Equal(211, decoded.BokoId);
        Assert.Equal(0xBA, decoded.RawWorld[398]);
        Assert.Equal(64, decoded.RawState.Length);
    }

    [Fact]
    public void Pc_compressed_container_is_identified_by_bytes()
    {
        byte[] source = PcImage();
        using MemoryStream stream = new();
        stream.Write(new byte[4]);
        for (int offset = 0; offset < source.Length; offset += 8)
        {
            int count = Math.Min(8, source.Length - offset);
            stream.WriteByte((byte)((1 << count) - 1));
            stream.Write(source, offset, count);
        }
        byte[] compressed = stream.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(compressed, (uint)(compressed.Length - 4));
        NativeChocoboWorldImport decoded = NativeChocoboWorldReader.Read(compressed);
        Assert.Equal("PC LZSS", decoded.Format);
        Assert.Equal(2u, decoded.Revision);
        Assert.Equal(source.AsSpan(2).ToArray(), decoded.RawWorld.ToArray());
        byte[] truncated = compressed[..^1];
        BinaryPrimitives.WriteUInt32LittleEndian(truncated, (uint)(truncated.Length - 4));
        Assert.Throws<InvalidDataException>(() => NativeChocoboWorldReader.Read(truncated));
    }

    [Fact]
    public void Mcs_selects_larger_unsigned_counter_and_falls_back_from_structural_corruption()
    {
        byte[] mcs = new byte[0xE080];
        Encoding.ASCII.GetBytes("BASLUSP00892C0426").CopyTo(mcs, 10);
        mcs[128] = (byte)'S';
        mcs[129] = (byte)'C';
        mcs[131] = 7;
        PcImage().AsSpan(2, 64).CopyTo(mcs.AsSpan(0x280));
        PcImage().AsSpan(2, 64).CopyTo(mcs.AsSpan(0x380));
        BinaryPrimitives.WriteUInt32LittleEndian(mcs.AsSpan(0x388), 0x80000000);
        Assert.Equal(1, NativeChocoboWorldReader.Read(mcs).SelectedBank);
        mcs[0x381] = 0xFA;
        NativeChocoboWorldImport fallback = NativeChocoboWorldReader.Read(mcs);
        Assert.Equal(0, fallback.SelectedBank);
        Assert.Equal(0x80000000u, fallback.BackupRevision);
        Assert.False(fallback.BackupStructurallyValid);
        mcs[0x281] = 0xFA;
        Assert.Throws<InvalidDataException>(() => NativeChocoboWorldReader.Read(mcs));
    }

    [Fact]
    public void Transfer_table_uses_one_of_128_rows_and_class_columns()
    {
        byte[] rows = Enumerable.Range(0, 512).Select(index => (byte)(index % 199)).ToArray();
        NativeChocoboWorldTransferTable table = new(rows.ToImmutableArray());
        Assert.Equal(rows[127 * 4 + 3], table.Select(3, 255));
        Assert.Equal(rows[2], table.Select(2, 128));
        Assert.Throws<ArgumentOutOfRangeException>(() => table.Select(4, 0));
    }

    [Fact]
    public void Arbitrary_raw_images_and_interrupted_pc_magic_are_rejected()
    {
        Assert.Throws<NotSupportedException>(() => NativeChocoboWorldReader.Read(new byte[0xE000]));
        byte[] image = PcImage();
        image[0] = 255;
        image[1] = 255;
        Assert.Throws<NotSupportedException>(() => NativeChocoboWorldReader.Read(image));
    }

    [Fact]
    public void Raw_application_requires_the_native_title_and_seven_block_header()
    {
        byte[] application = new byte[0xE000];
        application[0] = (byte)'S';
        application[1] = (byte)'C';
        application[3] = 7;
        Convert.FromHexString("826582658257815E82628288828F8283828F8282828F81408276828F8292828C8284").CopyTo(application, 4);
        PcImage().AsSpan(2, 64).CopyTo(application.AsSpan(0x200));
        Assert.Equal("PocketStation raw application", NativeChocoboWorldReader.Read(application).Format);
        application[3] = 1;
        Assert.Throws<InvalidDataException>(() => NativeChocoboWorldReader.Read(application));
        application[4] = 0;
        Assert.Throws<NotSupportedException>(() => NativeChocoboWorldReader.Read(application));
    }

    private static byte[] PcImage()
    {
        byte[] image = new byte[514];
        BinaryPrimitives.WriteUInt16LittleEndian(image, 0x08FF);
        image[2] = 3;
        image[3] = 0x12;
        image[4] = 0x06;
        image[5] = 0x12;
        image[8] = 0;
        image[9] = 4;
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(10), 2);
        image[14] = 0x11;
        image[15] = 2;
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(42), 0x01234567);
        return image;
    }
}
