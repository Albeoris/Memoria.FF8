namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRinoa89SpritesTests
{
    [Fact]
    public void SpriteBirthWritesLinkedTasksAndConsumesSevenDrawsPerMeshParticle()
    {
        var fixture = Fixture();
        NativeBattleVector point = new(11, -22, 33);
        NativeBattleRinoa89SpriteBirth born = NativeBattleRinoa89Controller.SpawnSprites6FD1B0(fixture.Frame, fixture.Pool, point, 2);
        born.Pool.Validate();
        Assert.Equal(0x6FD340u, born.Pool.Slots[0].Callback);
        Assert.Equal(0x6FD3C0u, born.Pool.Slots[1].Callback);
        Assert.Equal(0x6FD3C0u, born.Pool.Slots[2].Callback);
        Assert.Equal((ushort)11, born.Pool.Slots[2].ReadWord(12));
        Assert.Equal(unchecked((ushort)-22), born.Pool.Slots[2].ReadWord(14));
        Assert.Equal(new byte[] { 15 }, born.Pool.Slots[2].ReadBytes(31, 1));
        NativeCrtRandomState random = fixture.Frame.Random;
        for (int index = 0; index < 14; index++)
        {
            random = random.Draw(out _);
        }
        Assert.Equal(random, born.Frame.Random);
    }

    [Fact]
    public void FullPoolDoesNotConsumeParticleRandomness()
    {
        var fixture = Fixture();
        NativeBattleEffectTaskPool pool = fixture.Pool;
        for (int index = 0; index < 32; index++)
        {
            (pool, _) = pool.Append(0x6FD3C0);
        }
        NativeBattleRinoa89SpriteBirth born = NativeBattleRinoa89Controller.SpawnSprites6FD1B0(fixture.Frame, pool, new(0, 0, 0), 2);
        Assert.Equal(pool, born.Pool);
        Assert.Equal(fixture.Frame.Random, born.Frame.Random);
    }

    [Fact]
    public void FinalMeshDrawReleasesBeforeReadingUnknownVelocities()
    {
        var fixture = Fixture();
        (NativeBattleEffectTaskPool pool, int slot) = fixture.Pool.Append(0x6FD3C0);
        NativeBattleModulePoolTask task = pool.Slots[slot].WriteWord(12, 10).WriteWord(14, 20).WriteWord(16, 30).WriteBytes(26, [255, 128]).WriteBytes(31, [1]).MarkPayloadUnknown(20, 6);
        pool = pool with { Slots = pool.Slots.SetItem(slot, task) };
        int draws = 0;
        NativeBattleRinoa89SpriteServices services = new((_, _, _, _, _) => throw new InvalidOperationException(), (frame, pose, _) =>
        {
            draws++;
            Assert.Equal(10, pose.X);
            Assert.Equal(NativeBattleGfMatrices.Euler56CD50(new(-16, -2048, 0)).Rotation, pose.Rotation);
            return frame;
        });
        NativeBattleRinoa89TaskStep result = NativeBattleRinoa89Controller.AdvanceSprite6FD3C0(fixture.Frame, pool, slot, services);
        Assert.Equal(1, draws);
        Assert.Equal(2, result.Result);
        Assert.Equal(new byte[] { 0 }, result.Pool.Slots[slot].ReadBytes(31, 1));
        Assert.Equal((ushort)10, result.Pool.Slots[slot].ReadWord(12));
    }

    [Theory]
    [InlineData(-10, 3, -7, -17)]
    [InlineData(-2, 3, 1, -3)]
    public void MeshParticleBranchesOnUpdatedSignedY(short initialY, short velocityY, short expectedY, short expectedVelocityY)
    {
        var fixture = Fixture();
        (NativeBattleEffectTaskPool pool, int slot) = fixture.Pool.Append(0x6FD3C0);
        NativeBattleModulePoolTask task = pool.Slots[slot].WriteWord(12, 32767).WriteWord(14, unchecked((ushort)initialY)).WriteWord(16, 30).WriteWord(20, 1).WriteWord(22, unchecked((ushort)velocityY)).WriteWord(24, 2).WriteBytes(26, [255, 128, 236, 2, 255, 2]);
        pool = pool with { Slots = pool.Slots.SetItem(slot, task) };
        NativeBattleRinoa89SpriteServices services = new((_, _, _, _, _) => throw new InvalidOperationException(), (frame, _, _) => frame);
        NativeBattleRinoa89TaskStep result = NativeBattleRinoa89Controller.AdvanceSprite6FD3C0(fixture.Frame, pool, slot, services);
        task = result.Pool.Slots[slot];
        Assert.Equal(unchecked((ushort)short.MinValue), task.ReadWord(12));
        Assert.Equal(unchecked((ushort)expectedY), task.ReadWord(14));
        Assert.Equal(unchecked((ushort)expectedVelocityY), task.ReadWord(22));
        Assert.Equal(new byte[] { 1, 127 }, task.ReadBytes(26, 2));
    }

    [Fact]
    public void BillboardDrawsPhaseElevenBeforeIncrementAndRelease()
    {
        var fixture = Fixture();
        NativeBattleRinoa89SpriteBirth born = NativeBattleRinoa89Controller.SpawnSprites6FD1B0(fixture.Frame, fixture.Pool, new(1, 2, 3), 0);
        NativeBattleEffectTaskPool pool = born.Pool with { Slots = born.Pool.Slots.SetItem(0, born.Pool.Slots[0].WriteBytes(31, [11])) };
        int draws = 0;
        NativeBattleRinoa89SpriteServices services = new((frame, point, scale, displacement, phase) =>
        {
            draws++;
            Assert.Equal(new NativeBattleVector(1, 2, 3), point);
            Assert.Equal((ushort)4096, scale);
            Assert.Equal((short)-1024, displacement);
            Assert.Equal((short)11, phase);
            return frame;
        }, (_, _, _) => throw new InvalidOperationException());
        NativeBattleRinoa89TaskStep result = NativeBattleRinoa89Controller.AdvanceSprite6FD340(born.Frame, pool, 0, services);
        Assert.Equal(1, draws);
        Assert.Equal(2, result.Result);
        Assert.Equal(new byte[] { 12 }, result.Pool.Slots[0].ReadBytes(31, 1));
    }

    private static (NativeBattleRinoa89Frame Frame, NativeBattleEffectTaskPool Pool) Fixture()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(NativeBattleRinoa89Controller.Sprites, 0x20DFAB8 + 3136, 48, 32);
        NativeBattleOriginalEffectState effect = new(89, 88, new(89, 0x6F9CF0, 0x5755E0), new(0, 0, 8, 0, 0, 0), NativeBattleRinoa89Controller.Sprites) { Pools = [pool] };
        return (new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, effect, NativeBattleFileStaging.Unknown(), NativeCrtRandomState.FromColdProcess()), pool);
    }
}

