using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMemoryNodeAdmissionTests
{
    [Fact]
    public void Cold_null_core_source_is_admitted_to_memory_before_first_bank_operation()
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(0, 240, 0, 0, 12, 93, null));
        (packets, _) = packets.AppendContact(new(new(3, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        NativeBattleEffectHierarchy hierarchy = new([NativeBattleEffectTaskPool.Initialize(0x1000, 0x2000, 100, 1), NativeBattleEffectTaskPool.Initialize(0x3000, 0x4000, 676, 1)]);
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, 1, 100);
        hierarchy = root.Hierarchy.Write(root.Address, root.Hierarchy.Read(root.Address).WriteWord(0x5A, 1));
        NativeBattleEffectHierarchyAppend child = hierarchy.Append8DC540(1, NativeBattleRecover93Nodes.ContextCallback, 676, root.Address, (_, _, _) => new(0, 3));
        hierarchy = child.Hierarchy.Write(child.Address, child.Hierarchy.Read(child.Address).WriteDword(0x30, NativeBattleRecover93Nodes.SourceAddress).WriteWord(0x29C, 2));
        NativeBattleOriginalEffectState effect = new(93, 92, new(93, 1, 0), new(0, 1, 8, 0, 0, 0), null) { Pools = hierarchy.Pools };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        NativeBattleRecover93NodeFrame frame = new(memory, effect, NativeBattleRecover93NodeState.FromColdProcess(), new(1));
        Assert.Null(frame.State.Data);
        NativeBattleEffectNodeCallerPolicy policy = new(NativeBattleRecover93Nodes.SourceAddress, NativeBattleRecover93Nodes.SourceLength, NativeBattleRecover93Nodes.ContextCallback, 20, 70, false);
        NativeBattleGraphicsModel Model(int actor) => new(actor, 0) { Transform = new(new(0, 0, 0), new(0, 0, 0), 0, 0, 4096), GeometryBounds52 = new(new(-1, -1, -1), new(1, 1, 1)) };
        NativeBattleRecover93NodeFrame initialized = NativeBattleEffectNodeCaller.Initialize74EC00(policy, frame, child.Address, new(new byte[NativeBattleRecover93Nodes.SourceLength].ToImmutableArray()), packets, Model, _ => throw new InvalidOperationException("Explicit mode2 origins bypass pose reads."), new(new(0, 0, 0), new(0, 0, 0)));
        NativeBattleNodeSource source = Assert.IsType<NativeBattleNodeSource>(initialized.State.Data);
        Assert.Same(memory.RetainedAuthority, source.Arena!.Working.RetainedAuthority);
        Assert.NotSame(effect.RetainedAuthority, source.Arena.Working.RetainedAuthority);
        Assert.Equal(1u, source.ReadUInt32(0));
    }

    [Fact]
    public void Published_memory_accepts_a_new_core_source_and_later_suffix_still_rolls_back()
    {
        NativeBattleEffectMemory memory = new(null, 0, 0, [], 0, 0, 0, 0);
        NativeBattleOriginalEffectState effect = new(93, 92, new(93, 1, 0), null, null);
        NativeBattleNodeSource source = new(new byte[NativeBattleRecover93Nodes.SourceLength].ToImmutableArray());
        NativeBattleEffectArena arena = source.Arena!;
        using (NativeBattleTaskOperation operation = new(memory, effect))
        {
            NativeBattleTaskOperation.AdmitCurrentRetainedMemoryStorage(source);
            Assert.Same(memory.RetainedAuthority, arena.Working.RetainedAuthority);
            Assert.NotSame(effect.RetainedAuthority, arena.Working.RetainedAuthority);
            arena.WriteUInt32(4, 1);
            NativeBattleRecover93NodeState nodes = NativeBattleRecover93NodeState.FromColdProcess() with { Data = source };
            NativeBattleEffectMemory published = memory with { Recover93 = new(null, [], null, null, null, 0, 0, nodes) };
            NativeBattleTaskOperation.AcceptPublishedMemory(published);
            arena.WriteUInt32(4, 2);
        }
        Assert.Equal(1u, source.ReadUInt32(4));
    }
}
