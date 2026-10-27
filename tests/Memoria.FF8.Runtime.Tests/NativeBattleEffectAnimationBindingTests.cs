using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectAnimationBindingTests
{
    [Fact]
    public void Borrowed_task_cache_uses_shared_arena_and_preserves_uncleared_unknown_padding_and_unrelated_pointers()
    {
        NativeBattleEffectMemory memory = Memory();
        NativeBattleEffectTaskPool pool = Pool().BindArena(memory.Arena!);
        NativeBattleModulePoolTask task = pool.Slots[0];
        task.WriteDword(32, 0xAABBCCDD).MarkPayloadUnknown(32, 4).WriteResourcePointer(56, new(null, 0x1234));
        NativeBattleGfAnimationWorkspace workspace = NativeBattleEffectTaskMemory.BorrowAnimationWorkspace(memory, [pool], pool.BankAddress + 24, 16);
        workspace.Initialize7016B0(Source(false), 11);
        NativeBattleEffectTaskMemory.PublishAnimationWorkspace(memory, [pool], pool.BankAddress + 24, workspace);
        Assert.Same(task, NativeBattleEffectTaskMemory.PublishAnimationWorkspace(task, 24, workspace));
        Assert.Equal(SourceAddress, task.ReadDword(24));
        Assert.Equal(SourceAddress, memory.Arena!.ReadUInt32(0x100 + 24));
        Span<byte> padding = stackalloc byte[4];
        Span<bool> known = stackalloc bool[4];
        task.ReadWithKnownness(32, padding, known);
        Assert.Equal(0xAABBCCDDu, BinaryPrimitives.ReadUInt32LittleEndian(padding));
        Assert.False(known.Contains(true));
        Assert.Contains(task.EnumerateResourcePointers(), entry => entry.Key == 56);
    }

    [Fact]
    public void Failed_draw_remains_under_outer_pool_rollback_including_cache_and_header_changes()
    {
        NativeBattleEffectMemory memory = Memory();
        NativeBattleEffectTaskPool pool = Pool().BindArena(memory.Arena!);
        NativeBattleModulePoolTask task = pool.Slots[0];
        task.WriteDword(24, 0xDEADBEEF).WriteDword(28, 9).WriteDword(32, 88);
        pool.BeginCheckpoint();
        NativeBattleGfAnimationWorkspace workspace = NativeBattleEffectTaskMemory.BorrowAnimationWorkspace(memory, [pool], pool.BankAddress + 24, 16);
        workspace.Initialize7016B0(Source(true), 8);
        Assert.Throws<InvalidOperationException>(() => workspace.Advance701970(Source(true), 0, (current, node) =>
        {
            task.UpdateHeader(task.Flags, 7, task.Next, task.Callback).WriteDword(32, 99);
            Assert.Equal(99u, BinaryPrimitives.ReadUInt32LittleEndian(current.Read(8, 4)));
            throw new InvalidOperationException("Draw failed.");
        }));
        pool.RollbackCheckpoint();
        Assert.Equal(0xDEADBEEFu, task.ReadDword(24));
        Assert.Equal(9u, task.ReadDword(28));
        Assert.Equal(88u, task.ReadDword(32));
        Assert.Equal((ushort)0, task.State);
    }

    [Fact]
    public void Resolver_rejects_headers_cross_slot_ranges_and_unowned_module_addresses()
    {
        NativeBattleEffectMemory memory = Memory();
        NativeBattleEffectTaskPool pool = Pool();
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectTaskMemory.BorrowAnimationWorkspace(memory, [pool], pool.BankAddress + 8, 16));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectTaskMemory.BorrowAnimationWorkspace(memory, [pool], pool.BankAddress + 60, 16));
        Assert.Throws<NotSupportedException>(() => NativeBattleEffectTaskMemory.BorrowAnimationWorkspace(memory, [pool], 0x50000000, 16));
        NativeBattleGfAnimationWorkspace foreign = new(new byte[16].ToImmutableArray());
        Assert.Throws<InvalidOperationException>(() => NativeBattleEffectTaskMemory.PublishAnimationWorkspace(memory, [pool], pool.BankAddress + 24, foreign));
        Assert.Throws<InvalidOperationException>(() => NativeBattleEffectTaskMemory.PublishAnimationWorkspace(pool.Slots[0], 24, foreign));
        NativeBattleGfAnimationWorkspace common = NativeBattleEffectTaskMemory.BorrowAnimationWorkspace(memory, [pool], NativeBattleEffectNodeMemory.ArenaAddress + 0x300, 16);
        common.Initialize7016B0(Source(false), 8);
        Assert.Equal(SourceAddress, memory.Arena!.ReadUInt32(0x300));
    }

    private const uint SourceAddress = 0x1234000;
    private static NativeBattleEffectMemory Memory() => new(NativeBattleEffectArena.Clear(), 0, 0, [], 0, 0, 0, 0);
    private static NativeBattleEffectTaskPool Pool() => NativeBattleEffectTaskPool.Initialize(0x3000000, NativeBattleEffectNodeMemory.ArenaAddress + 0x100, 64, 2);

    private static NativeBattleGfAnimationSource Source(bool node)
    {
        byte[] bytes = new byte[48];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 16);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), node ? 1 : 0);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20), 3);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(24), 16);
        return new(SourceAddress, bytes.ToImmutableArray());
    }
}
