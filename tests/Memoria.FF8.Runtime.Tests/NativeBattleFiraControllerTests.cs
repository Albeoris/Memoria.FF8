using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleFiraControllerTests
{
    [Fact]
    public void FullSingleHeaderUsesOriginalBanksAndAllChildrenWith612OrderedRandomDraws()
    {
        var fixture = Fixture();
        NativeBattleFiraFrame frame = fixture.Frame;
        Assert.Equal(NativeBattleFiraController.MainDescriptor, frame.Effect.ReturnedDescriptor);
        Assert.Equal(2, frame.Effect.Pools[0].Slots.Length);
        Assert.Equal((ushort)16, frame.Effect.Pools[0].Stride);
        Assert.Equal(150, frame.Effect.Pools[1].Slots.Length);
        Assert.Equal((ushort)36, frame.Effect.Pools[1].Stride);
        List<(int Tick, NativeBattleFiraOperation Operation)> operations = [];
        int tick = 0;
        for (; tick < 96 && frame.Effect.ReturnedDescriptor != 0; tick++)
        {
            frame = NativeBattleFiraController.Advance61DD90(frame, Inputs(fixture.Packets, (current, operation) =>
            {
                operations.Add((tick, operation));
                return current;
            }));
            Assert.Equal((tick & 1) != 0 ? 0x10000 : 0, frame.Effect.DrawArenaOffset);
            if (tick == 0)
            {
                NativeBattleModulePoolTask controller = frame.Effect.Pools[1].Slots[0];
                Assert.Equal((ushort)100, controller.ReadWord(16));
                Assert.Equal((ushort)400, controller.ReadWord(18));
                Assert.Equal(unchecked((ushort)-200), controller.ReadWord(22));
                Assert.Equal((ushort)40, controller.ReadWord(26));
                Assert.Equal(unchecked((ushort)-100), controller.ReadWord(30));
            }
            if (tick == 15)
            {
                NativeBattleFiraFrame restored = JsonSerializer.Deserialize<NativeBattleFiraFrame>(JsonSerializer.Serialize(frame))!;
                NativeBattleFiraFrame expected = NativeBattleFiraController.Advance61DD90(frame, Inputs(fixture.Packets));
                NativeBattleFiraFrame actual = NativeBattleFiraController.Advance61DD90(restored, Inputs(fixture.Packets));
                Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
                frame = restored;
            }
            if (tick == 11)
            {
                NativeBattleModulePoolTask[] columns = frame.Effect.Pools[1].Slots.Where(task => (task.Flags & 1) != 0 && task.Callback == 0x61D7F0).ToArray();
                Assert.Equal(3, columns.Length);
                Assert.All(columns, column => Assert.Equal((ushort)0x9C9, column.ReadWord(32)));
            }
        }
        Assert.True(tick < 96);
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.All(frame.Effect.Pools, pool => Assert.Null(pool.Head));
        Assert.Equal(14, Assert.Single(operations, item => item.Operation is NativeBattleFiraContact).Tick);
        Assert.Equal(3, Assert.Single(operations, item => item.Operation is NativeBattleFiraSound).Tick);
        Assert.Equal(3, Assert.IsType<NativeBattleFiraSound>(operations.Single(item => item.Operation is NativeBattleFiraSound).Operation).Owner);
        Assert.Equal(new uint[] { 0x61D4C0, 0x61D5D0, 0x61D6B0, 0x61D7F0, 0x61D9A0, 0x61DC20 }, operations.Where(item => item.Operation is NativeBattleFiraDraw).Select(item => ((NativeBattleFiraDraw)item.Operation).Task.Callback).Distinct().Order().ToArray());
        Assert.DoesNotContain(operations, item => item.Operation is NativeBattleFiraCommonTextureLoad);
        NativeCrtRandomState expectedRandom = new(1);
        for (int index = 0; index < 612; index++)
        {
            expectedRandom = expectedRandom.Draw(out _);
        }
        Assert.Equal(expectedRandom, frame.Random);
        Assert.Equal(new short[] { 0, 341, 682, 1023, 1364, 1705, 2046 }, operations.Where(item => item.Tick <= 6 && item.Operation is NativeBattleFiraStage).Select(item => ((NativeBattleFiraStage)item.Operation).Value));
        Assert.Equal((short)0, ((NativeBattleFiraStage)operations.Last(item => item.Operation is NativeBattleFiraStage).Operation).Value);
    }

    [Fact]
    public void InclusiveLastHeaderAppendsItsControllerInTheSamePassAndKeepsEachContact()
    {
        var fixture = Fixture(2);
        NativeBattleFiraFrame frame = fixture.Frame;
        List<(int Tick, NativeBattleFiraOperation Operation)> operations = [];
        for (int tick = 0; tick < 128 && frame.Effect.ReturnedDescriptor != 0; tick++)
        {
            int currentTick = tick;
            frame = NativeBattleFiraController.Advance61DD90(frame, Inputs(fixture.Packets, (current, operation) =>
            {
                operations.Add((currentTick, operation));
                return current;
            }));
            if (tick == 21)
            {
                NativeBattleModulePoolTask next = Assert.Single(frame.Effect.Pools[1].Slots, task => (task.Flags & 1) != 0 && task.Callback == 0x61CEC0 && task.ReadWord(14) == 1);
                Assert.Equal((ushort)1, next.ReadWord(12));
                Assert.Equal((ushort)500, next.ReadWord(16));
                Assert.Equal((ushort)800, next.ReadWord(18));
            }
        }
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.Equal(new[] { 14, 35 }, operations.Where(item => item.Operation is NativeBattleFiraContact).Select(item => item.Tick));
        Assert.Equal(new[] { 0, 1 }, operations.Where(item => item.Operation is NativeBattleFiraContact).Select(item => ((NativeBattleFiraContact)item.Operation).Contact));
        Assert.Equal(new[] { 3, 24 }, operations.Where(item => item.Operation is NativeBattleFiraSound).Select(item => item.Tick));
        NativeCrtRandomState expectedRandom = new(1);
        for (int index = 0; index < 1224; index++)
        {
            expectedRandom = expectedRandom.Draw(out _);
        }
        Assert.Equal(expectedRandom, frame.Random);
    }

    [Theory]
    [InlineData(0x61D4C0u)]
    [InlineData(0x61D5D0u)]
    [InlineData(0x61D7F0u)]
    [InlineData(0x61DC20u)]
    public void DelayedLeavesWaitWithoutDrawingAndOnlyUnpausedDelayChanges(uint callback)
    {
        var fixture = Fixture();
        NativeBattleFiraFrame frame = WithLeaf(fixture.Frame, callback, task => task.WriteWord(14, 1));
        string original = JsonSerializer.Serialize(frame.Effect.Pools[1]);
        NativeBattleFiraInputs inputs = Inputs(fixture.Packets, (_, _) => throw new InvalidOperationException("Delayed leaves must not draw."));
        NativeBattleFiraFrame frozen = NativeBattleFiraController.Advance61DD90(frame, inputs with { GraphicsFlags = 0x201 });
        Assert.Equal(original, JsonSerializer.Serialize(frozen.Effect.Pools[1]));
        Assert.Equal(frame.Random, frozen.Random);
        NativeBattleFiraFrame advanced = NativeBattleFiraController.Advance61DD90(frozen, inputs);
        Assert.Equal((ushort)0, advanced.Effect.Pools[1].Slots[0].ReadWord(14));
        Assert.Equal((ushort)0, advanced.Effect.Pools[1].Slots[0].ReadWord(12));
    }

    [Theory]
    [InlineData(0x61D4C0u, 0, -1)]
    [InlineData(0x61D5D0u, 15, 16)]
    [InlineData(0x61D6B0u, 21, 22)]
    [InlineData(0x61D7F0u, 17, 18)]
    [InlineData(0x61D9A0u, 21, 22)]
    [InlineData(0x61DC20u, 15, 16)]
    public void EveryDrawableSubmitsBeforePauseAndCompletesAtItsOwnSignedCounter(uint callback, int counter, int completedCounter)
    {
        var fixture = Fixture();
        NativeBattleFiraFrame frame = WithLeaf(fixture.Frame, callback, task => task.WriteWord(12, (ushort)counter));
        List<NativeBattleFiraDraw> draws = [];
        NativeBattleFiraInputs inputs = Inputs(fixture.Packets, (current, operation) =>
        {
            draws.Add(Assert.IsType<NativeBattleFiraDraw>(operation));
            return current;
        });
        NativeBattleFiraFrame frozen = NativeBattleFiraController.Advance61DD90(frame, inputs with { GraphicsFlags = 0x201 });
        Assert.Equal(JsonSerializer.Serialize(frame.Effect.Pools[1]), JsonSerializer.Serialize(frozen.Effect.Pools[1]));
        Assert.Equal((ushort)counter, Assert.Single(draws).Task.ReadWord(12));
        draws.Clear();
        NativeBattleFiraFrame completed = NativeBattleFiraController.Advance61DD90(frozen, inputs);
        Assert.Equal((ushort)counter, Assert.Single(draws).Task.ReadWord(12));
        Assert.Equal(unchecked((ushort)completedCounter), completed.Effect.Pools[1].Slots[0].ReadWord(12));
        Assert.Equal(0u, completed.Effect.ReturnedDescriptor);
    }

    [Fact]
    public void SparkMotionKeepsSignedShiftsAndWordWrapping()
    {
        var fixture = Fixture();
        NativeBattleFiraFrame frame = WithLeaf(fixture.Frame, 0x61D4C0, task => task.WriteWord(12, 3).WriteWord(16, 32760).WriteWord(24, 20).WriteWord(26, unchecked((ushort)-40)).WriteWord(28, unchecked((ushort)-17)).WriteWord(32, 10));
        frame = NativeBattleFiraController.Advance61DD90(frame, Inputs(fixture.Packets));
        NativeBattleModulePoolTask spark = frame.Effect.Pools[1].Slots[0];
        Assert.Equal(unchecked((ushort)-32756), spark.ReadWord(16));
        Assert.Equal((ushort)19, spark.ReadWord(24));
        Assert.Equal(unchecked((ushort)-43), spark.ReadWord(26));
        Assert.Equal(unchecked((ushort)-15), spark.ReadWord(28));
        Assert.Equal(unchecked((ushort)-30), spark.ReadWord(32));
    }

    [Fact]
    public void FinalTaskAppendsInSamePassSchedulesAtOneAndWaitsForActualCompletionByteEvenPaused()
    {
        var fixture = Fixture(flags: 0);
        NativeBattleFiraFrame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = [fixture.Frame.Effect.Pools[0], NativeBattleEffectTaskPool.Initialize(NativeBattleFiraController.ChildDescriptor, NativeBattleFiraController.ChildBank, 36, 150)] } };
        NativeBattleFiraInputs inputs = Inputs(fixture.Packets, (_, _) => throw new InvalidOperationException("Final task must not load at counter0."));
        frame = NativeBattleFiraController.Advance61DD90(frame, inputs);
        NativeBattleEffectTaskPool main = frame.Effect.Pools[0];
        int final = main.Head!.Value;
        Assert.Equal(0x61DE10u, main.Slots[final].Callback);
        Assert.Equal((ushort)1, main.Slots[final].ReadWord(12));
        uint address = NativeBattleFiraController.MainBank + (uint)final * 16 + 14;
        Assert.Throws<InvalidDataException>(() => NativeBattleFiraController.WriteCompletionByte(frame.Effect, address + 1, 1));
        Assert.Throws<InvalidDataException>(() => NativeBattleFiraController.WriteCompletionByte(frame.Effect, NativeBattleFiraController.MainBank + 14, 1));
        int loads = 0;
        inputs = Inputs(fixture.Packets, (current, operation) =>
        {
            NativeBattleFiraCommonTextureLoad load = Assert.IsType<NativeBattleFiraCommonTextureLoad>(operation);
            Assert.Equal(0, load.DestinationArenaOffset);
            Assert.Equal(address, load.CompletionAddress);
            loads++;
            return current with { Effect = NativeBattleFiraController.WriteCompletionByte(current.Effect, address, 0) };
        }) with { GraphicsFlags = 0x201 };
        frame = NativeBattleFiraController.Advance61DD90(frame, inputs);
        Assert.Equal(1, loads);
        Assert.NotEqual(0u, frame.Effect.ReturnedDescriptor);
        Assert.Equal((ushort)2, frame.Effect.Pools[0].Slots[final].ReadWord(12));
        frame = JsonSerializer.Deserialize<NativeBattleFiraFrame>(JsonSerializer.Serialize(frame))!;
        frame = frame with { Effect = NativeBattleFiraController.WriteCompletionByte(frame.Effect, address, 1) };
        frame = NativeBattleFiraController.Advance61DD90(frame, inputs);
        Assert.Equal(1, loads);
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.Throws<InvalidDataException>(() => NativeBattleFiraController.WriteCompletionByte(frame.Effect, address, 1));
    }

    private static NativeBattleFiraFrame WithLeaf(NativeBattleFiraFrame frame, uint callback, Func<NativeBattleModulePoolTask, NativeBattleModulePoolTask> initialize)
    {
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(NativeBattleFiraController.ChildDescriptor, NativeBattleFiraController.ChildBank, 36, 150);
        (children, int slot) = children.Append(callback);
        children = children with { Slots = children.Slots.SetItem(slot, initialize(children.Slots[slot])) };
        return frame with { Effect = frame.Effect with { Pools = [frame.Effect.Pools[0], children] } };
    }

    private static NativeBattleFiraInputs Inputs(NativeBattleActionPacketsState packets, Func<NativeBattleFiraFrame, NativeBattleFiraOperation, NativeBattleFiraFrame>? execute = null) => new(0, packets, owner => owner switch { 0 => new(10, -100, 30, 40), 3 => new(100, -200, 300, 400), 4 => new(500, -600, 700, 800), _ => throw new NotSupportedException("An explicit graphics owner is required.") }, execute ?? ((frame, _) => frame));

    private static (NativeBattleFiraFrame Frame, NativeBattleActionPacketsState Packets) Fixture(int headers = 1, byte flags = 2)
    {
        NativeBattleActionPacketsState packets = NativeBattleRetainedMemory.FromColdProcess().ActionPackets;
        for (int index = 0; index < headers; index++)
        {
            (packets, _) = packets.Prepare(new(0, 2, 4, 0, 2, 142, null));
            (packets, _) = packets.AppendContact(new(new((byte)(index + 3), 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
            (packets, _) = packets.Publish(1);
        }
        packets = packets.WritePlayerRepeatMode(0, (byte)headers);
        NativeBattleOriginalEffectState effect = new(142, 141, new(142, 0x61CE10, 0x61CDF0), new(0, flags, 8, 0, 0, 0), null);
        effect = NativeBattleFiraController.Initialize61CE10(effect, packets);
        return (new(NativeBattleEffectMemory.FromColdProcess(), effect, new(1)), packets);
    }
}
