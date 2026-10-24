using static Memoria.FF8.Runtime.Tests.NativeBattleEffect140LowerFixture;
using static Memoria.FF8.Runtime.NativeBattleEffect140Controller;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect140ParticleTests
{
    [Fact]
    public void Successful_birth_uses_five_masked_random_operands_and_retains_unwritten_payload()
    {
        NativeBattleEffect140Frame frame = WithPool(Frame());
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[0];
        frame = frame with { Effect = frame.Effect with { Pools = [pool with { Slots = pool.Slots.SetItem(0, pool.Slots[0].WriteWord(26, 0x55AA)) }] } };
        NativeCrtRandomState random = frame.Random.Draw(out ushort x).Draw(out ushort z).Draw(out ushort y).Draw(out ushort vx).Draw(out ushort vz);
        NativeBattleEffect140FactoryStep result = NativeBattleEffect140Particles.Spawn6A7CE0(frame, 1234, 512, 1024, -5, 256, null!, null!);
        NativeBattleModulePoolTask task = Read(result.Frame, result.Address);
        Assert.Equal(random, result.Frame.Random);
        Assert.Equal(unchecked((ushort)((x & 511) - 256)), task.ReadWord(12));
        Assert.Equal(unchecked((ushort)(1234 + (z & 1023) - 512)), task.ReadWord(16));
        Assert.Equal(unchecked((ushort)(-5 - (y & 255))), task.ReadWord(14));
        Assert.Equal(unchecked((ushort)((vx & 8191) - 4096)), task.ReadWord(20));
        Assert.Equal(unchecked((ushort)((vz & 8191) - 4096)), task.ReadWord(22));
        Assert.Equal(0, task.ReadWord(18));
        Assert.Equal(0, task.ReadWord(24));
        Assert.Equal(0, task.ReadWord(26));
        Assert.Equal(0x6A7D90u, task.Callback);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Paused_or_full_factory_does_not_draw_random(bool paused)
    {
        NativeBattleEffect140Frame frame = WithPool(Frame());
        if (paused)
        {
            frame = frame with { State = frame.State.WriteDword(0x2517AAC, 1) };
        }
        else
        {
            (frame, _) = Append(frame, 0x2517AC8, 0x6A7D90);
        }
        NativeBattleEffect140FactoryStep result = NativeBattleEffect140Particles.Spawn6A7CE0(frame, 0, 512, 1024, 0, 512, null!, null!);
        Assert.Equal(0u, result.Address);
        Assert.Equal(frame.Random, result.Frame.Random);
        Assert.Equal(frame.Effect, result.Frame.Effect);
    }
}
