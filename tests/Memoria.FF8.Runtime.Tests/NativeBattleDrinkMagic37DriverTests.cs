using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDrinkMagic37DriverTests
{
    [Fact]
    public void Scheduler_captures_bone12_preserves_anchor_W_and_uses_exact_late_birth_ages_and_CRT_order()
    {
        var fixture = Fixture(0x85BBD0);
        NativeBattleEffectHierarchy hierarchy = fixture.Hierarchy;
        NativeCrtRandomState random = new(1);
        ImmutableArray<byte> anchor = [0, 0, 0, 0, 0, 0, 0xCD, 0xAB];
        int[] counts = new int[63];
        for (int age = 0; age <= 62; age++)
        {
            NativeBattleDrinkMagic37ParticleStep step = NativeBattleDrinkMagic37Particles.Advance(hierarchy, fixture.Address, random, anchor, Inputs());
            hierarchy = step.Hierarchy;
            random = step.Random;
            anchor = step.CapturedAnchor8;
            counts[age] = hierarchy.Pools[3].Slots.Count(task => task.Callback == 0x85E930);
            if (age == 0)
            {
                NativeBattleDrinkMagic37Anchor captured = Assert.Single(step.Operations.OfType<NativeBattleDrinkMagic37Anchor>());
                Assert.Equal((short)0, captured.Distance);
                Assert.Equal((short)-56, BinaryPrimitives.ReadInt16LittleEndian(anchor.AsSpan().Slice(2)));
                Assert.Equal((ushort)0xABCD, BinaryPrimitives.ReadUInt16LittleEndian(anchor.AsSpan().Slice(6)));
                Assert.Single(hierarchy.Pools[4].Slots, task => task.Callback == 0x85C1F0);
            }
        }
        Assert.Equal(0, counts[39]);
        Assert.Equal(1, counts[40]);
        Assert.Equal(2, counts[42]);
        Assert.Equal(3, counts[44]);
        Assert.Equal(5, counts[46]);
        NativeBattleModulePoolTask[] trajectories = hierarchy.Pools[3].Slots.Where(task => task.Callback == 0x85E930).ToArray();
        Assert.Equal(new ushort[] { 0, 1, 2, 3, 4 }, trajectories.Select(task => task.ReadWord(0x120)));
        Assert.Equal(new ushort[] { 0, 0, 0, 0, 1 }, trajectories.Select(task => task.ReadWord(0x122)));
        Assert.Equal(49, hierarchy.Pools[3].Slots.Count(task => task.Callback == 0x85BD50));
        NativeCrtRandomState expected = new(1);
        for (int draw = 0; draw < 162; draw++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, random);
        Assert.Equal((byte)1, (byte)(hierarchy.Read(fixture.Address).ReadByte(0x26) & 1));
    }

    [Fact]
    public void Related_particle_uses_three_CRT_draws_and_the_two_extra_304_byte_velocity_words()
    {
        var fixture = Fixture(0x85BD50);
        NativeBattleDrinkMagic37ParticleStep initialized = NativeBattleDrinkMagic37Particles.Advance(fixture.Hierarchy, fixture.Address, new(1), new byte[8].ToImmutableArray(), Inputs());
        NativeCrtRandomState expected = new(1);
        expected = expected.Draw(out _).Draw(out _).Draw(out ushort third);
        NativeBattleModulePoolTask task = initialized.Hierarchy.Read(fixture.Address);
        Assert.Equal(expected, initialized.Random);
        Assert.Equal((ushort)(0xA00 + (third & 0x1FF)), task.ReadWord(0x12C));
        Assert.Equal((ushort)0, task.ReadWord(0x12E));
        Assert.Equal(0x15D9624u, task.ReadDword(0x4C));
        Assert.Equal((ushort)10, task.ReadWord(0x52));
        task = task.WriteWord(0x12C, 0xFE0).WriteWord(0x12E, 0x10);
        NativeBattleDrinkMagic37ParticleStep done = NativeBattleDrinkMagic37Particles.Advance(initialized.Hierarchy.Write(fixture.Address, task), fixture.Address, initialized.Random, initialized.CapturedAnchor8, Inputs());
        Assert.Equal((ushort)0x40, done.Hierarchy.Read(fixture.Address).ReadWord(0x12E));
        Assert.Equal((ushort)0x1020, done.Hierarchy.Read(fixture.Address).ReadWord(0x12C));
        Assert.Equal(2, done.Result);
        Assert.DoesNotContain(done.Operations, operation => operation is NativeBattleDrawStock15HeadSprite);
        Assert.Equal(initialized.Random, done.Random);
    }

    private static NativeBattleDrinkMagic37ParticleInputs Inputs()
    {
        return new(0, (_, _, _) => new(0, 3), (_, _) => new(100, 200, 300), _ => 0, task => task.WriteDword(0x1C, 0).WriteDword(0x20, 0));
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
