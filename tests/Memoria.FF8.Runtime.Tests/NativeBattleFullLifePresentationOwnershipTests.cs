using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleFullLifePresentationOwnershipTests
{
    [Fact]
    public void Full_life_context_and_geometry_births_are_presentation_only_and_independent_after_restore()
    {
        var fixture = NativeBattleRestorationNodesTests.Fixture(27);
        string before = NativeBattleTestSnapshots.Json(fixture.Frame);
        NativeBattleRestorationFrame first = NativeBattleTestSnapshots.RestoreJson<NativeBattleRestorationFrame>(before)!;
        NativeBattleRestorationFrame second = NativeBattleTestSnapshots.RestoreJson<NativeBattleRestorationFrame>(before)!;
        using NativeBattleRestorationNodeMemory memory = new(first, fixture.Task);
        using NativeBattleRestorationNodeMemory detached = new(second, fixture.Task);
        int row = first.State.Geometry!.Value;
        NativeBattlePilotVisualIdentity task = NativeBattlePilotVisualLifetimes.Identity(27, new NativeBattleEffectHierarchy(first.Effect.Pools).Read(fixture.Task));
        NativeBattlePilotVisualIdentity otherTask = NativeBattlePilotVisualLifetimes.Identity(27, new NativeBattleEffectHierarchy(second.Effect.Pools).Read(fixture.Task));
        memory.AllocatedPilotGeometry(row);
        NativeBattlePilotVisualIdentity original = memory.PilotGeometryIdentity(task, row, 0);
        NativeBattlePilotVisualIdentity instance = memory.PilotGeometryIdentity(task, row, 1);
        memory.AllocatedPilotGeometry(row);
        NativeBattlePilotVisualIdentity reborn = memory.PilotGeometryIdentity(task, row, 0);
        detached.AllocatedPilotGeometry(row);
        NativeBattlePilotVisualIdentity independent = detached.PilotGeometryIdentity(otherTask, row, 0);
        Assert.Equal(NativeBattleRestorationProfile.FullLife.Descriptors[2], original.Bank);
        Assert.Equal(original.Generation, reborn.Generation);
        Assert.NotEqual(original.ResourceGeneration, reborn.ResourceGeneration);
        Assert.Equal(original.ResourceGeneration, instance.ResourceGeneration);
        Assert.NotEqual(original.Primitive, instance.Primitive);
        Assert.NotEqual(reborn.Generation, independent.Generation);
        Assert.NotEqual(reborn.ResourceGeneration, independent.ResourceGeneration);
        Assert.Equal(before, NativeBattleTestSnapshots.Json(memory.Flush()));
        Assert.Equal(before, NativeBattleTestSnapshots.Json(detached.Flush()));
    }
}
