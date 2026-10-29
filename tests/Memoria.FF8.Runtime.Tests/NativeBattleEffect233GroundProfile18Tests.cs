using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using Xunit;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect233GroundProfile18Tests
{
    private const int WorkOffset = 0x8000;
    private const int ParametersOffset = 0x5000;
    private const uint Manager = 0x2797690;
    private static uint Address(int offset) => NativeBattleEffectNodeMemory.ArenaAddress + (uint)offset;

    [Fact]
    public void ProducesThirtyTwoSignedThresholdsAndRetainsAllocationPadding()
    {
        NativeBattleWaterExecutionFrame frame = Create();
        frame = frame with { Arena = frame.Arena.MarkUnknown(WorkOffset, 4) };
        Capture capture = new();
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicGroundProfile18.Draw(frame, capture.Services());
        Assert.Equal(Address(WorkOffset), WaterSchedulerFixture.ReadSlotDword(result, 0, 0xBC));
        Assert.Equal(Address(WorkOffset + 0xA0), WaterSchedulerFixture.ReadCoreDword(result, Manager + 0x74));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(WorkOffset, 4));
        Assert.Equal(0x13F6, Word(result, WorkOffset + 4));
        Assert.Equal(32, Word(result, WorkOffset + 6));
        Assert.Equal(0, Word(result, WorkOffset + 0x60));
        Assert.Equal(1, Word(result, WorkOffset + 0x62));
        Assert.Equal(31, Word(result, WorkOffset + 0x9E));
        Assert.Equal(528, capture.Inputs.Count);
        Assert.Equal(0x11A, WaterSchedulerFixture.ReadSlotWord(result, 0, 0x92));
    }

    [Theory]
    [InlineData(256, 16)]
    [InlineData(-256, -16)]
    public void NativeFixedPointHeightUsesSignedAmplitude(int amplitude, int expected)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.SlotWord(Create(), 0, 0x8C, unchecked((ushort)amplitude));
        Capture capture = new();
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicGroundProfile18.Draw(frame, capture.Services());
        Assert.Equal(expected, unchecked((short)Word(result, WorkOffset + 0x20)));
        Assert.Equal(expected, unchecked((short)Word(result, WorkOffset + 0x5E)));
        Assert.All(capture.Inputs, input => Assert.Equal(expected, input.Y));
    }

    [Fact]
    public void NegativeRadiiDivideTowardZeroAndStoreWrappedWords()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.SlotWord(Create(), 1, 0x98, unchecked((ushort)-33));
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x90, 255);
        Capture capture = new();
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicGroundProfile18.Draw(frame, capture.Services());
        Assert.Equal(-1, unchecked((short)Word(result, WorkOffset + 0x62)));
        Assert.Equal(-31, unchecked((short)Word(result, WorkOffset + 0x9E)));
    }

    [Fact]
    public void PhysicalLedgerHas496LinksSixteenHolesAndUnlinkedTail()
    {
        Capture capture = new();
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicGroundProfile18.Draw(Create(), capture.Services());
        NativeBattleGfGeometryBatch batch = Assert.IsType<NativeBattleGfGeometryBatch>(capture.Batch);
        Assert.True(batch.NativeUnlinkedAllocation);
        Assert.Equal(0x6800, batch.AllocationBytes);
        NativeBattleGfGeometryAttempt[] linked = batch.Attempts.Where(attempt => attempt.Primitive is not null).ToArray();
        Assert.Equal(496, linked.Length);
        Assert.Equal(0, linked[0].ByteOffsetRelative);
        Assert.Equal(0x6798, linked[^1].ByteOffsetRelative);
        Assert.All(linked, attempt => Assert.True(attempt.ByteOffsetRelative + attempt.PacketLength <= 0x6800));
        for (int row = 0; row < 16; row++)
        {
            Assert.DoesNotContain(linked, attempt => attempt.ByteOffsetRelative == 52 * (32 * row + 31));
        }
        byte[] physical = Materialize(batch);
        Assert.Equal(0xA5, physical[52 * 31 + 4]);
        Assert.Equal(0xA5, physical[52 * 31 + 3]);
        Assert.Equal(0xA5, physical[0x6E5A]);
        Assert.Equal(0xA5, physical[0x6E5B]);
        int high = batch.Attempts.SelectMany(attempt => attempt.WriteMask.Select((state, index) => state == NativeBattleGfPacketByteState.Untouched ? 0 : attempt.ByteOffsetRelative + index + 1)).Max();
        Assert.Equal(0x6E5A, high);
        Assert.Equal(Address(0x10000 + 0x6800), WaterSchedulerFixture.ReadCoreDword(result, Manager + 0x7C));
        Assert.Equal(17u, result.ReadTemporaryDword(0x7C));
    }

    [Fact]
    public void QuadLayoutPreservesUvPaddingAndUsesNativeDepthNeighborAverage()
    {
        Capture capture = new();
        _ = NativeBattleCinematicGroundProfile18.Draw(Create(), capture.Services());
        NativeBattleGfGeometryAttempt first = capture.Batch!.Attempts.First(attempt => attempt.Primitive is not null);
        NativeBattleGeometryPrimitive primitive = first.Primitive!;
        Assert.Equal(12u, primitive.Words[0] >> 24);
        Assert.Equal(0x3E345678u, primitive.Words[1]);
        Assert.Equal(primitive.Words[1], primitive.Words[4]);
        Assert.Equal(primitive.Words[1], primitive.Words[7]);
        Assert.Equal(primitive.Words[1], primitive.Words[10]);
        Assert.Equal(0x2244, (int)(primitive.Words[3] >> 16));
        Assert.Equal(0x11A, (int)(primitive.Words[6] >> 16));
        Assert.Equal(100, primitive.Bucket);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, primitive.PhysicalWriteMask!.Value[38]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, primitive.PhysicalWriteMask.Value[50]);
        byte[] physical = Materialize(capture.Batch);
        Assert.Equal(0xA5, physical[38]);
        Assert.Equal(0xA5, physical[50]);
    }

    [Fact]
    public void ExistingPacketWriterAcceptsLogicalHolesAndPreservesUnknownPhysicalPadding()
    {
        Capture capture = new();
        _ = NativeBattleCinematicGroundProfile18.Draw(Create(), capture.Services());
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().MarkUnknown(0x10000 + 38, 2).MarkUnknown(0x10000 + 0x6E5A, 2);
        NativeBattleEffectMemory memory = new(arena, 0, 0, [], 0, 0, 0, 0);
        NativeBattleEffectPacketWrite write = NativeBattleEffectPacketArena.Append(memory, 0x10000, NativeBattleLogicalOrdering.Clear(), capture.Batch!);
        Assert.Equal(0x16800, write.Cursor);
        Assert.Equal(496, write.Ordering.Proxies.Length);
        Assert.Throws<NotSupportedException>(() => write.Memory.Arena!.Read(0x10000 + 38, 2));
        Assert.Throws<NotSupportedException>(() => write.Memory.Arena!.Read(0x10000 + 0x6E5A, 2));
        Assert.Equal(12, write.Memory.Arena!.Read(0x10003, 1)[0]);
    }

    [Fact]
    public void SignedDepthRetainsNegativeOrderingOffsetWithoutMaskingToTableSize()
    {
        Capture capture = new() { Depth = -5 };
        _ = NativeBattleCinematicGroundProfile18.Draw(Create(), capture.Services());
        Assert.All(capture.Batch!.Attempts.Where(attempt => attempt.Primitive is not null), attempt => Assert.Equal(-2, attempt.Primitive!.Bucket));
    }

    [Fact]
    public void SectorsApplyBothSignedAxesAndProjectionRetainsPackedPadding()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.SlotWord(Create(), 0, 0x90, 4096);
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x98, 256);
        frame.TemporaryFrame.MarkUnknown(0xD6, 2);
        Capture capture = new();
        _ = NativeBattleCinematicGroundProfile18.Draw(frame, capture.Services());
        Assert.Contains(capture.Inputs, input => input.X > 0 && input.Z > 0);
        Assert.Contains(capture.Inputs, input => input.X > 0 && input.Z < 0);
        Assert.Contains(capture.Inputs, input => input.X < 0 && input.Z > 0);
        Assert.Contains(capture.Inputs, input => input.X < 0 && input.Z < 0);
        Assert.All(capture.Padding, known => Assert.False(known));
    }

    [Fact]
    public void ExistingProfileRereadsCurrentReferenceAndSurvivesJsonContinuation()
    {
        Capture firstCapture = new();
        NativeBattleWaterExecutionFrame first = NativeBattleCinematicGroundProfile18.Draw(Create(), firstCapture.Services());
        NativeBattleWaterExecutionFrame next = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(first)))!);
        next = WaterSchedulerFixture.SlotWord(next, 1, 0x98, 64);
        Capture nextCapture = new();
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicGroundProfile18.Draw(next, nextCapture.Services());
        Assert.Equal(Address(WorkOffset + 0xA0), WaterSchedulerFixture.ReadCoreDword(result, Manager + 0x74));
        Assert.Equal(62, Word(result, WorkOffset + 0x9E));
        Assert.Equal(1, nextCapture.Resolves);
        Assert.Equal(1, nextCapture.Bases);
        Assert.Equal(first.ReadTemporaryDword(0xFC), result.ReadTemporaryDword(0xFC));
    }

    private static NativeBattleWaterExecutionFrame Create()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame = WaterSchedulerFixture.CoreDword(frame, Manager + 0x74, Address(WorkOffset));
        frame = WaterSchedulerFixture.CoreDword(frame, Manager + 0x7C, Address(0x10000));
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xB8, Address(ParametersOffset));
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xCC, 0xF2345678);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x90, 256);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x9A, 0x2244);
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x98, 32);
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x8C, 64);
        byte[] parameters = new byte[8];
        BinaryPrimitives.WriteUInt16LittleEndian(parameters, 640);
        BinaryPrimitives.WriteUInt16LittleEndian(parameters.AsSpan(2), 256);
        BinaryPrimitives.WriteUInt16LittleEndian(parameters.AsSpan(4), 256);
        BinaryPrimitives.WriteUInt16LittleEndian(parameters.AsSpan(6), 0x13F6);
        return frame with { Arena = frame.Arena.Write(ParametersOffset, parameters) };
    }

    private static ushort Word(NativeBattleWaterExecutionFrame frame, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(offset, 2));

    private static byte[] Materialize(NativeBattleGfGeometryBatch batch)
    {
        byte[] bytes = Enumerable.Repeat((byte)0xA5, 0x6E5C).ToArray();
        foreach (NativeBattleGfGeometryAttempt attempt in batch.Attempts)
        {
            for (int index = 0; index < attempt.Data.Length; index++)
            {
                if (attempt.WriteMask[index] == NativeBattleGfPacketByteState.Known)
                {
                    bytes[attempt.ByteOffsetRelative + index] = attempt.Data[index];
                }
            }
        }
        return bytes;
    }

    [Fact]
    public void Presentation_preserves_full_native_ground_ledger_and_explicit_empty_depth_policy()
    {
        Capture plain = new() { CaptureWorld = true };
        Capture captured = new() { CaptureWorld = true };
        NativeBattleWaterExecutionFrame a = NativeBattleCinematicGroundProfile18.Draw(Create(), plain.Services());
        NativeBattleWaterExecutionFrame b = NativeBattleCinematicGroundProfile18.Draw(Create(), captured.Services(), new(233, 0x100, 0, 123, 18));
        Assert.Equal(NativeBattleTestSnapshots.Json(a), NativeBattleTestSnapshots.Json(b));
        Assert.Equal(NativeBattleTestSnapshots.Json(plain.Batch), NativeBattleTestSnapshots.Json(captured.Batch));
        Assert.Equal(plain.Inputs, captured.Inputs);
        NativeBattleGfGeometryAttempt[] packets = captured.Batch!.Attempts.Where(attempt => attempt.Primitive is not null).ToArray();
        Assert.Equal(496, packets.Length);
        Assert.Equal(496, packets.Select(attempt => attempt.Primitive!.PilotGeometry!.Identity).Distinct().Count());
        Assert.All(packets, attempt =>
        {
            NativeBattlePilotVisualGeometry geometry = attempt.Primitive!.PilotGeometry!;
            Assert.Equal((ushort)233, geometry.Identity.Effect);
            Assert.Equal(0.125f, geometry.ScreenScale);
            Assert.Empty(attempt.Primitive.VertexPayload4);
            Assert.True(geometry.UseSourceDepths);
            Assert.Equal(System.Numerics.Vector4.Zero, geometry.SourceDepths);
        });
    }

    private sealed class Capture
    {
        public NativeBattleGfGeometryBatch? Batch { get; private set; }
        public List<(short X, short Y, short Z)> Inputs { get; } = [];
        public List<bool> Padding { get; } = [];
        public int Resolves { get; private set; }
        public int Bases { get; private set; }
        public int Depth { get; init; } = 400;
        public bool CaptureWorld { get; init; }

        public NativeBattleCinematicGroundProfile18Services Services() => new(Resolve, Prepare, Project, Append);

        private uint Resolve(NativeBattleWaterExecutionFrame frame, short selector)
        {
            Assert.Equal(0x13F6, selector);
            Resolves++;
            return Address(0x2100);
        }

        private NativeBattleWaterExecutionFrame Prepare(NativeBattleWaterExecutionFrame frame)
        {
            Bases++;
            return frame;
        }

        private NativeBattleCinematicGroundProjection18 Project(NativeBattleWaterExecutionFrame frame)
        {
            short x = unchecked((short)frame.ReadTemporaryWord(0xD0));
            short y = unchecked((short)frame.ReadTemporaryWord(0xD2));
            short z = unchecked((short)frame.ReadTemporaryWord(0xD4));
            Inputs.Add((x, y, z));
            Padding.Add(frame.TemporaryFrame.IsKnown(0xD6, 1));
            uint screen = unchecked((uint)(ushort)x | ((uint)(ushort)y << 16));
            return new(frame, screen, Depth, 0x80000000) { PilotWorld = CaptureWorld ? new System.Numerics.Vector3(x, y, z) : null };
        }

        private NativeBattleWaterExecutionFrame Append(NativeBattleWaterExecutionFrame frame, uint output, uint ordering, NativeBattleGfGeometryBatch batch)
        {
            Batch = batch;
            return frame;
        }
    }
}
