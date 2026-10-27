using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel274ParticlesTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void EachVelocitySelectorConsumesThreeDrawsPerSuccessfulAppendAndPreservesUnusedSuffix(int mode)
    {
        NativeBattleDuel274Frame frame = Frame();
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[2];
        pool = NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.SetItem(0, pool.Slots[0].MarkPayloadUnknown(34, 2)) });
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(2, pool) } };
        NativeCrtRandomState expected = frame.Random;
        for (int draw = 0; draw < 6; draw++)
        {
            expected = expected.Draw(out _);
        }
        int reads = 0;
        frame = NativeBattleDuel274Particles.Emit68AEE0(frame, 241, 2, mode, 3, bone =>
        {
            Assert.Equal(241, bone);
            reads++;
            return new(100, 200, 300);
        });

        Assert.Equal(1, reads);
        Assert.Equal(expected, frame.Random);
        Assert.Equal(2, frame.Effect.Pools[2].Slots.Count(task => (task.Flags & 1) != 0));
        NativeBattleModulePoolTask first = frame.Effect.Pools[2].Slots[0];
        Assert.Equal(0, first.ReadWord(18));
        Assert.Equal(3, first.ReadWord(26));
        Assert.Throws<NotSupportedException>(() => first.ReadWord(34));
        NativeBattleDuel274Frame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleDuel274Frame>(NativeBattleTestSnapshots.Json(frame))!;
        Assert.Throws<NotSupportedException>(() => restored.Effect.Pools[2].Slots[0].ReadWord(34));
        if (mode == 3)
        {
            Assert.Equal(0, first.ReadWord(28));
            Assert.Equal(20, first.ReadWord(30));
            Assert.Equal(0, first.ReadWord(32));
        }
        else if (mode == 4)
        {
            Assert.Equal(new ushort[] { 0, 0, 0 }, new[] { first.ReadWord(20), first.ReadWord(22), first.ReadWord(24) });
            Assert.Equal(unchecked((ushort)-20), first.ReadWord(30));
        }
        else
        {
            Assert.Equal(unchecked((ushort)(-unchecked((short)first.ReadWord(22)) >> 3)), first.ReadWord(30));
        }
    }

    [Fact]
    public void FullBankStillReadsBoneButConsumesNoRandomAndZeroCountFillsPhysicalCapacity()
    {
        NativeBattleDuel274Frame source = Frame();
        NativeBattleDuel274Frame full = NativeBattleDuel274Particles.Emit68AEE0(source, 21, 0, 3, 3, _ => new(0, 0, 0));
        Assert.Equal(64, full.Effect.Pools[2].Slots.Count(task => (task.Flags & 1) != 0));
        int reads = 0;
        NativeBattleDuel274Frame unchanged = NativeBattleDuel274Particles.Emit68AEE0(full, 21, 16, 4, 4, _ =>
        {
            reads++;
            return new(1, 2, 3);
        });
        Assert.Equal(1, reads);
        Assert.Equal(full.Random, unchanged.Random);
        Assert.Equal(NativeBattleTestSnapshots.Json(full.Effect.Pools), NativeBattleTestSnapshots.Json(unchanged.Effect.Pools));
    }

    [Fact]
    public void DrawUsesCurrentCounterAndScaleBeforeSignedLifetimeTestAndSkipsTerminalMotion()
    {
        NativeBattleDuel274Frame frame = NativeBattleDuel274Particles.Emit68AEE0(Frame(), 21, 1, 3, 3, _ => new(100, 200, 300));
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[2];
        pool = NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.SetItem(0, pool.Slots[0].WriteWord(18, 13)) });
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(2, pool) } };
        NativeBattleModulePoolTask before = pool.Slots[0];
        int calls = 0;
        NativeBattleModulePoolTask preserved = NativeBattleStorageSnapshots.Capture(before);
        NativeBattleDuel274TaskStep step = NativeBattleDuel274Particles.Advance68B140(frame, 0, Resources(), (current, task, sprite, counter, size, depth) =>
        {
            Assert.Equal(before, task);
            Assert.Equal(3, sprite);
            Assert.Equal(13, counter);
            Assert.Equal(1024, size);
            Assert.Equal(-256, depth);
            calls++;
            return current;
        });

        Assert.Equal(1, calls);
        Assert.Equal(2, step.Result);
        Assert.Equal(14, step.Pool.Slots[0].ReadWord(18));
        Assert.Equal(before.ReadBytes(12, 6), step.Pool.Slots[0].ReadBytes(12, 6));
        Assert.Equal(before.ReadBytes(20, 14), step.Pool.Slots[0].ReadBytes(20, 14));
    }

    [Fact]
    public void CounterWrapUsesSignedComparisonAndMovesPositionBeforeAcceleration()
    {
        NativeBattleDuel274Frame frame = NativeBattleDuel274Particles.Emit68AEE0(Frame(), 21, 1, 4, 4, _ => new(100, 200, 300));
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[2];
        NativeBattleModulePoolTask before = pool.Slots[0].WriteWord(18, 0x7FFF).WriteWord(20, 7).WriteWord(22, unchecked((ushort)-9)).WriteWord(24, 11).WriteWord(28, 3).WriteWord(30, 4).WriteWord(32, unchecked((ushort)-5));
        pool = NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.SetItem(0, before) });
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(2, pool) } };
        NativeBattleModulePoolTask preserved = NativeBattleStorageSnapshots.Capture(before);
        NativeBattleDuel274TaskStep step = NativeBattleDuel274Particles.Advance68B140(frame, 0, Resources(), (current, _, _, _, _, _) => current);
        NativeBattleModulePoolTask after = step.Pool.Slots[0];

        Assert.Equal(0, step.Result);
        Assert.Equal(0x8000, after.ReadWord(18));
        Assert.Equal(unchecked((ushort)(preserved.ReadWord(12) + 7)), after.ReadWord(12));
        Assert.Equal(unchecked((ushort)(preserved.ReadWord(14) - 9)), after.ReadWord(14));
        Assert.Equal(unchecked((ushort)(preserved.ReadWord(16) + 11)), after.ReadWord(16));
        Assert.Equal(10, after.ReadWord(20));
        Assert.Equal(unchecked((ushort)-5), after.ReadWord(22));
        Assert.Equal(6, after.ReadWord(24));
    }

    private static NativeBattleDuel274Frame Frame()
    {
        NativeBattleOriginalEffectState effect = new(274, 273, new(274, 0x6897E0, 0x6897F0), new(0, 0, 8, 0, 0, 0) { Parameter80 = 6 }, 0x25084B8)
        {
            Pools = [NativeBattleEffectTaskPool.Initialize(0x25084B8, 0x25084C8, 24, 1), NativeBattleEffectTaskPool.Initialize(0x25084A8, NativeBattleEffectNodeMemory.ArenaAddress, 116, 1), NativeBattleEffectTaskPool.Initialize(0x2508498, NativeBattleEffectNodeMemory.ArenaAddress + 4096, 36, 64)]
        };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() };
        memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(memory, effect.Pools);
        return new(memory, effect, NativeBattleFileStaging.Unknown(), new(1));
    }

    private static NativeBattleDuel274Resources Resources()
    {
        byte[] data = new byte[NativeBattleDuel274Resources.EndAddress - NativeBattleDuel274Resources.Address];
        for (int index = 0; index < 5; index++)
        {
            int offset = checked((int)(0x110E3B8 - NativeBattleDuel274Resources.Address) + index * 8);
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(offset + 4), index == 3 ? (short)14 : (short)10);
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(offset + 6), 1024);
        }
        return new(data.ToImmutableArray(), []);
    }
}
