using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicBrothersModel3Tests
{
    [Fact]
    public void PrimaryAlwaysRunsButClearOptionalFlagDoesNotReadReferenceOrRingParameters()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xBC, NativeBattleEffectNodeMemory.ArenaAddress + 0x3000);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x3000 + 0x7C, 4).MarkUnknown(0x2000 + 0x9C, 2) };
        int primary = 0;
        NativeBattleCinematicBrothersModelServices services = new(current =>
        {
            primary++;
            return current.WriteTemporaryDword(0x60, 123);
        }, (_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, _, _) => throw new InvalidOperationException(), () => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException());
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicBrothersModel3.Draw(frame, services);
        Assert.Equal(1, primary);
        Assert.Equal(123u, result.ReadTemporaryDword(0x60));
    }

    [Theory]
    [InlineData(12, false)]
    [InlineData(-1, false)]
    [InlineData(12, true)]
    public void RingProjectsNineCurrentPackedPointsAndEmitsEightMaskedModePrefixedFanTriangles(int depth, bool culled)
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        List<NativeBattleVector> points = [];
        List<NativeBattleActionMatrix> cameras = [];
        uint? ordering = null;
        uint? output = null;
        NativeBattleGfGeometryBatch? batch = null;
        uint[] screens = [Pack(10, 10), Pack(20, 10), Pack(30, 10), Pack(30, 20), Pack(30, 30), Pack(20, 30), Pack(10, 30), Pack(10, 20), Pack(20, 20)];
        if (culled)
        {
            screens[4] = Pack(30, -30);
        }
        NativeBattleCinematicBrothersModelServices services = new(_ => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (current, address) =>
        {
            int offset = checked((int)(address - current.TemporaryFrame.Address));
            Assert.Equal(6, current.ReadTemporary(offset, 6).Length);
            return current;
        }, (current, matrix, address) =>
        {
            int offset = checked((int)(address - current.TemporaryFrame.Address));
            points.Add(new(unchecked((short)current.ReadTemporaryWord(offset)), unchecked((short)current.ReadTemporaryWord(offset + 2)), unchecked((short)current.ReadTemporaryWord(offset + 4))));
            cameras.Add(matrix);
            int index = points.Count - 1;
            return new(current, screens[index], (uint)(0x100 + index), (uint)(0xAA000000 + index));
        }, () => 0, (current, pointer, table, packets) =>
        {
            output = pointer;
            ordering = table;
            batch = packets;
            return current;
        });
        NativeBattleActionMatrix parent = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 1000, 0, 4000, 4096);
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicBrothersModel3.DrawRingB67040(frame, NativeBattleEffectNodeMemory.ArenaAddress + 0x3000, parent, depth, 37, 128, services);
        Assert.Equal(9, points.Count);
        Assert.Equal(new NativeBattleVector(0, 37, 500), points[0]);
        Assert.Equal(new NativeBattleVector(353, 37, 353), points[1]);
        Assert.Equal(new NativeBattleVector(0, 37, 0), points[8]);
        Assert.All(cameras, matrix => Assert.Equal((1000, 0, 4000), (matrix.X, matrix.Y, matrix.Z)));
        Assert.Equal(0x108u, result.ReadTemporaryDword(0xF4));
        Assert.Equal(0xAA000008u, result.ReadTemporaryDword(0xFC));
        Assert.Equal(0, NativeBattleArenaPose.ReadMatrix(result.Arena, 0x4020).X);
        Assert.Equal(0, NativeBattleArenaPose.ReadMatrix(result.Arena, 0x4020).Z);
        if (culled)
        {
            Assert.Null(batch);
            Assert.Equal(0x80001000u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797068 + 0x7C));
        }
        else
        {
            Assert.NotNull(batch);
            Assert.Equal(288, batch.AllocationBytes);
            Assert.Equal(8, batch.Attempts.Length);
            NativeBattleCommonPacketWrite linked = NativeBattleCommonPacketArena.Append(NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400(), NativeBattleLogicalOrdering.Clear(), batch);
            Assert.Equal(288, linked.Process.ByteCursor);
            Assert.Equal(8, linked.Ordering.Proxies.Length);
            Assert.Equal(0x80001000u, output);
            Assert.Equal(depth == -1 ? 0x4064u : 12u, ordering);
            Assert.Equal(0x80001120u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797068 + 0x7C));
            Assert.Equal(new uint[] { 0x08000000, 0xE1000040, 0, 0x32808080, screens[8], 0, screens[0], 0, screens[1] }, batch.Attempts[0].Primitive!.Words);
            Assert.Equal(screens[0], batch.Attempts[7].Primitive!.Words[8]);
            Assert.All(batch.Attempts, attempt =>
            {
                Assert.Equal(new[] { NativeBattleGfPacketByteState.Untouched, NativeBattleGfPacketByteState.Untouched, NativeBattleGfPacketByteState.Untouched }, attempt.WriteMask.Take(3));
                Assert.All(attempt.WriteMask.Skip(3), state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
                Assert.Equal(new[] { 0, 0, 0, 0 }, attempt.Primitive!.VertexPayload4);
            });
        }
    }

    private static NativeBattleWaterExecutionFrame Fixture()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27973EC, 0x2797068);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797068 + 0x7C, 0x80001000);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27973B8, frame.TemporaryFrame.Address + 0x100);
        frame = frame.WriteTemporaryDword(0x14C, 0);
        byte[] view = new byte[32];
        BinaryPrimitives.WriteInt16LittleEndian(view, 4096);
        BinaryPrimitives.WriteInt16LittleEndian(view.AsSpan(8), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(view.AsSpan(16), 4096);
        frame = frame with { Core = frame.Core.Write(0x27979E8, view) };
        frame = frame with { Arena = frame.Arena.Write(0x3004, BitConverter.GetBytes(NativeBattleEffectNodeMemory.ArenaAddress + 0x3100)).Write(0x3100, BitConverter.GetBytes(NativeBattleEffectNodeMemory.ArenaAddress + 0x4000)) };
        byte[] skeleton = new byte[64];
        skeleton[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        return frame with { Arena = frame.Arena.Write(0x4000, skeleton) };
    }

    private static uint Pack(short x, short y) => unchecked((ushort)x) | unchecked((uint)(ushort)y << 16);
}
