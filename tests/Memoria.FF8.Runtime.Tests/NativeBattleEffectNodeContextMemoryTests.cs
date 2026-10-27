using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectNodeContextMemoryTests
{
    [Fact]
    public void Physical_view_shares_the_existing_prefix_and_Euler_owner_without_copying_away_unknown_values()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        NativeBattleEffectNodeScratch scratch = memory.NodeScratch2793E30! with { Bytes = new byte[] { 1, 2, 3, 4, 5, 6, 0xAB, 0xCD }.ToImmutableArray(), KnownMask = 0x3F, Prefix2793E08 = memory.NodeScratch2793E30!.Prefix2793E08!.Write(0, [17, 18]) };
        memory = memory with { NodeScratch2793E30 = scratch };
        NativeBattleEffectArena view = NativeBattleEffectNodeContextMemory.Capture(memory);
        Assert.Equal(new byte[] { 17, 18 }, view.Read(100, 2));
        (byte[] data, bool[] known) = view.ReadSnapshot(140, 8);
        Assert.Equal(scratch.Bytes.ToArray(), data);
        Assert.Equal(new[] { true, true, true, true, true, true, false, false }, known);
        Assert.Throws<NotSupportedException>(() => view.Read(146, 2));
        view = NativeBattleEffectNodeContextMemory.Copy(view, 140, view, 148, 8);
        NativeBattleEffectMemory published = NativeBattleEffectNodeContextMemory.Publish(memory, view);
        NativeBattleEffectArena next = NativeBattleEffectNodeContextMemory.Capture(published);
        Assert.Equal(data, next.ReadSnapshot(148, 8).Data);
        Assert.Equal(known, next.ReadSnapshot(148, 8).Known);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(published)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(NativeBattleEffectNodeContextMemory.Publish(NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(memory)))!), view))));
    }

    [Fact]
    public void Missing_extended_owner_stays_unknown_while_real_stores_publish_only_their_covered_bytes()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { NodeContextExtension = null };
        NativeBattleEffectArena view = NativeBattleEffectNodeContextMemory.Capture(memory);
        Assert.Throws<NotSupportedException>(() => view.Read(0, 1));
        Assert.Throws<NotSupportedException>(() => view.Read(148, 1));
        Assert.Equal(new byte[40], view.Read(100, 40));
        NativeBattleEffectMemory published = NativeBattleEffectNodeContextMemory.Publish(memory, view.Write(76, [1, 2, 3, 4]));
        NativeBattleEffectArena next = NativeBattleEffectNodeContextMemory.Capture(published);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, next.Read(76, 4));
        Assert.Throws<NotSupportedException>(() => next.Read(75, 1));
        Assert.Throws<NotSupportedException>(() => next.Read(80, 1));
    }
}
