using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRinoa87EmissionTests
{
    [Fact]
    public void Impact_spawner_returns_completion_separately_from_pool_release_after_acknowledging_parent()
    {
        NativeBattleEffectHierarchy hierarchy = new(Enumerable.Range(0, NativeBattleRinoa87Controller.Descriptors.Length).Select(index => NativeBattleEffectTaskPool.Initialize(NativeBattleRinoa87Controller.Descriptors[index], NativeBattleRinoa87Controller.Banks[index], NativeBattleRinoa87Controller.Strides[index], NativeBattleRinoa87Controller.Capacities[index])).ToImmutableArray());
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, 0x773A10, 100);
        NativeBattleEffectHierarchyAppend child = root.Hierarchy.Append8DC540(6, 0x77E800, 152, root.Address, (_, _, _) => new(0, 3));
        hierarchy = child.Hierarchy;
        hierarchy = hierarchy.Write(child.Address, hierarchy.Read(child.Address).WriteByte(38, 1).WriteByte(41, 1));
        Assert.Equal((byte)1, hierarchy.Read(root.Address).ReadByte(40));
        NativeCrtRandomState random = new(7);
        NativeBattleRinoa87ParticleEmission step = NativeBattleRinoa87Emitters.Advance77E800(hierarchy, child.Address, random, new short[6].ToImmutableArray(), new short[6].ToImmutableArray(), (_, _, _) => throw new InvalidOperationException("Completed spawner cannot allocate children."));
        Assert.True(step.Completed);
        Assert.Equal((byte)0, step.Hierarchy.Read(root.Address).ReadByte(40));
        Assert.NotEqual((ushort)0, step.Hierarchy.Read(child.Address).Flags);
        Assert.Equal(random, step.Random);
    }
}
