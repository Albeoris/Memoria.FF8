using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleFileStagingLoaderZeroTests
{
    private const uint ZeroStart = 0x188C000;
    private const uint ZeroEnd = 0x279F000;
    private const uint SelectedWord = 0x255FEC2;

    [Fact]
    public void Unobserved_large_tail_refuses_before_allocating_the_requested_storage()
    {
        NativeBattleFileStaging cold = NativeBattleFileStaging.FromColdProcess();
        long before = GC.GetAllocatedBytesForCurrentThread();
        Assert.Throws<NotSupportedException>(() => cold.Read(ZeroStart, 64 * 1024 * 1024));
        Assert.True(GC.GetAllocatedBytesForCurrentThread() - before < 65536);
    }

    [Fact]
    public void True_cold_loader_tail_has_exact_edges_and_unknown_processes_infer_nothing()
    {
        NativeBattleFileStaging cold = NativeBattleFileStaging.FromColdProcess();
        cold.Validate();
        NativeBattleLoaderZeroRegion region = Assert.Single(cold.LoaderZeroRanges);
        Assert.Equal(ZeroStart, region.Address);
        Assert.Equal(checked((int)(ZeroEnd - ZeroStart)), region.Length);
        Assert.Equal(new byte[2], cold.Read(SelectedWord, 2));
        Assert.Equal(new byte[2], cold.Read(ZeroStart, 2));
        Assert.Equal(new byte[2], cold.Read(ZeroEnd - 2, 2));
        Assert.Throws<NotSupportedException>(() => cold.Read(ZeroStart - 1, 1));
        Assert.Throws<NotSupportedException>(() => cold.Read(ZeroEnd, 1));
        Assert.Throws<NotSupportedException>(() => cold.Read(ZeroEnd - 1, 2));
        Assert.Empty(NativeBattleFileStaging.Unknown().LoaderZeroRanges);
        Assert.Throws<NotSupportedException>(() => NativeBattleFileStaging.Unknown().Read(SelectedWord, 2));
    }

    [Fact]
    public void Current_writes_override_loader_bytes_after_warm_json_restore()
    {
        NativeBattleFileStaging cold = NativeBattleFileStaging.FromColdProcess();
        NativeBattleFileStaging written = cold.Write(SelectedWord, [0xA7, 0x39]);
        NativeBattleFileStaging restored = Restore(written);
        Assert.Equal(new byte[] { 0, 0xA7, 0x39, 0 }, restored.Read(SelectedWord - 1, 4));
        Assert.Equal(new byte[2], cold.Read(SelectedWord, 2));
        Assert.Equal(JsonSerializer.Serialize(written), JsonSerializer.Serialize(restored));
    }

    [Fact]
    public void MarkUnknown_partial_writes_and_rebase_preserve_holes_in_absolute_zero_ranges()
    {
        NativeBattleFileStaging current = NativeBattleFileStaging.FromColdProcess().MarkUnknown(SelectedWord, 4).Write(SelectedWord, [0xA7]);
        NativeBattleFileStaging restored = Restore(current.Rebase(ZeroStart - 32));
        (byte[] bytes, bool[] known) = restored.ReadSnapshot(SelectedWord - 1, 6);
        Assert.Equal(new byte[] { 0, 0xA7, 0, 0, 0, 0 }, bytes);
        Assert.Equal(new[] { true, true, false, false, false, true }, known);
        Assert.Throws<NotSupportedException>(() => restored.Read(SelectedWord, 4));
        Assert.Equal(new byte[] { 0xA7 }, restored.Read(SelectedWord, 1));
        Assert.Equal(new byte[2], restored.Read(ZeroStart, 2));
        Assert.DoesNotContain(restored.LoaderZeroRanges, range => (ulong)range.Address <= SelectedWord + 1 && (ulong)range.Address + (uint)range.Length > SelectedWord + 1);
    }

    [Fact]
    public void Overlapping_copies_transfer_zero_knownness_and_holes_instead_of_healing_them()
    {
        NativeBattleFileStaging source = NativeBattleFileStaging.FromColdProcess().Write(SelectedWord, [1, 2, 3, 4, 5, 6]).MarkUnknown(SelectedWord + 2, 1);
        NativeBattleFileStaging current = source.CopyMemory(SelectedWord, SelectedWord + 1, 6);
        (byte[] bytes, bool[] known) = current.ReadSnapshot(SelectedWord, 7);
        Assert.Equal(new byte[] { 1, 1, 2, 0, 4, 5, 6 }, bytes);
        Assert.Equal(new[] { true, true, true, false, true, true, true }, known);
        current = current.CopyMemory(SelectedWord + 8, ZeroEnd + 32, 4);
        Assert.Equal(new byte[4], current.Read(ZeroEnd + 32, 4));
        Assert.Throws<NotSupportedException>(() => current.Read(ZeroEnd + 31, 1));
        current = current.CopyMemory(ZeroEnd + 64, SelectedWord + 16, 2);
        Assert.Throws<NotSupportedException>(() => current.Read(SelectedWord + 16, 2));
        NativeBattleFileStaging restored = Restore(current);
        Assert.Equal(known, restored.ReadSnapshot(SelectedWord, 7).Known);
        Assert.Throws<NotSupportedException>(() => restored.Read(SelectedWord + 16, 2));
        Assert.Equal(new byte[] { 1, 2 }, source.Read(SelectedWord, 2));
    }

    [Fact]
    public void Transaction_freeze_keeps_zero_pages_current_writes_and_explicit_unknown_bytes()
    {
        NativeBattleFileStaging source = NativeBattleFileStaging.FromColdProcess();
        using NativeBattleFileStagingTransaction transaction = new(source);
        Assert.Equal(new byte[4], transaction.View.Read(SelectedWord, 4));
        NativeBattleFileStaging readOnly = transaction.Freeze(transaction.View);
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(readOnly));
        const uint crossing = 0x255FFFE;
        NativeBattleFileStaging current = transaction.View.Write(crossing, [1, 2, 3, 4]).MarkUnknown(crossing + 2, 1).Write(crossing + 3, [9]);
        Assert.Equal(new[] { true, true, false, true }, current.ReadSnapshot(crossing, 4).Known);
        NativeBattleFileStaging first = transaction.Freeze(current);
        first.Validate();
        Assert.Equal(new byte[] { 1, 2 }, first.Read(crossing, 2));
        Assert.Throws<NotSupportedException>(() => first.Read(crossing + 2, 1));
        Assert.Equal(new byte[] { 9 }, first.Read(crossing + 3, 1));
        Assert.Equal(new byte[2], first.Read(crossing + 4, 2));
        current = current.Write(crossing + 2, [8]);
        NativeBattleFileStaging second = transaction.Freeze(current);
        Assert.Equal(new byte[] { 1, 2, 8, 9 }, second.Read(crossing, 4));
        Assert.Throws<NotSupportedException>(() => first.Read(crossing + 2, 1));
        Assert.Throws<NotSupportedException>(() => Restore(first).Read(crossing + 2, 1));
        Assert.Equal(new byte[4], source.Read(crossing, 4));
    }

    [Fact]
    public void Older_json_without_zero_provenance_retains_only_explicit_current_writes()
    {
        JsonObject old = JsonNode.Parse(JsonSerializer.Serialize(NativeBattleFileStaging.FromColdProcess().Write(SelectedWord, [7])))!.AsObject();
        Assert.True(old.Remove("LoaderZeroRanges"));
        NativeBattleFileStaging restored = JsonSerializer.Deserialize<NativeBattleFileStaging>(old.ToJsonString())!;
        restored.Validate();
        Assert.Empty(restored.LoaderZeroRanges);
        Assert.Equal(new byte[] { 7 }, restored.Read(SelectedWord, 1));
        Assert.Throws<NotSupportedException>(() => restored.Read(SelectedWord + 1, 1));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Quarter_depth_word_store_preserves_actual_high_zero_then_unknownness(bool transactional)
    {
        NativeBattleFileStaging cold = NativeBattleFileStaging.FromColdProcess();
        using NativeBattleFileStagingTransaction? transaction = transactional ? new(cold) : null;
        NativeBattleFileStaging current = transaction?.View ?? cold;
        uint input = NativeBattleTemporaryMemory.Address + 0x44;
        current = current.Write(input, BitConverter.GetBytes((ushort)0x10EA));
        NativeBattleEffectArena first = NativeBattleTemporaryMemory.ReadView(current, NativeBattleTemporaryMemory.Address, 76);
        Assert.Equal(0x10EAu, BinaryPrimitives.ReadUInt32LittleEndian(first.Read(0x44, 4)));
        current = current.MarkUnknown(input + 2, 2).Write(input, BitConverter.GetBytes((ushort)13));
        NativeBattleEffectArena temporary = NativeBattleTemporaryMemory.ReadView(current, NativeBattleTemporaryMemory.Address, 76);
        Assert.Equal((ushort)13, BinaryPrimitives.ReadUInt16LittleEndian(temporary.Read(0x44, 2)));
        Assert.Throws<NotSupportedException>(() => temporary.Read(0x46, 2));
        current = NativeBattleTemporaryMemory.PublishView(current, NativeBattleTemporaryMemory.Address, temporary, 76);
        NativeBattleFileStaging frozen = transaction is null ? current : transaction.Freeze(current);
        NativeBattleFileStaging restored = Restore(frozen);
        Assert.Equal((ushort)13, BinaryPrimitives.ReadUInt16LittleEndian(restored.Read(input, 2)));
        Assert.Throws<NotSupportedException>(() => restored.Read(input + 2, 2));
        Assert.Equal(new[] { true, true, false, false }, restored.ReadSnapshot(input, 4).Known);
    }

    private static NativeBattleFileStaging Restore(NativeBattleFileStaging staging) => JsonSerializer.Deserialize<NativeBattleFileStaging>(JsonSerializer.Serialize(staging))!;
}
