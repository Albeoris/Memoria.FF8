namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAngelWing84SceneBirthTests
{
    [Theory]
    [InlineData(1, 4)]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    public void Phase_births_keep_original_pool_sizes_sources_and_root_parent_links(ushort phase, byte expectedChildren)
    {
        var fixture = NativeBattleAngelWing84ControllerTests.Fixture();
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        uint parent = hierarchy.Address(0, 0);
        int modelInitializations = 0;
        NativeBattleAngelWing84Frame frame = NativeBattleAngelWing84SceneBirth.Apply(fixture.Frame, new(phase, parent), fixture.Inputs, (current, address) =>
        {
            Assert.Equal(0x7961E0u, new NativeBattleEffectHierarchy(current.Effect.Pools).Read(address).Callback);
            modelInitializations++;
            return current;
        });
        hierarchy = new(frame.Effect.Pools);
        Assert.Equal(expectedChildren, hierarchy.Read(parent).ReadByte(40));
        Assert.Equal(phase == 1 ? 1 : 0, modelInitializations);
        foreach (NativeBattleEffectTaskPool pool in hierarchy.Pools.Skip(1))
        {
            foreach (NativeBattleModulePoolTask task in pool.Slots.Where(task => (task.Flags & 1) != 0))
            {
                Assert.Equal(parent, task.ReadDword(16));
                Assert.Equal(parent, task.ReadDword(24));
            }
        }
        if (phase is 2 or 3)
        {
            NativeBattleModulePoolTask nodes = Assert.Single(frame.Effect.Pools[3].Slots.Where(task => (task.Flags & 1) != 0));
            Assert.Equal(0x7913C0u, nodes.Callback);
            Assert.Equal(0x171DE1Cu, nodes.ReadDword(48));
            Assert.Equal((ushort)8, nodes.ReadWord(664));
            Assert.Equal((ushort)15, nodes.ReadWord(670));
        }
        if (phase is 1 or 3)
        {
            NativeBattleModulePoolTask program = Assert.Single(frame.Effect.Pools[4].Slots.Where(task => (task.Flags & 1) != 0));
            Assert.Equal(phase == 1 ? 0x78ECD0u : 0x791260u, program.Callback);
            Assert.Equal(phase == 1 ? 0x171F2B0u : 0x17217A8u, program.ReadDword(116));
            Assert.Equal(phase == 1 ? 1184u : 1928u, program.ReadDword(120));
            Assert.Equal((ushort)(phase == 1 ? 0 : 2), program.ReadWord(128));
            Assert.Equal((ushort)0, program.ReadWord(130));
        }
    }
}
