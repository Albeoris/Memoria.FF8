using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativePcSaveCompressionTests
{
    [Fact]
    public void Native_checksum_preserves_the_original_zero_final_table_entry()
    {
        Assert.Equal((ushort)0x00FF, NativePcSaveCodec.Checksum([0]));
    }

    [Fact]
    public void Original_pc_fixture_recompresses_without_changing_decoded_bytes()
    {
        string? fixture = Environment.GetEnvironmentVariable("MEMORIA_FF8_PC_SAVE_FIXTURE");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(fixture), "Set MEMORIA_FF8_PC_SAVE_FIXTURE to an original PC save.");
        NativePcSaveData source = NativePcSaveCodec.Decode(File.ReadAllBytes(fixture!));
        byte[] encoded = NativePcSaveCodec.Encode(source);
        Assert.Equal(source.Block, NativePcSaveCodec.Decode(encoded).Block);
        Assert.True(encoded.Length < source.Block.Length);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Native_compression_roundtrips_overlapping_and_wrapped_dictionary_references(bool random)
    {
        byte[] block = new byte[NativePcSaveData.BlockSize];
        if (random)
        {
            new Random(42).NextBytes(block);
        }
        else
        {
            for (int index = 300; index < block.Length; index++)
            {
                block[index] = (byte)(index % 23);
            }
        }
        block[0] = (byte)'S';
        block[1] = (byte)'C';
        BinaryPrimitives.WriteUInt16LittleEndian(block.AsSpan(NativePcSaveData.SaveMapOffset + 2), 0x08FF);
        ushort checksum = NativePcSaveCodec.Checksum(block.AsSpan(NativePcSaveData.MainOffset, 4944));
        BinaryPrimitives.WriteUInt16LittleEndian(block.AsSpan(384), checksum);
        BinaryPrimitives.WriteUInt16LittleEndian(block.AsSpan(5408), checksum);
        byte[] compressed = NativePcSaveCodec.Encode(new(block));
        Assert.Equal(block, NativePcSaveCodec.Decode(compressed).Block);
        if (!random)
        {
            Assert.True(compressed.Length < block.Length / 4);
        }
    }
}
