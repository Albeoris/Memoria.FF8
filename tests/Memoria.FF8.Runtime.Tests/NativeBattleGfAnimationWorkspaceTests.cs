using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGfAnimationWorkspaceTests
{
    [Fact]
    public void Initializer_clears_only_complete_DWORDs_and_seeds_scale_cache_without_inventing_prior_bytes()
    {
        Fixture fixture = new(0x40);
        NativeBattleGfAnimationWorkspace prior = new(Enumerable.Repeat((byte)0xA5, 40).ToImmutableArray());
        NativeBattleGfAnimationWorkspace priorSnapshot = NativeBattleStorageSnapshots.Capture(prior);
        NativeBattleGfAnimationWorkspace initialized = prior.Initialize7016B0(fixture.Source, 23);
        Assert.Equal(Fixture.Address, initialized.ResourceAddress);
        Assert.Equal(0, initialized.Frame);
        Assert.Equal(new byte[] { 0, 0, 0, 0x10, 0, 0, 0, 0x10, 0, 0, 0, 0x10 }, NativeBattleStorageSnapshots.Capture(initialized).Data.AsSpan().Slice(8, 12).ToArray());
        Assert.All(NativeBattleStorageSnapshots.Capture(initialized).Data.Skip(20), value => Assert.Equal((byte)0xA5, value));
        Assert.All(priorSnapshot.Data, value => Assert.Equal((byte)0xA5, value));
        Assert.Throws<InvalidDataException>(() => prior.Initialize7016B0(fixture.Source, 44));
        NativeBattleGfAnimationWorkspace pointerOnly = NativeBattleStorageSnapshots.Restore(priorSnapshot).Initialize7016B0(new Fixture(0).Source, 7);
        Assert.Equal(unchecked((int)0xA5A5A5A5), pointerOnly.Frame);
    }

    [Fact]
    public void Vectors_integrate_acceleration_before_velocity_then_apply_exact_frame_keys()
    {
        Fixture fixture = new(7);
        fixture.Stream(0, 0, 0, Q(10), Q(-10), int.MaxValue, 2, Q(100), Q(200), Q(300), -1);
        fixture.Stream(0, 1, 0, Q(2), Q(-2), 1, -1);
        fixture.Stream(0, 2, 0, Q(1), Q(-1), 0, -1);
        NativeBattleGfAnimationWorkspace workspace = fixture.Initialize();
        List<NativeBattleGfAnimationNode> nodes = [];
        NativeBattleGfAnimationAdvance first = workspace.Advance701970(fixture.Source, 0, Collect);
        Assert.Equal(new NativeBattleVector(10, -10, 32767), nodes[0].Vector8);
        Assert.Equal(new NativeBattleVector(4096, 4096, 4096), nodes[0].Vector24);
        Assert.Equal(1, first.Workspace.Frame);
        NativeBattleGfAnimationAdvance second = first.Workspace.Advance701970(fixture.Source, 0, Collect);
        Assert.Equal(new NativeBattleVector(13, -13, -32768), nodes[1].Vector8);
        Assert.Equal(Q(3), Int(second.Workspace, 20));
        NativeBattleGfAnimationAdvance third = second.Workspace.Advance701970(fixture.Source, 0, Collect);
        Assert.Equal(new NativeBattleVector(100, 200, 300), nodes[2].Vector8);
        Assert.Equal(Q(4), Int(third.Workspace, 20));
        Assert.Equal(3, third.Workspace.Frame);
        Assert.Equal(7, third.Remaining);

        NativeBattleGfAnimationWorkspace Collect(NativeBattleGfAnimationWorkspace current, NativeBattleGfAnimationNode node)
        {
            Assert.Equal(nodes.Count, current.Frame);
            Assert.Equal((ushort)0, node.NodeIndex);
            Assert.Equal((ushort)0xA0, node.GeometryIndex);
            Assert.Equal(0x123400A0u, node.Header);
            Assert.Equal(44, node.Data.Length);
            nodes.Add(node);
            return current;
        }
    }

    [Fact]
    public void Frozen_frames_reapply_keys_and_emit_without_integrating_or_advancing()
    {
        Fixture fixture = new(7);
        fixture.Stream(0, 0, 0, Q(-2), Q(3), Q(4), -1);
        fixture.Stream(0, 1, 0, Q(8), Q(8), Q(8), -1);
        fixture.Stream(0, 2, 0, Q(1), Q(1), Q(1), -1);
        NativeBattleGfAnimationWorkspace workspace = fixture.Initialize();
        List<NativeBattleGfAnimationNode> nodes = [];
        NativeBattleGfAnimationAdvance first = workspace.Advance701970(fixture.Source, -1, Collect);
        NativeBattleGfAnimationWorkspace firstSnapshot = NativeBattleStorageSnapshots.Capture(first.Workspace);
        NativeBattleGfAnimationAdvance second = first.Workspace.Advance701970(fixture.Source, 1, Collect);
        Assert.Equal(0, first.Workspace.Frame);
        Assert.Equal(10, second.Remaining);
        Assert.Equal(firstSnapshot.Data.ToArray(), NativeBattleStorageSnapshots.Capture(second.Workspace).Data.ToArray());
        Assert.Equal(new NativeBattleVector(-2, 3, 4), nodes[0].Vector8);
        Assert.Equal(nodes[0].Data.ToArray(), nodes[1].Data.ToArray());

        NativeBattleGfAnimationWorkspace Collect(NativeBattleGfAnimationWorkspace current, NativeBattleGfAnimationNode node)
        {
            nodes.Add(node);
            return current;
        }
    }

    [Fact]
    public void Colors_use_unsigned_values_signed_WORD_derivatives_and_saturating_bounds()
    {
        Fixture fixture = new(0xE00);
        fixture.Stream(0, 9, 0, Pack(65530, 5), Pack(0x1000, 0xBEEF), -1);
        fixture.Stream(0, 10, 0, Pack(10, -10), Pack(32767, 0xCAFE), -1);
        fixture.Stream(0, 11, 0, Pack(0, 0), Pack(1, 0xCAFE), -1);
        List<NativeBattleGfAnimationNode> nodes = [];
        NativeBattleGfAnimationAdvance first = fixture.Initialize().Advance701970(fixture.Source, 0, Collect);
        NativeBattleGfAnimationAdvance second = first.Workspace.Advance701970(fixture.Source, 0, Collect);
        Assert.Equal(0x001000FFu, nodes[0].Color32);
        Assert.Equal(0x000000FFu, nodes[1].Color32);
        Assert.Equal((ushort)65535, UShort(second.Workspace, 8));
        Assert.Equal((ushort)0, UShort(second.Workspace, 10));
        Assert.Equal((ushort)0, UShort(second.Workspace, 12));
        Assert.Equal(short.MinValue, Short(second.Workspace, 20));
        Assert.Equal((ushort)0, UShort(second.Workspace, 14));

        NativeBattleGfAnimationWorkspace Collect(NativeBattleGfAnimationWorkspace current, NativeBattleGfAnimationNode node)
        {
            nodes.Add(node);
            return current;
        }
    }

    [Fact]
    public void Scalar_caches_clamp_before_keys_and_preserve_compound_WORDs()
    {
        Fixture fixture = new(0x3000);
        fixture.Stream(0, 12, 0, 0x10000001, Q(1), 0, 2, Q(-2), Q(1), 0, -1);
        fixture.Stream(0, 13, 0, Pack(-123, 234), Q(1), Q(-2), 0, -1);
        List<NativeBattleGfAnimationNode> nodes = [];
        NativeBattleGfAnimationAdvance first = fixture.Initialize().Advance701970(fixture.Source, 0, Collect);
        NativeBattleGfAnimationWorkspace firstSnapshot = NativeBattleStorageSnapshots.Capture(first.Workspace);
        NativeBattleGfAnimationAdvance second = first.Workspace.Advance701970(fixture.Source, 0, Collect);
        NativeBattleGfAnimationWorkspace secondSnapshot = NativeBattleStorageSnapshots.Capture(second.Workspace);
        NativeBattleGfAnimationAdvance third = second.Workspace.Advance701970(fixture.Source, 0, Collect);
        Assert.Equal((short)4096, nodes[0].Scalar36);
        Assert.Equal(0x10000001, Int(firstSnapshot, 8));
        Assert.Equal(0x10000000, Int(secondSnapshot, 8));
        Assert.Equal((short)1, nodes[0].Scalar38);
        Assert.Equal((short)0, nodes[1].Scalar38);
        Assert.Equal((short)-123, nodes[2].Word40);
        Assert.Equal((short)234, nodes[2].Word42);
        Assert.Equal((short)-2, nodes[2].Scalar36);
        Assert.Equal(Q(-2), Int(third.Workspace, 8));

        NativeBattleGfAnimationWorkspace Collect(NativeBattleGfAnimationWorkspace current, NativeBattleGfAnimationNode node)
        {
            nodes.Add(node);
            return current;
        }
    }

    [Fact]
    public void Callback_runs_in_node_order_before_frame_increment_and_its_frame_write_is_observed()
    {
        Fixture fixture = new(1, 1);
        fixture.Stream(0, 0, 0, Q(1), 0, 0, -1);
        fixture.Stream(1, 0, 0, Q(2), 0, 0, 3, Q(9), 0, 0, -1);
        List<NativeBattleGfAnimationNode> nodes = [];
        NativeBattleGfAnimationAdvance advanced = fixture.Initialize().Advance701970(fixture.Source, 0, (current, node) =>
        {
            nodes.Add(node);
            if (node.NodeIndex == 0)
            {
                Assert.Equal(0, current.Frame);
                Assert.Equal(Q(1), Int(current, 8));
                Assert.Equal(0, Int(current, 20));
                return WriteInt(current, 4, 3);
            }
            Assert.Equal(3, current.Frame);
            Assert.Equal(new NativeBattleVector(9, 0, 0), node.Vector8);
            return current;
        });
        Assert.Equal(new ushort[] { 0, 1 }, nodes.Select(node => node.NodeIndex).ToArray());
        Assert.Equal(2, advanced.EmittedNodes);
        Assert.Equal(4, advanced.Workspace.Frame);
        Assert.Equal(6, advanced.Remaining);
    }

    [Fact]
    public void Unsupported_callback_can_publish_exact_boundary_without_advancing_later_nodes()
    {
        Fixture fixture = new(1, 1);
        fixture.Stream(0, 0, 0, Q(7), 0, 0, -1);
        fixture.Stream(1, 0, 0, Q(9), 0, 0, -1);
        NativeBattleGfAnimationWorkspace? boundary = null;
        Assert.Throws<NotSupportedException>(() => fixture.Initialize().Advance701970(fixture.Source, 0, (current, node) =>
        {
            Assert.Equal((ushort)0, node.NodeIndex);
            boundary = NativeBattleStorageSnapshots.Capture(current);
            throw new NotSupportedException("Downstream native geometry callback is unimplemented.");
        }));
        Assert.NotNull(boundary);
        Assert.Equal(0, boundary.Frame);
        Assert.Equal(Q(7), Int(boundary, 8));
        Assert.Equal(0, Int(boundary, 20));
    }

    [Fact]
    public void Snapshot_continuation_is_identical_and_duration_boundary_is_inert()
    {
        Fixture fixture = new(2);
        fixture.Stream(0, 1, 0, Q(3), Q(-4), Q(5), -1);
        NativeBattleGfAnimationWorkspace original = fixture.Initialize().Advance701970(fixture.Source, 0, (current, _) => current).Workspace;
        NativeBattleGfAnimationWorkspace restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleGfAnimationWorkspace>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(original)))!);
        List<NativeBattleGfAnimationNode> left = [];
        List<NativeBattleGfAnimationNode> right = [];
        NativeBattleGfAnimationAdvance expected = original.Advance701970(fixture.Source, 0, (current, node) =>
        {
            left.Add(node);
            return current;
        });
        NativeBattleGfAnimationAdvance actual = restored.Advance701970(fixture.Source, 0, (current, node) =>
        {
            right.Add(node);
            return current;
        });
        Assert.Equal(NativeBattleStorageSnapshots.Capture(expected.Workspace).Data.ToArray(), NativeBattleStorageSnapshots.Capture(actual.Workspace).Data.ToArray());
        Assert.Equal(expected.Remaining, actual.Remaining);
        Assert.Equal(Assert.Single(left).Data.ToArray(), Assert.Single(right).Data.ToArray());
        NativeBattleGfAnimationWorkspace completed = WriteInt(actual.Workspace, 4, 10);
        NativeBattleGfAnimationAdvance inert = completed.Advance701970(fixture.Source, 0, (_, _) => throw new InvalidOperationException("Completed animation must not emit."));
        Assert.Same(completed, inert.Workspace);
        Assert.Equal(0, inert.EmittedNodes);
        Assert.Equal(0, inert.Remaining);
        Assert.Throws<InvalidDataException>(() => completed.Advance701970(fixture.Source with { Address = 1 }, 0, (current, _) => current));
    }

    [Theory]
    [InlineData(0, 1, 9)]
    [InlineData(1, 0, 10)]
    public void Empty_node_table_advances_only_when_not_frozen(int frozen, int expectedFrame, int expectedRemaining)
    {
        Fixture fixture = new();
        NativeBattleGfAnimationAdvance advanced = fixture.Initialize().Advance701970(fixture.Source, frozen, (_, _) => throw new InvalidOperationException("Empty node table must not emit."));
        Assert.Equal(expectedFrame, advanced.Workspace.Frame);
        Assert.Equal(expectedRemaining, advanced.Remaining);
    }

    private static int Q(int value) => unchecked(value << 16);
    private static int Pack(int low, int high) => unchecked((ushort)low | (int)((uint)(ushort)high << 16));
    private static int Int(NativeBattleGfAnimationWorkspace state, int offset) => BinaryPrimitives.ReadInt32LittleEndian(NativeBattleStorageSnapshots.Capture(state).Data.AsSpan().Slice(offset));
    private static short Short(NativeBattleGfAnimationWorkspace state, int offset) => BinaryPrimitives.ReadInt16LittleEndian(NativeBattleStorageSnapshots.Capture(state).Data.AsSpan().Slice(offset));
    private static ushort UShort(NativeBattleGfAnimationWorkspace state, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(NativeBattleStorageSnapshots.Capture(state).Data.AsSpan().Slice(offset));

    private static NativeBattleGfAnimationWorkspace WriteInt(NativeBattleGfAnimationWorkspace state, int offset, int value)
    {
        byte[] bytes = NativeBattleStorageSnapshots.Capture(state).Data.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset), value);
        return new(bytes.ToImmutableArray());
    }

    [Fact]
    public void Borrowed_source_preserves_unread_resource_bytes_across_callbacks_without_undoing_real_writes()
    {
        Fixture fixture = new(0, 0);
        NativeBattleEffectArena arena = NativeBattleEffectArena.CreateUnknown(fixture.Source.Data.Length).Write(0, fixture.Source.Data.AsSpan());
        NativeBattleGfAnimationSource borrowed = NativeBattleGfAnimationSource.Borrow(Fixture.Address, arena, 0, fixture.Source.Data.Length);
        NativeBattleGfAnimationWorkspace workspace = fixture.Initialize();
        List<uint> headers = [];
        NativeBattleGfAnimationAdvance first = workspace.Advance701970(borrowed, 0, (current, node) =>
        {
            headers.Add(node.Header);
            if (node.NodeIndex == 0)
            {
                arena.Write(0x200, [1, 2, 3, 4]);
                using NativeBattleGfSourceReader nested = borrowed.OpenReader();
                Assert.Equal(0x123400A1u, nested.UInt(0x200));
            }
            return current;
        });
        Assert.Equal(new uint[] { 0x123400A0, 0x123400A1 }, headers);
        Assert.Equal(0x04030201u, arena.ReadUInt32(0x200));
        headers.Clear();
        first.Workspace.Advance701970(borrowed, 0, (current, node) =>
        {
            headers.Add(node.Header);
            return current;
        });
        Assert.Equal(new uint[] { 0x123400A0, 0x04030201 }, headers);
        Assert.Null(borrowed.ActiveReader);
    }

    [Fact]
    public void Borrowed_unknown_source_failure_occurs_at_the_consumed_node_after_accepted_callback()
    {
        Fixture fixture = new(0, 0);
        NativeBattleEffectArena arena = NativeBattleEffectArena.CreateUnknown(fixture.Source.Data.Length).Write(0, fixture.Source.Data.AsSpan()).MarkUnknown(0x200, 4);
        NativeBattleGfAnimationSource borrowed = NativeBattleGfAnimationSource.Borrow(Fixture.Address, arena, 0, fixture.Source.Data.Length);
        int emitted = 0;
        Assert.Throws<NotSupportedException>(() => fixture.Initialize().Advance701970(borrowed, 0, (current, _) =>
        {
            emitted++;
            return current;
        }));
        Assert.Equal(1, emitted);
        Assert.Null(borrowed.ActiveReader);
        Assert.Throws<InvalidOperationException>(() => borrowed.Data);
    }

    [Fact]
    public void Local_cache_scope_accepts_completed_emits_and_discards_the_failed_emit_suffix()
    {
        Fixture fixture = new(1, 1);
        fixture.Stream(0, 0, 0, Q(7), 0, 0, -1);
        fixture.Stream(1, 0, 0, Q(9), 0, 0, -1);
        NativeBattleGfAnimationWorkspace workspace = fixture.Initialize();
        NativeBattleGfAnimationWorkspace original = NativeBattleStorageSnapshots.Capture(workspace);
        Assert.Throws<InvalidOperationException>(() => workspace.Advance701970(fixture.Source, 0, (current, node) => node.NodeIndex == 0 ? current : throw new InvalidOperationException("Failed second emit.")));
        Assert.Equal(Q(7), Int(workspace, 8));
        Assert.Equal(0, Int(workspace, 20));
        Assert.Equal(0, workspace.Frame);
        Assert.Equal(0, Int(original, 8));
    }

    [Fact]
    public void Task_backed_cache_does_not_accept_or_close_the_enclosing_pool_journal()
    {
        Fixture fixture = new(1, 1);
        fixture.Stream(0, 0, 0, Q(7), 0, 0, -1);
        fixture.Stream(1, 0, 0, Q(9), 0, 0, -1);
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x400, 0x800, 524, 1);
        (pool, _) = pool.Append(1);
        NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(pool.Slots[0], 12, 512).Initialize7016B0(fixture.Source, 512);
        NativeBattleEffectTaskPool original = NativeBattleStorageSnapshots.Capture(pool);
        pool.BeginCheckpoint();
        Assert.Throws<InvalidOperationException>(() => workspace.Advance701970(fixture.Source, 0, (current, node) => node.NodeIndex == 0 ? current : throw new InvalidOperationException("Failed second emit.")));
        Assert.True(pool.HasCheckpoint);
        Assert.Equal(Q(7), Int(workspace, 8));
        Assert.Equal(Q(9), Int(workspace, 20));
        pool.RollbackCheckpoint();
        Assert.False(pool.HasCheckpoint);
        Assert.Equal(0, Int(workspace, 8));
        Assert.Equal(0, Int(workspace, 20));
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(pool)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Lazy_retained_cache_enlists_before_initialization_or_emit_opens_a_public_transaction(bool advance)
    {
        Fixture fixture = new(1, 1);
        fixture.Stream(0, 0, 0, Q(7), 0, 0, -1);
        fixture.Stream(1, 0, 0, Q(9), 0, 0, -1);
        NativeBattleEffectArena bank = NativeBattleEffectArena.CreateUnknown(512).Write(0, Enumerable.Repeat((byte)0xCD, 512).ToArray());
        NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(bank, 0, 512);
        if (advance)
        {
            workspace.Initialize7016B0(fixture.Source, 512);
        }
        NativeBattleEffectArena before = NativeBattleStorageSnapshots.Capture(bank);
        NativeBattleRetainedEffectStorage.AdmitStorage(bank, new NativeBattleRetainedStorageAuthority());
        using (NativeBattleTaskOperation operation = new([]))
        {
            Assert.False(bank.Working.HasCheckpoint);
            if (advance)
            {
                Assert.Throws<InvalidOperationException>(() => workspace.Advance701970(fixture.Source, 0, (current, node) =>
                {
                    Assert.True(bank.Working.HasCheckpoint);
                    Assert.False(NativeBattleEffectArenaTransaction.HasActive(bank.Working));
                    return node.NodeIndex == 0 ? current : throw new InvalidOperationException("Unpublished second retained-cache emit failed.");
                }));
                Assert.Equal(Q(7), Int(workspace, 8));
                Assert.Equal(Q(9), Int(workspace, 20));
            }
            else
            {
                workspace.Initialize7016B0(fixture.Source, 512);
                Assert.Equal(Fixture.Address, workspace.ResourceAddress);
            }
            Assert.True(bank.Working.HasCheckpoint);
            Assert.False(NativeBattleEffectArenaTransaction.HasActive(bank.Working));
        }
        Assert.False(bank.Working.HasCheckpoint);
        Assert.False(NativeBattleEffectArenaTransaction.HasActive(bank.Working));
        Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(bank)));
    }
    private sealed class Fixture
    {
        public const uint Address = 0x1298C68;
        private readonly byte[] _data = new byte[16384];
        private readonly Dictionary<(int Node, int Bit), int> _pointers = [];
        private int _stream = 0x1000;

        public NativeBattleGfAnimationSource Source => new(Address, _data.ToImmutableArray());

        public Fixture(params uint[] flags)
        {
            Put(4, 0x20);
            Put(0x20, flags.Length);
            Put(0x24, 10);
            for (int index = 0; index < flags.Length; index++)
            {
                int node = Node(index);
                Put(0x28 + index * 4, node - 0x20);
                Put(node, unchecked((int)(0x123400A0u + (uint)index)));
                Put(node + 4, unchecked((int)flags[index]));
                int pointer = node + 8;
                for (int bit = 0; bit < 14; bit++)
                {
                    if ((flags[index] & (1u << bit)) != 0)
                    {
                        _pointers.Add((index, bit), pointer);
                        pointer += 4;
                        Stream(index, bit, -1);
                    }
                }
            }
        }

        public NativeBattleGfAnimationWorkspace Initialize() => new NativeBattleGfAnimationWorkspace(Enumerable.Repeat((byte)0xCD, 512).ToImmutableArray()).Initialize7016B0(Source, 512);

        public void Stream(int node, int bit, params int[] words)
        {
            Put(_pointers[(node, bit)], _stream - Node(node));
            foreach (int word in words)
            {
                Put(_stream, word);
                _stream += 4;
            }
        }

        private static int Node(int index) => 0x100 + index * 0x100;
        private void Put(int offset, int value) => BinaryPrimitives.WriteInt32LittleEndian(_data.AsSpan(offset), value);
    }
}
