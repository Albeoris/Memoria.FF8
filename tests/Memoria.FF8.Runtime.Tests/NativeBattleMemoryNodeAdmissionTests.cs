using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMemoryNodeAdmissionTests
{
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
