namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGroup25RecoveryOwnershipTests
{
    [Theory]
    [InlineData((ushort)37)]
    [InlineData((ushort)61)]
    public void Alias_node_capture_keeps_context_instance_and_rebirth_ownership_separate_from_native_storage(ushort effect)
    {
        NativeBattleCuragaFrame frame;
        uint address;
        if (effect == 61)
        {
            var fixture = NativeBattleActorDirectorGenericTests.Fixture(effect);
            frame = fixture.Frame;
            address = fixture.Address;
        }
        else
        {
            var fixture = NativeBattleDrinkMagic37ControllerTests.Fixture();
            NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
            NativeBattleEffectHierarchyAppend context = hierarchy.Append8DC540(2, NativeBattleDrinkMagic37Profile.GenericCallback, 676, hierarchy.Address(0, 0), (_, _, _) => new(0, 3));
            NativeBattleCuragaFrame generic = fixture.Frame.ToGeneric();
            frame = generic with { Effect = generic.Effect with { Pools = context.Hierarchy.Pools }, State = generic.State with { Auxiliary25F8 = 0x12000, Auxiliary16F80 = 0x12438 } };
            address = context.Address;
        }
        string before = NativeBattleTestSnapshots.Json(frame);
        NativeBattleCuragaFrame first = NativeBattleTestSnapshots.RestoreJson<NativeBattleCuragaFrame>(before)!;
        NativeBattleCuragaFrame second = NativeBattleTestSnapshots.RestoreJson<NativeBattleCuragaFrame>(before)!;
        using NativeBattleCuragaNodeMemory memory = new(first, address);
        using NativeBattleCuragaNodeMemory detached = new(second, address);
        int node = first.State.Auxiliary16F80!.Value;
        NativeBattlePilotVisualIdentity task = NativeBattlePilotVisualLifetimes.Identity(effect, new NativeBattleEffectHierarchy(first.Effect.Pools).Read(address));
        NativeBattlePilotVisualIdentity otherTask = NativeBattlePilotVisualLifetimes.Identity(effect, new NativeBattleEffectHierarchy(second.Effect.Pools).Read(address));
        memory.AllocatedPilotGeometry(node);
        NativeBattlePilotVisualIdentity original = memory.PilotGeometryIdentity(task, node, 0);
        NativeBattlePilotVisualIdentity instance = memory.PilotGeometryIdentity(task, node, 1);
        memory.AllocatedPilotGeometry(node);
        NativeBattlePilotVisualIdentity reborn = memory.PilotGeometryIdentity(task, node, 0);
        detached.AllocatedPilotGeometry(node);
        NativeBattlePilotVisualIdentity independent = detached.PilotGeometryIdentity(otherTask, node, 0);
        Assert.Equal(original.Generation, reborn.Generation);
        Assert.NotEqual(original.ResourceGeneration, reborn.ResourceGeneration);
        Assert.Equal(original.ResourceGeneration, instance.ResourceGeneration);
        Assert.NotEqual(original.Primitive, instance.Primitive);
        Assert.NotEqual(reborn.Generation, independent.Generation);
        Assert.NotEqual(reborn.ResourceGeneration, independent.ResourceGeneration);
        Assert.Equal(before, NativeBattleTestSnapshots.Json(memory.Flush()));
        Assert.Equal(before, NativeBattleTestSnapshots.Json(detached.Flush()));
    }

    [Fact]
    public void Effect57_restoration_node_capture_preserves_detached_arena_and_actual_context_birth()
    {
        var fixture = NativeBattleRestorationControllerTests.Fixture(57);
        NativeBattleRestorationProfile profile = NativeBattleRestorationProfile.ForEffect(57);
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        NativeBattleEffectHierarchyAppend context = hierarchy.Append8DC540(2, profile.GenericCallback, 676, hierarchy.Address(0, 0), (_, _, _) => new(0, 3));
        NativeBattleRestorationFrame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = context.Hierarchy.Pools }, State = fixture.Frame.State with { Emitters = 0x12000, Geometry = 0x12000 + profile.EmitterBytes } };
        string before = NativeBattleTestSnapshots.Json(frame);
        NativeBattleRestorationFrame first = NativeBattleTestSnapshots.RestoreJson<NativeBattleRestorationFrame>(before)!;
        NativeBattleRestorationFrame second = NativeBattleTestSnapshots.RestoreJson<NativeBattleRestorationFrame>(before)!;
        using NativeBattleRestorationNodeMemory memory = new(first, context.Address);
        using NativeBattleRestorationNodeMemory detached = new(second, context.Address);
        int node = first.State.Geometry!.Value;
        NativeBattlePilotVisualIdentity task = NativeBattlePilotVisualLifetimes.Identity(57, new NativeBattleEffectHierarchy(first.Effect.Pools).Read(context.Address));
        NativeBattlePilotVisualIdentity otherTask = NativeBattlePilotVisualLifetimes.Identity(57, new NativeBattleEffectHierarchy(second.Effect.Pools).Read(context.Address));
        memory.AllocatedPilotGeometry(node);
        detached.AllocatedPilotGeometry(node);
        NativeBattlePilotVisualIdentity original = memory.PilotGeometryIdentity(task, node, 0);
        NativeBattlePilotVisualIdentity independent = detached.PilotGeometryIdentity(otherTask, node, 0);
        Assert.Equal(profile.Descriptors[2], original.Bank);
        Assert.NotEqual(original.Generation, independent.Generation);
        Assert.NotEqual(original.ResourceGeneration, independent.ResourceGeneration);
        Assert.Equal(before, NativeBattleTestSnapshots.Json(memory.Flush()));
        Assert.Equal(before, NativeBattleTestSnapshots.Json(detached.Flush()));
    }
}
