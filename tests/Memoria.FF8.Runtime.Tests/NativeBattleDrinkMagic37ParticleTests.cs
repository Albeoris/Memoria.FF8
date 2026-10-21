using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDrinkMagic37ParticleTests
{
    [Theory]
    [InlineData(0, 0x800)]
    [InlineData(1, 0x400)]
    public void Trajectory_uses_two_CRT_draws_full_captured_anchor_and_its_own_selector_rotation(ushort selector, int angleBase)
    {
        var fixture = Fixture(0x85E930);
        NativeBattleModulePoolTask task = fixture.Hierarchy.Read(fixture.Address).WriteWord(0x120, selector).WriteWord(0x72, 0xABCD).WriteWord(0x12C, 0xBEEF).WriteWord(0x12E, 0x9876);
        NativeCrtRandomState expected = new(1);
        expected = expected.Draw(out ushort first).Draw(out ushort second);
        NativeBattleDrawStock15Jitter initialized = NativeBattleDrinkMagic37Particles.InitializeTrajectory(task, [120, 0, 240, 0, 104, 1, 0xCD, 0xAB], new(1));
        Assert.Equal(expected, initialized.Random);
        Assert.Equal((ushort)(first & 0xFFF), initialized.Task.ReadWord(0x126));
        Assert.Equal((ushort)0xABCD, initialized.Task.ReadWord(0x86));
        Assert.Equal((ushort)0xABCD, initialized.Task.ReadWord(0x72));
        Assert.Equal((ushort)0xBEEF, initialized.Task.ReadWord(0x12C));
        Assert.Equal((ushort)0x9876, initialized.Task.ReadWord(0x12E));
        Assert.Equal(0x15D92F4u, initialized.Task.ReadDword(0x4C));
        NativeBattleDrawStock15Rotate rotate = Assert.Single(initialized.Operations.OfType<NativeBattleDrawStock15Rotate>());
        Assert.Equal((short)(angleBase + (second & 0x3FF)), rotate.Angle);
        Assert.DoesNotContain(initialized.Operations, operation => operation is NativeBattleDrawStock15Anchor);
    }

    [Fact]
    public void Shared_leaf_appends_a_304_byte_sparkle_and_retains_layout_specific_suffix_on_the_parent()
    {
        var fixture = Fixture(0x85E930);
        NativeBattleModulePoolTask task = fixture.Hierarchy.Read(fixture.Address).WriteByte(0x29, 1).WriteWord(0x12C, 0xBEEF).WriteWord(0x12E, 0x9876);
        NativeBattleEffectHierarchy hierarchy = fixture.Hierarchy.Write(fixture.Address, task);
        NativeBattleDrawStock15ParticleStep step = NativeBattleDrawStock15Particles.AdvanceDrinkMagic37Leaf(hierarchy, fixture.Address, new(1), Inputs(), []);
        NativeBattleModulePoolTask sparkle = Assert.Single(step.Hierarchy.Pools[3].Slots, slot => slot.Callback == 0x85F060);
        Assert.Equal(292, sparkle.Payload.Length);
        Assert.Equal((ushort)0, sparkle.ReadWord(0x12C));
        Assert.Equal((ushort)0, sparkle.ReadWord(0x12E));
        Assert.Equal((ushort)0xBEEF, step.Hierarchy.Read(fixture.Address).ReadWord(0x12C));
        Assert.Equal((ushort)0x9876, step.Hierarchy.Read(fixture.Address).ReadWord(0x12E));
        Assert.Equal(new NativeCrtRandomState(1), step.Random);
        NativeBattleEffectHierarchy restored = JsonSerializer.Deserialize<NativeBattleEffectHierarchy>(JsonSerializer.Serialize(step.Hierarchy))!;
        uint sparkleAddress = step.Hierarchy.Address(3, step.Hierarchy.Pools[3].Slots.IndexOf(sparkle));
        NativeBattleDrawStock15ParticleStep next = NativeBattleDrawStock15Particles.AdvanceDrinkMagic37Leaf(step.Hierarchy, sparkleAddress, step.Random, Inputs(), []);
        NativeBattleDrawStock15ParticleStep restoredNext = NativeBattleDrawStock15Particles.AdvanceDrinkMagic37Leaf(restored, sparkleAddress, step.Random, Inputs(), []);
        Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(restoredNext));
        Assert.Equal(0x15D9504u, next.Hierarchy.Read(sparkleAddress).ReadDword(0x4C));
    }

    [Fact]
    public void Original_Draw_Stock_entry_cannot_consume_the_distinct_304_byte_bank()
    {
        var fixture = Fixture(0x85E930);
        Assert.Throws<InvalidOperationException>(() => NativeBattleDrawStock15Particles.Advance(fixture.Hierarchy, fixture.Address, new(1), Inputs()));
        Assert.Throws<NotSupportedException>(() => NativeBattleDrawStock15Particles.AdvanceDrinkMagic37Leaf(Fixture(0x85BBD0).Hierarchy, fixture.Address, new(1), Inputs(), []));
    }

    private static NativeBattleDrawStock15ParticleInputs Inputs()
    {
        return new(0, (_, _, _) => new(0, 3), _ => throw new InvalidOperationException("Drink Magic leaves must use their captured anchor."));
    }

    private static (NativeBattleEffectHierarchy Hierarchy, uint Address) Fixture(uint callback)
    {
        NativeBattleEffectHierarchy hierarchy = new(NativeBattleDrinkMagic37Profile.Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, NativeBattleDrinkMagic37Profile.RootCallback, 100);
        hierarchy = root.Hierarchy.Write(root.Address, root.Hierarchy.Read(root.Address).WriteDword(0x0C, 0x1D99A78));
        NativeBattleEffectHierarchyAppend target = hierarchy.Append8DC540(1, NativeBattleDrinkMagic37Profile.TargetCallback, 88, root.Address, (_, _, _) => new(0, 3));
        NativeBattleEffectHierarchyAppend particle = target.Hierarchy.Append8DC540(3, callback, 304, target.Address, (_, _, _) => new(0, 3));
        return (particle.Hierarchy, particle.Address);
    }
}
