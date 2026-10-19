using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleKiros262TrailTests
{
    [Fact]
    public void FirstDrawAllocatesSixteenBytesButOnlyClearsTheCounterAndActiveHistoryFlags()
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        NativeBattleKiros262TrailState state = NativeBattleKiros262TrailState.FromColdProcess();
        state = state with { Memory = state.Memory.Write(NativeBattleKiros262TrailState.History + 24, [7, 8, 9, 10]) };
        int restores = 0;
        NativeBattleKiros262TrailStep result = NativeBattleKiros262Trail.Draw(frame, state, Services((current, _, selector) =>
        {
            Assert.Equal((ushort)0xF1, selector);
            return new(current, new(10, 20, 30), 0, 0, -4096);
        }, (current, _) =>
        {
            restores++;
            return current;
        }, (_, _, _) => throw new InvalidOperationException()));
        Assert.Equal(1, restores);
        Assert.Equal(new byte[] { 7, 8, 9, 10 }, result.State.Memory.Read(NativeBattleKiros262TrailState.History + 24, 4));
        Assert.Equal((ushort)1, BinaryPrimitives.ReadUInt16LittleEndian(result.Frame.Arena.Read(0x5000, 2)));
        Assert.Equal(Enumerable.Repeat((byte)0xA5, 14), result.Frame.Arena.Read(0x5002, 14));
        Assert.Equal(0x20E4AB8u, BinaryPrimitives.ReadUInt32LittleEndian(result.Frame.Arena.Read(0x20BC, 4)));
        Assert.Equal(0x20E4AC8u, WaterSchedulerFixture.ReadCoreDword(result.Frame, WaterSchedulerFixture.ReadCoreDword(frame, 0x27973EC) + 0x74));
        Assert.Equal(new byte[] { 10, 0, 20, 0, 30, 0 }, result.State.Memory.Read(NativeBattleKiros262TrailState.History + 4, 6));
        Assert.Equal((short)-636, BinaryPrimitives.ReadInt16LittleEndian(result.State.Memory.Read(NativeBattleKiros262TrailState.History + 16, 2)));
        Assert.Throws<NotSupportedException>(() => result.State.Memory.Read(NativeBattleKiros262TrailState.History + 10, 2));
    }

    [Fact]
    public void ExistingCounterAndNonzeroYSkipPayloadReadsPoseMutationAndCounterAdvance()
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        frame = frame with { Arena = frame.Arena.Write(0x20BC, BitConverter.GetBytes(0x20E4AB8u)).Write(0x2096, [1, 0]).MarkUnknown(0x3000, 4) };
        NativeBattleKiros262TrailServices services = Services((_, _, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, _, _) => throw new InvalidOperationException());
        NativeBattleKiros262TrailStep result = NativeBattleKiros262Trail.Draw(frame, NativeBattleKiros262TrailState.FromColdProcess(), services);
        Assert.Equal(frame, result.Frame);
        Assert.Empty(result.State.Memory.Regions);
    }

    [Fact]
    public void SecondSampleReusesExactCurvesAndPreservesTheNativeHistoryAcrossSnapshot()
    {
        NativeBattleKiros262TrailServices firstServices = Services((current, actor, _) =>
        {
            Assert.Equal(0x1D972C0u, actor);
            return new(current, new(10, 20, 30), 0, 0, -4096);
        }, (current, _) => current, (_, _, _) => throw new InvalidOperationException());
        NativeBattleKiros262TrailStep first = NativeBattleKiros262Trail.Draw(Fixture(), NativeBattleKiros262TrailState.FromColdProcess(), firstServices);
        NativeBattleKiros262TrailState restored = JsonSerializer.Deserialize<NativeBattleKiros262TrailState>(JsonSerializer.Serialize(first.State))!;
        int emitted = 0;
        NativeBattleKiros262TrailServices secondServices = Services((current, _, _) => new(current, new(40, 50, 60), 0, 0, -4096), (current, _) => current, (current, state, color) =>
        {
            emitted++;
            Assert.Equal(0x3A123456u, color);
            Assert.Equal((short)40, BinaryPrimitives.ReadInt16LittleEndian(state.Memory.Read(NativeBattleKiros262TrailState.CurveA, 2)));
            Assert.Equal((short)10, BinaryPrimitives.ReadInt16LittleEndian(state.Memory.Read(NativeBattleKiros262TrailState.CurveA + 31 * 8, 2)));
            return current;
        });
        var expected = NativeBattleKiros262Trail.Draw(first.Frame, first.State, secondServices);
        var actual = NativeBattleKiros262Trail.Draw(first.Frame, restored, secondServices);
        Assert.Equal(2, emitted);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
        Assert.Equal((ushort)2, BinaryPrimitives.ReadUInt16LittleEndian(actual.Frame.Arena.Read(0x5000, 2)));
        Assert.Throws<InvalidDataException>(() => (actual.State with { Memory = actual.State.Memory.Write(0x288, [1]) }).Validate());
    }

    private static NativeBattleKiros262TrailServices Services(Func<NativeBattleWaterExecutionFrame, uint, ushort, NativeBattleKiros262Tip> read, Func<NativeBattleWaterExecutionFrame, uint, NativeBattleWaterExecutionFrame> restore, Func<NativeBattleWaterExecutionFrame, NativeBattleKiros262TrailState, uint, NativeBattleWaterExecutionFrame> emit) => new(read, restore, emit);

    private static NativeBattleWaterExecutionFrame Fixture()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        uint manager = WaterSchedulerFixture.ReadCoreDword(frame, 0x27973EC);
        uint owner = WaterSchedulerFixture.ReadCoreDword(frame, 0x279744C);
        frame = WaterSchedulerFixture.CoreDword(frame, manager + 0x74, 0x20E4AB8);
        frame = WaterSchedulerFixture.CoreDword(frame, owner + 0x60, 0x1D972C0);
        return frame with { Arena = frame.Arena.Write(0x20B8, BitConverter.GetBytes(NativeBattleEffectNodeMemory.ArenaAddress + 0x3000u)).Write(0x3000, [0xF1, 0, 2, 0]).Write(0x2094, BitConverter.GetBytes((short)666)).Write(0x20CC, BitConverter.GetBytes(0x123456u)).Write(0x5000, Enumerable.Repeat((byte)0xA5, 16).ToArray()) };
    }
}
