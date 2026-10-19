using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel212ParticlesTests
{
    [Fact]
    public void SpriteBirthRetainsPostDecrementCountAndCrtDrawOrderAcrossSnapshot()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(NativeBattleDuel212Controller.Sprites, 0x20DFAB8 + 5584, 24, 64);
        NativeBattleDuel212Frame frame = Frame(pool);
        NativeBattleDuel212Frame restored = JsonSerializer.Deserialize<NativeBattleDuel212Frame>(JsonSerializer.Serialize(frame))!;
        NativeBattleDuel212Step first = Spawn(frame, pool);
        NativeBattleDuel212Step second = Spawn(restored, restored.Effect.Pools[0]);
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
        first.Pool.Validate();
        Assert.Equal(2, first.Pool.Slots.Count(task => (task.Flags & 1) != 0));
        Assert.Equal(1, first.Pool.Slots[0].Next);
        NativeCrtRandomState random = frame.Random;
        for (int index = 0; index < 8; index++)
        {
            random = random.Draw(out _);
        }
        Assert.Equal(random, first.Frame.Random);
        foreach (NativeBattleModulePoolTask task in first.Pool.Slots.Take(2))
        {
            Assert.InRange(unchecked((short)task.ReadWord(12)), -50, 49);
            Assert.InRange(task.ReadWord(18), 800, 1054);
            Assert.Equal((byte)16, task.ReadByte(23));
            Assert.Equal((byte)252, task.ReadByte(21));
        }
    }

    [Fact]
    public void ExhaustedPhysicalBankStopsWithoutDrawingRandomOrReplacingLinks()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(NativeBattleDuel212Controller.Sprites, 0x20DFAB8 + 5584, 24, 1);
        (pool, _) = pool.Append(0x6A3750);
        NativeBattleDuel212Frame frame = Frame(pool);
        NativeBattleDuel212Step result = Spawn(frame, pool);
        Assert.Same(pool, result.Pool);
        Assert.Equal(frame.Random, result.Frame.Random);
    }

    private static NativeBattleDuel212Step Spawn(NativeBattleDuel212Frame frame, NativeBattleEffectTaskPool pool) => NativeBattleDuel212Controller.SpawnSprites(frame, pool, 1, 16, new(0, -100, 20), 100, -50, 0, -4, 0, 255, 800, 0x6A3750);

    private static NativeBattleDuel212Frame Frame(NativeBattleEffectTaskPool pool)
    {
        NativeBattleEffectParameters parameters = new(0, 0, 8, 0, 0, 0);
        NativeBattleOriginalEffectState effect = new(212, 211, new(212, 0x6A0D20, 0x6A0D30), parameters, NativeBattleDuel212Controller.Root) { Pools = [pool] };
        return new(NativeBattleEffectMemory.FromColdProcess(), effect, NativeBattleFileStaging.Unknown(), NativeCrtRandomState.FromColdProcess());
    }
}
