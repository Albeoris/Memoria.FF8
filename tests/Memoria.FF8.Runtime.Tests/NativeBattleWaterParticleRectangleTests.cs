using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWaterParticleRectangleTests
{
    [Theory]
    [InlineData((ushort)231)]
    [InlineData((ushort)232)]
    [InlineData((ushort)238)]
    [InlineData((ushort)240)]
    public void Group17_direct_rectangle_capture_keeps_actual_particle_birth_and_native_FIFO_writes(ushort effect)
    {
        var nativeFrame = Fixture();
        var capturedFrame = Fixture();
        NativeBattlePilotVisualLifetimes.Allocated(capturedFrame.Arena, 0x2200000, 3);
        var identity = NativeBattlePilotVisualLifetimes.Identity(effect, capturedFrame.Arena, 0x2200000, 3);
        var projection = new NativeBattleCameraProjection(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096), 216, 0, 0);
        var native = NativeBattleWaterParticleRectanglePackets.DrawA9EE90(nativeFrame, Registers(Matrix), projection, 1365, 0, 0, Resolve);
        string baseline = NativeBattleTestSnapshots.Json(native);
        var captured = NativeBattleWaterParticleRectanglePackets.DrawA9EE90(capturedFrame, Registers(Matrix), projection, 1365, 0, 0, Resolve, Matrix, identity);
        Assert.Equal(baseline, NativeBattleTestSnapshots.Json(captured));
        Assert.Null(Assert.Single(native.Packets.Attempts).Primitive!.PilotGeometry);
        var primitive = Assert.Single(captured.Packets.Attempts).Primitive!;
        var geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(primitive.PilotGeometry);
        Assert.Equal(effect, geometry.Identity.Effect);
        Assert.Equal(identity.Generation, geometry.Identity.Generation);
        Assert.Equal(4, geometry.CornerCount);
        Assert.Equal(primitive.VertexPayload4, Assert.Single(native.Packets.Attempts).Primitive!.VertexPayload4);
        NativeBattlePilotVisualLifetimes.Allocated(capturedFrame.Arena, 0x2200000, 3);
        Assert.NotEqual(identity.Generation, NativeBattlePilotVisualLifetimes.Identity(effect, capturedFrame.Arena, 0x2200000, 3).Generation);
    }

    [Theory]
    [InlineData((ushort)219)]
    [InlineData((ushort)228)]
    [InlineData((ushort)236)]
    [InlineData((ushort)237)]
    [InlineData((ushort)243)]
    [InlineData((ushort)244)]
    [InlineData((ushort)245)]
    [InlineData((ushort)246)]
    [InlineData((ushort)247)]
    public void Selected_resource_changes_at_one_particle_birth_preserve_detached_native_state_and_previous_geometry(ushort effect)
    {
        NativeBattleWaterExecutionFrame nativeFrame = Fixture();
        NativeBattleWaterExecutionFrame capturedFrame = Fixture();
        NativeBattlePilotVisualLifetimes.Allocated(capturedFrame.Arena, 0x2200000, 3);
        NativeBattlePilotVisualIdentity owner = NativeBattlePilotVisualLifetimes.Identity(effect, capturedFrame.Arena, 0x2200000, 3);
        NativeBattleCameraProjection projection = Projection with { View = NativeBattleCuragaMatrices.Identity8DD770() };
        NativeBattleWaterParticleRectangleResult native = NativeBattleWaterParticleRectanglePackets.DrawA9EE90(nativeFrame, Registers(Matrix), projection, 1365, 0, 0, Resolve);
        string firstNative = NativeBattleTestSnapshots.Json(native);
        NativeBattleWaterParticleRectangleResult captured = NativeBattleWaterParticleRectanglePackets.DrawA9EE90(capturedFrame, Registers(Matrix), projection, 1365, 0, 0, Resolve, Matrix, owner);
        Assert.Equal(firstNative, NativeBattleTestSnapshots.Json(captured));
        NativeBattlePilotVisualGeometry previous = Assert.Single(captured.Packets.Attempts).Primitive!.PilotGeometry!;
        Assert.Equal((long)(ArenaBase + Source), previous.Identity.ResourceGeneration);
        native = NativeBattleWaterParticleRectanglePackets.DrawA9EE90(Relocate(native.Frame), Registers(Matrix), projection, 1365, 0, 0, Resolve);
        string nextNative = NativeBattleTestSnapshots.Json(native);
        captured = NativeBattleWaterParticleRectanglePackets.DrawA9EE90(Relocate(captured.Frame), Registers(Matrix), projection, 1365, 0, 0, Resolve, Matrix, owner);
        Assert.Equal(nextNative, NativeBattleTestSnapshots.Json(captured));
        NativeBattlePilotVisualGeometry replacement = Assert.Single(captured.Packets.Attempts).Primitive!.PilotGeometry!;
        Assert.Equal(previous.Identity.Generation, replacement.Identity.Generation);
        Assert.NotEqual(previous.Identity.ResourceGeneration, replacement.Identity.ResourceGeneration);
        Assert.Equal((long)(ArenaBase + Source), previous.Identity.ResourceGeneration);
        Assert.Equal(previous.A, replacement.A);
        Assert.Equal(previous.D, replacement.D);
        Assert.Equal(captured.Packets.Attempts[0].Primitive!.VertexPayload4, native.Packets.Attempts[0].Primitive!.VertexPayload4);

        static NativeBattleWaterExecutionFrame Relocate(NativeBattleWaterExecutionFrame frame)
        {
            (byte[] bytes, bool[] known) = frame.Arena.ReadSnapshot(Source, 24);
            NativeBattleEffectArena arena = frame.Arena.Write(Source + 0x1000, bytes);
            for (int index = 0; index < known.Length; index++)
            {
                if (!known[index])
                {
                    arena = arena.MarkUnknown(Source + 0x1000 + index, 1);
                }
            }
            return (frame with { Arena = arena }).WriteTemporaryDword(0x90, ArenaBase + Source + 0x1000);
        }
    }

    private const uint ArenaBase = 0x20DFAB8;
    private const uint Manager = 0x2797690;
    private const int Source = 0x10000;
    private const int Slot = 0x20000;
    private const uint Table = 0x1D8E058;
    private static readonly NativeBattleActionMatrix Matrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 3000, 4096);
    private static readonly NativeBattleCameraProjection Projection = new(null, 216, 0, 0);

    [Fact]
    public void Zero_count_writes_only_four_zero_words_and_preserves_unknown_scratch_and_cursor()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0);
        frame = UnknownTemporary(frame, 0xE0, 32);
        NativeBattleWaterParticleRectangleResult result = Draw(frame);
        Assert.Empty(result.Packets.Attempts);
        Assert.Equal(0, result.Packets.AllocationBytes);
        Assert.Equal(0x300000u, result.NextCursor);
        for (int offset = 0xE0; offset < 0x100; offset++)
        {
            bool written = (offset & 7) is 4 or 5;
            Assert.Equal(written, NativeBattleStorageSnapshots.Capture(result.Frame.TemporaryFrame).Known[offset]);
        }
        Assert.Equal(frame.Arena, result.Frame.Arena);
        Assert.Equal(frame.Random, result.Frame.Random);
    }

    [Fact]
    public void Negative_count_is_not_admitted_as_an_empty_draw()
    {
        Assert.Throws<NotSupportedException>(() => Draw(Fixture(-1)));
    }

    [Theory]
    [InlineData(0x20, 40, 9)]
    [InlineData(0, 44, 10)]
    public void Current_record_uv_carry_and_masks_survive_physical_append_as_one_link(ushort tpage, int length, byte tagLength)
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        frame = SourceWord(frame, 0x12, tpage);
        frame = UnknownTemporary(frame, 0x98, 4);
        NativeBattleWaterParticleRectangleResult result = Draw(frame);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Packets.Attempts);
        Assert.Equal(length, result.Packets.AllocationBytes);
        Assert.Equal(0x2E333333u, Dword(attempt.Data, 4));
        Assert.Equal((ushort)0x12F8, Word(attempt.Data, 0xC));
        Assert.Equal((ushort)0x1318, Word(attempt.Data, 0x14));
        Assert.Equal((ushort)0x16F8, Word(attempt.Data, 0x1C));
        Assert.Equal((ushort)0x1718, Word(attempt.Data, 0x24));
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(221);
        memory = memory with { Arena = memory.Arena!.Write(0x1000, Enumerable.Repeat((byte)0xA5, length).ToArray()) };
        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(memory, 0x1000, NativeBattleLogicalOrdering.Clear(), result.Packets);
        byte[] packet = written.Memory.Arena!.Read(0x1000, length);
        Assert.Equal(tagLength, packet[3]);
        Assert.Equal((ushort)0xA5A5, BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(0x1E)));
        Assert.Equal((ushort)0xA5A5, BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(0x26)));
        Assert.Equal(0x1000 + length, written.Cursor);
        Assert.Single(written.Ordering.Packets);
        if (length == 44)
        {
            Assert.Equal(0xE1000220u, BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(0x28)));
        }
    }

    [Theory]
    [InlineData(0x80, 0)]
    [InlineData(0x33, 8)]
    public void Explicit_color_branch_does_not_consume_slot_CC(byte selector, byte flags)
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        frame = frame with { Arena = frame.Arena.Write(Source + 4 + 0x10, [selector]).Write(Slot + 0xDE, [flags]).MarkUnknown(Slot + 0xCC, 4) };
        NativeBattleWaterParticleRectangleResult result = Draw(frame);
        Assert.Equal(0x2D123456u, Dword(Assert.Single(result.Packets.Attempts).Data, 4));
    }

    [Fact]
    public void Tpage_override_reads_current_frame_and_does_not_consume_original_record_tpage()
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        frame = frame with { Arena = frame.Arena.Write(Slot + 0xDE, [4]).MarkUnknown(Source + 4 + 0x12, 2) };
        frame = frame.WriteTemporaryDword(0x94, 0xABCD0020);
        Assert.Equal(40, Draw(frame).Packets.AllocationBytes);
    }

    [Fact]
    public void Negative_fractional_scale_uses_signed_truncation_and_preserves_scratch_padding()
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        frame = SourceWord(frame, 4, unchecked((ushort)-123));
        frame = SourceWord(frame, 6, unchecked((ushort)-257));
        foreach (int offset in new[] { 0xE6, 0xEE, 0xF6, 0xFE })
        {
            frame = UnknownTemporary(frame, offset, 2);
        }
        NativeBattleWaterParticleRectangleResult result = Draw(frame);
        // Width512 * -123 /4096 = -15; height64 * -257 /4096 = -4.
        Assert.Equal((ushort)248, result.Frame.ReadTemporaryWord(0xE8));
        Assert.Equal((ushort)44, result.Frame.ReadTemporaryWord(0xEA));
        Assert.Equal((ushort)263, result.Frame.ReadTemporaryWord(0xF0));
        Assert.Equal((ushort)40, result.Frame.ReadTemporaryWord(0xF2));
        foreach (int offset in new[] { 0xE6, 0xEE, 0xF6, 0xFE })
        {
            // E6 is overwritten by the fourth-depth DWORD; remaining padding is retained.
            Assert.Equal(offset == 0xE6, NativeBattleStorageSnapshots.Capture(result.Frame.TemporaryFrame).Known[offset]);
        }
    }

    [Theory]
    [InlineData(1365)]
    [InlineData(-32768)]
    public void Raw_AVSZ3_and_fourth_depth_drive_exact_signed_table_address_without_culling(short zsf3)
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        NativeBattleActionMatrix matrix = Matrix with { Z = 65535, X = 100000 };
        uint? observed = null;
        NativeBattleWaterParticleRectangleResult result = NativeBattleWaterParticleRectanglePackets.DrawA9EE90(frame, Registers(matrix), Projection, zsf3, short.MinValue, int.MinValue, address =>
        {
            observed = address;
            return 17;
        });
        uint first = unchecked(3u * 65535u * (uint)zsf3) >> 12;
        uint fourth = 65535u >> 2;
        int combined = unchecked((int)(first + fourth)) / 2;
        Assert.Equal(unchecked(Table + (uint)((combined >> 2) * 4)), observed);
        Assert.Equal(first, result.Frame.ReadTemporaryDword(0xE0));
        Assert.Equal(fourth, result.Frame.ReadTemporaryDword(0xE4));
        Assert.Equal(new[] { 65535, 65535, 65535, 65535 }, Assert.Single(result.Packets.Attempts).Primitive!.VertexPayload4);
        Assert.Equal(40, result.Packets.AllocationBytes);
    }

    [Fact]
    public void Mixed_record_lengths_share_current_cursor_and_common_physical_proxy_order()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(2);
        foreach ((int offset, int length) in new[] { (0, 8), (0xA, 7), (0x12, 2) })
        {
            byte[] consumed = frame.Arena.Read(Source + 4 + offset, length);
            frame = frame with { Arena = frame.Arena.Write(Source + 4 + 0x14 + offset, consumed) };
        }
        frame = frame with { Arena = frame.Arena.MarkUnknown(Source + 4 + 0x14 + 8, 2).MarkUnknown(Source + 4 + 0x14 + 0x11, 1).Write(Source + 4 + 0x14 + 0x12, [0, 0]) };
        NativeBattleWaterParticleRectangleResult result = Draw(frame);
        Assert.Equal(new[] { 0, 40 }, result.Packets.Attempts.Select(attempt => attempt.ByteOffsetRelative));
        Assert.Equal(84, result.Packets.AllocationBytes);
        Assert.Equal(0x300054u, result.NextCursor);
        NativeBattlePrimitiveProcess process = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400() with { PacketBank = "off_B6D070" };
        process = process.ApplyCommonWrite(0, Enumerable.Repeat((byte)0xBB, 84).ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 84).ToImmutableArray());
        NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(process, NativeBattleLogicalOrdering.Clear(), result.Packets);
        Assert.Equal(84, written.Process.ByteCursor);
        Assert.Equal(2, written.Ordering.Packets.Length);
        Assert.Equal(2, written.Ordering.Proxies.Length);
        Assert.Equal(new[] { 0, 40 }, written.Ordering.Packets.Select(packet => packet.Reference.ByteOffset));
        Assert.Equal(0xE1000220u, BinaryPrimitives.ReadUInt32LittleEndian(written.Process.ReadCommonPacket(80, 4)));
    }

    [Fact]
    public void Independent_JSON_next_uses_mutated_current_source_and_preserves_all_other_owners()
    {
        NativeBattleWaterParticleRectangleResult first = Draw(Fixture());
        NativeBattleWaterExecutionFrame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleWaterExecutionFrame>(NativeBattleTestSnapshots.Json(first.Frame))!;
        NativeBattleWaterExecutionFrame live = SourceWord(first.Frame, 0xA, 0xFFFE);
        restored = SourceWord(restored, 0xA, 0xFFFE);
        NativeBattleWaterParticleRectangleResult expected = Draw(live);
        NativeBattleWaterParticleRectangleResult actual = Draw(restored);
        Assert.Equal(NativeBattleTestSnapshots.Json(expected), NativeBattleTestSnapshots.Json(actual));
        Assert.Equal((ushort)0x1E, Word(Assert.Single(actual.Packets.Attempts).Data, 0x14));
        Assert.Equal(0x300050u, actual.NextCursor);
    }

    [Fact]
    public void Later_record_failure_does_not_publish_earlier_temporary_or_cursor_writes()
    {
        NativeBattleWaterExecutionFrame source = Fixture(2);
        string original = NativeBattleTestSnapshots.Json(source);
        int resolved = 0;
        Assert.Throws<NotSupportedException>(() => NativeBattleWaterParticleRectanglePackets.DrawA9EE90(source, Registers(Matrix), Projection, 1365, 0, 0, _ =>
        {
            resolved++;
            if (resolved == 2)
            {
                throw new NotSupportedException("Selected unresolved ordering bucket.");
            }
            return 17;
        }));
        Assert.Equal(2, resolved);
        Assert.Equal(original, NativeBattleTestSnapshots.Json(source));
        Assert.Equal(0x300000u, WaterSchedulerFixture.ReadCoreDword(source, WaterSchedulerFixture.ReadCoreDword(source, 0x27973EC) + 0x7C));
    }

    [Fact]
    public void Complete_leaf_keeps_explicit_history_immutable_while_current_aliases_advance_through_next_draw()
    {
        NativeBattleWaterExecutionFrame source = Fixture(2);
        NativeBattleWaterExecutionFrame original = NativeBattleStorageSnapshots.Capture(source);
        NativeBattleWaterTemporaryFrame workingAlias = source.TemporaryFrame;
        string originalJson = NativeBattleTestSnapshots.Json(original);
        NativeBattleWaterParticleRectangleResult first = Draw(source);
        NativeBattleWaterParticleRectangleResult firstHistory = NativeBattleStorageSnapshots.Capture(first);
        NativeBattleWaterExecutionFrame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleWaterExecutionFrame>(NativeBattleTestSnapshots.Json(first.Frame))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(Draw(first.Frame)), NativeBattleTestSnapshots.Json(Draw(restored)));
        Assert.Same(workingAlias, source.TemporaryFrame);
        Assert.Same(workingAlias, first.Frame.TemporaryFrame);
        Assert.Equal(originalJson, NativeBattleTestSnapshots.Json(original));
        Assert.NotEqual(NativeBattleTestSnapshots.Json(original.TemporaryFrame), NativeBattleTestSnapshots.Json(firstHistory.Frame.TemporaryFrame));
    }
    private static NativeBattleWaterParticleRectangleResult Draw(NativeBattleWaterExecutionFrame frame) => NativeBattleWaterParticleRectanglePackets.DrawA9EE90(frame, Registers(Matrix), Projection, 1365, 0, 0, Resolve);
    private static NativeBattleGeometryRegisterStep Registers(NativeBattleActionMatrix matrix) => new(NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), matrix, true, true, null), NativeBattleScriptedColorRegisters.Unknown());

    private static int Resolve(uint address) => 17;

    private static NativeBattleWaterExecutionFrame Fixture(int count = 1)
    {
        NativeBattleWaterExecutionFrame frame = new(NativeBattleScriptedEffectMemory.FromColdProcess(), NativeBattleEffectArena.Clear(), new(0x700000, new byte[0x180].ToImmutableArray(), Enumerable.Repeat(true, 0x180).ToImmutableArray()), new(7));
        frame = CoreDword(frame, 0x27973EC, Manager);
        frame = CoreDword(frame, 0x27973E8, ArenaBase + Slot);
        frame = CoreDword(frame, Manager + 0x7C, 0x300000);
        frame = frame.WriteTemporaryDword(0x90, ArenaBase + Source).WriteTemporaryDword(0x98, 0x01123456).WriteTemporaryDword(0x14C, Table);
        byte[] header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, count);
        frame = frame with { Arena = frame.Arena.Write(Source, header).Write(Slot + 0xCC, [0, 0, 0, 2]) };
        if (count > 0)
        {
            frame = SourceWord(frame, 0, 0);
            frame = SourceWord(frame, 2, 10);
            frame = SourceWord(frame, 4, 4096);
            frame = SourceWord(frame, 6, 4096);
            frame = SourceWord(frame, 0xA, 0x12F8);
            frame = SourceWord(frame, 0xC, 0x0420);
            frame = SourceWord(frame, 0xE, 0x1234);
            frame = SourceWord(frame, 0x12, 0x20);
            frame = frame with { Arena = frame.Arena.Write(Source + 4 + 0x10, [0x33]).MarkUnknown(Source + 4 + 8, 2).MarkUnknown(Source + 4 + 0x11, 1) };
        }
        return frame;
    }

    private static NativeBattleWaterExecutionFrame UnknownTemporary(NativeBattleWaterExecutionFrame frame, int offset, int length)
    {
        bool[] known = NativeBattleStorageSnapshots.Capture(frame.TemporaryFrame).Known.ToArray();
        Array.Fill(known, false, offset, length);
        return frame with { TemporaryFrame = frame.TemporaryFrame.SetKnownness(0, known) };
    }

    private static NativeBattleWaterExecutionFrame CoreDword(NativeBattleWaterExecutionFrame frame, uint address, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return frame with { Core = frame.Core.Write(address, bytes) };
    }

    private static NativeBattleWaterExecutionFrame SourceWord(NativeBattleWaterExecutionFrame frame, int offset, ushort value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return frame with { Arena = frame.Arena.Write(Source + 4 + offset, bytes) };
    }

    private static ushort Word(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan().Slice(offset));
    private static uint Dword(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset));
}
