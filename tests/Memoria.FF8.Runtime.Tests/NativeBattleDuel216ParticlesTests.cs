namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel216ParticlesTests
{
    [Fact]
    public void ZeroCountStillCreatesOneAcceleratingBillboardWithSignedTruncatingAcceleration()
    {
        var fixture = Fixture(false);
        int queried = 0;
        NativeBattleDuel216ParticleBirth born = NativeBattleDuel216Particles.Spawn69A110(fixture.Frame, fixture.Pool, 0, new(1, 2, 3), _ =>
        {
            queried++;
            return new(10, 20, 30);
        });
        Assert.Equal(1, queried);
        NativeBattleModulePoolTask task = born.Pool.Slots[0];
        Assert.Equal(0x69A200u, task.Callback);
        Assert.Equal((ushort)11, task.ReadWord(12));
        Assert.Equal((ushort)22, task.ReadWord(14));
        Assert.Equal((ushort)33, task.ReadWord(16));
        Assert.Equal(new byte[] { 169, 93, 62 }, task.ReadBytes(20, 3));
        Assert.Equal(new byte[] { 10, 245, 249 }, task.ReadBytes(24, 3));
        Assert.Equal(After(fixture.Frame.Random, 3), born.Frame.Random);
        born.Pool.Validate();
    }

    [Fact]
    public void RadialBirthWritesWholeVelocityWordAndConsumesThreeRandomValues()
    {
        var fixture = Fixture(false);
        NativeBattleDuel216ParticleBirth born = NativeBattleDuel216Particles.Spawn699F80(fixture.Frame, fixture.Pool, 0, new(1, 2, 3), 0, 64, 0, 4096);
        Assert.Equal(0x69A060u, born.Pool.Slots[0].Callback);
        Assert.Equal((byte)0, born.Pool.Slots[0].ReadByte(21));
        Assert.Equal((ushort)4096, born.Pool.Slots[0].ReadWord(18));
        Assert.Equal(After(fixture.Frame.Random, 3), born.Frame.Random);
        Assert.Single(born.Pool.Slots.Where(task => (task.Flags & 1) != 0));
    }

    [Fact]
    public void MeshBirthKeepsWrapperChoiceAndConsumesNineDrawsWithUnwrittenPadding()
    {
        var fixture = Fixture(true);
        NativeBattleEffectTaskPool pool = NativeBattleTaskFixture.EditPool(fixture.Pool, data => data with { Slots = fixture.Pool.Slots.SetItem(0, fixture.Pool.Slots[0].MarkPayloadUnknown(34, 2)) });
        NativeBattleDuel216ParticleBirth born = NativeBattleDuel216Particles.Spawn69A2D0(fixture.Frame, pool, 0, new(1, 2, 3), 0, 20, 0, 64, 0, 4096);
        NativeBattleModulePoolTask task = born.Pool.Slots[0];
        Assert.Equal(0x69A3F0u, task.Callback);
        Assert.Equal(new byte[] { 0, 20, 0, 20 }, task.ReadBytes(24, 4));
        Assert.Equal((ushort)4096, task.ReadWord(32));
        Assert.Throws<NotSupportedException>(() => task.ReadWord(34));
        Assert.Equal(After(fixture.Frame.Random, 9), born.Frame.Random);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedAllocationConsumesOnlyTheNativePreallocationSelector(bool mesh)
    {
        var fixture = Fixture(mesh);
        NativeBattleEffectTaskPool pool = fixture.Pool;
        for (int index = 0; index < pool.Slots.Length; index++)
        {
            (pool, _) = pool.Append(mesh ? 0x69A3F0u : 0x69A200u);
        }
        NativeBattleDuel216ParticleBirth born = mesh ? NativeBattleDuel216Particles.Spawn69A2D0(fixture.Frame, pool, 0, new(0, 0, 0), 0, 20, 0, 64, 0, 4096) : NativeBattleDuel216Particles.Spawn69A110(fixture.Frame, pool, 0, new(0, 0, 0), _ => throw new InvalidOperationException());
        Assert.Equal(After(fixture.Frame.Random, mesh ? 1 : 0), born.Frame.Random);
        Assert.Equal(pool, born.Pool);
    }

    [Fact]
    public void FinalBillboardDrawStillMovesBeforeReleaseAndKeepsSignedByteVelocities()
    {
        var fixture = Fixture(false);
        (NativeBattleEffectTaskPool pool, int slot) = fixture.Pool.Append(0x69A060);
        NativeBattleModulePoolTask task = pool.Slots[slot].WriteWord(12, 10).WriteWord(14, 20).WriteWord(16, 30).WriteWord(18, 4096).WriteWord(20, 255).WriteByte(22, 128).WriteByte(23, 11);
        pool = NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.SetItem(slot, task) });
        NativeBattleDuel216ParticleServices services = new((frame, draw) =>
        {
            Assert.Equal(0x113C1ACu, draw.Source);
            Assert.Equal(new NativeBattleVector(10, 20, 30), draw.Point);
            Assert.Equal((short)11, draw.Phase);
            Assert.Equal((short)-1024, draw.DepthDisplacement);
            return frame;
        }, (_, _) => throw new InvalidOperationException());
        NativeBattleDuel216Step step = NativeBattleDuel216Particles.Advance69A060(fixture.Frame, pool, slot, services);
        Assert.Equal(2, step.Result);
        Assert.Equal((ushort)6, step.Pool.Slots[slot].ReadWord(12));
        Assert.Equal((ushort)20, step.Pool.Slots[slot].ReadWord(14));
        Assert.Equal(unchecked((ushort)-482), step.Pool.Slots[slot].ReadWord(16));
        Assert.Equal((byte)12, step.Pool.Slots[slot].ReadByte(23));
    }

    [Theory]
    [InlineData(0x69A3F0u)]
    [InlineData(0x69A530u)]
    public void FinalMeshDrawUsesSharedOriginalMeshAndReleasesBeforeUnknownPhysics(uint callback)
    {
        var fixture = Fixture(true);
        (NativeBattleEffectTaskPool pool, int slot) = fixture.Pool.Append(callback);
        NativeBattleModulePoolTask task = pool.Slots[slot].WriteWord(12, 1).WriteWord(14, 2).WriteWord(16, 3).WriteByte(27, 1).WriteByte(28, 255).WriteByte(29, 128).WriteWord(32, 4096).MarkPayloadUnknown(18, 9);
        pool = NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.SetItem(slot, task) });
        NativeBattleDuel216ParticleServices services = new((_, _) => throw new InvalidOperationException(), (frame, draw) =>
        {
            Assert.Equal(0x1140AA4u, draw.Source);
            Assert.Equal((short)-16, draw.AngleX);
            Assert.Equal((short)-2048, draw.AngleY);
            Assert.Equal(1, draw.Local88.X);
            Assert.Equal(2, draw.Local88.Y);
            Assert.Equal(3, draw.Local88.Z);
            return frame;
        });
        NativeBattleDuel216Step step = NativeBattleDuel216Particles.Advance69A410(fixture.Frame, pool, slot, services);
        Assert.Equal(2, step.Result);
        Assert.Equal((byte)0, step.Pool.Slots[slot].ReadByte(27));
        Assert.Equal((ushort)1, step.Pool.Slots[slot].ReadWord(12));
        Assert.Throws<NotSupportedException>(() => step.Pool.Slots[slot].ReadWord(18));
    }

    private static NativeCrtRandomState After(NativeCrtRandomState random, int count)
    {
        for (int index = 0; index < count; index++)
        {
            random = random.Draw(out _);
        }
        return random;
    }
    private static (NativeBattleDuel216Frame Frame, NativeBattleEffectTaskPool Pool) Fixture(bool mesh)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(mesh ? NativeBattleDuel216Controller.Particles : NativeBattleDuel216Controller.Small, checked(0x20DFAB8u + (mesh ? 8208u : 10512u)), mesh ? (ushort)36 : (ushort)28, mesh ? (ushort)64 : (ushort)32);
        NativeBattleOriginalEffectState effect = new(216, 215, new(216, 0x6959E0, 0x6959F0), new(0, 0, 8, 0, 0, 0), NativeBattleDuel216Controller.Root) { Pools = [pool] };
        return (new(NativeBattleEffectMemory.FromColdProcess(), effect, NativeBattleFileStaging.Unknown(), NativeCrtRandomState.FromColdProcess()), pool);
    }
}
