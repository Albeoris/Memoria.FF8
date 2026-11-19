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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Storage_free_immutable_bytes_inside_unknown_wrappers_do_not_allocate_per_element(bool privateContinuation)
    {
        UnknownContainer small = new(ImmutableArray.Create((byte)17));
        UnknownContainer large = new(Enumerable.Repeat((byte)17, 65536).ToImmutableArray());
        for (int iteration = 0; iteration < 8; iteration++)
        {
            Assert.Same(small, Capture(small));
            Assert.Same(large, Capture(large));
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int iteration = 0; iteration < 8; iteration++)
        {
            _ = Capture(small);
        }
        long smallBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        for (int iteration = 0; iteration < 8; iteration++)
        {
            _ = Capture(large);
        }
        long largeBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(largeBytes <= smallBytes + 16384, $"Large immutable bytes allocated {largeBytes}; the same wrapper with one byte allocated {smallBytes}.");

        UnknownContainer Capture(UnknownContainer value) => privateContinuation ? NativeBattleStorageSnapshots.CapturePrivate(value) : NativeBattleStorageSnapshots.Capture(value);
    }

    [Theory]
    [InlineData("direct")]
    [InlineData("object-array")]
    [InlineData("immutable-object-array")]
    [InlineData("mutable-list")]
    [InlineData("nested-wrapper")]
    [InlineData("frozen-node")]
    public void Unknown_containers_still_reject_storage_hidden_behind_runtime_object_types(string kind)
    {
        StorageNode node = new([23]);
        object hidden = kind switch
        {
            "object-array" => new object[] { node },
            "immutable-object-array" => ImmutableArray.Create<object>(node),
            "mutable-list" => new List<object> { node },
            "nested-wrapper" => new UnknownContainer(node),
            "frozen-node" => NativeBattleStorageSnapshots.Capture(node),
            _ => node
        };
        UnknownContainer wrapper = new(hidden);
        Assert.Throws<NotSupportedException>(() => NativeBattleStorageSnapshots.Capture(wrapper));
        Assert.Throws<NotSupportedException>(() => NativeBattleStorageSnapshots.CapturePrivate(wrapper));
        Assert.Equal(23, node.Read());
        Assert.Same(hidden, wrapper.Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Unknown_wrappers_still_visit_custom_immutable_record_enumerators_with_static_storage(bool immutableArray)
    {
        StaticStorageEnumerable enumerable = new();
        object hidden = immutableArray ? ImmutableArray.Create(enumerable) : enumerable;
        UnknownContainer wrapper = new(hidden);
        Assert.Throws<NotSupportedException>(() => NativeBattleStorageSnapshots.Capture(wrapper));
        Assert.Throws<NotSupportedException>(() => NativeBattleStorageSnapshots.CapturePrivate(wrapper));
        Assert.Same(hidden, wrapper.Value);
    }

    [Fact]
    public void Object_typed_storage_fields_still_map_aliases_and_detach_restored_authorities()
    {
        StorageNode node = new([17]);
        ImmutablePayload definition = new([1, 2, 3]);
        ObjectOwner source = new(node, node, definition);
        ObjectOwner captured = NativeBattleStorageSnapshots.CapturePrivate(source);
        FrozenStorageNode frozen = Assert.IsType<FrozenStorageNode>(captured.First);
        Assert.Same(frozen, captured.Second);
        Assert.Same(definition, captured.Definition);
        node.Write(99);
        Assert.Equal(17, frozen.Read());
        ObjectOwner first = NativeBattleStorageSnapshots.Restore(captured);
        ObjectOwner second = NativeBattleStorageSnapshots.Restore(captured);
        StorageNode firstNode = Assert.IsType<StorageNode>(first.First);
        StorageNode secondNode = Assert.IsType<StorageNode>(second.First);
        Assert.Same(first.First, first.Second);
        Assert.Same(second.First, second.Second);
        Assert.NotSame(firstNode.Authority, secondNode.Authority);
        Assert.Same(definition, first.Definition);
        Assert.Same(definition, second.Definition);
        firstNode.Write(7);
        Assert.Equal(17, secondNode.Read());
        Assert.Equal(17, frozen.Read());
        Assert.Equal(99, node.Read());
    }

    private sealed class UnknownContainer(object value)
    {
        public object Value { get; } = value;
    }

    private sealed record StaticStorageEnumerable : IEnumerable<object>
    {
        private static readonly StorageNode storage = new([31]);

        public IEnumerator<object> GetEnumerator()
        {
            yield return storage;
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    private sealed record ImmutablePayload(ImmutableArray<byte> Bytes);
    private sealed record ObjectOwner(object First, object Second, ImmutablePayload Definition);

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
