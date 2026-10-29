using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRinoa87NodeOwnershipTests
{
    [Fact]
    public void Fresh_node_birth_matches_existing_memory_owned_source_with_distinct_effect_authority()
    {
        var fresh = Fixture(false);
        var retained = Fixture(true);
        NativeBattleRinoa87NodeStep first = Advance(fresh.Frame, fresh.Address, fresh.Resources, fresh.Packets);
        NativeBattleRinoa87NodeStep control = Advance(retained.Frame, retained.Address, retained.Resources, retained.Packets);
        NativeBattleNodeSource source = Assert.IsType<NativeBattleNodeSource>(first.Frame.State.NodeData);
        Assert.NotSame(first.Frame.Memory.RetainedAuthority, first.Frame.Effect.RetainedAuthority);
        Assert.Same(first.Frame.Memory.RetainedAuthority, source.Arena.Working.RetainedAuthority);
        Assert.NotSame(first.Frame.Effect.RetainedAuthority, source.Arena.Working.RetainedAuthority);
        Assert.Equal(NativeBattleTestSnapshots.Json(control.Frame), NativeBattleTestSnapshots.Json(first.Frame));
        Assert.Equal(new NativeCrtRandomState(17), first.Random);
        byte[] expected = NativeBattleEffectNodeBankFixups.Relocate(fresh.Resources.Data, NativeBattleRinoa87NodeResources.Address);
        BinaryPrimitives.WriteUInt32LittleEndian(expected, 1);
        Assert.Equal(expected, source.Arena.Read(0, expected.Length));
        Assert.All(source.Arena.ReadSnapshot(0, expected.Length).Known, value => Assert.True(value));
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(first.Frame.Effect.Pools).Read(fresh.Address);
        Assert.Equal((byte)1, task.ReadByte(41));
        Assert.Equal((ushort)1, task.ReadWord(36));
    }

    [Fact]
    public void Reused_source_and_detached_restore_continue_the_next_callback_identically()
    {
        var fixture = Fixture(false);
        NativeBattleRinoa87NodeStep initialized = Advance(fixture.Frame, fixture.Address, fixture.Resources, fixture.Packets);
        NativeBattleEffectHierarchy hierarchy = new(initialized.Frame.Effect.Pools);
        // The reached completion phase avoids requiring a fabricated particle resource programme.
        hierarchy = hierarchy.Write(fixture.Address, hierarchy.Read(fixture.Address).WriteWord(0x34 + 0x20, 2));
        NativeBattleRinoa87Frame before = initialized.Frame with { Effect = initialized.Frame.Effect with { Pools = hierarchy.Pools } };
        NativeBattleRinoa87Frame restored = NativeBattleTestSnapshots.Independent(before);
        NativeBattleNodeSource source = before.State.NodeData!;
        byte[] bytes = source.Arena.Read(0, source.Length);
        NativeBattleRinoa87NodeStep next = Advance(before, fixture.Address, fixture.Resources, fixture.Packets);
        NativeBattleRinoa87NodeStep restoredNext = Advance(restored, fixture.Address, fixture.Resources, fixture.Packets);
        Assert.Same(source, next.Frame.State.NodeData);
        Assert.Equal(bytes, next.Frame.State.NodeData!.Arena.Read(0, source.Length));
        Assert.Equal(NativeBattleTestSnapshots.Json(next.Frame), NativeBattleTestSnapshots.Json(restoredNext.Frame));
        Assert.Equal(next.Random, restoredNext.Random);
        Assert.Equal(2, next.Result);
        Assert.NotSame(next.Frame.Memory.RetainedAuthority, restoredNext.Frame.Memory.RetainedAuthority);
        Assert.Same(restoredNext.Frame.Memory.RetainedAuthority, restoredNext.Frame.State.NodeData!.Arena.Working.RetainedAuthority);
    }

    [Fact]
    public void Foreign_source_remains_rejected_without_rebinding_or_native_mutation()
    {
        var fixture = Fixture(false);
        NativeBattleNodeSource foreign = new(fixture.Resources.Data);
        NativeBattleRetainedStorageAuthority authority = new();
        NativeBattleRetainedEffectStorage.AdmitStorage(foreign, authority);
        byte[] bytes = foreign.Arena.Read(0, foreign.Length);
        NativeBattleRinoa87Frame frame = fixture.Frame with { State = fixture.Frame.State with { NodeData = foreign } };
        Assert.Throws<InvalidOperationException>(() => Advance(frame, fixture.Address, fixture.Resources, fixture.Packets));
        Assert.Same(authority, foreign.Arena.Working.RetainedAuthority);
        Assert.Equal(bytes, foreign.Arena.Read(0, foreign.Length));
    }

    private static NativeBattleRinoa87NodeStep Advance(NativeBattleRinoa87Frame frame, uint address, NativeBattleRinoa87NodeResources resources, NativeBattleActionPacketsState packets)
    {
        using NativeBattleTaskOperation operation = new(frame.Memory, frame.Effect);
        NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattleRinoa87Inputs inputs = new(packets, identity, Model, _ => new([], []), (_, _) => throw new InvalidOperationException("Node ownership fixture cannot dispatch another leaf."), (_, _) => throw new InvalidOperationException("Node ownership fixture cannot reload a texture."));
        NativeBattleRinoa87NodeStep step = NativeBattleRinoa87Nodes.Advance(frame, address, resources, inputs, new(17), new(identity, 256, 160, 108), 0, NativeBattleLogicalOrdering.Clear(), new(0, 0, 1, 0), (_, _) => new(10, 20, 30));
        step = step with { Frame = step.Frame with { Memory = step.Frame.Memory with { Rinoa87 = step.Frame.State } } };
        operation.Commit();
        return step;
    }

    private static NativeBattleGraphicsModel Model(int actor) => new(actor, 0) { Transform = new(new(0, 0, 0), new(0, 0, 0), 0, 0, 4096), GeometryBounds52 = new(new(-1, -1, -1), new(1, 1, 1)) };

    private static (NativeBattleRinoa87Frame Frame, uint Address, NativeBattleRinoa87NodeResources Resources, NativeBattleActionPacketsState Packets) Fixture(bool existingSource)
    {
        // Component fixture for native node ownership, not installed Limit admission.
        byte[] resource = new byte[NativeBattleRinoa87NodeResources.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(resource.AsSpan(4), 0x1000);
        BinaryPrimitives.WriteUInt32LittleEndian(resource.AsSpan(8), 0x1158);
        BinaryPrimitives.WriteUInt32LittleEndian(resource.AsSpan(12), 0x12B0);
        NativeBattleRinoa87NodeResources resources = new(resource.ToImmutableArray());
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(0, 19, 0, -1, 1, 87, null));
        (packets, _) = packets.AppendContact(new(new(3, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        NativeBattleEffectHierarchy hierarchy = new([NativeBattleEffectTaskPool.Initialize(0x1000, 0x2000, 100, 1), NativeBattleEffectTaskPool.Initialize(0x3000, 0x4000, 676, 1)]);
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, 0x773A10, 100);
        hierarchy = root.Hierarchy.Write(root.Address, root.Hierarchy.Read(root.Address).WriteWord(90, 1));
        NativeBattleEffectHierarchyAppend child = hierarchy.Append8DC540(1, 0x779050, 676, root.Address, (_, _, _) => new(0, 3));
        hierarchy = child.Hierarchy.Write(child.Address, child.Hierarchy.Read(child.Address).WriteDword(48, NativeBattleRinoa87NodeResources.Address).WriteWord(668, 4));
        NativeBattleRinoa87State state = NativeBattleRinoa87Storage.FromColdProcess() with { Scene = new(NativeBattleEffectArena.CreateUnknown(84).Write(0, new byte[84])), NodeData = existingSource ? new(resources.Data) : null };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Rinoa87 = state };
        NativeBattleOriginalEffectState effect = new(87, 86, new(87, 1, 0), new(0, 1, 8, 0, 0, 0), null) { Pools = hierarchy.Pools };
        return (new(memory, effect, state), child.Address, resources, packets);
    }
}


