using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleFiragaControllerTests
{
    [Fact]
    public void SingleHeaderReachesAllLocalChildrenContactSoundFlashAndRealCacheReleaseWithin70Callbacks()
    {
        var fixture = Fixture();
        NativeBattleFiragaFrame frame = fixture.Frame;
        Assert.Single(frame.Effect.Pools[0].Slots);
        Assert.Equal(100, frame.Effect.Pools[1].Slots.Length);
        Assert.Throws<NotSupportedException>(() => frame.Effect.Firaga!.Read(0));
        List<(int Tick, NativeBattleFiragaOperation Operation)> operations = [];
        int tick = 0;
        for (; tick < 70 && frame.Effect.ReturnedDescriptor != 0; tick++)
        {
            frame = NativeBattleFiragaController.Advance61CDA0(frame, Inputs(fixture.Packets, (current, operation) =>
            {
                operations.Add((tick, operation is NativeBattleFiragaDraw draw ? draw with { Task = NativeBattleTaskFixture.ObserveTask(draw.Task) } : operation));
                return current;
            }));
            Assert.Equal((tick & 1) != 0 ? 0x10000 : 0, frame.Effect.DrawArenaOffset);
            Assert.Equal(new NativeBattleModuleAnchor(100, -200, 300, (short)(tick < 33 ? 1 : 0)), frame.Effect.Firaga!.Read(0));
            Assert.Null(frame.Effect.Firaga.Positions[1]);
            Assert.Null(frame.Effect.Firaga.Positions[2]);
            if (tick == 32)
            {
                NativeBattleFiragaFrame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleFiragaFrame>(NativeBattleTestSnapshots.Json(frame))!;
                NativeBattleFiragaFrame expected = NativeBattleFiragaController.Advance61CDA0(NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(frame)), Inputs(fixture.Packets));
                NativeBattleFiragaFrame actual = NativeBattleFiragaController.Advance61CDA0(restored, Inputs(fixture.Packets));
                Assert.Equal(NativeBattleTestSnapshots.Json(expected), NativeBattleTestSnapshots.Json(actual));
                Assert.Equal((short)0, actual.Effect.Firaga!.Read(0).W);

            }
        }
        Assert.InRange(tick, 61, 70);
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.All(frame.Effect.Pools, pool => Assert.Null(pool.Head));
        Assert.Equal(48, Assert.Single(operations, item => item.Operation is NativeBattleFiragaContact).Tick);
        Assert.Equal(1, Assert.Single(operations, item => item.Operation is NativeBattleFiragaSfx).Tick);
        Assert.Equal(32, Assert.Single(operations, item => item.Operation is NativeBattleFiragaFlash).Tick);
        Assert.Equal(new NativeBattleFiragaFlash(0, 1, 2, 255), operations.Single(item => item.Operation is NativeBattleFiragaFlash).Operation);
        Assert.Equal(26, operations.Count(item => item.Operation is NativeBattleFiragaDraw { Task.Callback: 0x61CBB0 } draw && draw.Task.ReadWord(12) == 0));
        Assert.Equal(26, operations.Count(item => item.Operation is NativeBattleFiragaDraw { Task.Callback: 0x61C6B0 } draw && draw.Task.ReadWord(12) == 0));
        Assert.Equal(2, operations.Count(item => item.Tick == 33 && item.Operation is NativeBattleFiragaDraw { Task.Callback: 0x61C9C0 }));
        Assert.Contains(operations, item => item.Tick == 33 && item.Operation is NativeBattleFiragaDraw { Task.Callback: 0x61C870 });
        Assert.Contains(operations, item => item.Tick == 33 && item.Operation is NativeBattleFiragaDraw { Task.Callback: 0x61CB10 });
        Assert.DoesNotContain(operations, item => item.Tick > 33 && item.Operation is NativeBattleFiragaDraw { Task.Callback: 0x61CB10 });
        Assert.Equal(new NativeBattleFiragaStage(0, 0), operations.Last(item => item.Operation is NativeBattleFiragaStage).Operation);
        Assert.Contains(operations, item => item.Tick == 8 && item.Operation is NativeBattleFiragaStage { Value: 2048, ColorFlags: 0x101060 });
        Assert.Contains(operations, item => item.Tick == 52 && item.Operation is NativeBattleFiragaStage { Value: 2048, ColorFlags: 0x101080 });
        NativeCrtRandomState random = new(1);
        int draws = 0;
        while (draws < 368 && random != frame.Random)
        {
            random = random.Draw(out _);
            draws++;
        }
        Assert.Equal(frame.Random, random);
        Assert.InRange(draws, 342, 368);
    }

    [Fact]
    public void ThreeInclusiveHeadersOverlap44CallbacksAndRetainEveryActualCacheCell()
    {
        var fixture = Fixture(3);
        NativeBattleFiragaFrame frame = fixture.Frame;
        List<int> contacts = [];
        for (int tick = 0; tick < 158 && frame.Effect.ReturnedDescriptor != 0; tick++)
        {
            int currentTick = tick;
            frame = NativeBattleFiragaController.Advance61CDA0(frame, Inputs(fixture.Packets, (current, operation) =>
            {
                if (operation is NativeBattleFiragaContact)
                {
                    contacts.Add(currentTick);
                }
                return current;
            }));
            if (tick == 44)
            {
                Assert.Equal(new NativeBattleModuleAnchor(500, -600, 700, 1), frame.Effect.Firaga!.Read(1));
            }
            if (tick == 88)
            {
                Assert.Equal(new NativeBattleModuleAnchor(700, -800, 900, 1), frame.Effect.Firaga!.Read(2));
            }
        }
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.Equal(new[] { 48, 92, 136 }, contacts);
        Assert.All(frame.Effect.Firaga!.Positions, position => Assert.Equal((short)0, Assert.IsType<NativeBattleModuleAnchor>(position).W));
        var tooMany = Fixture(4, initialize: false);
        Assert.Throws<NotSupportedException>(() => NativeBattleFiragaController.Initialize61BFE0(tooMany.Frame.Effect, tooMany.Packets));
    }

    [Fact]
    public void CacheUnknownAndAdjacentOwnerBoundsRemainExplicitAcrossSerialization()
    {
        NativeBattleFiragaState cache = NativeBattleFiragaState.FromUnknown().Write(1, new(20, -30, 40, 1));
        string json = NativeBattleTestSnapshots.Json(cache);
        NativeBattleFiragaState restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleFiragaState>(json)!;
        Assert.Equal(json, NativeBattleTestSnapshots.Json(restored));
        Assert.Equal(new NativeBattleModuleAnchor(20, -30, 40, 1), restored.Read(1));
        Assert.Throws<NotSupportedException>(() => restored.Read(0));
        Assert.Throws<NotSupportedException>(() => restored.Read(3));
        Assert.Throws<NotSupportedException>(() => restored.Write(-1, new(0, 0, 0, 0)));
        Assert.Throws<InvalidDataException>(() => new NativeBattleFiragaState([]).Validate());
        Assert.Throws<JsonException>(() => NativeBattleTestSnapshots.RestoreJson<NativeBattleFiragaState>("{}"));
    }

    [Theory]
    [InlineData(0x61C6B0u)]
    [InlineData(0x61C870u)]
    [InlineData(0x61C9C0u)]
    [InlineData(0x61CB10u)]
    [InlineData(0x61CBB0u)]
    public void PauseStillDrawsEveryActiveLeafAndPreservesMutationRandomAndCache(uint callback)
    {
        var fixture = Fixture();
        NativeBattleFiragaFrame frame = WithLeaf(fixture.Frame, callback, task => task.WriteWord(12, 3));
        List<NativeBattleFiragaDraw> draws = [];
        NativeBattleFiragaFrame paused = NativeBattleFiragaController.Advance61CDA0(frame, Inputs(fixture.Packets, (current, operation) =>
        {
            NativeBattleFiragaDraw draw = Assert.IsType<NativeBattleFiragaDraw>(operation);
            draws.Add(draw with { Task = NativeBattleTaskFixture.ObserveTask(draw.Task) });
            return current;
        }) with { GraphicsFlags = 0x201 });
        Assert.Single(draws);
        Assert.Equal(NativeBattleTestSnapshots.Json(frame.Effect.Pools[1]), NativeBattleTestSnapshots.Json(paused.Effect.Pools[1]));
        Assert.Equal(NativeBattleTestSnapshots.Json(frame.Effect.Firaga), NativeBattleTestSnapshots.Json(paused.Effect.Firaga));
        Assert.Equal(frame.Random, paused.Random);
    }

    [Fact]
    public void PositiveSurfaceDelaySkipsDrawAndZeroScatterDivisorIsNeverClamped()
    {
        var fixture = Fixture();
        NativeBattleFiragaFrame frame = WithLeaf(fixture.Frame, 0x61C6B0, task => task.WriteWord(14, 1));
        NativeBattleFiragaInputs inputs = Inputs(fixture.Packets, (_, _) => throw new InvalidOperationException("Positive delay must not draw."));
        NativeBattleFiragaFrame paused = NativeBattleFiragaController.Advance61CDA0(frame, inputs with { GraphicsFlags = 1 });
        Assert.Equal((ushort)1, paused.Effect.Pools[1].Slots[0].ReadWord(14));
        NativeBattleFiragaFrame unpaused = NativeBattleFiragaController.Advance61CDA0(paused, inputs);
        Assert.Equal((ushort)0, unpaused.Effect.Pools[1].Slots[0].ReadWord(14));
        NativeBattleEffectTaskPool children = fixture.Frame.Effect.Pools[1];
        children = NativeBattleTaskFixture.EditPool(children, data => data with { Slots = children.Slots.SetItem(0, children.Slots[0].WriteWord(12, 36)) });
        NativeBattleFiragaFrame scatter = fixture.Frame with { Effect = fixture.Frame.Effect with { Firaga = NativeBattleFiragaState.FromUnknown().Write(0, new(10, -20, 30, 1)), Pools = [fixture.Frame.Effect.Pools[0], children] } };
        Assert.Throws<NotSupportedException>(() => NativeBattleFiragaController.Advance61CDA0(scatter, Inputs(fixture.Packets) with { ReadTransform = _ => new(new(0, 0, 0), new(0, 0, 0), 0, 100, 0) }));
    }

    [Fact]
    public void PortalCompletionUsesRetainedAliveWordAndHasNoInventedFrameLimit()
    {
        var fixture = Fixture();
        NativeBattleFiragaFrame frame = WithLeaf(fixture.Frame, 0x61CB10, task => task.WriteWord(12, 300).WriteWord(28, 100).WriteWord(30, 128));
        frame = frame with { Effect = frame.Effect with { Firaga = NativeBattleFiragaState.FromUnknown().Write(0, new(10, 20, 30, 1)) } };
        frame = NativeBattleFiragaController.Advance61CDA0(NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(frame)), Inputs(fixture.Packets));
        Assert.NotEqual(0u, frame.Effect.ReturnedDescriptor);
        Assert.Equal((ushort)301, frame.Effect.Pools[1].Slots[0].ReadWord(12));
        frame = frame with { Effect = frame.Effect with { Firaga = frame.Effect.Firaga!.Write(0, new(10, 20, 30, 0)) } };
        int draws = 0;
        frame = NativeBattleFiragaController.Advance61CDA0(frame, Inputs(fixture.Packets, (current, operation) =>
        {
            Assert.IsType<NativeBattleFiragaDraw>(operation);
            draws++;
            return current;
        }));
        Assert.Equal(1, draws);
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
    }

    [Fact]
    public void ReleasedRingRetainsItsReusedSlotYawInsteadOfInventingZero()
    {
        var fixture = Fixture();
        NativeBattleEffectTaskPool children = fixture.Frame.Effect.Pools[1];
        children = NativeBattleTaskFixture.EditPool(children, data => data with { Slots = children.Slots.SetItem(0, children.Slots[0].WriteWord(12, 33)).SetItem(3, children.Slots[3].WriteWord(24, 0x4321)) });
        NativeBattleFiragaFrame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Firaga = NativeBattleFiragaState.FromUnknown().Write(0, new(10, 20, 30, 1)), Pools = [fixture.Frame.Effect.Pools[0], children] } };
        NativeBattleFiragaDraw? ring = null;
        frame = NativeBattleFiragaController.Advance61CDA0(frame, Inputs(fixture.Packets, (current, operation) =>
        {
            if (operation is NativeBattleFiragaDraw { Task.Callback: 0x61C870 } draw)
            {
                ring = draw;
            }
            return current;
        }));
        Assert.NotNull(ring);
        Assert.Equal((ushort)0x4321, ring.Task.ReadWord(24));
        Assert.Equal((short)0, frame.Effect.Firaga!.Read(0).W);
    }

    [Theory]
    [InlineData(0x61C870u, 19, -11, 89, -11)]
    [InlineData(0x61C9C0u, 23, -3, 97, -3)]
    public void RingAndFlareDecayUseSignedTruncatingDivision(uint callback, int phase, int velocity, int scale, int nextVelocity)
    {
        var fixture = Fixture();
        NativeBattleFiragaFrame frame = WithLeaf(fixture.Frame, callback, task => task.WriteWord(12, (ushort)phase).WriteWord(28, 100).WriteWord(30, unchecked((ushort)velocity)));
        frame = NativeBattleFiragaController.Advance61CDA0(NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(frame)), Inputs(fixture.Packets));
        Assert.Equal(unchecked((ushort)scale), frame.Effect.Pools[1].Slots[0].ReadWord(28));
        Assert.Equal(unchecked((ushort)nextVelocity), frame.Effect.Pools[1].Slots[0].ReadWord(30));
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
    }

    private static NativeBattleFiragaFrame WithLeaf(NativeBattleFiragaFrame frame, uint callback, Func<NativeBattleModulePoolTask, NativeBattleModulePoolTask> initialize)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(NativeBattleFiragaController.ChildDescriptor, NativeBattleFiragaController.ChildBank, 36, 100);
        (pool, int slot) = pool.Append(callback);
        pool = NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.SetItem(slot, initialize(pool.Slots[slot])) });
        return frame with { Effect = frame.Effect with { Pools = [frame.Effect.Pools[0], pool] } };
    }

    private static NativeBattleFiragaInputs Inputs(NativeBattleActionPacketsState packets, Func<NativeBattleFiragaFrame, NativeBattleFiragaOperation, NativeBattleFiragaFrame>? execute = null) => new(0, packets, owner => owner switch { 3 => new(100, -200, 300, 777), 4 => new(500, -600, 700, 888), 5 => new(700, -800, 900, 999), _ => throw new NotSupportedException("An explicit graphics owner is required.") }, _ => new(new(0, 0, 0), new(0, 0, 0), 0, 100, 4096), execute ?? ((frame, _) => frame));

    private static (NativeBattleFiragaFrame Frame, NativeBattleActionPacketsState Packets) Fixture(int headers = 1, bool initialize = true)
    {
        NativeBattleActionPacketsState packets = NativeBattleRetainedMemory.FromColdProcess().ActionPackets;
        for (int index = 0; index < headers; index++)
        {
            (packets, _) = packets.Prepare(new(0, 2, 4, 0, 3, 143, null));
            (packets, _) = packets.AppendContact(new(new((byte)(index + 3), 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
            (packets, _) = packets.Publish(1);
        }
        packets = packets.WritePlayerRepeatMode(0, (byte)headers);
        NativeBattleOriginalEffectState effect = new(143, 142, new(143, 0x61BFE0, 0x61BFC0), new(0, 0, 8, 0, 0, 0), null);
        if (initialize)
        {
            effect = NativeBattleFiragaController.Initialize61BFE0(effect, packets);
        }
        return (new(NativeBattleEffectMemory.FromColdProcess(), effect, new(1)), packets);
    }
}
