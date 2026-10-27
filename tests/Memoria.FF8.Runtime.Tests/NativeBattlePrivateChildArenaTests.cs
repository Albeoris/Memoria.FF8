namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePrivateChildArenaTests
{
    [Fact]
    public void Child_reads_pending_parent_and_accepts_only_its_published_prefix()
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.CreateUnknown(64).Write(0, new byte[64]);
        using NativeBattleEffectArenaTransaction parent = new(source);
        parent.View.Write(4, [1, 2, 3, 4]);
        using (NativeBattleEffectArenaTransaction child = NativeBattleEffectArenaTransaction.CreatePrivateChild(parent.View))
        {
            Assert.Equal(0x04030201u, child.View.ReadUInt32(4));
            child.View.Write(4, [9, 8]);
            Assert.Equal(0x04030201u, parent.View.ReadUInt32(4));
            Assert.Equal(0u, source.Working.ReadUInt32(4));
            child.Freeze();
            Assert.Equal(0x04030809u, parent.View.ReadUInt32(4));
            Assert.Equal(0u, source.Working.ReadUInt32(4));
            child.View.Write(4, [77]);
        }
        Assert.Equal((byte)9, parent.View.ReadByte(4));
        parent.Freeze();
        Assert.Equal(0x04030809u, source.Working.ReadUInt32(4));
    }

    [Fact]
    public void Child_acceptance_enlists_in_existing_parent_checkpoint_including_partial_pointer_overlap()
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.CreateUnknown(64).Write(0, new byte[64]);
        using NativeBattleEffectArenaTransaction parent = new(source);
        parent.View.WriteResourcePointer(16, new(null, null) { CpuRow = 3 });
        parent.BeginCheckpoint();
        using (NativeBattleEffectArenaTransaction child = NativeBattleEffectArenaTransaction.CreatePrivateChild(parent.View))
        {
            Assert.Equal(3, child.View.ReadResourcePointer(16)!.CpuRow);
            child.View.Write(17, [44]);
            Assert.False(child.View.TryGetResourcePointer(16, out _));
            Assert.Equal(3, parent.View.ReadResourcePointer(16)!.CpuRow);
            child.Freeze();
            Assert.False(parent.View.TryGetResourcePointer(16, out _));
            Assert.Equal((byte)44, parent.View.ReadByte(17));
        }
        parent.RollbackCheckpoint();
        Assert.Equal(3, parent.View.ReadResourcePointer(16)!.CpuRow);
        Span<byte> bytes = stackalloc byte[4];
        Span<bool> known = stackalloc bool[4];
        parent.View.ReadWithKnownness(16, bytes, known);
        Assert.False(known.Contains(true));
    }

    [Fact]
    public void Resource_window_preserves_first_selection_across_ancestor_writes_rollback_and_child_acceptance()
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.CreateUnknown(64).Write(0, new byte[64]);
        using NativeBattleEffectArenaTransaction parent = new(source);
        parent.BeginCheckpoint();
        parent.View.Write(8, [11]);
        using NativeBattleEffectArenaTransaction child = NativeBattleEffectArenaTransaction.CreatePrivateChild(parent.View);
        using NativeBattleReadWindow window = child.View.OpenReadWindow(8, 4, 4);
        Assert.Equal((byte)11, window.ReadByte(0));
        parent.View.Write(8, [22]);
        child.View.Write(9, [33]);
        child.Freeze();
        Assert.Equal((byte)11, window.ReadByte(0));
        Assert.Equal((byte)0, window.ReadByte(1));
        parent.RollbackCheckpoint();
        Assert.Equal((byte)11, window.ReadByte(0));
        Assert.Equal((byte)0, window.ReadByte(1));
        Assert.Equal((byte)0, parent.View.ReadByte(8));
    }

    [Fact]
    public void Failed_child_merge_restores_parent_pending_bytes_unknowns_metadata_and_outer_undo()
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.CreateUnknown(64).Write(0, new byte[64]);
        using NativeBattleEffectArenaTransaction parent = new(source);
        parent.View.Write(0, [5]);
        parent.View.WriteResourcePointer(16, new(null, null) { CpuRow = 2 });
        parent.BeginCheckpoint();
        using NativeBattleEffectArenaTransaction child = NativeBattleEffectArenaTransaction.CreatePrivateChild(parent.View);
        child.View.Write(0, [6]);
        child.View.Write(17, [7]);
        FailSecondStore sink = new();
        using (NativeBattleTelemetry.BeginScope(sink))
        {
            Assert.Throws<InvalidOperationException>(() => child.Freeze());
        }
        Assert.Equal((byte)5, parent.View.ReadByte(0));
        Assert.Equal(2, parent.View.ReadResourcePointer(16)!.CpuRow);
        parent.View.Write(0, [8]);
        parent.RollbackCheckpoint();
        Assert.Equal((byte)5, parent.View.ReadByte(0));
        child.Freeze();
        Assert.Equal((byte)6, parent.View.ReadByte(0));
        Assert.Equal((byte)7, parent.View.ReadByte(17));
    }

    [Fact]
    public void Disposed_parent_invalidates_child_and_its_resource_window()
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.CreateUnknown(32).Write(0, new byte[32]);
        NativeBattleEffectArenaTransaction parent = new(source);
        using NativeBattleEffectArenaTransaction child = NativeBattleEffectArenaTransaction.CreatePrivateChild(parent.View);
        using NativeBattleReadWindow window = child.View.OpenReadWindow(0, 4);
        parent.Dispose();
        Assert.Throws<ObjectDisposedException>(() => child.View.ReadByte(0));
        Assert.Throws<ObjectDisposedException>(() => window.ReadByte(0));
    }

    [Fact]
    public void Ancestor_descriptor_added_after_private_byte_store_is_masked_and_invalidated_on_acceptance()
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.CreateUnknown(32).Write(0, new byte[32]);
        using NativeBattleEffectArenaTransaction parent = new(source);
        using NativeBattleEffectArenaTransaction child = NativeBattleEffectArenaTransaction.CreatePrivateChild(parent.View);
        child.View.Write(17, [44]);
        parent.View.WriteResourcePointer(16, new(null, 0x12345678));
        Assert.True(parent.View.TryGetResourcePointer(16, out _));
        Assert.False(child.View.TryGetResourcePointer(16, out _));
        Assert.DoesNotContain(child.View.EnumerateResourcePointers(), item => item.Key == 16);
        Assert.Equal(0x12342C78u, child.View.ReadUInt32(16));
        child.Freeze();
        Assert.False(parent.View.TryGetResourcePointer(16, out _));
        Assert.Equal(0x12342C78u, parent.View.ReadUInt32(16));
    }

    private sealed class FailSecondStore : INativeBattleTelemetrySink
    {
        private int stores;
        public void Record(int siteId, NativeBattleTrafficKind kind, long payloadBytes)
        {
            if (siteId == 1005 && kind == NativeBattleTrafficKind.CpuCopyPayload && ++stores == 2)
            {
                throw new InvalidOperationException("Injected second private child store failure.");
            }
        }
    }
}
