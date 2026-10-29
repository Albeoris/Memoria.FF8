using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleVramRowCopyTests
{
    [Theory]
    [InlineData(3, 7, 6, 4, 100, 40)]
    [InlineData(0, 4, 1024, 3, 0, 8)]
    [InlineData(10, 20, 5, 3, 11, 20)]
    [InlineData(11, 20, 5, 3, 10, 20)]
    [InlineData(10, 20, 7, 2, 13, 20)]
    [InlineData(8, 10, 7, 4, 8, 11)]
    [InlineData(8, 11, 7, 4, 8, 10)]
    [InlineData(8, 10, 7, 4, 9, 11)]
    [InlineData(9, 11, 7, 4, 8, 10)]
    [InlineData(1019, 510, 5, 2, 1018, 510)]
    [InlineData(1023, 511, 1, 1, 0, 0)]
    [InlineData(13, 49, 5, 3, 13, 49)]
    [InlineData(1024, 510, 0, 2, 1024, 510)]
    [InlineData(1, 512, 3, 0, 12, 512)]
    [InlineData(1024, 512, 0, 0, 1024, 512)]
    public void Copy_matches_forward_native_dword_then_byte_order_across_rows_and_restore(short sourceX, short sourceY, short width, short height, short destinationX, short destinationY)
    {
        AssertCopies(new NativeBattleEffectVramCopy(sourceX, sourceY, width, height, destinationX, destinationY));
    }

    [Fact]
    public void Later_queued_copy_reads_prior_command_and_does_not_rewrite_its_emitted_upload()
    {
        AssertCopies(new NativeBattleEffectVramCopy(8, 10, 7, 4, 9, 11), new NativeBattleEffectVramCopy(9, 11, 7, 4, 8, 10));
    }

    [Fact]
    public void Odd_width_right_overlap_reference_distinguishes_movsd_from_memmove_and_bytewise_copy()
    {
        byte[] bytes = new byte[16];
        for (int index = 0; index < 8; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(index * 2), (ushort)(index + 1));
        }
        byte[] moved = bytes.ToArray();
        moved.AsSpan(0, 10).CopyTo(moved.AsSpan(2, 10));
        byte[] bytewise = bytes.ToArray();
        for (int index = 0; index < 10; index++)
        {
            bytewise[index + 2] = bytewise[index];
        }
        CopyReference(bytes, new(0, 0, 5, 1, 1, 0));
        Assert.Equal(new ushort[] { 1, 1, 2, 2, 4, 4, 7, 8 }, Enumerable.Range(0, 8).Select(index => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(index * 2))));
        Assert.NotEqual(moved, bytes);
        Assert.NotEqual(bytewise, bytes);
    }

    private static void AssertCopies(params NativeBattleEffectVramCopy[] copies)
    {
        byte[] initial = new byte[NativeBattleEffectArena.Length];
        new Random(0x45BDD0).NextBytes(initial);
        byte[] expected = initial.ToArray();
        List<byte[]> expectedUploads = [];
        NativeBattleEffectTextureState queued = NativeBattleEffectTextureState.FromColdProcess().Upload45BD30(0, 0, 1024, 512, initial);
        foreach (NativeBattleEffectVramCopy copy in copies)
        {
            queued = queued.EnqueueCopy505EB0(copy.SourceX, copy.SourceY, copy.Width, copy.Height, copy.DestinationX, copy.DestinationY);
            CopyReference(expected, copy);
            expectedUploads.Add(DestinationBytes(expected, copy));
        }
        NativeBattleEffectTextureState frozen = NativeBattleStorageSnapshots.Capture(queued);
        string saved = JsonSerializer.Serialize(frozen);
        NativeBattleEffectTextureState restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffectTextureState>(saved)!);
        NativeBattleEffectTextureState independent = NativeBattleStorageSnapshots.Restore(frozen);

        NativeBattleEffectTextureDrain actual = queued.Drain505D20();
        NativeBattleEffectTextureDrain replay = restored.Drain505D20();
        Assert.Equal(expected, actual.State.ReadVram());
        Assert.Equal(expected, replay.State.ReadVram());
        Assert.Equal(0, actual.State.Count);
        Assert.Equal(copies.Length, actual.Uploads.Length);
        for (int index = 0; index < copies.Length; index++)
        {
            NativeBattleEffectVramCopy copy = copies[index];
            NativeBattleEffectVramUpload upload = actual.Uploads[index];
            Assert.Equal(copy.DestinationX, upload.X);
            Assert.Equal(copy.DestinationY, upload.Y);
            Assert.Equal(copy.Width, upload.Width);
            Assert.Equal(copy.Height, upload.Height);
            Assert.True(upload.Copied);
            Assert.Equal(expectedUploads[index], upload.Bytes.ToArray());
        }
        Assert.Equal(JsonSerializer.Serialize(actual.Uploads), JsonSerializer.Serialize(replay.Uploads));
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(actual.State)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(replay.State)));
        Assert.Equal(initial, independent.ReadVram());
        Assert.Equal(initial, frozen.ReadVram());
        Assert.Equal(initial, queued.ReadVram());
        Assert.Equal(saved, JsonSerializer.Serialize(frozen));
    }

    private static void CopyReference(byte[] memory, NativeBattleEffectVramCopy copy)
    {
        Span<byte> register = stackalloc byte[4];
        int rowLength = copy.Width * 2;
        for (int row = 0; row < copy.Height; row++)
        {
            int source = 2 * ((copy.SourceY + row) * 1024 + copy.SourceX);
            int destination = 2 * ((copy.DestinationY + row) * 1024 + copy.DestinationX);
            int offset = 0;
            // REP MOVSD reads one complete DWORD before its write. Rows and the MOVSB tail advance forwards.
            for (; offset + 4 <= rowLength; offset += 4)
            {
                memory.AsSpan(source + offset, 4).CopyTo(register);
                register.CopyTo(memory.AsSpan(destination + offset, 4));
            }
            for (; offset < rowLength; offset++)
            {
                memory[destination + offset] = memory[source + offset];
            }
        }
    }

    private static byte[] DestinationBytes(byte[] memory, NativeBattleEffectVramCopy copy)
    {
        int rowLength = copy.Width * 2;
        byte[] result = new byte[rowLength * copy.Height];
        for (int row = 0; row < copy.Height; row++)
        {
            int offset = 2 * ((copy.DestinationY + row) * 1024 + copy.DestinationX);
            memory.AsSpan(offset, rowLength).CopyTo(result.AsSpan(row * rowLength));
        }
        return result;
    }
}
