using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWater222TexturePairTests
{
    [Fact]
    public void Successful_pair_submits_pixels_then_CLUT_and_advances_IP_once()
    {
        NativeBattleWater222Resources resources = Resources();
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess();
        NativeBattleWater222TextureStep step = NativeBattleWater222TexturePair.Transfer27(resources.Profile, resources.Files, 2, 0, 0, 0x100, 17, plan => textures = NativeBattleWater222TexturePair.Enqueue505DF0(textures, resources.Files, plan));
        Assert.Equal((byte)2, step.Count);
        Assert.Equal(0x104u, step.InstructionPointer);
        Assert.False(step.Yield);
        Assert.Null(step.Wait);
        Assert.Equal(new[] { 0x104, 0x124 }, step.Uploads.Select(plan => plan.Offset));
        NativeBattleEffectTextureDrain drain = textures.Drain505D20();
        Assert.Equal(new byte[] { 0x22, 0x33 }, drain.Uploads[0].Bytes);
        Assert.Equal(new byte[] { 0x42, 0x55 }, drain.Uploads[1].Bytes);
        Assert.Equal((short)0, drain.Uploads[0].Y);
        Assert.Equal((short)1, drain.Uploads[1].Y);
    }

    [Fact]
    public void Saturated_second_transfer_retains_first_upload_and_retry_repeats_pixels()
    {
        NativeBattleWater222Resources resources = Resources();
        List<NativeBattleWater222UploadPlan> submitted = [];
        NativeBattleWater222TextureStep first = NativeBattleWater222TexturePair.Transfer27(resources.Profile, resources.Files, 0, 0, 11, 0x100, 23, submitted.Add);
        Assert.Equal((byte)12, first.Count);
        Assert.Equal(0x100u, first.InstructionPointer);
        Assert.True(first.Yield);
        Assert.Equal((ushort?)23, first.Wait);
        Assert.Single(first.Uploads);
        NativeBattleWater222TextureStep retry = NativeBattleWater222TexturePair.Transfer27(resources.Profile, resources.Files, 0, 0, 0, first.InstructionPointer, 23, submitted.Add);
        Assert.False(retry.Yield);
        Assert.Equal(3, submitted.Count);
        Assert.Equal(submitted[0], submitted[1]);
        Assert.Equal(0x120, submitted[2].Offset);
    }

    [Theory]
    [InlineData(12)]
    [InlineData(255)]
    public void Unsigned_count_limit_does_not_wrap_or_reset(byte count)
    {
        NativeBattleWater222Resources resources = Resources();
        NativeBattleWater222TextureStep step = NativeBattleWater222TexturePair.Transfer27(resources.Profile, resources.Files, 0, 0, count, uint.MaxValue, 9, _ => throw new InvalidOperationException());
        Assert.Equal(count, step.Count);
        Assert.Equal(uint.MaxValue, step.InstructionPointer);
        Assert.True(step.Yield);
        Assert.Empty(step.Uploads);
    }

    [Fact]
    public void Busy_does_not_read_any_source_or_operand_while_capacity_check_follows_pixel_metadata()
    {
        NativeBattleWater222TextureStep busy = NativeBattleWater222TexturePair.Transfer27(null!, default, -1, 1, 7, 123, 42, _ => throw new InvalidOperationException());
        Assert.Equal((byte)7, busy.Count);
        Assert.Equal(123u, busy.InstructionPointer);
        Assert.Equal((ushort?)42, busy.Wait);
        NativeBattleWater222Resources resources = Resources();
        Assert.Throws<NotSupportedException>(() => NativeBattleWater222TexturePair.Transfer27(resources.Profile, resources.Files, -1, 0, 12, 0, 0, Ignore));
        ImmutableArray<NativeBattleEffectHeapAllocation> missing = resources.Files.SetItem(1, new("current", []));
        Assert.Throws<InvalidDataException>(() => NativeBattleWater222TexturePair.Transfer27(resources.Profile, missing, 0, 0, 12, 0, 0, Ignore));
    }

    [Fact]
    public void Current_file1_slot_mutation_changes_upload_source_and_survives_JSON_without_baseline_hydration()
    {
        NativeBattleWater222Resources original = Resources();
        NativeBattleEffectHeapAllocation changed = original.Files[1] with { Data = original.Files[1].Data.SetItem(0x100, 0xEA) };
        NativeBattleWater222Resources current = original with { Files = original.Files.SetItem(1, changed) };
        NativeBattleWater222Resources cold = JsonSerializer.Deserialize<NativeBattleWater222Resources>(JsonSerializer.Serialize(current))!;
        cold.Validate();
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess();
        NativeBattleWater222TextureStep step = NativeBattleWater222TexturePair.Transfer27(cold.Profile, cold.Files, 0, 0, 0, uint.MaxValue - 1, 0, plan => textures = NativeBattleWater222TexturePair.Enqueue505DF0(textures, cold.Files, plan));
        Assert.Equal(2u, step.InstructionPointer);
        Assert.Equal((byte)0xEA, textures.Drain505D20().Uploads[0].Bytes[0]);
        Assert.Equal((byte)0x20, original.Files[1].Data[0x100]);
    }

    [Fact]
    public void Pixel_directory_masks_high_byte_but_CLUT_directory_and_source_index_do_not()
    {
        NativeBattleWater222Resources resources = Resources();
        byte[] changed = resources.Files[1].Data.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(changed.AsSpan(0x40), 0xAB0000C0);
        ImmutableArray<NativeBattleEffectHeapAllocation> files = resources.Files.SetItem(1, new("current", changed.ToImmutableArray()));
        Assert.Equal(0x100, NativeBattleWater222TexturePair.Resolve(resources.Profile, files, 0, false).Offset);
        BinaryPrimitives.WriteUInt32LittleEndian(changed.AsSpan(0x60), 0xAB0000C0);
        files = files.SetItem(1, new("current", changed.ToImmutableArray()));
        Assert.Throws<InvalidDataException>(() => NativeBattleWater222TexturePair.Resolve(resources.Profile, files, 0, true));
        NativeBattleWater222Profile bad = resources.Profile with { Tables = resources.Profile.Tables with { Data = resources.Profile.Tables.Data.SetItem(88, 0x81) } };
        Assert.Throws<NotSupportedException>(() => NativeBattleWater222TexturePair.Resolve(bad, resources.Files, 0, true));
        Assert.Throws<NotSupportedException>(() => NativeBattleWater222TexturePair.Resolve(resources.Profile, resources.Files, 5, false));
    }

    private static void Ignore(NativeBattleWater222UploadPlan plan)
    {
    }

    private static NativeBattleWater222Resources Resources()
    {
        byte[] tables = new byte[0x70];
        byte[] data = new byte[0x26258];
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(0x14), 0x40);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(8), 0x60);
        for (int index = 0; index < 5; index++)
        {
            for (int table = 0; table < 2; table++)
            {
                int offset = table * 40 + index * 8;
                BinaryPrimitives.WriteInt16LittleEndian(tables.AsSpan(offset), (short)index);
                BinaryPrimitives.WriteInt16LittleEndian(tables.AsSpan(offset + 2), (short)table);
                BinaryPrimitives.WriteInt16LittleEndian(tables.AsSpan(offset + 4), 1);
                BinaryPrimitives.WriteInt16LittleEndian(tables.AsSpan(offset + 6), 1);
            }
            tables[80 + index] = 0x81;
            tables[88 + index] = 1;
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(0x40 + index * 4), (uint)(0xC0 + index * 2));
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(0x60 + index * 4), (uint)(0xC0 + index * 2));
            data[0x100 + index * 2] = (byte)(0x20 + index);
            data[0x101 + index * 2] = 0x33;
            data[0x120 + index * 2] = (byte)(0x40 + index);
            data[0x121 + index * 2] = 0x55;
        }
        return new([new("file0", new byte[0x1799C].ToImmutableArray()), new("file1", data.ToImmutableArray())], new(new("tables", tables.ToImmutableArray())));
    }
}
