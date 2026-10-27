using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicGrid28Tests
{
    [Fact]
    public void Calls_controls_in_native_order_projects_four_vertices_and_emits_masked_GT4()
    {
        NativeBattleWaterExecutionFrame initial = Fixture(1, 1);
        List<string> calls = [];
        int projections = 0;
        int packets = 0;
        NativeBattleCinematicGrid28Services services = Services(calls, (frame, vertex, normal, material, screen, color, flags) =>
        {
            Assert.Equal(new byte[] { 0, 0, 0, 0, 0, 0 }, frame.ReadTemporary(0xC8, 6));
            Assert.Equal(0x3C808080u, frame.ReadTemporaryDword(0xE0));
            int expectedX = projections / 2 * 256;
            int expectedZ = projections % 2 * 256;
            Assert.Equal(unchecked((ushort)expectedX), frame.ReadTemporaryWord(0xC0));
            Assert.Equal(unchecked((ushort)expectedZ), frame.ReadTemporaryWord(0xC4));
            Assert.Equal(Address(0x4000 + projections * 12), screen);
            frame = ArenaDword(frame, screen, unchecked((uint)(0x10000 + projections)));
            frame = ArenaDword(frame, color, 0x00030201);
            projections++;
            return new(frame.WriteTemporaryDword(0xFC, 0xCAFEBABE), -projections);
        }, (frame, output, ordering, batch) =>
        {
            packets++;
            Assert.Equal(4, projections);
            Assert.Equal(Address(0x5000), output);
            Assert.Equal(0xFFCu, ordering);
            NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
            Assert.Equal(52, batch.AllocationBytes);
            Assert.Equal(0x3E030201u, attempt.Primitive!.Words[1]);
            Assert.Equal(0x00010000u, attempt.Primitive.Words[2]);
            Assert.Equal(0x00010001u, attempt.Primitive.Words[5]);
            Assert.Equal(0x00010002u, attempt.Primitive.Words[8]);
            Assert.Equal(0x00010003u, attempt.Primitive.Words[11]);
            Assert.Equal(0xABCD0000u, attempt.Primitive.Words[3]);
            Assert.Equal(0x12340010u, attempt.Primitive.Words[6]);
            foreach (int index in new[] { 0, 1, 2, 0x26, 0x27, 0x32, 0x33 })
            {
                Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[index]);
            }
            Assert.Equal(45, attempt.WriteMask.Count(value => value == NativeBattleGfPacketByteState.Known));
            return frame;
        });
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicGrid28.Draw(initial, services);
        Assert.Equal(new[] { "page", "clut", "resolve", "basis", "lighting", "cue", "far" }, calls);
        Assert.Equal(1, packets);
        Assert.Equal(0x5678, WaterSchedulerFixture.ReadSlotWord(result, 0, 0x8E));
        Assert.Equal(Address(0x5034), WaterSchedulerFixture.ReadCoreDword(result, 0x2797690 + 0x7C));
        Assert.Equal(Address(0x4000), WaterSchedulerFixture.ReadCoreDword(result, 0x2797690 + 0x74));
        Assert.False(result.TemporaryFrame.IsKnown(0xC6, 1));
        Assert.False(result.TemporaryFrame.IsKnown(0xCE, 1));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x4006, 2));
    }

    [Theory]
    [InlineData(-1, 1, 0)]
    [InlineData(0, 0, 1)]
    [InlineData(1, -1, 0)]
    public void Signed_dimensions_keep_setup_but_only_nonnegative_vertices_and_positive_cells(short columns, short rows, int vertices)
    {
        int projections = 0;
        NativeBattleCinematicGrid28Services services = Services([], (frame, _, _, _, screen, color, _) =>
        {
            projections++;
            return new(ArenaDword(ArenaDword(frame, screen, 0), color, 0), 0);
        }, (_, _, _, _) => throw new InvalidOperationException("No positive cells exist."));
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicGrid28.Draw(Fixture(columns, rows), services);
        Assert.Equal(vertices, projections);
        Assert.Equal(Address(0x5000), WaterSchedulerFixture.ReadCoreDword(result, 0x2797690 + 0x7C));
    }

    [Fact]
    public void Endpoint_UV_substitutes_FF_without_changing_next_start_and_fixed_depth_uses_low_bits_only()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.SlotWord(Fixture(2, 1), 0, 0xDE, 0x20);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xC0, 0x10003);
        int emitted = 0;
        NativeBattleCinematicGrid28Services services = Services([], (current, _, _, _, screen, color, _) => new(ArenaDword(ArenaDword(current, screen, 0), color, 0), -10), (current, _, ordering, batch) =>
        {
            NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
            Assert.Equal(0x11000u, ordering);
            Assert.Equal(emitted == 0 ? 0xF0 : 0, attempt.Data[0xC]);
            Assert.Equal(emitted == 0 ? 0xFF : 0x10, attempt.Data[0x18]);
            emitted++;
            return current;
        });
        NativeBattleCinematicGrid28.Draw(frame, services);
        Assert.Equal(2, emitted);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(-1, true)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    public void Batched_grid_matches_scalar_packets_and_preserves_last_publication_when_a_cell_fails(int failingCell, bool commonPackets)
    {
        NativeBattleWaterExecutionFrame original = Fixture(3, 2);
        original = original with { Arena = original.Arena.WriteResourcePointer(0x7000, new(null, 123)).MarkUnknown(0x5000, 52 * 6) };
        NativeBattlePrimitiveProcess sourceProcess = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400() with { ByteCursor = 0x40 };
        if (commonPackets)
        {
            original = WaterSchedulerFixture.CoreDword(original, 0x2797690 + 0x7C, 0x40);
        }
        NativeBattleLogicalOrdering seed = NativeBattleLogicalOrdering.Clear().Prepend(20, new(commonPackets ? sourceProcess.PacketBank! : "20DFAB8", sourceProcess.PacketBuffer, commonPackets ? 0x40 : 0x5000), [0x04000000, 0x20010203, 1, 2, 3], 0, []);
        string originalFrame = SnapshotJson(original);
        string originalOrdering = SnapshotJson(seed);
        string originalProcess = SnapshotJson(sourceProcess);
        var scalar = Run(false);
        var batched = Run(true);
        Assert.Equal(SnapshotJson(scalar.Frame), SnapshotJson(batched.Frame));
        Assert.Equal(SnapshotJson(scalar.Published), SnapshotJson(batched.Published));
        Assert.Equal(SnapshotJson(scalar.Ordering), SnapshotJson(batched.Ordering));
        Assert.Equal(SnapshotJson(scalar.Process), SnapshotJson(batched.Process));
        Assert.Equal(scalar.Calls, batched.Calls);
        Assert.Equal(originalFrame, SnapshotJson(original));
        Assert.Equal(originalOrdering, SnapshotJson(seed));
        Assert.Equal(originalProcess, SnapshotJson(sourceProcess));
        Assert.Null(batched.Published.Transaction);
        Assert.Null(batched.Ordering.Transaction);
        Assert.Null(batched.Process.CommonTransaction);
        Assert.Equal(original.Arena.ReadResourcePointer(0x7000), batched.Published.ReadResourcePointer(0x7000));
        if (failingCell == 0)
        {
            Assert.Equal(SnapshotJson(original.Arena), SnapshotJson(batched.Published));
        }
        else
        {
            Assert.Throws<NotSupportedException>(() => batched.Published.Read(0x4006, 2));
            Assert.Throws<NotSupportedException>(() => batched.Published.Read(0x5026, 2));
        }
        if (commonPackets)
        {
            Assert.Equal(0x40 + (failingCell < 0 ? 6 : failingCell) * 52, batched.Process.ByteCursor);
            Assert.Throws<NotSupportedException>(() => batched.Process.ReadCommonPacket(0x40 + 0x26, 2));
        }

        (NativeBattleWaterExecutionFrame? Frame, NativeBattleEffectArena Published, NativeBattleLogicalOrdering Ordering, NativeBattlePrimitiveProcess Process, string[] Calls) Run(bool batchWrites)
        {
            NativeBattleWaterExecutionFrame source = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(original));
            using NativeBattleEffectArenaTransaction? arena = batchWrites ? new(source.Arena) : null;
            NativeBattleLogicalOrdering runSeed = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(seed));
            using NativeBattleLogicalOrderingTransaction? links = batchWrites ? new(runSeed) : null;
            NativeBattleLogicalOrdering ordering = links?.View ?? runSeed;
            NativeBattleEffectArena published = NativeBattleStorageSnapshots.Capture(source.Arena);
            NativeBattlePrimitiveProcess process = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(sourceProcess));
            NativeBattleCommonPacketTransaction? common = null;
            List<string> calls = [];
            int projections = 0;
            int cells = 0;
            NativeBattleCinematicGrid28Services services = Services(calls, (current, _, _, _, screen, color, _) =>
            {
                int index = projections++;
                current = ArenaDword(ArenaDword(current, screen, unchecked((uint)(0x10000 + index))), color, 0x00030201);
                return new(current.WriteTemporaryDword(0xFC, unchecked((uint)index)), 256 + index * 4);
            }, (current, output, _, packets) =>
            {
                NativeBattlePrimitiveProcess previousProcess = process;
                if (commonPackets && batchWrites && common is null)
                {
                    common = new(process);
                    process = common.View;
                }
                arena?.BeginCheckpoint();
                common?.BeginCheckpoint();
                try
                {
                    if (cells++ == failingCell)
                    {
                        packets = packets with { Attempts = [.. packets.Attempts, new(0, [], [], null)] };
                    }
                    if (commonPackets)
                    {
                        Assert.Equal(output, unchecked((uint)process.ByteCursor!.Value));
                        NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(process, ordering, packets);
                        process = written.Process;
                        ordering = written.Ordering;
                        published = current.Arena;
                    }
                    else
                    {
                        NativeBattleEffectMemory memory = new(current.Arena, 0, 0, [], 0, 0, 0, 0);
                        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(memory, checked((int)(output - NativeBattleEffectNodeMemory.ArenaAddress)), ordering, packets);
                        published = written.Memory.Arena!;
                        ordering = written.Ordering;
                    }
                    arena?.CommitCheckpoint();
                    common?.CommitCheckpoint();
                    return current with { Arena = published };
                }
                catch
                {
                    arena?.RollbackCheckpoint();
                    common?.RollbackCheckpoint();
                    process = previousProcess;
                    throw;
                }
            });
            NativeBattleWaterExecutionFrame? drawn = null;
            try
            {
                drawn = NativeBattleCinematicGrid28.Draw(source with { Arena = arena?.View ?? source.Arena }, services);
                Assert.Equal(-1, failingCell);
                if (arena is not null)
                {
                    drawn = drawn with { Arena = arena.Freeze() };
                }
                ordering = links?.Freeze() ?? ordering;
            }
            catch (InvalidDataException) when (failingCell >= 0)
            {
                ordering = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(seed));
            }
            finally
            {
                try
                {
                    if (arena is not null && ReferenceEquals(published.Transaction, arena))
                    {
                        published = drawn?.Arena ?? arena.Freeze();
                    }
                    if (common is not null && ReferenceEquals(process.CommonTransaction, common))
                    {
                        process = common.Freeze(process);
                    }
                }
                finally
                {
                    common?.Dispose();
                }
            }
            return (drawn, published, ordering, process, calls.ToArray());
        }
    }

    private static NativeBattleCinematicGrid28Services Services(List<string> calls, Func<NativeBattleWaterExecutionFrame, uint, uint, uint, uint, uint, uint, NativeBattleCinematicGridProjection> project, Func<NativeBattleWaterExecutionFrame, uint, uint, NativeBattleGfGeometryBatch, NativeBattleWaterExecutionFrame> append) => new((frame, index) =>
    {
        calls.Add("page");
        Assert.Equal(-2, index);
        return frame.WriteTemporaryDword(0xFC, 0x1234);
    }, (frame, index) =>
    {
        calls.Add("clut");
        Assert.Equal(0x1234u, frame.ReadTemporaryDword(0x68));
        return new(frame, 0xABCD);
    }, (frame, index) =>
    {
        calls.Add("resolve");
        Assert.Equal(-3, index);
        return 0x2797800;
    }, frame =>
    {
        calls.Add("basis");
        Assert.Equal(0x1000, WaterSchedulerFixture.ReadSlotWord(frame, 0, 0x8E));
        return frame;
    }, (frame, color) =>
    {
        calls.Add("lighting");
        Assert.Equal(0x5678, WaterSchedulerFixture.ReadSlotWord(frame, 0, 0x8E));
        Assert.Equal(0x02030201u, color);
        return frame;
    }, (frame, near, far, third) =>
    {
        calls.Add("cue");
        Assert.Equal(-100, near);
        Assert.Equal(1000, far);
        Assert.Equal(512, third);
        return frame;
    }, (frame, color) =>
    {
        calls.Add("far");
        Assert.Equal(0x00665544u, color);
        return frame;
    }, project, append);

    private static NativeBattleWaterExecutionFrame Fixture(short columns, short rows)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryDword(0x14C, 0x1000);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797690 + 0x74, Address(0x4000));
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797690 + 0x7C, Address(0x5000));
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797800 + 0xCC, 0x00665544);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xB8, Address(0x3000));
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xCC, 0x02030201);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x8E, 0x5678);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x94, 0);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x96, 0);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x98, 0);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0xDE, 0);
        byte[] payload = new byte[12];
        short[] values = [-3, -2, columns, rows, -100, 1000];
        for (int index = 0; index < values.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(index * 2), values[index]);
        }
        return frame with { Arena = frame.Arena.Write(0x3000, payload).MarkUnknown(0x4006, 2), TemporaryFrame = frame.TemporaryFrame.MarkUnknown(0xC6, 1).MarkUnknown(0xCE, 1) };
    }

    private static uint Address(int offset) => unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)offset);
    private static NativeBattleWaterExecutionFrame ArenaDword(NativeBattleWaterExecutionFrame frame, uint address, uint value)
    {
        byte[] data = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(data, value);
        return frame with { Arena = frame.Arena.Write(checked((int)(address - NativeBattleEffectNodeMemory.ArenaAddress)), data) };
    }

    private static string SnapshotJson<T>(T value) => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(value));
}
