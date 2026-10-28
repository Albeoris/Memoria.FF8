using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleVramWorkingStorageTests
{
    [Fact]
    public void First_zero_upload_publishes_coverage_and_repeated_known_zero_keeps_the_version()
    {
        NativeBattleEffectTextureState cold = NativeBattleEffectTextureState.FromColdProcess();
        NativeBattleEffectTextureState first = cold.Upload45BD30(4, 2, 1, 1, [0, 0]);
        Assert.NotSame(cold.VramPublicationIdentity, first.VramPublicationIdentity);
        NativeBattleEffectMemoryRegion region = Assert.Single(NativeBattleStorageSnapshots.Capture(first).VramRegions);
        Assert.Equal(2 * (2 * 1024 + 4), region.Offset);
        Assert.Equal(new byte[] { 0, 0 }, region.Data);

        NativeBattleEffectTextureState repeated = first.Upload45BD30(4, 2, 1, 1, [0, 0]);
        Assert.Same(first.VramPublicationIdentity, repeated.VramPublicationIdentity);
        Assert.Equal((ushort)0, Word(repeated, 4, 2));
        Assert.Empty(NativeBattleStorageSnapshots.Capture(cold).VramRegions);
    }

    [Fact]
    public void Equal_known_upload_still_performs_the_native_write_and_emits_its_upload()
    {
        NativeBattleEffectTextureState state = NativeBattleEffectTextureState.FromColdProcess().Upload45BD30(0, 0, 1, 1, [11, 0]);
        Traffic traffic = new();
        using IDisposable scope = NativeBattleTelemetry.BeginScope(traffic);
        NativeBattleEffectTextureDrain repeated = state.EnqueueRectangle505DF0(new(0, 0, 1, 1), new("same", [11, 0])).Drain505D20();

        Assert.Same(state.VramPublicationIdentity, repeated.State.VramPublicationIdentity);
        Assert.Equal(new byte[] { 11, 0 }, Assert.Single(repeated.Uploads).Bytes);
        Assert.Equal(2, traffic.Bytes[(1005, NativeBattleTrafficKind.CpuCopyPayload)]);
        Assert.Equal((ushort)11, Word(repeated.State, 0, 0));
    }

    [Fact]
    public void Equal_zero_bytes_that_were_unknown_are_published_as_known()
    {
        NativeBattleVramOwner owner = new([new(0, [0, 0])]);
        owner.Arena.MarkUnknown(0, 2);
        using NativeBattleVramOwner.Mutation mutation = owner.BeginMutation(owner.Initial);
        mutation.Write(0, [0, 0]);
        NativeBattleVramVersion version = mutation.Commit();

        Assert.NotSame(owner.Initial, version);
        Assert.Equal((ushort)0, owner.Arena.ReadUInt16(0));
        Assert.Equal((ushort)0, version.ReadWord(0));
    }

    [Fact]
    public void Equality_uses_current_working_bytes_after_earlier_writes_in_the_same_mutation()
    {
        NativeBattleVramOwner owner = new([new(0, [1, 0])]);
        using NativeBattleVramOwner.Mutation mutation = owner.BeginMutation(owner.Initial);
        mutation.Write(0, [2, 0]);
        mutation.Write(0, [2, 0]);
        mutation.Write(0, [1, 0]);
        NativeBattleVramVersion version = mutation.Commit();

        Assert.Equal((ushort)1, version.ReadWord(0));
        Assert.Equal((ushort)1, owner.Arena.ReadUInt16(0));
    }

    [Fact]
    public void Equal_pixels_do_not_preserve_overwritten_origin_or_copy_metadata()
    {
        NativeBattleTextureOrigin origin = new("known.tim", 0, NativeBattleTextureRole.Pixels, 16, 1, 1);
        NativeBattleEffectTextureState state = NativeBattleEffectTextureState.FromColdProcess().Upload45BD30(0, 0, 1, 1, [11, 0]) with
        {
            LiveTextureSources = [new(origin, 0, 0, 1, 1)],
            TextureRegistrations = NativeBattleTextureCopyHistory.Upload(null, 0, 0, 1, 1, origin)
        };
        NativeBattleEffectTextureDrain copied = state.EnqueueCopy505EB0(0, 0, 1, 1, 0, 0).Drain505D20();
        Assert.Same(state.VramPublicationIdentity, copied.State.VramPublicationIdentity);
        Assert.True(Assert.Single(copied.Uploads).Copied);
        Assert.Equal(0, Assert.Single(copied.State.TextureRegistrations!.Value).CurrentFrame);

        NativeBattleEffectTextureState overwritten = copied.State.Upload45BD30(0, 0, 1, 1, [11, 0]);
        Assert.Same(state.VramPublicationIdentity, overwritten.VramPublicationIdentity);
        Assert.Empty(overwritten.LiveTextureSources!.Value);
        Assert.Empty(overwritten.TextureRegistrations!.Value);
    }

    [Fact]
    public void Outer_rollback_restores_bytes_and_publication_after_changed_and_equal_nested_uploads()
    {
        NativeBattleVramOwner owner = new([new(0, [1, 0])]);
        owner.Arena.Working.BeginCheckpoint();
        using (NativeBattleVramOwner.Mutation changed = owner.BeginMutation(owner.Initial))
        {
            changed.Write(0, [2, 0]);
            changed.Commit();
        }
        NativeBattleVramVersion accepted = owner.Current;
        using (NativeBattleVramOwner.Mutation equal = owner.BeginMutation(accepted))
        {
            equal.Write(0, [2, 0]);
            Assert.Same(accepted, equal.Commit());
        }
        owner.Arena.Working.RollbackCheckpoint();

        Assert.Same(owner.Initial, owner.Current);
        Assert.Equal((ushort)1, owner.Arena.ReadUInt16(0));
        using NativeBattleVramOwner.Mutation next = owner.BeginMutation(owner.Initial);
        next.Write(0, [1, 0]);
        Assert.Same(owner.Initial, next.Commit());
    }

    [Fact]
    public void Equal_write_does_not_bypass_historical_completed_or_out_of_bounds_writer_checks()
    {
        NativeBattleVramOwner owner = new([new(0, [1, 0])]);
        using NativeBattleVramOwner.Mutation mutation = owner.BeginMutation(owner.Initial);
        Assert.Throws<InvalidDataException>(() => mutation.Write(-1, [1, 0]));
        Assert.Throws<InvalidDataException>(() => mutation.Write(NativeBattleEffectArena.Length, [0]));
        mutation.Write(0, [2, 0]);
        mutation.Commit();
        Assert.Throws<InvalidOperationException>(() => mutation.Write(0, [2, 0]));
        Assert.Throws<InvalidOperationException>(() => mutation.Commit());
        Assert.Throws<InvalidOperationException>(() => owner.BeginMutation(owner.Initial));
        NativeBattleEffectTextureState frozen = NativeBattleStorageSnapshots.Capture(NativeBattleEffectTextureState.FromColdProcess());
        Assert.Throws<InvalidOperationException>(() => frozen.Upload45BD30(0, 0, 1, 1, [0, 0]));
    }

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

    [Fact]
    public void Queue_metadata_keeps_publication_identity_and_sparse_word_reads_match_bulk_reads()
    {
        NativeBattleEffectTextureState state = NativeBattleEffectTextureState.FromColdProcess() with { VramRegions = [new(3, [0x12]), new(4095, [0x34, 0x56])] };
        NativeBattleEffectTextureState queued = state.EnqueueRectangle505DF0(new(0, 0, 1, 1), new("pixel", [1, 0]));
        Assert.Same(state.VramPublicationIdentity, queued.VramPublicationIdentity);
        Assert.Equal((ushort)0x1200, state.ReadVramWord(1, 0));
        Assert.Equal((ushort)0x3400, state.ReadVramWord(1023, 1));
        Assert.Equal((ushort)0x0056, state.ReadVramWord(0, 2));
        NativeBattleEffectTextureState changed = queued.Drain505D20().State;
        Assert.NotSame(state.VramPublicationIdentity, changed.VramPublicationIdentity);
        Assert.Equal((ushort)0x1200, state.ReadVramWord(1, 0));
        Assert.Equal((ushort)1, changed.ReadVramWord(0, 0));
    }

    [Fact]
    public void Bounded_version_reads_account_for_clear_and_actual_initial_and_page_copies()
    {
        NativeBattleEffectTextureState original = NativeBattleEffectTextureState.FromColdProcess() with { VramRegions = [new(0, [11, 0])] };
        NativeBattleEffectTextureState current = original.Upload45BD30(0, 0, 1, 1, [12, 0]);
        Traffic traffic = new();
        using IDisposable scope = NativeBattleTelemetry.BeginScope(traffic);
        Span<byte> bytes = stackalloc byte[2];
        current.ReadVramInto(0, bytes);
        Assert.Equal((ushort)12, BinaryPrimitives.ReadUInt16LittleEndian(bytes));
        Assert.Equal(2, traffic.Bytes[(1242, NativeBattleTrafficKind.ZeroFill)]);
        Assert.Equal(2, traffic.Bytes[(1243, NativeBattleTrafficKind.CpuCopyPayload)]);
        Assert.Equal(2, traffic.Bytes[(1244, NativeBattleTrafficKind.CpuCopyPayload)]);
    }

    [Fact]
    public void Current_owner_does_not_retain_released_intermediate_presentation_versions()
    {
        (NativeBattleEffectTextureState current, WeakReference[] released) = CreateReleasedVersions();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.All(released, reference => Assert.False(reference.IsAlive));
        Assert.Equal((ushort)33, current.ReadVramWord(0, 0));
        GC.KeepAlive(current);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static (NativeBattleEffectTextureState Current, WeakReference[] Released) CreateReleasedVersions()
    {
        NativeBattleEffectTextureState state = NativeBattleEffectTextureState.FromColdProcess().Upload45BD30(0, 0, 1, 1, [1, 0]);
        WeakReference[] released = new WeakReference[32];
        for (int index = 0; index < released.Length; index++)
        {
            released[index] = new(state.VramPublicationIdentity);
            state = state.Upload45BD30(0, 0, 1, 1, [(byte)(index + 2), 0]);
        }
        return (state, released);
    }

    private sealed class Traffic : INativeBattleTelemetrySink
    {
        internal Dictionary<(int Site, NativeBattleTrafficKind Kind), long> Bytes { get; } = [];

        public void Record(int siteId, NativeBattleTrafficKind kind, long payloadBytes)
        {
            Bytes[(siteId, kind)] = Bytes.GetValueOrDefault((siteId, kind)) + payloadBytes;
        }
    }

    private static ushort Word(NativeBattleEffectTextureState state, int x, int y)
    {
        Span<byte> bytes = stackalloc byte[2];
        state.ReadVramInto(2 * (y * 1024 + x), bytes);
        return BinaryPrimitives.ReadUInt16LittleEndian(bytes);
    }
}
