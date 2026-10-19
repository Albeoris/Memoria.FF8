using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel274MeteorParticlesTests
{
    [Theory]
    [InlineData(false, 24)]
    [InlineData(true, 18)]
    public void SuccessfulBirthsConsumeExactRandomAndRetainThreeUnwrittenWords(bool grounded, int draws)
    {
        NativeBattleDuel274Frame frame = Frame();
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[0];
        pool = pool with { Slots = pool.Slots.SetItem(0, pool.Slots[0].MarkPayloadUnknown(26, 2).MarkPayloadUnknown(34, 2).MarkPayloadUnknown(42, 2)) };
        frame = frame with { Effect = frame.Effect with { Pools = [pool] } };
        NativeCrtRandomState expected = frame.Random;
        for (int index = 0; index < draws; index++)
        {
            expected = expected.Draw(out _);
        }
        frame = NativeBattleDuel274MeteorParticles.Emit68CA60(frame, new(100, -200, 300), 0x1234, 2, grounded);
        Assert.Equal(expected, frame.Random);
        NativeBattleModulePoolTask first = frame.Effect.Pools[0].Slots[0];
        Assert.Equal(0, first.ReadWord(18));
        Assert.Throws<NotSupportedException>(() => first.ReadWord(26));
        Assert.Throws<NotSupportedException>(() => first.ReadWord(34));
        Assert.Throws<NotSupportedException>(() => first.ReadWord(42));
        Assert.Equal(2, frame.Effect.Pools[0].Slots.Count(task => (task.Flags & 1) != 0));
        NativeBattleDuel274Frame restored = JsonSerializer.Deserialize<NativeBattleDuel274Frame>(JsonSerializer.Serialize(frame))!;
        Assert.Throws<NotSupportedException>(() => restored.Effect.Pools[0].Slots[0].ReadWord(42));
        if (grounded)
        {
            Assert.Equal(0, first.ReadWord(14));
            Assert.InRange(unchecked((short)first.ReadWord(22)), -191, -64);
        }
        else
        {
            Assert.InRange(unchecked((short)first.ReadWord(22)), 0, 31);
        }
    }

    [Fact]
    public void TerminalCallbackDrawsBeforeUpdatingGravityBounceAndRotation()
    {
        NativeBattleDuel274Frame frame = NativeBattleDuel274MeteorParticles.Emit68CA60(Frame(), new(100, -2, 300), 0, 1, true);
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[0];
        NativeBattleModulePoolTask task = pool.Slots[0].WriteWord(12, 100).WriteWord(14, unchecked((ushort)-2)).WriteWord(16, 300).WriteWord(18, 19).WriteWord(20, 3).WriteWord(22, unchecked((ushort)-10)).WriteWord(24, 4).WriteWord(28, 100).WriteWord(30, 200).WriteWord(32, 300).WriteWord(36, 1).WriteWord(38, 2).WriteWord(40, 3);
        pool = pool with { Slots = pool.Slots.SetItem(0, task) };
        frame = frame with { Effect = frame.Effect with { Pools = [pool] } };
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        int calls = 0;
        NativeBattleDuel274TaskStep step = NativeBattleDuel274MeteorParticles.Advance68D3B0(frame, 0, view, (current, address, matrix, flags, fade, far) =>
        {
            calls++;
            Assert.Equal(0x10E9360u, address);
            Assert.Equal((100, -2, 300), (matrix.X, matrix.Y, matrix.Z));
            Assert.Equal(0u, flags);
            Assert.Null(fade);
            return current;
        });
        Assert.Equal(1, calls);
        Assert.Equal(2, step.Result);
        NativeBattleModulePoolTask after = step.Pool.Slots[0];
        Assert.Equal(new ushort[] { 103, 0, 304 }, new[] { after.ReadWord(12), after.ReadWord(14), after.ReadWord(16) });
        Assert.Equal(unchecked((ushort)-5), after.ReadWord(22));
        Assert.Equal(new ushort[] { 101, 202, 303 }, new[] { after.ReadWord(28), after.ReadWord(30), after.ReadWord(32) });
        Assert.Equal(20, after.ReadWord(18));
    }

    private static NativeBattleDuel274Frame Frame()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2508478, NativeBattleEffectNodeMemory.ArenaAddress + 0x4000, 44, 32);
        NativeBattleEffectMemory memory = NativeBattleEffectTaskMemory.RevokeArenaOwnership(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, [pool]);
        return new(memory, new(274, 273, new(274, 0x6897E0, 0x6897F0), new(0, 0, 8, 0, 0, 0), 0) { Pools = [pool] }, NativeBattleFileStaging.Unknown(), new(1));
    }
}
