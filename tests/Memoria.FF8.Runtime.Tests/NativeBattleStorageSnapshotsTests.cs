using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleStorageSnapshotsTests
{
    [Fact]
    public void Capture_and_two_restores_preserve_aliases_without_sharing_writable_authority()
    {
        byte[] authority = [1, 2, 3];
        StorageNode first = new(authority);
        StorageNode otherView = new(authority);
        ImmutableDictionary<string, StorageNode> names = ImmutableDictionary.Create<string, StorageNode>(StringComparer.OrdinalIgnoreCase, ReferenceEqualityComparer.Instance).Add("First", first).Add("Other", otherView);
        Graph source = new(first, first, [first, otherView], names, new StorageNode[,] { { first, otherView } }, new("stable"));
        Graph captured = NativeBattleStorageSnapshots.Capture(source);
        Assert.NotSame(source, captured);
        Assert.IsType<FrozenStorageNode>(captured.First);
        Assert.Same(captured.First, captured.Retained);
        Assert.Same(captured.First, captured.Items[0]);
        Assert.Same(captured.First, captured.Names["FIRST"]);
        Assert.Same(captured.First, captured.Grid[0, 0]);
        Assert.Same(captured.First.Authority, captured.Items[1].Authority);
        Assert.NotSame(authority, captured.First.Authority);
        Assert.Same(names.KeyComparer, captured.Names.KeyComparer);
        Assert.Same(names.ValueComparer, captured.Names.ValueComparer);
        Assert.Same(source.Leaf, captured.Leaf);
        first.Write(9);
        Assert.Equal(1, captured.First.Read());
        Assert.Throws<InvalidOperationException>(() => captured.First.Write(5));
        Graph restoredA = NativeBattleStorageSnapshots.Restore(captured);
        Graph restoredB = NativeBattleStorageSnapshots.Restore(captured);
        Assert.IsType<StorageNode>(restoredA.First);
        Assert.Same(restoredA.First, restoredA.Retained);
        Assert.Same(restoredA.First.Authority, restoredA.Items[1].Authority);
        Assert.NotSame(restoredA.First.Authority, restoredB.First.Authority);
        Assert.NotSame(restoredA.First.Authority, captured.First.Authority);
        restoredA.First.Write(7);
        Assert.Equal(1, restoredB.First.Read());
        Assert.Equal(1, captured.First.Read());
        Assert.Equal(9, source.First.Read());
    }

    [Fact]
    public void Unchanged_records_and_frozen_nodes_are_shared_but_readonly_storage_fields_are_replaced()
    {
        ImmutableLeaf source = new("unchanged");
        Assert.Same(source, NativeBattleStorageSnapshots.Capture(source));
        ImmutableArray<ImmutableLeaf> items = [source];
        LeafCollection leaves = new(items, ImmutableArray<ImmutableLeaf>.Empty, default);
        Assert.Same(leaves, NativeBattleStorageSnapshots.Capture(leaves));
        ReadonlyOwner owned = new(new StorageNode([3]));
        ReadonlyOwner frozen = NativeBattleStorageSnapshots.Capture(owned);
        Assert.NotSame(owned, frozen);
        Assert.IsType<FrozenStorageNode>(frozen.Value);
        Assert.Same(frozen, NativeBattleStorageSnapshots.Capture(frozen));
        Assert.IsType<StorageNode>(NativeBattleStorageSnapshots.Restore(frozen).Value);
        Assert.IsType<StorageNode>(owned.Value);
        Assert.Throws<InvalidOperationException>(() => NativeBattleStorageSnapshots.Restore(owned));
    }

    [Fact]
    public void Plain_arrays_are_detached_and_one_backing_authority_is_constructed_once_per_context()
    {
        byte[] bytes = [1, 2];
        RawArrays original = new(bytes, bytes);
        RawArrays captured = NativeBattleStorageSnapshots.Capture(original);
        Assert.NotSame(bytes, captured.First);
        Assert.Same(captured.First, captured.Second);
        bytes[0] = 9;
        Assert.Equal(1, captured.First[0]);
        NativeBattleStorageCaptureContext context = new();
        object key = new();
        int creations = 0;
        byte[] Create()
        {
            creations++;
            return [3];
        }
        Assert.Same(context.GetOrAddOwner(key, Create), context.GetOrAddOwner(key, Create));
        Assert.Equal(1, creations);
    }

    [Fact]
    public void Unsupported_containers_and_incompatible_declared_types_fail_without_modifying_sources()
    {
        StorageNode node = new([4]);
        MutableContainer source = new([node]);
        Assert.Throws<NotSupportedException>(() => NativeBattleStorageSnapshots.Capture(source));
        Assert.Same(node, source.Values[0]);
        Assert.Equal(4, node.Read());
        FrozenStorageNode frozen = (FrozenStorageNode)NativeBattleStorageSnapshots.Capture(node);
        Assert.Throws<InvalidOperationException>(() => NativeBattleStorageSnapshots.Restore(frozen));
        Assert.Throws<InvalidOperationException>(() => NativeBattleStorageSnapshots.Restore(new NarrowOwner(frozen)));
        Assert.Equal(4, frozen.Read());
        Assert.IsType<StorageNode>(NativeBattleStorageSnapshots.Restore<StorageNode>(frozen));
    }

    private sealed record Graph(StorageNode First, StorageNode Retained, ImmutableArray<StorageNode> Items, ImmutableDictionary<string, StorageNode> Names, StorageNode[,] Grid, ImmutableLeaf Leaf);
    private sealed record ImmutableLeaf(string Text);
    private sealed record LeafCollection(ImmutableArray<ImmutableLeaf> Items, ImmutableArray<ImmutableLeaf> Empty, ImmutableArray<ImmutableLeaf> Default);
    private sealed record RawArrays(byte[] First, byte[] Second);
    private sealed record NarrowOwner(FrozenStorageNode Storage);
    private sealed record MutableContainer(List<StorageNode> Values);
    private sealed record ReadonlyOwner
    {
        private readonly StorageNode storage;
        public ReadonlyOwner(StorageNode value) => storage = value;
        public StorageNode Value => storage;
    }

    private class StorageNode(byte[] bytes) : INativeBattleStorageNode
    {
        public object Authority => bytes;
        public virtual bool IsSnapshot => false;
        public byte Read() => bytes[0];
        public void Write(byte value)
        {
            if (IsSnapshot)
            {
                throw new InvalidOperationException("Frozen storage cannot be mutated.");
            }
            bytes[0] = value;
        }
        public object CaptureDetached(NativeBattleStorageCaptureContext context) => new FrozenStorageNode(context.GetOrAddOwner(bytes, () => (byte[])bytes.Clone()));
        public object RestoreOwned(NativeBattleStorageRestoreContext context) => new StorageNode(context.GetOrAddOwner(bytes, () => (byte[])bytes.Clone()));
    }

    private sealed class FrozenStorageNode(byte[] bytes) : StorageNode(bytes)
    {
        public override bool IsSnapshot => true;
    }
}
