using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel112FinishTests
{
    [Theory]
    [InlineData(137, 0x021E3C78u, 0x32142850u)]
    [InlineData(139, 0x020F1E3Cu, 0x320A1428u)]
    [InlineData(143, 0x02000000u, 0x32000000u)]
    [InlineData(148, 0x020F1E3Cu, 0x320A1428u)]
    [InlineData(151, 0x021E3C78u, 0x32142850u)]
    public void FadesWriteRgbAndExplicitAlphaBytesWithoutReadingTheOldDestination(short phase, uint color40, uint color44)
    {
        Fixture fixture = new(phase);
        NativeBattleDuel112TaskStep step = NativeBattleDuel112Target.Finish6CBF90(fixture.Frame, 0, fixture.Services);
        Assert.Equal(color40, fixture.Actor.Color40);
        Assert.Equal(color44, fixture.Actor.Color44);
        Assert.Equal((byte)0, fixture.Actor.Alpha7);
        Assert.NotEqual(0, fixture.Actor.Flags0 & 0x800);
        Assert.Equal((ushort)(phase + 1), step.Pool.Slots[0].ReadWord(12));
        Assert.Equal(0, step.Result);
        if (phase == 143)
        {
            Assert.Equal(0xC, fixture.Actor.Flags0 & 0xC);
        }
        if (phase == 137)
        {
            NativeBattleModulePoolTask texture = Assert.Single(step.Frame.Effect.Pools[4].Slots.Where(task => (task.Flags & 1) != 0));
            Assert.Equal(0x6CD870u, texture.Callback);
            Assert.All(NativeBattleStorageSnapshots.Capture(texture).Payload, value => Assert.Equal((byte)0, value));
        }
    }

    [Fact]
    public void RestoreCopiesBothPositionDwordsAndTerminalReleaseUsesTheSavedOverlappingAlphaByte()
    {
        Fixture fixture = new(145);
        NativeBattleDuel112TaskStep step = NativeBattleDuel112Target.Finish6CBF90(fixture.Frame, 0, fixture.Services);
        Assert.Equal(new NativeBattleVector(-10, 11, -12), fixture.Actor.Transform!.Position28);
        Assert.Equal((short)13, fixture.Actor.Transform.Vertical34);
        Assert.Equal(0, fixture.Actor.Flags0 & 0xC);
        NativeBattleEffectTaskPool pool = step.Pool;
        pool = NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.SetItem(0, pool.Slots[0].WriteWord(12, 152)) });
        NativeBattleDuel112Frame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleDuel112Frame>(NativeBattleTestSnapshots.Json(step.Frame with { Effect = step.Frame.Effect with { Pools = step.Frame.Effect.Pools.SetItem(1, pool) } }))!;
        step = NativeBattleDuel112Target.Finish6CBF90(restored, 0, fixture.Services);
        Assert.Equal(2, step.Result);
        Assert.Equal((ushort)152, step.Pool.Slots[0].ReadWord(12));
        Assert.Equal(0x001E3C78u, fixture.Actor.Color40);
        Assert.Equal(0x32142850u, fixture.Actor.Color44);
        Assert.Equal((byte)0xAB, fixture.Actor.Alpha7);
        Assert.Equal(0, fixture.Actor.Flags0 & 0x1800);
    }

    [Fact]
    public void TextureTaskPublishesActualCompletionPointerAndWaitsAcrossSnapshotUntilTheWordChanges()
    {
        Fixture fixture = new(137);
        NativeBattleDuel112TaskStep closing = NativeBattleDuel112Target.Finish6CBF90(fixture.Frame, 0, fixture.Services);
        int calls = 0;
        NativeBattleDuel112TaskStep step = NativeBattleDuel112Target.Advance6CD870(closing.Frame, 0, (frame, destination, completion) =>
        {
            calls++;
            Assert.Equal(0x20E1940u, destination);
            Assert.Equal(0x20E0A0Eu, completion);
            NativeBattleEffectTaskPool bank = frame.Effect.Pools[4];
            bank = NativeBattleTaskFixture.EditPool(bank, data => data with { Slots = bank.Slots.SetItem(0, bank.Slots[0].WriteByte(14, 0)) });
            return frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(4, bank) } };
        });
        Assert.Equal(1, calls);
        Assert.Equal(0, step.Result);
        NativeBattleDuel112Frame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleDuel112Frame>(NativeBattleTestSnapshots.Json(step.Frame))!;
        step = NativeBattleDuel112Target.Advance6CD870(restored, 0, (_, _, _) => throw new InvalidOperationException("A pending texture task cannot enqueue twice."));
        Assert.Equal(0, step.Result);
        NativeBattleEffectTaskPool pool = step.Pool;
        pool = NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.SetItem(0, pool.Slots[0].WriteByte(14, 1)) });
        step = NativeBattleDuel112Target.Advance6CD870(step.Frame with { Effect = step.Frame.Effect with { Pools = step.Frame.Effect.Pools.SetItem(4, pool) } }, 0, (_, _, _) => throw new InvalidOperationException());
        Assert.Equal(2, step.Result);
        Assert.Equal((ushort)3, step.Pool.Slots[0].ReadWord(12));
    }

    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(153, 2, 153)]
    public void CounterComparisonUsesSignedWord(short phase, int result, ushort next)
    {
        Fixture fixture = new(phase);
        NativeBattleDuel112TaskStep step = NativeBattleDuel112Target.Finish6CBF90(fixture.Frame, 0, fixture.Services);
        Assert.Equal(result, step.Result);
        Assert.Equal(next, step.Pool.Slots[0].ReadWord(12));
    }

    private sealed class Fixture
    {
        public NativeBattleDuel112Frame Frame { get; }
        public NativeBattleGraphicsModel Actor { get; private set; } = new(0, 0x1C0F) { Transform = new(new(1, 2, 3), new(100, 200, 300), -400, 17, 4096) };
        public NativeBattleDuel112ActorServices Services { get; }

        public Fixture(short phase)
        {
            NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(0x2521960, 0x25218F8, 28, 1);
            NativeBattleEffectTaskPool target = NativeBattleEffectTaskPool.Initialize(0x2521950, 0x20DFAB8, 0xF48, 1);
            (target, _) = target.Append(0x6CBF90);
            NativeBattleModulePoolTask task = target.Slots[0].WriteWord(12, unchecked((ushort)phase)).WriteDword(48, 0xCC1E3C78).WriteDword(52, 0xAB142850).WriteDword(60, 0x000BFFF6).WriteDword(64, 0x000DFFF4);
            target = NativeBattleTaskFixture.EditPool(target, data => data with { Slots = target.Slots.SetItem(0, task) });
            NativeBattleEffectParameters parameters = new(0, 0, 8, 0, 0, 0);
            NativeBattleOriginalEffectState effect = new(112, 111, new(112, 0x6CBBE0, 0x6CBBF0), parameters, 0x2521960) { Pools = [root, target, NativeBattleEffectTaskPool.Initialize(0x2521940, 0x20E0F40, 28, 32), NativeBattleEffectTaskPool.Initialize(0x2521930, 0x20E0D00, 36, 16), NativeBattleEffectTaskPool.Initialize(0x2521920, 0x20E0A00, 48, 16)] };
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Duel112 = new(true, parameters, null) };
            Frame = new(memory, effect, NativeBattleFileStaging.Unknown().Write(0x2521918, new byte[4]).Write(0x25218E8, BitConverter.GetBytes(0x20DFAB8u)), new(1));
            Services = new(_ => Actor, (_, model) => Actor = model, _ => throw new InvalidOperationException(), (_, _, _) => throw new InvalidOperationException(), (_, _, _) => throw new InvalidOperationException());
        }
    }
}
