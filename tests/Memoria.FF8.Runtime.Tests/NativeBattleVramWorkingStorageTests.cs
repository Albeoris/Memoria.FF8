using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleVramWorkingStorageTests
{
    [Fact]
    public void Later_uploads_keep_prepared_versions_and_detached_history_stable()
    {
        NativeBattleEffectTextureState before = NativeBattleEffectTextureState.FromColdProcess().Upload45BD30(4, 2, 1, 1, [1, 2]);
        NativeBattleEffectTextureState captured = NativeBattleStorageSnapshots.Capture(before);
        NativeBattleEffectTextureState after = before.Upload45BD30(4, 2, 1, 1, [3, 4]);
        Assert.Equal((ushort)0x0201, Word(before, 4, 2));
        Assert.Equal((ushort)0x0403, Word(after, 4, 2));
        Assert.Equal((ushort)0x0201, Word(captured, 4, 2));
        Assert.Throws<InvalidOperationException>(() => before.Upload45BD30(4, 2, 1, 1, [5, 6]));
        Assert.Throws<InvalidOperationException>(() => captured.Upload45BD30(4, 2, 1, 1, [5, 6]));
        Assert.Throws<InvalidOperationException>(() => JsonSerializer.Serialize(after));
        string json = JsonSerializer.Serialize(captured);
        Assert.Contains("VramRegions", json);
        Assert.DoesNotContain("pages", json, StringComparison.OrdinalIgnoreCase);
        NativeBattleEffectTextureState first = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffectTextureState>(json)!);
        NativeBattleEffectTextureState second = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffectTextureState>(json)!);
        first = first.Upload45BD30(4, 2, 1, 1, [7, 8]);
        Assert.Equal((ushort)0x0807, Word(first, 4, 2));
        Assert.Equal((ushort)0x0201, Word(second, 4, 2));
        Assert.Equal(json, JsonSerializer.Serialize(captured));
    }

    [Fact]
    public void Failed_drain_rolls_back_bytes_and_publication_cursor_before_another_upload()
    {
        NativeBattleEffectTextureState before = NativeBattleEffectTextureState.FromColdProcess().Upload45BD30(0, 0, 1, 1, [1, 0]);
        NativeBattleEffectTextureState queued = before.EnqueueRectangle505DF0(new(0, 0, 1, 1), new("rectangle", [2, 0]));
        byte[] bad = new byte[24];
        BinaryPrimitives.WriteUInt32LittleEndian(bad.AsSpan(8), 12);
        BinaryPrimitives.WriteInt16LittleEndian(bad.AsSpan(16), 1);
        BinaryPrimitives.WriteInt16LittleEndian(bad.AsSpan(18), 1);
        queued = queued with { Slots = queued.Slots.SetItem(1, new(new("invalid-block", bad.ToImmutableArray()), 0)), Count = 2 };
        Assert.Throws<InvalidDataException>(() => queued.Drain505D20());
        Assert.Equal((ushort)1, Word(before, 0, 0));
        NativeBattleEffectTextureState recovered = before.Upload45BD30(0, 0, 1, 1, [3, 0]);
        Assert.Equal((ushort)3, Word(recovered, 0, 0));
    }

    [Fact]
    public void Automatic_memory_publication_accepts_vram_prefix_and_rolls_back_only_suffix()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        NativeBattleEffectTextureState original = memory.Textures!;
        NativeBattleEffectTextureState? accepted = null;
        Assert.Throws<InvalidOperationException>((Action)(() =>
        {
            using NativeBattleTaskOperation operation = new(memory, []);
            accepted = original.Upload45BD30(0, 0, 1, 1, [11, 0]);
            NativeBattleTaskOperation.AcceptPublishedMemory(memory with { Textures = accepted });
            accepted.Upload45BD30(0, 0, 1, 1, [12, 0]);
            throw new InvalidOperationException("Unpublished suffix failed.");
        }));
        NativeBattleEffectTextureState next = accepted!.Upload45BD30(0, 0, 1, 1, [13, 0]);
        Assert.Equal((ushort)11, Word(accepted, 0, 0));
        Assert.Equal((ushort)13, Word(next, 0, 0));
        Assert.Equal((ushort)0, Word(original, 0, 0));
    }

    [Fact]
    public void Warm_small_upload_does_not_rehydrate_or_export_the_megabyte_bank()
    {
        NativeBattleEffectTextureState state = NativeBattleEffectTextureState.FromColdProcess().Upload45BD30(1023, 511, 1, 1, [1, 0]);
        long before = GC.GetAllocatedBytesForCurrentThread();
        state = state.Upload45BD30(1023, 511, 1, 1, [2, 0]);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated < 131072, $"A warm bounded upload allocated {allocated} bytes.");
        Assert.Equal((ushort)2, Word(state, 1023, 511));
    }

    private static ushort Word(NativeBattleEffectTextureState state, int x, int y)
    {
        Span<byte> bytes = stackalloc byte[2];
        state.ReadVramInto(2 * (y * 1024 + x), bytes);
        return BinaryPrimitives.ReadUInt16LittleEndian(bytes);
    }
}
