using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWaterTemporaryFrameTests
{
    [Fact]
    public void BulkPrivateReadsPreserveCapturedParentLanesHiddenBackingAndNestedPublication()
    {
        NativeBattleWaterTemporaryFrame live = Create();
        live.WriteWithKnownness(0, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, new bool[] { true, false, true, false, true, false, true, false });
        using NativeBattleWaterTemporaryFrame.PrivateWrite outer = live.BeginPrivateWrite();
        outer.View.WriteByte(0, 20);
        live.WriteWithKnownness(2, new byte[] { 90, 91 }, new bool[] { false, true });
        using NativeBattleWaterTemporaryFrame.PrivateWrite child = outer.View.BeginPrivateWrite();
        child.View.WriteByte(4, 50);
        outer.View.WriteByte(6, 60);
        byte[] bytes = new byte[8];
        bool[] known = new bool[8];
        child.View.ReadWithKnownness(0, bytes, known);
        Assert.Equal(new byte[] { 20, 2, 3, 4, 50, 6, 7, 8 }, bytes);
        Assert.Equal(new bool[] { true, false, true, false, true, false, true, false }, known);
        byte[] unchanged = Enumerable.Repeat((byte)99, 8).ToArray();
        Assert.Throws<NotSupportedException>(() => child.View.ReadInto(0, unchanged));
        Assert.All(unchanged, item => Assert.Equal(99, item));

        Assert.Same(outer.View, child.Publish());
        outer.View.ReadWithKnownness(0, bytes, known);
        Assert.Equal(new byte[] { 20, 2, 3, 4, 50, 6, 7, 8 }, bytes);
        Assert.Equal(new bool[] { true, false, true, false, true, false, true, false }, known);
        Assert.Throws<ObjectDisposedException>(() => child.View.ReadWithKnownness(0, bytes, known));
        Assert.Same(live, outer.Publish());
        live.ReadWithKnownness(0, bytes, known);
        Assert.Equal(new byte[] { 20, 2, 3, 4, 50, 6, 7, 8 }, bytes);
        Assert.Equal(new bool[] { true, false, true, false, true, false, true, false }, known);
        Assert.Throws<ObjectDisposedException>(() => outer.View.ReadWithKnownness(0, bytes, known));
    }

    [Fact]
    public void Capture_preserves_in_memory_aliases_and_standalone_json_has_independent_restores()
    {
        NativeBattleWaterTemporaryFrame live = Create();
        live.WriteUInt32(4, 0x12345678).MarkUnknown(5, 1);
        var captured = NativeBattleStorageSnapshots.Capture(new Pair(live, live with { Address = live.Address + 0x200 }));
        string json = JsonSerializer.Serialize(captured.Left);
        Assert.Contains("\"Address\"", json);
        Assert.Contains("\"Bytes\"", json);
        Assert.Contains("\"Known\"", json);
        Assert.DoesNotContain("StorageReference", json);
        Assert.Throws<InvalidOperationException>(() => JsonSerializer.Serialize(live));
        Assert.Throws<InvalidOperationException>(() => captured.Left.WriteByte(4, 0));
        live.WriteUInt32(4, 0xFFFFFFFF);
        Assert.Equal(0x78, captured.Left.Bytes[4]);
        Assert.False(captured.Left.Known[5]);
        Pair first = NativeBattleStorageSnapshots.Restore(captured);
        Pair second = NativeBattleStorageSnapshots.Restore(captured);
        first.Left.WriteByte(4, 0xAA);
        Assert.Equal(0xAA, first.Right.ReadByte(4));
        Assert.Equal(0x78, second.Left.ReadByte(4));
        Assert.Equal(0x78, captured.Left.Bytes[4]);
        NativeBattleWaterTemporaryFrame decoded = JsonSerializer.Deserialize<NativeBattleWaterTemporaryFrame>(json)!;
        NativeBattleWaterTemporaryFrame restored = NativeBattleStorageSnapshots.Restore(decoded);
        NativeBattleWaterTemporaryFrame otherRestore = NativeBattleStorageSnapshots.Restore(decoded);
        Assert.Equal(captured.Left.Bytes.ToArray(), NativeBattleStorageSnapshots.Capture(restored).Bytes.ToArray());
        Assert.Equal(captured.Left.Known.ToArray(), NativeBattleStorageSnapshots.Capture(restored).Known.ToArray());
        restored.WriteByte(4, 0xBB);
        Assert.Equal(0x78, otherRestore.ReadByte(4));
        Assert.Equal(0x78, decoded.Bytes[4]);
    }

    [Fact]
    public void Private_writes_preserve_open_values_and_publish_the_complete_view_after_parent_changes()
    {
        NativeBattleWaterTemporaryFrame live = Create();
        using NativeBattleWaterTemporaryFrame.PrivateWrite writes = live.BeginPrivateWrite();
        writes.View.WriteUInt16(4, 0xA1B2);
        live.WriteByte(10, 0xCC).MarkUnknown(11, 1);
        Assert.Equal(0, writes.View.ReadByte(10));
        Assert.True(writes.View.IsKnown(11, 1));
        Assert.Equal(0xCC, live.ReadByte(10));
        NativeBattleWaterTemporaryFrame before = NativeBattleStorageSnapshots.Capture(writes.View);
        Assert.Same(live, writes.Publish());
        NativeBattleWaterTemporaryFrame after = NativeBattleStorageSnapshots.Capture(live);
        Assert.Equal(before.Bytes.ToArray(), after.Bytes.ToArray());
        Assert.Equal(before.Known.ToArray(), after.Known.ToArray());
        Assert.Equal((ushort)0xA1B2, live.ReadUInt16(4));
        Assert.Throws<ObjectDisposedException>(() => writes.View.ReadByte(0));
    }

    [Fact]
    public void Nested_private_publish_and_failure_keep_external_parent_changes_at_their_own_boundary()
    {
        NativeBattleWaterTemporaryFrame live = Create();
        NativeBattleWaterTemporaryFrame captured = NativeBattleStorageSnapshots.Capture(live);
        Assert.Throws<InvalidOperationException>((Action)(() =>
        {
            using NativeBattleWaterTemporaryFrame.PrivateWrite outer = live.BeginPrivateWrite();
            outer.View.WriteByte(1, 0x11);
            using (NativeBattleWaterTemporaryFrame.PrivateWrite child = outer.View.BeginPrivateWrite())
            {
                child.View.WriteByte(2, 0x22);
                outer.View.WriteByte(3, 0x33);
                Assert.Equal(0, child.View.ReadByte(3));
                captured = NativeBattleStorageSnapshots.Capture(child.View);
                Assert.Same(outer.View, child.Publish());
            }
            Assert.Equal(0x11, outer.View.ReadByte(1));
            Assert.Equal(0x22, outer.View.ReadByte(2));
            Assert.Equal(0, outer.View.ReadByte(3));
            live.WriteByte(4, 0x44);
            throw new InvalidOperationException("Failed temporary producer.");
        }));
        Assert.Equal(0, live.ReadByte(1));
        Assert.Equal(0, live.ReadByte(2));
        Assert.Equal(0x44, live.ReadByte(4));
        Assert.Equal(0x22, captured.Bytes[2]);
        Assert.Equal(0, captured.Bytes[3]);
    }

    [Fact]
    public void Mixed_stores_and_overlap_copy_preserve_hidden_bytes_and_unknown_boundaries()
    {
        NativeBattleWaterTemporaryFrame live = Create();
        live.WriteWithKnownness(0, new byte[] { 1, 2, 3, 4, 5, 6 }, new bool[] { true, false, true, false, true, false });
        Assert.Same(live, live.Copy(0, 2, 6));
        NativeBattleWaterTemporaryFrame captured = NativeBattleStorageSnapshots.Capture(live);
        Assert.Equal(new byte[] { 1, 2, 1, 2, 3, 4, 5, 6 }, captured.Bytes.AsSpan(0, 8).ToArray());
        Assert.Equal(new bool[] { true, false, true, false, true, false, true, false }, captured.Known.AsSpan(0, 8).ToArray());
        live.SetKnownness(3, new bool[] { true, false });
        Assert.Equal(2, live.ReadByte(3));
        Assert.Throws<NotSupportedException>(() => live.ReadByte(4));
        Assert.Throws<InvalidDataException>(() => live.Copy(380, 0, 5));
        Assert.Throws<ArgumentException>(() => live.WriteWithKnownness(0, new byte[] { 9 }, ReadOnlySpan<bool>.Empty));
        Assert.Equal(captured.Bytes.ToArray(), NativeBattleStorageSnapshots.Capture(live).Bytes.ToArray());
    }

    private sealed record Pair(NativeBattleWaterTemporaryFrame Left, NativeBattleWaterTemporaryFrame Right);
    private static NativeBattleWaterTemporaryFrame Create() => new(0x100000, new byte[384].ToImmutableArray(), Enumerable.Repeat(true, 384).ToImmutableArray());
}
